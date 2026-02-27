using System;
using System.Collections.Generic;

namespace PhotoRental.Models
{
    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new();
        public decimal TotalAmount => Items.Sum(i => i.TotalPrice);
        public int TotalItems => Items.Sum(i => i.Quantity);
    }

    public class CartItemViewModel
    {
        public int CartItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal PricePerDay { get; set; }
        public int Quantity { get; set; }
        public DateTime AddedAt { get; set; }
        public decimal TotalPrice => PricePerDay * Quantity;
    }
}