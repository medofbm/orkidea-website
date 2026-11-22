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

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>"site" (left on the website) or "facebook" (imported from the Facebook page).</summary>
        [MaxLength(16)]
        public string Source { get; set; } = "site";

        /// <summary>The customer agreed to show the first word of their name next to the review.</summary>
        public bool AllowName { get; set; }

        /// <summary>The order this review belongs to (set for reviews left from the receipt page).</summary>
        public int? OrderId { get; set; }
    }
}