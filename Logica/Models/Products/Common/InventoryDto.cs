using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logica.Models.Products
{
    public class InventoryDto
    {
        public int Total { get; set; } = 0;
        public int Available { get; set; } = 0;
    }
}