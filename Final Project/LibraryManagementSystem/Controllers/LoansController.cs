using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

public class LoansController(IUnitOfWork unit, LibraryDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Loans.Include(l => l.Book).OrderByDescending(l => l.Status).ThenBy(l => l.DueDate).ToListAsync());
    public IActionResult Create() => RedirectToAction("Index", "Cart");
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(Loan loan)
    {
        TempData["Error"] = "Direct borrowing is disabled. Add books to the cart and submit an order instead.";
        return RedirectToAction("Index", "Cart");
        /*
        if (string.IsNullOrWhiteSpace(loan.ReaderName)) ModelState.AddModelError(nameof(loan.ReaderName), "Reader name is required.");
        var book = await db.Books.FindAsync(loan.BookId);
        if (book == null || book.AvailableCopies < 1) ModelState.AddModelError(nameof(loan.BookId), "This book is currently unavailable.");
        if (!ModelState.IsValid) { ViewBag.Books = await db.Books.Where(b => b.AvailableCopies > 0).OrderBy(b => b.Title).ToListAsync();
        return View(loan);
        }
        loan.BorrowedAt = DateTime.Today;
        loan.DueDate = DateTime.Today.AddDays(21);
        loan.Status = LoanStatus.Borrowed;
        book!.AvailableCopies--;
        unit.Loans.Add(loan);
        await unit.SaveAsync();
        TempData["Success"] = "Book borrowed for 21 days.";
        return RedirectToAction(nameof(Index));
        */
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(int id) {
        var loan = await db.Loans.Include(l => l.Book).FirstOrDefaultAsync(l => l.Id == id);
        if (loan == null || loan.Status == LoanStatus.Returned) return NotFound();
        loan.DueDate = loan.DueDate.AddDays(14);
        await unit.SaveAsync();
        TempData["Success"] = "Loan renewed for 14 days.";
        return RedirectToAction(nameof(Index));
        }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(int id) {
        var loan = await db.Loans.Include(l => l.Book).FirstOrDefaultAsync(l => l.Id == id);
        if (loan == null || loan.Status == LoanStatus.Returned) return NotFound();
        loan.Status = LoanStatus.Returned;
        loan.ReturnedAt = DateTime.Today;
        loan.Book!.AvailableCopies++;
        await unit.SaveAsync();
        TempData["Success"] = "Book returned successfully.";
        return RedirectToAction(nameof(Index));
        }
}
