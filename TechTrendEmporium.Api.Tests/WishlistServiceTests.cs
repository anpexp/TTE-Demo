// Add necessary using statements for your test project
using Xunit;
using Moq;
using Logica.Services;
using Logica.Interfaces;
using System.Security.Claims;
using Data.Entities;
using Data.Entities.Enums;
using Logica.Models.Wishlist;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;

// You might need to adjust the namespace to match your test project's structure
namespace TechTrendEmporium.Api.Tests.Services
{
    public class WishlistServiceTests
    {
        private readonly Mock<IWishlistRepository> _wishlistRepositoryMock;
        private readonly Mock<IProductRepository> _productRepositoryMock;
        private readonly WishlistService _wishlistService;
        private readonly Guid _testUserId = Guid.NewGuid();
        private readonly Guid _testProductId = Guid.NewGuid();

        public WishlistServiceTests()
        {
            _wishlistRepositoryMock = new Mock<IWishlistRepository>();
            _productRepositoryMock = new Mock<IProductRepository>();
            _wishlistService = new WishlistService(_wishlistRepositoryMock.Object, _productRepositoryMock.Object);
        }

        private ClaimsPrincipal CreateAuthenticatedUser(Guid userId)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.NameId, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        #region GetWishlistAsync Tests

        [Fact]
        public async Task GetWishlistAsync_ShouldReturnUnauthenticatedError_WhenUserIsNotAuthenticated()
        {
            // Arrange
            var unauthenticatedUser = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var (response, error) = await _wishlistService.GetWishlistAsync(unauthenticatedUser);

            // Assert
            Assert.Null(response);
            Assert.Equal("Usuario no autenticado.", error);
        }

        [Fact]
        public async Task GetWishlistAsync_ShouldReturnExistingWishlist_WhenWishlistExists()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            // AJUSTE: Se utiliza WishlistItems en lugar de Items
            var wishlist = new Wishlist { Id = Guid.NewGuid(), UserId = _testUserId, WishlistItems = new List<WishlistItem>() };
            _wishlistRepositoryMock.Setup(repo => repo.GetByUserIdAsync(_testUserId)).ReturnsAsync(wishlist);

            // Act
            var (response, error) = await _wishlistService.GetWishlistAsync(user);

            // Assert
            Assert.NotNull(response);
            Assert.Null(error);
            // AJUSTE: Se comprueba UserId en el DTO en lugar de Id
            Assert.Equal(wishlist.UserId, response.UserId);
        }

        [Fact]
        public async Task GetWishlistAsync_ShouldCreateAndReturnNewWishlist_WhenWishlistDoesNotExist()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            // AJUSTE: Se utiliza WishlistItems en lugar de Items
            var newWishlist = new Wishlist { Id = Guid.NewGuid(), UserId = _testUserId, WishlistItems = new List<WishlistItem>() };

            _wishlistRepositoryMock.Setup(repo => repo.GetByUserIdAsync(_testUserId)).ReturnsAsync((Wishlist)null);
            _wishlistRepositoryMock.Setup(repo => repo.CreateAsync(It.IsAny<Wishlist>())).ReturnsAsync(newWishlist);

            // Act
            var (response, error) = await _wishlistService.GetWishlistAsync(user);

            // Assert
            Assert.NotNull(response);
            Assert.Null(error);
            // AJUSTE: Se comprueba UserId en el DTO en lugar de Id
            Assert.Equal(newWishlist.UserId, response.UserId);
            _wishlistRepositoryMock.Verify(repo => repo.CreateAsync(It.Is<Wishlist>(w => w.UserId == _testUserId)), Times.Once);
        }

        [Fact]
        public async Task GetWishlistAsync_ShouldReturnError_WhenRepositoryThrowsException()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            var exceptionMessage = "Database error";
            _wishlistRepositoryMock.Setup(repo => repo.GetByUserIdAsync(_testUserId)).ThrowsAsync(new Exception(exceptionMessage));

            // Act
            var (response, error) = await _wishlistService.GetWishlistAsync(user);

            // Assert
            Assert.Null(response);
            Assert.Contains(exceptionMessage, error);
        }

        #endregion

        #region AddProductToWishlistAsync Tests

        [Fact]
        public async Task AddProductToWishlistAsync_ShouldReturnUnauthenticatedError_WhenUserIsNotAuthenticated()
        {
            // Arrange
            var unauthenticatedUser = new ClaimsPrincipal(new ClaimsIdentity());
            var request = new WishlistAddProductDto { ProductId = _testProductId };

            // Act
            var (response, error) = await _wishlistService.AddProductToWishlistAsync(unauthenticatedUser, request);

            // Assert
            Assert.False(response.Success);
            Assert.Equal("Usuario no autenticado.", response.Message);
            Assert.Null(error);
        }

        [Fact]
        public async Task AddProductToWishlistAsync_ShouldReturnProductNotFoundError_WhenProductDoesNotExist()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            var request = new WishlistAddProductDto { ProductId = _testProductId };
            _productRepositoryMock.Setup(repo => repo.GetByIdAsync(_testProductId)).ReturnsAsync((Product)null);

            // Act
            var (response, error) = await _wishlistService.AddProductToWishlistAsync(user, request);

            // Assert
            Assert.False(response.Success);
            Assert.Equal("El producto no existe.", response.Message);
        }

        [Fact]
        public async Task AddProductToWishlistAsync_ShouldReturnProductNotAvailableError_WhenProductIsNotApproved()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            var request = new WishlistAddProductDto { ProductId = _testProductId };
            var product = new Product { Id = _testProductId, State = ApprovalState.PendingApproval };
            _productRepositoryMock.Setup(repo => repo.GetByIdAsync(_testProductId)).ReturnsAsync(product);

            // Act
            var (response, error) = await _wishlistService.AddProductToWishlistAsync(user, request);

            // Assert
            Assert.False(response.Success);
            Assert.Equal("El producto no está disponible.", response.Message);
        }

        [Fact]
        public async Task AddProductToWishlistAsync_ShouldReturnAlreadyExistsError_WhenProductIsInWishlist()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            var request = new WishlistAddProductDto { ProductId = _testProductId };
            var product = new Product { Id = _testProductId, State = ApprovalState.Approved };

            _productRepositoryMock.Setup(repo => repo.GetByIdAsync(_testProductId)).ReturnsAsync(product);
            _wishlistRepositoryMock.Setup(repo => repo.ProductExistsInWishlistAsync(_testUserId, _testProductId)).ReturnsAsync(true);

            // Act
            var (response, error) = await _wishlistService.AddProductToWishlistAsync(user, request);

            // Assert
            Assert.False(response.Success);
            Assert.Equal("El producto ya está en tu wishlist.", response.Message);
        }

        [Fact]
        public async Task AddProductToWishlistAsync_ShouldAddProductSuccessfully_WhenWishlistExists()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            var request = new WishlistAddProductDto { ProductId = _testProductId };
            var product = new Product { Id = _testProductId, State = ApprovalState.Approved };
            // AJUSTE: Se utiliza WishlistItems en lugar de Items
            var wishlist = new Wishlist { Id = Guid.NewGuid(), UserId = _testUserId, WishlistItems = new List<WishlistItem>() };

            _productRepositoryMock.Setup(repo => repo.GetByIdAsync(_testProductId)).ReturnsAsync(product);
            _wishlistRepositoryMock.Setup(repo => repo.ProductExistsInWishlistAsync(_testUserId, _testProductId)).ReturnsAsync(false);
            _wishlistRepositoryMock.Setup(repo => repo.AddItemAsync(It.IsAny<WishlistItem>()))
                .ReturnsAsync((WishlistItem item) => item);

            _wishlistRepositoryMock.SetupSequence(repo => repo.GetByUserIdAsync(_testUserId))
               .ReturnsAsync(wishlist)
               .ReturnsAsync(wishlist);

            // Act
            var (response, error) = await _wishlistService.AddProductToWishlistAsync(user, request);

            // Assert
            Assert.True(response.Success);
            Assert.Equal("Producto agregado a tu wishlist exitosamente.", response.Message);
            _wishlistRepositoryMock.Verify(repo => repo.AddItemAsync(It.Is<WishlistItem>(item => item.ProductId == _testProductId && item.WishlistId == wishlist.Id)), Times.Once);
        }

        #endregion

        #region RemoveProductFromWishlistAsync Tests

        [Fact]
        public async Task RemoveProductFromWishlistAsync_ShouldReturnUnauthenticatedError_WhenUserIsNotAuthenticated()
        {
            // Arrange
            var unauthenticatedUser = new ClaimsPrincipal(new ClaimsIdentity());
            var request = new WishlistRemoveProductDto { ProductId = _testProductId };

            // Act
            var (response, error) = await _wishlistService.RemoveProductFromWishlistAsync(unauthenticatedUser, request);

            // Assert
            Assert.False(response.Success);
            Assert.Equal("Usuario no autenticado.", response.Message);
        }

        [Fact]
        public async Task RemoveProductFromWishlistAsync_ShouldReturnNoWishlistError_WhenWishlistDoesNotExist()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            var request = new WishlistRemoveProductDto { ProductId = _testProductId };
            _wishlistRepositoryMock.Setup(repo => repo.GetByUserIdAsync(_testUserId)).ReturnsAsync((Wishlist)null);

            // Act
            var (response, error) = await _wishlistService.RemoveProductFromWishlistAsync(user, request);

            // Assert
            Assert.False(response.Success);
            Assert.Equal("No tienes una wishlist.", response.Message);
        }

        [Fact]
        public async Task RemoveProductFromWishlistAsync_ShouldReturnProductNotFoundError_WhenItemNotInWishlist()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            var request = new WishlistRemoveProductDto { ProductId = _testProductId };
            var wishlist = new Wishlist { Id = Guid.NewGuid(), UserId = _testUserId };

            _wishlistRepositoryMock.Setup(repo => repo.GetByUserIdAsync(_testUserId)).ReturnsAsync(wishlist);
            _wishlistRepositoryMock.Setup(repo => repo.GetWishlistItemAsync(wishlist.Id, _testProductId)).ReturnsAsync((WishlistItem)null);

            // Act
            var (response, error) = await _wishlistService.RemoveProductFromWishlistAsync(user, request);

            // Assert
            Assert.False(response.Success);
            Assert.Equal("El producto no está en tu wishlist.", response.Message);
        }

        [Fact]
        public async Task RemoveProductFromWishlistAsync_ShouldRemoveProductSuccessfully()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            var request = new WishlistRemoveProductDto { ProductId = _testProductId };
            var wishlist = new Wishlist { Id = Guid.NewGuid(), UserId = _testUserId };
            var wishlistItem = new WishlistItem { ProductId = _testProductId, WishlistId = wishlist.Id };

            _wishlistRepositoryMock.SetupSequence(repo => repo.GetByUserIdAsync(_testUserId))
                .ReturnsAsync(wishlist)
                .ReturnsAsync(wishlist);

            _wishlistRepositoryMock.Setup(repo => repo.GetWishlistItemAsync(wishlist.Id, _testProductId)).ReturnsAsync(wishlistItem);
            _wishlistRepositoryMock.Setup(repo => repo.RemoveItemAsync(wishlistItem)).Returns(Task.CompletedTask);

            // Act
            var (response, error) = await _wishlistService.RemoveProductFromWishlistAsync(user, request);

            // Assert
            Assert.True(response.Success);
            Assert.Equal("Producto removido de tu wishlist exitosamente.", response.Message);
            _wishlistRepositoryMock.Verify(repo => repo.RemoveItemAsync(wishlistItem), Times.Once);
        }

        #endregion

        #region ProductExistsInWishlistAsync Tests

        [Fact]
        public async Task ProductExistsInWishlistAsync_ShouldReturnUnauthenticatedError_WhenUserIsNotAuthenticated()
        {
            // Arrange
            var unauthenticatedUser = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var (exists, error) = await _wishlistService.ProductExistsInWishlistAsync(unauthenticatedUser, _testProductId);

            // Assert
            Assert.False(exists);
            Assert.Equal("Usuario no autenticado.", error);
        }

        [Fact]
        public async Task ProductExistsInWishlistAsync_ShouldReturnTrue_WhenProductExists()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            _wishlistRepositoryMock.Setup(repo => repo.ProductExistsInWishlistAsync(_testUserId, _testProductId)).ReturnsAsync(true);

            // Act
            var (exists, error) = await _wishlistService.ProductExistsInWishlistAsync(user, _testProductId);

            // Assert
            Assert.True(exists);
            Assert.Null(error);
        }

        [Fact]
        public async Task ProductExistsInWishlistAsync_ShouldReturnFalse_WhenProductDoesNotExist()
        {
            // Arrange
            var user = CreateAuthenticatedUser(_testUserId);
            _wishlistRepositoryMock.Setup(repo => repo.ProductExistsInWishlistAsync(_testUserId, _testProductId)).ReturnsAsync(false);

            // Act
            var (exists, error) = await _wishlistService.ProductExistsInWishlistAsync(user, _testProductId);

            // Assert
            Assert.False(exists);
            Assert.Null(error);
        }

        #endregion
    }
}