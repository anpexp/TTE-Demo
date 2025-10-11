using Data.Entities;
using Data.Entities.Enums;
using Logica.Models.Auth;
using Logica.Models.Auth.Create;
using Logica.Models.User;

namespace Logica.Interfaces
{
    public interface IUserService
    {
        // Local user operations
        Task<IEnumerable<GetUserResponse>> GetAllUsersAsync(CancellationToken cancellationToken = default);
        Task<UserResponse?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<(UserResponse? User, string? Error)> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
        Task<(UserResponse? User, string? Error)> UpdateUserAsync(string username, UpdateUserRequest request, CancellationToken cancellationToken = default);
        Task<(bool Success, string? Error)> DeleteUsersAsync(DeleteUsersRequest request, CancellationToken cancellationToken = default);

        
        Task<IEnumerable<GetUserResponse>> GetUsersFromFakeStoreAsync();
        Task<GetUserResponse?> GetUserFromFakeStoreAsync(int id);
        Task<int> SyncAllUsersFromFakeStoreAsync();
        Task<GetUserResponse?> ImportUserFromFakeStoreAsync(int fakeStoreId);
    }
}