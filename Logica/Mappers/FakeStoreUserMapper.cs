using Data.Entities;
using Data.Entities.Enums;
using External.FakeStore.Models;
using Logica.Models.User;
using System.Security.Cryptography;
using System.Text;

namespace Logica.Mappers
{
    public static class FakeStoreUserMapper
    {
        public static User ToUser(FakeStoreUserResponse fakeStoreUser)
        {
            if (fakeStoreUser == null)
                throw new ArgumentNullException(nameof(fakeStoreUser));

            return new User
            {
                Id = Guid.NewGuid(),
                Email = fakeStoreUser.Email.ToLower(),
                Username = fakeStoreUser.Username.ToLower(),
                PasswordHash = HashPassword(fakeStoreUser.Password), // Hash the password from FakeStore
                Role = Role.Shopper, // All FakeStore users are shoppers by default
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static GetUserResponse ToUserDto(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            return new GetUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Role = user.Role
            };
        }

        public static GetUserResponse ToUserDto(FakeStoreUserResponse fakeStoreUser)
        {
            if (fakeStoreUser == null)
                throw new ArgumentNullException(nameof(fakeStoreUser));

            return new GetUserResponse
            {
                Id = ConvertIntToGuid(fakeStoreUser.Id),
                Email = fakeStoreUser.Email,
                Username = fakeStoreUser.Username,
                Role = Role.Shopper
            };
        }

        public static FakeStoreUserResponse ToFakeStoreUserResponse(User user, int fakeStoreId)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            return new FakeStoreUserResponse
            {
                Id = fakeStoreId,
                Email = user.Email,
                Username = user.Username,
                Password = "***", // Don't expose real password
                Name = new FakeStoreUserName
                {
                    Firstname = user.Username.Split('.').FirstOrDefault() ?? user.Username,
                    Lastname = user.Username.Split('.').LastOrDefault() ?? "User"
                },
                Address = new FakeStoreUserAddress
                {
                    City = "Unknown",
                    Street = "Unknown",
                    Number = 0,
                    Zipcode = "00000",
                    Geolocation = new FakeStoreGeolocation
                    {
                        Lat = "0",
                        Long = "0"
                    }
                },
                Phone = "Unknown"
            };
        }

        private static string HashPassword(string password)
        {
            // Simple hash for demonstration - in production use proper password hashing
            // like BCrypt, Argon2, or ASP.NET Core Identity
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private static Guid ConvertIntToGuid(int id)
        {
            var bytes = new byte[16];
            var idBytes = BitConverter.GetBytes(id);
            Array.Copy(idBytes, 0, bytes, 0, 4);
            return new Guid(bytes);
        }

        private static int ConvertGuidToInt(Guid guid)
        {
            var bytes = guid.ToByteArray();
            return BitConverter.ToInt32(bytes, 0);
        }
    }
}