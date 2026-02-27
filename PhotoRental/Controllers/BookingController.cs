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
        private const decimal DeliveryFee = 500m;
        private readonly ApplicationDbContext _context;

        public BookingController(ApplicationDbContext context)
        {
            _context = context;
        }

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
            if (User.Identity?.IsAuthenticated != true)
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

            PopulateCheckoutModelFromCart(model, cart);

            if (!ModelState.IsValid)
            {
                return View("~/Views/Cart/Checkout.cshtml", model);
            }

            foreach (var item in model.Items)
            {
                var isAvailable = await CheckAvailability(item.ProductId, model.StartDate, model.EndDate);
                if (!isAvailable)
                {
                    ModelState.AddModelError(string.Empty, $"Товар {item.ProductName} недоступен на выбранные даты.");
                    return View("~/Views/Cart/Checkout.cshtml", model);
                }
            }

            var createdBookings = new List<Booking>();

            foreach (var item in model.Items)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                {
                    continue;
                }

                var booking = new Booking
                {
                    ProductId = item.ProductId,
                    UserId = userId,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    TotalPrice = item.PricePerDay * model.Days * item.Quantity,
                    Deposit = product.Deposit,
                    Status = "Pending",
                    CreatedAt = DateTime.Now,
                    Notes = BuildBookingNotes(model, item.Quantity)
                };

                createdBookings.Add(booking);
                _context.Bookings.Add(booking);
            }

            if (!createdBookings.Any())
            {
                ModelState.AddModelError(string.Empty, "Не удалось сформировать заказ: товары не найдены.");
                return View("~/Views/Cart/Checkout.cshtml", model);
            }

            await _context.SaveChangesAsync();

            _context.CartItems.RemoveRange(cart.CartItems);
            cart.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Спасибо! Ваш заказ успешно оформлен.";
            return RedirectToAction(nameof(ThankYou), new { id = createdBookings.First().Id });
        }

        [HttpGet]
        public async Task<IActionResult> ThankYou(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        private async Task<bool> CheckAvailability(int productId, DateTime startDate, DateTime endDate)
        {
            var conflictingBookings = await _context.Bookings
                .Where(b => b.ProductId == productId &&
                            !string.Equals(b.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
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

        private static void PopulateCheckoutModelFromCart(CheckoutViewModel model, Cart cart)
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

            model.Days = Math.Max(1, (model.EndDate.Date - model.StartDate.Date).Days);
            model.DeliveryCost = model.DeliveryType == "delivery" ? DeliveryFee : 0;
            model.TotalAmount = (model.Items.Sum(i => i.PricePerDay * i.Quantity) * model.Days) + model.DeliveryCost;
        }

        private static string BuildBookingNotes(CheckoutViewModel model, int quantity)
        {
            return $"Имя: {model.CustomerName}\n" +
                   $"Телефон: {model.CustomerPhone}\n" +
                   $"Email: {model.CustomerEmail}\n" +
                   $"Количество: {quantity}\n" +
                   $"Способ получения: {(model.DeliveryType == "delivery" ? "Доставка" : "Самовывоз")}\n" +
                   $"Адрес доставки: {model.Address} {model.Apartment}\n" +
                   $"Комментарий курьеру: {model.CourierComment}\n" +
                   $"Способ оплаты: {(model.PaymentMethod == "card" ? "Картой" : "Наличными")}\n" +
                   $"Комментарий: {model.Comment}";
        }

        [HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var bookings = await _context.Bookings
                .Include(b => b.Product)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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

            booking.Status = "Cancelled";
            booking.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Бронирование отменено";
            return RedirectToAction("MyBookings");
        }
    }
}
