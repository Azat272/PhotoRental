using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoRental.Data;
using PhotoRental.Models;
using System.Security.Claims;

namespace PhotoRental.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Получение или создание корзины для текущего пользователя
        private async Task<Cart> GetOrCreateCartAsync()
        {
            string? userId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            }

            string sessionId = HttpContext.Session.Id;

            // Ищем корзину по userId или sessionId
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c =>
                    (userId != null && c.UserId.ToString() == userId) ||
                    c.SessionId == sessionId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId != null ? int.Parse(userId) : (int?)null,
                    SessionId = sessionId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }

        // Страница корзины
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cart = await GetOrCreateCartAsync();

            var model = new CartViewModel
            {
                Items = cart.CartItems.Select(ci => new CartItemViewModel
                {
                    CartItemId = ci.Id,
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.Name,
                    ImageUrl = ci.Product.ImageUrl,
                    PricePerDay = ci.Product.PricePerDay,
                    Quantity = ci.Quantity,
                    AddedAt = ci.AddedAt
                }).ToList()
            };

            return View(model);
        }

        // Добавление товара в корзину
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            try
            {
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Товар не найден" });
                }

                var cart = await GetOrCreateCartAsync();

                // Проверяем, есть ли уже такой товар в корзине
                var existingItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);

                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                    existingItem.AddedAt = DateTime.Now;
                }
                else
                {
                    cart.CartItems.Add(new CartItem
                    {
                        ProductId = productId,
                        Quantity = quantity,
                        AddedAt = DateTime.Now
                    });
                }

                cart.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                var cartCount = cart.CartItems.Sum(ci => ci.Quantity);

                return Json(new
                {
                    success = true,
                    message = "Товар добавлен в корзину",
                    cartCount = cartCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Ошибка: " + ex.Message });
            }
        }

        // Обновление количества товара
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            if (quantity < 1)
            {
                return Json(new { success = false, message = "Количество должно быть больше 0" });
            }

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId);

            if (cartItem == null)
            {
                return Json(new { success = false, message = "Товар не найден в корзине" });
            }

            cartItem.Quantity = quantity;
            cartItem.Cart.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            var totalAmount = await _context.CartItems
                .Where(ci => ci.CartId == cartItem.CartId)
                .SumAsync(ci => ci.Quantity * ci.Product.PricePerDay);

            return Json(new
            {
                success = true,
                totalAmount = totalAmount.ToString("C2")
            });
        }

        // Удаление товара из корзины
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId);

            if (cartItem == null)
            {
                return Json(new { success = false, message = "Товар не найден в корзине" });
            }

            var cartId = cartItem.CartId;
            _context.CartItems.Remove(cartItem);

            var cart = await _context.Carts.FindAsync(cartId);
            if (cart != null)
            {
                cart.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            var cartCount = await _context.CartItems
                .Where(ci => ci.CartId == cartId)
                .SumAsync(ci => ci.Quantity);

            return Json(new
            {
                success = true,
                cartCount = cartCount
            });
        }

        // Очистка корзины
        [HttpPost]
        public async Task<IActionResult> ClearCart()
        {
            var cart = await GetOrCreateCartAsync();

            _context.CartItems.RemoveRange(cart.CartItems);
            cart.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // Получение количества товаров в корзине
        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            var cart = await GetOrCreateCartAsync();
            var count = cart.CartItems.Sum(ci => ci.Quantity);
            return Json(new { count = count });
        }

        // Переход к оформлению заказа
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            // Проверяем авторизацию
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = "/Cart/Checkout" });
            }

            // Получаем корзину
            var cart = await GetOrCreateCartAsync();
            if (!cart.CartItems.Any())
            {
                return RedirectToAction("Index");
            }

            // Получаем данные пользователя для предзаполнения формы
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(userId);

            // Создаем модель для оформления заказа
            var model = new CheckoutViewModel
            {
                Items = cart.CartItems.Select(ci => new CartItemViewModel
                {
                    CartItemId = ci.Id,
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.Name,
                    ImageUrl = ci.Product.ImageUrl,
                    PricePerDay = ci.Product.PricePerDay,
                    Quantity = ci.Quantity
                }).ToList(),

                // Предзаполняем данными пользователя
                CustomerName = user?.FullName ?? "",
                CustomerEmail = user?.Email ?? "",
                CustomerPhone = user?.PhoneNumber ?? "",

                // Даты по умолчанию
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(1)
            };

            return View(model);
        }
    }
}