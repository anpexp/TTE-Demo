using Data.Entities;
using Data.Entities.Enums;
using Logica.Interfaces;
using Logica.Models;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using BCrypt.Net;
using Microsoft.Extensions.Logging;

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
                    return (null, "Todos los campos son requeridos.");
                }

                // Check if user already exists
                if (await _userRepository.EmailExistsAsync(request.Email) || await _userRepository.UsernameExistsAsync(request.Username))
                {
                    _logger.LogWarning("Intento de registro con email o nombre de usuario existente: {Email}/{Username}", request.Email, request.Username);
                    return (null, "El email o nombre de usuario ya existe.");
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
                _logger.LogInformation("Nuevo shopper registrado: {Email} (ID: {UserId})", user.Email, user.Id);

                var token = _tokenService.CreateToken(createdUser);
                var response = new AuthResponse(createdUser.Id, createdUser.Email, createdUser.Username, createdUser.Role.ToString(), token);
                return (response, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el registro del shopper con email: {Email}", request.Email);
                return (null, "Ocurrió un error durante el registro. Por favor, inténtelo de nuevo.");
            }
        }

        public async Task<(AuthResponse? Response, string? Error)> RegisterByAdminAsync(AdminRegisterRequest request)
        {
            try
            {
                // Validate role
                if (!Enum.TryParse<Role>(request.Role, true, out var role) || role != Role.Employee)
                {
                    return (null, "Rol inválido especificado. Solo se puede crear el rol de Empleado.");
                }

                // Validate input
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return (null, "Todos los campos son requeridos.");
                }

                // Check if user already exists
                if (await _userRepository.EmailExistsAsync(request.Email) || await _userRepository.UsernameExistsAsync(request.Username))
                {
                    _logger.LogWarning("Intento de registro por admin con email o nombre de usuario existente: {Email}/{Username}", request.Email, request.Username);
                    return (null, "El email o nombre de usuario ya existe.");
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
                _logger.LogInformation("Nuevo empleado creado por admin: {Email} (ID: {UserId})", user.Email, user.Id);

                var token = _tokenService.CreateToken(createdUser);
                var response = new AuthResponse(createdUser.Id, createdUser.Email, createdUser.Username, createdUser.Role.ToString(), token);
                return (response, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el registro por admin para email: {Email}", request.Email);
                return (null, "Ocurrió un error durante el registro. Por favor, inténtelo de nuevo.");
            }
        }

        public async Task<(AuthResponse? Response, string? Error)> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent)
        {
            try
            {
                // AC #1: Login user with valid role - Validate input
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    _logger.LogWarning("Intento de inicio de sesión con email o contraseña faltantes");
                    return (null, "Se requieren email y contraseña.");
                }

                // AC #1: Find user by email
                var user = await _userRepository.GetByEmailAsync(request.Email);

                // AC #3: Invalid login - User not found or password incorrect
                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Error en el intento de inicio de sesión para el email: {Email} - Credenciales inválidas", request.Email);
                    return (null, "Email o contraseña incorrectos.");
                }

                // AC #1: Check if user is active and has valid role
                if (!user.IsActive)
                {
                    _logger.LogWarning("Intento de inicio de sesión para un usuario inactivo: {Email}", request.Email);
                    return (null, "La cuenta está inactiva. Por favor, contacte al soporte.");
                }

                // Validate role exists and is valid
                if (!Enum.IsDefined(typeof(Role), user.Role))
                {
                    _logger.LogWarning("Intento de inicio de sesión para un usuario con rol inválido: {Email}, Rol: {Role}", request.Email, user.Role);
                    return (null, "La cuenta tiene un rol inválido. Por favor, contacte al soporte.");
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

                _logger.LogInformation("Inicio de sesión exitoso para el usuario: {Email} (ID: {UserId}), Rol: {Role}, Sesión: {SessionId}", 
                    user.Email, user.Id, user.Role, session.Id);

                var response = new AuthResponse(user.Id, user.Email, user.Username, user.Role.ToString(), tokenString);
                return (response, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el inicio de sesión para el email: {Email}", request.Email);
                return (null, "Ocurrió un error durante el inicio de sesión. Por favor, inténtelo de nuevo.");
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
                    _logger.LogWarning("Intento de cierre de sesión con token inválido - sin reclamo JTI");
                    return (false, "Formato de token inválido.");
                }

                var jti = jtiClaim.Value;
                var userIdClaim = userPrincipal.FindFirst(ClaimTypes.NameIdentifier);
                var userId = userIdClaim?.Value;

                // AC #2: Unsuccessfully logout - Check if session exists and is active
                var activeSession = await _userRepository.GetActiveSessionByJtiAsync(jti);

                if (activeSession == null)
                {
                    _logger.LogWarning("Intento de cierre de sesión para una sesión inexistente o inactiva. JTI: {JTI}, Usuario: {UserId}", jti, userId);
                    return (false, "No se encontró una sesión activa. El usuario puede que ya haya cerrado sesión.");
                }

                // AC #1: User successfully logs out - Close the session
                activeSession.Status = SessionStatus.Closed;
                activeSession.ClosedAt = DateTime.UtcNow;
                await _userRepository.UpdateSessionAsync(activeSession);

                _logger.LogInformation("Cierre de sesión exitoso para el usuario: {UserId}, Sesión: {SessionId}", activeSession.UserId, activeSession.Id);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el cierre de sesión");
                return (false, "Ocurrió un error durante el cierre de sesión. Por favor, inténtelo de nuevo.");
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
                _logger.LogError(ex, "Error al obtener la sesión activa para JTI: {JTI}", jti);
                return null;
            }
        }
    }
}