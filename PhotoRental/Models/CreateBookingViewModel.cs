using System;
using System.ComponentModel.DataAnnotations;

namespace PhotoRental.Models
{
    public class CreateBookingViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public decimal PricePerDay { get; set; }
        public decimal Deposit { get; set; }

        [Required(ErrorMessage = "Укажите дату начала")]
        [Display(Name = "Дата начала")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Укажите дату окончания")]
        [Display(Name = "Дата окончания")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Укажите имя")]
        [Display(Name = "Имя")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя должно содержать от 2 до 100 символов")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите телефон")]
        [Display(Name = "Телефон")]
        [Phone(ErrorMessage = "Введите корректный номер телефона")]
        [RegularExpression(@"^\+?[0-9]{10,15}$", ErrorMessage = "Введите корректный номер телефона")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите email")]
        [Display(Name = "Email")]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Display(Name = "Комментарий")]
        [StringLength(500, ErrorMessage = "Комментарий не должен превышать 500 символов")]
        public string? Comment { get; set; }

        public int Days => (EndDate - StartDate).Days;
        public decimal TotalPrice => PricePerDay * Days;
    }
}