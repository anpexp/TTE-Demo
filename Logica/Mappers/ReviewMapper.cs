using Data.Entities;
using Logica.Models.Review.Responses;

namespace Logica.Mappers
{
    public static class ReviewMapper
    {
        public static ReviewDto ToDto(this Review e)
            => new()
            {
                Id = e.Id,
                ProductId = e.ProductId,
                UserId = e.UserId,
                Username = e.User?.Username ?? "(unknown)",
                Rating = e.Rating,
                Comment = e.Comment,
                CreatedAt = e.CreatedAt
            };
    }
}
