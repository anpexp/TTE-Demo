using System;
using System.Collections.Generic;

namespace Logica.Models.Carts
{
    /// <summary>
    /// DTO con informaci�n completa del carrito para administradores
    /// </summary>
    public class CartFullDetailsDto
    {
        // Informaci�n b�sica del carrito
        public Guid CartId { get; set; }
        public string CartStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Informaci�n del usuario
        public Guid UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserUsername { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public bool UserIsActive { get; set; }

        // Informaci�n financiera
        public decimal TotalBeforeDiscount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal FinalTotal { get; set; }

        // Informaci�n del cup�n
        public Guid? AppliedCouponId { get; set; }
        public string? CouponCode { get; set; }
        public decimal? CouponDiscountPercentage { get; set; }
        public bool? CouponIsActive { get; set; }
        public DateTime? CouponValidFrom { get; set; }
        public DateTime? CouponValidTo { get; set; }

        // Items del carrito
        public List<CartItemFullDetailsDto> CartItems { get; set; } = new();

        // Estad�sticas
        public int TotalItems { get; set; }
        public int TotalQuantity { get; set; }
    }

    /// <summary>
    /// DTO con informaci�n completa del item del carrito
    /// </summary>
    public class CartItemFullDetailsDto
    {
        // Informaci�n b�sica del item
        public Guid CartItemId { get; set; }
        public Guid CartId { get; set; }
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPriceSnapshot { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Snapshots guardados
        public string? TitleSnapshot { get; set; }
        public string? ImageUrlSnapshot { get; set; }
        public string? CategoryNameSnapshot { get; set; }

        // Informaci�n actual del producto
        public string? CurrentProductTitle { get; set; }
        public decimal? CurrentProductPrice { get; set; }
        public string? CurrentProductDescription { get; set; }
        public string? CurrentProductImageUrl { get; set; }
        public string? CurrentProductCategoryName { get; set; }
        public int? CurrentProductInventoryAvailable { get; set; }
        public decimal? CurrentProductRatingAverage { get; set; }
        public int? CurrentProductRatingCount { get; set; }
        public string? CurrentProductState { get; set; }

        // Informaci�n del creador del producto
        public Guid? ProductCreatedBy { get; set; }
        public string? ProductCreatorUsername { get; set; }
        public DateTime? ProductCreatedAt { get; set; }
        public Guid? ProductApprovedBy { get; set; }
        public string? ProductApproverUsername { get; set; }
        public DateTime? ProductApprovedAt { get; set; }

        // An�lisis de cambios
        public decimal PriceDifference { get; set; }
        public bool HasPriceChanged { get; set; }

        // Informaci�n de inventario adicional
        public int ProductInventoryTotal { get; set; }
        public int ProductInventoryAvailable { get; set; }
        public bool IsOutOfStock { get; set; }
    }
}