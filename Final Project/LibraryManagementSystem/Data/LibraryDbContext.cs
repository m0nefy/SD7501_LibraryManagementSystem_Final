using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Data;

public class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<BorrowingOrder> BorrowingOrders => Set<BorrowingOrder>();
    public DbSet<BorrowingOrderItem> BorrowingOrderItems => Set<BorrowingOrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>().Property(b => b.Price).HasPrecision(18, 2);
        modelBuilder.Entity<Book>().Property(b => b.BorrowingFee).HasPrecision(18, 2);
        modelBuilder.Entity<Book>().Property(b => b.DailyFineRate).HasPrecision(18, 2);
        modelBuilder.Entity<BorrowingOrder>().Property(o => o.BorrowingFee).HasPrecision(18, 2);
        modelBuilder.Entity<BorrowingOrder>().Property(o => o.FineAmount).HasPrecision(18, 2);
        modelBuilder.Entity<BorrowingOrder>().Property(o => o.BookChargeAmount).HasPrecision(18, 2);
        modelBuilder.Entity<BorrowingOrder>().HasIndex(o => o.StripeSessionId).IsUnique().HasFilter("[StripeSessionId] IS NOT NULL");
        modelBuilder.Entity<BorrowingOrderItem>().Property(i => i.UnitBorrowingFee).HasPrecision(18, 2);
        modelBuilder.Entity<BorrowingOrderItem>().Property(i => i.UnitBookPrice).HasPrecision(18, 2);
        modelBuilder.Entity<BorrowingOrderItem>().Property(i => i.DailyFineRate).HasPrecision(18, 2);
        modelBuilder.Entity<BorrowingOrderItem>().Property(i => i.FineAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<Payment>().HasIndex(p => p.StripeSessionId).HasFilter("[StripeSessionId] IS NOT NULL");
        modelBuilder.Entity<Book>().HasIndex(b => b.ISBN).IsUnique().HasFilter("[ISBN] IS NOT NULL");
        modelBuilder.Entity<Book>().HasOne(b => b.Category).WithMany(c => c.Books)
            .HasForeignKey(b => b.CategoryId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Loan>().Property(l => l.Status).HasConversion<string>();
        modelBuilder.Entity<BorrowingOrder>().Property(o => o.Status).HasConversion<string>();
        modelBuilder.Entity<Payment>().Property(p => p.Type).HasConversion<string>();
        modelBuilder.Entity<AppUser>().Property(u => u.Role).HasConversion<string>();
        modelBuilder.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<BorrowingOrder>().HasMany(o => o.Items).WithOne(i => i.BorrowingOrder)
            .HasForeignKey(i => i.BorrowingOrderId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<BorrowingOrder>().HasMany(o => o.Payments).WithOne(p => p.BorrowingOrder)
            .HasForeignKey(p => p.BorrowingOrderId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<BorrowingOrderItem>().HasOne(i => i.Book).WithMany(b => b.OrderItems)
            .HasForeignKey(i => i.BookId).OnDelete(DeleteBehavior.SetNull);
    }
}
