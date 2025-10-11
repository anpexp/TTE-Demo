using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Logica.Interfaces;
using Logica.Models.Review;
using Logica.Models.Products;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TechTrendEmporium.Api.Controllers;
using Xunit;
using Logica.Models.Review.Responses;
using Logica.Models.Review.Requests;

namespace TechTrendEmporium.Api.Tests.Controllers;

public class ReviewsControllerTests
{
    // Mock for the controller's dependency
    private readonly Mock<IReviewService> _mockReviewService;

    // The controller instance to be tested
    private readonly ReviewsController _reviewsController;

    public ReviewsControllerTests()
    {
        // Initialize mock and controller for each test
        _mockReviewService = new Mock<IReviewService>();
        _reviewsController = new ReviewsController(_mockReviewService.Object);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task Get_ShouldReturnOk_WithReviewsResponseDto()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var reviewsResponse = new ReviewsResponseDto
        {
            Product_Id = productId,
            Reviews = new List<ReviewDto>()
        };

        // Setup the service mock to return a valid response.
        _mockReviewService.Setup(service => service.GetByProductAsync(productId, default))
            .ReturnsAsync(reviewsResponse);

        // Act
        var result = await _reviewsController.Get(productId, default);

        // Assert
        // Verify that the result is an OkObjectResult.
        var okResult = Assert.IsType<OkObjectResult>(result);
        // Verify that the value returned is the expected DTO.
        var returnedDto = Assert.IsType<ReviewsResponseDto>(okResult.Value);
        Assert.Equal(productId, returnedDto.Product_Id);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task Post_ShouldReturnCreatedAtAction_WhenReviewIsCreatedSuccessfully()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var createDto = new ReviewCreateDto { User = "testuser", Rating = 5, Comment = "Great product!" };
        var createdDto = new ReviewDto
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            UserId = Guid.NewGuid(), // Use a Guid for UserId
            Username = createDto.User, // Assign the username from createDto.User
            Rating = createDto.Rating,
            Comment = createDto.Comment,
            CreatedAt = DateTime.UtcNow // Provide a value for CreatedAt
        };

        // Setup the service mock to return the created review DTO.
        _mockReviewService.Setup(service => service.AddAsync(productId, createDto, default))
            .ReturnsAsync(createdDto);

        // Act
        var result = await _reviewsController.Post(productId, createDto, default);

        // Assert
        // Verify that the result is a CreatedAtActionResult.
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
        // Verify the action name and the returned value are correct.
        Assert.Equal(nameof(ReviewsController.Get), createdAtActionResult.ActionName);
        Assert.Equal(createdDto, createdAtActionResult.Value);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task Post_ShouldReturnValidationProblem_WhenModelStateIsInvalid()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var createDto = new ReviewCreateDto(); // Invalid DTO

        // Manually add a model state error to simulate an invalid request.
        _reviewsController.ModelState.AddModelError("Comment", "The Comment field is required.");

        // Act
        var result = await _reviewsController.Post(productId, createDto, default);

        // Assert
        // Verify that the result is a BadRequestObjectResult containing validation problems.
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsAssignableFrom<ValidationProblemDetails>(badRequestResult.Value);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task Post_ShouldLetServiceExceptionBubbleUp_WhenServiceThrowsException()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var createDto = new ReviewCreateDto { User = "testuser", Rating = 5, Comment = "Great product!" };

        // Setup the service mock to throw an exception (e.g., product not found).
        _mockReviewService.Setup(service => service.AddAsync(productId, createDto, default))
            .ThrowsAsync(new KeyNotFoundException("Product not found."));

        // Act & Assert
        // Verify that the controller action throws the same exception thrown by the service.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _reviewsController.Post(productId, createDto, default));
    }
}