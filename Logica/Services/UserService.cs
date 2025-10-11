using Data.Entities;
using Data.Entities.Enums;
using External.FakeStore;
using Logica.Interfaces;
using Logica.Mappers;
using Logica.Models.Auth;
using Logica.Models.Auth.Create;
using Logica.Models.User;
using System.Text.Json;

namespace Logica.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IFakeStoreApiService _fakeStoreApiService;
        private readonly IExternalMappingRepository _externalMappingRepository;

        public UserService(
            IUserRepository userRepository,
            IFakeStoreApiService fakeStoreApiService,
            IExternalMappingRepository externalMappingRepository)
        {
            _userRepository = userRepository;
            _fakeStoreApiService = fakeStoreApiService;
            _externalMappingRepository = externalMappingRepository;
        }

        // Local user operations
        public async Task<IEnumerable<GetUserResponse>> GetAllUsersAsync(CancellationToken cancellationToken = default)
        {
            var users = await _userRepository.GetAllAsync(cancellationToken);

            var model = users.Select(u => new GetUserResponse
            {
                Id = u.Id,
                Email = u.Email,
                Username = u.Username,
                Role = u.Role,
            }).ToList();
            return model;
        }

        public async Task<UserResponse?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(id, cancellationToken);
            if (user == null)
            {
                return null;
            }
            // Map to UserResponse
            return new UserResponse(user.Id, user.Name, user.Email, user.Username, user.Role.ToString());
        }

        public async Task<(UserResponse? User, string? Error)> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
        {
            if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken) || await _userRepository.UsernameExistsAsync(request.Username, cancellationToken))
            {
                return (null, "This email or user already exist.");
            }

            if (!Enum.IsDefined(typeof(Role), request.Role))
            {
                return (null, "The specified role is not valid.");
            }

            var user = new User
            {
                Name = request.Name,
                Username = request.Username,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role,
                IsActive = true
            };

            var addedUser = await _userRepository.AddAsync(user, cancellationToken);
            var response = new UserResponse(addedUser.Id, addedUser.Name, addedUser.Email, addedUser.Username, addedUser.Role.ToString());
            return (response, null);
        }

        public async Task<(UserResponse? User, string? Error)> UpdateUserAsync(string username, UpdateUserRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
            if (user == null)
            {
                return (null, "Usuario no encontrado.");
            }

            // Actualizamos solo los campos que vienen en la petición
            if (!string.IsNullOrEmpty(request.Name)) user.Name = request.Name;
            if (!string.IsNullOrEmpty(request.Email)) user.Email = request.Email;
            if (!string.IsNullOrEmpty(request.Password)) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            if (!string.IsNullOrEmpty(request.Role) && Enum.TryParse<Role>(request.Role, true, out var role))
            {
                user.Role = role;
            }

            await _userRepository.UpdateUserAsync(user, cancellationToken);
            var response = new UserResponse(user.Id, user.Name, user.Email, user.Username, user.Role.ToString());
            return (response, null);
        }

        public async Task<(bool Success, string? Error)> DeleteUsersAsync(DeleteUsersRequest request, CancellationToken cancellationToken = default)
        {
            var usersToDelete = await _userRepository.GetUsersByUsernamesAsync(request.Usernames, cancellationToken);
            if (usersToDelete.Count == 0)
            {
                return (false, "Ninguno de los usuarios especificados fue encontrado.");
            }

            await _userRepository.DeleteUsersAsync(usersToDelete, cancellationToken);
            return (true, null);
        }

        // FakeStore user operations
        public async Task<IEnumerable<GetUserResponse>> GetUsersFromFakeStoreAsync()
        {
            var fakeStoreUsers = await _fakeStoreApiService.GetUsersAsync();
            return fakeStoreUsers.Select(FakeStoreUserMapper.ToUserDto);
        }

        public async Task<GetUserResponse?> GetUserFromFakeStoreAsync(int id)
        {
            var fakeStoreUser = await _fakeStoreApiService.GetUserByIdAsync(id);
            return fakeStoreUser != null ? FakeStoreUserMapper.ToUserDto(fakeStoreUser) : null;
        }

       
        public async Task<int> SyncAllUsersFromFakeStoreAsync()
        {
            var fakeStoreUsers = await _fakeStoreApiService.GetUsersAsync();
            int importedCount = 0;

            foreach (var fakeStoreUser in fakeStoreUsers)
            {
                try
                {
                    
                    var existingMapping = await _externalMappingRepository.GetByExternalIdAsync(
                        ExternalSource.FakeStore, 
                        "USER", 
                        fakeStoreUser.Id.ToString());

                    if (existingMapping != null)
                        continue; 

                    // Check if email or username already exists
                    if (await _userRepository.EmailExistsAsync(fakeStoreUser.Email))
                        continue;

                    if (await _userRepository.UsernameExistsAsync(fakeStoreUser.Username))
                        continue;

                    
                    var user = FakeStoreUserMapper.ToUser(fakeStoreUser);
                    var createdUser = await _userRepository.AddAsync(user);

                    // Create the external mapping
                    var mapping = new ExternalMapping
                    {
                        Source = ExternalSource.FakeStore,
                        SourceType = "USER",
                        SourceId = fakeStoreUser.Id.ToString(),
                        InternalId = createdUser.Id,
                        SnapshotJson = JsonSerializer.Serialize(fakeStoreUser),
                        ImportedAt = DateTime.UtcNow
                    };

                    await _externalMappingRepository.CreateAsync(mapping);
                    importedCount++;
                }
                catch (Exception)
                {
                    // Log error and continue with next user
                    continue;
                }
            }

            return importedCount;
        }

        public async Task<GetUserResponse?> ImportUserFromFakeStoreAsync(int fakeStoreId)
        {
            var fakeStoreUser = await _fakeStoreApiService.GetUserByIdAsync(fakeStoreId);
            if (fakeStoreUser == null)
                return null;

        
            var existingMapping = await _externalMappingRepository.GetByExternalIdAsync(
                ExternalSource.FakeStore, 
                "USER", 
                fakeStoreId.ToString());

            if (existingMapping != null)
            {
                var existingUser = await _userRepository.GetByIdAsync(existingMapping.InternalId);
                return existingUser != null ? FakeStoreUserMapper.ToUserDto(existingUser) : null;
            }

            // Check if email or username already exists
            if (await _userRepository.EmailExistsAsync(fakeStoreUser.Email))
                throw new InvalidOperationException($"Email {fakeStoreUser.Email} already exists in the system");

            if (await _userRepository.UsernameExistsAsync(fakeStoreUser.Username))
                throw new InvalidOperationException($"Username {fakeStoreUser.Username} already exists in the system");

           
            var user = FakeStoreUserMapper.ToUser(fakeStoreUser);
            var createdUser = await _userRepository.AddAsync(user);

          
            var mapping = new ExternalMapping
            {
                Source = ExternalSource.FakeStore,
                SourceType = "USER",
                SourceId = fakeStoreId.ToString(),
                InternalId = createdUser.Id,
                SnapshotJson = JsonSerializer.Serialize(fakeStoreUser),
                ImportedAt = DateTime.UtcNow
            };

            await _externalMappingRepository.CreateAsync(mapping);

            return FakeStoreUserMapper.ToUserDto(createdUser);
        }
    }
}