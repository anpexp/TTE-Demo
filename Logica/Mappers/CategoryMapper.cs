using Data.Entities;
using Logica.Models.Category.Requests;
using Logica.Models.Category.Responses;

namespace Logica.Mappers
{
  
    public static class CategoryMapper
    {
        public static CategoryDto ToCategoryDto(this Category category)
        {
            return new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                State = category.State.ToString(),
                CreatedBy = category.CreatedBy,
                ApprovedBy = category.ApprovedBy,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt,
                ApprovedAt = category.ApprovedAt,
                CreatorName = category.Creator?.Username ?? string.Empty,
                ApproverName = category.Approver?.Username,
                ProductCount = category.Products?.Count ?? 0
            };
        }

        public static Category ToCategory(this CategoryCreateDto createDto)
        {
            return new Category
            {
                Name = createDto.Name.Trim(),
                Slug = createDto.Slug.Trim().ToLower(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static void UpdateCategory(this Category category, CategoryUpdateDto updateDto)
        {
            if (!string.IsNullOrWhiteSpace(updateDto.Name))
                category.Name = updateDto.Name.Trim();
            
            if (!string.IsNullOrWhiteSpace(updateDto.Slug))
                category.Slug = updateDto.Slug.Trim().ToLower();
            
            category.UpdatedAt = DateTime.UtcNow;
        }
    }
}