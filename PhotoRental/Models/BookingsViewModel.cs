using PhotoRental.Models;

namespace PhotoRental.Models
{
    public class BookingsViewModel
    {
        public string CurrentTab { get; set; } = "active";
        public List<Booking> ActiveBookings { get; set; } = new();
        public List<Booking> HistoryBookings { get; set; } = new();
    }
}