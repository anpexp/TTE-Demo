using System;
using System.Collections.Generic;

namespace Logica.Models.Carts
{
    /// <summary>
    /// DTO para resumen dashboard de carritos
    /// </summary>
    public class CartsDashboardSummaryDto
    {
        // Estadísticas generales
        public int TotalCarts { get; set; }
        public int ActiveCarts { get; set; }
        public int CheckedOutCarts { get; set; }
        public int AbandonedCarts { get; set; }
        
        // Estadísticas financieras
        public decimal TotalRevenue { get; set; }
        public decimal AverageCartValue { get; set; }
        public decimal TotalPendingValue { get; set; }
        
        // Estadísticas de productos
        public int TotalItemsInCarts { get; set; }
        public int UniqueProductsInCarts { get; set; }
        
        // Estadísticas adicionales para compatibilidad con CartMapper
        public int TotalItems { get; set; }
        public int TotalQuantity { get; set; }
        public int CartsWithCoupons { get; set; }
        public decimal TotalDiscountAmount { get; set; }
        public int RecentCarts { get; set; }
        
        // Top usuarios por número de carritos
        public List<UserCartSummaryDto> TopUsersByCarts { get; set; } = new();
        
        // Top usuarios por valor de carritos
        public List<UserCartSummaryDto> TopUsersByValue { get; set; } = new();
        
        // Productos más populares en carritos
        public List<ProductCartSummaryDto> MostPopularProducts { get; set; } = new();
        
        // Estadísticas por período
        public List<DailyCartStatsDto> DailyStats { get; set; } = new();
        
        // Información de cupones
        public List<CouponUsageDto> CouponUsage { get; set; } = new();
        
        // Timestamp del reporte
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// DTO para resumen de carritos por usuario
    /// </summary>
    public class UserCartSummaryDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        
        // Propiedades originales
        public int TotalCarts { get; set; }
        public int ActiveCarts { get; set; }
        public int CheckedOutCarts { get; set; }
        public decimal TotalValue { get; set; }
        public decimal AverageCartValue { get; set; }
        public DateTime? LastActivity { get; set; }
        
        // Propiedades adicionales para compatibilidad con CartMapper
        public int CartCount { get; set; }
        public decimal TotalSpent { get; set; }
    }

    /// <summary>
    /// DTO para resumen de productos en carritos
    /// </summary>
    public class ProductCartSummaryDto
    {
        public Guid ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public int TimesAddedToCart { get; set; }
        public int TotalQuantityInCarts { get; set; }
        public int UniqueCartsWithProduct { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal TotalValueInCarts { get; set; }
    }

    /// <summary>
    /// DTO para estadísticas diarias de carritos
    /// </summary>
    public class DailyCartStatsDto
    {
        public DateTime Date { get; set; }
        public int CartsCreated { get; set; }
        public int CartsCheckedOut { get; set; }
        public int ItemsAdded { get; set; }
        public decimal RevenueGenerated { get; set; }
        public int UniqueActiveUsers { get; set; }
    }

    /// <summary>
    /// DTO para uso de cupones
    /// </summary>
    public class CouponUsageDto
    {
        public string CouponCode { get; set; } = string.Empty;
        public int TimesUsed { get; set; }
        public decimal TotalDiscountApplied { get; set; }
        public decimal AverageDiscountPerUse { get; set; }
        public DateTime? LastUsed { get; set; }
        public bool IsActive { get; set; }
    }
}