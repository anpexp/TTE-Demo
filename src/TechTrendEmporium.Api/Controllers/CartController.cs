using Data.Entities.Enums;
using Logica.Interfaces;
using Logica.Models;
using Logica.Models.Carts;
using Logica.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TechTrendEmporium.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Shopper")]
    public class CartController : BaseController
    {
        private readonly ICartService _cartService;
        private readonly ILogger<CartController> _logger;

        public CartController(
            ICartService cartService,
            ILogger<CartController> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        // === OPERACIONES DEL USUARIO AUTENTICADO ===

        [HttpGet("my-carts")]
        public async Task<ActionResult<IEnumerable<CartDto>>> GetMyCarts()
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Getting carts for user {UserId}", userId);
                
                var carts = await _cartService.GetCartsByUserIdAsync(userId);
                return Ok(carts);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid user token");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user carts");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("active")]
        public async Task<ActionResult<CartDto>> GetMyActiveCart()
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Getting active cart for user {UserId}", userId);
                
                var cart = await _cartService.GetActiveCartByUserIdAsync(userId);
                if (cart == null)
                {
                    // Crear carrito activo si no existe
                    _logger.LogInformation("Creating new active cart for user {UserId}", userId);
                    cart = await _cartService.CreateEmptyCartForUserAsync(userId);
                }

                return Ok(cart);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid user token");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active cart");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("add-item")]
        public async Task<ActionResult<CartDto>> AddItemToMyCart([FromBody] AddItemToCartRequest request)
        {
            try
            {
                // Debug logging para ver qué se está recibiendo
                _logger.LogInformation("Received request: {Request}", System.Text.Json.JsonSerializer.Serialize(request));
                
                // Validar que el request no sea null
                if (request == null)
                {
                    _logger.LogWarning("Request is null");
                    return BadRequest("Request body is required");
                }

                // Validar ModelState
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState is invalid: {Errors}", 
                        string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }

                // Validar campos específicos
                if (request.ProductId == Guid.Empty)
                {
                    return BadRequest("ProductId is required and must be a valid GUID");
                }

                if (request.Quantity <= 0)
                {
                    return BadRequest("Quantity must be greater than 0");
                }

                var userId = GetCurrentUserId();
                _logger.LogInformation("User {UserId} adding item {ProductId} quantity {Quantity}", 
                    userId, request.ProductId, request.Quantity);
                
                var cart = await _cartService.AddItemToUserCartAsync(userId, request);
                return Ok(cart);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid user token");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Business logic error: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("update-item")]
        public async Task<ActionResult<CartDto>> UpdateItemInMyCart(UpdateCartItemQuantityRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("User {UserId} updating item {ProductId} to quantity {Quantity}", 
                    userId, request.ProductId, request.Quantity);
                
                var cart = await _cartService.UpdateItemInUserCartAsync(userId, request);
                return Ok(cart);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid user token");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("remove-item/{productId:guid}")]
        public async Task<ActionResult<CartDto>> RemoveItemFromMyCart(Guid productId)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("User {UserId} removing item {ProductId}", userId, productId);
                
                var cart = await _cartService.RemoveItemFromUserCartAsync(userId, productId);
                return Ok(cart);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid user token");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from cart");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("checkout")]
        public async Task<ActionResult<CartDto>> CheckoutMyCart()
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("User {UserId} checking out cart", userId);
                
                var cart = await _cartService.CheckoutUserCartAsync(userId);
                return Ok(cart);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid user token");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during checkout");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("clear")]
        public async Task<ActionResult<CartDto>> ClearMyCart()
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("User {UserId} clearing cart", userId);
                
                var cart = await _cartService.ClearUserCartAsync(userId);
                return Ok(cart);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid user token");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("inventory-warnings")]
        public async Task<ActionResult<IEnumerable<string>>> GetInventoryWarnings()
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Getting inventory warnings for user {UserId}", userId);
                
                var warnings = await _cartService.GetInventoryWarningsAsync(userId);
                return Ok(warnings);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid user token");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inventory warnings");
                return StatusCode(500, "Internal server error");
            }
        }

        // === ENDPOINTS DE TESTING (SOLO DESARROLLO) ===

        [HttpGet("test/active/{userId:guid}")]
        [AllowAnonymous] // Permitir sin autenticación para testing
        public async Task<ActionResult<CartDto>> GetActiveCartForTesting(Guid userId)
        {
            try
            {
                _logger.LogInformation("TEST: Getting active cart for user {UserId}", userId);
                
                var cart = await _cartService.GetActiveCartByUserIdAsync(userId);
                if (cart == null)
                {
                    cart = await _cartService.CreateEmptyCartForUserAsync(userId);
                }

                return Ok(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test endpoint");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("test/add-item/{userId:guid}")]
        [AllowAnonymous] // Permitir sin autenticación para testing
        public async Task<ActionResult<CartDto>> AddItemForTesting(Guid userId, AddItemToCartRequest request)
        {
            try
            {
                _logger.LogInformation("TEST: User {UserId} adding item {ProductId}", userId, request.ProductId);
                
                var cart = await _cartService.AddItemToUserCartAsync(userId, request);
                return Ok(cart);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test add item");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("test/checkout/{userId:guid}")]
        [AllowAnonymous] // Permitir sin autenticación para testing
        public async Task<ActionResult<CartDto>> CheckoutForTesting(Guid userId)
        {
            try
            {
                _logger.LogInformation("TEST: User {UserId} checking out", userId);
                
                var cart = await _cartService.CheckoutUserCartAsync(userId);
                return Ok(cart);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test checkout");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("test/system-user-cart")]
        [AllowAnonymous] // Permitir sin autenticación para testing
        public async Task<ActionResult<CartDto>> GetSystemUserCart()
        {
            try
            {
                var systemUserId = new Guid("00000000-0000-0000-0000-000000000001");
                _logger.LogInformation("TEST: Getting system user cart");
                
                var cart = await _cartService.GetActiveCartByUserIdAsync(systemUserId);
                if (cart == null)
                {
                    cart = await _cartService.CreateEmptyCartForUserAsync(systemUserId);
                }

                return Ok(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system user cart");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("test/validate-request")]
        [AllowAnonymous] // Para testing sin auth
        public async Task<ActionResult> ValidateAddItemRequest([FromBody] AddItemToCartRequest request)
        {
            try
            {
                _logger.LogInformation("TEST: Validating request format");
                _logger.LogInformation("Raw request: {Request}", 
                    System.Text.Json.JsonSerializer.Serialize(request ?? new AddItemToCartRequest()));
                
                if (request == null)
                {
                    return BadRequest(new { 
                        error = "Request is null",
                        expectedFormat = new {
                            productId = "guid-string",
                            quantity = 1
                        }
                    });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new {
                        error = "Validation failed",
                        errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage),
                        receivedData = new {
                            productId = request.ProductId,
                            quantity = request.Quantity
                        }
                    });
                }

                return Ok(new { 
                    message = "Request format is valid!",
                    receivedData = new {
                        productId = request.ProductId,
                        quantity = request.Quantity
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating request");
                return BadRequest(new { 
                    error = "JSON parsing failed",
                    details = ex.Message,
                    expectedFormat = new {
                        productId = "guid-string",
                        quantity = 1
                    }
                });
            }
        }

        [HttpPost("test/stress-add-item/{userId:guid}")]
        [AllowAnonymous] // Para testing de concurrencia
        public async Task<ActionResult> StressTestAddItem(Guid userId, [FromBody] AddItemToCartRequest request)
        {
            try
            {
                _logger.LogInformation("STRESS TEST: Multiple concurrent add-item requests for user {UserId}", userId);
                
                // Simular múltiples requests concurrentes
                var tasks = new List<Task<CartDto>>();
                for (int i = 0; i < 5; i++)
                {
                    var taskRequest = new AddItemToCartRequest 
                    { 
                        ProductId = request.ProductId, 
                        Quantity = 1 
                    };
                    tasks.Add(_cartService.AddItemToUserCartAsync(userId, taskRequest));
                }

                var results = await Task.WhenAll(tasks);
                
                return Ok(new { 
                    message = "Stress test completed",
                    concurrentRequests = tasks.Count,
                    finalCart = results.LastOrDefault()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stress test");
                return BadRequest(new { 
                    error = "Stress test failed",
                    details = ex.Message
                });
            }
        }

        [HttpPut("test/update-item/{userId:guid}")]
        [AllowAnonymous] // Para testing sin auth
        public async Task<ActionResult<CartDto>> UpdateItemForTesting(Guid userId, [FromBody] UpdateCartItemQuantityRequest request)
        {
            try
            {
                _logger.LogInformation("TEST: User {UserId} updating item {ProductId} to quantity {Quantity}", 
                    userId, request.ProductId, request.Quantity);
                
                var cart = await _cartService.UpdateItemInUserCartAsync(userId, request);
                return Ok(cart);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test update item");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("test/remove-item/{userId:guid}/{productId:guid}")]
        [AllowAnonymous] // Para testing sin auth
        public async Task<ActionResult<CartDto>> RemoveItemForTesting(Guid userId, Guid productId)
        {
            try
            {
                _logger.LogInformation("TEST: User {UserId} removing item {ProductId}", userId, productId);
                
                var cart = await _cartService.RemoveItemFromUserCartAsync(userId, productId);
                return Ok(cart);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test remove item");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}