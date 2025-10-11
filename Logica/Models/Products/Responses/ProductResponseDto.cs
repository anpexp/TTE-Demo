using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logica.Models.Products
{
    public class ProductCreateResponseDto
    {
        public Guid ProductId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}