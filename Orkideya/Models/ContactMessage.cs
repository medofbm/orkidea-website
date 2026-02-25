using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models
{
    public class ContactMessage
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "الاسم")]
        public required string Name { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "البريد الإلكتروني")]
        public required string Email { get; set; }

        [Required]
        [Display(Name = "الرسالة")]
        public required string Message { get; set; }
        [Required]
        [Display(Name = "رقم الهاتف")]
        public required string PhoneNumber { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.Now;
    }
}