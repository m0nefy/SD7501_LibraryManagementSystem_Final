using System.Text.Json;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;

namespace LibraryManagementSystem.Controllers.Api;

[ApiController]
[Route("api/payment")]
[Authorize]
public class PaymentApiController(LibraryDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpPost("create-checkout-session")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCheckoutSession()
    {
        var readerName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(readerName)) return Unauthorized();

        var blocked = await db.BorrowingOrders.AnyAsync(o =>
            o.ReaderName == readerName &&
            (o.Status == OrderStatus.Overdue || o.Status == OrderStatus.ReturnRejected));
        if (blocked)
        {
            return BadRequest(new { message = "You cannot create a new borrowing order while you have an overdue or rejected-return order." });
        }

        var cart = ReadCart();
        if (cart.Count == 0) return BadRequest(new { message = "Your cart is empty." });

        var books = await db.Books
            .Where(b => cart.Select(line => line.BookId).Contains(b.Id))
            .ToDictionaryAsync(b => b.Id);

        if (!cart.All(line => books.ContainsKey(line.BookId) &&
                              line.Quantity >= 1 &&
                              line.Quantity <= books[line.BookId].AvailableCopies))
        {
            return BadRequest(new { message = "One or more books are no longer available in the requested quantity." });
        }

        var total = cart.Sum(line => books[line.BookId].BorrowingFee * line.Quantity);
        if (total < 0.50m)
        {
            return BadRequest(new { message = "The total borrowing fee must be at least 0.50 NZD." });
        }

        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Stripe is not configured." });
        }

        Stripe.StripeConfiguration.ApiKey = secretKey;
        var successUrl = Url.Action("PaymentSuccess", "Orders", null, Request.Scheme)
                         + "?session_id={CHECKOUT_SESSION_ID}";
        var cancelUrl = Url.Action("PaymentCancelled", "Orders", null, Request.Scheme)!;
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = readerName,
            Metadata = new Dictionary<string, string> { ["reader_name"] = readerName },
            LineItems = cart.Select(line => new SessionLineItemOptions
            {
                Quantity = line.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = configuration["Stripe:Currency"] ?? "nzd",
                    UnitAmount = ToMinorUnits(books[line.BookId].BorrowingFee),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = books[line.BookId].Title,
                        Description = $"Library borrowing fee for {books[line.BookId].BorrowingPeriodDays} days"
                    }
                }
            }).ToList()
        };

        var session = await new SessionService().CreateAsync(options);
        return Ok(new { sessionId = session.Id, url = session.Url });
    }

    private List<CartLine> ReadCart()
    {
        var key = $"library_cart:{User.Identity!.Name}";
        var json = HttpContext.Session.GetString(key);
        return string.IsNullOrWhiteSpace(json)
            ? new List<CartLine>()
            : JsonSerializer.Deserialize<List<CartLine>>(json) ?? new List<CartLine>();
    }

    private static long ToMinorUnits(decimal amount) =>
        decimal.ToInt64(decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero));
}
