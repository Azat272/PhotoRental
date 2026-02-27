using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhotoRental.Models
{
    public class CheckoutViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new();

        [Required(ErrorMessage = "Укажите имя")]
        [Display(Name = "Имя")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя должно содержать от 2 до 100 символов")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите телефон")]
        [Display(Name = "Телефон")]
        [Phone(ErrorMessage = "Введите корректный номер телефона")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите email")]
        [Display(Name = "Email")]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите дату начала")]
        [Display(Name = "Дата начала")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Укажите дату окончания")]
        [Display(Name = "Дата окончания")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Display(Name = "Способ получения")]
        public string DeliveryType { get; set; } = "delivery";

        [Display(Name = "Адрес доставки")]
        public string? Address { get; set; }

        [Display(Name = "Квартира/офис")]
        public string? Apartment { get; set; }

        [Display(Name = "Комментарий для курьера")]
        public string? CourierComment { get; set; }

        [Display(Name = "Комментарий к заказу")]
        public string? Comment { get; set; }

        [Display(Name = "Способ оплаты")]
        public string PaymentMethod { get; set; } = "card";

        // Добавляем эти поля для получения из формы
        public int Days { get; set; }
        public decimal DeliveryCost { get; set; }
        public decimal TotalAmount { get; set; }

        // Вычисляемые свойства (можно оставить для отображения)
        public int TotalItems => Items.Sum(i => i.Quantity);
        public decimal ItemsTotal => Items.Sum(i => i.TotalPrice);
        public decimal RentalCost => ItemsTotal * Days;
        public decimal GrandTotal => RentalCost + DeliveryCost;
    }
}