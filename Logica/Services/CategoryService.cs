using Data.Entities;
using Data.Entities.Enums;
using External.FakeStore;
using Logica.Interfaces;
using Logica.Mappers;
using Logica.Models.Category.Requests;
using Logica.Models.Category.Responses;

namespace Logica.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IFakeStoreApiService _fakeStoreApiService;
        private readonly IUserService _userService;

        public CategoryService(
            ICategoryRepository categoryRepository,
            IFakeStoreApiService fakeStoreApiService,
            IUserService userService)
        {
            _categoryRepository = categoryRepository;
            _fakeStoreApiService = fakeStoreApiService;
            _userService = userService;
        }

        #region CRUD Operations

        public async Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync()
        {
            var categories = await _categoryRepository.GetAllAsync();
            return categories.Select(c => c.ToCategoryDto());
        }

        public async Task<IEnumerable<CategoryDto>> GetApprovedCategoriesAsync()
        {
            var categories = await _categoryRepository.GetApprovedAsync();
            return categories.Select(c => c.ToCategoryDto());
        }

        public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            return category?.ToCategoryDto();
        }

        public async Task<CategoryDto?> GetCategoryBySlugAsync(string slug)
        {
            var category = await _categoryRepository.GetBySlugAsync(slug);
            return category?.ToCategoryDto();
        }

        public async Task<CategoryDto> CreateCategoryAsync(CategoryCreateDto categoryDto, Guid createdBy)
        {
            // Business validations
            if (await _categoryRepository.ExistsByNameAsync(categoryDto.Name))
                throw new InvalidOperationException("A category with that name already exists");

            if (await _categoryRepository.ExistsBySlugAsync(categoryDto.Slug))
                throw new InvalidOperationException("A category with that slug already exists");

            // Validate that user exists
            var user = await _userService.GetUserByIdAsync(createdBy);
            if (user == null)
                throw new InvalidOperationException("User not found");

            var category = categoryDto.ToCategory();
            category.CreatedBy = createdBy;
            category.State = ApprovalState.PendingApproval;

            var createdCategory = await _categoryRepository.AddAsync(category);
            return createdCategory.ToCategoryDto();
        }

        public async Task<CategoryDto?> UpdateCategoryAsync(Guid id, CategoryUpdateDto categoryDto)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
                return null;

            // Business validations
            if (!string.IsNullOrWhiteSpace(categoryDto.Name) &&
                await _categoryRepository.ExistsByNameAsync(categoryDto.Name, id))
                throw new InvalidOperationException("A category with that name already exists");

            if (!string.IsNullOrWhiteSpace(categoryDto.Slug) &&
                await _categoryRepository.ExistsBySlugAsync(categoryDto.Slug, id))
                throw new InvalidOperationException("A category with that slug already exists");

            category.UpdateCategory(categoryDto);
            var updatedCategory = await _categoryRepository.UpdateAsync(category);
            return updatedCategory?.ToCategoryDto();
        }

        public async Task<bool> DeleteCategoryAsync(Guid id)
        {
            return await _categoryRepository.DeleteAsync(id);
        }

        public async Task<bool> DeactivateCategoryAsync(Guid id)
        {
            return await _categoryRepository.DeactivateAsync(id);
        }

        #endregion

        #region Search Operations

        public async Task<IEnumerable<CategoryDto>> SearchCategoriesAsync(string searchTerm)
        {
            var categories = await _categoryRepository.SearchAsync(searchTerm);
            return categories.Select(c => c.ToCategoryDto());
        }

        #endregion

        #region Approval Operations

        public async Task<IEnumerable<CategoryDto>> GetPendingApprovalAsync()
        {
            var categories = await _categoryRepository.GetPendingApprovalAsync();
            return categories.Select(c => c.ToCategoryDto());
        }

        public async Task<bool> ApproveCategoryAsync(Guid id, Guid approvedBy)
        {
            // Validate that approver user exists
            var user = await _userService.GetUserByIdAsync(approvedBy);
            if (user == null)
                throw new InvalidOperationException("Approver user not found");

            return await _categoryRepository.ApproveAsync(id, approvedBy);
        }

        public async Task<bool> RejectCategoryAsync(Guid id)
        {
            return await _categoryRepository.RejectAsync(id);
        }

        #endregion

        #region FakeStore API Operations

        public async Task<IEnumerable<string>> GetCategoriesFromFakeStoreAsync()
        {
            return await _fakeStoreApiService.GetCategoriesAsync();
        }

        public async Task<int> SyncCategoriesFromFakeStoreAsync(Guid createdBy)
        {
            var fakeStoreCategories = await _fakeStoreApiService.GetCategoriesAsync();
            var importedCount = 0;

            foreach (var categoryName in fakeStoreCategories)
            {
                // Create slug from name
                var slug = GenerateSlug(categoryName);

                // Check if already exists
                if (await _categoryRepository.ExistsBySlugAsync(slug))
                    continue;

                try
                {
                    var categoryCreateDto = new CategoryCreateDto
                    {
                        Name = CapitalizeWords(categoryName),
                        Slug = slug
                    };

                    await CreateCategoryAsync(categoryCreateDto, createdBy);
                    importedCount++;
                }
                catch (Exception)
                {
                    // Continue with next category if there's an error
                    continue;
                }
            }

            return importedCount;
        }

        #endregion

        #region Helper Methods

        private static string GenerateSlug(string input)
        {
            return input.ToLower()
                       .Replace(" ", "-")
                       .Replace("'", "")
                       .Replace("\"", "")
                       .Replace("&", "and")
                       .Trim();
        }

        private static string CapitalizeWords(string input)
        {
            return string.Join(" ", input.Split(' ')
                .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower()));
        }

        #endregion
    }
}