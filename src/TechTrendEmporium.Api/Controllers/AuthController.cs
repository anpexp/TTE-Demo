using Logica.Interfaces;
using Logica.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace TechTrendEmporium.Api.Controllers
{
    [ApiController]
    [Route("api")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
        }

        //IA debug de token - Endpoint temporal para diagnosticar JWT (REMOVER EN PRODUCCIÓN)
        [HttpGet("debug/jwt")]
        public IActionResult DebugJwt()
        {
            //IA debug de token - Buscar la clave JWT en múltiples ubicaciones para compatibilidad con Azure
            var jwtKey = _configuration["Jwt:Key"] 
                      ?? _configuration["Jwt_Key"] 
                      ?? Environment.GetEnvironmentVariable("Jwt_Key")
                      ?? Environment.GetEnvironmentVariable("Jwt__Key");

            //IA debug de token - Retornar información de diagnóstico
            return Ok(new 
            { 
                jwtKeyFound = !string.IsNullOrWhiteSpace(jwtKey),
                jwtKeyLength = jwtKey?.Length ?? 0,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                siteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")
            });
        }
        //IA debug de token - FIN del endpoint de diagnóstico

        /// <summary>
        /// AC #1: Register new shopper account
        /// </summary>
        [HttpPost("auth")]
        public async Task<IActionResult> RegisterShopper(ShopperRegisterRequest request)
        {
            try
            {
                var (response, error) = await _authService.RegisterShopperAsync(request);
                
                if (error != null) 
                {
                    _logger.LogWarning("Shopper registration failed: {Error}", error);
                    return BadRequest(new { message = error });
                }

                _logger.LogInformation("Shopper registration successful: {Email}", response!.Email);
                return Ok(new { 
                    id = response.Id, 
                    email = response.Email, 
                    username = response.Username,
                    message = "Registration successful"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during shopper registration");
                return StatusCode(500, new { message = "Internal server error during registration" });
            }
        }

        /// <summary>
        /// AC #1: Register new employee account (SuperAdmin only)
        /// </summary>
        [HttpPost("admin/auth")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> RegisterByAdmin(AdminRegisterRequest request)
        {
            try
            {
                var (response, error) = await _authService.RegisterByAdminAsync(request);
                
                if (error != null) 
                {
                    _logger.LogWarning("Admin registration failed: {Error}", error);
                    return BadRequest(new { message = error });
                }

                _logger.LogInformation("Employee registration successful by admin: {Email}", response!.Email);
                return Ok(new { 
                    id = response.Id, 
                    email = response.Email, 
                    username = response.Username, 
                    role = response.Role,
                    message = "Employee registration successful"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during admin registration");
                return StatusCode(500, new { message = "Internal server error during registration" });
            }
        }

        /// <summary>
        /// AC #1: Login user with valid role
        /// AC #2 & #3: Session record - records session in system
        /// AC #3: Invalid login - handles invalid credentials
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            try
            {
                // Capture request data for session tracking
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers["User-Agent"].ToString();

                var (response, error) = await _authService.LoginAsync(request, ipAddress, userAgent);

                if (error != null) 
                {
                    // AC #3: Invalid login - return appropriate error message
                    _logger.LogWarning("Login failed for {Email}: {Error}", request.Email, error);
                    return Unauthorized(new { message = error });
                }

                // AC #1: User successfully logs in
                // AC #2: Session is recorded in the system
                _logger.LogInformation("Login successful for {Email}", response!.Email);
                return Ok(new { 
                    token = response.Token, 
                    email = response.Email, 
                    username = response.Username,
                    role = response.Role,
                    message = "Login successful"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for {Email}", request.Email);
                return StatusCode(500, new { message = "Internal server error during login" });
            }
        }

        /// <summary>
        /// AC #1: User successfully logs out
        /// AC #2: Unsuccessfully logout - handles when user is not logged in
        /// AC #3: Session record - records logout in system
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var (success, error) = await _authService.LogoutAsync(User);
                
                if (!success) 
                {
                    // AC #2: Unsuccessfully logout - user not logged in or session not found
                    _logger.LogWarning("Logout failed: {Error}", error);
                    return BadRequest(new { message = error ?? "Logout failed" });
                }

                // AC #1: User successfully logs out
                // AC #3: Session record - logout is recorded in system
                _logger.LogInformation("Logout successful");
                return Ok(new { 
                    status = "OK",
                    message = "Logout successful"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during logout");
                return StatusCode(500, new { message = "Internal server error during logout" });
            }
        }

        /// <summary>
        /// Get current session information (for testing/debugging)
        /// </summary>
        [HttpGet("session/current")]
        [Authorize]
        public async Task<IActionResult> GetCurrentSession()
        {
            try
            {
                var jtiClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti);
                if (jtiClaim == null)
                {
                    return BadRequest(new { message = "Invalid token - no session identifier" });
                }

                var session = await _authService.GetActiveSessionAsync(jtiClaim.Value);
                if (session == null)
                {
                    return NotFound(new { message = "No active session found" });
                }

                return Ok(new {
                    sessionId = session.Id,
                    userId = session.UserId,
                    status = session.Status.ToString(),
                    createdAt = session.CreatedAt,
                    ipAddress = session.Ip,
                    userAgent = session.UserAgent
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current session");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Test endpoint to verify authentication is working
        /// </summary>
        [HttpGet("test/auth")]
        [Authorize]
        public IActionResult TestAuth()
        {
            var userName = User.Identity?.Name;
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            return Ok(new {
                message = "Authentication working",
                authenticated = true,
                userName = userName,
                userRole = userRole,
                userId = userId
            });
        }
    }
}