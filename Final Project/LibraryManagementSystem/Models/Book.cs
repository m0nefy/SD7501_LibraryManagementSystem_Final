using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

public class Book
{
    public int Id { get; set; }

    [Required, StringLength(120)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Author { get; set; } = string.Empty;
    [StringLength(20)] public string? ISBN { get; set; }
    [Range(0, 9999)] public int TotalCopies { get; set; }
    [Range(0, 9999)] public int AvailableCopies { get; set; }

    [Range(0, 999999)]
    [DataType(DataType.Currency)]
    [Display(Name = "Book price")]
    public decimal Price { get; set; }

    [Range(0.50, 999999, ErrorMessage = "Borrowing fee must be at least 0.50.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Borrowing fee")]
    public decimal BorrowingFee { get; set; }

    [Range(1, 365)]
    [Display(Name = "Borrowing period (days)")]
    public int BorrowingPeriodDays { get; set; } = 21;

    [Range(0, 999999)]
    [DataType(DataType.Currency)]
    [Display(Name = "Daily fine rate")]
    public decimal DailyFineRate { get; set; } = 1;

    [Display(Name = "Category")]
    [Range(1, int.MaxValue, ErrorMessage = "Please choose a category.")]
    public int CategoryId { get; set; }

    public Category? Category { get; set; }
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    public ICollection<BorrowingOrderItem> OrderItems { get; set; } = new List<BorrowingOrderItem>();
}
