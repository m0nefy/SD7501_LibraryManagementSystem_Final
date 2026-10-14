using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

public enum OrderStatus
{
    Borrowing,
    ReturnRequested,
    ReturnRejected,
    Overdue,
    Completed
}

public enum PaymentType
{
    BorrowingFee,
    Fine,
    BookCharge
}

public class BorrowingOrder
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "Reader name")]
    public string ReaderName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Today;
    public DateTime DueDate { get; set; }
    public DateTime? ReturnRequestedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Borrowing;

    [DataType(DataType.Currency)]
    public decimal BorrowingFee { get; set; }

    [DataType(DataType.Currency)]
    public decimal FineAmount { get; set; }

    [DataType(DataType.Currency)]
    public decimal BookChargeAmount { get; set; }

    [StringLength(250)]
    public string? RejectionReason { get; set; }

    [StringLength(200)]
    public string? StripeSessionId { get; set; }

    public ICollection<BorrowingOrderItem> Items { get; set; } = new List<BorrowingOrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class BorrowingOrderItem
{
    public int Id { get; set; }
    public int BorrowingOrderId { get; set; }
    public BorrowingOrder? BorrowingOrder { get; set; }
    public int? BookId { get; set; }
    public Book? Book { get; set; }
    [StringLength(200)]
    public string? BookTitle { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitBorrowingFee { get; set; }
    public decimal UnitBookPrice { get; set; }
    public decimal DailyFineRate { get; set; }

    public DateTime? DueDate { get; set; }
    public decimal FineAmount { get; set; }

    public int ReturnRequestedQuantity { get; set; }

    public int ReturnedQuantity { get; set; }
    public int RejectedQuantity { get; set; }

    [StringLength(250)]
    public string? RejectionReason { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int BorrowingOrderId { get; set; }
    public BorrowingOrder? BorrowingOrder { get; set; }
    public PaymentType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.Today;

    [StringLength(200)]
    public string? StripeSessionId { get; set; }
}
