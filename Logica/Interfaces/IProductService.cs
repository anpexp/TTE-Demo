using Logica.Models.Products;

namespace Logica.Interfaces
{
    public interface IProductService
    {
        // CRUD Operations para productos locales (BD)
        Task<IEnumerable<ProductDto>> GetAllProductsAsync();
        Task<IEnumerable<ProductDto>> GetApprovedProductsAsync();
        Task<ProductDto?> GetProductByIdAsync(Guid id);
        Task<ProductDto> CreateProductAsync(ProductCreateDto productDto, Guid createdBy);
        Task<ProductDto?> UpdateProductAsync(Guid id, ProductUpdateDto productDto);
        Task<bool> DeleteProductAsync(Guid id);
        Task<IEnumerable<ProductDto>> SearchProductsAsync(string searchTerm);
        Task<IEnumerable<ProductDto>> GetProductsByCategoryIdAsync(Guid categoryId);
        
        // FakeStore API Operations (externos)
        Task<IEnumerable<ProductDto>> GetProductsFromFakeStoreAsync();
        Task<ProductDto?> GetProductFromFakeStoreAsync(int id);
        Task<IEnumerable<string>> GetCategoriesFromFakeStoreAsync();
        Task<IEnumerable<ProductDto>> GetProductsByCategoryFromFakeStoreAsync(string category);
        
        // Sync Operations
        Task<int> SyncAllFromFakeStoreAsync(Guid createdBy);
        Task<ProductDto?> ImportProductFromFakeStoreAsync(int fakeStoreId, Guid createdBy);
        
        // Approval Operations
        Task<bool> ApproveProductAsync(Guid id, Guid approvedBy);
        Task<bool> RejectProductAsync(Guid id);
        Task<IEnumerable<ProductDto>> GetPendingApprovalAsync();

        Task<IEnumerable<ProductSummaryDto>> GetProductsByUserIdAsync(Guid userId);
    }
}