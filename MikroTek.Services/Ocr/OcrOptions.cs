namespace MikroTek.Services.Ocr
{
    /// <summary>How the scan reader (Tesseract, free and offline) is run. Set from appsettings "Ocr".</summary>
    public class OcrOptions
    {
        /// <summary>
        /// "Auto" (default): on Windows the reader built into the portal (NuGet package "Tesseract", nothing to install);
        /// elsewhere the installed tesseract program. "Engine" = always the built-in reader; "Program" = always tesseract.exe.
        /// </summary>
        public string Mode { get; set; } = "Auto";

        /// <summary>Program mode: full path of tesseract.exe. Empty = the usual install folders on Windows, or "tesseract" on the PATH.</summary>
        public string TesseractPath { get; set; } = "";

        /// <summary>Built-in reader: the folder with eng.traineddata and osd.traineddata. Empty = "tessdata" beside the portal's files.</summary>
        public string TessdataFolder { get; set; } = "";

        /// <summary>Folder for the page pictures cut up while reading (cleaned up afterwards).</summary>
        public string WorkFolder { get; set; } = Path.Combine(Path.GetTempPath(), "mikrotek-ocr");

        public string Language { get; set; } = "eng";

        /// <summary>How long one Tesseract run may take before it is stopped.</summary>
        public int TimeoutSeconds { get; set; } = 300;

        public string TessdataPath => string.IsNullOrWhiteSpace(TessdataFolder) ? Path.Combine(AppContext.BaseDirectory, "tessdata") : TessdataFolder;
    }
}
