using System.ComponentModel.DataAnnotations;

namespace StyleMart.Models;

/// <summary>Cart row stored in the database for logged-in users.</summary>
public class CartItem
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = "";

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public int Quantity { get; set; } = 1;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Light-weight cart line kept in Session for guests (and for "Buy Now").</summary>
public class SessionCartLine
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public int Quantity { get; set; } = 1;
}
