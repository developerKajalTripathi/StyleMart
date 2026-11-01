using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StyleMart.Data;
using StyleMart.Models;
using StyleMart.Services;

namespace StyleMart.Controllers;

/// <summary>
/// Shopping flow: Cart -> (login) -> Address -> Payment -> Order success.
/// </summary>
public class CartController : Controller
{
    private readonly CartService _cart;
    private readonly OrderService _orders;
    private readonly StyleMartContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IConfiguration _config;

    public CartController(CartService cart, OrderService orders, StyleMartContext db,
        UserManager<ApplicationUser> users, IConfiguration config)
    {
        _cart = cart;
        _orders = orders;
        _db = db;
        _users = users;
        _config = config;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- cart ----------

    public async Task<IActionResult> Index()
    {
        _cart.ClearBuyNow(); // opening the cart means "checkout the whole cart"
        return View(await _cart.GetCartAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    //public async Task<IActionResult> Add(int id, int? variantId, int quantity = 1, bool buyNow = false)
    //{
    //    if (buyNow)
    //    {
    //        var buy = await _cart.SetBuyNowAsync(id, variantId, quantity);
    //        if (!buy.Ok)
    //        {
    //            TempData["Error"] = buy.Message;
    //            return RedirectToAction("Details", "Products", new { id });
    //        }
    //        return RedirectToAction(nameof(Checkout));
    //    }

    //    var result = await _cart.AddAsync(id, variantId, quantity);
    //    if (!result.Ok)
    //    {
    //        TempData["Error"] = result.Message;
    //        return RedirectToAction("Details", "Products", new { id });
    //    }
    //    //TempData["Message"] = result.Message;

    //    //TempData["CartSuccess"] = "Product added to cart successfully!";
    //    //return RedirectToAction(nameof(Index));

    //    if (!result.Ok)
    //    {
    //        TempData["Error"] = result.Message;
    //        return RedirectToAction("Details", "Products", new { id });
    //    }

    //    TempData["Message"] = result.Message;
    //    return RedirectToAction(nameof(Index));

    //}



    public async Task<IActionResult> Add(
    int id,
    int? variantId,
    int quantity = 1,
    bool buyNow = false)
    {
        if (buyNow)
        {
            var buy = await _cart.SetBuyNowAsync(id, variantId, quantity);

            if (!buy.Ok)
            {
                TempData["Error"] = buy.Message;
                return RedirectToAction("Details", "Products", new { id });
            }

            return RedirectToAction(nameof(Checkout));
        }

        var result = await _cart.AddAsync(id, variantId, quantity);

        if (!result.Ok)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction("Details", "Products", new { id });
        }

        TempData["Message"] = "Product added to your cart successfully";
        return RedirectToAction(nameof(Index));
    }







    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, int? variantId, int quantity)
    {
        await _cart.UpdateQuantityAsync(id, variantId, quantity);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, int? variantId)
    {
        await _cart.RemoveAsync(id, variantId);
        return RedirectToAction(nameof(Index));
    }

    // ---------- step 1: delivery address (login required) ----------

    [Authorize]
    public async Task<IActionResult> Checkout()
    {
        var checkout = await _cart.GetCheckoutAsync();
        if (!checkout.Lines.Any()) return RedirectToAction(nameof(Index));
        if (checkout.HasStockIssues)
        {
            TempData["Error"] = "Some items are out of stock or exceed the available quantity. Please update your cart.";
            return RedirectToAction(nameof(Index));
        }
        return View(await BuildCheckoutViewModelAsync(checkout, new Address()));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout([Bind(Prefix = "NewAddress")] Address newAddress)
    {
        var checkout = await _cart.GetCheckoutAsync();
        if (!checkout.Lines.Any()) return RedirectToAction(nameof(Index));

        if (!ModelState.IsValid)
            return View(await BuildCheckoutViewModelAsync(checkout, newAddress));

        var uid = CurrentUserId;
        newAddress.Id = 0;
        newAddress.UserId = uid;
        newAddress.IsDefault = !await _db.Addresses.AnyAsync(a => a.UserId == uid);
        _db.Addresses.Add(newAddress);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Payment), new { addressId = newAddress.Id });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        var uid = CurrentUserId;
        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == uid);
        if (address != null)
        {
            _db.Addresses.Remove(address);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Checkout));
    }

    private async Task<CheckoutViewModel> BuildCheckoutViewModelAsync(CartViewModel checkout, Address newAddress)
    {
        var uid = CurrentUserId;
        var vm = new CheckoutViewModel
        {
            Cart = checkout,
            NewAddress = newAddress,
            Addresses = await _db.Addresses.Where(a => a.UserId == uid)
                .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.Id).ToListAsync()
        };
        if (string.IsNullOrEmpty(vm.NewAddress.FullName))
        {
            var user = await _users.GetUserAsync(User);
            vm.NewAddress.FullName = user?.FullName ?? "";
        }
        return vm;
    }

    // ---------- step 2: order summary + payment ----------

    [Authorize]
    public async Task<IActionResult> Payment(int addressId)
    {
        var uid = CurrentUserId;
        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == uid);
        if (address == null) return RedirectToAction(nameof(Checkout));

        var checkout = await _cart.GetCheckoutAsync();
        if (!checkout.Lines.Any()) return RedirectToAction(nameof(Index));
        if (checkout.HasStockIssues)
        {
            TempData["Error"] = "Some items are out of stock or exceed the available quantity. Please update your cart.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.RazorpayKeyId = _config["Razorpay:KeyId"] ?? "";
        return View(new PaymentViewModel { Cart = checkout, Address = address });
    }

    /// <summary>
    /// Creates a Razorpay order and returns the order details as JSON for the frontend.
    /// </summary>
    [Authorize, HttpPost]
    public async Task<IActionResult> CreateRazorpayOrder([FromBody] RazorpayOrderRequest req)
    {
        var checkout = await _cart.GetCheckoutAsync();
        if (!checkout.Lines.Any()) return BadRequest("Cart is empty");

        var keyId = _config["Razorpay:KeyId"] ?? "";
        var keySecret = _config["Razorpay:KeySecret"] ?? "";

        try
        {
            var client = new Razorpay.Api.RazorpayClient(keyId, keySecret);
            var options = new Dictionary<string, object>
            {
                { "amount", (int)(checkout.Total * 100) }, // amount in paise
                { "currency", "INR" },
                { "receipt", $"SM-{DateTime.Now.Ticks}" }
            };
            var order = client.Order.Create(options);
            return Json(new
            {
                orderId = order["id"].ToString(),
                amount = (int)(checkout.Total * 100),
                currency = "INR",
                keyId
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Place order after Razorpay payment verified or COD selected.
    /// Also captures GPS location.
    /// </summary>
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(int addressId, string paymentMethod = "COD", string? razorpayPaymentId = null,   string? razorpayOrderId = null, string? razorpaySignature = null,decimal? latitude = null,   decimal? longitude = null,   string? detectedCity = null)
    {
        var uid = CurrentUserId;
        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == uid);
        if (address == null) return RedirectToAction(nameof(Checkout));

        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        // --- Razorpay signature verification ---
        if (paymentMethod == "Online")
        {
            if (string.IsNullOrEmpty(razorpayPaymentId) ||
                string.IsNullOrEmpty(razorpayOrderId) ||
                string.IsNullOrEmpty(razorpaySignature))
            {
                TempData["Error"] = "Payment verification failed. Please try again.";
                return RedirectToAction(nameof(Payment), new { addressId });
            }

            var keySecret = _config["Razorpay:KeySecret"] ?? "";
            var isValid = VerifyRazorpaySignature(razorpayOrderId, razorpayPaymentId, razorpaySignature, keySecret);
            if (!isValid)
            {
                TempData["Error"] = "Payment signature verification failed. If money was deducted, contact support.";
                return RedirectToAction(nameof(Payment), new { addressId });
            }
        }

        var checkout = await _cart.GetCheckoutAsync(); // re-read: prices/stock may have changed
        if (!checkout.Lines.Any()) return RedirectToAction(nameof(Index));

        var result = await _orders.PlaceOrderAsync(
            user, address, checkout, paymentMethod,
            razorpayPaymentId, latitude, longitude, detectedCity);

        if (result.Order == null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        if (checkout.IsBuyNow) _cart.ClearBuyNow();
        else await _cart.ClearCartAsync(uid);

        return RedirectToAction(nameof(Confirmation), new { id = result.Order.Id });
    }

    // ---------- step 3: success ----------

    [Authorize]
    public async Task<IActionResult> Confirmation(int id)
    {
        var uid = CurrentUserId;
        var order = await _db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && (o.UserId == uid || User.IsInRole("Admin")));
        return order == null ? NotFound() : View(order);
    }

    // ---------- helpers ----------

    private static bool VerifyRazorpaySignature(string orderId, string paymentId, string signature, string secret)
    {
        try
        {
            var payload = $"{orderId}|{paymentId}";
            var key = System.Text.Encoding.UTF8.GetBytes(secret);
            var data = System.Text.Encoding.UTF8.GetBytes(payload);
            using var hmac = new System.Security.Cryptography.HMACSHA256(key);
            var hash = hmac.ComputeHash(data);
            var computed = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return computed == signature;
        }
        catch { return false; }
    }
}

public class RazorpayOrderRequest
{
    public int AddressId { get; set; }
}
