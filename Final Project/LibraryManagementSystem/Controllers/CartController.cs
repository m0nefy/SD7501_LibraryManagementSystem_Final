using System.Text.Json;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

[Authorize]
public class CartController(LibraryDbContext db) : Controller
{
    private string CartKey => $"library_cart:{User.Identity!.Name}";

    public async Task<IActionResult> Index()
    {
        var cart = ReadCart();
        var books = await db.Books.Where(b => cart.Select(x => x.BookId).Contains(b.Id)).ToListAsync();
        var viewModel = new CartViewModel
        {
            Items = cart.Join(books, line => line.BookId, book => book.Id,
                (line, book) => new CartLineViewModel { Book = book, Quantity = line.Quantity }).ToList()
        };
        return View(viewModel);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int id, int quantity = 1)
    {
        var book = await db.Books.FindAsync(id);
        if (book == null) return NotFound();
        if (quantity < 1)
        {
            TempData["Error"] = "Quantity must be at least 1.";
            return RedirectToAction("Index", "Home");
        }
        var cart = ReadCart();
        var line = cart.FirstOrDefault(x => x.BookId == id);
        var requested = (line?.Quantity ?? 0) + quantity;
        if (requested > book.AvailableCopies)
        {
            TempData["Error"] = "The requested quantity is not available.";
            return RedirectToAction("Index", "Home");
        }
        if (line == null) cart.Add(new CartLine { BookId = id, Quantity = quantity });
        else line.Quantity = requested;
        SaveCart(cart);
        TempData["Success"] = "Book added to your cart.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Remove(int id)
    {
        var cart = ReadCart();
        cart.RemoveAll(x => x.BookId == id);
        SaveCart(cart);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        HttpContext.Session.Remove(CartKey);
        return RedirectToAction(nameof(Index));
    }

    private List<CartLine> ReadCart()
    {
        var json = HttpContext.Session.GetString(CartKey);
        return string.IsNullOrWhiteSpace(json) ? new List<CartLine>() : JsonSerializer.Deserialize<List<CartLine>>(json) ?? new List<CartLine>();
    }

    private void SaveCart(List<CartLine> cart) => HttpContext.Session.SetString(CartKey, JsonSerializer.Serialize(cart));
}
