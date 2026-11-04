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

        public string? GalleryJson { get; set; }

        [NotMapped]
        public IReadOnlyList<string> GalleryImages => GalleryJson is null
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<string[]>(GalleryJson) ?? [];

        [Display(Name = "Category")]
        public int? CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        public virtual ICollection<ProductVariant> ProductVariants { get; set; }
    }
}