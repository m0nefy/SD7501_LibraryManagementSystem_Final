using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

public enum LoanStatus { Borrowed, Returned }

public class Loan
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string ReaderName { get; set; } = string.Empty;
    public int BookId { get; set; }
    public Book? Book { get; set; }
    public DateTime BorrowedAt { get; set; } = DateTime.Today;
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(21);
    public DateTime? ReturnedAt { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Borrowed;
}
