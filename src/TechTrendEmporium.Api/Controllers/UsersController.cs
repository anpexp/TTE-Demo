using Data.Entities.Enums;
using Logica.Interfaces;
using Logica.Models.Auth;
using Logica.Models.Auth.Create;
using Logica.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TechTrendEmporium.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin")]
    public class UsersController : BaseController
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IUserService userService,
            ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        // Local User Operations
        
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GetUserResponse>>> GetAllUsers()
        {
            try
            {
                var users = await _userService.GetAllUsersAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetUserResponse>> GetUser(Guid id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);

                if (user == null)
                {
                    return NotFound($"User with ID {id} not found");
                }
                Enum.TryParse<Role>(user.Role, true, out var roleEnum);
                var response = new GetUserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    Username = user.Username,
                    Role = roleEnum
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<GetUserResponse>> CreateUser(CreateUserRequest request)
        {
            try
            {
                var (user, error) = await _userService.CreateUserAsync(request);
                if (error != null)
                {
                    return BadRequest(new { message = error });
                }
                Enum.TryParse<Role>(user.Role, true, out var roleEnum);
                var response = new GetUserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    Username = user.Username,
                    Role = roleEnum
                };

                return CreatedAtAction(nameof(GetUser), new { id = user!.Id }, user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpPut("{username}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UpdateUser(string username, [FromBody] UpdateUserRequest request)
        {
            var (user, error) = await _userService.UpdateUserAsync(username, request);
            if (error != null) return NotFound(new { message = error });

            return Ok(user);
        }
        [HttpDelete]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeleteUsers([FromBody] DeleteUsersRequest request)
        {
            var (success, error) = await _userService.DeleteUsersAsync(request);
            if (!success) return BadRequest(new { message = error });

            return NoContent(); // 204 is a succesful answer for a delete.
        }
        // FakeStore Operations

        [HttpGet("fakestore")]
        public async Task<ActionResult<IEnumerable<GetUserResponse>>> GetUsersFromFakeStore()
        {
            try
            {
                var users = await _userService.GetUsersFromFakeStoreAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users from FakeStore");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("fakestore/{id:int}")]
        public async Task<ActionResult<GetUserResponse>> GetUserFromFakeStore(int id)
        {
            try
            {
                var user = await _userService.GetUserFromFakeStoreAsync(id);

                if (user == null)
                {
                    return NotFound($"User with ID {id} not found in FakeStore");
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId} from FakeStore", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // Sync Operations

        [HttpPost("sync-from-fakestore")]
        public async Task<ActionResult<object>> SyncAllUsersFromFakeStore()
        {
            try
            {
                var importedCount = await _userService.SyncAllUsersFromFakeStoreAsync();

                return Ok(new
                {
                    Message = "User synchronization completed successfully",
                    ImportedCount = importedCount,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing users from FakeStore");
                return StatusCode(500, "Error during synchronization");
            }
        }

        [HttpPost("import-from-fakestore/{fakeStoreId:int}")]
        public async Task<ActionResult<GetUserResponse>> ImportUserFromFakeStore(int fakeStoreId)
        {
            try
            {
                var user = await _userService.ImportUserFromFakeStoreAsync(fakeStoreId);

                if (user == null)
                {
                    return NotFound($"User with ID {fakeStoreId} not found in FakeStore");
                }

                return Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing user {UserId} from FakeStore", fakeStoreId);
                return StatusCode(500, "Error during import");
            }
        }

        // Utility methods (similar to ProductsController)

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