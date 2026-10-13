using System.Text.RegularExpressions;

namespace MikroTek.Services.Ocr
{
    /// <summary>One line of text on the DV form, with where it is: row (between ruled lines), cell in that row, line in that cell.</summary>
    public record FormLine(int Row, int Cell, int Line, int Left, int Top, int Right, int Bottom, string Text)
    {
        public int Middle => (Top + Bottom) / 2;
    }

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
    }

    /// <summary>
    /// Reads a scanned DV packet: finds the page with the Disbursement Voucher form (Appendix 32) and the checklist cover
    /// sheet, then reads the DV form box by box. Offline, with Tesseract.
    /// </summary>
    public class DvScanReader(TesseractRunner tesseract, OcrOptions options)
    {
        private enum Kind { Other, Dv, Cover }

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

                // What each page is, a few pages at a time, until the DV form is found (the cover sheet comes before it).
                (int Number, int Rotate)? dv = null;
                for (var start = 0; start < pages.Count && dv is null; start += 4)
                {
                    var batch = await Task.WhenAll(Enumerable.Range(start, Math.Min(4, pages.Count - start))
                        .Select(i => ClassifyAsync(pages[i], i + 1, work)));
                    foreach (var p in batch)
                    {
                        if (p.Kind == Kind.Cover && result.CoverText.Length == 0) result.CoverText = p.Text;
                        if (p.Kind == Kind.Dv && dv is null) dv = (p.Number, p.Rotate);
                    }
                }
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

        private async Task<(int Number, Kind Kind, int Rotate, string Text)> ClassifyAsync(byte[] picture, int number, string work)
        {
            var original = Path.Combine(work, $"c{number}.img");
            await File.WriteAllBytesAsync(original, picture);
            var rotate = await tesseract.DetectRotationAsync(original);
            var preview = Path.Combine(work, $"c{number}.png");
            if (!PageImage.SavePreview(picture, rotate, preview, width: 1500)) return (number, Kind.Other, rotate, "");
            var text = await tesseract.ReadTextAsync(preview);

            // The DV form: its title and at least two of its printed boxes (a cover sheet also says "Disbursement Voucher").
            var formWords = new[] { @"Appendix\s*32", @"Mode\s+of\s+Payment", @"Accounting\s+Entry", @"Responsibility\s+Cent", @"Approved\s+for\s+Payment", @"Receipt\s+of\s+Payment", @"MFO\s*/?\s*PAP" }
                .Count(w => Regex.IsMatch(text, w, RegexOptions.IgnoreCase));
            var kind = Regex.IsMatch(text, @"DISBURSEMENT\s+VOUCHER", RegexOptions.IgnoreCase) && formWords >= 2
                ? Kind.Dv
                : Regex.IsMatch(text, @"\bDV\s*[#No.]{1,3}\s*:", RegexOptions.IgnoreCase) && Regex.IsMatch(text, @"Check(ed)?\s*by|Remarks", RegexOptions.IgnoreCase)
                    ? Kind.Cover
                    : Kind.Other;
            // The cover sheet's title line ("JOB ORDER - …", often highlighted) is read better as lines of different sizes.
            if (kind == Kind.Cover)
            {
                var large = Path.Combine(work, $"c{number}-large.png");
                if (PageImage.SavePreview(picture, rotate, large, width: 2400)) text += "\n" + await tesseract.ReadTextAsync(large, layout: 4);
            }
            return (number, kind, rotate, text);
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
