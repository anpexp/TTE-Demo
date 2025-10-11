using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Data.Entities;
using Data.Entities.Enums;
using Logica.Models;
using Logica.Models.Products;
using Logica.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TechTrendEmporium.Api.Tests.Services;

public class StoreServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly StoreService _storeService;

    public StoreServiceTests()
    {
        // Setup the in-memory database for each test.
        // A unique name ensures that tests are isolated from each other.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        SeedDatabase(); // Populate the in-memory database with test data.

        _storeService = new StoreService(_dbContext);
    }

    private void SeedDatabase()
    {
        var products = new List<Product>
        {
            new Product { Id = Guid.NewGuid(), Title = "Gaming Laptop", Price = 1500, RatingAverage = 4.5m, State = ApprovalState.Approved },
            new Product { Id = Guid.NewGuid(), Title = "Office Laptop", Price = 900, RatingAverage = 4.0m, State = ApprovalState.Approved },
            new Product { Id = Guid.NewGuid(), Title = "Smartphone", Price = 1100, RatingAverage = 4.8m, State = ApprovalState.Approved },
            new Product { Id = Guid.NewGuid(), Title = "Smartwatch", Price = 300, RatingAverage = 4.2m, State = ApprovalState.Approved },
            new Product { Id = Guid.NewGuid(), Title = "Wireless Headphones", Price = 150, RatingAverage = 4.6m, State = ApprovalState.Approved },
            // This product should not be returned by the queries
            new Product { Id = Guid.NewGuid(), Title = "Pending Product", Price = 500, RatingAverage = 3.0m, State = ApprovalState.PendingApproval }
        };

        _dbContext.Products.AddRange(products);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetProductsAsync_ShouldOnlyReturnApprovedProducts()
    {
        // Arrange
        var query = new ProductQuery(); // Default query

        // Act
        var result = await _storeService.GetProductsAsync(query, default);

        // Assert
        // The seeded database has 6 products, but only 5 are 'Approved'.
        Assert.Equal(5, result.TotalItems);
        Assert.DoesNotContain(result.Items, p => p.Title == "Pending Product");
    }

    [Fact]
    public async Task GetProductsAsync_ShouldApplyPaginationCorrectly()
    {
        // Arrange
        // Request the second page, with 2 items per page.
        var query = new ProductQuery { Page = 2, PageSize = 2 };

        // Act
        var result = await _storeService.GetProductsAsync(query, default);

        // Assert
        Assert.Equal(2, result.Items.Count());
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        // The default sort is by Title ascending.
        Assert.Equal("Office Laptop", result.Items.First().Title);
        Assert.Equal("Smartphone", result.Items.Last().Title);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldFilterByTitle()
    {
        // Arrange
        var query = new ProductQuery { Title = "Laptop" };

        // Act
        var result = await _storeService.GetProductsAsync(query, default);

        // Assert
        Assert.Equal(2, result.TotalItems);
        Assert.All(result.Items, item => Assert.Contains("Laptop", item.Title));
    }

    [Fact]
    public async Task GetProductsAsync_ShouldFilterByMaxPrice()
    {
        // Arrange
        var query = new ProductQuery { Price = 1000 };

        // Act
        var result = await _storeService.GetProductsAsync(query, default);

        // Assert
        Assert.Equal(3, result.TotalItems); // Office Laptop (900), Smartwatch (300), Headphones (150)
        Assert.All(result.Items, item => Assert.True(item.Price <= 1000));
    }

    [Fact]
    public async Task GetProductsAsync_ShouldSortByPriceDescending()
    {
        // Arrange
        var query = new ProductQuery { SortBy = ProductSortBy.Price, SortDir = SortDirection.Desc };

        // Act
        var result = await _storeService.GetProductsAsync(query, default);

        // Assert
        // The most expensive approved item is 'Gaming Laptop' at 1500.
        Assert.Equal("Gaming Laptop", result.Items.First().Title);
        Assert.Equal(1500, result.Items.First().Price);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldSortByRatingAscending()
    {
        // Arrange
        var query = new ProductQuery { SortBy = ProductSortBy.Rating, SortDir = SortDirection.Asc };

        // Act
        var result = await _storeService.GetProductsAsync(query, default);

        // Assert
        // The lowest-rated approved item is 'Office Laptop' with a rating of 4.0.
        Assert.Equal("Office Laptop", result.Items.First().Title);
    }

    // This is required by xUnit for proper cleanup of the in-memory database after tests run.
    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}