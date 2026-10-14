using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

public class LoginViewModel
{
    [Required, StringLength(80)] public string Username { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(80, MinimumLength = 3)] public string Username { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Compare(nameof(Password)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = string.Empty;
}
