using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orkideya.Models
{
    public class ProductVariant
    {
        public int ProductVariantId { get; set; }

        [Required]
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public required virtual Product Product { get; set; }

        [Required]
        [Display(Name = "Size (e.g., 60ml, 120ml)")]
        public required string Size { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
    }
}