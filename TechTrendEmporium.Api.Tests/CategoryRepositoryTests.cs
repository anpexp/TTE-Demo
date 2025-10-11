using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Data.Entities;
using Data.Entities.Enums;
using Logica.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TechTrendEmporium.Api.Tests.Repositories;

public class CategoryRepositoryTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CategoryRepository _categoryRepository;
    private readonly Guid _categoryWithProductId;
    private readonly Guid _categoryWithoutProductId;
    private readonly Guid _pendingCategoryId;

    public CategoryRepositoryTests()
    {
        // Setup the in-memory database for each test.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        // Seed the database with predictable data for testing.
        _categoryWithProductId = Guid.NewGuid();
        _categoryWithoutProductId = Guid.NewGuid();
        _pendingCategoryId = Guid.NewGuid();
        SeedDatabase();

        _categoryRepository = new CategoryRepository(_dbContext);
    }

    private void SeedDatabase()
    {
        var categories = new List<Category>
        {
            new Category { Id = _categoryWithProductId, Name = "Electronics", Slug = "electronics", State = ApprovalState.Approved },
            new Category { Id = _categoryWithoutProductId, Name = "Books", Slug = "books", State = ApprovalState.Approved },
            new Category { Id = _pendingCategoryId, Name = "Pending Category", Slug = "pending-category", State = ApprovalState.PendingApproval },
            new Category { Id = Guid.NewGuid(), Name = "Deleted Category", Slug = "deleted-category", State = ApprovalState.Deleted },
        };

        var products = new List<Product>
        {
            new Product { Id = Guid.NewGuid(), Title = "Laptop", CategoryId = _categoryWithProductId }
        };

        _dbContext.Categories.AddRange(categories);
        _dbContext.Products.AddRange(products);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetApprovedAsync_ShouldReturnOnlyApprovedCategories()
    {
        // Act
        var result = await _categoryRepository.GetApprovedAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count()); // Electronics and Books
        Assert.All(result, c => Assert.Equal(ApprovalState.Approved, c.State));
    }

    [Fact]
    public async Task AddAsync_ShouldAddNewCategoryToDatabase()
    {
        // Arrange
        var newCategory = new Category { Id = Guid.NewGuid(), Name = "Apparel", Slug = "apparel" };

        // Act
        var result = await _categoryRepository.AddAsync(newCategory);

        // Assert
        Assert.NotNull(result);
        var categoryInDb = await _dbContext.Categories.FindAsync(newCategory.Id);
        Assert.NotNull(categoryInDb);
        Assert.Equal("Apparel", categoryInDb.Name);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteCategory_WhenItHasNoProducts()
    {
        // Arrange
        var categoryIdToDelete = _categoryWithoutProductId;

        // Act
        var result = await _categoryRepository.DeleteAsync(categoryIdToDelete);

        // Assert
        Assert.True(result);
        var categoryInDb = await _dbContext.Categories.FindAsync(categoryIdToDelete);
        Assert.NotNull(categoryInDb);
        Assert.Equal(ApprovalState.Deleted, categoryInDb.State); // Verify it was soft-deleted.
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowException_WhenCategoryHasProducts()
    {
        // Arrange
        var categoryIdToDelete = _categoryWithProductId;

        // Act & Assert
        // Verify that the repository correctly throws an exception to prevent data integrity issues.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _categoryRepository.DeleteAsync(categoryIdToDelete));

        Assert.Equal("Can't delete a category that has asociated products.", exception.Message);
    }

    [Fact]
    public async Task ApproveAsync_ShouldChangeStateToApproved()
    {
        // Arrange
        var categoryIdToApprove = _pendingCategoryId;
        var approverId = Guid.NewGuid();

        // Act
        var result = await _categoryRepository.ApproveAsync(categoryIdToApprove, approverId);

        // Assert
        Assert.True(result);
        var categoryInDb = await _dbContext.Categories.FindAsync(categoryIdToApprove);
        Assert.NotNull(categoryInDb);
        Assert.Equal(ApprovalState.Approved, categoryInDb.State);
        Assert.Equal(approverId, categoryInDb.ApprovedBy);
    }

    [Fact]
    public async Task RejectAsync_ShouldChangeStateToDeclined()
    {
        // Arrange
        var categoryIdToReject = _pendingCategoryId;

        // Act
        var result = await _categoryRepository.RejectAsync(categoryIdToReject);

        // Assert
        Assert.True(result);
        var categoryInDb = await _dbContext.Categories.FindAsync(categoryIdToReject);
        Assert.NotNull(categoryInDb);
        Assert.Equal(ApprovalState.Declined, categoryInDb.State);
    }

    [Fact]
    public async Task ExistsByNameAsync_ShouldReturnTrue_WhenCategoryExists()
    {
        // Act
        var result = await _categoryRepository.ExistsByNameAsync("Electronics");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsByNameAsync_ShouldReturnFalse_WhenCategoryDoesNotExist()
    {
        // Act
        var result = await _categoryRepository.ExistsByNameAsync("NonExistentCategory");

        // Assert
        Assert.False(result);
    }

    // This is required by xUnit for proper cleanup of the in-memory database after tests run.
    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}