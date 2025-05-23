using Microsoft.AspNetCore.Mvc;
using ManufacturingManagementSystem.Data;
using ManufacturingManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace ManufacturingManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CalculateController : ControllerBase
    {
        private readonly ProductionContext _context;

        public CalculateController(ProductionContext context)
        {
            _context = context;
        }

        // POST: api/calculate/production
        [HttpPost("production")]
        public async Task<ActionResult<object>> CalculateProductionTime([FromBody] ProductionCalculationRequest request)
        {
            if (request.Quantity <= 0)
            {
                return BadRequest("Количество должно быть больше 0");
            }

            var product = await _context.Products.FindAsync(request.ProductId);
            if (product == null)
            {
                return NotFound("Продукт не найден");
            }

            double totalProductionTime = product.ProductionTimePerUnit * request.Quantity;

            // Если указана линия, учитываем её коэффициент эффективности
            if (request.ProductionLineId.HasValue)
            {
                var line = await _context.ProductionLines.FindAsync(request.ProductionLineId.Value);
                if (line == null)
                {
                    return NotFound("Производственная линия не найдена");
                }
                totalProductionTime = totalProductionTime * line.EfficiencyFactor;
            }

            // Проверяем доступность материалов
            var productMaterials = await _context.ProductMaterials
                .Where(pm => pm.ProductId == request.ProductId)
                .Include(pm => pm.Material)
                .ToListAsync();

            var materialAvailability = new List<object>();
            foreach (var pm in productMaterials)
            {
                var requiredQuantity = pm.QuantityNeeded * request.Quantity;
                var availableQuantity = pm.Material.Quantity;
                var isSufficient = availableQuantity >= requiredQuantity;

                materialAvailability.Add(new
                {
                    MaterialId = pm.MaterialId,
                    MaterialName = pm.Material.Name,
                    RequiredQuantity = requiredQuantity,
                    AvailableQuantity = availableQuantity,
                    IsSufficient = isSufficient
                });

                if (!isSufficient)
                {
                    return BadRequest($"Недостаточно материала {pm.Material.Name}: требуется {requiredQuantity}, доступно {availableQuantity}");
                }
            }

            return new
            {
                TotalProductionTimeInMinutes = totalProductionTime,
                Materials = materialAvailability
            };
        }
    }

    public class ProductionCalculationRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public int? ProductionLineId { get; set; }
    }
}