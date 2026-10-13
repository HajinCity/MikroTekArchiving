using System.Text.RegularExpressions;
using SkiaSharp;
using UglyToad.PdfPig;

namespace MikroTek.Services.Ocr
{
    public enum PageKind
    {
        Unknown,
        MooeSlip,
        Register,
        Alphalist
    }

    /// <summary>What a column of a scanned table holds.</summary>
    public enum ColumnRole
    {
        Other,
        Date,
        DocNo,
        Particulars,
        CashAdvance,
        Payment,
        Balance,
        OfficeSupplies,
        AccountDescription,
        Code,
        Amount,
        // Alphalist
        Payee,
        GrossBase,
        TaxBase,
        NonVat1,
        NonVat3,
        Vat1,
        Vat5,
        Total,
        Remarks,
        Tin
    }

    /// <summary>One page as read: its kind, the text above the table, and the table's cells (row by row).</summary>
    public class ReadPage
    {
        public int Number { get; init; }
        public PageKind Kind { get; set; }
        public string Header { get; set; } = "";
        public List<ColumnRole> Columns { get; set; } = [];
        public List<string[]> Rows { get; set; } = [];

        public string Cell(string[] row, ColumnRole role)
        {
            var i = Columns.IndexOf(role);
            return i >= 0 && i < row.Length ? row[i] : "";
        }
    }

    /// <summary>
    /// Turns an uploaded scan (PDF of scanned pages, or JPG/PNG photos) into text, page by page and cell by cell.
    /// Everything runs on this server with Tesseract; nothing is sent anywhere.
    /// </summary>
    public class DocumentReader(TesseractRunner tesseract, OcrOptions options)
    {
        private static readonly Regex DataRow = new(@"\d{1,2}/\d{1,2}/\d{2,4}|[JS]H\S?S?\s*\d{4}|O[EF]-\d|\d{3}-\d{3}-\d{3}", RegexOptions.IgnoreCase);

        /// <summary>The page pictures of a PDF (one per page) or the picture itself.</summary>
        public static List<byte[]> PagePictures(byte[] file, string fileName)
        {
            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                && !(file.Length > 4 && file[0] == '%' && file[1] == 'P' && file[2] == 'D' && file[3] == 'F'))
                return [file];

            var pages = new List<byte[]>();
            using var pdf = PdfDocument.Open(file);
            foreach (var page in pdf.GetPages())
            {
                // A scanned page is one big picture; take the biggest one on the page.
                var picture = page.GetImages().OrderByDescending(i => i.WidthInSamples * i.HeightInSamples).FirstOrDefault();
                if (picture is null)
                    throw new InvalidDataException($"Page {page.Number} has no scanned picture. Only scanned PDFs can be read here.");

                var raw = picture.RawBytes.ToArray();
                using var test = SKBitmap.Decode(raw);
                if (test is not null) pages.Add(raw);
                else if (picture.TryGetPng(out var png)) pages.Add(png);
                else throw new InvalidDataException($"The picture on page {page.Number} could not be opened.");
            }
            return pages;
        }

        public async Task<List<ReadPage>> ReadAsync(byte[] file, string fileName)
        {
            var work = Path.Combine(options.WorkFolder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                var pictures = PagePictures(file, fileName);

                // 1. Every page: turn upright, clean, find the table and read the text above it.
                var prepared = await Task.WhenAll(pictures.Select((p, i) => PrepareAsync(p, i + 1, work)));

                // 2. Decide what each page is. A page with no title but the same table as a register page continues it.
                var registerColumns = prepared.FirstOrDefault(x => x.Page.Kind == PageKind.Register).Image?.ColumnCount ?? 0;
                var continued = new HashSet<int>();
                foreach (var (page, image) in prepared)
                    if (page.Kind == PageKind.Unknown && image.HasTable && image.ColumnCount == registerColumns && registerColumns > 0)
                    {
                        page.Kind = PageKind.Register;
                        continued.Add(page.Number);
                    }

                // 3. Read the tables cell by cell.
                List<ColumnRole>? registerRoles = null;
                foreach (var (page, image) in prepared.Where(x => x.Image.HasTable && x.Page.Kind is PageKind.Register or PageKind.Alphalist))
                {
                    await ReadTableAsync(page, image, work, registerRoles, continued.Contains(page.Number));
                    if (page.Kind == PageKind.Register && page.Columns.Contains(ColumnRole.Code)) registerRoles ??= page.Columns;
                }
                return prepared.Select(x => x.Page).ToList();
            }
            finally
            {
                try { Directory.Delete(work, recursive: true); } catch { /* left for the OS temp clean-up */ }
            }
        }

        private async Task<(ReadPage Page, PageImage Image)> PrepareAsync(byte[] picture, int number, string work)
        {
            var original = Path.Combine(work, $"p{number}.img");
            await File.WriteAllBytesAsync(original, picture);
            var rotate = await tesseract.DetectRotationAsync(original);
            var image = PageImage.Load(picture, rotate);

            var headerBottom = image.HasTable ? image.RowLines[0] : image.Height;
            var headerFile = Path.Combine(work, $"p{number}-header.png");
            var header = image.SaveRegion(headerFile, 0, 0, image.Width, headerBottom, minInk: 200, keepColour: true)
                ? await tesseract.ReadTextAsync(headerFile) : "";

            var kind = Regex.IsMatch(header, @"MOOE\s*SLIP", RegexOptions.IgnoreCase) ? PageKind.MooeSlip
                : Regex.IsMatch(header, @"DISBURSEMENTS?\s*REGISTER", RegexOptions.IgnoreCase) ? PageKind.Register
                : Regex.IsMatch(header, @"ALPHABETICAL|WERE\s*WIT", RegexOptions.IgnoreCase) ? PageKind.Alphalist
                : PageKind.Unknown;
            return (new ReadPage { Number = number, Kind = kind, Header = header }, image);
        }

        private async Task ReadTableAsync(ReadPage page, PageImage image, string work, List<ColumnRole>? knownRegisterRoles, bool continuation)
        {
            var dir = Path.Combine(work, $"t{page.Number}");
            Directory.CreateDirectory(dir);

            // Pass 1: the first rows as plain text, to find the column titles and where the data starts.
            var headRows = Math.Min(6, image.RowCount);
            var headText = await ReadCellsAsync(image, dir, Enumerable.Range(0, headRows), _ => CellKind.Text, "h");
            // A page that continues the register from the page before has no column titles: its first row is data.
            var firstData = continuation ? 0
                : Enumerable.Range(0, headRows).FirstOrDefault(r => headText[r].Take(3).Any(c => DataRow.IsMatch(c)), headRows);

            page.Columns = page.Kind == PageKind.Alphalist
                ? AlphalistRoles(image.ColumnCount)
                : continuation && knownRegisterRoles is not null && knownRegisterRoles.Count == image.ColumnCount
                    ? knownRegisterRoles
                    : RegisterRoles(Enumerable.Range(0, firstData).Select(r => headText[r]).ToList(), image.ColumnCount);

            // Pass 2: the data rows, each column read as what it holds (amounts as amounts, codes as digits...).
            var rows = Enumerable.Range(firstData, image.RowCount - firstData);
            var data = await ReadCellsAsync(image, dir, rows, c => KindOf(page.Columns.ElementAtOrDefault(c)), "d");
            page.Rows = rows.Select(r => data[r]).Where(r => r.Any(c => c.Length > 0)).ToList();
        }

        /// <summary>Reads the given rows; returns row index → texts of its columns.</summary>
        private async Task<Dictionary<int, string[]>> ReadCellsAsync(PageImage image, string dir, IEnumerable<int> rows,
                                                                   Func<int, CellKind> kindOf, string prefix)
        {
            var result = new Dictionary<int, string[]>();
            var jobs = new Dictionary<CellKind, List<(int Row, int Col, string Path)>>();
            foreach (var r in rows)
            {
                result[r] = new string[image.ColumnCount];
                Array.Fill(result[r], "");
                for (var c = 0; c < image.ColumnCount; c++)
                {
                    var path = Path.Combine(dir, $"{prefix}{r:000}_{c:00}.png");
                    if (!image.SaveCell(path, r, c)) continue;
                    var kind = kindOf(c);
                    if (!jobs.TryGetValue(kind, out var list)) jobs[kind] = list = [];
                    list.Add((r, c, path));
                }
            }
            foreach (var (kind, list) in jobs)
            {
                var texts = await tesseract.ReadCellsAsync(list.Select(j => j.Path).ToList(), kind, dir);
                for (var i = 0; i < list.Count; i++) result[list[i].Row][list[i].Col] = texts[i];
            }
            return result;
        }

        private static CellKind KindOf(ColumnRole role) => role switch
        {
            ColumnRole.Date => CellKind.Date,
            ColumnRole.DocNo => CellKind.DocNo,
            ColumnRole.Code => CellKind.Code,
            ColumnRole.Tin => CellKind.Tin,
            ColumnRole.CashAdvance or ColumnRole.Payment or ColumnRole.Balance or ColumnRole.OfficeSupplies or ColumnRole.Amount
                or ColumnRole.GrossBase or ColumnRole.TaxBase or ColumnRole.NonVat1 or ColumnRole.NonVat3
                or ColumnRole.Vat1 or ColumnRole.Vat5 or ColumnRole.Total => CellKind.Amount,
            _ => CellKind.Text
        };

        /// <summary>The Alphalist always has the same columns (Voucher No. … TIN No.).</summary>
        private static List<ColumnRole> AlphalistRoles(int count)
        {
            List<ColumnRole> standard =
            [
                ColumnRole.DocNo, ColumnRole.Payee, ColumnRole.GrossBase, ColumnRole.TaxBase, ColumnRole.NonVat1, ColumnRole.NonVat3,
                ColumnRole.Vat1, ColumnRole.Vat5, ColumnRole.Total, ColumnRole.Remarks, ColumnRole.Tin
            ];
            if (count == standard.Count) return standard;
            // Unusual layout: keep the first two and the TIN at the end; the rest is not used.
            var roles = Enumerable.Repeat(ColumnRole.Other, count).ToList();
            if (count > 0) roles[0] = ColumnRole.DocNo;
            if (count > 1) roles[1] = ColumnRole.Payee;
            if (count > 2) roles[^1] = ColumnRole.Tin;
            return roles;
        }

        /// <summary>
        /// The Cash Disbursements Register's columns, found from their titles. The usual Appendix 43 has 12:
        /// Date · DV/Check No. · Particulars · Cash Advance · Payments · Balance · 2 Salaries · Office Supplies ·
        /// Account Description · UACS Code · Amount.
        /// </summary>
        private static List<ColumnRole> RegisterRoles(List<string[]> header, int count)
        {
            var titles = Enumerable.Range(0, count).Select(c => string.Join(" ", header.Select(r => r[c]))).ToList();
            var roles = Enumerable.Repeat(ColumnRole.Other, count).ToList();
            int Find(string pattern) => titles.FindIndex(t => Regex.IsMatch(t, pattern, RegexOptions.IgnoreCase));

            void Set(int i, ColumnRole role) { if (i >= 0 && roles[i] == ColumnRole.Other) roles[i] = role; }
            Set(Find(@"\bDate\b"), ColumnRole.Date);
            Set(Find(@"DV|Payroll|Check"), ColumnRole.DocNo);
            Set(Find(@"Partic"), ColumnRole.Particulars);
            Set(Find(@"Cash\s*Adv"), ColumnRole.CashAdvance);
            Set(Find(@"Balance"), ColumnRole.Balance);
            Set(Find(@"Office|Supplies|5020301"), ColumnRole.OfficeSupplies);
            Set(Find(@"Descr|Account"), ColumnRole.AccountDescription);
            Set(Find(@"UACS|Object|Code"), ColumnRole.Code);
            if (count > 0 && roles[^1] == ColumnRole.Other) roles[^1] = ColumnRole.Amount;
            var ca = roles.IndexOf(ColumnRole.CashAdvance);
            if (ca >= 0 && ca + 1 < count && roles[ca + 1] == ColumnRole.Other) roles[ca + 1] = ColumnRole.Payment;

            // Titles not readable: fall back to the standard 12-column layout.
            var found = roles.Count(r => r != ColumnRole.Other);
            if (count == 12)
                return
                [
                    ColumnRole.Date, ColumnRole.DocNo, ColumnRole.Particulars, ColumnRole.CashAdvance, ColumnRole.Payment,
                    ColumnRole.Balance, ColumnRole.Other, ColumnRole.Other, ColumnRole.OfficeSupplies,
                    ColumnRole.AccountDescription, ColumnRole.Code, ColumnRole.Amount
                ];
            return roles;
        }
    }
}
