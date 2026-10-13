using System.Text.Json;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Ocr;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    public class DvScanServices : CoreServices, IDvScan
    {
        /// <summary>Changed when the reader reads differently, so earlier readings are read again.</summary>
        private const string ReaderVersion = "3";

        private readonly DvScanReader _reader;
        private readonly TesseractRunner _tesseract;
        private readonly UploadOptions _uploads;
        private readonly IArchiveFiles _files;
        private readonly IUacsAccounts _accounts;
        private readonly IPayees _payees;
        private readonly IChecklistTemplates _templates;

        public DvScanServices(DB db, Logger logger, DvScanReader reader, TesseractRunner tesseract, UploadOptions uploads,
            IArchiveFiles files, IUacsAccounts accounts, IPayees payees, IChecklistTemplates templates) : base(db, logger)
        {
            _reader = reader;
            _tesseract = tesseract;
            _uploads = uploads;
            _files = files;
            _accounts = accounts;
            _payees = payees;
            _templates = templates;
        }

        public async Task<DvScanResult> ReadAsync(long voucherId, bool again, long byUserId)
        {
            var scans = await _files.GetForOwnerAsync(FileOwner.Voucher, voucherId);
            if (scans.Count == 0) return new DvScanResult { Message = "No scan is attached to this DV yet." };

            // The same files read before: the earlier reading (kept beside the uploads) is returned at once.
            var cacheFolder = Path.Combine(_uploads.Folder, "ocr");
            // The key names the exact files (and the reader's version), so a reading is never reused for other files.
            var key = string.Join("|", scans.OrderBy(s => s.Id).Select(s => $"{s.Id}:{s.SizeBytes}:{s.UploadedAt.Ticks}")) + "|" + ReaderVersion;
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)))[..16];
            var cache = Path.Combine(cacheFolder, $"dv-{voucherId}-{hash}.json");
            if (!again && File.Exists(cache))
            {
                try { return JsonSerializer.Deserialize<DvScanResult>(await File.ReadAllTextAsync(cache)) ?? new DvScanResult(); }
                catch (JsonException) { /* read again below */ }
            }

            var problem = await _tesseract.CheckAsync();
            if (problem is not null)
                return new DvScanResult { Message = "The scan reader cannot start on this server, so the details cannot be filled in automatically. " + problem };

            if (!await ScanGate.One.WaitAsync(ScanGate.Wait))
                return new DvScanResult { Message = "The server is busy reading another scan. Press \"Read the scan\" again in a minute." };

            var clock = System.Diagnostics.Stopwatch.StartNew();
            DvScanPages pages;
            try
            {
                var files = new List<(byte[], string)>();
                foreach (var s in scans)
                {
                    var stored = await _files.GetAsync(s.Id);
                    if (stored is not null) files.Add((await File.ReadAllBytesAsync(stored.FullPath), s.FileName));
                }
                pages = await _reader.ReadAsync(files);
            }
            catch (InvalidDataException ex)
            {
                return new DvScanResult { Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.WriteLog(Level.Error, LogType.Activity, $"Reading the scan of DV #{voucherId} failed: {ex}");
                return new DvScanResult { Message = "The scan could not be read. Type the details from the scan instead." };
            }
            finally
            {
                ScanGate.One.Release();
            }

            var accounts = (await _accounts.GetAllAsync()).Where(a => a.IsActive).Select(a => new PacketParser.Account(a.Code, a.Title)).ToList();
            var payees = (await _payees.GetAllAsync()).Select(p => new PacketParser.KnownPayee(p.Name, p.Tin, p.Address, p.TaxType)).ToList();
            var templates = (await _templates.GetAllAsync()).Where(t => t.IsActive).Select(t => new DvScanParser.Template(t.Id, t.Name, t.Items)).ToList();
            var result = DvScanParser.Parse(pages, accounts, payees, templates);
            result.Seconds = Math.Round(clock.Elapsed.TotalSeconds, 1);

            try
            {
                Directory.CreateDirectory(cacheFolder);
                foreach (var old in Directory.GetFiles(cacheFolder, $"dv-{voucherId}-*.json")) File.Delete(old);
                foreach (var old in Directory.GetFiles(cacheFolder, "dv-*.json").Where(p => File.GetLastWriteTime(p) < DateTime.Now.AddDays(-30))) File.Delete(old);
                await File.WriteAllTextAsync(cache, JsonSerializer.Serialize(result));
            }
            catch (IOException) { /* only a cache */ }

            _logger.WriteLog(Level.Info, LogType.Activity,
                $"User {byUserId} read the scan of DV #{voucherId}: {(result.Found ? $"page {result.DvPage}, {result.Fields.Count} fields, {result.Lines.Count} lines" : result.Message)} ({result.Seconds}s).");
            return result;
        }
    }
}
