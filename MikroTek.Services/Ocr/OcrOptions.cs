namespace MikroTek.Services.Ocr
{
    /// <summary>Where Tesseract (the free offline text reader) is installed. Set from appsettings "Ocr".</summary>
    public class OcrOptions
    {
        /// <summary>Full path of tesseract.exe. Empty = the usual install folder on Windows, or "tesseract" on the PATH.</summary>
        public string TesseractPath { get; set; } = "";

        /// <summary>Folder for the page pictures cut up while reading (cleaned up afterwards).</summary>
        public string WorkFolder { get; set; } = Path.Combine(Path.GetTempPath(), "mikrotek-ocr");

        public string Language { get; set; } = "eng";

        /// <summary>How long one Tesseract run may take before it is stopped.</summary>
        public int TimeoutSeconds { get; set; } = 300;
    }
}
