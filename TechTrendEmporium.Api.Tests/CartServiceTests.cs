using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Entities;
using Data.Entities.Enums;
using External.FakeStore;
using Logica.Interfaces;
using Logica.Models.Carts;
using Logica.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace TechTrendEmporium.Api.Tests.Services;

public class CartServiceTests
{
    // Mocks for all service dependencies
    private readonly Mock<IFakeStoreApiService> _mockFakeStoreApiService;
    private readonly Mock<IExternalMappingRepository> _mockExternalMappingRepository;
    private readonly Mock<ICartRepository> _mockCartRepository;
    private readonly Mock<IProductRepository> _mockProductRepository;
    private readonly Mock<ILogger<CartService>> _mockLogger;

    // The service instance to be tested
    private readonly CartService _cartService;

    public CartServiceTests()
    {
        // Initialize mocks for a clean test environment
        _mockFakeStoreApiService = new Mock<IFakeStoreApiService>();
        _mockExternalMappingRepository = new Mock<IExternalMappingRepository>();
        _mockCartRepository = new Mock<ICartRepository>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockLogger = new Mock<ILogger<CartService>>();

        // Create the service with the mocked dependencies
        _cartService = new CartService(
            _mockFakeStoreApiService.Object,
            _mockExternalMappingRepository.Object,
            _mockCartRepository.Object,
            _mockProductRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetActiveCartByUserIdAsync_ShouldReturnCart_WhenCartExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var cartEntity = new Cart { Id = cartId, UserId = userId, Status = CartStatus.Active };

        // Setup the repository to return the active cart.
        _mockCartRepository.Setup(repo => repo.GetActiveCartByUserIdAsync(userId)).ReturnsAsync(cartEntity);

        // Act
        var result = await _cartService.GetActiveCartByUserIdAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(cartId, result.Id);
    }

    [Fact]
    public async Task AddItemToUserCartAsync_ShouldCreateNewCart_WhenUserHasNoActiveCart()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var request = new AddItemToCartRequest { ProductId = productId, Quantity = 1 };
        var productEntity = new Product { Id = productId, Title = "Test Product", Price = 100, InventoryAvailable = 10 };
        var newCart = new Cart { Id = Guid.NewGuid(), UserId = userId, Status = CartStatus.Active };

        // Simulate that the user has no active cart.
        _mockCartRepository.Setup(repo => repo.GetActiveCartByUserIdAsync(userId)).ReturnsAsync((Cart)null);
        // Simulate product exists and has stock.
        _mockProductRepository.Setup(repo => repo.GetByIdAsync(productId)).ReturnsAsync(productEntity);
        // Simulate cart creation.
        _mockCartRepository.Setup(repo => repo.CreateCartAsync(It.IsAny<Cart>())).ReturnsAsync(newCart);
        // Setup the final GetCartByIdAsync call to return the cart with items.
        _mockCartRepository.Setup(repo => repo.GetCartByIdAsync(It.IsAny<Guid>())).ReturnsAsync(newCart);

        // Act
        var result = await _cartService.AddItemToUserCartAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        // Verify that a new cart was created.
        _mockCartRepository.Verify(repo => repo.CreateCartAsync(It.Is<Cart>(c => c.UserId == userId)), Times.Once);
        // Verify that stock was reserved.
        _mockProductRepository.Verify(repo => repo.UpdateAsync(It.Is<Product>(p => p.InventoryAvailable == 9)), Times.Once);
    }

    [Fact]
    public async Task AddItemToUserCartAsync_ShouldThrowException_WhenStockIsInsufficient()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var request = new AddItemToCartRequest { ProductId = productId, Quantity = 5 };
        // Product only has 2 units available, but 5 are requested.
        var productEntity = new Product { Id = productId, Title = "Test Product", Price = 100, InventoryAvailable = 2 };
        var activeCart = new Cart { Id = Guid.NewGuid(), UserId = userId, Status = CartStatus.Active };

        _mockCartRepository.Setup(repo => repo.GetActiveCartByUserIdAsync(userId)).ReturnsAsync(activeCart);
        _mockProductRepository.Setup(repo => repo.GetByIdAsync(productId)).ReturnsAsync(productEntity);

        // Act & Assert
        // We expect an InvalidOperationException because there is not enough stock.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cartService.AddItemToUserCartAsync(userId, request));

        Assert.Contains("Not enough available stock", exception.Message);
    }

    [Fact]
    public async Task CheckoutUserCartAsync_ShouldSucceed_WhenCartIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var product = new Product { Id = productId, InventoryTotal = 10, InventoryAvailable = 8 };
        var cartItem = new CartItem { ProductId = productId, Quantity = 2 };
        var cartEntity = new Cart { Id = Guid.NewGuid(), UserId = userId, Status = CartStatus.Active, CartItems = new List<CartItem> { cartItem } };

        _mockCartRepository.Setup(repo => repo.GetActiveCartByUserIdAsync(userId)).ReturnsAsync(cartEntity);
        _mockProductRepository.Setup(repo => repo.GetByIdAsync(productId)).ReturnsAsync(product);
        _mockCartRepository.Setup(repo => repo.UpdateCartAsync(It.IsAny<Cart>())).ReturnsAsync(cartEntity);

        // Act
        var result = await _cartService.CheckoutUserCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        // Verify the cart status is updated to CheckedOut.
        Assert.Equal(CartStatus.CheckedOut, cartEntity.Status);
        // Verify the total inventory was correctly updated (10 - 2 = 8).
        Assert.Equal(8, product.InventoryTotal);
        _mockProductRepository.Verify(repo => repo.UpdateAsync(product), Times.Once);
        _mockCartRepository.Verify(repo => repo.UpdateCartAsync(cartEntity), Times.Once);
    }

    [Fact]
    public async Task CheckoutUserCartAsync_ShouldThrowException_WhenCartIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var emptyCart = new Cart { Id = Guid.NewGuid(), UserId = userId, Status = CartStatus.Active, CartItems = new List<CartItem>() };

        _mockCartRepository.Setup(repo => repo.GetActiveCartByUserIdAsync(userId)).ReturnsAsync(emptyCart);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cartService.CheckoutUserCartAsync(userId));

        Assert.Equal("Cannot checkout empty cart", exception.Message);
    }
    [Fact]
    public async Task UpdateItemInUserCartAsync_ShouldIncreaseQuantity_WhenStockIsAvailable()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var request = new UpdateCartItemQuantityRequest { ProductId = productId, Quantity = 5 }; // New quantity

        var product = new Product { Id = productId, InventoryTotal = 20, InventoryAvailable = 10 };
        var cartItem = new CartItem { ProductId = productId, Quantity = 2 }; // Current quantity
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId, Status = CartStatus.Active, CartItems = new List<CartItem> { cartItem } };

        _mockCartRepository.Setup(repo => repo.GetActiveCartByUserIdAsync(userId)).ReturnsAsync(cart);
        _mockProductRepository.Setup(repo => repo.GetByIdAsync(productId)).ReturnsAsync(product);
        _mockCartRepository.Setup(repo => repo.GetCartByIdAsync(cart.Id)).ReturnsAsync(cart); // For final recalculation

        // Act
        var result = await _cartService.UpdateItemInUserCartAsync(userId, request);

        // Assert
        // Verify that the stock was correctly reserved (10 available - (5 new - 2 old) = 7)
        Assert.Equal(7, product.InventoryAvailable);
        // Verify that the repository was called to update the product's stock
        _mockProductRepository.Verify(repo => repo.UpdateAsync(It.Is<Product>(p => p.Id == productId)), Times.Once);
        // Verify that the cart item quantity was updated
        _mockCartRepository.Verify(repo => repo.AddOrUpdateCartItemAsync(cart.Id, productId, 5, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateItemInUserCartAsync_ShouldRemoveItem_WhenQuantityIsZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var request = new UpdateCartItemQuantityRequest { ProductId = productId, Quantity = 0 };

        var product = new Product { Id = productId, InventoryTotal = 20, InventoryAvailable = 8 };
        var cartItem = new CartItem { ProductId = productId, Quantity = 2 }; // Current quantity
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId, Status = CartStatus.Active, CartItems = new List<CartItem> { cartItem } };

        _mockCartRepository.Setup(repo => repo.GetActiveCartByUserIdAsync(userId)).ReturnsAsync(cart);
        _mockProductRepository.Setup(repo => repo.GetByIdAsync(productId)).ReturnsAsync(product);
        _mockCartRepository.Setup(repo => repo.GetCartByIdAsync(cart.Id)).ReturnsAsync(cart);

        // Act
        var result = await _cartService.UpdateItemInUserCartAsync(userId, request);

        // Assert
        // Verify that the previously reserved stock was released (8 available + 2 old = 10)
        Assert.Equal(10, product.InventoryAvailable);
        _mockProductRepository.Verify(repo => repo.UpdateAsync(It.Is<Product>(p => p.Id == productId)), Times.Once);
        // Verify that the item was removed from the cart repository
        _mockCartRepository.Verify(repo => repo.RemoveCartItemAsync(cart.Id, productId), Times.Once);
    }

    [Fact]
    public async Task UpdateItemInUserCartAsync_ShouldThrowException_WhenItemNotInCart()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var request = new UpdateCartItemQuantityRequest { ProductId = productId, Quantity = 5 };

        // Cart exists but does not contain the requested product
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId, Status = CartStatus.Active, CartItems = new List<CartItem>() };

        _mockCartRepository.Setup(repo => repo.GetActiveCartByUserIdAsync(userId)).ReturnsAsync(cart);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cartService.UpdateItemInUserCartAsync(userId, request));

        Assert.Equal("Product not found in cart", exception.Message);
    }

    [Fact]
    public async Task RemoveItemFromUserCartAsync_ShouldRemoveItemAndReleaseStock_WhenItemExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var product = new Product { Id = productId, InventoryTotal = 20, InventoryAvailable = 8 };
        var cartItem = new CartItem { ProductId = productId, Quantity = 2 }; // Current quantity in cart
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId, Status = CartStatus.Active, CartItems = new List<CartItem> { cartItem } };

        _mockCartRepository.Setup(repo => repo.GetActiveCartByUserIdAsync(userId)).ReturnsAsync(cart);
        _mockProductRepository.Setup(repo => repo.GetByIdAsync(productId)).ReturnsAsync(product);
        _mockCartRepository.Setup(repo => repo.GetCartByIdAsync(cart.Id)).ReturnsAsync(cart);

        // Act
        var result = await _cartService.RemoveItemFromUserCartAsync(userId, productId);

        // Assert
        Assert.NotNull(result);
        // Verify that the stock was released back to available (8 available + 2 removed = 10)
        Assert.Equal(10, product.InventoryAvailable);
        // Verify that the item was removed from the repository
        _mockCartRepository.Verify(repo => repo.RemoveCartItemAsync(cart.Id, productId), Times.Once);
        // Verify that the product stock update was saved
        _mockProductRepository.Verify(repo => repo.UpdateAsync(product), Times.Once);
    }
}