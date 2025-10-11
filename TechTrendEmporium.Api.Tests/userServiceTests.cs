using Data.Entities;
using Data.Entities.Enums;
using External.FakeStore;
using Logica.Interfaces;
using Logica.Models.Auth.Create;
using Logica.Services;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace TechTrendEmporium.Api.Tests.Services;

public class UserServiceTests
{
    // Mocks for the dependencies of UserService
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IFakeStoreApiService> _mockFakeStoreApiService;
    private readonly Mock<IExternalMappingRepository> _mockExternalMappingRepository;

    // The instance of the service we are going to test
    private readonly UserService _userService;

    public UserServiceTests()
    {
        // Setup a clean environment for each test
        _mockUserRepository = new Mock<IUserRepository>();
        _mockFakeStoreApiService = new Mock<IFakeStoreApiService>();
        _mockExternalMappingRepository = new Mock<IExternalMappingRepository>();

        // Create the service instance with the mocked dependencies
        _userService = new UserService(
            _mockUserRepository.Object,
            _mockFakeStoreApiService.Object,
            _mockExternalMappingRepository.Object);
    }

    [Fact]
    public async Task GetUserByIdAsync_ShouldReturnUserResponse_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userEntity = new User
        {
            Id = userId,
            Name = "Test User",
            Email = "test@example.com",
            Username = "testuser",
            Role = Role.Shopper
        };

        // We configure the mock repository to return our test user when GetByIdAsync is called.
        _mockUserRepository.Setup(repo => repo.GetByIdAsync(userId, default)).ReturnsAsync(userEntity);

        // Act
        // We call the method we want to test.
        var result = await _userService.GetUserByIdAsync(userId);

        // Assert
        // We verify that the result is what we expected.
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal("Test User", result.Name);
        Assert.Equal("Shopper", result.Role);
    }

    [Fact]
    public async Task GetUserByIdAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        // We configure the mock to return null, simulating that the user was not found.
        _mockUserRepository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((User)null);

        // Act
        var result = await _userService.GetUserByIdAsync(Guid.NewGuid());

        // Assert
        // We verify that the result is null.
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldReturnUser_WhenDataIsValid()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Name = "New User",
            Username = "newuser",
            Email = "new@example.com",
            Password = "Password123!",
            Role = Role.Shopper
        };
        var userEntity = new User { Id = Guid.NewGuid(), Name = request.Name, Email = request.Email, Username = request.Username, Role = request.Role };

        // We simulate that the user does not exist yet.
        _mockUserRepository.Setup(repo => repo.EmailExistsAsync(request.Email, default)).ReturnsAsync(false);
        _mockUserRepository.Setup(repo => repo.UsernameExistsAsync(request.Username, default)).ReturnsAsync(false);
        // We configure the mock to return the created user entity.
        _mockUserRepository.Setup(repo => repo.AddAsync(It.IsAny<User>(), default)).ReturnsAsync(userEntity);

        // Act
        var (user, error) = await _userService.CreateUserAsync(request);

        // Assert
        Assert.Null(error);
        Assert.NotNull(user);
        Assert.Equal(request.Email, user.Email);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldReturnError_WhenEmailExists()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Name = "New User",
            Username = "newuser",
            Email = "new@example.com",
            Password = "Password123!",
            Role = Role.Shopper
        };

        // We simulate that the email already exists in the database.
        _mockUserRepository.Setup(repo => repo.EmailExistsAsync(request.Email, default)).ReturnsAsync(true);

        // Act
        var (user, error) = await _userService.CreateUserAsync(request);

        // Assert
        Assert.Null(user);
        Assert.NotNull(error);
        Assert.Equal("This email or user already exist.", error);
    }
}