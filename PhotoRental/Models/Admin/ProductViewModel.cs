using Microsoft.AspNetCore.Http;
using PhotoRental.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace PhotoRental.Models.Admin
{
    public class ProductViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Введите название товара")]
        [Display(Name = "Название")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите бренд")]
        [Display(Name = "Бренд")]
        public string Brand { get; set; } = string.Empty;

        [Display(Name = "Модель")]
        public string? Model { get; set; }

        [Required(ErrorMessage = "Введите описание")]
        [Display(Name = "Описание")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите цену за день")]
        [Display(Name = "Цена за день (₽)")]
        [Range(0, 1000000, ErrorMessage = "Цена должна быть от 0 до 1 000 000")]
        public decimal PricePerDay { get; set; }

        [Display(Name = "Цена за неделю (₽)")]
        [Range(0, 1000000, ErrorMessage = "Цена должна быть от 0 до 1 000 000")]
        public decimal? PricePerWeek { get; set; }

        [Required(ErrorMessage = "Укажите размер залога")]
        [Display(Name = "Залог (₽)")]
        [Range(0, 1000000, ErrorMessage = "Залог должен быть от 0 до 1 000 000")]
        public decimal Deposit { get; set; }

        [Display(Name = "Категория")]
        [Required(ErrorMessage = "Выберите категорию")]
        public int CategoryId { get; set; }

        [Display(Name = "Главное изображение")]
        public IFormFile? ImageFile { get; set; }

        [Display(Name = "Дополнительные изображения")]
        public List<IFormFile>? AdditionalImageFiles { get; set; }

        [Display(Name = "URL главного изображения")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Дополнительные изображения (URL)")]
        public string? AdditionalImagesJson { get; set; }

        // Для удобства работы в коде
        public List<string> AdditionalImages
        {
            get
            {
                if (string.IsNullOrEmpty(AdditionalImagesJson))
                    return new List<string>();
                try
                {
                    return JsonSerializer.Deserialize<List<string>>(AdditionalImagesJson) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                AdditionalImagesJson = JsonSerializer.Serialize(value);
            }
        }

        [Display(Name = "Доступен для аренды")]
        public bool IsAvailable { get; set; } = true;
        [Display(Name = "Дополнительные изображения (существующие)")]
        public List<string> ExistingAdditionalImages { get; set; } = new List<string>();
        // Для выпадающего списка категорий
        public List<Category>? Categories { get; set; }
    }
}