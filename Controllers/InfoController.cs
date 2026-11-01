using Microsoft.AspNetCore.Mvc;

namespace StyleMart.Controllers;

/// <summary>Simple static pages linked from the footer.</summary>
public class InfoController : Controller
{
    public IActionResult About() => View();
    public IActionResult Contact() => View();
    public IActionResult Help() => View();
}
