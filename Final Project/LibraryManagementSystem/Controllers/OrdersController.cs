using System.Text.Json;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;

namespace LibraryManagementSystem.Controllers;

[Authorize]
public class OrdersController(LibraryDbContext db, IConfiguration configuration) : Controller
{
    private string CartKey => $"library_cart:{User.Identity!.Name}";

    public async Task<IActionResult> Index()
    {
        await RefreshOverdueOrders();
        var readerName = User.Identity!.Name!;
        var orders = await db.BorrowingOrders.Include(o => o.Items).ThenInclude(i => i.Book).Include(o => o.Payments)
            .Where(o => o.ReaderName == readerName).OrderByDescending(o => o.CreatedAt).ToListAsync();
        ViewBag.ReaderName = readerName;
        return View(orders);
    }

    public async Task<IActionResult> Checkout()
    {
        await RefreshOverdueOrders();
        if (await HasBorrowingBlockAsync())
        {
            TempData["Error"] = "You cannot create a new borrowing order while you have an overdue or rejected-return order.";
            return RedirectToAction(nameof(Index));
        }
        var cart = ReadCart();
        if (cart.Count == 0) return RedirectToAction("Index", "Cart");
        var books = await GetBooksAsync(cart);
        if (!HasAvailableStock(cart, books))
        {
            TempData["Error"] = "One or more books are no longer available in the requested quantity.";
            return RedirectToAction("Index", "Cart");
        }
        ViewBag.Total = cart.Sum(line => books[line.BookId].BorrowingFee * line.Quantity);
        if ((decimal)ViewBag.Total < 0.50m)
        {
            TempData["Error"] = "The total borrowing fee must be at least 0.50 NZD. Please update the book borrowing fee.";
            return RedirectToAction("Index", "Cart");
        }
        ViewBag.Items = cart.Select(line => new CartLineViewModel { Book = books[line.BookId], Quantity = line.Quantity }).ToList();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCheckoutSession()
    {
        await RefreshOverdueOrders();
        var cart = ReadCart();
        if (cart.Count == 0) return RedirectToAction("Index", "Cart");
        if (await HasBorrowingBlockAsync())
        {
            TempData["Error"] = "You cannot create a new borrowing order while you have an overdue or rejected-return order.";
            return RedirectToAction("Index", "Cart");
        }

        var books = await GetBooksAsync(cart);
        if (!HasAvailableStock(cart, books))
        {
            TempData["Error"] = "One or more books are no longer available in the requested quantity.";
            return RedirectToAction("Index", "Cart");
        }
        var total = cart.Sum(line => books[line.BookId].BorrowingFee * line.Quantity);
        if (total < 0.50m)
        {
            TempData["Error"] = "The total borrowing fee must be at least 0.50 NZD. Please update the book borrowing fee.";
            return RedirectToAction("Index", "Cart");
        }
        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            TempData["Error"] = "Stripe is not configured. Add Stripe:SecretKey to user secrets or appsettings.Development.json.";
            return RedirectToAction(nameof(Checkout));
        }

        Stripe.StripeConfiguration.ApiKey = secretKey;
        var successUrl = Url.Action(nameof(PaymentSuccess), "Orders", null, Request.Scheme) + "?session_id={CHECKOUT_SESSION_ID}";
        var cancelUrl = Url.Action(nameof(PaymentCancelled), "Orders", null, Request.Scheme)!;
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = User.Identity!.Name,
            Metadata = new Dictionary<string, string> { ["reader_name"] = User.Identity!.Name! },
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
        return Redirect(session.Url!);
    }

    public async Task<IActionResult> PaymentSuccess(string session_id)
    {
        if (string.IsNullOrWhiteSpace(session_id)) return RedirectToAction(nameof(Index));
        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey)) return RedirectToAction(nameof(Index));
        Stripe.StripeConfiguration.ApiKey = secretKey;
        var session = await new SessionService().GetAsync(session_id);
        if (session.PaymentStatus != "paid")
        {
            TempData["Error"] = "Stripe payment was not completed.";
            return RedirectToAction(nameof(Checkout));
        }
        var existing = await db.BorrowingOrders.FirstOrDefaultAsync(o => o.StripeSessionId == session.Id);
        if (existing != null) return RedirectToAction(nameof(Index));

        var cart = ReadCart();
        var books = await GetBooksAsync(cart);
        if (cart.Count == 0 || !HasAvailableStock(cart, books))
        {
            TempData["Error"] = "Payment succeeded, but the cart is no longer available. Please contact the librarian.";
            return RedirectToAction(nameof(Index));
        }
        var items = cart.Select(line => new BorrowingOrderItem
        {
            BookId = line.BookId,
            BookTitle = books[line.BookId].Title,
            Quantity = line.Quantity,
            UnitBorrowingFee = books[line.BookId].BorrowingFee,
            UnitBookPrice = books[line.BookId].Price,
            DailyFineRate = books[line.BookId].DailyFineRate,
            DueDate = DateTime.Today.AddDays(books[line.BookId].BorrowingPeriodDays)
        }).ToList();
        var total = items.Sum(i => i.UnitBorrowingFee * i.Quantity);
        var order = new BorrowingOrder
        {
            ReaderName = User.Identity!.Name!,
            CreatedAt = DateTime.Today,
            DueDate = items.Min(i => i.DueDate) ?? DateTime.Today,
            Status = OrderStatus.Borrowing,
            BorrowingFee = total,
            StripeSessionId = session.Id,
            Items = items,
            Payments = new List<Payment> { new() { Type = PaymentType.BorrowingFee, Amount = total } }
        };
        foreach (var line in cart) books[line.BookId].AvailableCopies -= line.Quantity;
        db.BorrowingOrders.Add(order);
        await db.SaveChangesAsync();
        HttpContext.Session.Remove(CartKey);
        TempData["Success"] = $"Payment succeeded. Order #{order.Id} is now borrowing.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult PaymentCancelled()
    {
        TempData["Error"] = "Payment was cancelled. Your cart was not submitted.";
        return RedirectToAction("Index", "Cart");
    }

    public async Task<IActionResult> AdditionalPaymentSuccess(string session_id)
    {
        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(session_id)) return RedirectToAction(nameof(Index));
        Stripe.StripeConfiguration.ApiKey = secretKey;
        var session = await new SessionService().GetAsync(session_id);
        if (session.PaymentStatus != "paid" || !session.Metadata.TryGetValue("order_id", out var orderIdText) || !int.TryParse(orderIdText, out var orderId) || !session.Metadata.TryGetValue("payment_type", out var paymentTypeText) || !Enum.TryParse<PaymentType>(paymentTypeText, out var paymentType))
        {
            TempData["Error"] = "Stripe payment was not completed.";
            return RedirectToAction(nameof(Index));
        }

        var order = await db.BorrowingOrders.Include(o => o.Payments).Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId && o.ReaderName == User.Identity!.Name);
        if (order == null) return NotFound();
        if (order.Payments.Any(p => p.StripeSessionId == session.Id)) return RedirectToAction(nameof(Index));
        var expected = paymentType == PaymentType.BookCharge ? order.BookChargeAmount : GetUnpaidFine(order);
        if (expected < 0.50m || session.AmountTotal != ToMinorUnits(expected))
        {
            TempData["Error"] = "The Stripe amount does not match the order amount.";
            return RedirectToAction(nameof(Index));
        }
        order.Payments.Add(new Payment { Type = paymentType, Amount = expected, StripeSessionId = session.Id });
        if (paymentType == PaymentType.Fine)
        {
            ApplyFinePayment(order, expected);
        }
        if (paymentType == PaymentType.BookCharge)
        {
            order.BookChargeAmount = 0;
            order.Status = GetStatusForOutstandingItems(order);
            if (order.Status == OrderStatus.Completed) order.ReturnedAt = DateTime.Today;
        }
        await db.SaveChangesAsync();
        TempData["Success"] = paymentType == PaymentType.BookCharge ? "Book charge paid. The order is now completed." : "Overdue fine paid.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestReturn(int id, Dictionary<int, int> returnQuantities)
    {
        var readerName = User.Identity!.Name!;
        var order = await db.BorrowingOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.ReaderName == readerName);
        if (order == null || (order.Status != OrderStatus.Borrowing && order.Status != OrderStatus.Overdue)) return NotFound();
        var totalRequested = 0;
        foreach (var item in order.Items)
        {
            var outstanding = item.Quantity - item.ReturnedQuantity - item.RejectedQuantity;
            var requested = returnQuantities.GetValueOrDefault(item.Id);
            if (requested < 0 || requested > outstanding)
            {
                TempData["Error"] = "Requested return quantity cannot exceed the outstanding quantity.";
                return RedirectToAction(nameof(Index));
            }
            item.ReturnRequestedQuantity = requested;
            totalRequested += requested;
        }
        if (totalRequested == 0)
        {
            TempData["Error"] = "Select at least one copy to return.";
            return RedirectToAction(nameof(Index));
        }
        order.Status = OrderStatus.ReturnRequested;
        order.ReturnRequestedAt = DateTime.Today;
        await db.SaveChangesAsync();
        TempData["Success"] = "Return request submitted for librarian review.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Admin()
    {
        await RefreshOverdueOrders();
        return View(await db.BorrowingOrders.Include(o => o.Items).ThenInclude(i => i.Book).OrderByDescending(o => o.CreatedAt).ToListAsync());
    }

    [HttpPost, Authorize(Roles = "Administrator"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessReturn(int id, Dictionary<int, int> rejectedQuantities, string reason)
    {
        var order = await db.BorrowingOrders.Include(o => o.Items).ThenInclude(i => i.Book).Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null || order.Status != OrderStatus.ReturnRequested) return NotFound();

        if (GetUnpaidFine(order) > 0.005m)
        {
            TempData["Error"] = "The overdue fine must be paid before this return can be processed.";
            return RedirectToAction(nameof(Admin));
        }

        var rejectedTotal = 0;
        foreach (var item in order.Items)
        {
            var requested = item.ReturnRequestedQuantity;
            var rejected = rejectedQuantities.GetValueOrDefault(item.Id);
            if (requested <= 0) continue;
            if (rejected < 0 || rejected > requested)
            {
                TempData["Error"] = "Rejected quantity must be between 0 and the requested return quantity.";
                return RedirectToAction(nameof(Admin));
            }
            item.RejectedQuantity += rejected;
            item.ReturnedQuantity += requested - rejected;
            item.ReturnRequestedQuantity = 0;
            // The fine paid for this return batch belongs to these copies.
            // Do not let it reduce the next fine for the copies still out.
            item.FinePaidAmount = 0;
            item.RejectionReason = rejected > 0 ? (string.IsNullOrWhiteSpace(reason) ? "The book did not meet the return condition." : reason.Trim()) : null;
            item.Book!.AvailableCopies += requested - rejected;
            rejectedTotal += rejected;
        }

        if (rejectedTotal == 0)
        {
            order.Status = GetStatusForOutstandingItems(order);
            order.BookChargeAmount = 0;
            if (order.Status == OrderStatus.Completed) order.ReturnedAt = DateTime.Today;
            order.RejectionReason = null;
            TempData["Success"] = "Return approved and all stock restored.";
        }
        else
        {
            order.Status = OrderStatus.ReturnRejected;
            var totalRejectedCharge = order.Items.Sum(i => i.RejectedQuantity * i.UnitBookPrice);
            var paidBookCharge = order.Payments.Where(p => p.Type == PaymentType.BookCharge).Sum(p => p.Amount);
            order.BookChargeAmount = Math.Max(0, totalRejectedCharge - paidBookCharge);
            order.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Some books did not meet the return condition." : reason.Trim();
            TempData["Error"] = "The selected book copies were rejected. The reader must pay the book charge.";
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Admin));
    }

    [HttpPost, Authorize(Roles = "Administrator"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveReturn(int id)
    {
        var order = await db.BorrowingOrders.Include(o => o.Items).ThenInclude(i => i.Book).Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null || order.Status != OrderStatus.ReturnRequested) return NotFound();
        if (GetUnpaidFine(order) > 0.005m)
        {
            TempData["Error"] = "The overdue fine must be paid before this return can be completed.";
            return RedirectToAction(nameof(Admin));
        }
        order.Status = OrderStatus.Completed;
        order.ReturnedAt = DateTime.Today;
        foreach (var item in order.Items)
        {
            item.Book!.AvailableCopies += item.Quantity - item.ReturnedQuantity - item.RejectedQuantity;
            item.ReturnedQuantity = item.Quantity - item.RejectedQuantity;
            item.FinePaidAmount = 0;
        }
        await db.SaveChangesAsync();
        TempData["Success"] = "Return approved and stock restored.";
        return RedirectToAction(nameof(Admin));
    }

    [HttpPost, Authorize(Roles = "Administrator"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectReturn(int id, string reason)
    {
        var order = await db.BorrowingOrders.FirstOrDefaultAsync(o => o.Id == id && o.Status == OrderStatus.ReturnRequested);
        if (order == null) return NotFound();
        order.Status = OrderStatus.ReturnRejected;
        var totalRejectedCharge = await db.BorrowingOrderItems.Where(i => i.BorrowingOrderId == id).SumAsync(i => i.UnitBookPrice * i.Quantity);
        var paidBookCharge = await db.Payments.Where(p => p.BorrowingOrderId == id && p.Type == PaymentType.BookCharge).SumAsync(p => p.Amount);
        order.BookChargeAmount = Math.Max(0, totalRejectedCharge - paidBookCharge);
        order.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "The book did not meet the return condition." : reason.Trim();
        await db.SaveChangesAsync();
        TempData["Error"] = "Return rejected. The reader must pay the book charge.";
        return RedirectToAction(nameof(Admin));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PayBookCharge(int id)
    {
        var readerName = User.Identity!.Name!;
        var order = await db.BorrowingOrders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == id && o.ReaderName == readerName && o.Status == OrderStatus.ReturnRejected);
        if (order == null) return NotFound();
        return await CreateAdditionalPaymentSession(order, PaymentType.BookCharge, order.BookChargeAmount);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PayFine(int id)
    {
        var readerName = User.Identity!.Name!;
        var order = await db.BorrowingOrders.Include(o => o.Payments).Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.ReaderName == readerName && (o.Status == OrderStatus.Overdue || o.Status == OrderStatus.ReturnRequested));
        if (order == null) return NotFound();
        return await CreateAdditionalPaymentSession(order, PaymentType.Fine, GetUnpaidFine(order));
    }

    private async Task<IActionResult> CreateAdditionalPaymentSession(BorrowingOrder order, PaymentType paymentType, decimal amount)
    {
        if (amount < 0.50m)
        {
            TempData["Error"] = "The payment amount must be at least 0.50 NZD.";
            return RedirectToAction(nameof(Index));
        }
        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            TempData["Error"] = "Stripe is not configured.";
            return RedirectToAction(nameof(Index));
        }
        Stripe.StripeConfiguration.ApiKey = secretKey;
        var successUrl = Url.Action(nameof(AdditionalPaymentSuccess), "Orders", null, Request.Scheme) + "?session_id={CHECKOUT_SESSION_ID}";
        var cancelUrl = Url.Action(nameof(Index), "Orders", null, Request.Scheme)!;
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = User.Identity!.Name,
            Metadata = new Dictionary<string, string> { ["order_id"] = order.Id.ToString(), ["payment_type"] = paymentType.ToString() },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = configuration["Stripe:Currency"] ?? "nzd",
                        UnitAmount = ToMinorUnits(amount),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = paymentType == PaymentType.BookCharge ? $"Book charge for order #{order.Id}" : $"Overdue fine for order #{order.Id}"
                        }
                    }
                }
            }
        };
        var session = await new SessionService().CreateAsync(options);
        return Redirect(session.Url!);
    }

    private async Task RefreshOverdueOrders()
    {
        var orders = await db.BorrowingOrders.Include(o => o.Items).ThenInclude(i => i.Book)
            .Where(o => o.Status == OrderStatus.Borrowing || o.Status == OrderStatus.Overdue || o.Status == OrderStatus.ReturnRequested)
            .ToListAsync();
        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                var outstanding = item.Quantity - item.ReturnedQuantity - item.RejectedQuantity;
                var dueDate = item.DueDate ?? order.DueDate;
                if (outstanding > 0 && dueDate < DateTime.Today)
                {
                    var days = (DateTime.Today - dueDate).Days;
                    item.FineAmount = Math.Min(item.DailyFineRate * outstanding * days, item.UnitBookPrice * outstanding);
                }
                else
                {
                    item.FineAmount = 0;
                }
            }
            order.FineAmount = order.Items.Sum(i => i.FineAmount);
            if (order.Status != OrderStatus.ReturnRequested)
            {
                order.Status = GetStatusForOutstandingItems(order);
            }
        }
        if (orders.Count > 0) await db.SaveChangesAsync();
    }

    private static OrderStatus GetStatusForOutstandingItems(BorrowingOrder order)
    {
        var activeItems = order.Items.Where(i => i.Quantity - i.ReturnedQuantity - i.RejectedQuantity > 0).ToList();
        if (activeItems.Count == 0) return OrderStatus.Completed;
        return activeItems.Any(i => (i.DueDate ?? order.DueDate) < DateTime.Today) ? OrderStatus.Overdue : OrderStatus.Borrowing;
    }

    private static decimal GetUnpaidFine(BorrowingOrder order) =>
        order.Items.Sum(i => Math.Max(0m, i.FineAmount - i.FinePaidAmount));

    private static void ApplyFinePayment(BorrowingOrder order, decimal amount)
    {
        foreach (var item in order.Items.OrderBy(i => i.Id))
        {
            var unpaid = Math.Max(0m, item.FineAmount - item.FinePaidAmount);
            var applied = Math.Min(unpaid, amount);
            item.FinePaidAmount += applied;
            amount -= applied;
            if (amount <= 0m) break;
        }
    }

    private Task<bool> HasBorrowingBlockAsync() =>
        db.BorrowingOrders.AnyAsync(o => o.ReaderName == User.Identity!.Name &&
            (o.Status == OrderStatus.Overdue || o.Status == OrderStatus.ReturnRejected));

    private async Task<Dictionary<int, Book>> GetBooksAsync(List<CartLine> cart) =>
        await db.Books.Where(b => cart.Select(x => x.BookId).Contains(b.Id)).ToDictionaryAsync(b => b.Id);

    private static bool HasAvailableStock(List<CartLine> cart, Dictionary<int, Book> books) =>
        cart.All(line => books.ContainsKey(line.BookId) && line.Quantity >= 1 && line.Quantity <= books[line.BookId].AvailableCopies);

    private static long ToMinorUnits(decimal amount) => decimal.ToInt64(decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero));

    private List<CartLine> ReadCart()
    {
        var json = HttpContext.Session.GetString(CartKey);
        return string.IsNullOrWhiteSpace(json) ? new List<CartLine>() : JsonSerializer.Deserialize<List<CartLine>>(json) ?? new List<CartLine>();
    }
}
