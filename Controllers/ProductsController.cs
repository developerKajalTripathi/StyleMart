using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StyleMart.Data;
using StyleMart.Models;

namespace StyleMart.Controllers;

public class ProductsController : Controller
{
    private readonly StyleMartContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public ProductsController(StyleMartContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index(string? search, string? category, string? sort)
    {
        var query = _db.Products.Include(p => p.Category).Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Category!.Name.Contains(search));
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category!.Name == category);

        // Sort on the discounted (selling) price, as shoppers see it.
        query = sort switch
        {
            "price-low" => query.OrderBy(p => p.Price - (p.Price * p.DiscountPercentage / 100)),
            "price-high" => query.OrderByDescending(p => p.Price - (p.Price * p.DiscountPercentage / 100)),
            _ => query.OrderBy(p => p.Name)
        };

        ViewBag.Search = search;
        ViewBag.Category = category;
        ViewBag.Sort = sort;
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
        return View(await query.ToListAsync());
    }

    //public async Task<IActionResult> Details(int id)
    //{
    //    var product = await _db.Products
    //        .Include(p => p.Category)
    //        .Include(p => p.Variants)
    //        .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

    //    if (product == null) return NotFound();

    //    // Load visible reviews for this product
    //    ViewBag.Reviews = await _db.ProductReviews
    //        .Where(r => r.ProductId == id && r.IsVisible)
    //        .OrderByDescending(r => r.CreatedAt)
    //        .ToListAsync();

    //    // Check if logged-in user already reviewed
    //    if (User.Identity?.IsAuthenticated == true)
    //    {
    //        var userId = _users.GetUserId(User);
    //        ViewBag.AlreadyReviewed = await _db.ProductReviews.AnyAsync(r => r.ProductId == id && r.UserId == userId);
    //    }
    //    else
    //    {
    //        ViewBag.AlreadyReviewed = false;
    //    }

    //    return View(product);
    //}




    //second code
    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (product == null)
            return NotFound();

        // Existing customer reviews
        ViewBag.Reviews = await _db.ProductReviews
            .Where(r => r.ProductId == id && r.IsVisible)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // Existing review check
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = _users.GetUserId(User);

            ViewBag.AlreadyReviewed = await _db.ProductReviews
                .AnyAsync(r => r.ProductId == id && r.UserId == userId);
        }
        else
        {
            ViewBag.AlreadyReviewed = false;
        }

        // Similar products from the same category
        ViewBag.SimilarProducts = await _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive
                        && p.Id != id
                        && p.CategoryId == product.CategoryId)
            .OrderBy(p => p.Name)
            .Take(4)
            .ToListAsync();

        return View(product);
    }





    //POST: Submit a review
    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> SubmitReview(int productId, int rating, string? comment, string reviewerName)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        if (product == null) return NotFound();

        var userId = _users.GetUserId(User);

        // Prevent duplicate review
        bool alreadyReviewed = await _db.ProductReviews.AnyAsync(r => r.ProductId == productId && r.UserId == userId);
        if (alreadyReviewed)
        {
            TempData["ReviewError"] = "Aapne is product par pehle hi review de diya hai.";
            return RedirectToAction(nameof(Details), new { id = productId });
        }

        if (rating < 1 || rating > 5)
            rating = 5;

        var name = (reviewerName ?? "").Trim();
        if (name.Length == 0) name = User.Identity?.Name?.Split('@')[0] ?? "Customer";

        _db.ProductReviews.Add(new ProductReview
        {
            ProductId = productId,
            UserId = userId,
            ReviewerName = name.Length > 80 ? name[..80] : name,
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            IsVisible = true
        });

        await _db.SaveChangesAsync();
        TempData["ReviewSuccess"] = "Shukriya! Aapka review submit ho gaya.";
        return RedirectToAction(nameof(Details), new { id = productId });
    }





    //[HttpGet]
    //public async Task<IActionResult> RateProduct(int id)
    //{
    //    if (User.Identity?.IsAuthenticated != true)
    //        return RedirectToAction("Login", "Account");

    //    var product = await _db.Products
    //        .FirstOrDefaultAsync(p => p.Id == id);

    //    if (product == null)
    //        return NotFound();

    //    return View(product);
    //}

    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //public async Task<IActionResult> SubmitReview(int productId, string reviewerName, int rating,   string? comment)
    //{
    //    if (User.Identity?.IsAuthenticated != true)
    //        return RedirectToAction("Login", "Account");

    //    var product = await _db.Products
    //        .FirstOrDefaultAsync(p => p.Id == productId);

    //    if (product == null)
    //        return NotFound();

    //    if (string.IsNullOrWhiteSpace(reviewerName) ||
    //        reviewerName.Trim().Length > 80)
    //    {
    //        TempData["ReviewError"] = "Please enter your name.";
    //        return RedirectToAction("RateProduct", new { id = productId });
    //    }

    //    if (rating < 1 || rating > 5)
    //    {
    //        TempData["ReviewError"] = "Please select a rating.";
    //        return RedirectToAction("RateProduct", new { id = productId });
    //    }

    //    if (comment != null && comment.Length > 1000)
    //    {
    //        TempData["ReviewError"] = "Review must be under 1000 characters.";
    //        return RedirectToAction("RateProduct", new { id = productId });
    //    }

    //    var review = new StyleMart.Models.ProductReview
    //    {
    //        ProductId = productId,
    //        ReviewerName = reviewerName.Trim(),
    //        Rating = rating,
    //        Comment = comment?.Trim(),
    //        CreatedAt = DateTime.UtcNow,
    //        IsVisible = true
    //    };

    //    _db.ProductReviews.Add(review);
    //    await _db.SaveChangesAsync();

    //    TempData["ReviewSuccess"] = "Thank you! Your review has been submitted.";

    //    return RedirectToAction("Details", new { id = productId });
    //}





}
