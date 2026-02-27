namespace PhotoRental.Models
{
    public class HomeViewModel
    {
        public string Title { get; set; }  // Заголовок страницы
        public string Description { get; set; }  // Описание
        public List<Category> Categories { get; set; }  // Список категорий
        public List<Product> FeaturedProducts { get; set; }  // Список популярных товаров
    }

}