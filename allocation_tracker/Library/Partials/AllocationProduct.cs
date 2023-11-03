namespace Library;

#pragma warning disable 108, 114, 472, 612, 1573, 1591, 8073, 3016, 8603
public partial class AllocationProduct
{
    public ICollection<Product> MappedProducts { get; set; }

    public ICollection<Product> AllProducts
    {
        get
        {
            if (Product != null)
                return new[] { Product };

            if (MappedProducts != null) return MappedProducts;

            return Array.Empty<Product>();
        }
    }
}