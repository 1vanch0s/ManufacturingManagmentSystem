using Microsoft.AspNetCore.Mvc;
using ManufacturingManagementSystem.Data;
using ManufacturingManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace ManufacturingManagementSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LinesApiController : ControllerBase
    {
        private readonly ProductionContext _context;

        public LinesApiController(ProductionContext context)
        {
            _context = context;
        }

        // GET: /api/lines?available=true
        [HttpGet]
        public async Task<IActionResult> GetLines([FromQuery] bool? available)
        {
            var lines = _context.ProductionLines.AsQueryable();
            if (available == true)
            {
                lines = lines.Where(l => l.Status == "Active" && l.CurrentWorkOrderId == null);
            }
            return Ok(await lines.ToListAsync());
        }

        // PUT: /api/lines/{id}/status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateLineStatus(int id, [FromBody] string status)
        {
            if (string.IsNullOrEmpty(status) || !new[] { "Active", "Stopped" }.Contains(status))
                return BadRequest("Статус должен быть 'Active' или 'Stopped'");

            var line = await _context.ProductionLines.FindAsync(id);
            if (line == null)
                return NotFound("Линия не найдена");

            line.Status = status;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET: /api/lines/{id}/schedule
        [HttpGet("{id}/schedule")]
        public async Task<IActionResult> GetLineSchedule(int id)
        {
            var line = await _context.ProductionLines
                .Include(l => l.WorkOrders)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (line == null)
                return NotFound("Линия не найдена");

            var schedule = line.WorkOrders
                .Select(wo => new
                {
                    OrderId = wo.Id,
                    ProductName = wo.Product.Name,
                    Quantity = wo.Quantity,
                    StartDate = wo.StartDate,
                    EstimatedEndDate = wo.EstimatedEndDate,
                    Status = wo.Status
                })
                .ToList();

            return Ok(schedule);
        }
    }
}