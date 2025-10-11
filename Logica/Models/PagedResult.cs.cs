namespace Logica.Models
{
    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalItems { get; init; }   // <<— necesaria para el initializer del servicio
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public bool HasMore => Page < TotalPages;
    }
}
