using System.ComponentModel.DataAnnotations;

namespace MikroTekArchiving.Models.ViewModels;

public class HotspotLoginViewModel
{
    [Display(Name = "Voucher Code")]
    [StringLength(20)]
    public string? Code { get; set; }

    public string? Error { get; set; }
    public string? Info { get; set; }
}
