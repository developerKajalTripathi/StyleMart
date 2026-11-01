using Microsoft.AspNetCore.Identity;

namespace StyleMart.Models;

public class ApplicationUser : IdentityUser
//public class ApplicationUser : IdentityUser<int>
{
    public string FullName { get; set; } = "";


}
