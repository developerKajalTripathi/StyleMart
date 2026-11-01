using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StyleMart.Models;

namespace StyleMart.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp, IConfiguration config)
    {
        var db = sp.GetRequiredService<StyleMartContext>();
        await db.Database.MigrateAsync();

        // Roles
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Customer" })
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        // Admin user (change the password in appsettings.json -> AdminSeed)
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var adminUserName = config["AdminSeed:UserName"] ?? "admin";
        var adminEmail = config["AdminSeed:Email"] ?? "admin@stylemart.com";
        var adminPassword = config["AdminSeed:Password"] ?? "Admin@123";
        var admin = await users.FindByNameAsync(adminUserName);
        if (admin == null)
        {
            admin = new ApplicationUser { UserName = adminUserName, Email = adminEmail, EmailConfirmed = true, FullName = "Store Admin" };
            var created = await users.CreateAsync(admin, adminPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException("Could not create admin user: " + string.Join("; ", created.Errors.Select(e => e.Description)));
        }
        if (!await users.IsInRoleAsync(admin, "Admin"))
            await users.AddToRoleAsync(admin, "Admin");

        // Categories + sample products
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "Women" },
                new Category { Name = "Men" },
                new Category { Name = "Footwear" },
                new Category { Name = "Accessories" });
            await db.SaveChangesAsync();
        }

        if (!await db.Products.AnyAsync())
        {
            int Cat(string name) => db.Categories.First(c => c.Name == name).Id;
            const string img = "?auto=format&fit=crop&w=800&q=85";

            var products = new List<Product>
            {
                new() { Name = "Classic Denim Jacket", CategoryId = Cat("Women"), Price = 2499, DiscountPercentage = 25, Stock = 0, Description = "Everyday denim jacket with a modern fit.", ImageUrl = "https://images.unsplash.com/photo-1543076447-215ad9ba6923" + img },
                new() { Name = "Floral Summer Dress", CategoryId = Cat("Women"), Price = 1799, DiscountPercentage = 30, Stock = 0, Description = "Lightweight floral dress for everyday style.", ImageUrl = "https://images.unsplash.com/photo-1595777457583-95e059d581b8" + img },
                new() { Name = "Casual Cotton T-Shirt", CategoryId = Cat("Men"), Price = 999, DiscountPercentage = 30, Stock = 0, Description = "Soft cotton tee for a comfortable everyday look.", ImageUrl = "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab" + img },
                new() { Name = "Relaxed Fit Hoodie", CategoryId = Cat("Men"), Price = 1999, DiscountPercentage = 25, Stock = 0, Description = "A relaxed hoodie for cool days and casual outfits.", ImageUrl = "https://images.unsplash.com/photo-1556821840-3a63f95609a7" + img },
                new() { Name = "Minimal White Sneakers", CategoryId = Cat("Footwear"), Price = 2999, DiscountPercentage = 27, Stock = 0, Description = "Versatile sneakers to pair with your favourite outfits.", ImageUrl = "https://images.unsplash.com/photo-1542291026-7eec264c27ff" + img },
                new() { Name = "Everyday Tote Bag", CategoryId = Cat("Accessories"), Price = 899, DiscountPercentage = 0, Stock = 25, Description = "A spacious everyday bag with a clean silhouette.", ImageUrl = "https://images.unsplash.com/photo-1548036328-c9fa89d128fa" + img }
            };

            // Clothing gets colour/size variants; the tote bag has none (uses Product.Stock).
            foreach (var p in products.Where(p => p.Name != "Everyday Tote Bag"))
            {
                var isShoe = p.CategoryId == Cat("Footwear");
                var colors = isShoe ? new[] { "White" } : new[] { "Black", "Blue" };
                var sizes = isShoe ? new[] { "7", "8", "9", "10" } : new[] { "S", "M", "L", "XL" };
                foreach (var c in colors)
                    foreach (var s in sizes)
                        p.Variants.Add(new ProductVariant { Color = c, Size = s, Stock = 8 });
            }

            db.Products.AddRange(products);
            await db.SaveChangesAsync();
        }
    }
}
