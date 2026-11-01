using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace StyleMart.Models;

public class Product
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [ValidateNever]
    public Category? Category { get; set; }

    [Range(1, 1000000)]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Range(0, 100)]
    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercentage { get; set; } = 0;

    /// <summary>Selling price after discount (whole rupees).</summary>
    [NotMapped]
    public decimal DiscountedPrice =>
        Math.Round(Price - (Price * DiscountPercentage / 100), 0);

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Used only when the product has no colour/size variants.</summary>
    [Range(0, 100000)]
    public int Stock { get; set; }

    public bool IsActive { get; set; } = true;

    [ValidateNever]
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();

    /// <summary>Multiple product images (gallery).</summary>
    [ValidateNever]
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    /// <summary>Total sellable stock: variant stock when variants exist, otherwise Stock.</summary>
    [NotMapped]
    public int TotalStock => Variants.Count > 0 ? Variants.Sum(v => v.Stock) : Stock;
}

/// <summary>Additional product gallery images.</summary>
public class ProductImage
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    [Required, StringLength(500)]
    public string ImageUrl { get; set; } = "";

    /// <summary>Display order (lower = first).</summary>
    public int SortOrder { get; set; }

    [ValidateNever]
    public Product Product { get; set; } = null!;
}
