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

public class ExternalMappingRepositoryTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ExternalMappingRepository _repository;

    public ExternalMappingRepositoryTests()
    {
        // Setup the in-memory database for each test.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        SeedDatabase(); // Populate the in-memory database with test data.

        _repository = new ExternalMappingRepository(_dbContext);
    }

    private void SeedDatabase()
    {
        var mappings = new List<ExternalMapping>
        {
            new ExternalMapping { Id = Guid.NewGuid(), Source = ExternalSource.FakeStore, SourceType = "PRODUCT", SourceId = "1", InternalId = Guid.NewGuid() },
            new ExternalMapping { Id = Guid.NewGuid(), Source = ExternalSource.FakeStore, SourceType = "PRODUCT", SourceId = "2", InternalId = Guid.NewGuid() },
            new ExternalMapping { Id = Guid.NewGuid(), Source = ExternalSource.FakeStore, SourceType = "CATEGORY", SourceId = "cat1", InternalId = Guid.NewGuid() }
        };

        _dbContext.ExternalMappings.AddRange(mappings);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetMappingAsync_ShouldReturnMapping_WhenExists()
    {
        // Arrange
        var sourceId = "1";
        var source = ExternalSource.FakeStore;
        var sourceType = "PRODUCT";

        // Act
        var result = await _repository.GetMappingAsync(sourceId, source, sourceType);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sourceId, result.SourceId);
        Assert.Equal(source, result.Source);
        Assert.Equal(sourceType, result.SourceType);
    }

    [Fact]
    public async Task GetMappingAsync_ShouldReturnNull_WhenDoesNotExist()
    {
        // Act
        var result = await _repository.GetMappingAsync("999", ExternalSource.FakeStore, "PRODUCT");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMappingsBySourceIdsAsync_ShouldReturnMatchingMappings()
    {
        // Arrange
        var sourceIds = new List<string> { "1", "3", "cat1" }; // "3" does not exist
        var source = ExternalSource.FakeStore;
        var sourceType = "PRODUCT";

        // Act
        var result = await _repository.GetMappingsBySourceIdsAsync(sourceIds, source, sourceType);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result); // Should only find one "PRODUCT" with SourceId "1"
        Assert.Equal("1", result.First().SourceId);
    }

    [Fact]
    public async Task GetInternalIdMappingsAsync_ShouldReturnCorrectDictionary()
    {
        // Arrange
        var sourceIds = new List<string> { "1", "2" };
        var source = ExternalSource.FakeStore;
        var sourceType = "PRODUCT";

        // Act
        var result = await _repository.GetInternalIdMappingsAsync(sourceIds, source, sourceType);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, Guid>>(result);
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("1"));
        Assert.True(result.ContainsKey("2"));
    }

    [Fact]
    public async Task CreateAsync_ShouldAddMappingToDatabase()
    {
        // Arrange
        var newMapping = new ExternalMapping
        {
            Id = Guid.NewGuid(),
            Source = ExternalSource.FakeStore,
            SourceType = "USER",
            SourceId = "user-123",
            InternalId = Guid.NewGuid()
        };

        // Act
        var result = await _repository.CreateAsync(newMapping);

        // Assert
        Assert.NotNull(result);
        var mappingInDb = await _dbContext.ExternalMappings.FindAsync(newMapping.Id);
        Assert.NotNull(mappingInDb);
        Assert.Equal("user-123", mappingInDb.SourceId);
    }

    // This is required by xUnit for proper cleanup of the in-memory database after tests run.
    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}