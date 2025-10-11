using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Entities;
using Data.Entities.Enums;
using External.FakeStore;
using Logica.Interfaces;
using Logica.Models;
using Logica.Models.Auth;
using Logica.Models.Category;
using Logica.Models.Category.Requests;
using Logica.Services;
using Moq;
using Xunit;

namespace TechTrendEmporium.Api.Tests.Services;

public class CategoryServiceTests
{
    // Mocks for all service dependencies
    private readonly Mock<ICategoryRepository> _mockCategoryRepository;
    private readonly Mock<IFakeStoreApiService> _mockFakeStoreApiService;
    private readonly Mock<IUserService> _mockUserService;

    // The service instance to be tested
    private readonly CategoryService _categoryService;

    public CategoryServiceTests()
    {
        // Initialize mocks and the service for each test
        _mockCategoryRepository = new Mock<ICategoryRepository>();
        _mockFakeStoreApiService = new Mock<IFakeStoreApiService>();
        _mockUserService = new Mock<IUserService>();

        _categoryService = new CategoryService(
            _mockCategoryRepository.Object,
            _mockFakeStoreApiService.Object,
            _mockUserService.Object);
    }

    [Fact]
    public async Task GetCategoryByIdAsync_ShouldReturnCategory_WhenCategoryExists()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var categoryEntity = new Category { Id = categoryId, Name = "Electronics" };
        _mockCategoryRepository.Setup(repo => repo.GetByIdAsync(categoryId)).ReturnsAsync(categoryEntity);

        // Act
        var result = await _categoryService.GetCategoryByIdAsync(categoryId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(categoryId, result.Id);
    }

    [Fact]
    public async Task CreateCategoryAsync_ShouldCreateCategory_WhenDataIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userResponse = new UserResponse(userId, "Test User", "test@test.com", "testuser", "Admin");
        var createDto = new CategoryCreateDto { Name = "New Category", Slug = "new-category" };
        var createdCategory = new Category { Id = Guid.NewGuid(), Name = createDto.Name, CreatedBy = userId, State = ApprovalState.PendingApproval };

        // Setup mocks for validation checks
        _mockCategoryRepository.Setup(repo => repo.ExistsByNameAsync(createDto.Name)).ReturnsAsync(false);
        _mockCategoryRepository.Setup(repo => repo.ExistsBySlugAsync(createDto.Slug)).ReturnsAsync(false);
        _mockUserService.Setup(service => service.GetUserByIdAsync(userId, default)).ReturnsAsync(userResponse);
        _mockCategoryRepository.Setup(repo => repo.AddAsync(It.IsAny<Category>())).ReturnsAsync(createdCategory);

        // Act
        var result = await _categoryService.CreateCategoryAsync(createDto, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createDto.Name, result.Name);
        // Verify that the service correctly sets the state to PendingApproval
        _mockCategoryRepository.Verify(repo => repo.AddAsync(It.Is<Category>(c => c.State == ApprovalState.PendingApproval)), Times.Once);
    }

    [Fact]
    public async Task CreateCategoryAsync_ShouldThrowException_WhenCategoryNameExists()
    {
        // Arrange
        var createDto = new CategoryCreateDto { Name = "Existing Category", Slug = "existing-category" };
        // Simulate that a category with this name already exists
        _mockCategoryRepository.Setup(repo => repo.ExistsByNameAsync(createDto.Name)).ReturnsAsync(true);

        // Act & Assert
        // Verify that the correct exception is thrown
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _categoryService.CreateCategoryAsync(createDto, Guid.NewGuid()));
        Assert.Equal("Ya existe una categoría con ese nombre", exception.Message);
    }

    [Fact]
    public async Task UpdateCategoryAsync_ShouldUpdateCategory_WhenDataIsValid()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var updateDto = new CategoryUpdateDto { Name = "Updated Name" };
        var existingCategory = new Category { Id = categoryId, Name = "Old Name" };

        _mockCategoryRepository.Setup(repo => repo.GetByIdAsync(categoryId)).ReturnsAsync(existingCategory);
        _mockCategoryRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(existingCategory);

        // Act
        var result = await _categoryService.UpdateCategoryAsync(categoryId, updateDto);

        // Assert
        Assert.NotNull(result);
        // Verify the mock was called, implicitly testing the UpdateCategory extension method
        _mockCategoryRepository.Verify(repo => repo.UpdateAsync(It.Is<Category>(c => c.Name == "Updated Name")), Times.Once);
    }

    [Fact]
    public async Task ApproveCategoryAsync_ShouldReturnTrue_WhenApprovalIsSuccessful()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var userResponse = new UserResponse(approverId, "Approver", "approver@test.com", "approver", "SuperAdmin");

        // Simulate that the approver user exists
        _mockUserService.Setup(service => service.GetUserByIdAsync(approverId, default)).ReturnsAsync(userResponse);
        // Simulate that the repository approval succeeds
        _mockCategoryRepository.Setup(repo => repo.ApproveAsync(categoryId, approverId)).ReturnsAsync(true);

        // Act
        var result = await _categoryService.ApproveCategoryAsync(categoryId, approverId);

        // Assert
        Assert.True(result);
        _mockCategoryRepository.Verify(repo => repo.ApproveAsync(categoryId, approverId), Times.Once);
    }

    [Fact]
    public async Task ApproveCategoryAsync_ShouldThrowException_WhenApproverNotFound()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        // Simulate that the approver user does NOT exist
        _mockUserService.Setup(service => service.GetUserByIdAsync(approverId, default)).ReturnsAsync((UserResponse)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _categoryService.ApproveCategoryAsync(categoryId, approverId));
        Assert.Equal("Usuario aprobador no encontrado", exception.Message);
    }
}