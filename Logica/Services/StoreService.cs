using Data;
using Data.Entities.Enums;
using Logica.Mappers;
using Logica.Models;
using Logica.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace Logica.Services
{
    public interface IStoreService
    {
        Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductQuery query, CancellationToken ct);
    }

    public sealed class StoreService : IStoreService
    {
        private readonly AppDbContext _db;
        public StoreService(AppDbContext db) => _db = db;

        public async Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductQuery q, CancellationToken ct)
        {
            // Normaliza página y tamaño (sin usar 'with', porque no es record)
            var page = q.Page <= 0 ? 1 : q.Page;
            var size = (q.PageSize <= 0 || q.PageSize > 100) ? 12 : q.PageSize;

            var qry = _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.State == ApprovalState.Approved)
                .AsQueryable();

            // Filtros
            if (!string.IsNullOrWhiteSpace(q.Title))
            {
                var term = q.Title.Trim();
                qry = qry.Where(p => EF.Functions.Like(p.Title, $"%{term}%"));
            }
            if (q.Price.HasValue)
            {
                qry = qry.Where(p => p.Price <= q.Price.Value);
            }

            // Orden (enums)
            var desc = q.SortDir == SortDirection.Desc;
            qry = (q.SortBy, desc) switch
            {
                (ProductSortBy.Price, false) => qry.OrderBy(p => p.Price).ThenBy(p => p.Title),
                (ProductSortBy.Price, true) => qry.OrderByDescending(p => p.Price).ThenBy(p => p.Title),

                (ProductSortBy.Rating, false) => qry.OrderBy(p => p.RatingAverage)
                                                    .ThenByDescending(p => p.RatingCount)
                                                    .ThenBy(p => p.Title),
                (ProductSortBy.Rating, true) => qry.OrderByDescending(p => p.RatingAverage)
                                                    .ThenByDescending(p => p.RatingCount)
                                                    .ThenBy(p => p.Title),

                (ProductSortBy.Title, true) => qry.OrderByDescending(p => p.Title),
                _ => qry.OrderBy(p => p.Title)
            };

            // Paginación
            var total = await qry.CountAsync(ct);
            var items = await qry
                .Skip((page - 1) * size)
                .Take(size)
                .Select(p => p.ToListItemDto())
                .ToListAsync(ct);

            return new PagedResult<ProductListItemDto>
            {
                Items = items,
                Page = page,
                PageSize = size,
                TotalItems = total
            };
        }
    }
}
