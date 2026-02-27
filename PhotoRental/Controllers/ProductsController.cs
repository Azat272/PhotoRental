using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoRental.Models;
using PhotoRental.Data;
using System.Threading.Tasks;
using System.Linq;

public class ProductController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProductController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Product/Catalog
    public async Task<IActionResult> Catalog()
    {
        var products = await _context.Products.Include(p => p.Category).ToListAsync();
        return View(products);
    }

    // GET: /Product/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (product == null)
        {
            return NotFound();
        }
        return View(product);
    }

    // POST: /Product/Order
    [HttpPost]
    public async Task<IActionResult> Order(int productId, DateTime startDate, DateTime endDate)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
        {
            return NotFound();
        }

        var totalPrice = (endDate - startDate).Days * product.PricePerDay;

        var order = new Booking
        {

            ProductId = productId,
            StartDate = startDate,
            EndDate = endDate,
            TotalPrice = totalPrice
        };

        _context.Bookings.Add(order);
        await _context.SaveChangesAsync();

        return RedirectToAction("Index", "Home");
    }
}