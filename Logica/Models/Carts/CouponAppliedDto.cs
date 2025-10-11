namespace Logica.Models.Carts
{
    public class CouponAppliedDto
    {
        public string CouponCode { get; set; } = string.Empty;
        
        public decimal DiscountPercentage { get; set; }
    }
}