using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MikroTekArchiving.Data;
using MikroTekArchiving.Models.Entities;
using MikroTekArchiving.Services.Routers;

namespace MikroTekArchiving.Services;

public record LoginResult(bool Success, string? Error = null);

/// <summary>What the Status page shows for a logged-in device.</summary>
public record SessionStatus(
    string VoucherCode, string PlanName, string MacAddress, string IpAddress,
    DateTime LoginAt, DateTime ExpiresAt)
{
    public TimeSpan Uptime => DateTime.Now - LoginAt;
    public TimeSpan TimeLeft => ExpiresAt > DateTime.Now ? ExpiresAt - DateTime.Now : TimeSpan.Zero;
}

/// <summary>What the Logout page shows after a device logs off.</summary>
public record LogoutSummary(
    string VoucherCode, string MacAddress, string IpAddress, TimeSpan SessionTime, TimeSpan TimeLeft);

/// <summary>
/// All the voucher rules live here (not in the controllers):
/// continuous time, one device per voucher, expiry, and code generation.
/// </summary>
public class VoucherService(AppDbContext db, IRouterService router)
{
    // No 0/O, 1/I/L so codes are easy to read off a printed slip.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string NormalizeCode(string? code) =>
        (code ?? "").Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();

    public async Task<LoginResult> LoginAsync(string? rawCode, HotspotClient client)
    {
        var code = NormalizeCode(rawCode);
        if (code.Length == 0)
            return new LoginResult(false, "Please enter your voucher code.");

        var voucher = await db.Vouchers.Include(v => v.Plan).FirstOrDefaultAsync(v => v.Code == code);
        var now = DateTime.Now;

        if (voucher is null)
            return new LoginResult(false, "Invalid voucher code.");

        if (voucher.Status == VoucherStatus.Disabled)
            return new LoginResult(false, "This voucher has been disabled. Please contact the office.");

        if (voucher.Status == VoucherStatus.Expired || (voucher.ExpiresAt is { } exp && exp <= now))
        {
            voucher.Status = VoucherStatus.Expired;
            await db.SaveChangesAsync();
            return new LoginResult(false, "This voucher has already expired.");
        }

        if (voucher.Status == VoucherStatus.Active && voucher.MacAddress != client.MacAddress)
            return new LoginResult(false, "This voucher is already being used on another device.");

        // First use: start the clock and lock the voucher to this device.
        if (voucher.Status == VoucherStatus.Unused)
        {
            voucher.Status = VoucherStatus.Active;
            voucher.FirstUsedAt = now;
            voucher.ExpiresAt = now.AddMinutes(voucher.Plan.DurationMinutes);
            voucher.MacAddress = client.MacAddress;
        }

        // A device can only have one open session. Close any old one (e.g. a different voucher).
        await CloseOpenSessionsAsync(client.MacAddress, "Replaced by new login");

        db.HotspotSessions.Add(new HotspotSession
        {
            Voucher = voucher,
            MacAddress = client.MacAddress,
            IpAddress = client.IpAddress,
            LoginAt = now
        });
        await db.SaveChangesAsync();

        await router.GrantAccessAsync(client, voucher.Code, voucher.ExpiresAt!.Value);
        return new LoginResult(true);
    }

    /// <summary>Returns the device's current session, or null if it is not logged in.</summary>
    public async Task<SessionStatus?> GetStatusAsync(HotspotClient client)
    {
        var session = await db.HotspotSessions
            .Include(s => s.Voucher).ThenInclude(v => v.Plan)
            .Where(s => s.MacAddress == client.MacAddress && s.LogoutAt == null)
            .OrderByDescending(s => s.LoginAt)
            .FirstOrDefaultAsync();

        if (session is null) return null;

        if (session.Voucher.ExpiresAt is null || session.Voucher.ExpiresAt <= DateTime.Now
            || session.Voucher.Status != VoucherStatus.Active)
        {
            await ExpireDueAsync(); // time ran out between background checks
            return null;
        }

        return new SessionStatus(session.Voucher.Code, session.Voucher.Plan.Name,
            session.MacAddress, session.IpAddress, session.LoginAt, session.Voucher.ExpiresAt.Value);
    }

    public async Task<LogoutSummary?> LogoutAsync(HotspotClient client)
    {
        var session = await db.HotspotSessions.Include(s => s.Voucher)
            .Where(s => s.MacAddress == client.MacAddress && s.LogoutAt == null)
            .OrderByDescending(s => s.LoginAt)
            .FirstOrDefaultAsync();

        if (session is null) return null;

        var now = DateTime.Now;
        session.LogoutAt = now;
        session.EndReason = "Logout";
        await db.SaveChangesAsync();
        await router.RevokeAccessAsync(client.MacAddress);

        // Continuous time: the voucher keeps counting down even while logged out.
        var left = session.Voucher.ExpiresAt is { } exp && exp > now ? exp - now : TimeSpan.Zero;
        return new LogoutSummary(session.Voucher.Code, session.MacAddress, session.IpAddress,
            now - session.LoginAt, left);
    }

    /// <summary>
    /// Marks vouchers whose time is up as Expired and kicks their devices off.
    /// Called every 30 seconds by VoucherExpiryWorker.
    /// </summary>
    public async Task<int> ExpireDueAsync()
    {
        var now = DateTime.Now;
        var due = await db.Vouchers
            .Include(v => v.Sessions.Where(s => s.LogoutAt == null))
            .Where(v => v.Status == VoucherStatus.Active && v.ExpiresAt <= now)
            .ToListAsync();

        foreach (var v in due)
        {
            v.Status = VoucherStatus.Expired;
            foreach (var s in v.Sessions)
            {
                s.LogoutAt = v.ExpiresAt;
                s.EndReason = "Expired";
                await router.RevokeAccessAsync(s.MacAddress);
            }
        }

        // Sessions of vouchers an admin disabled (in case they are still open).
        var orphaned = await db.HotspotSessions
            .Where(s => s.LogoutAt == null && s.Voucher.Status != VoucherStatus.Active)
            .ToListAsync();
        foreach (var s in orphaned)
        {
            s.LogoutAt = now;
            s.EndReason ??= "Voucher no longer active";
            await router.RevokeAccessAsync(s.MacAddress);
        }

        await db.SaveChangesAsync();
        return due.Count;
    }

    /// <summary>Creates <paramref name="quantity"/> new unused vouchers for a plan.</summary>
    public async Task<List<Voucher>> GenerateAsync(int planId, int quantity, string createdBy, int codeLength = 8)
    {
        var plan = await db.Plans.FindAsync(planId) ?? throw new ArgumentException("Plan not found.");
        var batchNo = $"B{DateTime.Now:yyyyMMdd-HHmmss}";

        var existing = new HashSet<string>(await db.Vouchers.Select(v => v.Code).ToListAsync());
        var created = new List<Voucher>();
        while (created.Count < quantity)
        {
            var code = RandomCode(codeLength);
            if (!existing.Add(code)) continue; // already taken, try again
            created.Add(new Voucher { Code = code, Plan = plan, BatchNo = batchNo, CreatedBy = createdBy });
        }

        db.Vouchers.AddRange(created);
        await db.SaveChangesAsync();
        return created;
    }

    private async Task CloseOpenSessionsAsync(string mac, string reason)
    {
        var open = await db.HotspotSessions.Where(s => s.MacAddress == mac && s.LogoutAt == null).ToListAsync();
        foreach (var s in open)
        {
            s.LogoutAt = DateTime.Now;
            s.EndReason = reason;
        }
    }

    private static string RandomCode(int length) =>
        new(Enumerable.Range(0, length)
            .Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]).ToArray());
}
