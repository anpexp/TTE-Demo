using External.FakeStore;
using External.FakeStore.Models;
using Logica.Models;
using Logica.Interfaces;
using Logica.Mappers;
using Data;
using Data.Entities;
using Data.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Logica.Models.Products;

namespace Logica.Services
{
   
    public class ProductService : IProductService
    {
        private readonly IFakeStoreApiService _fakeStoreClient;
        private readonly IProductRepository _productRepository;
        private readonly AppDbContext _context;
        private readonly ILogger<ProductService> _logger;

        public ProductService(
    IFakeStoreApiService fakeStoreClient,
    IProductRepository productRepository,
    AppDbContext context,
    ILogger<ProductService> logger)
{
    _fakeStoreClient = fakeStoreClient;
    _productRepository = productRepository;
    _context = context;
    _logger = logger;
}

        #region CRUD Operations (Local Database)

        public async Task<IEnumerable<ProductDto>> GetAllProductsAsync()
        {
            var products = await _productRepository.GetAllAsync();
            return products.Select(p => p.ToProductDto());
        }

        public async Task<IEnumerable<ProductDto>> GetApprovedProductsAsync()
        {
            var products = await _productRepository.GetByStateAsync(ApprovalState.Approved);
            return products.Select(p => p.ToProductDto());
        }

        public async Task<ProductDto?> GetProductByIdAsync(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            return product?.ToProductDto();
        }

        public async Task<ProductDto> CreateProductAsync(ProductCreateDto productDto, Guid createdBy)
        {
            try
            {
                // Validate that the createdBy user exists, fallback to system user if not
                var systemUserId = new Guid("00000000-0000-0000-0000-000000000001");
                var userExists = await _context.Users.AnyAsync(u => u.Id == createdBy);
                if (!userExists)
                {
                    _logger.LogWarning("User {UserId} not found for product creation, using system user", createdBy);
                    createdBy = systemUserId;
                }

                // Buscar o crear categoría
                var category = await GetOrCreateCategoryAsync(productDto.Category, createdBy);

                // Crear producto
                var product = productDto.ToProduct();
                product.CategoryId = category.Id;
                product.CreatedBy = createdBy;
                product.State = ApprovalState.PendingApproval; // Productos manuales necesitan aprobación

                var createdProduct = await _productRepository.CreateAsync(product);
                
                _logger.LogInformation("Producto creado: {Title} por usuario {UserId}", 
                    product.Title, createdBy);

                return createdProduct.ToProductDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando producto: {Title}", productDto.Title);
                throw;
            }
        }

        public async Task<ProductDto?> UpdateProductAsync(Guid id, ProductUpdateDto productDto)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null) return null;

                // Si se cambia la categoría, buscar o crear la nueva
                if (!string.IsNullOrEmpty(productDto.Category))
                {
                    var category = await GetOrCreateCategoryAsync(productDto.Category, product.CreatedBy);
                    product.CategoryId = category.Id;
                }

                // Actualizar producto usando extension method
                product.UpdateProduct(productDto);
                
                var updatedProduct = await _productRepository.UpdateAsync(product);
                
                _logger.LogInformation("Producto actualizado: {ProductId}", id);
                
                return updatedProduct.ToProductDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando producto {ProductId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteProductAsync(Guid id)
        {
            try
            {
                var result = await _productRepository.DeleteAsync(id);
                
                if (result)
                {
                    _logger.LogInformation("Producto eliminado (soft delete): {ProductId}", id);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando producto {ProductId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<ProductDto>> SearchProductsAsync(string searchTerm)
        {
            var products = await _productRepository.SearchAsync(searchTerm);
            return products.Select(p => p.ToProductDto());
        }

        #endregion

        #region FakeStore API Operations

        public async Task<IEnumerable<ProductDto>> GetProductsFromFakeStoreAsync()
        {
            var fakeStoreProducts = await _fakeStoreClient.GetProductsAsync();
            return fakeStoreProducts.Select(MapToProductDto);
        }

        public async Task<ProductDto?> GetProductFromFakeStoreAsync(int id)
        {
            var fakeStoreProduct = await _fakeStoreClient.GetProductByIdAsync(id);
            return fakeStoreProduct != null ? MapToProductDto(fakeStoreProduct) : null;
        }

        public async Task<IEnumerable<string>> GetCategoriesFromFakeStoreAsync()
        {
            return await _fakeStoreClient.GetCategoriesAsync();
        }

        public async Task<IEnumerable<ProductDto>> GetProductsByCategoryFromFakeStoreAsync(string category)
        {
            var fakeStoreProducts = await _fakeStoreClient.GetProductsByCategoryAsync(category);
            return fakeStoreProducts.Select(MapToProductDto);
        }

        #endregion

        #region Sync Operations

        public async Task<int> SyncAllFromFakeStoreAsync(Guid createdBy)
        {
            try
            {
                _logger.LogInformation("Iniciando sincronización completa desde FakeStore");

                var fakeStoreProducts = await _fakeStoreClient.GetProductsAsync();
                var importedCount = 0;

                foreach (var fakeStoreProduct in fakeStoreProducts)
                {
                    try
                    {
                        var importedProduct = await ImportProductFromFakeStoreAsync(fakeStoreProduct.Id, createdBy);
                        if (importedProduct != null)
                        {
                            importedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error importando producto {ProductId} desde FakeStore", 
                            fakeStoreProduct.Id);
                    }
                }

                _logger.LogInformation("Sincronización completada: {Count} productos importados", importedCount);
                return importedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sincronización completa");
                throw;
            }
        }

        public async Task<ProductDto?> ImportProductFromFakeStoreAsync(int fakeStoreId, Guid createdBy)
        {
            try
            {
                // Validate that the createdBy user exists, fallback to system user if not
                var systemUserId = new Guid("00000000-0000-0000-0000-000000000001");
                var userExists = await _context.Users.AnyAsync(u => u.Id == createdBy);
                if (!userExists)
                {
                    _logger.LogWarning("User {UserId} not found, using system user for import", createdBy);
                    createdBy = systemUserId;
                }

                // Verificar si ya existe
                var existingProduct = await _productRepository.GetByExternalIdAsync(
                    fakeStoreId.ToString(), ExternalSource.FakeStore);

                if (existingProduct != null)
                {
                    _logger.LogInformation("Producto {ProductId} ya existe en BD", fakeStoreId);
                    return existingProduct.ToProductDto();
                }

                // Obtener desde FakeStore
                var fakeStoreProduct = await _fakeStoreClient.GetProductByIdAsync(fakeStoreId);
                if (fakeStoreProduct == null)
                {
                    _logger.LogWarning("Producto {ProductId} no encontrado en FakeStore", fakeStoreId);
                    return null;
                }

                // Buscar o crear categoría
                var category = await GetOrCreateCategoryAsync(fakeStoreProduct.Category, createdBy);

                // Crear producto
                var product = new Product
                {
                    Title = fakeStoreProduct.Title,
                    Price = fakeStoreProduct.Price,
                    Description = fakeStoreProduct.Description,
                    ImageUrl = fakeStoreProduct.Image,
                    CategoryId = category.Id,
                    CreatedBy = createdBy,
                    State = ApprovalState.Approved, // Auto-aprobar productos importados
                    RatingAverage = (decimal)(fakeStoreProduct.Rating?.Rate ?? 0),
                    RatingCount = fakeStoreProduct.Rating?.Count ?? 0,
                    InventoryTotal = 100, // Default inventory for imported products
                    InventoryAvailable = 100,
                    CreatedAt = DateTime.UtcNow
                };

                var savedProduct = await _productRepository.CreateAsync(product);

                // Crear ExternalMapping
                var mapping = new ExternalMapping
                {
                    Source = ExternalSource.FakeStore,
                    SourceType = "PRODUCT",
                    SourceId = fakeStoreId.ToString(),
                    InternalId = savedProduct.Id,
                    SnapshotJson = System.Text.Json.JsonSerializer.Serialize(fakeStoreProduct),
                    ImportedAt = DateTime.UtcNow
                };

                _context.ExternalMappings.Add(mapping);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Producto importado: {Title} (FakeStore ID: {FakeStoreId})", 
                    product.Title, fakeStoreId);

                return savedProduct.ToProductDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importando producto {ProductId} desde FakeStore", fakeStoreId);
                throw;
            }
        }

        #endregion

        #region Approval Operations

        public async Task<bool> ApproveProductAsync(Guid id, Guid approvedBy)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null) return false;

                product.State = ApprovalState.Approved;
                product.ApprovedBy = approvedBy;
                product.ApprovedAt = DateTime.UtcNow;

                await _productRepository.UpdateAsync(product);

                _logger.LogInformation("Producto {ProductId} aprobado por {ApprovedBy}", id, approvedBy);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aprobando producto {ProductId}", id);
                throw;
            }
        }

        public async Task<bool> RejectProductAsync(Guid id)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null) return false;

                product.State = ApprovalState.Deleted;
                await _productRepository.UpdateAsync(product);

                _logger.LogInformation("Producto {ProductId} rechazado", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rechazando producto {ProductId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<ProductDto>> GetPendingApprovalAsync()
        {
            var products = await _productRepository.GetByStateAsync(ApprovalState.PendingApproval);
            return products.Select(p => p.ToProductDto());
        }

        public async Task<IEnumerable<ProductSummaryDto>> GetProductsByUserIdAsync(Guid userId)
        {
            try
            {
                var products = await _productRepository.GetByCreatorIdAsync(userId);
                return products.Select(p => p.ToSummaryDto());

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products for user {UserId}", userId);
                throw;
            }
        }

        #endregion

        #region Helper Methods

        private async Task<Category> GetOrCreateCategoryAsync(string categoryName, Guid createdBy)
        {
            // Validate that the createdBy user exists, fallback to system user if not
            var systemUserId = new Guid("00000000-0000-0000-0000-000000000001");
            var userExists = await _context.Users.AnyAsync(u => u.Id == createdBy);
            if (!userExists)
            {
                _logger.LogWarning("User {UserId} not found for category creation, using system user", createdBy);
                createdBy = systemUserId;
            }

            var existing = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name.ToLower() == categoryName.ToLower());

            if (existing != null)
                return existing;

            var category = new Category
            {
                Name = categoryName,
                Slug = GenerateSlug(categoryName),
                State = ApprovalState.Approved, // Auto-approve categories from FakeStore
                CreatedBy = createdBy,
                ApprovedBy = createdBy, // Auto-approve with same user
                CreatedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow // Auto-approve
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Categoría creada: {CategoryName}", categoryName);
            return category;
        }

        private static string GenerateSlug(string name)
        {
            return name.ToLower()
                      .Replace(" ", "-")
                      .Replace("ñ", "n")
                      .Replace("á", "a")
                      .Replace("é", "e")
                      .Replace("í", "i")
                      .Replace("ó", "o")
                      .Replace("ú", "u")
                      .Trim();
        }

        private static ProductDto MapToProductDto(FakeStoreProductResponse fakeStoreProduct)
        {
            return new ProductDto
            {
                Id = Guid.NewGuid(),
                Title = fakeStoreProduct.Title,
                Price = fakeStoreProduct.Price,
                Description = fakeStoreProduct.Description,
                Category = fakeStoreProduct.Category,
                Image = fakeStoreProduct.Image,
                Rating = fakeStoreProduct.Rating != null
                    ? new RatingDto { Rate = fakeStoreProduct.Rating.Rate, Count = fakeStoreProduct.Rating.Count }
                    : null
            };
        }

        #endregion

    }
}