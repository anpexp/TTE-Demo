using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logica.Models.Products
{
    public class ProductDto
    {
        public Guid Id { get; set; } // Cambiar de int a Guid
        
        public string Title { get; set; } = string.Empty;
        
        public decimal Price { get; set; }
        
        public string Description { get; set; } = string.Empty;
        
        public string Category { get; set; } = string.Empty;
        
        public string Image { get; set; } = string.Empty;
        
        public RatingDto? Rating { get; set; }

        // Inventory properties
        public int InventoryTotal { get; set; } = 0;
        public int InventoryAvailable { get; set; } = 0;

        // Calculated inventory properties
        public bool IsLowStock => InventoryAvailable <= 5;
        public bool IsOutOfStock => InventoryAvailable <= 0;
        public bool IsInStock => InventoryAvailable > 0;
    }
}