using System.ComponentModel.DataAnnotations;

namespace Logica.Models.Auth.Create
{
    public record AdminRegisterRequest(
        [Required][EmailAddress] string Email,
        [Required] string Username,
        [Required] string Password,
        [Required] string Role
    );
}