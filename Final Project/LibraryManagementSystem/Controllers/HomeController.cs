using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

public class HomeController(LibraryDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search, int? categoryId)
    {
        var query = db.Books.Include(b => b.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(b => b.Title.Contains(search) || b.Author.Contains(search) ||
                (b.Category != null && b.Category.Name.Contains(search)));
        }
        if (categoryId.HasValue) query = query.Where(b => b.CategoryId == categoryId.Value);
        ViewBag.Categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        return View(await query.OrderBy(b => b.Title).ToListAsync());
    }
}
