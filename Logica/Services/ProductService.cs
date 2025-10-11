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
        private readonly IExternalMappingRepository _externalMappingRepository;
        private readonly AppDbContext _context;
        private readonly ILogger<ProductService> _logger;

        public ProductService(
    IFakeStoreApiService fakeStoreClient,
    IProductRepository productRepository,
    IExternalMappingRepository externalMappingRepository,
    AppDbContext context,
    ILogger<ProductService> logger)
{
    _fakeStoreClient = fakeStoreClient;
    _productRepository = productRepository;
    _externalMappingRepository = externalMappingRepository;
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
            // Only return approved products for public consumption
            var approvedProducts = products.Where(p => p.State == ApprovalState.Approved);
            return approvedProducts.Select(p => p.ToProductDto());
        }

        public async Task<IEnumerable<ProductDto>> GetProductsByCategoryIdAsync(Guid categoryId)
        {
            try
            {
                var products = await _productRepository.GetByCategoryIdAsync(categoryId);
                // Only return approved products for public consumption
                var approvedProducts = products.Where(p => p.State == ApprovalState.Approved);
                return approvedProducts.Select(p => p.ToProductDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products for category {CategoryId}", categoryId);
                throw;
            }
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
                var skippedCount = 0;

                // Get all existing mappings for products to avoid checking one by one
                var fakeStoreProductIds = fakeStoreProducts.Select(p => p.Id.ToString()).ToList();
                var existingMappings = await _externalMappingRepository.GetMappingsBySourceIdsAsync(
                    fakeStoreProductIds, ExternalSource.FakeStore, "PRODUCT");
                
                var existingSourceIds = existingMappings.Select(em => em.SourceId).ToHashSet();

                foreach (var fakeStoreProduct in fakeStoreProducts)
                {
                    try
                    {
                        // Skip if mapping already exists unless the product was deleted
                        if (existingSourceIds.Contains(fakeStoreProduct.Id.ToString()))
                        {
                            var mapping = existingMappings.First(em => em.SourceId == fakeStoreProduct.Id.ToString());
                            var existingProduct = await _productRepository.GetByIdAsync(mapping.InternalId);
                            
                            if (existingProduct != null)
                            {
                                _logger.LogDebug("Producto {ProductId} ya existe, omitiendo", fakeStoreProduct.Id);
                                skippedCount++;
                                continue;
                            }
                            else
                            {
                                _logger.LogInformation("Producto {ProductId} tiene mapeo pero fue eliminado, re-importando", fakeStoreProduct.Id);
                            }
                        }

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

                _logger.LogInformation("Sincronización completada: {ImportedCount} productos importados, {SkippedCount} omitidos", 
                    importedCount, skippedCount);
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

                // First check if external mapping already exists
                var existingMapping = await _externalMappingRepository.GetByExternalIdAsync(
                    ExternalSource.FakeStore, "PRODUCT", fakeStoreId.ToString());

                if (existingMapping != null)
                {
                    // Mapping exists, check if the internal product still exists
                    var existingProduct = await _productRepository.GetByIdAsync(existingMapping.InternalId);
                    if (existingProduct != null)
                    {
                        _logger.LogInformation("Producto {ProductId} ya existe en BD", fakeStoreId);
                        return existingProduct.ToProductDto();
                    }
                    else
                    {
                        // Product was deleted but mapping still exists - will be updated below
                        _logger.LogInformation("Mapeo existe pero producto fue eliminado, actualizando mapeo para producto {ProductId}", fakeStoreId);
                    }
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

                // Create or update ExternalMapping using repository
                var mapping = new ExternalMapping
                {
                    Source = ExternalSource.FakeStore,
                    SourceType = "PRODUCT",
                    SourceId = fakeStoreId.ToString(),
                    InternalId = savedProduct.Id,
                    SnapshotJson = System.Text.Json.JsonSerializer.Serialize(fakeStoreProduct),
                    ImportedAt = DateTime.UtcNow
                };

                await _externalMappingRepository.CreateOrUpdateAsync(mapping);

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
                State = ApprovalState.PendingApproval, // Categories should always start as PendingApproval
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category created with PendingApproval state: {CategoryName}", categoryName);
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