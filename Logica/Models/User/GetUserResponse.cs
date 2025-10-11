using Data.Entities.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logica.Models.User
{
    public class GetUserResponse
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public required string Email { get; set; } 


        public required string  Username { get; set; }
        public required Role Role { get; set; }


    }
}
