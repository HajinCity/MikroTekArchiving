using System.Text.RegularExpressions;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DAL;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    public class PayeeServices : CoreServices, IPayees
    {
        private readonly PayeesQueries _payees;

        public PayeeServices(DB db, Logger logger, PayeesQueries payees) : base(db, logger)
        {
            _payees = payees;
        }

        public async Task<List<PayeeDto>> GetAllAsync()
        {
            var counts = await _payees.CountDvsByPayeeAsync();
            return (await _payees.GetAllSortedAsync())
                .Select(p => new PayeeDto(p.Id, p.Name, p.Tin ?? "", p.Address ?? "", p.Kind, p.TaxType, p.IsActive,
                                          counts.GetValueOrDefault(p.Name))).ToList();
        }

        public async Task<ServiceResult> SaveAsync(SavePayeeRequest r, long byUserId)
        {
            var name = Clean(r.Name);
            var tin = Clean(r.Tin);
            var address = Clean(r.Address);

            if (name.Length == 0 || name.Length > 200)
                return ServiceResult.Fail("Please enter the payee's name (up to 200 characters).");
            if (tin.Length > 30) return ServiceResult.Fail("The TIN is too long (up to 30 characters).");
            if (address.Length > 250) return ServiceResult.Fail("The address is too long (up to 250 characters).");
            if (!Enum.IsDefined(r.Kind) || !Enum.IsDefined(r.TaxType))
                return ServiceResult.Fail("Please choose the kind of payee and the tax type.");
            if (await _payees.NameExistsAsync(name, r.Id))
                return ServiceResult.Fail($"\"{name}\" is already in the payee list.");

            _payees.UserId = byUserId;
            if (r.Id == 0)
            {
                var id = await _payees.CreateAsync(new Payees
                    { Name = name, Tin = Null(tin), Address = Null(address), Kind = r.Kind, TaxType = r.TaxType, IsActive = r.IsActive });
                _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} added payee \"{name}\".");
                return ServiceResult.Ok(id);
            }

            var payee = await _payees.GetForEditAsync(r.Id);
            if (payee is null) return ServiceResult.Fail("That payee no longer exists.");

            payee.Name = name;
            payee.Tin = Null(tin);
            payee.Address = Null(address);
            payee.Kind = r.Kind;
            payee.TaxType = r.TaxType;
            payee.IsActive = r.IsActive;
            await _payees.SaveChangesAsync();
            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} updated payee \"{name}\".");
            return ServiceResult.Ok(payee.Id);
        }

        private static string Clean(string? text) => Regex.Replace((text ?? "").Trim(), @"\s+", " ");
        private static string? Null(string text) => text.Length == 0 ? null : text;
    }
}
