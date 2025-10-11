using Data.Entities;
using Data.Entities.Enums;
using Logica.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using BCrypt.Net;
using Microsoft.Extensions.Logging;
using Logica.Models.Auth.Create;
using Logica.Models.Auth.Login;
using Logica.Models.Auth.Reponse;

namespace Logica.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUserRepository userRepository, ITokenService tokenService, ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<(AuthResponse? Response, string? Error)> RegisterShopperAsync(ShopperRegisterRequest request)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return (null, "All fields are required.");
                }

                // Check if user already exists
                if (await _userRepository.EmailExistsAsync(request.Email) || await _userRepository.UsernameExistsAsync(request.Username))
                {
                    _logger.LogWarning("Registration attempt with existing email or username: {Email}/{Username}", request.Email, request.Username);
                    return (null, "Email or username already exists.");
                }

                var user = new User
                {
                    Name = request.Username, // Using username as name if no name provided
                    Email = request.Email,
                    Username = request.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = Role.Shopper,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createdUser = await _userRepository.AddAsync(user);
                _logger.LogInformation("New shopper registered: {Email} (ID: {UserId})", user.Email, user.Id);

                var token = _tokenService.CreateToken(createdUser);
                var response = new AuthResponse(createdUser.Id, createdUser.Email, createdUser.Username, createdUser.Role.ToString(), token);
                return (response, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during shopper registration with email: {Email}", request.Email);
                return (null, "An error occurred during registration. Please try again.");
            }
        }

        public async Task<(AuthResponse? Response, string? Error)> RegisterByAdminAsync(AdminRegisterRequest request)
        {
            try
            {
                // Validate role
                if (!Enum.TryParse<Role>(request.Role, true, out var role) || role != Role.Employee)
                {
                    return (null, "Invalid role specified. Only Employee role can be created.");
                }

                // Validate input
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return (null, "All fields are required.");
                }

                // Check if user already exists
                if (await _userRepository.EmailExistsAsync(request.Email) || await _userRepository.UsernameExistsAsync(request.Username))
                {
                    _logger.LogWarning("Admin registration attempt with existing email or username: {Email}/{Username}", request.Email, request.Username);
                    return (null, "Email or username already exists.");
                }

                var user = new User
                {
                    Name = request.Username, // Using username as name if no name provided
                    Email = request.Email,
                    Username = request.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = role,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createdUser = await _userRepository.AddAsync(user);
                _logger.LogInformation("New employee created by admin: {Email} (ID: {UserId})", user.Email, user.Id);

                var token = _tokenService.CreateToken(createdUser);
                var response = new AuthResponse(createdUser.Id, createdUser.Email, createdUser.Username, createdUser.Role.ToString(), token);
                return (response, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during admin registration for email: {Email}", request.Email);
                return (null, "An error occurred during registration. Please try again.");
            }
        }

        public async Task<(AuthResponse? Response, string? Error)> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent)
        {
            try
            {
                // AC #1: Login user with valid role - Validate input
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    _logger.LogWarning("Login attempt with missing email or password");
                    return (null, "Email and password are required.");
                }

                // AC #1: Find user by email
                var user = await _userRepository.GetByEmailAsync(request.Email);

                // AC #3: Invalid login - User not found or password incorrect
                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Failed login attempt for email: {Email} - Invalid credentials", request.Email);
                    return (null, "Incorrect email or password.");
                }

                // AC #1: Check if user is active and has valid role
                if (!user.IsActive)
                {
                    _logger.LogWarning("Login attempt for inactive user: {Email}", request.Email);
                    return (null, "Account is inactive. Please contact support.");
                }

                // Validate role exists and is valid
                if (!Enum.IsDefined(typeof(Role), user.Role))
                {
                    _logger.LogWarning("Login attempt for user with invalid role: {Email}, Role: {Role}", request.Email, user.Role);
                    return (null, "Account has an invalid role. Please contact support.");
                }

                // AC #1: User successfully logs in - Update last login
                user.LastLoginAt = DateTime.UtcNow;
                await _userRepository.UpdateUserAsync(user);

                // Generate token FIRST to get its JTI
                var tokenString = _tokenService.CreateToken(user);
                var jwtHandler = new JwtSecurityTokenHandler();
                var jwtToken = jwtHandler.ReadJwtToken(tokenString);
                var jti = jwtToken.Id;

                // AC #2 & #3: Session record - Create new session with all data
                var session = new Session
                {
                    UserId = user.Id,
                    Status = SessionStatus.Active,
                    Ip = ipAddress,
                    UserAgent = userAgent,
                    TokenJtiHash = jti, // Store the JTI of the token
                    CreatedAt = DateTime.UtcNow
                };
                await _userRepository.CreateSessionAsync(session);

                _logger.LogInformation("Successful login for user: {Email} (ID: {UserId}), Role: {Role}, Session: {SessionId}", 
                    user.Email, user.Id, user.Role, session.Id);

                var response = new AuthResponse(user.Id, user.Email, user.Username, user.Role.ToString(), tokenString);
                return (response, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
                return (null, "An error occurred during login. Please try again.");
            }
        }

        public async Task<(bool Success, string? Error)> LogoutAsync(ClaimsPrincipal userPrincipal)
        {
            try
            {
                // Validate token contains JTI
                var jtiClaim = userPrincipal.FindFirst(JwtRegisteredClaimNames.Jti);
                if (jtiClaim == null)
                {
                    _logger.LogWarning("Logout attempt with invalid token - no JTI claim");
                    return (false, "Invalid token format.");
                }

                var jti = jtiClaim.Value;
                var userIdClaim = userPrincipal.FindFirst(ClaimTypes.NameIdentifier);
                var userId = userIdClaim?.Value;

                // AC #2: Unsuccessfully logout - Check if session exists and is active
                var activeSession = await _userRepository.GetActiveSessionByJtiAsync(jti);

                if (activeSession == null)
                {
                    _logger.LogWarning("Logout attempt for non-existent or inactive session. JTI: {JTI}, User: {UserId}", jti, userId);
                    return (false, "No active session found. User may already be logged out.");
                }

                // AC #1: User successfully logs out - Close the session
                activeSession.Status = SessionStatus.Closed;
                activeSession.ClosedAt = DateTime.UtcNow;
                await _userRepository.UpdateSessionAsync(activeSession);

                _logger.LogInformation("Successful logout for user: {UserId}, Session: {SessionId}", activeSession.UserId, activeSession.Id);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return (false, "An error occurred during logout. Please try again.");
            }
        }

        // Helper method to get active session info (useful for debugging)
        public async Task<Session?> GetActiveSessionAsync(string jti)
        {
            try
            {
                return await _userRepository.GetActiveSessionByJtiAsync(jti);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active session for JTI: {JTI}", jti);
                return null;
            }
        }
    }
}