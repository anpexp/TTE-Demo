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

public class ProductRepositoryTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ProductRepository _productRepository;
    private readonly Guid _userOneId = Guid.NewGuid();
    private readonly Guid _categoryOneId = Guid.NewGuid();

    public ProductRepositoryTests()
    {
        // Setup the in-memory database for each test.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        SeedDatabase(); // Populate the in-memory database with test data.

        _productRepository = new ProductRepository(_dbContext);
    }

    private void SeedDatabase()
    {
        var user1 = new User { Id = _userOneId, Name = "User One" };
        var user2 = new User { Id = Guid.NewGuid(), Name = "User Two" };
        var category1 = new Category { Id = _categoryOneId, Name = "Electronics" };

        var products = new List<Product>
        {
            new Product { Id = Guid.NewGuid(), Title = "Laptop Pro", Description = "A powerful laptop.", CategoryId = _categoryOneId, CreatedBy = _userOneId, State = ApprovalState.Approved, Price = 1200 },
            new Product { Id = Guid.NewGuid(), Title = "Gaming Mouse", Description = "A precise mouse.", CategoryId = _categoryOneId, CreatedBy = _userOneId, State = ApprovalState.Approved, Price = 75 },
            new Product { Id = Guid.NewGuid(), Title = "Draft Keyboard", Description = "A mechanical keyboard.", CategoryId = _categoryOneId, CreatedBy = user2.Id, State = ApprovalState.PendingApproval, Price = 150 },
            new Product { Id = Guid.NewGuid(), Title = "Old Monitor", Description = "An old monitor.", CategoryId = _categoryOneId, CreatedBy = _userOneId, State = ApprovalState.Deleted, Price = 100 }
        };

        var mapping = new ExternalMapping { SourceId = "ext-123", Source = ExternalSource.FakeStore, SourceType = "PRODUCT", InternalId = products[0].Id };

        _dbContext.Users.AddRange(user1, user2);
        _dbContext.Categories.Add(category1);
        _dbContext.Products.AddRange(products);
        _dbContext.ExternalMappings.Add(mapping);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetByStateAsync_ShouldReturnOnlyProductsWithMatchingState()
    {
        // Act
        var pendingProducts = await _productRepository.GetByStateAsync(ApprovalState.PendingApproval);
        var approvedProducts = await _productRepository.GetByStateAsync(ApprovalState.Approved);

        // Assert
        Assert.Single(pendingProducts);
        Assert.Equal("Draft Keyboard", pendingProducts.First().Title);
        Assert.Equal(2, approvedProducts.Count());
    }

    [Fact]
    public async Task GetByCreatorIdAsync_ShouldReturnOnlyProductsFromCreator()
    {
        // Act
        var result = await _productRepository.GetByCreatorIdAsync(_userOneId);

        // Assert
        Assert.Equal(3, result.Count()); // Laptop Pro, Gaming Mouse, Old Monitor
        Assert.All(result, p => Assert.Equal(_userOneId, p.CreatedBy));
    }

    [Fact]
    public async Task CreateAsync_ShouldAddProductToDatabase()
    {
        // Arrange
        var newProduct = new Product { Id = Guid.NewGuid(), Title = "New Tablet", CreatedBy = _userOneId, CategoryId = _categoryOneId };

        // Act
        var result = await _productRepository.CreateAsync(newProduct);

        // Assert
        var productInDb = await _dbContext.Products.FindAsync(newProduct.Id);
        Assert.NotNull(productInDb);
        Assert.Equal("New Tablet", productInDb.Title);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteProduct_WhenProductExists()
    {
        // Arrange
        var productToUpdate = await _dbContext.Products.FirstAsync(p => p.State == ApprovalState.Approved);

        // Act
        var result = await _productRepository.DeleteAsync(productToUpdate.Id);

        // Assert
        Assert.True(result);
        // Refresh entity from context to check updated state
        await _dbContext.Entry(productToUpdate).ReloadAsync();
        Assert.Equal(ApprovalState.Deleted, productToUpdate.State);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowException_WhenProductIsAlreadyDeleted()
    {
        // Arrange
        var deletedProduct = await _dbContext.Products.FirstAsync(p => p.State == ApprovalState.Deleted);

        // Act & Assert
        // Verify that the repository correctly throws an exception.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _productRepository.DeleteAsync(deletedProduct.Id));

        Assert.Equal("Product already deleted.", exception.Message);
    }

    [Fact]
    public async Task GetByExternalIdAsync_ShouldReturnProduct_WhenMappingExists()
    {
        // Act
        var result = await _productRepository.GetByExternalIdAsync("ext-123", ExternalSource.FakeStore);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Laptop Pro", result.Title);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnMatchingProducts_FromTitleAndDescription()
    {
        // Act
        var result = await _productRepository.SearchAsync("laptop"); // Should match "Laptop Pro" and its description

        // Assert
        Assert.Single(result);
        Assert.Equal("Laptop Pro", result.First().Title);
    }

    [Fact]
    public async Task GetCountByStateAsync_ShouldReturnCorrectCount()
    {
        // Act
        var approvedCount = await _productRepository.GetCountByStateAsync(ApprovalState.Approved);
        var pendingCount = await _productRepository.GetCountByStateAsync(ApprovalState.PendingApproval);
        var deletedCount = await _productRepository.GetCountByStateAsync(ApprovalState.Deleted);

        // Assert
        Assert.Equal(2, approvedCount);
        Assert.Equal(1, pendingCount);
        Assert.Equal(1, deletedCount);
    }

    // This is required by xUnit for proper cleanup of the in-memory database after tests run.
    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}