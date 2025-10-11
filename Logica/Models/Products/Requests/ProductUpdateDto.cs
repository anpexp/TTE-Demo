using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Logica.Models;

namespace Logica.Models.Products
{
    public class ProductUpdateDto
    {
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string? Title { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal? Price { get; set; }

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters")]
        public string? Category { get; set; }

        [Url(ErrorMessage = "Invalid URL format")]
        [StringLength(500, ErrorMessage = "Image URL cannot exceed 500 characters")]
        public string? ImageUrl { get; set; }

        // ? NUEVOS CAMPOS DE INVENTARIO OPCIONAL
        [Range(0, int.MaxValue, ErrorMessage = "Total inventory must be 0 or greater")]
        public int? InventoryTotal { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Available inventory must be 0 or greater")]
        public int? InventoryAvailable { get; set; }
    }
}