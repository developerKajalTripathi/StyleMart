using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StyleMart.Data;
using StyleMart.Extensions;
using StyleMart.Models;

namespace StyleMart.Services;

public record WishlistToggleResult(bool Ok, bool Added, int Count, string Message);

/// <summary>
/// Guests: wishlist lives in Session. Logged-in users: wishlist lives in the database (WishlistItems).
/// After login the guest wishlist is merged into the user's saved wishlist.
/// </summary>
public class WishlistService
{
    public const string WishKey = "StyleMartWishlist";

    private readonly StyleMartContext _db;
    private readonly IHttpContextAccessor _http;
    private HashSet<int>? _cache; // per-request cache (service is scoped)

    public WishlistService(StyleMartContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    private HttpContext Ctx => _http.HttpContext!;
    private ISession Session => Ctx.Session;

    private string? CurrentUserId =>
        Ctx.User.Identity?.IsAuthenticated == true ? Ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    /// <summary>Ids of the active products the current visitor has liked.</summary>
    public async Task<HashSet<int>> GetIdsAsync()
    {
        if (_cache != null) return _cache;

        var uid = CurrentUserId;
        if (uid == null)
        {
            _cache = (Session.GetObject<List<int>>(WishKey) ?? new List<int>()).ToHashSet();
        }
        else
        {
            var ids = await _db.WishlistItems
                .Where(w => w.UserId == uid && w.Product!.IsActive)
                .Select(w => w.ProductId)
                .ToListAsync();
            _cache = ids.ToHashSet();
        }
        return _cache;
    }

    public async Task<int> CountAsync() => (await GetIdsAsync()).Count;

    /// <summary>Liked products, newest first.</summary>
    public async Task<List<Product>> GetProductsAsync()
    {
        var uid = CurrentUserId;
        if (uid == null)
        {
            var order = Session.GetObject<List<int>>(WishKey) ?? new List<int>();
            if (order.Count == 0) return new List<Product>();
            var found = await _db.Products
                .Include(p => p.Category).Include(p => p.Variants)
                .Where(p => p.IsActive && order.Contains(p.Id))
                .ToListAsync();
            return found.OrderByDescending(p => order.IndexOf(p.Id)).ToList();
        }

        var ids = await _db.WishlistItems
            .Where(w => w.UserId == uid)
            .OrderByDescending(w => w.AddedAt)
            .Select(w => w.ProductId)
            .ToListAsync();
        if (ids.Count == 0) return new List<Product>();

        var products = await _db.Products
            .Include(p => p.Category).Include(p => p.Variants)
            .Where(p => p.IsActive && ids.Contains(p.Id))
            .ToListAsync();
        return products.OrderBy(p => ids.IndexOf(p.Id)).ToList();
    }

    public async Task<WishlistToggleResult> ToggleAsync(int productId)
    {
        var exists = await _db.Products.AnyAsync(p => p.Id == productId && p.IsActive);
        if (!exists) return new WishlistToggleResult(false, false, await CountAsync(), "This product is no longer available.");

        var uid = CurrentUserId;
        bool added;

        if (uid == null)
        {
            var list = Session.GetObject<List<int>>(WishKey) ?? new List<int>();
            if (list.Contains(productId)) { list.Remove(productId); added = false; }
            else { list.Add(productId); added = true; }
            Session.SetObject(WishKey, list);
        }
        else
        {
            var item = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == uid && w.ProductId == productId);
            if (item == null)
            {
                _db.WishlistItems.Add(new WishlistItem { UserId = uid, ProductId = productId });
                added = true;
            }
            else
            {
                _db.WishlistItems.Remove(item);
                added = false;
            }
            await _db.SaveChangesAsync();
        }

        _cache = null; // force a fresh read for the count
        var count = await CountAsync();
        return new WishlistToggleResult(true, added, count,
            added ? "Added to your wishlist" : "Removed from your wishlist");
    }

    /// <summary>After login/registration: move the guest (Session) wishlist into the user's saved wishlist.</summary>
    public async Task MergeGuestWishlistAsync(string userId)
    {
        var guest = Session.GetObject<List<int>>(WishKey);
        if (guest == null || guest.Count == 0) return;

        var existing = await _db.WishlistItems.Where(w => w.UserId == userId).Select(w => w.ProductId).ToListAsync();
        var validIds = await _db.Products.Where(p => p.IsActive && guest.Contains(p.Id)).Select(p => p.Id).ToListAsync();

        foreach (var pid in validIds.Where(id => !existing.Contains(id)))
            _db.WishlistItems.Add(new WishlistItem { UserId = userId, ProductId = pid });

        await _db.SaveChangesAsync();
        Session.Remove(WishKey);
        _cache = null;
    }
}
