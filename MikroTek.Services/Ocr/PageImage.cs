using SkiaSharp;

namespace MikroTek.Services.Ocr
{
    /// <summary>
    /// One scanned page, cleaned up for reading:
    ///  - enlarged 2× (phone photos and 150-dpi scans are too small for good reading),
    ///  - coloured marks (blue/red pen ticks, stamps) are painted white,
    ///  - the ruled lines of tables are found (to cut the table into cells) and then painted white.
    /// The grey shades of the printed text are kept: Tesseract reads them better than pure black/white.
    /// </summary>
    public sealed class PageImage
    {
        /// <summary>How much the page was enlarged (2 for the MOOE packet pages; set from the scan's size for DV forms).</summary>
        public float Scale { get; }

        public int Width { get; }
        public int Height { get; }

        /// <summary>true = printed ink to read (pen marks and table lines removed).</summary>
        private readonly bool[] _ink;
        /// <summary>The page in grey (0 black … 255 white), with pen marks and table lines painted white.</summary>
        private readonly byte[] _gray;
        /// <summary>The page in grey with coloured writing kept (for titles printed in colour).</summary>
        private readonly byte[] _grayWithColour;
        /// <summary>Writing of any colour (pen too), without the table lines.</summary>
        private bool[] _inkWithColour = [];
        /// <summary>The ruled lines found, kept to cut each row of a form into its own cells.</summary>
        private bool[] _vLine = [];

        /// <summary>y positions of the table's horizontal lines, top to bottom.</summary>
        public List<int> RowLines { get; } = [];
        /// <summary>x positions of the table's vertical lines, left to right.</summary>
        public List<int> ColumnLines { get; } = [];

        public bool HasTable => RowLines.Count >= 3 && ColumnLines.Count >= 3;
        public int ColumnCount => Math.Max(0, ColumnLines.Count - 1);
        public int RowCount => Math.Max(0, RowLines.Count - 1);

        private PageImage(int width, int height, float scale, bool[] ink, byte[] gray, byte[] grayWithColour)
        {
            Scale = scale;
            Width = width;
            Height = height;
            _ink = ink;
            _gray = gray;
            _grayWithColour = grayWithColour;
        }

        /// <summary>Decodes a JPG/PNG page, turns it upright (rotate = degrees clockwise) and cleans it.</summary>
        /// <param name="scale">How much to enlarge it; null = enough for about 3,300 dots across an A4 page.</param>
        public static PageImage Load(byte[] picture, int rotate, float? scale = 2f)
        {
            using var original = SKBitmap.Decode(picture) ?? throw new InvalidDataException("The page picture could not be opened.");
            var turned = rotate is 90 or 270;
            var across = Math.Min(original.Width, original.Height);
            var factor = scale ?? Math.Clamp(2800f / across, 1f, 3f);
            var w = (int)((turned ? original.Height : original.Width) * factor);
            var h = (int)((turned ? original.Width : original.Height) * factor);

            // A page fed a little crooked: straighten it, so ruled lines run level and text lines do not merge.
            var tilt = Tilt(original, rotate);
            using var page = Draw(original, rotate + tilt, factor, w, h);

            var px = page.GetPixelSpan();
            var gray = new float[w * h];
            var colorful = new bool[w * h];
            var tinted = new bool[w * h];
            for (int i = 0, p = 0; i < gray.Length; i++, p += 4)
            {
                int r = px[p], g = px[p + 1], b = px[p + 2];
                gray[i] = 0.299f * r + 0.587f * g + 0.114f * b;
                var chroma = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
                colorful[i] = chroma >= 60;
                tinted[i] = chroma >= 25;
            }
            // The faint edges of a stamp or pen stroke are only a little coloured: take them too when they touch it.
            var reach = Math.Max(1, (int)Math.Round(factor));
            var grown = (bool[])colorful.Clone();
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    if (!colorful[i]) continue;
                    for (var dy = -reach; dy <= reach; dy++)
                        for (var dx = -reach; dx <= reach; dx++)
                        {
                            int yy = y + dy, xx = x + dx;
                            if (yy >= 0 && yy < h && xx >= 0 && xx < w && tinted[yy * w + xx]) grown[yy * w + xx] = true;
                        }
                }
            colorful = grown;

            // "Dark" = clearly darker than the area around it (works on grey phone photos and shadows too).
            var dark = LocalDark(gray, w, h, radius: 25, margin: 18f);
            var ink = new bool[w * h];
            var clean = new byte[w * h];
            var withColour = new byte[w * h];
            for (var i = 0; i < ink.Length; i++)
            {
                ink[i] = dark[i] && !colorful[i];
                withColour[i] = (byte)Math.Clamp(gray[i], 0, 255);
                clean[i] = colorful[i] ? (byte)255 : withColour[i];
            }

            var result = new PageImage(w, h, factor, ink, clean, withColour);
            result.FindTable(dark);
            return result;
        }

        /// <summary>About how many dots per inch a page was scanned at: its short side over 8.5 inches (A4 / Letter / Legal).</summary>
        public static int Dpi(byte[] picture)
        {
            using var codec = SKCodec.Create(new MemoryStream(picture));
            if (codec is null) return 0;
            return (int)Math.Round(Math.Min(codec.Info.Width, codec.Info.Height) / 8.5);
        }

        /// <summary>
        /// A quick, smaller copy of the top part of a page (turned upright), to tell what kind of page it is
        /// without cleaning the whole page. Returns false when the picture cannot be opened.
        /// </summary>
        public static bool SavePreview(byte[] picture, int rotate, string path, int width = 1500, double topPart = 1.0)
        {
            using var original = SKBitmap.Decode(picture);
            if (original is null) return false;
            var turned = rotate is 90 or 270;
            var factor = Math.Min(2f, (float)width / (turned ? original.Height : original.Width));
            int w = (int)((turned ? original.Height : original.Width) * factor), h = (int)((turned ? original.Width : original.Height) * factor);
            using var page = Draw(original, rotate + Tilt(original, rotate), factor, w, h);
            using var top = new SKBitmap(w, Math.Max(1, (int)(h * topPart)));
            page.ExtractSubset(top, new SKRectI(0, 0, w, top.Height));
            using var image = SKImage.FromBitmap(top);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(path);
            data.SaveTo(file);
            return true;
        }

        private static SKBitmap Draw(SKBitmap original, float degrees, float factor, int w, int h)
        {
            var page = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(page);
            using var image = SKImage.FromBitmap(original);
            canvas.Clear(SKColors.White);
            canvas.Translate(w / 2f, h / 2f);
            canvas.RotateDegrees(degrees);
            canvas.Scale(factor);
            canvas.DrawImage(image, -original.Width / 2f, -original.Height / 2f, new SKSamplingOptions(SKCubicResampler.Mitchell));
            return page;
        }

        /// <summary>
        /// How many degrees (−3 … 3) to turn the upright page so its lines are level: the angle at which the dark
        /// pixels pile up into the fewest, fullest rows. Worked out on a small copy of the page.
        /// </summary>
        private static float Tilt(SKBitmap original, int rotate)
        {
            var turned = rotate is 90 or 270;
            var small = 900f / (turned ? original.Height : original.Width);
            int w = (int)((turned ? original.Height : original.Width) * small), h = (int)((turned ? original.Width : original.Height) * small);
            if (w < 100 || h < 100) return 0;
            using var preview = Draw(original, rotate, small, w, h);
            var px = preview.GetPixelSpan();
            var xs = new List<int>();
            var ys = new List<int>();
            for (int y = 0, p = 0; y < h; y++)
                for (var x = 0; x < w; x++, p += 4)
                    if (0.299f * px[p] + 0.587f * px[p + 1] + 0.114f * px[p + 2] < 130) { xs.Add(x - w / 2); ys.Add(y); }
            if (xs.Count < 500) return 0;

            double Score(double degrees)
            {
                var slope = Math.Tan(degrees * Math.PI / 180);
                var bins = new int[h + w];
                for (var i = 0; i < xs.Count; i++)
                {
                    var b = (int)Math.Round(ys[i] - xs[i] * slope) + w / 2;
                    if (b >= 0 && b < bins.Length) bins[b]++;
                }
                double s = 0;
                foreach (var n in bins) s += (double)n * n;
                return s;
            }
            double best = 0, bestScore = Score(0);
            for (var a = -3.0; a <= 3.0001; a += 0.25) { var s = Score(a); if (s > bestScore) { bestScore = s; best = a; } }
            var coarse = best;
            for (var a = coarse - 0.25; a <= coarse + 0.2501; a += 0.05) { var s = Score(a); if (s > bestScore) { bestScore = s; best = a; } }
            // The page is turned the other way to level a line that slopes by "best".
            return Math.Abs(best) < 0.1 ? 0 : (float)-best;
        }

        /// <summary>True for pixels darker than the average of the square around them by more than "margin".</summary>
        private static bool[] LocalDark(float[] gray, int w, int h, int radius, float margin)
        {
            // Summed-area table: the sum of any rectangle in four look-ups.
            var sum = new double[(w + 1) * (h + 1)];
            for (var y = 0; y < h; y++)
            {
                double row = 0;
                for (var x = 0; x < w; x++)
                {
                    row += gray[y * w + x];
                    sum[(y + 1) * (w + 1) + x + 1] = sum[y * (w + 1) + x + 1] + row;
                }
            }
            var dark = new bool[w * h];
            for (var y = 0; y < h; y++)
            {
                int y0 = Math.Max(0, y - radius), y1 = Math.Min(h, y + radius + 1);
                for (var x = 0; x < w; x++)
                {
                    int x0 = Math.Max(0, x - radius), x1 = Math.Min(w, x + radius + 1);
                    var area = (y1 - y0) * (x1 - x0);
                    var total = sum[y1 * (w + 1) + x1] - sum[y0 * (w + 1) + x1] - sum[y1 * (w + 1) + x0] + sum[y0 * (w + 1) + x0];
                    dark[y * w + x] = gray[y * w + x] < total / area - margin;
                }
            }
            return dark;
        }

        /// <summary>Finds the ruled lines of the table and erases them from the ink.</summary>
        private void FindTable(bool[] dark)
        {
            int w = Width, h = Height;
            var minH = (int)(80 * Scale);   // a horizontal rule is at least this long
            var minV = (int)(60 * Scale);   // a vertical rule is at least this tall

            var hLine = new bool[w * h];
            for (var y = 0; y < h; y++)
            {
                var start = -1;
                for (var x = 0; x <= w; x++)
                {
                    var on = x < w && dark[y * w + x];
                    if (on && start < 0) start = x;
                    if (!on && start >= 0)
                    {
                        if (x - start >= minH) for (var k = start; k < x; k++) hLine[y * w + k] = true;
                        start = -1;
                    }
                }
            }
            var vLine = new bool[w * h];
            for (var x = 0; x < w; x++)
            {
                var start = -1;
                for (var y = 0; y <= h; y++)
                {
                    var on = y < h && dark[y * w + x];
                    if (on && start < 0) start = y;
                    if (!on && start >= 0)
                    {
                        if (y - start >= minV) for (var k = start; k < y; k++) vLine[k * w + x] = true;
                        start = -1;
                    }
                }
            }

            // Rows: y where a long stretch of the width is ruled.
            var rowProfile = new int[h];
            for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) if (hLine[y * w + x]) rowProfile[y]++;
            RowLines.AddRange(Clusters(rowProfile, w * 0.25));
            FindColumns(vLine);

            // Underlined headings above the table ("Entity Name: ____   Register No.: ____") can look like rows.
            // The table starts at the first rule drawn across most of the table.
            if (ColumnLines.Count >= 2)
            {
                int left = ColumnLines[0], right = ColumnLines[^1];
                int Covered(int y)
                {
                    var best = 0;
                    for (var yy = Math.Max(0, y - 3); yy <= Math.Min(h - 1, y + 3); yy++)
                    {
                        var n = 0;
                        for (var x = left; x < right; x++) if (hLine[yy * w + x]) n++;
                        best = Math.Max(best, n);
                    }
                    return best;
                }
                // (Half is enough: on a slightly tilted scan a long rule breaks into pieces.)
                var first = RowLines.FindIndex(y => Covered(y) > (right - left) * 0.5);
                if (first > 0)
                {
                    RowLines.RemoveRange(0, first);
                    FindColumns(vLine);
                }
            }

            _vLine = vLine;
            _inkWithColour = new bool[w * h];
            for (var i = 0; i < _inkWithColour.Length; i++) _inkWithColour[i] = dark[i] && !hLine[i] && !vLine[i];

            // Erase the rules (one pixel wider, so their grey edges do not look like "1" or "|").
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    if (!hLine[i] && !vLine[i]) continue;
                    _ink[i] = false;
                    for (var dy = -1; dy <= 1; dy++)
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            int yy = y + dy, xx = x + dx;
                            if (yy >= 0 && yy < h && xx >= 0 && xx < w) _gray[yy * w + xx] = 255;
                        }
                }
        }

        private void FindColumns(bool[] vLine)
        {
            ColumnLines.Clear();
            if (RowLines.Count < 2) return;
            int w = Width, top = RowLines[0], bottom = RowLines[^1];
            var colProfile = new int[w];
            for (var y = top; y < bottom; y++) for (var x = 0; x < w; x++) if (vLine[y * w + x]) colProfile[x]++;
            ColumnLines.AddRange(Clusters(colProfile, (bottom - top) * 0.5));
        }

        /// <summary>The middle of each group of neighbouring positions whose count is above the threshold.</summary>
        private static List<int> Clusters(int[] profile, double threshold)
        {
            var result = new List<int>();
            int first = -1, last = -1;
            for (var i = 0; i <= profile.Length; i++)
            {
                var on = i < profile.Length && profile[i] > threshold;
                if (on && first >= 0 && i - last > 6) { result.Add((first + last) / 2); first = -1; }
                if (on) { if (first < 0) first = i; last = i; }
            }
            if (first >= 0) result.Add((first + last) / 2);
            return result;
        }

        /// <summary>
        /// Saves a rectangle of the cleaned page as a PNG for Tesseract (with a white margin).
        /// Returns false when the rectangle is (almost) empty.
        /// </summary>
        /// <param name="keepColour">true = coloured writing is kept (titles printed in red or blue).</param>
        public bool SaveRegion(string path, int left, int top, int right, int bottom, int minInk = 40, bool keepColour = false)
        {
            left = Math.Clamp(left, 0, Width); right = Math.Clamp(right, 0, Width);
            top = Math.Clamp(top, 0, Height); bottom = Math.Clamp(bottom, 0, Height);
            int w = right - left, h = bottom - top;
            if (w < 15 || h < 15) return false;

            var count = 0;
            var ink = keepColour ? _inkWithColour : _ink;
            for (var y = top; y < bottom; y++) for (var x = left; x < right; x++) if (ink[y * Width + x]) count++;
            if (count < minInk) return false;

            const int pad = 20;
            using var bmp = new SKBitmap(new SKImageInfo(w + 2 * pad, h + 2 * pad, SKColorType.Gray8, SKAlphaType.Opaque));
            var span = bmp.GetPixelSpan();
            span.Fill(255);
            var stride = w + 2 * pad;
            var source = keepColour ? _grayWithColour : _gray;
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    span[(y + pad) * stride + x + pad] = source[(top + y) * Width + left + x];

            using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(path);
            data.SaveTo(file);
            return true;
        }

        // ------------------------------------------------------------------ forms (DV): rows whose cells differ row by row

        public record TextLine(int Top, int Bottom);
        public record FormCell(int Left, int Right, List<TextLine> Lines);
        public record FormRow(int Top, int Bottom, List<FormCell> Cells);

        /// <summary>
        /// The form as rows (between the long ruled lines), each row cut at its own vertical lines into cells,
        /// and each cell into its lines of text. Works for forms like the DV whose rows have different columns.
        /// </summary>
        public List<FormRow> FormRows()
        {
            var rows = new List<FormRow>();
            var edges = new List<int> { 0 };
            edges.AddRange(RowLines);
            edges.Add(Height);
            for (var i = 0; i + 1 < edges.Count; i++)
            {
                int top = edges[i], bottom = edges[i + 1];
                if (bottom - top < 12 * Scale) continue;

                // The vertical rules crossing (most of) this row.
                int y0 = top + 3, y1 = bottom - 3, need = (int)((y1 - y0) * 0.6);
                var profile = new int[Width];
                for (var y = y0; y < y1; y++)
                    for (var x = 0; x < Width; x++)
                        if (_vLine[y * Width + x]) profile[x]++;
                var cuts = Clusters(profile, need);
                if (cuts.Count == 0 || cuts[0] > 40 * Scale) cuts.Insert(0, 0);
                if (cuts[^1] < Width - 40 * Scale) cuts.Add(Width);

                var cells = new List<FormCell>();
                for (var c = 0; c + 1 < cuts.Count; c++)
                {
                    int left = cuts[c] + 4, right = cuts[c + 1] - 4;
                    if (right - left < 8 * Scale) continue;
                    cells.Add(new FormCell(left, right, TextLines(left, right, top + 4, bottom - 4)));
                }
                rows.Add(new FormRow(top, bottom, cells));
            }
            return rows;
        }

        /// <summary>
        /// The lines of text inside a box. Lines set close together touch through their tails (p, g, f), so a line is
        /// where the writing is dense (its middle), widened afterwards to take in the tails.
        /// </summary>
        private List<TextLine> TextLines(int left, int right, int top, int bottom)
        {
            var counts = new int[Math.Max(0, bottom - top)];
            for (var y = top; y < bottom; y++)
                for (var x = left; x < right; x++)
                    if (_ink[y * Width + x]) counts[y - top]++;
            if (counts.Length == 0) return [];
            var peak = counts.Max();
            var on = Math.Max(Math.Max(2, (int)Scale), (int)(peak * 0.12));

            var runs = new List<(int Start, int End, int Ink)>();
            int start = -1, ink = 0;
            for (var i = 0; i <= counts.Length; i++)
            {
                var dense = i < counts.Length && counts[i] >= on;
                if (dense) { if (start < 0) start = i; ink += counts[i]; }
                else if (start >= 0) { runs.Add((start, i - 1, ink)); start = -1; ink = 0; }
            }
            // Pieces of the same line (a gap of a pixel or two) are joined; specks are dropped.
            var merged = new List<(int Start, int End, int Ink)>();
            foreach (var r in runs)
            {
                if (merged.Count > 0 && r.Start - merged[^1].End <= Math.Max(2, Scale * 1.5))
                    merged[^1] = (merged[^1].Start, r.End, merged[^1].Ink + r.Ink);
                else merged.Add(r);
            }
            merged = merged.Where(r => r.End - r.Start + 1 >= 4 * Scale && r.Ink >= 30 * Scale).ToList();

            var result = new List<TextLine>();
            for (var i = 0; i < merged.Count; i++)
            {
                var (s0, e0, _) = merged[i];
                var reach = (int)((e0 - s0 + 1) * 0.45) + 2;
                var up = i > 0 ? (merged[i - 1].End + s0) / 2 : 0;
                var down = i + 1 < merged.Count ? (e0 + merged[i + 1].Start) / 2 : counts.Length - 1;
                result.Add(new TextLine(top + Math.Max(up, s0 - reach), top + Math.Min(down, e0 + reach)));
            }
            return result;
        }

        /// <summary>The cell between row lines r, r+1 and column lines c, c+1 (a little inside the rules).</summary>
        public bool SaveCell(string path, int row, int column) =>
            SaveRegion(path, ColumnLines[column] + 5, RowLines[row] + 5, ColumnLines[column + 1] - 5, RowLines[row + 1] - 5);
    }
}
