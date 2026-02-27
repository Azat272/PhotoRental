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
            Console.WriteLine("=== НАЧАЛО ОБРАБОТКИ ЗАКАЗА ===");
            Console.WriteLine($"StartDate: {model.StartDate}");
            Console.WriteLine($"EndDate: {model.EndDate}");
            Console.WriteLine($"Days: {model.Days}");
            Console.WriteLine($"DeliveryType: {model.DeliveryType}");
            Console.WriteLine($"Address: {model.Address}");
            Console.WriteLine($"PaymentMethod: {model.PaymentMethod}");
            Console.WriteLine($"Items count: {model.Items?.Count ?? 0}");

            // Проверяем, авторизован ли пользователь
            if (!User.Identity.IsAuthenticated)
            {
                Console.WriteLine("Пользователь не авторизован");
                return RedirectToAction("Login", "Account", new { returnUrl = "/Cart/Checkout" });
            }

            // Проверяем валидность модели
            if (!ModelState.IsValid)
            {
                Console.WriteLine("Модель невалидна");
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                foreach (var error in errors)
                {
                    Console.WriteLine($"Ошибка: {error}");
                }
                return View("Checkout", model);
            }

            // Проверяем, есть ли товары
            if (model.Items == null || !model.Items.Any())
            {
                Console.WriteLine("Корзина пуста");
                return RedirectToAction("Index", "Cart");
            }

            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                Console.WriteLine($"UserId: {userId}");

                List<Booking> createdBookings = new List<Booking>();

                // Проверяем доступность товаров на эти даты
                foreach (var item in model.Items)
                {
                    Console.WriteLine($"Проверка товара {item.ProductId} - {item.ProductName}");

                    var isAvailable = await CheckAvailability(item.ProductId, model.StartDate, model.EndDate);
                    if (!isAvailable)
                    {
                        Console.WriteLine($"Товар {item.ProductName} недоступен");
                        ModelState.AddModelError("", $"Товар {item.ProductName} недоступен на выбранные даты");
                        return View("Checkout", model);
                    }
                }

                // Создаем бронирование для каждого товара в корзине
                foreach (var item in model.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                    {
                        Console.WriteLine($"Товар {item.ProductId} не найден");
                        continue;
                    }

                    var totalPrice = item.PricePerDay * model.Days * item.Quantity;
                    Console.WriteLine($"Создание брони: {item.ProductName}, дней: {model.Days}, цена: {totalPrice}");

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
                Console.WriteLine($"Сохранено {createdBookings.Count} бронирований");

                // Очищаем корзину
                var cart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart != null && cart.CartItems != null && cart.CartItems.Any())
                {
                    _context.CartItems.RemoveRange(cart.CartItems);
                    await _context.SaveChangesAsync();
                    Console.WriteLine("Корзина очищена");
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
                        Console.WriteLine($"Редирект на ThankYou с ID: {bookingWithProduct.Id}");
                        TempData["SuccessMessage"] = "Спасибо за аренду! Ваш заказ успешно оформлен.";
                        return View("ThankYou", bookingWithProduct);
                    }
                }

                Console.WriteLine("Редирект на главную (нет бронирований)");
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА: {ex.Message}");
                Console.WriteLine($"СТЕК: {ex.StackTrace}");
                ModelState.AddModelError("", "Произошла ошибка при оформлении заказа: " + ex.Message);
                return View("Checkout", model);
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
                       b.Status != "cancelled" &&
                       ((b.StartDate <= startDate && b.EndDate > startDate) ||
                        (b.StartDate < endDate && b.EndDate >= endDate) ||
                        (b.StartDate >= startDate && b.EndDate <= endDate)))
                .AnyAsync();

            return !conflictingBookings;
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