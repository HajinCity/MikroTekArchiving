using MikroTek.Domain.Custom;

namespace MikroTek.Services.Interface
{
   
    public interface IGenericQuery<T> where T : BaseEntity, new()
    {       
        T? SelectById(long id);     
        long Create(IEnumerable<T> datas);
        long Create(T data);
        int DeleteById(long id);
        bool Delete(T data);
        bool Update(IEnumerable<T> datas);
        bool Update(T data);
        Task<T?> SelectByIdAsync(long id, CancellationToken ct = default);
        Task<long> CreateAsync(IEnumerable<T> datas, CancellationToken ct = default);
        Task<long> CreateAsync(T data, CancellationToken ct = default);
        Task<int> DeleteByIdAsync(long id, CancellationToken ct = default);
        Task<bool> DeleteAsync(T data, CancellationToken ct = default);
        Task<bool> UpdateAsync(IEnumerable<T> datas, CancellationToken ct = default);
        Task<bool> UpdateAsync(T data, CancellationToken ct = default);
    }
}
