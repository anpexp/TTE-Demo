using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Data.Entities;
using Data.Entities.Enums;
using Logica.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace TechTrendEmporium.Api.Tests.Repositories;

public class CartRepositoryTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CartRepository _cartRepository;
    private readonly Guid _seededUserId = Guid.NewGuid();
    private readonly Guid _seededProductId = Guid.NewGuid();
    private readonly Guid _seededCartId = Guid.NewGuid();

    public CartRepositoryTests()
    {
        // Setup the in-memory database for each test.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        SeedDatabase(); // Populate the in-memory database with test data.

        var mockLogger = new Mock<ILogger<CartRepository>>();
        _cartRepository = new CartRepository(_dbContext, mockLogger.Object);
    }

    private void SeedDatabase()
    {
        var user = new User { Id = _seededUserId, Name = "Test User" };
        var product = new Product { Id = _seededProductId, Title = "Test Product", Price = 100 };
        var cart = new Cart { Id = _seededCartId, UserId = _seededUserId, Status = CartStatus.Active };
        var cartItem = new CartItem { CartId = _seededCartId, ProductId = _seededProductId, Quantity = 2, UnitPriceSnapshot = 100 };

        _dbContext.Users.Add(user);
        _dbContext.Products.Add(product);
        _dbContext.Carts.Add(cart);
        _dbContext.CartItems.Add(cartItem);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetCartByIdAsync_ShouldReturnCartWithItems_WhenCartExists()
    {
        // Act
        var result = await _cartRepository.GetCartByIdAsync(_seededCartId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_seededCartId, result.Id);
        Assert.Single(result.CartItems); // Verify that related items are included.
        Assert.Equal(_seededProductId, result.CartItems.First().ProductId);
    }

    [Fact]
    public async Task GetActiveCartByUserIdAsync_ShouldReturnActiveCart_WhenExists()
    {
        // Act
        var result = await _cartRepository.GetActiveCartByUserIdAsync(_seededUserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_seededUserId, result.UserId);
        Assert.Equal(CartStatus.Active, result.Status);
    }

    [Fact]
    public async Task CreateCartAsync_ShouldAddCartToDatabase()
    {
        // Arrange
        var newCart = new Cart { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = CartStatus.Active };

        // Act
        var result = await _cartRepository.CreateCartAsync(newCart);

        // Assert
        Assert.NotNull(result);
        var cartInDb = await _dbContext.Carts.FindAsync(newCart.Id);
        Assert.NotNull(cartInDb);
        Assert.Equal(newCart.Id, cartInDb.Id);
    }

    [Fact]
    public async Task AddOrUpdateCartItemAsync_ShouldAddNewItem_WhenItemDoesNotExist()
    {
        // Arrange
        var newProductId = Guid.NewGuid();
        _dbContext.Products.Add(new Product { Id = newProductId, Title = "New Product", Price = 50 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _cartRepository.AddOrUpdateCartItemAsync(_seededCartId, newProductId, 3, 50, "New Product");

        // Assert
        var cart = await _dbContext.Carts.Include(c => c.CartItems).FirstAsync(c => c.Id == _seededCartId);
        Assert.Equal(2, cart.CartItems.Count); // Should now have 2 items.
        var newItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == newProductId);
        Assert.NotNull(newItem);
        Assert.Equal(3, newItem.Quantity);
    }

    [Fact]
    public async Task AddOrUpdateCartItemAsync_ShouldUpdateQuantity_WhenItemExists()
    {
        // Arrange
        var existingProductId = _seededProductId;
        var newQuantity = 5;

        // Act
        await _cartRepository.AddOrUpdateCartItemAsync(_seededCartId, existingProductId, newQuantity, 100);

        // Assert
        var cartItem = await _dbContext.CartItems.FirstAsync(ci => ci.CartId == _seededCartId && ci.ProductId == existingProductId);
        Assert.Equal(newQuantity, cartItem.Quantity);
    }

    [Fact]
    public async Task DeleteCartAsync_ShouldRemoveCartAndItems()
    {
        // Act
        var result = await _cartRepository.DeleteCartAsync(_seededCartId);

        // Assert
        Assert.True(result);
        var cartInDb = await _dbContext.Carts.FindAsync(_seededCartId);
        var itemsInDb = await _dbContext.CartItems.Where(ci => ci.CartId == _seededCartId).ToListAsync();
        Assert.Null(cartInDb); // Verify cart is deleted.
        Assert.Empty(itemsInDb); // Verify associated items are also deleted.
    }

    [Fact]
    public async Task SoftDeleteCartAsync_ShouldChangeStatusToAbandoned()
    {
        // Act
        var result = await _cartRepository.SoftDeleteCartAsync(_seededCartId);

        // Assert
        Assert.True(result);
        var cartInDb = await _dbContext.Carts.FindAsync(_seededCartId);
        Assert.NotNull(cartInDb);
        Assert.Equal(CartStatus.Abandoned, cartInDb.Status);
    }

    [Fact]
    public async Task UpdateCartTotalsAsync_ShouldUpdateTotalsCorrectly()
    {
        // Arrange
        var newTotal = 500m;
        var newDiscount = 50m;

        // Act
        await _cartRepository.UpdateCartTotalsAsync(_seededCartId, newTotal, newDiscount, 10m, 460m);

        // Assert
        var cartInDb = await _dbContext.Carts.FindAsync(_seededCartId);
        Assert.NotNull(cartInDb);
        Assert.Equal(newTotal, cartInDb.TotalBeforeDiscount);
        Assert.Equal(newDiscount, cartInDb.DiscountAmount);
        Assert.Equal(460m, cartInDb.FinalTotal);
    }

    // This is required by xUnit for proper cleanup of the in-memory database after tests run.
    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}