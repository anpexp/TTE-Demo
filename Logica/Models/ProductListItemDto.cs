namespace Logica.Models.Products
{
    public sealed class ProductListItemDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public decimal Price { get; set; }
        public string Category { get; set; } = "";
        public string Image { get; set; } = "";
        public string Description { get; set; } = "";
        public RatingDto Rating { get; set; } = new(); 
    }
}
