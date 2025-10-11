namespace Logica.Models.Products
{
    public enum ProductSortBy { Title, Price, Rating }
    public enum SortDirection { Asc, Desc }

    // Clase simple (no record), con tipos correctos
    public sealed class ProductQuery
    {
        public string? Title { get; set; }
        public decimal? Price { get; set; }

        // Paginación
        public int Page { get; set; } = 1;        // 1-based
        public int PageSize { get; set; } = 12;

        // Ordenamiento
        public ProductSortBy SortBy { get; set; } = ProductSortBy.Title;
        public SortDirection SortDir { get; set; } = SortDirection.Asc;
    }
}
