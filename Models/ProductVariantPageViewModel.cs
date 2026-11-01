namespace StyleMart.Models
{
    public class ProductVariantPageViewModel
    {
        public ProductVariant Form { get; set; } = new();

        public List<Product> Products { get; set; } = new();

        public List<ProductVariant> Variants { get; set; } = new();
    }
}
