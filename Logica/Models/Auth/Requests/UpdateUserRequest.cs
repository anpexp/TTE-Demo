using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logica.Models.Auth;

// El username se pasará por la URL, por eso no está aquí.
// La contraseña es opcional, solo se actualiza si se envía un valor.
public record UpdateUserRequest(
    string? Name,
    [EmailAddress] string? Email,
    string? Password,
    string? Role
);