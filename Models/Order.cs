using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StyleMart.Models;

public class Order
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    [StringLength(100)]
    public string CustomerName { get; set; } = "";

    [StringLength(256)]
    public string Email { get; set; } = "";

    [StringLength(20)]
    public string Phone { get; set; } = "";

    /// <summary>Delivery address snapshot taken at the time of the order.</summary>
    [StringLength(400)]
    public string Address { get; set; } = "";

    // ---------- GPS location (captured at checkout) ----------
    [Column(TypeName = "decimal(9,6)")]
    public decimal? Latitude { get; set; }

    [Column(TypeName = "decimal(9,6)")]
    public decimal? Longitude { get; set; }

    [StringLength(200)]
    public string? DetectedCity { get; set; }

    // ----------------------------------------------------------

    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    [StringLength(20)]
    public string PaymentMethod { get; set; } = "COD";

    [StringLength(20)]
    public string PaymentStatus { get; set; } = "Pending";

    /// <summary>Razorpay payment ID (for online payments).</summary>
    [StringLength(100)]
    public string? RazorpayPaymentId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = "Processing";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // ---------- Flipkart-style delivery timeline ----------
    public DateTime? ShippedAt { get; set; }
    public DateTime? OutForDeliveryAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    /// <summary>Expected delivery date shown to customer.</summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>Current delivery location / courier message.</summary>
    [StringLength(300)]
    public string? TrackingMessage { get; set; }
    // -------------------------------------------------------

    public List<OrderItem> Items { get; set; } = new();

    [NotMapped]
    public string OrderNumber => $"SM-{Id}";
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }

    [StringLength(100)]
    public string ProductName { get; set; } = "";

    [StringLength(80)]
    public string? VariantInfo { get; set; }

    public string? ImageUrl { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Mrp { get; set; }

    public int Quantity { get; set; }

    [NotMapped]
    public decimal LineTotal => UnitPrice * Quantity;
}
