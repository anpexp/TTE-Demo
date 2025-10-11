using System;
using System.Collections.Generic;

namespace Logica.Models.Carts
{
    public class CartDto
    {
        public Guid Id { get; set; }
        
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Lista de IDs de productos como enteros (para compatibilidad con FakeStore)
        /// </summary>
        public List<int> ShoppingCart { get; set; } = new();
        
        /// <summary>
        /// Items detallados del carrito con información completa
        /// </summary>
        public List<CartItemSimpleDto> Items { get; set; } = new();
        
        public CouponAppliedDto? CouponApplied { get; set; }
        
        public decimal TotalBeforeDiscount { get; set; }
        
        public decimal TotalAfterDiscount { get; set; }
        
        public decimal ShippingCost { get; set; }
        
        public decimal FinalTotal { get; set; }

        /// <summary>
        /// Fechas para auditoría
        /// </summary>
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Estado del carrito
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }
}