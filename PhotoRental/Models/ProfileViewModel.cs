using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhotoRental.Models
{
    // 1. Модель для главной страницы
    public class ProfileDashboardViewModel
    {
        public ApplicationUser User { get; set; }
        public int ActiveBookingsCount { get; set; }
        public int CompletedBookingsCount { get; set; }
        public decimal TotalSpent { get; set; }
        public int CartItemsCount { get; set; }

        public List<Booking> RecentBookings { get; set; }
    }

   

    // 3. Редактирование профиля
    public class EditProfileViewModel
    {
        [Display(Name = "Полное имя")]
        [Required(ErrorMessage = "Введите полное имя")]
        [MinLength(2, ErrorMessage = "Имя должно содержать минимум 2 символа")]
        public string FullName { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Телефон")]
        [Phone(ErrorMessage = "Введите корректный номер телефона")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Дата регистрации")]
        public DateTime CreatedAt { get; set; }
    }

    // 4. Смена пароля
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Введите текущий пароль")]
        [DataType(DataType.Password)]
        [Display(Name = "Текущий пароль")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Введите новый пароль")]
        [MinLength(6, ErrorMessage = "Пароль должен быть минимум 6 символов")]
        [DataType(DataType.Password)]
        [Display(Name = "Новый пароль")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Подтвердите новый пароль")]
        [DataType(DataType.Password)]
        [Display(Name = "Подтверждение пароля")]
        [Compare("NewPassword", ErrorMessage = "Пароли не совпадают")]
        public string ConfirmPassword { get; set; }
    }

    // 5. Платежные данные
    public class PaymentsViewModel
    {
        public List<PaymentMethod> PaymentMethods { get; set; }
        public List<Payment> PaymentHistory { get; set; }
    }

    public class PaymentMethod
    {
        public int Id { get; set; }
        public string CardType { get; set; }
        public string LastFourDigits { get; set; }
        public string ExpiryMonth { get; set; }
        public string CardNumber { get; set; } = string.Empty; // Добавить эту строку
        public string ExpiryYear { get; set; }
        public string CardHolderName { get; set; }
        public bool IsDefault { get; set; }
    }

    public class Payment
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
    }

    public class AddCardViewModel
    {
        [Required(ErrorMessage = "Введите номер карты")]
        [CreditCard(ErrorMessage = "Введите корректный номер карты")]
        public string CardNumber { get; set; }

        [Required(ErrorMessage = "Введите имя владельца")]
        public string CardHolderName { get; set; }

        [Required(ErrorMessage = "Введите месяц")]
        [Range(1, 12, ErrorMessage = "Некорректный месяц")]
        public int ExpiryMonth { get; set; }

        [Required(ErrorMessage = "Введите год")]
        public int ExpiryYear { get; set; }

        [Required(ErrorMessage = "Введите CVV")]
        [StringLength(3, MinimumLength = 3, ErrorMessage = "CVV должен быть 3 цифры")]
        public string Cvv { get; set; }

        public bool SetAsDefault { get; set; }
    }
}