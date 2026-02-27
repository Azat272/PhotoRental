using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoRental.Data;
using PhotoRental.Models;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace PhotoRental.Controllers
{
    public class CatalogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CatalogController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            int? categoryId,
            decimal? minPrice,
            decimal? maxPrice,
            string search,
            string sort = "popular",
            int page = 1)
        {
            // Базовый запрос
            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable == true);

            // Поиск по названию
            if (!string.IsNullOrEmpty(search))
            {
                productsQuery = productsQuery.Where(p =>
                    p.Name.Contains(search) ||
                    p.Brand.Contains(search) ||
                    (p.Description != null && p.Description.Contains(search)));
            }

            // Фильтр по категории
            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId);
            }

            // Фильтр по цене
            if (minPrice.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.PricePerDay >= minPrice);
            }
            if (maxPrice.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.PricePerDay <= maxPrice);
            }

            // Сортировка
            productsQuery = sort switch
            {
                "price_asc" => productsQuery.OrderBy(p => p.PricePerDay),
                "price_desc" => productsQuery.OrderByDescending(p => p.PricePerDay),
                "name_asc" => productsQuery.OrderBy(p => p.Name),
                "name_desc" => productsQuery.OrderByDescending(p => p.Name),
                "newest" => productsQuery.OrderByDescending(p => p.Id),
                _ => productsQuery.OrderByDescending(p => p.Id) // popular по умолчанию
            };

            // Пагинация
            int pageSize = 12;
            int totalProducts = await productsQuery.CountAsync();
            int totalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);

            var products = await productsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Получаем категории для фильтра
            var categories = await _context.Categories.ToListAsync();

            // Получаем популярные товары (например, с наибольшим количеством отзывов)
            var popularProducts = await _context.Products
                .Include(p => p.Reviews)
                .Where(p => p.IsAvailable == true)
                .OrderByDescending(p => p.Reviews.Count)
                .Take(4)
                .ToListAsync();

            // Получаем доступный диапазон цен
            var minAvailablePrice = await _context.Products
                .Where(p => p.IsAvailable == true)
                .MinAsync(p => (decimal?)p.PricePerDay) ?? 0;

            var maxAvailablePrice = await _context.Products
                .Where(p => p.IsAvailable == true)
                .MaxAsync(p => (decimal?)p.PricePerDay) ?? 10000;

            var model = new CatalogViewModel
            {
                Title = search != null ? $"Поиск: {search}" : "Каталог оборудования",
                Description = "Профессиональная фото- и видеотехника для аренды",
                Categories = categories,
                Products = products,
                PopularProducts = popularProducts,
                SelectedCategoryId = categoryId,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                SearchQuery = search,
                SortBy = sort,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalProducts = totalProducts,
                PageSize = pageSize,
                MinAvailablePrice = minAvailablePrice,
                MaxAvailablePrice = maxAvailablePrice
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(p => p.Id == id); // Убрал проверку IsAvailable

            if (product == null)
            {
                return NotFound();
            }

            // Похожие товары (из той же категории)
            var relatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != id && p.IsAvailable == true)
                .Take(4)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;

            return View(product);
        }
    }
}