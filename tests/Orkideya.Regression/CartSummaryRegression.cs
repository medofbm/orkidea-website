using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Orkideya.Models;
using Orkideya.ViewComponents;
using System.Text;

internal static class CartSummaryRegression
{
    public static void Run(Action<bool, string> check)
    {
        foreach (var (json, expected, name) in new (string?, int, string)[]
        {
            (null, 0, "Missing cart renders empty header"),
            ("null", 0, "Null cart JSON renders empty header"),
            ("{broken", 0, "Malformed cart JSON does not break the page header"),
            ("[]", 0, "Empty cart renders empty header"),
            ("[{\"ProductName\":\"Synthetic product\",\"Quantity\":2,\"Price\":30}]", 1, "Valid cart remains visible in header")
        })
        {
            var session = new MemorySession();
            if (json is not null) session.Set("Cart", Encoding.UTF8.GetBytes(json));
            var component = new CartSummaryViewComponent
            {
                ViewComponentContext = new ViewComponentContext
                {
                    ViewContext = new ViewContext { HttpContext = new DefaultHttpContext { Session = session } }
                }
            };
            var result = (ViewViewComponentResult)component.Invoke();
            check(result.ViewData?.Model is List<CartItem> cart && cart.Count == expected, name);
        }
    }

    private sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();
        public bool IsAvailable => true;
        public string Id => "synthetic-session";
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
