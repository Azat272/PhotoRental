namespace PhotoRental.Models.Admin
{
    public class ProductListViewModel
    {
        public List<Product> Products { get; set; } = new();
        public int TotalProducts { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public string? SearchQuery { get; set; }
        public int? CategoryId { get; set; }
        public List<Category> Categories { get; set; } = new();
        public int? HighlightId { get; internal set; }
    }
}