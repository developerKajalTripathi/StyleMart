using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace StyleMart.Models;

public class Category
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string Name { get; set; } = "";

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [ValidateNever]
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
