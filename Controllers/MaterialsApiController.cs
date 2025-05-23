using Microsoft.AspNetCore.Mvc;
using ManufacturingManagementSystem.Data;
using ManufacturingManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace ManufacturingManagementSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MaterialsApiController : ControllerBase
    {
        private readonly ProductionContext _context;

        public MaterialsApiController(ProductionContext context)
        {
            _context = context;
        }

        // GET: /api/materials?low_stock=true
        [HttpGet]
        public async Task<IActionResult> GetMaterials([FromQuery] bool? low_stock)
        {
            var materials = _context.Materials.AsQueryable();
            if (low_stock == true)
            {
                materials = materials.Where(m => m.Quantity < m.MinimalStock);
            }
            return Ok(await materials.ToListAsync());
        }

        // POST: /api/materials
        [HttpPost]
        public async Task<IActionResult> AddMaterial([FromBody] Material material)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _context.Materials.Add(material);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMaterials), new { id = material.Id }, material);
        }

        // PUT: /api/materials/{id}/stock
        [HttpPut("{id}/stock")]
        public async Task<IActionResult> UpdateStock(int id, [FromBody] decimal amount)
        {
            if (amount < 0)
                return BadRequest("Количество не может быть отрицательным");

            var material = await _context.Materials.FindAsync(id);
            if (material == null)
                return NotFound("Материал не найден");

            material.Quantity = amount;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}