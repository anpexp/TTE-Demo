using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logica.Models.Auth;
public record UserResponse(Guid Id, string Name, string Email, string Username, string Role);
