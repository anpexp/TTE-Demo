using System.ComponentModel.DataAnnotations;

namespace Logica.Models.Category.Requests
{
    public class CategoryDeleteDto
    {
        [Required]
        public Guid Id { get; set; }
    }
}