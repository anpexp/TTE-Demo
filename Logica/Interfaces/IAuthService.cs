using Data.Entities;
using Logica.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Logica.Interfaces
{
    public interface IAuthService
    {
        Task<(AuthResponse? Response, string? Error)> RegisterShopperAsync(ShopperRegisterRequest request);
        Task<(AuthResponse? Response, string? Error)> RegisterByAdminAsync(AdminRegisterRequest request);
        Task<(AuthResponse? Response, string? Error)> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent);
        Task<(bool Success, string? Error)> LogoutAsync(ClaimsPrincipal userPrincipal);
        Task<Session?> GetActiveSessionAsync(string jti);
    }
}