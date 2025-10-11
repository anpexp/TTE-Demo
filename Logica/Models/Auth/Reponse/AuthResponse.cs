using System;

namespace Logica.Models.Auth.Reponse
{
    public record AuthResponse(
        Guid Id,
        string Email,
        string Username,
        string Role,
        string Token
    );
}