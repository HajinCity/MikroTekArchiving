using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Tesseract;

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
    /// Runs Tesseract, the free offline text reader, in one of two ways:
    ///  - Engine: the reader built into the portal (NuGet package "Tesseract" + the language files in "tessdata").
    ///    Nothing to install on Windows; used by default there.
    ///  - Program: the installed tesseract program, many small pictures in one run (used on Linux, or when chosen).
    /// </summary>
    public class TesseractRunner(OcrOptions options)
    {
        static TesseractRunner()
        {
            // Several pages are read side by side; each reader must use one processor core, or they slow each other down.
            // (Set before the reader's library is loaded, which happens on first use.)
            Environment.SetEnvironmentVariable("OMP_THREAD_LIMIT", "1");
        }

        public bool UsesEngine =>
            options.Mode.Equals("Engine", StringComparison.OrdinalIgnoreCase)
            || (options.Mode.Equals("Auto", StringComparison.OrdinalIgnoreCase) && OperatingSystem.IsWindows() && string.IsNullOrWhiteSpace(options.TesseractPath));

        /// <summary>Where the Windows installers put it: for all users, or only for the person who installed it (winget does that).</summary>
        private static IEnumerable<string> WindowsPlaces()
        {
            foreach (var root in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                         Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         @"C:\Program Files"
                     })
                if (!string.IsNullOrEmpty(root)) yield return Path.Combine(root, "Tesseract-OCR", "tesseract.exe");
        }

        /// <summary>
        /// Where Homebrew puts it on a Mac (Apple Silicon M1/M2/M3: /opt/homebrew, Intel: /usr/local) and the usual
        /// Linux place. Looked up by full path because an app started at log-in does not get Homebrew on its PATH.
        /// </summary>
        private static readonly string[] UnixPlaces = ["/opt/homebrew/bin/tesseract", "/usr/local/bin/tesseract", "/usr/bin/tesseract"];

        public string ExePath =>
            !string.IsNullOrWhiteSpace(options.TesseractPath) ? options.TesseractPath
            : OperatingSystem.IsWindows() ? WindowsPlaces().FirstOrDefault(File.Exists) ?? "tesseract"
            : UnixPlaces.FirstOrDefault(File.Exists) ?? "tesseract";

        /// <summary>
        /// The language files shipped with the portal (tessdata beside its files), when they are there, so the installed
        /// program reads with exactly the same data as the built-in reader on Windows. Null = the program's own files.
        /// </summary>
        private string? ShippedTessdata
        {
            get
            {
                var folder = options.TessdataPath;
                return File.Exists(Path.Combine(folder, options.Language + ".traineddata")) && File.Exists(Path.Combine(folder, "osd.traineddata"))
                    ? folder : null;
            }
        }

        /// <summary>Null when the reader can be used; otherwise what is wrong, in words for the admin.</summary>
        public async Task<string?> CheckAsync()
        {
            if (UsesEngine) return await Task.Run(CheckEngine);
            try
            {
                var (code, output, errors) = await RunAsync(["--list-langs"]);
                if (code != 0 && !output.Contains("eng"))
                    return "Tesseract OCR is installed but did not start correctly. Please reinstall it with the default options.";
                // English for the words, "osd" to turn sideways pages upright: both come with the default install.
                var languages = output + errors;
                if (!languages.Contains("eng"))
                    return "Tesseract OCR is installed without the English language data. Reinstall it and keep \"English\" ticked.";
                return null;
            }
            catch (Exception)
            {
                return options.TesseractPath.Length > 0
                    ? $"Tesseract OCR was not found at \"{options.TesseractPath}\" (appsettings.json → Ocr → TesseractPath). Check the path, or leave it empty."
                    : OperatingSystem.IsMacOS()
                        ? "Tesseract OCR is not installed on this Mac. Open Terminal, run: brew install tesseract  (or run setup-mac.command again), " +
                          "then open this page again. No restart of the portal is needed."
                        : OperatingSystem.IsWindows()
                            ? "Tesseract OCR is not installed on this server PC. Install it (free) from the UB Mannheim page on GitHub, " +
                              "with the default options, then open this page again. No restart of the portal is needed."
                            : "Tesseract OCR is not installed on this server. Install the tesseract-ocr package, then open this page again.";
            }
        }

        /// <summary>How far the page is turned: 0, 90, 180 or 270 (degrees to turn it clockwise to read it).</summary>
        public async Task<int> DetectRotationAsync(string imagePath)
        {
            if (UsesEngine) return await Task.Run(() => EngineRotation(imagePath));
            var (_, output, _) = await RunAsync([imagePath, "stdout", "--psm", "0"]);
            var m = Regex.Match(output, @"Rotate:\s*(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        /// <summary>Reads a block of normal text (a page header).</summary>
        /// <param name="layout">Tesseract's page layout mode: 6 = one block of text, 4 = a column of lines of different sizes.</param>
        public async Task<string> ReadTextAsync(string imagePath, int layout = 6)
        {
            if (UsesEngine) return await Task.Run(() => EngineRead([imagePath], layout, null)[0]);
            var (_, output, _) = await RunAsync([imagePath, "stdout", "-l", options.Language, "--psm", layout.ToString()]);
            return output;
        }

        /// <summary>One word read, with its box on the picture; Line = which line of the page it is on.</summary>
        public record Word(int Line, int Left, int Top, int Width, int Height, string Text);

        /// <summary>The words of a page with where each one is (Tesseract's TSV output), read as lines of different sizes.</summary>
        public async Task<List<Word>> ReadWordsAsync(string imagePath)
        {
            string tsv;
            if (UsesEngine)
                tsv = await Task.Run(() =>
                {
                    using var engine = new TesseractEngine(options.TessdataPath, options.Language, EngineMode.Default);
                    Quiet(engine);
                    using var picture = Pix.LoadFromFile(imagePath);
                    using var page = engine.Process(picture, PageSegMode.SingleColumn);
                    return page.GetTsvText(1) ?? "";
                });
            else
                // -c tessedit_create_tsv=1 rather than the "tsv" config file: that file is not in the shipped tessdata folder.
                tsv = (await RunAsync([imagePath, "stdout", "-l", options.Language, "--psm", "4", "-c", "tessedit_create_tsv=1"])).Output;

            var words = new List<Word>();
            foreach (var row in tsv.Split('\n'))
            {
                var c = row.TrimEnd('\r').Split('\t');
                // level page block paragraph line word left top width height confidence text
                if (c.Length < 12 || c[0] != "5" || string.IsNullOrWhiteSpace(c[11])) continue;
                if (!int.TryParse(c[2], out var block) || !int.TryParse(c[3], out var par) || !int.TryParse(c[4], out var line)) continue;
                words.Add(new Word(block * 10000 + par * 100 + line, int.Parse(c[6]), int.Parse(c[7]), int.Parse(c[8]), int.Parse(c[9]), c[11].Trim()));
            }
            return words;
        }

        private static (int Layout, string? Allowed) Settings(CellKind kind) => kind switch
        {
            CellKind.Amount => (7, "0123456789,.()-"),
            CellKind.Code => (7, "0123456789"),
            CellKind.Date => (7, "0123456789/"),
            CellKind.Tin => (7, "0123456789-"),
            CellKind.DocNo => (6, "SHJOE0123456789-/"),
            CellKind.Line => (7, null),
            _ => (6, null)
        };

        /// <summary>Reads many cell pictures of the same kind in one run. Returns one text per picture, in order.</summary>
        public async Task<List<string>> ReadCellsAsync(IReadOnlyList<string> imagePaths, CellKind kind, string workFolder)
        {
            if (imagePaths.Count == 0) return [];
            var (layout, allowed) = Settings(kind);
            if (UsesEngine)
                return (await Task.Run(() => EngineRead(imagePaths, layout, allowed)))
                    .Select(t => Regex.Replace(t, @"\s+", " ").Trim()).ToList();

            var list = Path.Combine(workFolder, $"list-{kind}-{Guid.NewGuid():N}.txt");
            // One path per line, "\n" only (also on Windows), no BOM.
            await File.WriteAllTextAsync(list, string.Join("\n", imagePaths) + "\n", new UTF8Encoding(false));

            var args = new List<string> { list, "stdout", "-l", options.Language, "--psm", layout.ToString() };
            if (allowed is not null) args.AddRange(["-c", "tessedit_char_whitelist=" + allowed]);

            var (_, output, _) = await RunAsync(args);
            // One result per picture, separated by a form feed.
            var pages = output.Split('\f');
            return imagePaths.Select((_, i) => i < pages.Length ? Regex.Replace(pages[i], @"\s+", " ").Trim() : "").ToList();
        }

        // ------------------------------------------------------------------ the built-in reader (NuGet "Tesseract")

        private string? CheckEngine()
        {
            var folder = options.TessdataPath;
            foreach (var file in new[] { options.Language + ".traineddata", "osd.traineddata" })
                if (!File.Exists(Path.Combine(folder, file)))
                    return $"The language file {file} is missing from \"{folder}\". Rebuild the portal (it is copied there from MikroTek.Services\\Ocr\\tessdata).";
            try
            {
                using var engine = new TesseractEngine(folder, options.Language, EngineMode.Default);
                return null;
            }
            catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException || ex.InnerException is DllNotFoundException or BadImageFormatException)
            {
                return "The built-in scan reader could not start on this PC. Install the \"Microsoft Visual C++ Redistributable\" (x64) from Microsoft, " +
                       "or install Tesseract OCR and set \"Ocr\": { \"Mode\": \"Program\" } in appsettings.json.";
            }
            catch (Exception ex)
            {
                return "The built-in scan reader could not start: " + ex.GetBaseException().Message;
            }
        }

        /// <summary>Reads pictures one after another with one engine (an engine reads one picture at a time).</summary>
        private List<string> EngineRead(IReadOnlyList<string> paths, int layout, string? allowed)
        {
            using var engine = new TesseractEngine(options.TessdataPath, options.Language, EngineMode.Default);
            Quiet(engine);
            if (allowed is not null) engine.SetVariable("tessedit_char_whitelist", allowed);
            var mode = layout switch
            {
                4 => PageSegMode.SingleColumn, 7 => PageSegMode.SingleLine, 8 => PageSegMode.SingleWord, 0 => PageSegMode.OsdOnly, _ => PageSegMode.SingleBlock
            };
            var result = new List<string>(paths.Count);
            foreach (var path in paths)
            {
                try
                {
                    using var picture = Pix.LoadFromFile(path);
                    using var page = engine.Process(picture, mode);
                    result.Add(page.GetText() ?? "");
                }
                catch (Exception)
                {
                    result.Add("");   // one unreadable picture does not stop the others
                }
            }
            return result;
        }

        /// <summary>No "Estimating resolution…" notes in the portal's console for every picture.</summary>
        private static void Quiet(TesseractEngine engine) =>
            engine.SetVariable("debug_file", OperatingSystem.IsWindows() ? "NUL" : "/dev/null");

        private int EngineRotation(string path)
        {
            try
            {
                using var engine = new TesseractEngine(options.TessdataPath, "osd", EngineMode.TesseractOnly);
                Quiet(engine);
                using var picture = Pix.LoadFromFile(path);
                using var page = engine.Process(picture, PageSegMode.OsdOnly);
                page.DetectBestOrientationAndScript(out var degrees, out var confidence, out _, out _);
                // "degrees" = how the text is turned (counter-clockwise); turning it back is the rest of a full turn.
                return confidence < 1 ? 0 : (360 - degrees) % 360;
            }
            catch (Exception)
            {
                return 0;   // a page too empty to tell is read as it is
            }
        }

        // ------------------------------------------------------------------ the installed program

        private async Task<(int Code, string Output, string Errors)> RunAsync(IEnumerable<string> args)
        {
            var info = new ProcessStartInfo(ExePath)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8
            };
            if (ShippedTessdata is { } tessdata)
            {
                info.ArgumentList.Add("--tessdata-dir");
                info.ArgumentList.Add(tessdata);
            }
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
