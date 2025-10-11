namespace Logica.Models.Category.Responses
{
    public class CategoryCreateResponseDto
    {
        public Guid CategoryId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}