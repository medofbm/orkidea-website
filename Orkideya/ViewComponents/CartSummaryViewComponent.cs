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
            List<CartItem> cart = string.IsNullOrEmpty(cartJson)
                ? new List<CartItem>()
                : JsonSerializer.Deserialize<List<CartItem>>(cartJson);

            return View(cart);
        }
    }
}