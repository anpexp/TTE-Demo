using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logica.Models.Category.Responses
{
    public class CategoryDto
    {
        public Guid Id { get; set; }
        
        public required string Name { get; set; }
        
        public required string Slug { get; set; }
        
        public string State { get; set; } = string.Empty;
        
        public Guid CreatedBy { get; set; }
        
        public Guid? ApprovedBy { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime? UpdatedAt { get; set; }
        
        public DateTime? ApprovedAt { get; set; }
        
        public string CreatorName { get; set; } = string.Empty;
        
        public string? ApproverName { get; set; }
        
        public int ProductCount { get; set; }
    }
}