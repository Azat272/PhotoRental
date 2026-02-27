using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoRental.Data;
using PhotoRental.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PhotoRental.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ProfileController(UserManager<ApplicationUser> userManager, ApplicationDbContext context, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _context = context;
            _signInManager = signInManager;

        }

        // 1. ГЛАВНАЯ СТРАНИЦА ПРОФИЛЯ (DASHBOARD)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // Получаем бронирования пользователя
            var bookings = await _context.Bookings
                .Include(b => b.Product)
                .Where(b => b.UserId == user.Id)
                .ToListAsync();

            // Получаем корзину пользователя
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            // Считаем общую сумму потраченную на все завершенные аренды
            var totalSpent = bookings
                .Where(b => b.Status == "Completed")
                .Sum(b => b.TotalPrice);

            var model = new ProfileDashboardViewModel
            {
                User = user,
                ActiveBookingsCount = bookings.Count(b => b.Status == "Active"),
                CompletedBookingsCount = bookings.Count(b => b.Status == "Completed"),
                TotalSpent = totalSpent,
                CartItemsCount = cart?.CartItems?.Sum(ci => ci.Quantity) ?? 0,
                RecentBookings = bookings.OrderByDescending(b => b.CreatedAt).Take(5).ToList()
            };

            return View(model);
        }

        // 2. МОИ БРОНИРОВАНИЯ
        [HttpGet]
        public async Task<IActionResult> Bookings(string type = "active")
        {
            var user = await _userManager.GetUserAsync(User);

            var bookings = await _context.Bookings
                .Include(b => b.Product)
                .Where(b => b.UserId == user.Id)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var activeBookings = bookings.Where(b => b.Status == "Active" || b.Status == "Pending").ToList();
            var historyBookings = bookings.Where(b => b.Status == "Completed" || b.Status == "Cancelled").ToList();

            var model = new BookingsViewModel
            {
                ActiveBookings = activeBookings,
                HistoryBookings = historyBookings,
                CurrentTab = type
            };

            return View(model);
        }

        // 3. ДЕТАЛИ БРОНИРОВАНИЯ
        [HttpGet]
        public async Task<IActionResult> BookingDetails(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == user.Id);

            if (booking == null)
                return NotFound();

            return View(booking);
        }

        // 4. ПРОДЛИТЬ БРОНИРОВАНИЕ
        [HttpPost]
        public async Task<IActionResult> ExtendBooking(int bookingId, DateTime newEndDate)
        {
            var user = await _userManager.GetUserAsync(User);

            var booking = await _context.Bookings
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == user.Id);

            if (booking == null)
                return NotFound();

            if (newEndDate <= booking.EndDate)
            {
                TempData["Error"] = "Новая дата окончания должна быть позже текущей";
                return RedirectToAction("BookingDetails", new { id = bookingId });
            }

            var additionalDays = (newEndDate - booking.EndDate).Days;
            var additionalCost = additionalDays * booking.Product.PricePerDay;

            booking.EndDate = newEndDate;
            booking.TotalPrice += additionalCost;
            booking.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Бронирование успешно продлено";
            return RedirectToAction("BookingDetails", new { id = bookingId });
        }

        // 5. ОТМЕНИТЬ БРОНИРОВАНИЕ
        [HttpPost]
        public async Task<IActionResult> CancelBooking(int bookingId)
        {
            var user = await _userManager.GetUserAsync(User);

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == user.Id);

            if (booking == null)
                return NotFound();

            if (booking.Status != "Active" && booking.Status != "Pending")
            {
                TempData["Error"] = "Это бронирование нельзя отменить";
                return RedirectToAction("BookingDetails", new { id = bookingId });
            }

            booking.Status = "Cancelled";
            booking.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Бронирование отменено";
            return RedirectToAction("Bookings");
        }

        // 6. РЕДАКТИРОВАНИЕ ПРОФИЛЯ (GET)
        
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);

            var model = new EditProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber ?? ""
            };

            return View(model);
        }

        // 7. РЕДАКТИРОВАНИЕ ПРОФИЛЯ (POST)
        [HttpPost]
        public async Task<IActionResult> Edit(EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Профиль успешно обновлен";
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // 8. СМЕНА ПАРОЛЯ (GET)
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // 9. СМЕНА ПАРОЛЯ (POST)
        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Пароль успешно изменен";

                // Опционально: перелогинить пользователя
                await _signInManager.RefreshSignInAsync(user);

                return RedirectToAction("ChangePassword");
            }

            // Добавляем ошибки от Identity в ModelState
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }


        // 10. МОИ ОТЗЫВЫ
        [HttpGet]
        public async Task<IActionResult> Reviews()
        {
            var user = await _userManager.GetUserAsync(User);

            var reviews = await _context.Reviews
                .Include(r => r.Product)
                .Where(r => r.UserId == user.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(reviews);
        }

        // 11. ДОБАВИТЬ/РЕДАКТИРОВАТЬ ОТЗЫВ (GET)
        [HttpGet]
        public async Task<IActionResult> EditReview(int? id, int? productId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (id.HasValue)
            {
                // Редактирование существующего отзыва
                var review = await _context.Reviews
                    .Include(r => r.Product)
                    .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

                if (review == null)
                    return NotFound();

                return View(review);
            }
            else if (productId.HasValue)
            {
                // Новый отзыв для товара
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                    return NotFound();

                // Проверяем, есть ли уже отзыв от этого пользователя на этот товар
                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == user.Id);

                if (existingReview != null)
                    return RedirectToAction("EditReview", new { id = existingReview.Id });

                return View(new Review { ProductId = productId.Value, Product = product });
            }

            return BadRequest();
        }

        // 12. СОХРАНИТЬ ОТЗЫВ (POST)
        [HttpPost]
        public async Task<IActionResult> SaveReview(Review review)
        {
            var user = await _userManager.GetUserAsync(User);

            if (!ModelState.IsValid)
            {
                review.Product = await _context.Products.FindAsync(review.ProductId);
                return View("EditReview", review);
            }

            if (review.Id == 0)
            {
                // Новый отзыв
                review.UserId = user.Id;
                review.CreatedAt = DateTime.Now;
                _context.Reviews.Add(review);
            }
            else
            {
                // Обновление существующего
                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.Id == review.Id && r.UserId == user.Id);

                if (existingReview == null)
                    return NotFound();

                existingReview.Rating = review.Rating;
                existingReview.Comment = review.Comment;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Отзыв сохранен";
            return RedirectToAction("Reviews");
        }

        // 13. УДАЛИТЬ ОТЗЫВ
        [HttpPost]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (review == null)
                return NotFound();

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Отзыв удален";
            return RedirectToAction("Reviews");
        }

        // 14. ПЛАТЕЖНЫЕ ДАННЫЕ
        [HttpGet]
        public async Task<IActionResult> Payments()
        {
            var user = await _userManager.GetUserAsync(User);

            // Здесь будет логика получения платежных данных
            // Пока заглушка
            var model = new PaymentsViewModel
            {
                PaymentMethods = new List<PaymentMethod>(),
                PaymentHistory = new List<Payment>()
            };

            return View(model);
        }

        // 15. ДОБАВИТЬ КАРТУ (GET)
        [HttpGet]
        public IActionResult AddCard()
        {
            return View();
        }

        // 16. ДОБАВИТЬ КАРТУ (POST)
        [HttpPost]
        public async Task<IActionResult> AddCard(AddCardViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Здесь будет логика сохранения карты
            // Пока просто перенаправляем

            TempData["Success"] = "Карта успешно добавлена";
            return RedirectToAction("Payments");
        }

        // 17. УДАЛИТЬ КАРТУ
        [HttpPost]
        public async Task<IActionResult> DeleteCard(int id)
        {
            // Здесь будет логика удаления карты

            TempData["Success"] = "Карта удалена";
            return RedirectToAction("Payments");
        }
    }
}