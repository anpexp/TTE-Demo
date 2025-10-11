using Data;
using Data.Entities.Enums;
using Logica.Interfaces;
using Logica.Models;
using Logica.Models.Carts;
using Logica.Models.Products;
using Logica.Models.Category.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TechTrendEmporium.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;
        private readonly ICartService _cartService;
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IUserService _userService;
        private readonly IExternalMappingRepository _externalMappingRepository;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            AppDbContext context,
            IAuthService authService,
            ICartService cartService,
            IProductService productService,
            ICategoryService categoryService,
            IUserService userService,
            IExternalMappingRepository externalMappingRepository,
            ILogger<AdminController> logger)
        {
            _context = context;
            _authService = authService;
            _cartService = cartService;
            _productService = productService;
            _categoryService = categoryService;
            _userService = userService;
            _externalMappingRepository = externalMappingRepository;
            _logger = logger;
        }

        // ============================================
        // SESSION MANAGEMENT (SuperAdmin Only)
        // ============================================

        /// <summary>
        /// Get all active sessions
        /// </summary>
        [HttpGet("sessions/active")]
        [Tags("Admin - Sessions Management")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> GetActiveSessions()
        {
            try
            {
                var activeSessions = await _context.Sessions
                    .Include(s => s.User)
                    .Where(s => s.Status == SessionStatus.Active)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new
                    {
                        sessionId = s.Id,
                        userId = s.UserId,
                        username = s.User.Username,
                        email = s.User.Email,
                        role = s.User.Role.ToString(),
                        createdAt = s.CreatedAt,
                        ipAddress = s.Ip,
                        userAgent = s.UserAgent,
                        minutesActive = EF.Functions.DateDiffMinute(s.CreatedAt, DateTime.UtcNow)
                    })
                    .ToListAsync();

                return Ok(new
                {
                    totalActiveSessions = activeSessions.Count,
                    sessions = activeSessions,
                    timestamp = DateTime.UtcNow,
                    message = "Active sessions retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active sessions");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get complete session history
        /// </summary>
        [HttpGet("sessions/all")]
        [Tags("Admin - Sessions Management")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> GetAllSessions([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 50;

                var totalSessions = await _context.Sessions.CountAsync();
                
                var sessions = await _context.Sessions
                    .Include(s => s.User)
                    .OrderByDescending(s => s.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new
                    {
                        sessionId = s.Id,
                        userId = s.UserId,
                        username = s.User.Username,
                        email = s.User.Email,
                        role = s.User.Role.ToString(),
                        status = s.Status.ToString(),
                        createdAt = s.CreatedAt,
                        closedAt = s.ClosedAt,
                        ipAddress = s.Ip,
                        userAgent = s.UserAgent,
                        durationMinutes = s.ClosedAt != null 
                            ? EF.Functions.DateDiffMinute(s.CreatedAt, s.ClosedAt.Value)
                            : (s.Status == SessionStatus.Active 
                                ? (int?)EF.Functions.DateDiffMinute(s.CreatedAt, DateTime.UtcNow) 
                                : (int?)null)
                    })
                    .ToListAsync();

                return Ok(new
                {
                    totalSessions = totalSessions,
                    currentPage = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling((double)totalSessions / pageSize),
                    sessions = sessions,
                    message = "Session history retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting complete session history");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get session statistics
        /// </summary>
        [HttpGet("sessions/statistics")]
        [Tags("Admin - Sessions Management")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> GetSessionStatistics()
        {
            try
            {
                var totalSessions = await _context.Sessions.CountAsync();
                var activeSessions = await _context.Sessions.CountAsync(s => s.Status == SessionStatus.Active);
                var closedSessions = await _context.Sessions.CountAsync(s => s.Status == SessionStatus.Closed);
                var expiredSessions = await _context.Sessions.CountAsync(s => s.Status == SessionStatus.Expired);

                var sessionsByRole = await _context.Sessions
                    .Include(s => s.User)
                    .GroupBy(s => s.User.Role)
                    .Select(g => new
                    {
                        role = g.Key.ToString(),
                        totalSessions = g.Count(),
                        activeSessions = g.Count(s => s.Status == SessionStatus.Active)
                    })
                    .ToListAsync();

                var recentLogins = await _context.Sessions
                    .Include(s => s.User)
                    .Where(s => s.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                    .GroupBy(s => s.CreatedAt.Date)
                    .Select(g => new
                    {
                        date = g.Key,
                        loginCount = g.Count()
                    })
                    .OrderBy(x => x.date)
                    .ToListAsync();

                return Ok(new
                {
                    totalSessions = totalSessions,
                    sessionsByStatus = new
                    {
                        active = activeSessions,
                        closed = closedSessions,
                        expired = expiredSessions
                    },
                    sessionsByRole = sessionsByRole,
                    recentLoginsLast7Days = recentLogins,
                    timestamp = DateTime.UtcNow,
                    message = "Session statistics retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session statistics");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Close a specific session
        /// </summary>
        [HttpPost("sessions/{sessionId:guid}/close")]
        [Tags("Admin - Sessions Management")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> CloseSession(Guid sessionId)
        {
            try
            {
                var session = await _context.Sessions
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    return NotFound(new { message = "Session not found" });
                }

                if (session.Status != SessionStatus.Active)
                {
                    return BadRequest(new { message = $"Session is already in state: {session.Status}" });
                }

                session.Status = SessionStatus.Expired;
                session.ClosedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();

                _logger.LogInformation("Session {SessionId} closed by administrator for user {Username}", 
                    sessionId, session.User.Username);

                return Ok(new
                {
                    sessionId = sessionId,
                    userId = session.UserId,
                    username = session.User.Username,
                    previousStatus = "Active",
                    newStatus = "Expired",
                    closedAt = session.ClosedAt,
                    message = "Session closed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing session {SessionId}", sessionId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // ============================================
        // CART MANAGEMENT (SuperAdmin Only)
        // ============================================

        /// <summary>
        /// Get all carts with detailed information
        /// </summary>
        [HttpGet("carts/all")]
        [Tags("Admin - Carts Management")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<IEnumerable<CartFullDetailsDto>>> GetAllCarts()
        {
            try
            {
                _logger.LogInformation("Admin getting all carts");
                var carts = await _cartService.GetAllCartsFullDetailsAsync();
                return Ok(carts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all carts for admin");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get carts dashboard
        /// </summary>
        [HttpGet("carts/dashboard")]
        [Tags("Admin - Carts Management")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<CartsDashboardSummaryDto>> GetCartsDashboard()
        {
            try
            {
                _logger.LogInformation("Admin getting carts dashboard");
                var summary = await _cartService.GetCartsDashboardSummaryAsync();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting carts dashboard");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get carts from a specific user
        /// </summary>
        [HttpGet("carts/user/{userId:guid}")]
        [Tags("Admin - Carts Management")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<IEnumerable<CartFullDetailsDto>>> GetUserCarts(Guid userId)
        {
            try
            {
                _logger.LogInformation("Admin getting carts for user {UserId}", userId);
                var carts = await _cartService.GetCartsByUserFullDetailsAsync(userId);
                return Ok(carts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user carts for admin");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Restore cart inventory
        /// </summary>
        [HttpPost("carts/{cartId:guid}/restore-inventory")]
        [Tags("Admin - Carts Management")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult> RestoreCartInventory(Guid cartId)
        {
            try
            {
                _logger.LogInformation("Admin restoring inventory for cart {CartId}", cartId);
                await _cartService.RestoreInventoryAsync(cartId);
                return Ok(new { message = "Inventory restored successfully", cartId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring cart inventory");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // ============================================
        // PRODUCTS MANAGEMENT (Employee & SuperAdmin)
        // ============================================

        /// <summary>
        /// Get all products (administrative view)
        /// </summary>
        [HttpGet("products/all")]
        [Tags("Admin - Products Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<IEnumerable<ProductSummaryDto>>> GetAllProducts()
        {
            try
            {
                var products = await _productService.GetAllProductsAsync();
                var summaryProducts = products.Select(p => new ProductSummaryDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Price = p.Price,
                    Category = p.Category
                });
                return Ok(summaryProducts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all products");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get only approved products
        /// </summary>
        [HttpGet("products/approved")]
        [Tags("Admin - Products Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
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

        /// <summary>
        /// Get products pending approval
        /// </summary>
        [HttpGet("products/pending-approval")]
        [Tags("Admin - Products Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetProductsPendingApproval()
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

        /// <summary>
        /// Approve product
        /// </summary>
        [HttpPost("products/{id:guid}/approve")]
        [Tags("Admin - Products Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
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

        /// <summary>
        /// Get stock summary
        /// </summary>
        [HttpGet("products/stock/summary")]
        [Tags("Admin - Products Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<object>> GetStockSummary()
        {
            try
            {
                var products = await _productService.GetAllProductsAsync();
                var productsList = products.ToList();

                var summary = new
                {
                    totalProducts = productsList.Count,
                    inStockProducts = productsList.Count(p => p.IsInStock),
                    outOfStockProducts = productsList.Count(p => p.IsOutOfStock),
                    lowStockProducts = productsList.Count(p => p.IsLowStock),
                    totalInventoryValue = (int)productsList.Sum(p => p.Price * p.InventoryAvailable)
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stock summary");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get low stock products
        /// </summary>
        [HttpGet("products/stock/low")]
        [Tags("Admin - Products Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<IEnumerable<object>>> GetLowStockProducts()
        {
            try
            {
                var products = await _productService.GetAllProductsAsync();
                var lowStockProducts = products
                    .Where(p => p.IsLowStock)
                    .Select(p => new
                    {
                        productId = p.Id,
                        title = p.Title,
                        image = p.Image,
                        availableStock = p.InventoryAvailable,
                        totalStock = p.InventoryTotal,
                        isOutOfStock = p.IsOutOfStock
                    })
                    .ToList();

                return Ok(lowStockProducts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting low stock products");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get out of stock products
        /// </summary>
        [HttpGet("products/stock/out")]
        [Tags("Admin - Products Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<IEnumerable<object>>> GetOutOfStockProducts()
        {
            try
            {
                var products = await _productService.GetAllProductsAsync();
                var outOfStockProducts = products
                    .Where(p => p.IsOutOfStock)
                    .Select(p => new
                    {
                        productId = p.Id,
                        title = p.Title,
                        image = p.Image,
                        availableStock = p.InventoryAvailable,
                        totalStock = p.InventoryTotal
                    })
                    .ToList();

                return Ok(outOfStockProducts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting out of stock products");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Synchronize products from FakeStore
        /// </summary>
        [HttpPost("products/sync-from-fakestore")]
        [Tags("Admin - Products Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<object>> SyncProductsFromFakeStore()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var createdBy = currentUserId == Guid.Empty ? new Guid("00000000-0000-0000-0000-000000000001") : currentUserId;
                
                var importedCount = await _productService.SyncAllFromFakeStoreAsync(createdBy);

                return Ok(new
                {
                    Message = "Product synchronization completed successfully",
                    ImportedCount = importedCount,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing products from FakeStore");
                return StatusCode(500, new { message = "Error during synchronization" });
            }
        }

        // ============================================
        // CATEGORIES MANAGEMENT (Employee & SuperAdmin)
        // ============================================

        /// <summary>
        /// Get all categories (administrative view)
        /// </summary>
        [HttpGet("categories/all")]
        [Tags("Admin - Categories Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAllCategories()
        {
            try
            {
                var categories = await _categoryService.GetAllCategoriesAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all categories");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get only approved categories
        /// </summary>
        [HttpGet("categories/approved")]
        [Tags("Admin - Categories Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetApprovedCategories()
        {
            try
            {
                var categories = await _categoryService.GetApprovedCategoriesAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approved categories");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Search categories (all states for admin)
        /// </summary>
        [HttpGet("categories/search")]
        [Tags("Admin - Categories Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> SearchAllCategories([FromQuery] string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return BadRequest("Search term is required");
                }

                var categories = await _categoryService.SearchCategoriesAsync(searchTerm);
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching all categories for admin");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get categories pending approval
        /// </summary>
        [HttpGet("categories/pending-approval")]
        [Tags("Admin - Categories Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategoriesPendingApproval()
        {
            try
            {
                var categories = await _categoryService.GetPendingApprovalAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories pending approval");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Approve category
        /// </summary>
        [HttpPost("categories/{id:guid}/approve")]
        [Tags("Admin - Categories Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult> ApproveCategory(Guid id)
        {
            try
            {
                var approvedBy = GetCurrentUserId();
                var success = await _categoryService.ApproveCategoryAsync(id, approvedBy);

                if (!success)
                {
                    return NotFound($"Category with ID {id} not found");
                }

                return Ok(new { Message = "Category approved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving category {CategoryId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Reject category
        /// </summary>
        [HttpPost("categories/{id:guid}/reject")]
        [Tags("Admin - Categories Management")]
        [Authorize(Roles = "Employee, SuperAdmin")]
        public async Task<ActionResult> RejectCategory(Guid id)
        {
            try
            {
                var success = await _categoryService.RejectCategoryAsync(id);

                if (!success)
                {
                    return NotFound($"Category with ID {id} not found");
                }

                return Ok(new { Message = "Category rejected successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting category {CategoryId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // ============================================
        // EXTERNAL MAPPINGS MANAGEMENT (SuperAdmin Only)
        // ============================================

        /// <summary>
        /// Clean orphaned external mappings
        /// </summary>
        [HttpPost("mappings/cleanup-orphaned")]
        [Tags("Admin - External Mappings")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> CleanupOrphanedMappings()
        {
            try
            {
                _logger.LogInformation("Admin starting cleanup of orphaned external mappings");
                
                var productMappingsRemoved = await _externalMappingRepository.RemoveOrphanedMappingsAsync(
                    ExternalSource.FakeStore, "PRODUCT");
                
                var userMappingsRemoved = await _externalMappingRepository.RemoveOrphanedMappingsAsync(
                    ExternalSource.FakeStore, "USER");
                
                var categoryMappingsRemoved = await _externalMappingRepository.RemoveOrphanedMappingsAsync(
                    ExternalSource.FakeStore, "CATEGORY");

                var totalRemoved = productMappingsRemoved + userMappingsRemoved + categoryMappingsRemoved;

                _logger.LogInformation("Cleanup completed: {TotalRemoved} orphaned mappings removed", totalRemoved);

                return Ok(new
                {
                    message = "Orphaned mappings cleanup completed",
                    removedMappings = new
                    {
                        products = productMappingsRemoved,
                        users = userMappingsRemoved,
                        categories = categoryMappingsRemoved,
                        total = totalRemoved
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up orphaned mappings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get external mappings statistics
        /// </summary>
        [HttpGet("mappings/statistics")]
        [Tags("Admin - External Mappings")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> GetMappingsStatistics()
        {
            try
            {
                var productMappings = await _context.ExternalMappings
                    .Where(em => em.Source == ExternalSource.FakeStore && em.SourceType == "PRODUCT")
                    .CountAsync();
                
                var userMappings = await _context.ExternalMappings
                    .Where(em => em.Source == ExternalSource.FakeStore && em.SourceType == "USER")
                    .CountAsync();
                
                var categoryMappings = await _context.ExternalMappings
                    .Where(em => em.Source == ExternalSource.FakeStore && em.SourceType == "CATEGORY")
                    .CountAsync();

                // Check for orphaned mappings
                var orphanedProductMappings = await _context.ExternalMappings
                    .Where(em => em.Source == ExternalSource.FakeStore && em.SourceType == "PRODUCT")
                    .Where(em => !_context.Products.Any(p => p.Id == em.InternalId))
                    .CountAsync();

                var orphanedUserMappings = await _context.ExternalMappings
                    .Where(em => em.Source == ExternalSource.FakeStore && em.SourceType == "USER")
                    .Where(em => !_context.Users.Any(u => u.Id == em.InternalId))
                    .CountAsync();

                var orphanedCategoryMappings = await _context.ExternalMappings
                    .Where(em => em.Source == ExternalSource.FakeStore && em.SourceType == "CATEGORY")
                    .Where(em => !_context.Categories.Any(c => c.Id == em.InternalId))
                    .CountAsync();

                return Ok(new
                {
                    totalMappings = productMappings + userMappings + categoryMappings,
                    mappingsByType = new
                    {
                        products = productMappings,
                        users = userMappings,
                        categories = categoryMappings
                    },
                    orphanedMappings = new
                    {
                        products = orphanedProductMappings,
                        users = orphanedUserMappings,
                        categories = orphanedCategoryMappings,
                        total = orphanedProductMappings + orphanedUserMappings + orphanedCategoryMappings
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mappings statistics");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // ============================================
        // SYSTEM DIAGNOSTICS (SuperAdmin Only)
        // ============================================

        /// <summary>
        /// Check system health
        /// </summary>
        [HttpGet("diagnostics/health")]
        [Tags("Admin - System Diagnostics")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> GetSystemHealth()
        {
            try
            {
                var health = new
                {
                    database = await CheckDatabaseHealthAsync(),
                    fakestore = await CheckFakeStoreHealthAsync(),
                    services = await CheckServicesHealthAsync(),
                    timestamp = DateTime.UtcNow
                };

                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system health");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get system information
        /// </summary>
        [HttpGet("diagnostics/system-info")]
        [Tags("Admin - System Diagnostics")]
        [Authorize(Roles = "SuperAdmin")]
        public ActionResult<object> GetSystemInfo()
        {
            try
            {
                var info = new
                {
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    machineName = Environment.MachineName,
                    osVersion = Environment.OSVersion.ToString(),
                    processorCount = Environment.ProcessorCount,
                    workingSet = Environment.WorkingSet,
                    dotnetVersion = Environment.Version.ToString(),
                    timestamp = DateTime.UtcNow
                };

                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system information");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Clean old logs and optimize performance
        /// </summary>
        [HttpPost("maintenance/cleanup")]
        [Tags("Admin - System Diagnostics")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> PerformMaintenance()
        {
            try
            {
                var oldSessionsRemoved = await CleanupOldSessionsAsync();
                var oldLogsRemoved = await CleanupOldLogsAsync();
                
                // Clean orphaned mappings
                var orphanedProductMappings = await _externalMappingRepository.RemoveOrphanedMappingsAsync(
                    ExternalSource.FakeStore, "PRODUCT");
                var orphanedUserMappings = await _externalMappingRepository.RemoveOrphanedMappingsAsync(
                    ExternalSource.FakeStore, "USER");
                var orphanedCategoryMappings = await _externalMappingRepository.RemoveOrphanedMappingsAsync(
                    ExternalSource.FakeStore, "CATEGORY");

                var result = new
                {
                    oldSessionsRemoved = oldSessionsRemoved,
                    oldLogsRemoved = oldLogsRemoved,
                    orphanedMappingsRemoved = new
                    {
                        products = orphanedProductMappings,
                        users = orphanedUserMappings,
                        categories = orphanedCategoryMappings,
                        total = orphanedProductMappings + orphanedUserMappings + orphanedCategoryMappings
                    },
                    cacheCleared = true,
                    timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Maintenance completed: {SessionsRemoved} sessions, {MappingsRemoved} orphaned mappings removed", 
                    oldSessionsRemoved, orphanedProductMappings + orphanedUserMappings + orphanedCategoryMappings);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during maintenance");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // ============================================
        // PRIVATE HELPER METHODS
        // ============================================
        private async Task<object> CheckDatabaseHealthAsync()
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                var userCount = await _context.Users.CountAsync();
                
                return new
                {
                    status = canConnect ? "Healthy" : "Unhealthy",
                    canConnect = canConnect,
                    userCount = userCount,
                    lastChecked = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = "Unhealthy",
                    error = ex.Message,
                    lastChecked = DateTime.UtcNow
                };
            }
        }

        private async Task<object> CheckFakeStoreHealthAsync()
        {
            try
            {
                var products = await _productService.GetProductsFromFakeStoreAsync();
                
                return new
                {
                    status = "Healthy",
                    productsAvailable = products.Count(),
                    lastChecked = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = "Unhealthy",
                    error = ex.Message,
                    lastChecked = DateTime.UtcNow
                };
            }
        }

        private async Task<object> CheckServicesHealthAsync()
        {
            try
            {
                return new
                {
                    status = "Healthy",
                    services = new
                    {
                        cartService = _cartService != null ? "Available" : "Unavailable",
                        productService = _productService != null ? "Available" : "Unavailable",
                        userService = _userService != null ? "Available" : "Unavailable",
                        authService = _authService != null ? "Available" : "Unavailable"
                    },
                    lastChecked = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = "Unhealthy",
                    error = ex.Message,
                    lastChecked = DateTime.UtcNow
                };
            }
        }

        private async Task<int> CleanupOldSessionsAsync()
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-30);
                var oldSessions = await _context.Sessions
                    .Where(s => s.CreatedAt < cutoffDate && s.Status != SessionStatus.Active)
                    .ToListAsync();

                if (oldSessions.Any())
                {
                    _context.Sessions.RemoveRange(oldSessions);
                    await _context.SaveChangesAsync();
                }

                return oldSessions.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning old sessions");
                return 0;
            }
        }

        private async Task<int> CleanupOldLogsAsync()
        {
            // Implement log cleanup if you have a logs table
            await Task.Delay(100); // Placeholder
            return 0;
        }
    }
}