using System;
using System.Collections.Generic;

namespace PhotoRental.Models
{
    public class CatalogViewModel
    {
        public string Title { get; set; } = "Каталог оборудования";
        public string Description { get; set; } = "Профессиональная фото- и видеотехника для аренды";

        // Фильтры
        public int? SelectedCategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string SearchQuery { get; set; }
        public string SortBy { get; set; } = "popular";

        // Списки
        public List<Category> Categories { get; set; }
        public List<Product> Products { get; set; }
        public List<Product> PopularProducts { get; set; } // Для блока популярных товаров внизу

        // Пагинация
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 12;
        public int TotalProducts { get; set; }

        // Цены для фильтра
        public decimal MinAvailablePrice { get; set; }
        public decimal MaxAvailablePrice { get; set; }
    }
}