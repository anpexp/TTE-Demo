using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data;
using Data.Entities;
using Logica.Interfaces;
using Logica.Models.Review.Requests;
using Logica.Models.Reviews;
using Logica.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Moq.EntityFrameworkCore;
using Xunit;

namespace TechTrendEmporium.Api.Tests.Services;

public class NoopExecutionStrategy : IExecutionStrategy
{
    public bool RetriesOnFailure => false;
    public void Execute(Action operation) => operation();
    public TResult Execute<TResult>(Func<TResult> operation) => operation();
    public Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default) => operation();
    public Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default) => operation();

    // Implement missing interface member
    public TResult Execute<TState, TResult>(
        TState state,
        Func<DbContext, TState, TResult> operation,
        Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded)
    {
        // No retry logic, just execute the operation
        // DbContext is not used in this test strategy, so pass null
        return operation(null, state);
    }

    public Task<TResult> ExecuteAsync<TState, TResult>(
        TState state,
        Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
        Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
        CancellationToken cancellationToken = default)
    {
        // No retry logic, just execute the operation
        // DbContext is not used in this test strategy, so pass null
        return operation(null, state, cancellationToken);
    }
}

public class ReviewServiceTests
{
    // Mocks for all service dependencies
    private readonly Mock<IReviewRepository> _mockReviewRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IProductRepository> _mockProductRepository;
    private readonly Mock<AppDbContext> _mockDbContext;

    // Mocks for EF Core transaction management
    private readonly Mock<IDbContextTransaction> _mockTransaction;
    private readonly Mock<DatabaseFacade> _mockDatabase;

    // The service instance to be tested
    private readonly ReviewService _reviewService;

    public ReviewServiceTests()
    {
        // Initialize mocks
        _mockReviewRepository = new Mock<IReviewRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockProductRepository = new Mock<IProductRepository>();

        // Setup DbContext and transaction mocks
        _mockTransaction = new Mock<IDbContextTransaction>();
        _mockDatabase = new Mock<DatabaseFacade>(new Mock<DbContext>().Object);
        _mockDatabase.Setup(db => db.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                     .ReturnsAsync(_mockTransaction.Object);
        _mockDatabase.Setup(db => db.CreateExecutionStrategy()).Returns(new NoopExecutionStrategy());

        _mockDbContext = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
        _mockDbContext.Setup(db => db.Database).Returns(_mockDatabase.Object);

        // Initialize the service with mocked dependencies
        _reviewService = new ReviewService(
            _mockReviewRepository.Object,
            _mockUserRepository.Object,
            _mockProductRepository.Object,
            _mockDbContext.Object);
    }

    [Fact]
    public async Task GetByProductAsync_ShouldReturnReviews_WhenReviewsExist()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var reviewList = new List<Review>
        {
            new Review { Id = Guid.NewGuid(), ProductId = productId, Comment = "Great!" },
            new Review { Id = Guid.NewGuid(), ProductId = productId, Comment = "Awesome!" }
        };

        _mockReviewRepository.Setup(repo => repo.GetByProductAsync(productId, default)).ReturnsAsync(reviewList);

        // Act
        var result = await _reviewService.GetByProductAsync(productId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(productId, result.Product_Id);
        Assert.Equal(2, result.Reviews.Count);
    }

    [Fact]
    public async Task AddAsync_ShouldAddReviewAndUpdateProductRating_WhenRequestIsValid()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reviewDto = new ReviewCreateDto { User = "testuser", Rating = 5, Comment = "Excellent!" };

        var productEntity = new Product { Id = productId, RatingAverage = 4.0m, RatingCount = 10 };
        var userEntity = new User { Id = userId, Username = "testuser" };
        var newReview = new Review { Id = Guid.NewGuid(), ProductId = productId, UserId = userId, Rating = reviewDto.Rating };

        // Setup mocks for all validation steps
        _mockProductRepository.Setup(repo => repo.GetByIdAsync(productId)).ReturnsAsync(productEntity);
        _mockUserRepository.Setup(repo => repo.GetByUsernameAsync(reviewDto.User, default)).ReturnsAsync(userEntity);
        _mockDbContext.Setup(db => db.Reviews).ReturnsDbSet(new List<Review>()); // Simulate no existing review
        _mockReviewRepository.Setup(repo => repo.AddAsync(It.IsAny<Review>(), default)).ReturnsAsync(newReview);

        // Act
        var result = await _reviewService.AddAsync(productId, reviewDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newReview.Id, result.Id);

        // Verify product rating was recalculated and updated
        Assert.Equal(11, productEntity.RatingCount);
        Assert.Equal(4.1m, productEntity.RatingAverage); // (4.0 * 10 + 5) / 11 = 4.0909... rounded to 4.1
        _mockProductRepository.Verify(repo => repo.UpdateAsync(productEntity), Times.Once);

        // Verify transaction was committed
        _mockTransaction.Verify(t => t.CommitAsync(default), Times.Once);
        _mockTransaction.Verify(t => t.RollbackAsync(default), Times.Never);
    }

    [Fact]
    public async Task AddAsync_ShouldThrowKeyNotFoundException_WhenProductDoesNotExist()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var reviewDto = new ReviewCreateDto { User = "testuser", Rating = 5, Comment = "Ok" };
        // Simulate product not found
        _mockProductRepository.Setup(repo => repo.GetByIdAsync(productId)).ReturnsAsync((Product)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _reviewService.AddAsync(productId, reviewDto));
        // Verify transaction was rolled back (even though it might not have been fully started, good practice to check)
        _mockTransaction.Verify(t => t.RollbackAsync(default), Times.Once);
    }

    [Fact]
    public async Task AddAsync_ShouldThrowInvalidOperationException_WhenUserHasAlreadyReviewed()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reviewDto = new ReviewCreateDto { User = "testuser", Rating = 5, Comment = "Ok" };

        var productEntity = new Product { Id = productId };
        var userEntity = new User { Id = userId, Username = "testuser" };
        var existingReview = new List<Review> { new Review { ProductId = productId, UserId = userId } };

        _mockProductRepository.Setup(repo => repo.GetByIdAsync(productId)).ReturnsAsync(productEntity);
        _mockUserRepository.Setup(repo => repo.GetByUsernameAsync(reviewDto.User, default)).ReturnsAsync(userEntity);
        // Simulate user has already reviewed this product
        _mockDbContext.Setup(db => db.Reviews).ReturnsDbSet(existingReview);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _reviewService.AddAsync(productId, reviewDto));
        Assert.Equal("User has already reviewed this product.", exception.Message);
        _mockTransaction.Verify(t => t.RollbackAsync(default), Times.Once);
    }
}