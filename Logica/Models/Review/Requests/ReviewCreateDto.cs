using System.ComponentModel.DataAnnotations;

namespace Logica.Models.Review.Requests
{
    public class ReviewCreateDto
    {
        
        [Required, MaxLength(100)]
        public string User { get; set; } = default!;

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }
}
