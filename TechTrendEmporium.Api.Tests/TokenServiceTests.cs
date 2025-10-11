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
        // Create an in-memory configuration with a valid Jwt:Key
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

        // Decode the token to verify its contents
        var handler = new JwtSecurityTokenHandler();
        var decodedToken = handler.ReadJwtToken(tokenString);

        // Verify the signing algorithm
        Assert.Equal(SecurityAlgorithms.HmacSha512, decodedToken.Header.Alg);

        // Verify the claims embedded in the token
        var claims = decodedToken.Claims.ToDictionary(c => c.Type, c => c.Value);

        Assert.Equal(user.Id.ToString(), claims[JwtRegisteredClaimNames.NameId]);
        Assert.Equal(user.Username, claims[JwtRegisteredClaimNames.UniqueName]);
        Assert.Equal(user.Role.ToString(), claims[ClaimTypes.Role]);
        Assert.True(Guid.TryParse(claims[JwtRegisteredClaimNames.Jti], out _)); // Check that Jti is a valid Guid
    }
}