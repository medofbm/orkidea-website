using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models
{
    public class Category
    {
        public int CategoryId { get; set; }

        [Required]
        [Display(Name = "Category Name")]
        public required string Name { get; set; }
    }
}