using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace StyleMart.Models;

public class ProductReview
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    [ValidateNever]
    public Product? Product { get; set; }

    /// <summary>User who left the review (null = anonymous / guest won't happen here).</summary>
    public string? UserId { get; set; }

    [ValidateNever]
    public ApplicationUser? User { get; set; }

    /// <summary>Display name stored at review time (so it survives account deletion).</summary>
    [Required, StringLength(80)]
    public string ReviewerName { get; set; } = "";

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [StringLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Admin can hide inappropriate reviews.</summary>
    public bool IsVisible { get; set; } = true;
}
