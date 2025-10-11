using Logica.Models;
using Logica.Models.Products;
using Logica.Services;
using Microsoft.AspNetCore.Mvc;

namespace TechTrendEmporium.Api.Controllers
{
    [ApiController]
    [Route("api/store")]
    public class StoreController : ControllerBase
    {
        private readonly IStoreService _store;
        public StoreController(IStoreService store) => _store = store;

        /// <summary>
        /// F01: Product Display Page con filtros, orden y paginación.
        /// </summary>
        [HttpGet("products")]
        [ProducesResponseType(typeof(PagedResult<ProductListItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProducts(
            [FromQuery] string? title,
            [FromQuery] decimal? price,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] ProductSortBy sortBy = ProductSortBy.Title,   // enum: Title|Price|Rating
            [FromQuery] SortDirection sortDir = SortDirection.Asc,    // enum: Asc|Desc
            CancellationToken ct = default)
        {
            var query = new ProductQuery
            {
                Title = title,
                Price = price,
                Page = page,
                PageSize = pageSize,
                SortBy = sortBy,
                SortDir = sortDir
            };

            var result = await _store.GetProductsAsync(query, ct);
            return Ok(result); // usa Ok(result.Items) si quieres un array "puro"
        }
    }
}
