using System.Collections.Generic;

namespace ABCRetailByRH.Models
{
    public class HomeViewModel
    {
        public IEnumerable<Product> FeaturedProducts { get; set; } = new List<Product>();
    }
}
