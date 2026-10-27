using Microsoft.AspNetCore.Mvc;
using Orkideya.Models;
using System.Text.Json;

namespace Orkideya.ViewComponents
{
    public class CartSummaryViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = new List<CartItem>();
            if (!string.IsNullOrEmpty(cartJson))
            {
                try { cart = JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new(); }
                catch (JsonException) { /* An invalid session must not break every page's header. */ }
            }

            return View(cart);
        }
    }
}