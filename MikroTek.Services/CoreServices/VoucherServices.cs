using System.Security.Cryptography;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DAL;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    /// <summary>
    /// All the voucher rules live here (not in the controllers):
    /// continuous time, one device per voucher, expiry, and code generation.
    /// </summary>
    public class VoucherServices : CoreServices, IVouchers
    {
        // No 0/O, 1/I/L so codes are easy to read off a printed slip.
        private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        private readonly VouchersQueries _vouchers;
        private readonly HotspotSessionsQueries _sessions;
        private readonly PlansQueries _plans;
        private readonly IRouterService _router;

        public VoucherServices(DB db, Logger logger, VouchersQueries vouchers, HotspotSessionsQueries sessions,
                               PlansQueries plans, IRouterService router) : base(db, logger)
        {
            _vouchers = vouchers;
            _sessions = sessions;
            _plans = plans;
            _router = router;
        }

        public static string NormalizeCode(string? code) =>
            (code ?? "").Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();

        public async Task<LoginResult> LoginAsync(string? rawCode, HotspotClient client)
        {
            var code = NormalizeCode(rawCode);
            if (code.Length == 0)
                return new LoginResult(false, "Please enter your voucher code.");

            var voucher = await _vouchers.GetByCodeWithPlanAsync(code);
            var now = DateTime.Now;

            if (voucher is null)
                return new LoginResult(false, "Invalid voucher code.");

            if (voucher.Status == VoucherStatus.Disabled)
                return new LoginResult(false, "This voucher has been disabled. Please contact the office.");

            if (voucher.Status == VoucherStatus.Expired || (voucher.ExpiresAt is { } exp && exp <= now))
            {
                voucher.Status = VoucherStatus.Expired;
                await _vouchers.SaveChangesAsync();
                return new LoginResult(false, "This voucher has already expired.");
            }

            if (voucher.Status == VoucherStatus.Active && voucher.MacAddress != client.MacAddress)
                return new LoginResult(false, "This voucher is already being used on another device.");

            // First use: start the clock and lock the voucher to this device.
            if (voucher.Status == VoucherStatus.Unused)
            {
                voucher.Status = VoucherStatus.Active;
                voucher.FirstUsedAt = now;
                voucher.ExpiresAt = now.AddMinutes(voucher.Plan.Duration);
                voucher.MacAddress = client.MacAddress;
            }

            // A device can only have one open session. Close any old one (e.g. a different voucher).
            foreach (var old in await _sessions.GetOpenByMacAsync(client.MacAddress))
            {
                old.LogoutAt = now;
                old.EndReason = "Replaced by new login";
            }

            _sessions.Add(new HotspotSessions
            {
                Voucher = voucher,
                MacAddress = client.MacAddress,
                IpAddress = client.IpAddress,
                LoginAt = now
            });
            await _sessions.SaveChangesAsync();

            await _router.GrantAccessAsync(client, voucher.VoucherCode, voucher.ExpiresAt!.Value);
            _logger.WriteLog(Level.Info, LogType.Activity, $"Voucher {voucher.VoucherCode} logged in on {client.MacAddress}");
            return new LoginResult(true);
        }

        /// <summary>Returns the device's current session, or null if it is not logged in.</summary>
        public async Task<SessionStatus?> GetStatusAsync(HotspotClient client)
        {
            var session = await _sessions.GetOpenSessionAsync(client.MacAddress);
            if (session is null) return null;

            if (session.Voucher.ExpiresAt is null || session.Voucher.ExpiresAt <= DateTime.Now
                || session.Voucher.Status != VoucherStatus.Active)
            {
                await ExpireDueAsync(); // time ran out between background checks
                return null;
            }

            return new SessionStatus(session.Voucher.VoucherCode, session.Voucher.Plan.Name,
                session.MacAddress, session.IpAddress, session.LoginAt, session.Voucher.ExpiresAt.Value);
        }

        public async Task<LogoutSummary?> LogoutAsync(HotspotClient client)
        {
            var session = await _sessions.GetOpenSessionAsync(client.MacAddress);
            if (session is null) return null;

            var now = DateTime.Now;
            session.LogoutAt = now;
            session.EndReason = "Logout";
            await _sessions.SaveChangesAsync();
            await _router.RevokeAccessAsync(client.MacAddress);

            var left = session.Voucher.ExpiresAt is { } exp && exp > now ? exp - now : TimeSpan.Zero;
            return new LogoutSummary(session.Voucher.VoucherCode, session.MacAddress, session.IpAddress,
                now - session.LoginAt, left);
        }

        public async Task<int> ExpireDueAsync()
        {
            var now = DateTime.Now;
            var due = await _vouchers.GetDueForExpiryAsync(now);

            foreach (var v in due)
            {
                v.Status = VoucherStatus.Expired;
                foreach (var s in v.Sessions)
                {
                    s.LogoutAt = v.ExpiresAt;
                    s.EndReason = "Expired";
                    await _router.RevokeAccessAsync(s.MacAddress);
                }
            }

          
            foreach (var s in await _sessions.GetOrphanedOpenAsync())
            {
                s.LogoutAt = now;
                s.EndReason ??= "Voucher no longer active";
                await _router.RevokeAccessAsync(s.MacAddress);
            }

            await _vouchers.SaveChangesAsync();
            return due.Count;
        }

        /// <summary>Creates <paramref name="quantity"/> new unused vouchers for a plan.</summary>
        public async Task<List<Vouchers>> GenerateAsync(long planId, int quantity, string createdBy, int codeLength = 8)
        {
            var plan = await _plans.GetForEditAsync(planId) ?? throw new ArgumentException("Plan not found.");
            var batchNo = $"B{DateTime.Now:yyyyMMdd-HHmmss}";

            var existing = await _vouchers.GetAllCodesAsync();
            var created = new List<Vouchers>();
            while (created.Count < quantity)
            {
                var code = RandomCode(codeLength);
                if (!existing.Add(code)) continue; // already taken, try again
                created.Add(new Vouchers { VoucherCode = code, Plan = plan, BatchNo = batchNo, CreatedBy = createdBy });
            }

            await _vouchers.CreateAsync(created);
            return created;
        }

        private static string RandomCode(int length) =>
            new(Enumerable.Range(0, length)
                .Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]).ToArray());
    }
}
