using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace StyleMart.Models;

public class Address
{
    public int Id { get; set; }

    [ValidateNever]
    public string UserId { get; set; } = "";

    [Required(ErrorMessage = "Please enter the full name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Please enter a mobile number.")]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit mobile number.")]
    [Display(Name = "Phone number")]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Please enter a pincode.")]
    [RegularExpression(@"^[1-9]\d{5}$", ErrorMessage = "Enter a valid 6-digit pincode.")]
    public string Pincode { get; set; } = "";

    [Required(ErrorMessage = "Please enter the address."), StringLength(250)]
    [Display(Name = "Address")]
    public string AddressLine { get; set; } = "";

    [Required(ErrorMessage = "Please enter the city."), StringLength(60)]
    public string City { get; set; } = "";

    [Required(ErrorMessage = "Please enter the state."), StringLength(60)]
    public string State { get; set; } = "";

    public bool IsDefault { get; set; }

    public string OneLine => $"{AddressLine}, {City}, {State} - {Pincode}";
}
