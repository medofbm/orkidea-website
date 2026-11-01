using ClosedXML.Excel;
using Orkideya.Models;
namespace Orkideya.Services;

public sealed class ExcelExportService
{
    public byte[] ExportOrders(IReadOnlyCollection<Order> orders)
    {
            // Create Excel workbook using ClosedXML
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("الطلبات");
                
                // Set RTL for Arabic
                worksheet.RightToLeft = true;
                
                // Headers
                var headers = new[] {
                    "رقم الطلب", "اسم العميل", "رقم الواتساب", "المدينة", 
                    "العنوان", "تاريخ الطلب", "إجمالي الطلب شامل التوصيل (د.ل)", "أجرة التوصيل (د.ل)",
                    "حالة الطلب (لا تثبت التحصيل)", "طريقة الدفع", "قيمة المنتجات دون التوصيل (د.ل)", "مصدر الطلب"
                };
                
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontSize = 12;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(106, 27, 154); // Purple
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                
                // Data rows
                int row = 2;
                foreach (var order in orders)
                {
                    worksheet.Cell(row, 1).Value = order.OrderId;
                    worksheet.Cell(row, 2).Value = order.CustomerName;
                    worksheet.Cell(row, 3).Value = order.WhatsAppNumber;
                    worksheet.Cell(row, 4).Value = order.City;
                    worksheet.Cell(row, 5).Value = order.ShippingAddress;
                    worksheet.Cell(row, 6).Value = order.OrderDate.ToString("yyyy-MM-dd HH:mm");
                    worksheet.Cell(row, 7).Value = order.TotalAmount;
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "0.00";
                    worksheet.Cell(row, 8).Value = order.ShippingCost;
                    worksheet.Cell(row, 8).Style.NumberFormat.Format = "0.00";
                    worksheet.Cell(row, 9).Value = GetStatusArabic(order.Status);
                    worksheet.Cell(row, 10).Value = order.PaymentMethod ?? "";
                    // Use the saved order amounts, never today's product prices.
                    worksheet.Cell(row, 11).Value = order.TotalAmount - order.ShippingCost;
                    worksheet.Cell(row, 11).Style.NumberFormat.Format = "0.00";
                    
                    worksheet.Cell(row,12).Value = OrderSources.Label(order.Creation?.Source);
                row++;
                }
                
                // Auto-fit columns
                worksheet.Columns().AdjustToContents();
                worksheet.SheetView.FreezeRows(1);
                worksheet.Range(1, 1, row - 1, headers.Length).SetAutoFilter();
                
                // Add borders
                var dataRange = worksheet.Range(1, 1, row - 1, headers.Length);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                
                // Generate file
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    
                    return stream.ToArray();
                }
            }
    }
        /// <summary>
        /// Get Arabic status name
        /// </summary>
        private string GetStatusArabic(string status)
        {
            return status?.ToLower() switch
            {
                "pending" => "جديد",
                "processing" => "قيد المعالجة",
                "shipped" => "تم الشحن",
                "delivered" => "مكتمل",
                "cancelled" => "ملغي",
                _ => status ?? ""
            };
        }
}
