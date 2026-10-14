using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

public enum UserRole
{
    Reader,
    Administrator
}

public class AppUser
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Reader;
}
