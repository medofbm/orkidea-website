using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models
{
    public class Review
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "اسم العميل")]
        public required string CustomerName { get; set; }

        [Required]
        [Display(Name = "الرسالة")]
        public required string Message { get; set; }
        
        [Display(Name = "التقييم")]
        [Range(1, 5, ErrorMessage = "التقييم يجب أن يكون بين 1 و 5")]
        public int Rating { get; set; } = 5;

        [Display(Name = "إظهار في الموقع؟")]
        public bool IsVisible { get; set; } = true;
    }
}