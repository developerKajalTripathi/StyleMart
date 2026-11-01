using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StyleMart.Data;
using StyleMart.Extensions;
using StyleMart.Models;

namespace StyleMart.Services;

public record CartResult(bool Ok, string Message);

/// <summary>
/// Guests: cart lives in Session. Logged-in users: cart lives in the database (CartItems).
/// "Buy Now" keeps a single line in Session so only that item is checked out.
/// </summary>
public class CartService
{
    public const string CartKey = "StyleMartCart";
    public const string BuyNowKey = "StyleMartBuyNow";
    public const int MaxQtyPerItem = 10;

    private readonly StyleMartContext _db;
    private readonly IHttpContextAccessor _http;

    public CartService(StyleMartContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    private HttpContext Ctx => _http.HttpContext!;
    private ISession Session => Ctx.Session;

    public string? CurrentUserId =>
        Ctx.User.Identity?.IsAuthenticated == true ? Ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    // ---------- reading ----------

    private async Task<List<SessionCartLine>> LoadRawAsync()
    {
        var uid = CurrentUserId;
        if (uid == null)
            return Session.GetObject<List<SessionCartLine>>(CartKey) ?? new List<SessionCartLine>();

        return await _db.CartItems
            .Where(c => c.UserId == uid)
            .OrderBy(c => c.Id)
            .Select(c => new SessionCartLine
            {
                ProductId = c.ProductId,
                ProductVariantId = c.ProductVariantId,
                Quantity = c.Quantity
            })
            .ToListAsync();
    }

    public async Task<CartViewModel> BuildAsync(IEnumerable<SessionCartLine> raw)
    {
        var rawList = raw.ToList();
        var ids = rawList.Select(r => r.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Include(p => p.Variants)
            .Where(p => p.IsActive && ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var vm = new CartViewModel();
        foreach (var r in rawList)
        {
            if (!products.TryGetValue(r.ProductId, out var p)) continue;

            ProductVariant? v = null;
            if (r.ProductVariantId.HasValue)
            {
                v = p.Variants.FirstOrDefault(x => x.Id == r.ProductVariantId.Value);
                if (v == null) continue;
            }
            else if (p.Variants.Count > 0)
            {
                continue; // product now needs a colour/size choice
            }

            vm.Lines.Add(new CartLine
            {
                ProductId = p.Id,
                ProductVariantId = v?.Id,
                Name = p.Name,
                ImageUrl = !string.IsNullOrWhiteSpace(v?.ImageUrl) ? v!.ImageUrl : p.ImageUrl,
                Color = v?.Color,
                Size = v?.Size,
                UnitPrice = p.DiscountedPrice,
                Mrp = p.Price,
                DiscountPercentage = p.DiscountPercentage,
                Quantity = Math.Max(1, r.Quantity),
                Available = v?.Stock ?? p.Stock
            });
        }
        return vm;
    }

    public async Task<CartViewModel> GetCartAsync() => await BuildAsync(await LoadRawAsync());

    /// <summary>The lines being checked out: the Buy Now item if present, otherwise the whole cart.</summary>
    public async Task<CartViewModel> GetCheckoutAsync()
    {
        var buyNow = Session.GetObject<SessionCartLine>(BuyNowKey);
        if (buyNow != null)
        {
            var vm = await BuildAsync(new[] { buyNow });
            vm.IsBuyNow = true;
            return vm;
        }
        return await GetCartAsync();
    }

    public void ClearBuyNow() => Session.Remove(BuyNowKey);

    public async Task<int> CountAsync()
    {
        var uid = CurrentUserId;
        if (uid == null)
            return (Session.GetObject<List<SessionCartLine>>(CartKey) ?? new()).Sum(l => l.Quantity);
        return await _db.CartItems.Where(c => c.UserId == uid).SumAsync(c => (int?)c.Quantity) ?? 0;
    }

    // ---------- helpers ----------

    private async Task<(Product? Product, ProductVariant? Variant, string? Error)> ResolveAsync(int productId, int? variantId)
    {
        var p = await _db.Products.Include(x => x.Variants)
            .FirstOrDefaultAsync(x => x.Id == productId && x.IsActive);
        if (p == null) return (null, null, "This product is no longer available.");

        ProductVariant? v = null;
        if (p.Variants.Count > 0)
        {
            if (variantId == null) return (p, null, "Please select a colour and size.");
            v = p.Variants.FirstOrDefault(x => x.Id == variantId.Value);
            if (v == null) return (p, null, "The selected colour/size is not available.");
        }
        return (p, v, null);
    }

    // ---------- writing ----------

    public async Task<CartResult> AddAsync(int productId, int? variantId, int quantity)
    {
        var (p, v, error) = await ResolveAsync(productId, variantId);
        if (error != null) return new CartResult(false, error);

        int available = v?.Stock ?? p!.Stock;
        if (available <= 0) return new CartResult(false, "Sorry, this item is out of stock.");

        int qty = Math.Clamp(quantity, 1, MaxQtyPerItem);
        int limit = Math.Min(MaxQtyPerItem, available);
        var uid = CurrentUserId;

        if (uid == null)
        {
            var cart = Session.GetObject<List<SessionCartLine>>(CartKey) ?? new();
            var line = cart.FirstOrDefault(x => x.ProductId == productId && x.ProductVariantId == variantId);
            if (line == null) cart.Add(new SessionCartLine { ProductId = productId, ProductVariantId = variantId, Quantity = Math.Min(qty, limit) });
            else line.Quantity = Math.Min(line.Quantity + qty, limit);
            Session.SetObject(CartKey, cart);
        }
        else
        {
            var item = await _db.CartItems.FirstOrDefaultAsync(c =>
                c.UserId == uid && c.ProductId == productId && c.ProductVariantId == variantId);
            if (item == null)
                _db.CartItems.Add(new CartItem { UserId = uid, ProductId = productId, ProductVariantId = variantId, Quantity = Math.Min(qty, limit) });
            else
                item.Quantity = Math.Min(item.Quantity + qty, limit);
            await _db.SaveChangesAsync();
        }
        return new CartResult(true, "Product added to cart!");
    }

    public async Task<CartResult> SetBuyNowAsync(int productId, int? variantId, int quantity)
    {
        var (p, v, error) = await ResolveAsync(productId, variantId);
        if (error != null) return new CartResult(false, error);

        int available = v?.Stock ?? p!.Stock;
        if (available <= 0) return new CartResult(false, "Sorry, this item is out of stock.");

        Session.SetObject(BuyNowKey, new SessionCartLine
        {
            ProductId = productId,
            ProductVariantId = variantId,
            Quantity = Math.Clamp(quantity, 1, Math.Min(MaxQtyPerItem, available))
        });
        return new CartResult(true, "");
    }

    public async Task UpdateQuantityAsync(int productId, int? variantId, int quantity)
    {
        var (p, v, error) = await ResolveAsync(productId, variantId);
        if (error != null) { await RemoveAsync(productId, variantId); return; }

        int available = v?.Stock ?? p!.Stock;
        int qty = Math.Clamp(quantity, 1, Math.Max(1, Math.Min(MaxQtyPerItem, available)));
        var uid = CurrentUserId;

        if (uid == null)
        {
            var cart = Session.GetObject<List<SessionCartLine>>(CartKey) ?? new();
            var line = cart.FirstOrDefault(x => x.ProductId == productId && x.ProductVariantId == variantId);
            if (line != null) line.Quantity = qty;
            Session.SetObject(CartKey, cart);
        }
        else
        {
            var item = await _db.CartItems.FirstOrDefaultAsync(c =>
                c.UserId == uid && c.ProductId == productId && c.ProductVariantId == variantId);
            if (item != null)
            {
                item.Quantity = qty;
                await _db.SaveChangesAsync();
            }
        }
    }

    public async Task RemoveAsync(int productId, int? variantId)
    {
        var uid = CurrentUserId;
        if (uid == null)
        {
            var cart = Session.GetObject<List<SessionCartLine>>(CartKey) ?? new();
            cart.RemoveAll(x => x.ProductId == productId && x.ProductVariantId == variantId);
            Session.SetObject(CartKey, cart);
        }
        else
        {
            var items = await _db.CartItems
                .Where(c => c.UserId == uid && c.ProductId == productId && c.ProductVariantId == variantId)
                .ToListAsync();
            _db.CartItems.RemoveRange(items);
            await _db.SaveChangesAsync();
        }
    }

    public async Task ClearCartAsync(string userId)
    {
        await _db.CartItems.Where(c => c.UserId == userId).ExecuteDeleteAsync();
    }

    /// <summary>After login/registration: move the guest (Session) cart into the user's database cart.</summary>
    public async Task MergeGuestCartAsync(string userId)
    {
        var guest = Session.GetObject<List<SessionCartLine>>(CartKey);
        if (guest == null || guest.Count == 0) return;

        foreach (var g in guest)
        {
            var (p, v, error) = await ResolveAsync(g.ProductId, g.ProductVariantId);
            if (error != null) continue;

            int available = v?.Stock ?? p!.Stock;
            if (available <= 0) continue;
            int limit = Math.Min(MaxQtyPerItem, available);
            var variantId = g.ProductVariantId;

            var existing = await _db.CartItems.FirstOrDefaultAsync(c =>
                c.UserId == userId && c.ProductId == g.ProductId && c.ProductVariantId == variantId);
            if (existing == null)
                _db.CartItems.Add(new CartItem { UserId = userId, ProductId = g.ProductId, ProductVariantId = variantId, Quantity = Math.Min(g.Quantity, limit) });
            else
                existing.Quantity = Math.Min(existing.Quantity + g.Quantity, limit);

            await _db.SaveChangesAsync();
        }
        Session.Remove(CartKey);
    }
}
