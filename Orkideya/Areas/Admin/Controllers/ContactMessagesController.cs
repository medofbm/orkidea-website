using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Orkideya.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    
    public class ContactMessagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContactMessagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/ContactMessages
        public async Task<IActionResult> Index()
        {
            var messages = await _context.ContactMessages
                .OrderByDescending(m => m.ReceivedAt)
                .ToListAsync();
            return View(messages);
        }
        
        // GET: Admin/ContactMessages/GetMessage/5 (AJAX for modal)
        [HttpGet]
        public async Task<IActionResult> GetMessage(int id)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null)
            {
                return Json(new { success = false, message = "الرسالة غير موجودة" });
            }
            
            return Json(new { 
                success = true, 
                data = new {
                    id = message.Id,
                    name = message.Name,
                    email = message.Email,
                    phoneNumber = message.PhoneNumber,
                    message = message.Message,
                    receivedAt = message.ReceivedAt.ToString("dd/MM/yyyy hh:mm tt")
                }
            });
        }

        // GET: Admin/ContactMessages/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contactMessage = await _context.ContactMessages
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contactMessage == null)
            {
                return NotFound();
            }

            return View(contactMessage);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToReviews(int id)
        {
            var contactMessage = await _context.ContactMessages.FindAsync(id);
            if (contactMessage == null)
            {
                return NotFound();
            }

            var review = new Review
            {
                CustomerName = contactMessage.Name,
                Message = contactMessage.Message,
                IsVisible = true // اجعلها ظاهرة تلقائياً
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تمت إضافة الرسالة إلى آراء الزبائن بنجاح!";
            return RedirectToAction(nameof(Index));
        }
        // GET: Admin/ContactMessages/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contactMessage = await _context.ContactMessages
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contactMessage == null)
            {
                return NotFound();
            }

            return View(contactMessage);
        }

        // POST: Admin/ContactMessages/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var contactMessage = await _context.ContactMessages.FindAsync(id);
            if (contactMessage != null)
            {
                _context.ContactMessages.Remove(contactMessage);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ContactMessageExists(int id)
        {
            return _context.ContactMessages.Any(e => e.Id == id);
        }
    }
}
