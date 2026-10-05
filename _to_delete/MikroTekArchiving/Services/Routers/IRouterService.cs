namespace MikroTekArchiving.Services.Routers;
public record HotspotClient(string MacAddress, string IpAddress);


public interface IRouterService{    
    HotspotClient GetClient(HttpContext httpContext);  
    Task GrantAccessAsync(HotspotClient client, string voucherCode, DateTime expiresAt);
    Task RevokeAccessAsync(string macAddress);
    Task<IReadOnlyList<RouterActiveUser>> GetActiveUsersAsync();
}
public record RouterActiveUser(string MacAddress, string IpAddress, string VoucherCode,
                               DateTime LoginAt, DateTime ExpiresAt);
