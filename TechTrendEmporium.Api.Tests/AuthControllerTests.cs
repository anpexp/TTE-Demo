using Data.Entities;
using Logica.Interfaces;
using Logica.Models.Auth;
using Logica.Models.Auth.Create;
using Logica.Models.Auth.Login;
using Logica.Models.Auth.Reponse;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using TechTrendEmporium.Api.Controllers;
using Xunit;

namespace TechTrendEmporium.Api.Tests.Controllers;

public class AuthControllerTests
{
    // Mocks for the controller dependencies
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<AuthController>> _mockLogger;

    // The controller instance to be tested
    private readonly AuthController _authController;

    public AuthControllerTests()
    {
        // Initialize mocks for a clean test environment
        _mockAuthService = new Mock<IAuthService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<AuthController>>();

        // Create the controller instance with mocked dependencies
        _authController = new AuthController(
            _mockAuthService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Mock HttpContext for methods that need it
        _authController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    // Helper method to simulate an authenticated user
    private ClaimsPrincipal CreateClaimsPrincipal(string userId, string role, string? jti = null)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        };
        if (jti != null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        }
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task RegisterShopper_ShouldReturnOk_WhenRegistrationSucceeds()
    {
        // Arrange
        var request = new ShopperRegisterRequest("test@example.com", "testuser", "Password123!");
        var authResponse = new AuthResponse(Guid.NewGuid(), request.Email, request.Username, "Shopper", "fake-token");
        _mockAuthService.Setup(s => s.RegisterShopperAsync(request)).ReturnsAsync((authResponse, null));

        // Act
        var result = await _authController.RegisterShopper(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task RegisterShopper_ShouldReturnBadRequest_WhenRegistrationFails()
    {
        // Arrange
        var request = new ShopperRegisterRequest("test@example.com", "testuser", "Password123!");
        _mockAuthService.Setup(s => s.RegisterShopperAsync(request)).ReturnsAsync((null, "User already exists."));

        // Act
        var result = await _authController.RegisterShopper(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task Login_ShouldReturnOkWithToken_WhenCredentialsAreValid()
    {
        // Arrange
        var request = new LoginRequest("test@example.com", "Password123!");
        var authResponse = new AuthResponse(Guid.NewGuid(), request.Email, "testuser", "Shopper", "fake-token");
        _mockAuthService.Setup(s => s.LoginAsync(request, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((authResponse, null));

        // Act
        var result = await _authController.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic value = okResult.Value;
        Assert.Equal("fake-token", value.token);
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenCredentialsAreInvalid()
    {
        // Arrange
        var request = new LoginRequest("test@example.com", "WrongPassword");
        _mockAuthService.Setup(s => s.LoginAsync(request, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((null, "Invalid credentials."));

        // Act
        var result = await _authController.Login(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Logout_ShouldReturnOk_WhenLogoutSucceeds()
    {
        // Arrange
        _authController.ControllerContext.HttpContext.User = CreateClaimsPrincipal(Guid.NewGuid().ToString(), "Shopper");
        _mockAuthService.Setup(s => s.LogoutAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((true, null));

        // Act
        var result = await _authController.Logout();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetCurrentSession_ShouldReturnOk_WhenSessionIsFound()
    {
        // Arrange
        var jti = Guid.NewGuid().ToString();
        var session = new Session { Id = Guid.NewGuid() };
        _authController.ControllerContext.HttpContext.User = CreateClaimsPrincipal(Guid.NewGuid().ToString(), "Shopper", jti);
        _mockAuthService.Setup(s => s.GetActiveSessionAsync(jti)).ReturnsAsync(session);

        // Act
        var result = await _authController.GetCurrentSession();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic value = okResult.Value;
        Assert.Equal(session.Id, value.sessionId);
    }

    [Fact]
    public async Task GetCurrentSession_ShouldReturnNotFound_WhenSessionIsNotFound()
    {
        // Arrange
        var jti = Guid.NewGuid().ToString();
        _authController.ControllerContext.HttpContext.User = CreateClaimsPrincipal(Guid.NewGuid().ToString(), "Shopper", jti);
        // Simulate service returns null
        _mockAuthService.Setup(s => s.GetActiveSessionAsync(jti)).ReturnsAsync((Session)null);

        // Act
        var result = await _authController.GetCurrentSession();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetCurrentSession_ShouldReturnBadRequest_WhenTokenHasNoJti()
    {
        // Arrange
        // Create a user principal without a JTI claim
        _authController.ControllerContext.HttpContext.User = CreateClaimsPrincipal(Guid.NewGuid().ToString(), "Shopper");

        // Act
        var result = await _authController.GetCurrentSession();

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}