using System;
using System.ComponentModel.DataAnnotations;

namespace Logica.Models.Products
{
    public class ProductCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Category is required")]
        [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters")]
        public string Category { get; set; } = string.Empty;

        [Url(ErrorMessage = "Invalid URL format")]
        [StringLength(500, ErrorMessage = "Image URL cannot exceed 500 characters")]
        public string? ImageUrl { get; set; }

        // ? NUEVOS CAMPOS DE INVENTARIO
        [Range(0, int.MaxValue, ErrorMessage = "Total inventory must be 0 or greater")]
        public int InventoryTotal { get; set; } = 0;

        [Range(0, int.MaxValue, ErrorMessage = "Available inventory must be 0 or greater")]
        public int InventoryAvailable { get; set; } = 0;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (InventoryAvailable > InventoryTotal)
            {
                yield return new ValidationResult(
                    "Available inventory cannot be greater than total inventory.",
                    new[] { nameof(InventoryAvailable) });
            }
        }
    }
}