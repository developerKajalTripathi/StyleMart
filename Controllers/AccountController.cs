using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StyleMart.Data;
using StyleMart.Models;
using StyleMart.Services;

namespace StyleMart.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly CartService _cart;
    private readonly WishlistService _wish;
    private readonly OrderService _orders;
    private readonly StyleMartContext _db;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        CartService cart, WishlistService wish, OrderService orders, StyleMartContext db)
    {
        _users = users;
        _signIn = signIn;
        _cart = cart;
        _wish = wish;
        _orders = orders;
        _db = db;
    }

    // ---------- login / register ----------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }



    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var input = model.Email.Trim();
        var user = await _users.FindByEmailAsync(input) ?? await _users.FindByNameAsync(input);
        if (user != null)
        {
            var result = await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                await _cart.MergeGuestCartAsync(user.Id);
                await _wish.MergeGuestWishlistAsync(user.Id);
                TempData["Message"] = "Login successful! Welcome back.";

                return await RedirectAfterLoginAsync(user, returnUrl);


            }
            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "Too many failed attempts. Please try again in a few minutes.");
                return View(model);
            }
        }

        ModelState.AddModelError("", "Invalid email or password.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new RegisterViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim()
        };
        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(model);
        }

        await _users.AddToRoleAsync(user, "Customer");
        await _signIn.SignInAsync(user, isPersistent: false);
        await _cart.MergeGuestCartAsync(user.Id);
        await _wish.MergeGuestWishlistAsync(user.Id);
        TempData["Message"] = $"Welcome to StyleMart, {user.FullName}! Your account has been created successfully.";
        return await RedirectAfterLoginAsync(user, returnUrl);
    }




    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();

    private async Task<IActionResult> RedirectAfterLoginAsync(ApplicationUser user, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        if (await _users.IsInRoleAsync(user, "Admin"))
            return RedirectToAction("Index", "Admin");
        return RedirectToAction("Index", "Home");
    }

    // ---------- my orders ----------

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [Authorize]
    public async Task<IActionResult> MyOrders()
    {
        var uid = CurrentUserId;
        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == uid)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return View(orders);
    }

    [Authorize]
    public async Task<IActionResult> OrderDetails(int id)
    {
        var uid = CurrentUserId;
        var order = await _db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        return order == null ? NotFound() : View(order);
    }


    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var uid = CurrentUserId;
        var order = await _db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        if (order == null) return NotFound();

        if (order.Status != "Processing")
        {
            TempData["Error"] = "This order can no longer be cancelled.";
        }
        else
        {
            await _orders.CancelAsync(order);
            TempData["Message"] = $"Order #{order.OrderNumber} has been cancelled.";
        }
        return RedirectToAction(nameof(OrderDetails), new { id });
    }
}
