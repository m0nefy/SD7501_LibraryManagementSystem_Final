using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

[Authorize(Roles = "Administrator")]
public class CategoriesController(IUnitOfWork unit, LibraryDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Categories.Include(c => c.Books).OrderBy(c => c.Name).ToListAsync());
    public IActionResult Create() => View();
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category) {
        if (!ModelState.IsValid) return View(category);
        unit.Categories.Add(category);
        await unit.SaveAsync();
        TempData["Success"] = "Category added.";
        return RedirectToAction(nameof(Index));
        }
    public async Task<IActionResult> Edit(int id) {
        var item = await unit.Categories.GetByIdAsync(id);
        return item == null ? NotFound() : View(item);
        }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category category) {
        if (id != category.Id) return NotFound();
        if (!ModelState.IsValid) return View(category);
        unit.Categories.Update(category);
        await unit.SaveAsync();
        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Index));
        }
    public async Task<IActionResult> Delete(int id) {
        var item = await db.Categories.Include(c => c.Books).FirstOrDefaultAsync(c => c.Id == id);
        return item == null ? NotFound() : View(item);
        }
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id) {
        var item = await unit.Categories.GetByIdAsync(id);
        if (item == null) return NotFound();
        if (await db.Books.AnyAsync(b => b.CategoryId == id)) { TempData["Error"] = "Remove or reassign books before deleting this category.";
        return RedirectToAction(nameof(Index));
        } unit.Categories.Delete(item);
        await unit.SaveAsync();
        TempData["Success"] = "Category deleted.";
        return RedirectToAction(nameof(Index));
        }
}
