using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MikroTek.Domain;
using MikroTek.Services.DTO;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Helpers;

/// <summary>Role names for [Authorize(Roles = ...)]. Most pages use [Permission(...)] instead.</summary>
public static class PortalRoles
{
    public const string Admin = "Admin";
}

/// <summary>
/// Put on a controller or action: only roles with this feature ticked on the
/// Roles &amp; Access page can open it. Everyone else is sent to the "No access" page.
///     [Permission(Permissions.VouchersEncode)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public string Permission { get; } = permission;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) context.Result = new ChallengeResult();
        else if (!user.Can(Permission)) context.Result = new ForbidResult();
    }
}

/// <summary>Builds the sign-in cookie's contents and re-checks the account on every request.</summary>
public static class PortalAuth
{
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string FullNameClaim = "full_name";
    public const string PositionClaim = "position";
    public const string DefaultPasswordClaim = "default_password";
    public const string TemporaryPasswordClaim = "temporary_password";
    public const string PermissionClaim = "permission";

    public static ClaimsPrincipal CreatePrincipal(UserDto user, bool usingDefaultPassword = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(FullNameClaim, user.Name),
            new(PositionClaim, user.Position ?? "")
        };
        if (usingDefaultPassword) claims.Add(new Claim(DefaultPasswordClaim, "1"));
        if (user.MustChangePassword) claims.Add(new Claim(TemporaryPasswordClaim, "1"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
    }

    /// <summary>
    /// Runs on every request: a disabled or deleted account is signed out at once, and a changed
    /// name, role or access takes effect without the person having to sign in again.
    /// </summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var services = context.HttpContext.RequestServices;
        var principal = context.Principal;
        var id = principal?.UserId() ?? 0;
        var user = id == 0 ? null : await services.GetRequiredService<IUsers>().GetAsync(id);

        if (principal is null || user is null || !user.ActiveStatus)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme);
            return;
        }

        if (user.Role.ToString() != principal.RoleName() || user.Name != principal.FullName()
            || user.Username != principal.Identity?.Name || (user.Position ?? "") != principal.FindFirstValue(PositionClaim)
            || user.MustChangePassword != principal.HasTemporaryPassword())
        {
            principal = CreatePrincipal(user, principal.UsingDefaultPassword());
            context.ShouldRenew = true;
        }

        // What this role may do is read fresh on every request (never trusted from the cookie),
        // so a change on the Roles & Access page applies to everyone right away.
        var account = principal.Identities.First(i => i.AuthenticationType == Scheme);
        var allowed = await services.GetRequiredService<IRoles>().GetPermissionsAsync(user.Role);
        var access = new ClaimsIdentity(allowed.Select(p => new Claim(PermissionClaim, p)));
        context.ReplacePrincipal(new ClaimsPrincipal([account, access]));
    }
}

/// <summary>Shortcuts for views and controllers: User.FullName(), User.Can(...), ...</summary>
public static class PortalUserExtensions
{
    public static long UserId(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static string FullName(this ClaimsPrincipal user) => user.FindFirstValue(PortalAuth.FullNameClaim) ?? user.Identity?.Name ?? "";
    public static string FirstName(this ClaimsPrincipal user) => user.FullName().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    public static string RoleName(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? "";
    /// <summary>The role as people read it: "System Admin", "Disbursement Officer", ...</summary>
    public static string RoleDisplay(this ClaimsPrincipal user) => RoleNames.Display(user.RoleName());
    public static string Position(this ClaimsPrincipal user) => user.FindFirstValue(PortalAuth.PositionClaim) ?? "";
    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(PortalRoles.Admin);

    /// <summary>True if this person's role has the feature ticked, e.g. User.Can(Permissions.VouchersEncode).</summary>
    public static bool Can(this ClaimsPrincipal user, string permission) => user.HasClaim(PortalAuth.PermissionClaim, permission);
    public static bool CanAny(this ClaimsPrincipal user, params string[] permissions) => permissions.Any(user.Can);

    public static bool UsingDefaultPassword(this ClaimsPrincipal user) => user.HasClaim(c => c.Type == PortalAuth.DefaultPasswordClaim);
    /// <summary>True while the password is one an admin gave: the person must choose their own first.</summary>
    public static bool HasTemporaryPassword(this ClaimsPrincipal user) => user.HasClaim(c => c.Type == PortalAuth.TemporaryPasswordClaim);

    public static string Initials(this ClaimsPrincipal user) =>
        string.Concat(user.FullName().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpper(p[0])));

    /// <summary>The first page this person is allowed to open (used after sign-in and by "Back" links).</summary>
    public static (string Controller, string Action) HomePage(this ClaimsPrincipal user) =>
        user.Can(Permissions.DashboardView) ? ("Dashboard", "Index")
        : user.Can(Permissions.CashAdvancesView) ? ("CashAdvances", "Index")
        : user.Can(Permissions.VouchersView) ? ("Vouchers", "Index")
        : user.Can(Permissions.ReportsView) ? ("Reports", "Index")
        : ("Settings", "Account");
}
