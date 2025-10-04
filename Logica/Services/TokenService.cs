﻿using Data.Entities;
using Logica.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Logica.Services
{
    public class TokenService : ITokenService
    {
        private readonly SymmetricSecurityKey _key;

        public TokenService(IConfiguration config)
        {
            // IA debug ayuda Buscar la clave JWT en múltiples ubicaciones para compatibilidad con Azure
            var secretKey = config["Jwt:Key"] 
                         ?? config["Jwt_Key"] 
                         ?? Environment.GetEnvironmentVariable("Jwt_Key")
                         ?? Environment.GetEnvironmentVariable("Jwt__Key")
                         ?? throw new InvalidOperationException("La clave JWT no fue encontrada en ninguna ubicación válida.");
            
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException("La clave JWT está vacía o es nula.");
            }

            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        }

        public string CreateToken(User user)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.NameId, user.Id.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, user.Username),
                new(ClaimTypes.Role, user.Role.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}