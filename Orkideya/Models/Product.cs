using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orkideya.Models
{
    public class Product
    {
        public Product()
        {
            ProductVariants = new HashSet<ProductVariant>();
        }

        [Key]
        public int ProductId { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }

        [Display(Name = "Category")]
        public int? CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        public virtual ICollection<ProductVariant> ProductVariants { get; set; }
    }
}