using System.Text.RegularExpressions;

namespace MikroTek.Services.Ocr
{
    /// <summary>One line of text on the DV form, with where it is: row (between ruled lines), cell in that row, line in that cell.</summary>
    public record FormLine(int Row, int Cell, int Line, int Left, int Top, int Right, int Bottom, string Text)
    {
        public int Middle => (Top + Bottom) / 2;
    }

    /// <summary>One line of the checklist cover sheet and the mark in its box: "✓", "NA" or "" (empty box / not a checklist line).</summary>
    public record CoverLine(string Text, string Mark);

    /// <summary>Everything read from a scanned DV packet, before it is turned into the DV's fields.</summary>
    public class DvScanPages
    {
        public int PageCount { get; set; }
        /// <summary>The page with the Disbursement Voucher form (1-based), or null when none was found.</summary>
        public int? DvPage { get; set; }
        public List<FormLine> Lines { get; set; } = [];
        /// <summary>Boxes usually filled in by pen (DV No., Date, Check/ADA No., date paid), read with the ink colour kept.</summary>
        public Dictionary<string, string> PenFields { get; set; } = [];
        /// <summary>About how many dots per inch the sharpest page was scanned at (from its width).</summary>
        public int Dpi { get; set; }
        /// <summary>The text of the checklist cover sheet, when the packet has one.</summary>
        public string CoverText { get; set; } = "";
        /// <summary>The cover sheet line by line, with the tick or NA in each box.</summary>
        public List<CoverLine> CoverLines { get; set; } = [];
        /// <summary>What each page was taken to be ("2 DV form (9)": page, kind, how many DV-form words were seen) — for checking.</summary>
        public List<string> PageKinds { get; set; } = [];
        /// <summary>The text of the photocopied check, when the packet has one (its check number is printed).</summary>
        public string CheckText { get; set; } = "";
    }

    /// <summary>
    /// Reads a scanned DV packet: finds the page with the Disbursement Voucher form (Appendix 32) and the checklist cover
    /// sheet, then reads the DV form box by box. Offline, with Tesseract.
    /// </summary>
    public class DvScanReader(TesseractRunner tesseract, OcrOptions options)
    {
        private enum Kind { Other, Dv, Cover, Check }

        public async Task<DvScanPages> ReadAsync(IReadOnlyList<(byte[] Data, string Name)> files)
        {
            var result = new DvScanPages();
            var work = Path.Combine(options.WorkFolder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                var pages = files.SelectMany(f => DocumentReader.PagePictures(f.Data, f.Name)).ToList();
                result.PageCount = pages.Count;
                result.Dpi = pages.Select(p => PageImage.Dpi(p)).DefaultIfEmpty(0).Max();
                // Too small to read reliably (under about 120 dpi): better to say so than to fill in guesses.
                if (result.Dpi is > 0 and < 120) return result;

                // What each page is, a few pages at a time, until the DV form is found (the cover sheet comes before it,
                // the photocopied check usually right after it: one more batch is looked at for the check).
                (int Number, int Rotate)? dv = null, cover = null;
                var seen = new List<(int Number, Kind Kind, int Rotate, string Text, int Score, bool Title)>();
                var extra = 1;
                for (var start = 0; start < pages.Count && (dv is null || (result.CheckText.Length == 0 && extra-- > 0)); start += 4)
                {
                    var batch = await Task.WhenAll(Enumerable.Range(start, Math.Min(4, pages.Count - start))
                        .Select(i => ClassifyAsync(pages[i], i + 1, work)));
                    foreach (var p in batch)
                    {
                        seen.Add(p);
                        result.PageKinds.Add($"{p.Number} {p.Kind} ({p.Score})");
                        if (p.Kind == Kind.Cover && cover is null) { result.CoverText = p.Text; cover = (p.Number, p.Rotate); }
                        if (p.Kind == Kind.Dv && dv is null) dv = (p.Number, p.Rotate);
                        if (p.Kind == Kind.Check && result.CheckText.Length == 0) result.CheckText = p.Text;
                    }
                }
                // No page was clearly the DV form: the one that looks most like it, if it looks enough like it.
                if (dv is null)
                {
                    var likely = seen.Where(p => p.Kind is Kind.Other or Kind.Dv && ((p.Title && p.Score >= 2) || p.Score >= 4))
                        .OrderByDescending(p => p.Score + (p.Title ? 3 : 0)).FirstOrDefault();
                    if (likely.Number > 0) dv = (likely.Number, likely.Rotate);
                }
                if (cover is not null) await ReadCoverAsync(pages[cover.Value.Number - 1], cover.Value.Rotate, work, result);
                if (dv is null) return result;

                result.DvPage = dv.Value.Number;
                await ReadFormAsync(pages[dv.Value.Number - 1], dv.Value.Rotate, work, result);
                return result;
            }
            finally
            {
                try { Directory.Delete(work, recursive: true); } catch { /* left for the OS temp clean-up */ }
            }
        }

        /// <summary>Words printed on the DV form (Appendix 32), allowing for the usual misreadings.</summary>
        private static readonly string[] DvFormWords =
        [
            @"Appendix\s*3\s*2", @"Mode\s+of", @"Pay[ec]e\b", @"TIN\s*/?\s*Empl", @"Particulars", @"Fund\s*Cl", @"Amount\s*Due",
            @"UACS", @"Certified", @"Printed\s*Name", @"Responsib", @"MFO", @"Account\w*\s+Entry|Accounting", @"Approved",
            @"Receipt\s+of", @"Check\s*/?\s*ADA|ADA\s*No", @"JEV", @"Debit", @"Credit"
        ];

        private async Task<(int Number, Kind Kind, int Rotate, string Text, int Score, bool Title)> ClassifyAsync(byte[] picture, int number, string work)
        {
            var original = Path.Combine(work, $"c{number}.img");
            await File.WriteAllBytesAsync(original, picture);
            var rotate = await tesseract.DetectRotationAsync(original);
            var preview = Path.Combine(work, $"c{number}.png");
            if (!PageImage.SavePreview(picture, rotate, preview, width: 1800)) return (number, Kind.Other, rotate, "", 0, false);
            var text = await tesseract.ReadTextAsync(preview);

            // The DV form: its title and several of its printed boxes (a cover sheet also says "Disbursement Voucher",
            // and other forms share a few of these words, but not many).
            var title = Regex.IsMatch(text, @"DISB\w*\s*V[O0]UCH", RegexOptions.IgnoreCase);
            var score = DvFormWords.Count(w => Regex.IsMatch(text, w, RegexOptions.IgnoreCase));
            // A photocopied check: "PAY TO THE ORDER OF", "PESOS", the bank, and its printed check number.
            var checkWords = new[] { @"PA[YV]\s*T[O0]", @"ORDER\s*[O0]F", @"PESOS", @"DBP|Development\s+Bank|LAND\s*BANK", @"CHECK\s*N[o0]", @"ACC[O0]UNT\s*(N[o0]|NAME)" }
                .Count(w => Regex.IsMatch(text, w, RegexOptions.IgnoreCase));
            // The cover sheet: a list of supporting documents with boxes, "Remarks" and "Checked by".
            var documentLines = text.Split('\n').Count(l => Regex.IsMatch(l,
                @"Voucher|Receipt|Invoice|Report|Order|Certificat|Request|Payroll|Contract|Slip|Photos|Quotation|Resolution|Record|List|Timebook", RegexOptions.IgnoreCase));
            var kind = (title && score >= 3) || score >= 6
                ? Kind.Dv
                : checkWords >= 2 && Regex.IsMatch(text, @"\b\d{8,12}\b")
                    ? Kind.Check
                    : (Regex.IsMatch(text, @"\bDV\s*[#No.]{1,3}\s*:", RegexOptions.IgnoreCase) || Regex.IsMatch(text, @"Remarks", RegexOptions.IgnoreCase))
                      && (Regex.IsMatch(text, @"Check(ed)?\s*by", RegexOptions.IgnoreCase) || documentLines >= 4)
                        ? Kind.Cover
                        : Kind.Other;
            // The cover sheet's title line ("JOB ORDER - …", often highlighted) is read better as lines of different sizes.
            if (kind == Kind.Cover)
            {
                var large = Path.Combine(work, $"c{number}-large.png");
                if (PageImage.SavePreview(picture, rotate, large, width: 2400)) text += "\n" + await tesseract.ReadTextAsync(large, layout: 4);
            }
            return (number, kind, rotate, text, score, title);
        }

        /// <summary>
        /// The cover sheet line by line (with where each line starts), and the mark in the box left of each line:
        /// a tick or "NA" by pen. The box is the space just left of where the printed words start.
        /// </summary>
        private async Task ReadCoverAsync(byte[] picture, int rotate, string work, DvScanPages result)
        {
            var image = PageImage.Load(picture, rotate, scale: null);
            var clean = Path.Combine(work, "cover-clean.png");
            const int pad = 20;   // SaveRegion adds a white margin of 20 dots
            if (!image.SaveRegion(clean, 0, 0, image.Width, image.Height, minInk: 100)) return;
            var words = await tesseract.ReadWordsAsync(clean);

            var lines = words.GroupBy(w => w.Line)
                .Select(g =>
                {
                    var list = g.OrderBy(w => w.Left).ToList();
                    // Where the printed words start: the first word with letters (stray marks before it are not words).
                    var first = list.FirstOrDefault(w => Regex.IsMatch(w.Text, "[A-Za-z]{3}")) ?? list[0];
                    var text = string.Join(" ", list.SkipWhile(w => w != first).Select(w => w.Text));
                    return (Text: text, Left: first.Left - pad, Top: list.Min(w => w.Top) - pad, Bottom: list.Max(w => w.Top + w.Height) - pad);
                })
                .Where(l => l.Text.Length >= 3).OrderBy(l => l.Top).ToList();
            if (lines.Count == 0) return;

            // The list's words all start at the same place; the boxes are just left of it.
            var starts = lines.Select(l => l.Left).Where(x => x < image.Width * 0.45).OrderBy(x => x).ToList();
            var labelStart = starts.Count == 0 ? 0 : starts[starts.Count / 2];
            int boxLeft = (int)(labelStart - image.Width * 0.08), boxRight = (int)(labelStart - image.Width * 0.006);

            // A tick is one stroke; "NA" is two letters, about three times the ink of a tick.
            var found = new string[lines.Count];
            for (var i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                found[i] = "";
                if (Math.Abs(l.Left - labelStart) > image.Width * 0.03 || boxLeft < 0) continue;   // not a line of the list
                int h = l.Bottom - l.Top, top = l.Top + h / 6, bottom = l.Bottom - h / 6;
                var ink = image.InkCount(boxLeft, top, boxRight, bottom, withColour: true) / (h * image.Scale);
                found[i] = ink < 1.5 ? "" : ink >= 8.5 ? "NA" : "✓";
            }

            result.CoverLines = lines.Select((l, i) => new CoverLine(l.Text, found[i])).ToList();
        }

        private async Task ReadFormAsync(byte[] picture, int rotate, string work, DvScanPages result)
        {
            var image = PageImage.Load(picture, rotate, scale: null);
            var rows = image.FormRows();

            var jobs = new List<(int Row, int Cell, int Line, PageImage.FormCell Box, PageImage.TextLine Text, string Path)>();
            for (var r = 0; r < rows.Count; r++)
                for (var c = 0; c < rows[r].Cells.Count; c++)
                {
                    var cell = rows[r].Cells[c];
                    for (var l = 0; l < cell.Lines.Count; l++)
                    {
                        var path = Path.Combine(work, $"f{r:00}_{c:00}_{l:00}.png");
                        if (image.SaveRegion(path, cell.Left, cell.Lines[l].Top, cell.Right, cell.Lines[l].Bottom, minInk: 20))
                            jobs.Add((r, c, l, cell, cell.Lines[l], path));
                    }
                }

            // Read in four parts side by side (one Tesseract run per part).
            var parts = jobs.Chunk(Math.Max(1, (jobs.Count + 3) / 4)).ToList();
            var texts = (await Task.WhenAll(parts.Select(p => tesseract.ReadCellsAsync(p.Select(j => j.Path).ToList(), CellKind.Line, work))))
                .SelectMany(t => t).ToList();
            for (var i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                if (texts[i].Length > 0)
                    result.Lines.Add(new FormLine(j.Row, j.Cell, j.Line, j.Box.Left, j.Text.Top, j.Box.Right, j.Text.Bottom, texts[i]));
            }

            // Amounts and codes again, as digits only: a second, stricter reading of every line that looks like a number.
            var numeric = result.Lines.Select((l, i) => (l, i)).Where(x => Regex.IsMatch(x.l.Text, @"^\W*[\dOoSlI,.() P₱-]{4,}\W*$")).ToList();
            var numericPaths = new List<string>();
            foreach (var (l, i) in numeric)
            {
                var path = Path.Combine(work, $"n{i:000}.png");
                image.SaveRegion(path, l.Left, l.Top, l.Right, l.Bottom, minInk: 1);
                numericPaths.Add(path);
            }
            var digits = await tesseract.ReadCellsAsync(numericPaths, CellKind.Amount, work);
            for (var k = 0; k < numeric.Count; k++)
            {
                var (l, i) = numeric[k];
                var strict = digits[k];
                // Keep the stricter reading when it still looks like the same kind of number.
                if (Regex.IsMatch(l.Text, @"\d[.,]\d{2}\D*$") == Regex.IsMatch(strict, @"\d[.,]\d{2}\D*$") && Regex.Matches(strict, @"\d").Count >= Regex.Matches(l.Text, @"\d").Count - 1)
                    result.Lines[i] = l with { Text = Regex.IsMatch(l.Text, @"^\W*[P₱]") ? "₱" + strict : strict };
            }

            await ReadPenFieldsAsync(image, rows, result, work);
        }

        /// <summary>
        /// The DV No., Date and Check/ADA No. are often written by pen, which the cleaning removes. Their boxes are read
        /// again with the ink kept; what comes back is only used when it has the right shape (checked by the caller).
        /// </summary>
        private async Task ReadPenFieldsAsync(PageImage image, List<PageImage.FormRow> rows, DvScanPages result, string work)
        {
            var boxes = new List<(string Key, int L, int T, int R, int B)>();
            FormLine? Find(string pattern, Func<FormLine, bool>? where = null) => result.Lines
                .FirstOrDefault(l => Regex.IsMatch(l.Text, pattern, RegexOptions.IgnoreCase) && (where?.Invoke(l) ?? true));

            // In the top right box: from the label to the next label (or the bottom of the box).
            void Below(string key, FormLine? label)
            {
                if (label is null) return;
                var next = result.Lines.Where(l => l.Row == label.Row && l.Cell == label.Cell && l.Line > label.Line)
                    .Where(l => Regex.IsMatch(l.Text, @"^\W*(Date|DV\s*No|Fund|Mode)", RegexOptions.IgnoreCase)).Select(l => l.Top).DefaultIfEmpty(rows[label.Row].Bottom).Min();
                boxes.Add((key, label.Left, label.Top, label.Right, next));
            }
            var dvNo = Find(@"^\W*DV\s*No");
            var receipt = Find(@"Receipt\s+of\s+Payment");
            Below("DvNo", dvNo);
            Below("Date", Find(@"^\W*Date\s*:", l => dvNo is not null && l.Row <= dvNo.Row && l.Cell == dvNo.Cell));

            // Receipt of Payment: the box right of "Check/ADA No." and the box under its "Date :".
            var check = Find(@"^\W*Check\s*/?", l => receipt is null || l.Row > receipt.Row);
            if (check is not null && check.Cell + 1 < rows[check.Row].Cells.Count)
            {
                var box = rows[check.Row].Cells[check.Cell + 1];
                boxes.Add(("CheckNo", box.Left, rows[check.Row].Top + 4, box.Right, rows[check.Row].Bottom - 4));
            }
            var paid = Find(@"^\W*Date\s*:", l => receipt is not null && l.Row > receipt.Row);
            if (paid is not null) boxes.Add(("PaidDate", paid.Left, paid.Top, paid.Right, rows[paid.Row].Bottom - 4));

            var paths = new List<(string Key, string Path)>();
            foreach (var (key, l, t, r, b) in boxes)
            {
                var path = Path.Combine(work, $"pen-{key}.png");
                if (image.SaveRegion(path, l, t, r, b, minInk: 30, keepColour: true)) paths.Add((key, path));
            }
            var texts = await tesseract.ReadCellsAsync(paths.Select(p => p.Path).ToList(), CellKind.Text, work);
            for (var i = 0; i < paths.Count; i++) result.PenFields[paths[i].Key] = texts[i];
        }
    }
}
