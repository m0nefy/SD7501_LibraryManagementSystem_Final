using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

public class Category
{
    public int Id { get; set; }

    [Required, StringLength(60, MinimumLength = 2)]
    [Display(Name = "Category name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }
    public ICollection<Book> Books { get; set; } = new List<Book>();
}
