using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Data.Entities;
using Data.Entities.Enums;
using Logica.Interfaces;
using Logica.Models.Auth;
using Logica.Models.Auth.Create;
using Logica.Models.Auth.Login;
using Logica.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;

namespace TechTrendEmporium.Api.Tests.Services;

public class AuthServiceTests
{
    // Mocks for the service dependencies
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<ILogger<AuthService>> _mockLogger;

    // The service instance to be tested
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockTokenService = new Mock<ITokenService>();
        _mockLogger = new Mock<ILogger<AuthService>>();

        _authService = new AuthService(
            _mockUserRepository.Object,
            _mockTokenService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RegisterShopperAsync_ShouldReturnResponse_WhenUserIsNew()
    {
        // Arrange
        var request = new ShopperRegisterRequest("new@example.com", "newuser", "Password123!");
        var userEntity = new User { Id = Guid.NewGuid(), Name = request.Username, Email = request.Email, Username = request.Username, Role = Role.Shopper };

        _mockUserRepository.Setup(repo => repo.EmailExistsAsync(request.Email, default)).ReturnsAsync(false);
        _mockUserRepository.Setup(repo => repo.UsernameExistsAsync(request.Username, default)).ReturnsAsync(false);
        _mockUserRepository.Setup(repo => repo.AddAsync(It.IsAny<User>(), default)).ReturnsAsync(userEntity);
        _mockTokenService.Setup(ts => ts.CreateToken(It.IsAny<User>())).Returns("fake-jwt-token");

        // Act
        var (response, error) = await _authService.RegisterShopperAsync(request);

        // Assert
        Assert.Null(error);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task RegisterShopperAsync_ShouldReturnError_WhenInputIsMissing()
    {
        // Arrange
        var request = new ShopperRegisterRequest("", "newuser", "Password123!");

        // Act
        var (response, error) = await _authService.RegisterShopperAsync(request);

        // Assert
        Assert.Null(response);
        Assert.NotNull(error);
        Assert.Equal("All fields are required.", error);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnResponse_WhenCredentialsAreValid()
    {
        // Arrange
        var testPassword = "Password123!";
        var request = new LoginRequest("test@example.com", testPassword);
        var userEntity = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(testPassword),
            IsActive = true,
            Role = Role.Shopper
        };
        _mockUserRepository.Setup(repo => repo.GetByEmailAsync(request.Email, default)).ReturnsAsync(userEntity);

        var jti = Guid.NewGuid().ToString();
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(claims: new[] { new Claim(JwtRegisteredClaimNames.Jti, jti) });
        _mockTokenService.Setup(ts => ts.CreateToken(userEntity)).Returns(handler.WriteToken(token));

        // Act
        var (response, error) = await _authService.LoginAsync(request, "127.0.0.1", "Test Agent");

        // Assert
        Assert.Null(error);
        Assert.NotNull(response);
        _mockUserRepository.Verify(repo => repo.CreateSessionAsync(It.Is<Session>(s => s.TokenJtiHash == jti), default), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnError_WhenUserIsInactive()
    {
        // Arrange
        var testPassword = "Password123!";
        var request = new LoginRequest("inactive@example.com", testPassword);
        var userEntity = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(testPassword),
            IsActive = false // User is inactive
        };
        _mockUserRepository.Setup(repo => repo.GetByEmailAsync(request.Email, default)).ReturnsAsync(userEntity);

        // Act
        var (response, error) = await _authService.LoginAsync(request, null, null);

        // Assert
        Assert.Null(response);
        Assert.NotNull(error);
        Assert.Equal("Account is inactive. Please contact support.", error);
    }

    [Fact]
    public async Task LogoutAsync_ShouldReturnSuccess_WhenSessionExists()
    {
        // Arrange
        var jti = Guid.NewGuid().ToString();
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Jti, jti) };
        var identity = new ClaimsIdentity(claims);
        var userPrincipal = new ClaimsPrincipal(identity);
        var activeSession = new Session { Status = SessionStatus.Active };
        _mockUserRepository.Setup(repo => repo.GetActiveSessionByJtiAsync(jti, default)).ReturnsAsync(activeSession);

        // Act
        var (success, error) = await _authService.LogoutAsync(userPrincipal);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        _mockUserRepository.Verify(repo => repo.UpdateSessionAsync(activeSession, default), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_ShouldReturnError_WhenTokenHasNoJti()
    {
        // Arrange
        var identity = new ClaimsIdentity(); // Identity without any claims
        var userPrincipal = new ClaimsPrincipal(identity);

        // Act
        var (success, error) = await _authService.LogoutAsync(userPrincipal);

        // Assert
        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("Invalid token format.", error);
    }

    [Fact]
    public async Task RegisterByAdminAsync_ShouldCreateEmployee_WhenRequestIsValid()
    {
        // Arrange
        var request = new AdminRegisterRequest("new.employee@example.com", "newemployee", "Password123!", "Employee");
        var userEntity = new User { Id = Guid.NewGuid(), Email = request.Email, Role = Role.Employee };

        _mockUserRepository.Setup(repo => repo.EmailExistsAsync(request.Email, default)).ReturnsAsync(false);
        _mockUserRepository.Setup(repo => repo.UsernameExistsAsync(request.Username, default)).ReturnsAsync(false);
        _mockUserRepository.Setup(repo => repo.AddAsync(It.IsAny<User>(), default)).ReturnsAsync(userEntity);
        _mockTokenService.Setup(ts => ts.CreateToken(It.IsAny<User>())).Returns("fake-admin-created-token");

        // Act
        var (response, error) = await _authService.RegisterByAdminAsync(request);

        // Assert
        Assert.Null(error);
        Assert.NotNull(response);
        _mockUserRepository.Verify(repo => repo.AddAsync(It.Is<User>(u => u.Role == Role.Employee), default), Times.Once);
    }

    [Fact]
    public async Task GetActiveSessionAsync_ShouldReturnSession_WhenFound()
    {
        // Arrange
        var jti = Guid.NewGuid().ToString();
        var session = new Session { Id = Guid.NewGuid(), TokenJtiHash = jti };
        _mockUserRepository.Setup(repo => repo.GetActiveSessionByJtiAsync(jti, default)).ReturnsAsync(session);

        // Act
        var result = await _authService.GetActiveSessionAsync(jti);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jti, result.TokenJtiHash);
    }
}