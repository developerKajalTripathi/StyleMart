using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StyleMart.Models;

namespace StyleMart.Data;

public class StyleMartContext : IdentityDbContext<ApplicationUser>
{
    public StyleMartContext(DbContextOptions<StyleMartContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderReturn> OrderReturns { get; set; }
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ===== Rename Identity tables to meaningful names =====
        b.Entity<ApplicationUser>().ToTable("Login");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("Roles");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("UserRoles");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UserClaims");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UserLogins");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("RoleClaims");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UserTokens");

        // ===== App tables =====
        b.Entity<Category>().ToTable("Categories");
        b.Entity<Product>().ToTable("Products");
        b.Entity<ProductImage>().ToTable("ProductImages");
        b.Entity<ProductVariant>().ToTable("ProductVariants");
        b.Entity<CartItem>().ToTable("Cart");
        b.Entity<WishlistItem>().ToTable("Wishlist");
        b.Entity<Address>().ToTable("Addresses");
        b.Entity<Order>().ToTable("Orders");
        b.Entity<OrderItem>().ToTable("OrderItems");
        b.Entity<OrderReturn>().ToTable("OrderReturns");
        b.Entity<ProductReview>().ToTable("ProductReviews");

        b.Entity<Category>().HasIndex(c => c.Name).IsUnique();

        b.Entity<Product>()
            .HasOne(p => p.Category).WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // ProductImage -> Product
        b.Entity<ProductImage>()
            .HasOne(pi => pi.Product).WithMany(p => p.Images)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // CartItem points at both Product and ProductVariant (variant also belongs to product),
        // so one of the two must not cascade or SQL Server rejects the schema.
        b.Entity<CartItem>()
            .HasOne(c => c.Product).WithMany()
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<CartItem>()
            .HasOne(c => c.ProductVariant).WithMany()
            .HasForeignKey(c => c.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<CartItem>()
            .HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<CartItem>().HasIndex(c => new { c.UserId, c.ProductId, c.ProductVariantId });

        b.Entity<WishlistItem>()
            .HasOne(w => w.Product).WithMany()
            .HasForeignKey(w => w.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<WishlistItem>()
            .HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<WishlistItem>().HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();

        b.Entity<Address>()
            .HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Order>()
            .HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<ProductReview>()
            .HasOne(r => r.Product).WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<ProductReview>()
            .HasOne(r => r.User).WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
