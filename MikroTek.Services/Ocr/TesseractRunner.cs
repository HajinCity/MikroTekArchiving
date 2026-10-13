using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace MikroTek.Services.Ocr
{
    /// <summary>What kind of text a picture holds, so Tesseract only "sees" the characters that can be there.</summary>
    public enum CellKind
    {
        /// <summary>Normal words (payee, account title).</summary>
        Text,
        /// <summary>Peso amounts: 1,234.56 or (1,234.56).</summary>
        Amount,
        /// <summary>UACS codes: digits only.</summary>
        Code,
        /// <summary>Dates: 08/05/2026.</summary>
        Date,
        /// <summary>DV / payroll / check numbers: SHS2026-03-037, OE-26-07-2118, 0103264037.</summary>
        DocNo,
        /// <summary>TIN: 626-341-557-000.</summary>
        Tin,
        /// <summary>One line of words of a form (any characters).</summary>
        Line
    }

    /// <summary>
    /// Runs the Tesseract program (installed once on the server). Many small pictures are read in one run,
    /// which is much faster than starting Tesseract for each one.
    /// </summary>
    public class TesseractRunner(OcrOptions options)
    {
        private static readonly string WindowsDefault = @"C:\Program Files\Tesseract-OCR\tesseract.exe";

        public string ExePath =>
            !string.IsNullOrWhiteSpace(options.TesseractPath) ? options.TesseractPath
            : OperatingSystem.IsWindows() && File.Exists(WindowsDefault) ? WindowsDefault
            : "tesseract";

        /// <summary>Null when Tesseract can be started; otherwise what is wrong, in words for the admin.</summary>
        public async Task<string?> CheckAsync()
        {
            try
            {
                var (code, output, _) = await RunAsync(["--version"]);
                return code == 0 || output.Contains("tesseract", StringComparison.OrdinalIgnoreCase)
                    ? null : "Tesseract did not start correctly.";
            }
            catch (Exception ex)
            {
                return $"Tesseract is not installed or not found ({ExePath}). {ex.GetBaseException().Message}";
            }
        }

        /// <summary>How far the page is turned: 0, 90, 180 or 270 (degrees to turn it clockwise to read it).</summary>
        public async Task<int> DetectRotationAsync(string imagePath)
        {
            var (_, output, _) = await RunAsync([imagePath, "stdout", "--psm", "0"]);
            var m = Regex.Match(output, @"Rotate:\s*(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        /// <summary>Reads a block of normal text (a page header).</summary>
        /// <param name="layout">Tesseract's page layout mode: 6 = one block of text, 4 = a column of lines of different sizes.</param>
        public async Task<string> ReadTextAsync(string imagePath, int layout = 6)
        {
            var (_, output, _) = await RunAsync([imagePath, "stdout", "-l", options.Language, "--psm", layout.ToString()]);
            return output;
        }

        /// <summary>Reads many cell pictures of the same kind in one run. Returns one text per picture, in order.</summary>
        public async Task<List<string>> ReadCellsAsync(IReadOnlyList<string> imagePaths, CellKind kind, string workFolder)
        {
            if (imagePaths.Count == 0) return [];

            var list = Path.Combine(workFolder, $"list-{kind}-{Guid.NewGuid():N}.txt");
            // One path per line, "\n" only (also on Windows), no BOM.
            await File.WriteAllTextAsync(list, string.Join("\n", imagePaths) + "\n", new UTF8Encoding(false));

            var args = new List<string> { list, "stdout", "-l", options.Language };
            args.AddRange(kind switch
            {
                CellKind.Amount => ["--psm", "7", "-c", "tessedit_char_whitelist=0123456789,.()-"],
                CellKind.Code => ["--psm", "7", "-c", "tessedit_char_whitelist=0123456789"],
                CellKind.Date => ["--psm", "7", "-c", "tessedit_char_whitelist=0123456789/"],
                CellKind.Tin => ["--psm", "7", "-c", "tessedit_char_whitelist=0123456789-"],
                CellKind.DocNo => ["--psm", "6", "-c", "tessedit_char_whitelist=SHJOE0123456789-/"],
                CellKind.Line => ["--psm", "7"],
                _ => ["--psm", "6"]
            });

            var (_, output, _) = await RunAsync(args);
            // One result per picture, separated by a form feed.
            var pages = output.Split('\f');
            return imagePaths.Select((_, i) => i < pages.Length ? Regex.Replace(pages[i], @"\s+", " ").Trim() : "").ToList();
        }

        private async Task<(int Code, string Output, string Errors)> RunAsync(IEnumerable<string> args)
        {
            var info = new ProcessStartInfo(ExePath)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8
            };
            foreach (var a in args) info.ArgumentList.Add(a);
            // Tesseract would otherwise use every processor core for each page; we read pages side by side instead.
            info.Environment["OMP_THREAD_LIMIT"] = "1";

            using var process = Process.Start(info) ?? throw new InvalidOperationException("Tesseract could not be started.");
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Reading the scan took too long.");
            }
            return (process.ExitCode, await output, await errors);
        }
    }
}
