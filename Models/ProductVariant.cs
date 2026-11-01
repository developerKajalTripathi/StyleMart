using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace StyleMart.Models
{
    public class ProductVariant
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        [Required]
        public string Color { get; set; } = "";

        [Required]
        public string Size { get; set; } = "";

        [Range(0, 100000)]
        public int Stock { get; set; }

        /// <summary>Stock Keeping Unit – unique identifier for this variant (e.g. SM-RED-XL).</summary>
        [StringLength(100)]
        public string? Sku { get; set; }

        public string? ImageUrl { get; set; }

        [ForeignKey(nameof(ProductId)), ValidateNever]
        public Product Product { get; set; } = null!;
    }
}
