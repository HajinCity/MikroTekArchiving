namespace MikroTekArchiving.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public bool DatabaseDown { get; set; }
    }
}
