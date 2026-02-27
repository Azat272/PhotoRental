using System;
using System.Collections.Generic;

namespace PhotoRental.Models;

public partial class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Brand { get; set; } = null!;

    public string? Model { get; set; }

    public string? Description { get; set; }

    public decimal PricePerDay { get; set; }

    public decimal? PricePerWeek { get; set; }

    public decimal Deposit { get; set; }

    public string? ImageUrl { get; set; }
    public string? AdditionalImages { get; set; } // JSON строка с дополнительными фото

    public bool? IsAvailable { get; set; }

    public int CategoryId { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
