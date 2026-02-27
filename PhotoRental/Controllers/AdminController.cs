using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoRental.Data;
using PhotoRental.Models;
using PhotoRental.Models.Admin;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PhotoRental.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // ========== ГЛАВНАЯ АДМИНКИ ==========
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalCategories = await _context.Categories.CountAsync();
            ViewBag.TotalBookings = await _context.Bookings.CountAsync();
            ViewBag.TotalUsers = await _context.Users.CountAsync();

            ViewBag.RecentProducts = await _context.Products
                .Include(p => p.Category)
                .OrderByDescending(p => p.Id)
                .Take(5)
                .ToListAsync();

            ViewBag.RecentBookings = await _context.Bookings
                .Include(b => b.Product)
                .Include(b => b.User)
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View();
        }

        // ========== УПРАВЛЕНИЕ ТОВАРАМИ ==========

        // Список товаров
        [HttpGet]
        public async Task<IActionResult> Products(string search, int? categoryId, int page = 1, int? highlightId = null)
        {
            int pageSize = 10;

            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            // Поиск
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.Name.Contains(search) ||
                    p.Brand.Contains(search) ||
                    (p.Model != null && p.Model.Contains(search)));
            }

            // Фильтр по категории
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId);
            }

            // Пагинация
            int totalProducts = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);

            var products = await query
                .OrderByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var categories = await _context.Categories.ToListAsync();

            var model = new ProductListViewModel
            {
                Products = products,
                TotalProducts = totalProducts,
                CurrentPage = page,
                TotalPages = totalPages,
                SearchQuery = search,
                CategoryId = categoryId,
                Categories = categories,
                HighlightId = highlightId
            };

            return View(model);
        }

        // Создание товара (GET)
        [HttpGet]
        public async Task<IActionResult> CreateProduct()
        {
            var categories = await _context.Categories.ToListAsync();

            var model = new ProductViewModel
            {
                Categories = categories,
                IsAvailable = true
            };

            return View(model);
        }

        // Создание товара (POST) - ИСПРАВЛЕННЫЙ
        // Создание товара (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct([Bind(Prefix = "")] ProductViewModel model)
        {
            // Определяем статус по наличию checkbox в запросе
            model.IsAvailable = Request.Form.ContainsKey("IsAvailable");

            if (ModelState.IsValid)
            {
                try
                {
                    string imageUrl = "";
                    List<string> additionalImages = new List<string>();

                    // Загружаем главное изображение
                    if (model.ImageFile != null && model.ImageFile.Length > 0)
                    {
                        imageUrl = await UploadImage(model.ImageFile);
                    }
                    else if (!string.IsNullOrEmpty(model.ImageUrl))
                    {
                        imageUrl = model.ImageUrl;
                    }

                    // Загружаем дополнительные изображения
                    if (model.AdditionalImageFiles != null && model.AdditionalImageFiles.Any())
                    {
                        foreach (var file in model.AdditionalImageFiles)
                        {
                            if (file != null && file.Length > 0)
                            {
                                var fileUrl = await UploadImage(file);
                                additionalImages.Add(fileUrl);
                            }
                        }
                    }

                    var product = new Product
                    {
                        Name = model.Name,
                        Brand = model.Brand,
                        Model = model.Model,
                        Description = model.Description,
                        PricePerDay = model.PricePerDay,
                        PricePerWeek = model.PricePerWeek,
                        Deposit = model.Deposit,
                        CategoryId = model.CategoryId,
                        ImageUrl = imageUrl,
                        AdditionalImages = additionalImages.Any() ? JsonSerializer.Serialize(additionalImages) : null,
                        IsAvailable = model.IsAvailable
                    };

                    _context.Products.Add(product);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Товар успешно создан!";
                    return RedirectToAction("Products", new { highlightId = product.Id });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Ошибка при сохранении: " + ex.Message);
                }
            }

            model.Categories = await _context.Categories.ToListAsync();
            return View(model);
        }


        // Редактирование товара (GET)
        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var categories = await _context.Categories.ToListAsync();

            var model = new ProductViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Brand = product.Brand,
                Model = product.Model,
                Description = product.Description,
                PricePerDay = product.PricePerDay,
                PricePerWeek = product.PricePerWeek,
                Deposit = product.Deposit,
                CategoryId = product.CategoryId,
                ImageUrl = product.ImageUrl,
                IsAvailable = product.IsAvailable ?? true,
                Categories = categories
            };

            // Загружаем существующие дополнительные изображения
            if (!string.IsNullOrEmpty(product.AdditionalImages))
            {
                try
                {
                    model.ExistingAdditionalImages = JsonSerializer.Deserialize<List<string>>(product.AdditionalImages) ?? new List<string>();
                }
                catch
                {
                    model.ExistingAdditionalImages = new List<string>();
                }
            }

            return View(model);
        }

        // Редактирование товара (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(ProductViewModel model)
        {
            if (ModelState.IsValid)
            {
                var product = await _context.Products.FindAsync(model.Id);
                if (product == null)
                {
                    return NotFound();
                }

                string imageUrl = product.ImageUrl ?? "";
                List<string> additionalImages = new List<string>();

                // Загружаем существующие дополнительные изображения из БД
                if (!string.IsNullOrEmpty(product.AdditionalImages))
                {
                    try
                    {
                        additionalImages = JsonSerializer.Deserialize<List<string>>(product.AdditionalImages) ?? new List<string>();
                    }
                    catch
                    {
                        additionalImages = new List<string>();
                    }
                }

                // Обработка удаления главного изображения
                if (Request.Form.ContainsKey("DeleteMainImage") && Request.Form["DeleteMainImage"] == "true")
                {
                    if (!string.IsNullOrEmpty(product.ImageUrl) && product.ImageUrl.Contains("/uploads/"))
                    {
                        DeleteImage(product.ImageUrl);
                    }
                    imageUrl = "";
                }

                // Обработка удаления дополнительных изображений
                if (Request.Form.ContainsKey("DeletedAdditionalImages"))
                {
                    try
                    {
                        var deletedImagesJson = Request.Form["DeletedAdditionalImages"].ToString();
                        var deletedImages = JsonSerializer.Deserialize<List<string>>(deletedImagesJson) ?? new List<string>();

                        foreach (var imgUrl in deletedImages)
                        {
                            if (!string.IsNullOrEmpty(imgUrl) && imgUrl.Contains("/uploads/"))
                            {
                                DeleteImage(imgUrl);
                            }
                            additionalImages.RemoveAll(url => url == imgUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при удалении изображений: {ex.Message}");
                    }
                }

                // Если загружен новый файл главного изображения
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    if (!string.IsNullOrEmpty(product.ImageUrl) && product.ImageUrl.Contains("/uploads/") &&
                        !(Request.Form.ContainsKey("DeleteMainImage") && Request.Form["DeleteMainImage"] == "true"))
                    {
                        DeleteImage(product.ImageUrl);
                    }
                    imageUrl = await UploadImage(model.ImageFile);
                }
                else if (!string.IsNullOrEmpty(model.ImageUrl))
                {
                    imageUrl = model.ImageUrl;
                }

                // Загружаем новые дополнительные изображения
                if (model.AdditionalImageFiles != null && model.AdditionalImageFiles.Any())
                {
                    foreach (var file in model.AdditionalImageFiles)
                    {
                        if (file != null && file.Length > 0)
                        {
                            var fileUrl = await UploadImage(file);
                            additionalImages.Add(fileUrl);
                        }
                    }
                }

                // Обновляем поля
                product.Name = model.Name;
                product.Brand = model.Brand;
                product.Model = model.Model;
                product.Description = model.Description;
                product.PricePerDay = model.PricePerDay;
                product.PricePerWeek = model.PricePerWeek;
                product.Deposit = model.Deposit;
                product.CategoryId = model.CategoryId;
                product.ImageUrl = imageUrl;
                product.AdditionalImages = additionalImages.Any() ? JsonSerializer.Serialize(additionalImages) : null;
                product.IsAvailable = model.IsAvailable;

                try
                {
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Товар успешно обновлен!";
                    return RedirectToAction("Products");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Ошибка при сохранении в БД: " + ex.Message);
                }
            }

            model.Categories = await _context.Categories.ToListAsync();
            return View(model);
        }

        // Удаление товара
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Проверяем, есть ли бронирования
            bool hasBookings = await _context.Bookings.AnyAsync(b => b.ProductId == id);
            if (hasBookings)
            {
                TempData["ErrorMessage"] = "Нельзя удалить товар, на который есть бронирования";
                return RedirectToAction("Products");
            }

            // Удаляем изображение
            if (!string.IsNullOrEmpty(product.ImageUrl) && product.ImageUrl.Contains("/uploads/"))
            {
                DeleteImage(product.ImageUrl);
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Товар удален";
            return RedirectToAction("Products");
        }

        // ========== УПРАВЛЕНИЕ КАТЕГОРИЯМИ ==========

        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }

        [HttpGet]
        public IActionResult CreateCategory()
        {
            return View(new CategoryViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CategoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Проверяем, есть ли уже такая категория
                    var existingCategory = await _context.Categories
                        .FirstOrDefaultAsync(c => c.Name.ToLower() == model.Name.ToLower());

                    if (existingCategory != null)
                    {
                        ModelState.AddModelError("Name", "Категория с таким названием уже существует");
                        return View(model);
                    }

                    // Создаем объект Category из модели представления
                    var category = new Category
                    {
                        Name = model.Name,
                        Description = model.Description
                    };

                    _context.Categories.Add(category);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Категория '{model.Name}' успешно создана!";
                    return RedirectToAction("Categories");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Ошибка при сохранении: " + ex.Message);
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(CategoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var category = await _context.Categories.FindAsync(model.Id);
                    if (category == null)
                    {
                        return NotFound();
                    }

                    // Проверяем, нет ли другой категории с таким же именем
                    var existingCategory = await _context.Categories
                        .FirstOrDefaultAsync(c => c.Name.ToLower() == model.Name.ToLower() && c.Id != model.Id);

                    if (existingCategory != null)
                    {
                        ModelState.AddModelError("Name", "Категория с таким названием уже существует");
                        var categories = await _context.Categories
                            .Include(c => c.Products)
                            .OrderBy(c => c.Name)
                            .ToListAsync();
                        return View("Categories", categories);
                    }

                    // Обновляем поля
                    category.Name = model.Name;
                    category.Description = model.Description;

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Категория '{model.Name}' обновлена";
                    return RedirectToAction("Categories");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Ошибка при обновлении: " + ex.Message);
                }
            }

            var allCategories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View("Categories", allCategories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return NotFound();
            }

            // Проверяем, есть ли товары
            if (category.Products.Any())
            {
                TempData["ErrorMessage"] = "Нельзя удалить категорию, в которой есть товары";
                return RedirectToAction("Categories");
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Категория удалена";
            return RedirectToAction("Categories");
        }

        // ========== УПРАВЛЕНИЕ БРОНИРОВАНИЯМИ ==========

        [HttpGet]
        public async Task<IActionResult> Bookings(string status = "all", int page = 1)
        {
            int pageSize = 20;

            var query = _context.Bookings
                .Include(b => b.Product)
                .Include(b => b.User)
                .AsQueryable();

            if (status != "all")
            {
                query = query.Where(b => b.Status == status);
            }

            int totalBookings = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalBookings / (double)pageSize);

            var bookings = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.TotalPages = totalPages;
            ViewBag.CurrentPage = page;

            return View(bookings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBookingStatus(int id, string status)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            booking.Status = status;
            booking.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Статус бронирования изменен на '{status}'";
            return RedirectToAction("Bookings");
        }

        // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========

        private async Task<string> UploadImage(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return "/uploads/" + uniqueFileName;
        }

        private void DeleteImage(string imageUrl)
        {
            try
            {
                string fileName = imageUrl.Replace("/uploads/", "");
                string filePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", fileName);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch { }
        }

        [HttpGet]
        [Authorize]
        public IActionResult IsAdmin()
        {
            return Json(new { isAdmin = User.IsInRole("Admin") });
        }
    }
}