namespace MikroTek.Services.Ocr
{
    /// <summary>Reading a scan uses every processor core: one scan at a time keeps the server responsive for everyone else.</summary>
    public static class ScanGate
    {
        public static readonly SemaphoreSlim One = new(1, 1);
        public static readonly TimeSpan Wait = TimeSpan.FromMinutes(3);
    }
}
