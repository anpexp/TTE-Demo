using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Data.Entities;
using Data.Entities.Enums;
using Logica.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace TechTrendEmporium.Api.Tests.Services;

public class TokenServiceTests
{
    // A valid secret key for testing purposes (must be >= 64 characters for HS512)
    private const string TestSecretKey = "ThisIsAValidAndSufficientlyLongSecretKeyForTestingHmacSha512Algorithm";

    [Fact]
    public void Constructor_ShouldThrowInvalidOperationException_WhenJwtKeyIsMissing()
    {
        // Arrange
        // Create an in-memory configuration without the required Jwt:Key
        var inMemorySettings = new Dictionary<string, string>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act & Assert
        // Verify that creating the service throws an exception because the key is not found.
        var exception = Assert.Throws<InvalidOperationException>(() => new TokenService(configuration));
        Assert.Equal("JWT key was not found in any valid location.", exception.Message);
    }

    [Fact]
    public void CreateToken_ShouldGenerateValidToken_WithCorrectClaims()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string> {
        { "Jwt:Key", TestSecretKey }
    };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var tokenService = new TokenService(configuration);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            Role = Role.Shopper
        };

        // Act
        var tokenString = tokenService.CreateToken(user);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(tokenString));

        var handler = new JwtSecurityTokenHandler();
        var decodedToken = handler.ReadJwtToken(tokenString);

        Assert.Equal(SecurityAlgorithms.HmacSha512, decodedToken.Header.Alg);

        // Find claims safely using LINQ instead of creating a dictionary.
        var roleClaim = decodedToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        var nameIdClaim = decodedToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.NameId);
        var uniqueNameClaim = decodedToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName);
        var jtiClaim = decodedToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);

        // Verify that claims exist and have the correct value.
        Assert.NotNull(roleClaim);
        Assert.Equal(user.Role.ToString(), roleClaim.Value);

        Assert.NotNull(nameIdClaim);
        Assert.Equal(user.Id.ToString(), nameIdClaim.Value);

        Assert.NotNull(uniqueNameClaim);
        Assert.Equal(user.Username, uniqueNameClaim.Value);

        Assert.NotNull(jtiClaim);
        Assert.True(Guid.TryParse(jtiClaim.Value, out _));
    }
}