using System.ComponentModel.DataAnnotations;

namespace PhotoRental.Models.Admin
{
    public class CategoryViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Введите название категории")]
        [Display(Name = "Название категории")]
        [MinLength(2, ErrorMessage = "Название должно содержать минимум 2 символа")]
        [MaxLength(50, ErrorMessage = "Название не должно превышать 50 символов")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Описание")]
        [MaxLength(200, ErrorMessage = "Описание не должно превышать 200 символов")]
        public string? Description { get; set; }
    }
}