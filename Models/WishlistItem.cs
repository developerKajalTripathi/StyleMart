using System.ComponentModel.DataAnnotations;

namespace StyleMart.Models;

/// <summary>A product a logged-in customer has liked (hearted). Guests keep their wishlist in Session.</summary>
public class WishlistItem
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = "";

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
