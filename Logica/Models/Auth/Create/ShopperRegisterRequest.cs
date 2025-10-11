using System.ComponentModel.DataAnnotations;

namespace Logica.Models.Auth.Create
{
    public record ShopperRegisterRequest(
        [Required][EmailAddress] string Email,
        [Required] string Username,
        [Required] string Password
    );
}