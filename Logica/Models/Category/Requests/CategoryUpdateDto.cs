using System;
using System.ComponentModel.DataAnnotations;

namespace Logica.Models.Category.Requests
{
    public class CategoryUpdateDto
    {
        [Required]
        public Guid Id { get; set; }
        
        [Required]
        public required string Name { get; set; }

        public string Slug { get; set; }
    }
}