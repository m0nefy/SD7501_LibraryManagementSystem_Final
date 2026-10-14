using System.Security.Claims;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

public class AccountController(LibraryDbContext db, IPasswordHasher<AppUser> passwordHasher) : Controller
{
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == model.Username.Trim());
        if (user == null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }
        await SignInAsync(user);
        return LocalRedirect(string.IsNullOrWhiteSpace(model.ReturnUrl) ? Url.Action("Index", "Home")! : model.ReturnUrl);
    }

    [AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (await db.Users.AnyAsync(u => u.Username == model.Username.Trim())) ModelState.AddModelError(nameof(model.Username), "This username is already registered.");
        if (!ModelState.IsValid) return View(model);
        var user = new AppUser { Username = model.Username.Trim(), Role = UserRole.Reader };
        user.PasswordHash = passwordHasher.HashPassword(user, model.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await SignInAsync(user);
        return RedirectToAction("Index", "Home");
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult RegisterAdministrator() => View(new RegisterViewModel());

    [HttpPost, Authorize(Roles = "Administrator"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterAdministrator(RegisterViewModel model)
    {
        if (await db.Users.AnyAsync(u => u.Username == model.Username.Trim())) ModelState.AddModelError(nameof(model.Username), "This username is already registered.");
        if (!ModelState.IsValid) return View(model);
        var user = new AppUser { Username = model.Username.Trim(), Role = UserRole.Administrator };
        user.PasswordHash = passwordHasher.HashPassword(user, model.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TempData["Success"] = "Administrator account created.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task SignInAsync(AppUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    }
}
