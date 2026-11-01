using Microsoft.EntityFrameworkCore;
using StyleMart.Data;
using StyleMart.Models;

namespace StyleMart.Services;

public record OrderResult(Order? Order, string? Error);

public class OrderService
{
    private readonly StyleMartContext _db;
    public OrderService(StyleMartContext db) => _db = db;

    public async Task<OrderResult> PlaceOrderAsync(
        ApplicationUser user,
        Address address,
        CartViewModel checkout,
        string paymentMethod,
        string? razorpayPaymentId = null,
        decimal? latitude = null,
        decimal? longitude = null,
        string? detectedCity = null)
    {
        if (checkout.Lines.Count == 0) return new OrderResult(null, "Your cart is empty.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        // Reserve stock atomically: the UPDATE only succeeds if enough stock is left.
        foreach (var line in checkout.Lines)
        {
            int qty = line.Quantity;
            int rows;
            if (line.ProductVariantId.HasValue)
            {
                int variantId = line.ProductVariantId.Value;
                rows = await _db.ProductVariants
                    .Where(v => v.Id == variantId && v.Stock >= qty)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, v => v.Stock - qty));
            }
            else
            {
                int productId = line.ProductId;
                rows = await _db.Products
                    .Where(p => p.Id == productId && p.Stock >= qty)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - qty));
            }

            if (rows == 0)
            {
                await tx.RollbackAsync();
                return new OrderResult(null, $"Sorry, '{line.Name}' no longer has enough stock.");
            }
        }

        var order = new Order
        {
            UserId = user.Id,
            CustomerName = address.FullName,
            Email = user.Email ?? "",
            Phone = address.Phone,
            Address = address.OneLine,
            Subtotal = checkout.Mrp,
            Discount = checkout.Savings,
            Total = checkout.Total,
            PaymentMethod = paymentMethod,
            PaymentStatus = paymentMethod == "Online" ? "Paid" : "Pending",
            RazorpayPaymentId = razorpayPaymentId,
            Status = "Processing",
            CreatedAt = DateTime.Now,
            Latitude = latitude,
            Longitude = longitude,
            DetectedCity = detectedCity,
            ExpectedDeliveryDate = DateTime.Now.AddDays(5) // default 5 days delivery estimate
        };
        foreach (var line in checkout.Lines)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = line.Name,
                VariantInfo = line.VariantInfo,
                ImageUrl = line.ImageUrl,
                UnitPrice = line.UnitPrice,
                Mrp = line.Mrp,
                Quantity = line.Quantity
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return new OrderResult(order, null);
    }



    /// <summary>Marks the order as cancelled and puts the stock back. Returns false if it was already cancelled.</summary>
    public async Task<bool> CancelAsync(Order order)
    {
        if (order.Status == "Cancelled") return false;

        await using var tx = await _db.Database.BeginTransactionAsync();
        foreach (var item in order.Items)
        {
            int qty = item.Quantity;
            if (item.ProductVariantId.HasValue)
            {
                int variantId = item.ProductVariantId.Value;
                await _db.ProductVariants.Where(v => v.Id == variantId)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, v => v.Stock + qty));
            }
            else
            {
                int productId = item.ProductId;
                await _db.Products.Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + qty));
            }
        }
        order.Status = "Cancelled";
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }

    /// <summary>Updates order tracking status with timestamps (like Flipkart).</summary>
    public async Task UpdateTrackingAsync(Order order, string newStatus, string? trackingMessage = null)
    {
        order.Status = newStatus;
        if (trackingMessage != null) order.TrackingMessage = trackingMessage;

        switch (newStatus)
        {
            case "Shipped":
                order.ShippedAt ??= DateTime.Now;
                order.ExpectedDeliveryDate = DateTime.Now.AddDays(3);
                break;
            case "Out for Delivery":
                order.OutForDeliveryAt ??= DateTime.Now;
                order.ExpectedDeliveryDate = DateTime.Now;
                break;
            case "Delivered":
                order.DeliveredAt ??= DateTime.Now;
                if (order.PaymentMethod == "COD") order.PaymentStatus = "Paid";
                break;
        }

        await _db.SaveChangesAsync();
    }
}
