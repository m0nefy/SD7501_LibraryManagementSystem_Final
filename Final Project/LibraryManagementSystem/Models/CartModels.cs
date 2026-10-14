namespace LibraryManagementSystem.Models;

public class CartLine
{
    public int BookId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class CartLineViewModel
{
    public Book Book { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal TotalBorrowingFee => Book.BorrowingFee * Quantity;
}

public class CartViewModel
{
    public List<CartLineViewModel> Items { get; set; } = new();
    public string ReaderName { get; set; } = string.Empty;
    public decimal TotalBorrowingFee => Items.Sum(i => i.TotalBorrowingFee);
}
