using System;
using System.Collections.Generic;

namespace PhotoRental.Models;

public partial class Booking
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int UserId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public decimal TotalPrice { get; set; }

    public decimal Deposit { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? Notes { get; set; }
    public virtual ApplicationUser User { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public virtual Product Product { get; set; } = null!;

}
