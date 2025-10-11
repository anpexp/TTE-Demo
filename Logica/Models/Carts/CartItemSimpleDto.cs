using System;

namespace Logica.Models.Carts
{
    public class CartItemSimpleDto
    {
        public Guid ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string? ProductImage { get; set; }
    }
}