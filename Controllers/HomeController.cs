using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StyleMart.Data;

namespace StyleMart.Controllers;

public class HomeController : Controller
{
    private readonly StyleMartContext _db;
    public HomeController(StyleMartContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Id).ToListAsync();
        var products = await _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Id)
            .Take(8)
            .ToListAsync();
        return View(products);
    }

    public IActionResult Error() => View();
}
