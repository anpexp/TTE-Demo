using Data;
using Data.Entities;
using Logica.Interfaces;
using Logica.Mappers;
using Logica.Models.Review.Responses;
using Logica.Models.Review.Requests;
using Microsoft.EntityFrameworkCore;

namespace Logica.Services
{
    public class ReviewService : IReviewService
    {
        private readonly IReviewRepository _reviews;
        private readonly IUserRepository _users;
        private readonly IProductRepository _products;
        private readonly AppDbContext _db;

        public ReviewService(
            IReviewRepository reviews,
            IUserRepository users,
            IProductRepository products,
            AppDbContext db)
        {
            _reviews = reviews;
            _users = users;
            _products = products;
            _db = db;
        }

        public async Task<ReviewsResponseDto> GetByProductAsync(Guid productId, CancellationToken ct = default)
        {
            var list = await _reviews.GetByProductAsync(productId, ct);
            return new ReviewsResponseDto
            {
                Product_Id = productId,
                Reviews = list.Select(x => x.ToDto()).ToList()
            };
        }

        public async Task<ReviewDto> AddAsync(Guid productId, ReviewCreateDto dto, CancellationToken ct = default)
        {
            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);

                try
                {
                    // 1) Validar producto
                    var product = await _products.GetByIdAsync(productId);
                    if (product is null)
                        throw new KeyNotFoundException($"Product '{productId}' not found.");

                    // 2) Resolver usuario por username
                    var user = await _users.GetByUsernameAsync(dto.User, ct);
                    if (user is null)
                        throw new KeyNotFoundException($"User '{dto.User}' not found.");

                    // 3) Evitar review duplicada del mismo usuario
                    var alreadyReviewed = await _db.Reviews
                        .AsNoTracking()
                        .AnyAsync(r => r.ProductId == productId && r.UserId == user.Id, ct);

                    if (alreadyReviewed)
                        throw new InvalidOperationException("User has already reviewed this product.");

                    // 4) Crear review
                    var entity = new Review
                    {
                        ProductId = productId,
                        UserId = user.Id,
                        Rating = dto.Rating,
                        Comment = dto.Comment
                    };

                    // Guardar review a través del repositorio (mismo DbContext/Scope)
                    var saved = await _reviews.AddAsync(entity, ct);

                    // 5) Recalcular agregados incrementalmente
                    var newCount = product.RatingCount + 1;
                    var sum = (decimal)product.RatingAverage * product.RatingCount + dto.Rating;
                    product.RatingCount = newCount;
                    product.RatingAverage = Math.Round(sum / newCount, 1, MidpointRounding.AwayFromZero);
                    product.UpdatedAt = DateTime.UtcNow;

                    await _products.UpdateAsync(product);

                    await tx.CommitAsync(ct);

                    return saved.ToDto();
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            });
        }
    }
}