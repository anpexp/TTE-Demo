using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logica.Models.Products
{
    public class RatingDto
    {
        public double Rate { get; set; }
        public int Count { get; set; }
    }
}               