using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoRental.Data;
using PhotoRental.Models;
using System.Linq;
using System.Threading.Tasks;

namespace PhotoRental.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Получаем категории из БД
            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            // Получаем популярные товары из БД (последние 8 доступных товаров)
            var featuredProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable == true)
                .OrderByDescending(p => p.Id)
                .Take(8)
                .ToListAsync();

            var model = new HomeViewModel
            {
                Title = "Добро пожаловать на аренду фото- и видеотехники",
                Description = "Более 500 единиц техники: камеры, объективы, свет, стабилизация и звук.",
                Categories = categories,
                FeaturedProducts = featuredProducts
            };

            return View(model);
        }

        public IActionResult RedirectToLogin()
        {
            return RedirectToAction("Login", "Account");
        }
    }
}