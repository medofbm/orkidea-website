using ClosedXML.Excel;
using Orkideya.Models;

namespace Orkideya.Services
{
    public class ExcelExportService
    {
        private readonly string _filePath;

        public ExcelExportService(IWebHostEnvironment env)
        {
            // سنقوم بحفظ الملف داخل مجلد wwwroot ليسهل الوصول إليه
            string wwwRootPath = env.WebRootPath;
            _filePath = Path.Combine(wwwRootPath, "orders.xlsx");
        }

        public void AddOrderToSheet(Order order, List<CartItem> cartItems)
        {
            var workbook = File.Exists(_filePath) ? new XLWorkbook(_filePath) : new XLWorkbook();
            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == "Orders");

            if (worksheet == null)
            {
                worksheet = workbook.Worksheets.Add("Orders");
                // إنشاء رأس الجدول عند إنشاء الملف لأول مرة
                worksheet.Cell("A1").Value = "رقم الطلب";
                worksheet.Cell("B1").Value = "اسم العميل";
                worksheet.Cell("C1").Value = "رقم الواتساب";
                worksheet.Cell("D1").Value = "المدينة";
                worksheet.Cell("E1").Value = "العنوان";
                worksheet.Cell("F1").Value = "تاريخ الطلب";
                worksheet.Cell("G1").Value = "المنتج";
                worksheet.Cell("H1").Value = "الكمية";
                worksheet.Cell("I1").Value = "السعر الإجمالي";
            }

            int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

            foreach (var item in cartItems)
            {
                int newRow = lastRow + 1;
                worksheet.Cell(newRow, 1).Value = order.OrderId;
                worksheet.Cell(newRow, 2).Value = order.CustomerName;
                worksheet.Cell(newRow, 3).Value = order.WhatsAppNumber;
                worksheet.Cell(newRow, 4).Value = order.City;
                worksheet.Cell(newRow, 5).Value = order.ShippingAddress;
                worksheet.Cell(newRow, 6).Value = order.OrderDate.ToString("yyyy-MM-dd HH:mm");
                worksheet.Cell(newRow, 7).Value = item.ProductName;
                worksheet.Cell(newRow, 8).Value = item.Quantity;
                worksheet.Cell(newRow, 9).Value = order.TotalAmount;

                lastRow++;
            }

            workbook.SaveAs(_filePath);
        }
    }
}