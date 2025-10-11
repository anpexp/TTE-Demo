using System.ComponentModel.DataAnnotations;

namespace Logica.Models.Category.Requests
{
    public class CategoryCreateDto
    {
        [Required]
        public required string Name { get; set; }

        public string Slug { get; set; }
    }
}