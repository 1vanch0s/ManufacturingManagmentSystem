using Microsoft.AspNetCore.Mvc;
using ManufacturingManagementSystem.Data;
using ManufacturingManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace ManufacturingManagementSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsApiController : ControllerBase
    {
        private readonly ProductionContext _context;

        public ProductsApiController(ProductionContext context)
        {
            _context = context;
        }

        // GET: /api/products?category={category}
        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] string? category)
        {
            var products = _context.Products.AsQueryable();
            if (!string.IsNullOrEmpty(category))
            {
                products = products.Where(p => p.Category == category);
            }
            return Ok(await products.ToListAsync());
        }

        // GET: /api/products/{id}/materials
        [HttpGet("{id}/materials")]
        public async Task<IActionResult> GetProductMaterials(int id)
        {
            var productMaterials = await _context.ProductMaterials
                .Where(pm => pm.ProductId == id)
                .Include(pm => pm.Material)
                .Select(pm => new { pm.MaterialId, pm.Material.Name, pm.QuantityNeeded })
                .ToListAsync();

            if (!productMaterials.Any())
                return NotFound("Материалы для продукта не найдены");

            return Ok(productMaterials);
        }

        // POST: /api/products
        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateDto productDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var product = new Product
            {
                Name = productDto.Name,
                ProductionTimePerUnit = productDto.ProductionTime,
                Category = productDto.Category,
                MinimalStock = productDto.MinimalStock ?? 0,
                Description = productDto.Description,
                Specifications = productDto.Specifications
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetProducts), new { id = product.Id }, product);
        }
    }

    public class ProductCreateDto
    {
        public string Name { get; set; }
        public int ProductionTime { get; set; }
        public string Category { get; set; }
        public int? MinimalStock { get; set; }
        public string Description { get; set; }
        public string Specifications { get; set; }
    }
}