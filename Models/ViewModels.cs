using System.ComponentModel.DataAnnotations;

namespace StyleMart.Models;

public class CartLine
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string Name { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Mrp { get; set; }
    public decimal DiscountPercentage { get; set; }
    public int Quantity { get; set; }
    public int Available { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
    public decimal LineMrp => Mrp * Quantity;
    public bool InStock => Available >= Quantity && Available > 0;
    public string? VariantInfo =>
        string.IsNullOrEmpty(Color) && string.IsNullOrEmpty(Size) ? null : $"{Color} / {Size}";
}

public class CartViewModel
{
    public List<CartLine> Lines { get; set; } = new();
    public bool IsBuyNow { get; set; }

    public int ItemCount => Lines.Sum(l => l.Quantity);
    public decimal Mrp => Lines.Sum(l => l.LineMrp);
    public decimal Total => Lines.Sum(l => l.LineTotal);
    public decimal Savings => Mrp - Total;
    public bool HasStockIssues => Lines.Any(l => !l.InStock);
}

public class CheckoutViewModel
{
    public CartViewModel Cart { get; set; } = new();
    public List<Address> Addresses { get; set; } = new();
    public Address NewAddress { get; set; } = new();
}

public class PaymentViewModel
{
    public CartViewModel Cart { get; set; } = new();
    public Address Address { get; set; } = new();
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your email."), EmailAddress]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please choose a password."), StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}
