using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MikroTek.Services.DTO;
using MikroTek.Services.Interface;

namespace MikroTek.Archiving.Areas.Portal.Helpers;

/// <summary>Role names for [Authorize(Roles = ...)].</summary>
public static class PortalRoles
{
    public const string Admin = "Admin";
    /// <summary>Can add and edit DVs, cash advances and payees.</summary>
    public const string Encoders = "Admin,Bookkeeper";
}

/// <summary>Builds the sign-in cookie's contents and re-checks the account on every request.</summary>
public static class PortalAuth
{
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string FullNameClaim = "full_name";
    public const string PositionClaim = "position";
    public const string DefaultPasswordClaim = "default_password";

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
        return new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
    }

    /// <summary>
    /// Runs on every request: a disabled or deleted account is signed out at once, and a changed
    /// name or role takes effect without the person having to sign in again.
    /// </summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var id = principal?.UserId() ?? 0;
        var user = id == 0 ? null : await context.HttpContext.RequestServices.GetRequiredService<IUsers>().GetAsync(id);

        if (principal is null || user is null || !user.ActiveStatus)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme);
            return;
        }

        if (user.Role.ToString() != principal.RoleName() || user.Name != principal.FullName()
            || user.Username != principal.Identity?.Name || (user.Position ?? "") != principal.FindFirstValue(PositionClaim))
        {
            context.ReplacePrincipal(CreatePrincipal(user, principal.UsingDefaultPassword()));
            context.ShouldRenew = true;
        }
    }
}

/// <summary>Shortcuts for views and controllers: User.FullName(), User.CanEncode(), ...</summary>
public static class PortalUserExtensions
{
    public static long UserId(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static string FullName(this ClaimsPrincipal user) => user.FindFirstValue(PortalAuth.FullNameClaim) ?? user.Identity?.Name ?? "";
    public static string FirstName(this ClaimsPrincipal user) => user.FullName().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    public static string RoleName(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? "";
    public static string Position(this ClaimsPrincipal user) => user.FindFirstValue(PortalAuth.PositionClaim) ?? "";
    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(PortalRoles.Admin);
    public static bool CanEncode(this ClaimsPrincipal user) => user.IsInRole("Admin") || user.IsInRole("Bookkeeper");
    public static bool UsingDefaultPassword(this ClaimsPrincipal user) => user.HasClaim(c => c.Type == PortalAuth.DefaultPasswordClaim);

    public static string Initials(this ClaimsPrincipal user) =>
        string.Concat(user.FullName().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpper(p[0])));
}
