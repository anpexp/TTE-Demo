using Logica.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Logica.Models.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TechTrendEmporium.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : BaseController
    {
        private readonly IProductService _productService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            IProductService productService,
            ILogger<ProductsController> logger)
        {
            _productService = productService;
            _logger = logger;
        }


        [HttpGet("products")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<IEnumerable<ProductSummaryDto>>> GetMyProducts()
        {
            try
            {
                var userId = GetCurrentUserId(); // From JWT when implemented
                var products = await _productService.GetProductsByUserIdAsync(userId);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user products");
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpGet("approved")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetApprovedProducts()
        {
            try
            {
                var products = await _productService.GetApprovedProductsAsync();
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approved products");
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProductDto>> GetProduct(Guid id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);

                if (product == null)
                {
                    return NotFound($"Product with ID {id} not found");
                }

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpPost]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<ProductCreateResponseDto>> CreateProduct(ProductCreateDto productDto)
        {
            try
            {
                var createdBy = GetCurrentUserId();

                var product = await _productService.CreateProductAsync(productDto, createdBy);

                var response = new ProductCreateResponseDto
                {
                    ProductId = product.Id,
                    Message = "Successful"
                };

                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");

                var errorResponse = new ProductCreateResponseDto
                {
                    ProductId = Guid.Empty,
                    Message = "Failure"
                };

                return StatusCode(500, errorResponse);
            }
        }


        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<ProductResponseDto>> UpdateProduct(Guid id, ProductUpdateDto productDto)
        {
            try
            {
                var product = await _productService.UpdateProductAsync(id, productDto);

                if (product == null)
                {
                    return NotFound($"Product with ID {id} not found");
                }

                var response = new ProductResponseDto
                {
                    Message = "Updated successfully"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<ProductResponseDto>> DeleteProduct(Guid id)
        {
            try
            {
                var success = await _productService.DeleteProductAsync(id);

                if (!success)
                {
                    return NotFound($"Product with ID {id} not found");
                }

                var response = new ProductResponseDto
                {
                    Message = "Deleted successfully"
                };
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Operación inválida al eliminar producto {ProductId}", id);
                return NotFound(ex.Message); // Returns 404 if the product is already deleted
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> SearchProducts([FromQuery] string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return BadRequest("Search term is required");
                }

                // The service already filters for approved products only
                var products = await _productService.SearchProductsAsync(searchTerm);
                
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products");
                return StatusCode(500, "Internal server error");
            }
        }



        // FakeStore usage


        [HttpGet("fakestore")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetProductsFromFakeStore()
        {
            try
            {
                var products = await _productService.GetProductsFromFakeStoreAsync();
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products from FakeStore");
                return StatusCode(500, "Error getting products from FakeStore");
            }
        }


        [HttpGet("fakestore/{id:int}")]
        public async Task<ActionResult<ProductDto>> GetProductFromFakeStore(int id)
        {
            try
            {
                var product = await _productService.GetProductFromFakeStoreAsync(id);
                if (product == null)
                {
                    return NotFound($"Product with ID {id} not found in FakeStore");
                }
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product {ProductId} from FakeStore", id);
                return StatusCode(500, "Error getting product from FakeStore");
            }
        }

        [HttpGet("fakestore/categories")]
        public async Task<ActionResult<IEnumerable<string>>> GetCategoriesFromFakeStore()
        {
            try
            {
                var categories = await _productService.GetCategoriesFromFakeStoreAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories from FakeStore");
                return StatusCode(500, "Error getting categories from FakeStore");
            }
        }

        [HttpGet("fakestore/category/{category}")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetProductsByCategoryFromFakeStore(string category)
        {
            try
            {
                var products = await _productService.GetProductsByCategoryFromFakeStoreAsync(category);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products by category from FakeStore");
                return StatusCode(500, "Error getting products by category from FakeStore");
            }
        }


        // Approval Operations


        [HttpGet("pending-approval")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetPendingApproval()
        {
            try
            {
                var products = await _productService.GetPendingApprovalAsync();
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products pending approval");
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpPost("{id:guid}/approve")]
        public async Task<ActionResult> ApproveProduct(Guid id)
        {
            try
            {
                var approvedBy = GetCurrentUserId();
                var success = await _productService.ApproveProductAsync(id, approvedBy);

                if (!success)
                {
                    return NotFound($"Product with ID {id} not found");
                }

                return Ok(new { Message = "Product approved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving product {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpPost("{id:guid}/reject")]
        public async Task<ActionResult> RejectProduct(Guid id)
        {
            try
            {
                var success = await _productService.RejectProductAsync(id);

                if (!success)
                {
                    return NotFound($"Product with ID {id} not found");
                }

                return Ok(new { Message = "Product rejected successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting product {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }



        // Utilities


        private static Guid ConvertIntToGuid(int id)
        {
            var bytes = new byte[16];
            var idBytes = BitConverter.GetBytes(id);
            Array.Copy(idBytes, 0, bytes, 0, 4);
            return new Guid(bytes);
        }

        private static int ConvertGuidToInt(Guid guid)
        {
            var bytes = guid.ToByteArray();
            return BitConverter.ToInt32(bytes, 0);
        }

        // === SIMPLE INVENTORY OPERATIONS ===

        [HttpGet("{id:guid}/stock")]
        public async Task<ActionResult<object>> GetProductStock(Guid id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    return NotFound($"Product with ID {id} not found");
                }

                var stockInfo = new
                {
                    productId = product.Id,
                    productTitle = product.Title,
                    productImage = product.Image,
                    totalStock = product.InventoryTotal,
                    availableStock = product.InventoryAvailable,
                    reservedStock = Math.Max(0, product.InventoryTotal - product.InventoryAvailable), // Calculado
                    isInStock = product.IsInStock,
                    isLowStock = product.IsLowStock,
                    isOutOfStock = product.IsOutOfStock
                };

                return Ok(stockInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stock for product {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id:guid}/stock")]
        public async Task<ActionResult> UpdateProductStock(Guid id, [FromBody] object stockUpdate)
        {
            try
            {
                // TODO: Implementar actualización de stock cuando sea necesario
                _logger.LogInformation("📦 Stock update requested for product {ProductId}: {Update}", 
                    id, stockUpdate);

                return Ok(new { 
                    message = "Stock update request logged",
                    productId = id,
                    updateData = stockUpdate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock for product {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}