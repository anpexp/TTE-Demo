using Data;
using Data.Entities;
using External.FakeStore.Models;
using Logica.Interfaces;
using Logica.Mappers;
using Logica.Models.Auth.Create;
using Logica.Models.User;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Back_End_TechTrend_Emporium.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IUserService _userService;

        public TestController(AppDbContext context, IUserService userService)
        {
            _context = context;
            _userService = userService;
        }

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<GetUserResponse>>> GetUsers()
        {   
            var users = await _userService.GetAllUsersAsync();
          

            return Ok(users);
        }

        [HttpPost("users")]
        public async Task<ActionResult<User>> CreateUser(CreateUserRequest request)
        {
            try
            {
                // Usando el servicio con validaciones automáticas
                var (user, error) = await _userService.CreateUserAsync(request);

                if (user == null)
                    return BadRequest(error);

                return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            return await _context.Categories.ToListAsync();
        }

        [HttpGet("products")]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Creator)
                .ToListAsync();
        }

        [HttpGet("fake-user-mapping")]
        public IActionResult TestFakeUserMapping()
        {
            // Sample FakeStore user data (like the one you provided)
            var fakeUser = new FakeStoreUserResponse
            {
                Id = 1,
                Email = "john@gmail.com",
                Username = "johnd",
                Password = "m38rmF$",
                Name = new FakeStoreUserName
                {
                    Firstname = "john",
                    Lastname = "doe"
                },
                Address = new FakeStoreUserAddress
                {
                    Geolocation = new FakeStoreGeolocation
                    {
                        Lat = "-37.3159",
                        Long = "81.1496"
                    },
                    City = "kilcoole",
                    Street = "new road",
                    Number = 7682,
                    Zipcode = "12926-3874"
                },
                Phone = "1-570-236-7033",
                __v = 0
            };

            try
            {
                // Test the mapping
                var mappedUser = FakeStoreUserMapper.ToUser(fakeUser);
                var userDto = FakeStoreUserMapper.ToUserDto(fakeUser);

                return Ok(new
                {
                    Message = "FakeStore user mapping test successful",
                    OriginalFakeStoreUser = fakeUser,
                    MappedUser = new
                    {
                        mappedUser.Id,
                        mappedUser.Email,
                        mappedUser.Username,
                        mappedUser.Role,
                        mappedUser.IsActive,
                        PasswordHashLength = mappedUser.PasswordHash.Length
                    },
                    UserDto = userDto
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = "Error in FakeStore user mapping",
                    Error = ex.Message
                });
            }
        }
    }

  
}