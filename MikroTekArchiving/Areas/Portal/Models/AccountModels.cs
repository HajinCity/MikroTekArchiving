using System.ComponentModel.DataAnnotations;

namespace MikroTek.Archiving.Areas.Portal.Models;

public class LoginInput
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool Remember { get; set; }
    public string? ReturnUrl { get; set; }
    public string? Error { get; set; }
}

public class ChangePasswordInput
{
    [DataType(DataType.Password)] public string? CurrentPassword { get; set; }
    [DataType(DataType.Password)] public string? NewPassword { get; set; }
    [DataType(DataType.Password)] public string? ConfirmPassword { get; set; }
}
