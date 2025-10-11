using System.ComponentModel.DataAnnotations;

namespace Logica.Models.Auth.Login

{
    public record LoginRequest(
        [Required][EmailAddress] string Email,
        [Required] string Password
    );
}