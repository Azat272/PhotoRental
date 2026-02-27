using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoRental.Data;
using PhotoRental.Models;
using System.Security.Claims;

namespace PhotoRental.Controllers
{
    [Authorize]
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Страница создания бронирования (из карточки товара)
        [HttpGet]
        public async Task<IActionResult> Create(int productId, DateTime? startDate, DateTime? endDate)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound();
            }

            var model = new CreateBookingViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductImage = product.ImageUrl,
                PricePerDay = product.PricePerDay,
                Deposit = product.Deposit,
                StartDate = startDate ?? DateTime.Today,
                EndDate = endDate ?? DateTime.Today.AddDays(1)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CheckoutViewModel model)
        {
            if (!User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = "/Cart/Checkout" });
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var cart = await GetUserCartAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["ErrorMessage"] = "Корзина пуста. Добавьте товары перед оформлением заказа.";
                return RedirectToAction("Index", "Cart");
            }

            HydrateCheckoutFromCart(model, cart);

            if (!ModelState.IsValid)
            {
                return View("~/Views/Cart/Checkout.cshtml", model);
            }

            if (model.Days < 1)
            {
                ModelState.AddModelError(string.Empty, "Период аренды указан некорректно.");
                return View("~/Views/Cart/Checkout.cshtml", model);
            }

            try
            {
                List<Booking> createdBookings = new();

                // Проверяем доступность товаров на эти даты
                foreach (var item in model.Items)
                {
                    var isAvailable = await CheckAvailability(item.ProductId, model.StartDate, model.EndDate);
                    if (!isAvailable)
                    {
                        ModelState.AddModelError("", $"Товар {item.ProductName} недоступен на выбранные даты");
                        return View("~/Views/Cart/Checkout.cshtml", model);
                    }
                }

                // Создаем бронирование для каждого товара в корзине
                foreach (var item in model.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                    {
                        continue;
                    }

                    var totalPrice = item.PricePerDay * model.Days * item.Quantity;

                    var booking = new Booking
                    {
                        ProductId = item.ProductId,
                        UserId = userId,
                        StartDate = model.StartDate,
                        EndDate = model.EndDate,
                        TotalPrice = totalPrice,
                        Deposit = product.Deposit,
                        Status = "pending",
                        CreatedAt = DateTime.Now,
                        Notes = $"Имя: {model.CustomerName}\n" +
                               $"Телефон: {model.CustomerPhone}\n" +
                               $"Email: {model.CustomerEmail}\n" +
                               $"Количество: {item.Quantity}\n" +
                               $"Способ получения: {(model.DeliveryType == "delivery" ? "Доставка" : "Самовывоз")}\n" +
                               $"Адрес доставки: {model.Address} {model.Apartment}\n" +
                               $"Комментарий курьеру: {model.CourierComment}\n" +
                               $"Способ оплаты: {(model.PaymentMethod == "card" ? "Картой" : "Наличными")}\n" +
                               $"Комментарий: {model.Comment}"
                    };

                    _context.Bookings.Add(booking);
                    createdBookings.Add(booking);
                }

                // Сохраняем в БД
                await _context.SaveChangesAsync();

                // Очищаем корзину
                if (cart.CartItems.Any())
                {
                    _context.CartItems.RemoveRange(cart.CartItems);
                    await _context.SaveChangesAsync();
                }

                // Получаем первое созданное бронирование для отображения на странице ThankYou
                var firstBooking = createdBookings.FirstOrDefault();

                if (firstBooking != null)
                {
                    // Загружаем продукт для отображения
                    var bookingWithProduct = await _context.Bookings
                        .Include(b => b.Product)
                        .FirstOrDefaultAsync(b => b.Id == firstBooking.Id);

                    if (bookingWithProduct != null)
                    {
                        TempData["SuccessMessage"] = "Спасибо за аренду! Ваш заказ успешно оформлен.";
                        return RedirectToAction(nameof(ThankYou), new { id = bookingWithProduct.Id });
                    }
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Произошла ошибка при оформлении заказа: " + ex.Message);
                return View("~/Views/Cart/Checkout.cshtml", model);
            }
        }


        [HttpGet]
        public async Task<IActionResult> ThankYou(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }
        // Проверка доступности товара
        private async Task<bool> CheckAvailability(int productId, DateTime startDate, DateTime endDate)
        {
            var conflictingBookings = await _context.Bookings
                .Where(b => b.ProductId == productId &&
                       b.Status.ToLower() != "cancelled" &&
                       ((b.StartDate <= startDate && b.EndDate > startDate) ||
                        (b.StartDate < endDate && b.EndDate >= endDate) ||
                        (b.StartDate >= startDate && b.EndDate <= endDate)))
                .AnyAsync();

            return !conflictingBookings;
        }

        private async Task<Cart?> GetUserCartAsync(int userId)
        {
            return await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId);
        }

        private static void HydrateCheckoutFromCart(CheckoutViewModel model, Cart cart)
        {
            model.Items = cart.CartItems
                .Select(ci => new CartItemViewModel
                {
                    CartItemId = ci.Id,
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.Name,
                    ImageUrl = ci.Product.ImageUrl,
                    PricePerDay = ci.Product.PricePerDay,
                    Quantity = ci.Quantity,
                    AddedAt = ci.AddedAt
                })
                .ToList();

            model.Days = Math.Max(1, (model.EndDate - model.StartDate).Days);
            model.DeliveryCost = model.DeliveryType == "delivery" ? 500 : 0;
            model.TotalAmount = model.ItemsTotal * model.Days + model.DeliveryCost;
        }

        // Список бронирований пользователя
        [HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var bookings = await _context.Bookings
                .Include(b => b.Product)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }

        // Детали бронирования
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        // Отмена бронирования
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            if (booking.StartDate <= DateTime.Today)
            {
                TempData["ErrorMessage"] = "Нельзя отменить бронирование, которое уже началось";
                return RedirectToAction("Details", new { id });
            }

            booking.Status = "cancelled";
            booking.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Бронирование отменено";
            return RedirectToAction("MyBookings");
        }
    }
}
