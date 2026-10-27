using System.ComponentModel.DataAnnotations;
namespace Orkideya.Models;

public sealed class ProductInput
{
    public int ProductId { get; set; }
    [Required(ErrorMessage = "اسم المنتج مطلوب")]
    [StringLength(150, ErrorMessage = "اسم المنتج طويل جداً")]
    public string Name { get; set; } = "";
    [StringLength(10000, ErrorMessage = "وصف المنتج طويل جداً")]
    public string? Description { get; set; }
    [StringLength(2048)]
    public string? ImageUrl { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "اختر فئة صحيحة")]
    public int? CategoryId { get; set; }
}
