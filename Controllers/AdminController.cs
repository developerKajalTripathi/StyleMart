using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StyleMart.Data;
using StyleMart.Models;
using StyleMart.Services;

namespace StyleMart.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly StyleMartContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly OrderService _orders;
    private readonly IWebHostEnvironment _env;

    public AdminController(StyleMartContext db, UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn, OrderService orders, IWebHostEnvironment env)
    {
        _db = db;
        _users = users;
        _signIn = signIn;
        _orders = orders;
        _env = env;
    }

    // ---------- auth ----------

    [AllowAnonymous]
    public IActionResult Login() =>
        User.IsInRole("Admin") ? RedirectToAction(nameof(Index)) : View();

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password)
    {
        var name = (username ?? "").Trim();
        var user = name.Length == 0 ? null
            : await _users.FindByNameAsync(name) ?? await _users.FindByEmailAsync(name);

        if (user != null && await _users.IsInRoleAsync(user, "Admin"))
        {
            var result = await _signIn.CheckPasswordSignInAsync(user, password ?? "", lockoutOnFailure: true);
            if (result.Succeeded)
            {
                await _signIn.SignInAsync(user, isPersistent: false);
                return RedirectToAction(nameof(Index));
            }
            if (result.IsLockedOut)
            {
                ViewBag.Error = "Too many failed attempts. Try again in a few minutes.";
                return View();
            }
        }
        ViewBag.Error = "Invalid username or password.";
        return View();
    }

    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ---------- dashboard ----------

    public async Task<IActionResult> Index()
    {
        ViewBag.ProductCount = await _db.Products.CountAsync();
        ViewBag.OrderCount = await _db.Orders.CountAsync();
        ViewBag.Sales = await _db.Orders.Where(o => o.Status != "Cancelled").SumAsync(o => (decimal?)o.Total) ?? 0;
        ViewBag.CustomerCount = (await _users.GetUsersInRoleAsync("Customer")).Count;
        return View(await _db.Orders.OrderByDescending(o => o.CreatedAt).Take(8).ToListAsync());
    }

    // ---------- products ----------

    public async Task<IActionResult> Products()
    {
        return View(await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .OrderByDescending(p => p.Id)
            .ToListAsync());
    }

    private async Task LoadCategoryListAsync()
    {
        ViewBag.CategoryList = await _db.Categories.OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();
    }

    public async Task<IActionResult> CreateProduct()
    {
        await LoadCategoryListAsync();
        return View("ProductForm", new Product());
    }

    public async Task<IActionResult> EditProduct(int id)
    {
        var product = await _db.Products
            .Include(p => p.Variants)
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();
        await LoadCategoryListAsync();
        return View("ProductForm", product);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProduct(Product product, IFormFile? imageFile, IList<IFormFile>? galleryFiles)
    {
        Product? oldProduct = null;
        if (product.Id > 0)
        {
            oldProduct = await _db.Products.FirstOrDefaultAsync(p => p.Id == product.Id);
            if (oldProduct == null) return NotFound();
            product.ImageUrl = oldProduct.ImageUrl;
        }

        if (!await _db.Categories.AnyAsync(c => c.Id == product.CategoryId))
            ModelState.AddModelError(nameof(product.CategoryId), "Please select a category.");

        string[] allowedExts = { ".jpg", ".jpeg", ".png", ".webp" };
        string extension = "";
        if (imageFile != null && imageFile.Length > 0)
        {
            extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExts.Contains(extension))
                ModelState.AddModelError("imageFile", "Only JPG, PNG and WEBP allowed.");
            if (imageFile.Length > 5 * 1024 * 1024)
                ModelState.AddModelError("imageFile", "Image must be under 5 MB.");
        }

        if (!ModelState.IsValid)
        {
            await LoadCategoryListAsync();
            return View("ProductForm", product);
        }

        string folder = Path.Combine(_env.WebRootPath, "product-images");
        Directory.CreateDirectory(folder);

        // Save main/cover image
        if (imageFile != null && imageFile.Length > 0)
        {
            string fileName = Guid.NewGuid().ToString("N") + extension;
            using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
                await imageFile.CopyToAsync(stream);
            product.ImageUrl = "/product-images/" + fileName;
        }

        if (product.Id == 0)
        {
            _db.Products.Add(product);
            await _db.SaveChangesAsync(); // need Id before adding gallery images
        }
        else
        {
            oldProduct!.Name = product.Name;
            oldProduct.CategoryId = product.CategoryId;
            oldProduct.Price = product.Price;
            oldProduct.DiscountPercentage = product.DiscountPercentage;
            oldProduct.Stock = product.Stock;
            oldProduct.Description = product.Description;
            oldProduct.IsActive = product.IsActive;
            oldProduct.ImageUrl = product.ImageUrl;
            await _db.SaveChangesAsync();
        }

        // Save gallery images
        if (galleryFiles != null)
        {
            int pid = product.Id;
            int nextOrder = await _db.ProductImages.Where(pi => pi.ProductId == pid)
                .MaxAsync(pi => (int?)pi.SortOrder) ?? 0;

            foreach (var gf in galleryFiles)
            {
                if (gf == null || gf.Length == 0) continue;
                var ext = Path.GetExtension(gf.FileName).ToLowerInvariant();
                if (!allowedExts.Contains(ext)) continue;
                if (gf.Length > 5 * 1024 * 1024) continue;

                string fn = Guid.NewGuid().ToString("N") + ext;
                using (var s = new FileStream(Path.Combine(folder, fn), FileMode.Create))
                    await gf.CopyToAsync(s);

                nextOrder++;
                _db.ProductImages.Add(new ProductImage
                {
                    ProductId = pid,
                    ImageUrl = "/product-images/" + fn,
                    SortOrder = nextOrder
                });
            }
            await _db.SaveChangesAsync();
        }

        TempData["Message"] = "Product saved successfully.";
        return RedirectToAction(nameof(Products));
    }

    /// <summary>Deletes a gallery image.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProductImage(int id, int productId)
    {
        var img = await _db.ProductImages.FindAsync(id);
        if (img != null)
        {
            // Delete physical file
            if (!string.IsNullOrEmpty(img.ImageUrl))
            {
                var path = Path.Combine(_env.WebRootPath, img.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
            _db.ProductImages.Remove(img);
            await _db.SaveChangesAsync();
            TempData["Message"] = "Image deleted.";
        }
        return RedirectToAction(nameof(EditProduct), new { id = productId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product != null)
        {
            product.IsActive = false; // soft delete keeps order history intact
            await _db.SaveChangesAsync();
        }
        TempData["Message"] = "Product hidden successfully.";
        return RedirectToAction(nameof(Products));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ShowProduct(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product != null)
        {
            product.IsActive = true;
            await _db.SaveChangesAsync();
        }
        TempData["Message"] = "Product is visible in the store again.";
        return RedirectToAction(nameof(Products));
    }

    // ---------- categories ----------

    //public async Task<IActionResult> Categories(int? editId)
    //{
    //    ViewBag.Form = editId.HasValue
    //        ? await _db.Categories.FindAsync(editId.Value) ?? new Category()
    //        : new Category();
    //    return View(await _db.Categories.Include(c => c.Products).OrderBy(c => c.Name).ToListAsync());
    //}

    //[HttpPost, ValidateAntiForgeryToken]
    //public async Task<IActionResult> SaveCategory(Category category)
    //{
    //    var name = (category.Name ?? "").Trim();
    //    if (name.Length == 0 || name.Length > 40)
    //    {
    //        TempData["Error"] = "Please enter a category name (max 40 characters).";
    //        return RedirectToAction(nameof(Categories));
    //    }
    //    if (await _db.Categories.AnyAsync(c => c.Name == name && c.Id != category.Id))
    //    {
    //        TempData["Error"] = "A category with this name already exists.";
    //        return RedirectToAction(nameof(Categories));
    //    }

    //    var imageUrl = string.IsNullOrWhiteSpace(category.ImageUrl) ? null : category.ImageUrl.Trim();
    //    if (category.Id == 0)
    //    {
    //        _db.Categories.Add(new Category { Name = name, ImageUrl = imageUrl });
    //    }
    //    else
    //    {
    //        var existing = await _db.Categories.FindAsync(category.Id);
    //        if (existing == null) return NotFound();
    //        existing.Name = name;
    //        existing.ImageUrl = imageUrl;
    //    }
    //    await _db.SaveChangesAsync();
    //    TempData["Message"] = "Category saved.";
    //    return RedirectToAction(nameof(Categories));
    //}

    //[HttpPost, ValidateAntiForgeryToken]
    //public async Task<IActionResult> DeleteCategory(int id)
    //{
    //    var category = await _db.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
    //    if (category == null) return NotFound();
    //    if (category.Products.Any())
    //    {
    //        TempData["Error"] = "This category still has products. Move or delete them first.";
    //    }
    //    else
    //    {
    //        _db.Categories.Remove(category);
    //        await _db.SaveChangesAsync();
    //        TempData["Message"] = "Category deleted.";
    //    }
    //    return RedirectToAction(nameof(Categories));
    //}

    // ---------- orders ----------

    public async Task<IActionResult> Orders()
    {
        return View(await _db.Orders.Include(o => o.Items).OrderByDescending(o => o.CreatedAt).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status, string? trackingMessage = null)
    {
        var allowed = new[] { "Processing", "Confirmed", "Shipped", "Out for Delivery", "Delivered", "Cancelled" };
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order != null && allowed.Contains(status) && order.Status != "Cancelled" && order.Status != status)
        {
            if (status == "Cancelled")
            {
                await _orders.CancelAsync(order); // restores stock
            }
            else
            {
                await _orders.UpdateTrackingAsync(order, status, trackingMessage);
            }
        }
        return RedirectToAction(nameof(Orders));
    }

    #region variants

    public async Task<IActionResult> ProductVariants(int? editId)
    {
        var model = new ProductVariantPageViewModel();

        if (editId.HasValue)
        {
            var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == editId.Value);
            if (variant != null) model.Form = variant;
        }

        model.Products = await _db.Products.OrderBy(p => p.Name).ToListAsync();
        model.Variants = await _db.ProductVariants
            .Include(v => v.Product)
            .OrderBy(v => v.ProductId).ThenBy(v => v.Color).ThenBy(v => v.Size)
            .ToListAsync();
        return View(model);
    }

    //Stock Keeping Unit==Sku
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProductVariant(ProductVariantPageViewModel model)
    {
        var variant = model.Form;
        var color = (variant.Color ?? "").Trim();
        var size = (variant.Size ?? "").Trim();
        var sku = string.IsNullOrWhiteSpace(variant.Sku) ? null : variant.Sku.Trim();

        if (variant.ProductId <= 0 || color.Length == 0 || size.Length == 0 || variant.Stock < 0)
        {
            TempData["Error"] = "Please enter valid product, color, size and stock.";
            return RedirectToAction(nameof(ProductVariants));
        }

        if (!await _db.Products.AnyAsync(p => p.Id == variant.ProductId))
        {
            TempData["Error"] = "Please select a valid product.";
            return RedirectToAction(nameof(ProductVariants));
        }

        bool duplicate = await _db.ProductVariants.AnyAsync(v =>
            v.ProductId == variant.ProductId && v.Color == color && v.Size == size && v.Id != variant.Id);
        if (duplicate)
        {
            TempData["Error"] = "This product already has that colour and size. Edit the existing variation instead.";
            return RedirectToAction(nameof(ProductVariants));
        }

        if (variant.Id == 0)
        {
            _db.ProductVariants.Add(new ProductVariant
            {
                ProductId = variant.ProductId,
                Color = color,
                Size = size,
                Stock = variant.Stock,
                Sku = sku
            });
            TempData["Success"] = "Variation added successfully.";
        }
        else
        {
            var existing = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == variant.Id);
            if (existing == null) return NotFound();
            existing.ProductId = variant.ProductId;
            existing.Color = color;
            existing.Size = size;
            existing.Stock = variant.Stock;
            existing.Sku = sku;
            TempData["Success"] = "Variation updated successfully.";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(ProductVariants));
    }


    /// <summary>Bulk add multiple variants for a product in one go.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkAddVariants(int productId,
        string[] colors, string[] sizes, int[] stocks)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId))
        {
            TempData["Error"] = "Product not found.";
            return RedirectToAction(nameof(ProductVariants));
        }

        int added = 0, skipped = 0;
        for (int i = 0; i < Math.Min(colors.Length, sizes.Length); i++)
        {
            var color = (colors.ElementAtOrDefault(i) ?? "").Trim();
            var size = (sizes.ElementAtOrDefault(i) ?? "").Trim();
            var stock = stocks.ElementAtOrDefault(i);
            if (string.IsNullOrEmpty(color) || string.IsNullOrEmpty(size)) { skipped++; continue; }

            bool exists = await _db.ProductVariants.AnyAsync(v =>
                v.ProductId == productId && v.Color == color && v.Size == size);
            if (exists) { skipped++; continue; }

            _db.ProductVariants.Add(new ProductVariant
            {
                ProductId = productId,
                Color = color,
                Size = size,
                Stock = stock
            });
            added++;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"{added} variation(s) added. {(skipped > 0 ? skipped + " skipped (duplicate or empty)." : "")}";
        return RedirectToAction(nameof(ProductVariants));
    }




    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProductVariant(int id)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == id);
        if (variant == null) return NotFound();

        // Cart rows reference variants without cascade, so clear them first.
        await _db.CartItems.Where(c => c.ProductVariantId == id).ExecuteDeleteAsync();
        _db.ProductVariants.Remove(variant);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Variation deleted successfully.";
        return RedirectToAction(nameof(ProductVariants));
    }

    #endregion



    #region Categories


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(Category category, IFormFile? imageFile)
    {
        var name = (category.Name ?? "").Trim();
        if (name.Length == 0 || name.Length > 40)
        {
            TempData["Error"] = "Please enter a category name (max 40 characters).";
            return RedirectToAction(nameof(Categories));
        }
        if (await _db.Categories.AnyAsync(c => c.Name == name && c.Id != category.Id))
        {
            TempData["Error"] = "A category with this name already exists.";
            return RedirectToAction(nameof(Categories));
        }

        // purani image (hidden field se aati hai)
        var imageUrl = string.IsNullOrWhiteSpace(category.ImageUrl) ? null : category.ImageUrl.Trim();

        // naya image select kiya hai to save karo
        if (imageFile != null && imageFile.Length > 0)
        {
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            string[] allowed = { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(ext))
            {
                TempData["Error"] = "Only JPG, PNG and WEBP images are allowed.";
                return RedirectToAction(nameof(Categories));
            }
            if (imageFile.Length > 5 * 1024 * 1024)
            {
                TempData["Error"] = "Image must be under 5 MB.";
                return RedirectToAction(nameof(Categories));
            }

            string folder = Path.Combine(_env.WebRootPath, "category-images");
            Directory.CreateDirectory(folder);
            string fileName = Guid.NewGuid().ToString("N") + ext;
            using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }
            imageUrl = "/category-images/" + fileName;
        }

        if (category.Id == 0)
        {
            _db.Categories.Add(new Category { Name = name, ImageUrl = imageUrl });
        }
        else
        {
            var existing = await _db.Categories.FindAsync(category.Id);
            if (existing == null) return NotFound();
            existing.Name = name;
            existing.ImageUrl = imageUrl;
        }
        await _db.SaveChangesAsync();
        TempData["Message"] = "Category saved.";
        return RedirectToAction(nameof(Categories));
    }


    public async Task<IActionResult> Categories(int? editId)
    {
        ViewBag.Form = editId.HasValue
            ? await _db.Categories.FindAsync(editId.Value) ?? new Category()
            : new Category();
        return View(await _db.Categories.Include(c => c.Products).OrderBy(c => c.Name).ToListAsync());
    }



    //[HttpPost, ValidateAntiForgeryToken]
    //public async Task<IActionResult> SaveCategory(Category category)
    //{
    //    var name = (category.Name ?? "").Trim();
    //    if (name.Length == 0 || name.Length > 40)
    //    {
    //        TempData["Error"] = "Please enter a category name (max 40 characters).";
    //        return RedirectToAction(nameof(Categories));
    //    }
    //    if (await _db.Categories.AnyAsync(c => c.Name == name && c.Id != category.Id))
    //    {
    //        TempData["Error"] = "A category with this name already exists.";
    //        return RedirectToAction(nameof(Categories));
    //    }

    //    var imageUrl = string.IsNullOrWhiteSpace(category.ImageUrl) ? null : category.ImageUrl.Trim();
    //    if (category.Id == 0)
    //    {
    //        _db.Categories.Add(new Category { Name = name, ImageUrl = imageUrl });
    //    }
    //    else
    //    {
    //        var existing = await _db.Categories.FindAsync(category.Id);
    //        if (existing == null) return NotFound();
    //        existing.Name = name;
    //        existing.ImageUrl = imageUrl;
    //    }
    //    await _db.SaveChangesAsync();
    //    TempData["Message"] = "Category saved.";
    //    return RedirectToAction(nameof(Categories));
    //}

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();
        if (category.Products.Any())
        {
            TempData["Error"] = "This category still has products. Move or delete them first.";
        }
        else
        {
            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();
            TempData["Message"] = "Category deleted.";
        }
        return RedirectToAction(nameof(Categories));
    }

    #endregion









    #region Reviews

    public async Task<IActionResult> Reviews()
    {
        var reviews = await _db.ProductReviews
            .Include(r => r.Product)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        return View(reviews);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleReview(int id)
    {
        var review = await _db.ProductReviews.FindAsync(id);
        if (review != null)
        {
            review.IsVisible = !review.IsVisible;
            await _db.SaveChangesAsync();
            TempData["Message"] = review.IsVisible ? "Review has been made visible." : "Review has been hidden.";
        }
        return RedirectToAction(nameof(Reviews));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var review = await _db.ProductReviews.FindAsync(id);
        if (review != null)
        {
            _db.ProductReviews.Remove(review);
            await _db.SaveChangesAsync();
            TempData["Message"] = "Review deleted Successfully";
        }
        return RedirectToAction(nameof(Reviews));
    }

    #endregion


}
