using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Data.Entities;
using Data.Entities.Enums;
using External.FakeStore;
using Logica.Interfaces;
using Logica.Models.Products;
using Logica.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.EntityFrameworkCore; // Required for DbContext mocking
using Xunit;

namespace TechTrendEmporium.Api.Tests.Services;

public class ProductServiceTests
{
    // Mocks for all dependencies
    private readonly Mock<IFakeStoreApiService> _mockFakeStoreClient;
    private readonly Mock<IProductRepository> _mockProductRepository;
    private readonly Mock<IExternalMappingRepository> _mockExternalMappingRepository; // FIX: Added missing mock
    private readonly Mock<AppDbContext> _mockContext;
    private readonly Mock<ILogger<ProductService>> _mockLogger;

    // The service instance we are testing
    private readonly ProductService _productService;

    public ProductServiceTests()
    {
        // Initialize mocks
        _mockFakeStoreClient = new Mock<IFakeStoreApiService>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockExternalMappingRepository = new Mock<IExternalMappingRepository>(); // FIX: Initialize the mock
        _mockContext = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
        _mockLogger = new Mock<ILogger<ProductService>>();

        // Initialize the service with all mocked dependencies
        _productService = new ProductService(
            _mockFakeStoreClient.Object,
            _mockProductRepository.Object,
            _mockExternalMappingRepository.Object, // FIX: Pass the new mock to the constructor
            _mockContext.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetProductByIdAsync_ShouldReturnProductDto_WhenProductExists()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productEntity = new Product { Id = productId, Title = "Test Product" };
        _mockProductRepository.Setup(repo => repo.GetByIdAsync(productId)).ReturnsAsync(productEntity);

        // Act
        var result = await _productService.GetProductByIdAsync(productId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(productId, result.Id);
    }

    [Fact]
    public async Task GetProductByIdAsync_ShouldReturnNull_WhenProductDoesNotExist()
    {
        // Arrange
        _mockProductRepository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Product)null);

        // Act
        var result = await _productService.GetProductByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateProductAsync_ShouldCreateProductWithPendingState_WhenCategoryIsNew()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var productDto = new ProductCreateDto { Title = "New Gadget", Category = "New Tech" };
        var createdProductEntity = new Product { Id = Guid.NewGuid(), Title = productDto.Title, State = ApprovalState.PendingApproval };

        // Setup mock for GetOrCreateCategoryAsync: simulate category does not exist.
        _mockContext.Setup(x => x.Categories).ReturnsDbSet(new List<Category>());
        _mockProductRepository.Setup(repo => repo.CreateAsync(It.IsAny<Product>())).ReturnsAsync(createdProductEntity);

        // Act
        var result = await _productService.CreateProductAsync(productDto, userId);

        // Assert
        Assert.NotNull(result);
        // Verify that the product is created with PendingApproval state.
        _mockProductRepository.Verify(repo => repo.CreateAsync(It.Is<Product>(p => p.State == ApprovalState.PendingApproval)), Times.Once);
        // Verify that a new category was added to the context.
        _mockContext.Verify(x => x.Categories.Add(It.Is<Category>(c => c.Name == "New Tech")), Times.Once);
    }

    [Fact]
    public async Task ApproveProductAsync_ShouldChangeStateToApproved_WhenProductExists()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var productToApprove = new Product { Id = productId, State = ApprovalState.PendingApproval };
        _mockProductRepository.Setup(repo => repo.GetByIdAsync(productId)).ReturnsAsync(productToApprove);

        // Act
        var result = await _productService.ApproveProductAsync(productId, approverId);

        // Assert
        Assert.True(result);
        // Verify that the product's state was changed to Approved.
        Assert.Equal(ApprovalState.Approved, productToApprove.State);
        Assert.Equal(approverId, productToApprove.ApprovedBy);
        _mockProductRepository.Verify(repo => repo.UpdateAsync(It.Is<Product>(p => p.State == ApprovalState.Approved)), Times.Once);
    }
}