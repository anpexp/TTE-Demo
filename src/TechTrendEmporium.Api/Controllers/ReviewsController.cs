using Logica.Interfaces;
using Logica.Models.Review.Requests;
using Logica.Models.Review.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace TechTrendEmporium.Api.Controllers
{
    [ApiController]
    [Route("api/store/products/{productId:guid}/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _service;
        public ReviewsController(IReviewService service) => _service = service;

       
        [HttpGet]
        // [Authorize] // <- descomenta si quieres exigir token
        [SwaggerOperation(Tags = new[] { "Products" })]
        [ProducesResponseType(typeof(ReviewsResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(Guid productId, CancellationToken ct)
            => Ok(await _service.GetByProductAsync(productId, ct));

        // POST api/store/products/{productId}/reviews/add
        [HttpPost("add")]
        // [Authorize]
        [SwaggerOperation(Tags = new[] { "Products" })]
        [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Post(Guid productId, [FromBody] ReviewCreateDto body, CancellationToken ct)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            var created = await _service.AddAsync(productId, body, ct);
            return CreatedAtAction(nameof(Get), new { productId }, created);
        }
    }
}
