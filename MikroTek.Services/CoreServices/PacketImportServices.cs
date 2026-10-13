using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Ocr;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    public class PacketImportServices : CoreServices, IPacketImport
    {
        private const string PacketFile = "packet", DraftFile = "draft.json";
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

        private readonly DocumentReader _reader;
        private readonly TesseractRunner _tesseract;
        private readonly UploadOptions _uploads;
        private readonly IUacsAccounts _accounts;
        private readonly IPayees _payees;
        private readonly ICashAdvances _advances;
        private readonly IDisbursements _vouchers;
        private readonly IArchiveFiles _files;
        private readonly ISchoolProfile _school;

        public PacketImportServices(DB db, Logger logger, DocumentReader reader, TesseractRunner tesseract, UploadOptions uploads,
            IUacsAccounts accounts, IPayees payees, ICashAdvances advances, IDisbursements vouchers, IArchiveFiles files,
            ISchoolProfile school) : base(db, logger)
        {
            _reader = reader;
            _tesseract = tesseract;
            _uploads = uploads;
            _accounts = accounts;
            _payees = payees;
            _advances = advances;
            _vouchers = vouchers;
            _files = files;
            _school = school;
        }

        private string ImportRoot => Path.Combine(_uploads.Folder, "import");

        private string? FolderOf(string token) =>
            Regex.IsMatch(token ?? "", "^[0-9a-f]{32}$") ? Path.Combine(ImportRoot, token!) : null;

        public async Task<ScanReaderStatus> GetStatusAsync()
        {
            var problem = await _tesseract.CheckAsync();
            return problem is null
                ? new ScanReaderStatus(true, "The scan reader (Tesseract) is ready.")
                : new ScanReaderStatus(false, problem);
        }

        public async Task<(ServiceResult Result, PacketDraft? Draft)> ReadAsync(string fileName, long sizeBytes, Stream content, long byUserId)
        {
            fileName = Path.GetFileName(fileName ?? "").Trim();
            if (fileName.Length == 0) fileName = "packet.pdf";
            if (sizeBytes <= 0) return (ServiceResult.Fail("Please choose the scanned packet (PDF, JPG or PNG)."), null);
            if (sizeBytes > _uploads.MaxFileBytes)
                return (ServiceResult.Fail($"The file is too big. The limit is {_uploads.MaxFileBytes / 1048576} MB."), null);

            using var memory = new MemoryStream();
            await content.CopyToAsync(memory);
            var bytes = memory.ToArray();
            if (!IsPdf(bytes) && !IsJpg(bytes) && !IsPng(bytes))
                return (ServiceResult.Fail($"\"{fileName}\" is not a PDF, JPG or PNG file."), null);

            var status = await GetStatusAsync();
            if (!status.Ready)
                return (ServiceResult.Fail("The scan reader cannot start on this server, so files cannot be read. " + status.Message), null);

            RemoveOldDrafts();
            if (!await ScanGate.One.WaitAsync(ScanGate.Wait))
                return (ServiceResult.Fail("The server is busy reading another packet. Please try again in a minute."), null);

            List<ReadPage> pages;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                pages = await _reader.ReadAsync(bytes, fileName);
            }
            catch (InvalidDataException ex)
            {
                return (ServiceResult.Fail(ex.Message), null);
            }
            catch (Exception ex)
            {
                _logger.WriteLog(Level.Error, LogType.Activity, $"Reading packet \"{fileName}\" failed: {ex}");
                return (ServiceResult.Fail("The file could not be read. Please check that it is a clear scan and try again."), null);
            }
            finally
            {
                ScanGate.One.Release();
            }

            var accounts = (await _accounts.GetAllAsync()).Where(a => a.IsActive)
                .Select(a => new PacketParser.Account(a.Code, a.Title)).ToList();
            var payees = (await _payees.GetAllAsync())
                .Select(p => new PacketParser.KnownPayee(p.Name, p.Tin, p.Address, p.TaxType)).ToList();
            var draft = PacketParser.Parse(pages, accounts, payees);
            draft.FileName = fileName;
            draft.Seconds = Math.Round(clock.Elapsed.TotalSeconds, 1);
            draft.Token = Guid.NewGuid().ToString("N");
            await FillFromRecordsAsync(draft);

            var folder = FolderOf(draft.Token)!;
            Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(Path.Combine(folder, PacketFile + Extension(bytes)), bytes);
            await File.WriteAllTextAsync(Path.Combine(folder, DraftFile), JsonSerializer.Serialize(draft, Json));

            _logger.WriteLog(Level.Info, LogType.Activity,
                $"User {byUserId} read packet \"{fileName}\": {draft.PageCount} pages, {draft.Vouchers.Count} DVs, {draft.Seconds}s.");
            return (ServiceResult.Ok(), draft);
        }

        /// <summary>What the archive already knows: a cash advance already recorded, DVs already encoded, the Cash Unit signatory.</summary>
        private async Task FillFromRecordsAsync(PacketDraft draft)
        {
            var ca = draft.CashAdvance;
            var existing = (await _advances.GetAllAsync())
                .FirstOrDefault(c => string.Equals(c.PayrollNo, ca.PayrollNo, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                ca.ExistingId = existing.Id;
                ca.Flags.RemoveAll(f => f.Field == "PayrollNo");
                ca.Flags.Add(new("PayrollNo", "check",
                    $"This cash advance is already recorded (batch {existing.BatchNo:00}, {existing.Amount:N2}). The DVs will be added to it."));
            }
            else
            {
                var school = await _school.GetAsync();
                if (string.IsNullOrWhiteSpace(ca.VerifiedBy)) ca.VerifiedBy = school.CashUnitName;
                if (string.IsNullOrWhiteSpace(ca.VerifiedByPosition)) ca.VerifiedByPosition = school.CashUnitPosition;
                if (string.IsNullOrWhiteSpace(ca.FundCluster)) ca.FundCluster = school.FundCluster;
            }

            var encoded = (await _vouchers.GetAllAsync()).Select(v => v.DvNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var dv in draft.Vouchers.Where(v => v.DvNo is not null && encoded.Contains(v.DvNo)))
            {
                dv.Include = false;
                dv.Flags.Insert(0, new("DvNo", "check", $"{dv.DvNo} is already in the archive, so it is left out."));
            }
        }

        public async Task<PacketDraft?> GetDraftAsync(string token)
        {
            var folder = FolderOf(token);
            var path = folder is null ? null : Path.Combine(folder, DraftFile);
            if (path is null || !File.Exists(path)) return null;
            return JsonSerializer.Deserialize<PacketDraft>(await File.ReadAllTextAsync(path), Json);
        }

        public string? GetPacketPath(string token)
        {
            var folder = FolderOf(token);
            return folder is null || !Directory.Exists(folder)
                ? null
                : Directory.GetFiles(folder, PacketFile + ".*").FirstOrDefault();
        }

        public async Task<ServiceResult> SaveAsync(PacketDraft draft, long byUserId)
        {
            var packet = GetPacketPath(draft.Token);
            var original = await GetDraftAsync(draft.Token);
            if (packet is null || original is null)
                return ServiceResult.Fail("This packet is no longer waiting for review (it was saved, discarded or is more than 2 days old). Please upload it again.");

            var chosen = draft.Vouchers.Where(v => v.Include).ToList();
            if (chosen.Count == 0 && draft.CashAdvance.ExistingId != 0)
                return ServiceResult.Fail("No DV is ticked, so there is nothing to save.");
            var dupes = chosen.Where(v => !string.IsNullOrWhiteSpace(v.DvNo)).GroupBy(v => v.DvNo!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0) return ServiceResult.Fail($"DV number {dupes[0]} appears twice. Please correct one of them.");

            var ca = draft.CashAdvance;
            var strategy = db.Database.CreateExecutionStrategy();
            ServiceResult result = ServiceResult.Fail("Nothing was saved.");
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                result = await SaveAllAsync(ca, chosen, byUserId);
                if (result.Success) await transaction.CommitAsync();
                else
                {
                    await transaction.RollbackAsync();
                    db.ChangeTracker.Clear();
                }
            });
            if (!result.Success) return result;

            // The packet becomes one of the cash advance's documents.
            var caId = result.Id;
            await using (var stream = File.OpenRead(packet))
            {
                var stored = await _files.AddAsync(FileOwner.CashAdvance, caId, original.FileName, stream.Length, stream, byUserId);
                if (!stored.Success) _logger.WriteLog(Level.Error, LogType.Activity, $"Packet \"{original.FileName}\" could not be attached: {stored.Error}");
            }
            Discard(draft.Token);

            _logger.WriteLog(Level.Info, LogType.Activity,
                $"User {byUserId} imported packet \"{original.FileName}\": cash advance {ca.PayrollNo}, {chosen.Count} DVs.");
            return ServiceResult.Ok(caId);
        }

        private async Task<ServiceResult> SaveAllAsync(PacketCashAdvance ca, List<PacketVoucher> chosen, long byUserId)
        {
            long caId = ca.ExistingId;
            if (caId == 0)
            {
                var saved = await _advances.SaveAsync(new SaveCashAdvanceRequest
                {
                    Dept = ca.Dept, Fund = ca.Fund, BatchNo = ca.BatchNo, PayrollNo = ca.PayrollNo, AdaNo = ca.AdaNo, Date = ca.Date,
                    Amount = ca.Amount, Period = ca.Period, FundCluster = ca.FundCluster, Officer = ca.Officer,
                    OfficerPosition = ca.OfficerPosition, VerifiedBy = ca.VerifiedBy, VerifiedByPosition = ca.VerifiedByPosition
                }, byUserId);
                if (!saved.Success) return ServiceResult.Fail("Cash advance: " + saved.Error);
                caId = saved.Id;
            }
            else if (await _advances.GetAsync(caId) is null)
                return ServiceResult.Fail("The cash advance these DVs belong to no longer exists. Please upload the packet again.");

            var school = await _school.GetAsync();
            var bank = school.BankFor(ca.Dept.ToString());
            foreach (var dv in chosen)
            {
                var lines = dv.Lines.Where(l => !string.IsNullOrWhiteSpace(l.Code) || (l.Amount ?? 0) != 0).ToList();
                var gross = lines.Sum(l => l.Amount ?? 0);
                var saved = await _vouchers.SaveAsync(new SaveDvRequest
                {
                    CashAdvanceId = caId,
                    DvNo = dv.DvNo,
                    Date = dv.Date,
                    FundCluster = ca.FundCluster ?? school.FundCluster,
                    ResponsibilityCenter = school.ResponsibilityCenter,
                    MfoPap = "MOOE",
                    Payee = dv.Payee,
                    Tin = dv.Tin,
                    Address = dv.Address,
                    Lines = lines.Select(l => new DvLineInput { Code = l.Code?.Trim(), Amount = l.Amount }).ToList(),
                    PayeeTaxType = dv.PayeeTaxType,
                    // The taxes as the bookkeeper withheld them (from the register), not recomputed.
                    ManualTax = true,
                    TaxBase = dv.PayeeTaxType == PayeeTaxType.Vat ? dv.TaxBase ?? TaxRules.Compute(gross, PayeeTaxType.Vat).TaxBase : gross,
                    Tax1 = dv.Tax1 ?? 0,
                    Tax3 = dv.Tax3 ?? 0,
                    Tax5 = dv.Tax5 ?? 0,
                    Mode = "Check",
                    CheckNo = dv.CheckNo,
                    PaidDate = dv.Date,
                    Bank = bank
                }, byUserId);
                if (!saved.Success) return ServiceResult.Fail($"{(string.IsNullOrWhiteSpace(dv.DvNo) ? "A DV" : dv.DvNo)}: {saved.Error}");
            }
            return ServiceResult.Ok(caId);
        }

        public void Discard(string token)
        {
            var folder = FolderOf(token);
            try { if (folder is not null && Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
            catch (IOException) { /* removed with the old drafts later */ }
        }

        /// <summary>Packets read but never saved are removed after 2 days.</summary>
        private void RemoveOldDrafts()
        {
            if (!Directory.Exists(ImportRoot)) return;
            foreach (var folder in Directory.GetDirectories(ImportRoot))
                try
                {
                    if (Directory.GetLastWriteTime(folder) < DateTime.Now.AddDays(-2)) Directory.Delete(folder, recursive: true);
                }
                catch (IOException) { }
        }

        private static bool IsPdf(byte[] b) => b.Length > 4 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F';
        private static bool IsJpg(byte[] b) => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;
        private static bool IsPng(byte[] b) => b.Length > 8 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G';
        private static string Extension(byte[] b) => IsPdf(b) ? ".pdf" : IsPng(b) ? ".png" : ".jpg";
    }
}
