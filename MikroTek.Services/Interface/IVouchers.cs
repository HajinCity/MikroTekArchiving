using MikroTek.Domain;
using MikroTek.Services.DTO;

namespace MikroTek.Services.Interface
{
    
    public interface IVouchers
    {
        Task<LoginResult> LoginAsync(string? rawCode, HotspotClient client);
        Task<SessionStatus?> GetStatusAsync(HotspotClient client);
        Task<LogoutSummary?> LogoutAsync(HotspotClient client);       
        Task<int> ExpireDueAsync();
        Task<List<Vouchers>> GenerateAsync(long planId, int quantity, string createdBy, int codeLength = 8);
    }
}
