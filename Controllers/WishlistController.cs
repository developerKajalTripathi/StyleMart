using Microsoft.AspNetCore.Mvc;
using StyleMart.Services;

namespace StyleMart.Controllers;

/// <summary>Liked products. Works for guests (Session) and logged-in users (database).</summary>
public class WishlistController : Controller
{
    private readonly WishlistService _wish;
    private readonly CartService _cart;

    public WishlistController(WishlistService wish, CartService cart)
    {
        _wish = wish;
        _cart = cart;
    }

    public async Task<IActionResult> Index() => View(await _wish.GetProductsAsync());

    // Called by the heart buttons (fetch/AJAX). Returns JSON so the page does not reload.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var r = await _wish.ToggleAsync(id);
        return Json(new { ok = r.Ok, added = r.Added, count = r.Count, message = r.Message });
    }

    /// <summary>Move a product from wishlist to cart, then redirect back to wishlist.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveToCart(int id)
    {
        var cartResult = await _cart.AddAsync(id, null, 1);
        if (cartResult.Ok)
        {
           
            var wishIds = await _wish.GetIdsAsync();
            if (wishIds.Contains(id))
                await _wish.ToggleAsync(id); 
            TempData["Message"] = "Product moved to your cart successfully!";
        }
        else
        {
            TempData["Error"] = cartResult.Message;
          
        }
        return RedirectToAction(nameof(Index));
    }



}
