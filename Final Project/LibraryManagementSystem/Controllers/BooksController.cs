using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

[Authorize(Roles = "Administrator")]
public class BooksController(IUnitOfWork unit, LibraryDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Books.Include(b => b.Category).OrderBy(b => b.Title).ToListAsync());
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id) {
        var item = await db.Books.Include(b => b.Category).FirstOrDefaultAsync(b => b.Id == id);
        return item == null ? NotFound() : View(item);
        }
    public async Task<IActionResult> Create() { await Categories();
        return View();
        }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Book book)
    {
        book.ISBN = string.IsNullOrWhiteSpace(book.ISBN) ? null : book.ISBN.Trim();
        if (book.ISBN != null && await db.Books.AnyAsync(b => b.ISBN == book.ISBN))
        {
            ModelState.AddModelError(nameof(book.ISBN), "A book with this ISBN already exists.");
        }
        if (book.AvailableCopies > book.TotalCopies) ModelState.AddModelError(nameof(book.AvailableCopies), "Available copies cannot exceed total copies.");
        if (!ModelState.IsValid) { await Categories();
        return View(book);
        }
        try
        {
            unit.Books.Add(book);
            await unit.SaveAsync();
            TempData["Success"] = "Book added successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(book.ISBN), "A book with this ISBN already exists.");
            await Categories();
            return View(book);
        }
    }
    public async Task<IActionResult> Edit(int id) {
        var item = await unit.Books.GetByIdAsync(id);
        if (item == null) return NotFound();
        await Categories();
        return View(item);
        }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Book book)
    {
        if (id != book.Id) return NotFound();
        book.ISBN = string.IsNullOrWhiteSpace(book.ISBN) ? null : book.ISBN.Trim();
        if (book.ISBN != null && await db.Books.AnyAsync(b => b.ISBN == book.ISBN && b.Id != id))
        {
            ModelState.AddModelError(nameof(book.ISBN), "Another book already uses this ISBN.");
        }
        if (book.AvailableCopies > book.TotalCopies) ModelState.AddModelError(nameof(book.AvailableCopies), "Available copies cannot exceed total copies.");
        if (!ModelState.IsValid) { await Categories();
        return View(book);
        }
        try
        {
            unit.Books.Update(book);
            await unit.SaveAsync();
            TempData["Success"] = "Book updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(book.ISBN), "Another book already uses this ISBN.");
            await Categories();
            return View(book);
        }
    }
    public async Task<IActionResult> Delete(int id) {
        var item = await db.Books.Include(b => b.Category).FirstOrDefaultAsync(b => b.Id == id);
        return item == null ? NotFound() : View(item);
        }
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await unit.Books.GetByIdAsync(id);
        if (item == null) return NotFound();

        var hasActiveBorrowing = await db.BorrowingOrderItems.AnyAsync(i =>
            i.BookId == id && i.Quantity > i.ReturnedQuantity + i.RejectedQuantity);
        if (hasActiveBorrowing)
        {
            TempData["Error"] = "This book cannot be deleted while copies are still borrowed or awaiting return processing.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            unit.Books.Delete(item);
            await unit.SaveAsync();
            TempData["Success"] = "Book deleted.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "This book cannot be deleted because it is referenced by existing records.";
        }

        return RedirectToAction(nameof(Index));
    }
    private async Task Categories() => ViewBag.Categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();
}
