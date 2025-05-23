using Microsoft.AspNetCore.Mvc;
using ManufacturingManagementSystem.Data;
using ManufacturingManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace ManufacturingManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersApiController : ControllerBase
    {
        private readonly ProductionContext _context;

        public OrdersApiController(ProductionContext context)
        {
            _context = context;
        }

        // GET: api/orders?status=active&date=today
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkOrder>>> GetWorkOrders(string? status, string? date)
        {
            var query = _context.WorkOrders
                .Include(wo => wo.Product)
                .Include(wo => wo.ProductionLine)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && status.ToLower() == "active")
            {
                query = query.Where(wo => wo.Status == "Pending" || wo.Status == "InProgress");
            }
            else if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(wo => wo.Status == status);
            }

            if (!string.IsNullOrEmpty(date) && date.ToLower() == "today")
            {
                var today = DateTime.UtcNow.Date;
                query = query.Where(wo => wo.StartDate.Date == today);
            }

            return await query.ToListAsync();
        }

        // GET: api/orders/5
        [HttpGet("{id}")]
        public async Task<ActionResult<WorkOrder>> GetWorkOrder(int id)
        {
            var workOrder = await _context.WorkOrders
                .Include(wo => wo.Product)
                .Include(wo => wo.ProductionLine)
                .FirstOrDefaultAsync(wo => wo.Id == id);

            if (workOrder == null)
            {
                return NotFound();
            }

            return workOrder;
        }

        // POST: api/orders
        [HttpPost]
        public async Task<ActionResult<WorkOrder>> CreateWorkOrder(WorkOrder workOrder)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var product = await _context.Products.FindAsync(workOrder.ProductId);
            if (product == null)
            {
                ModelState.AddModelError("ProductId", "Продукт не найден");
                return BadRequest(ModelState);
            }

            ProductionLine line = null;
            if (workOrder.ProductionLineId.HasValue)
            {
                line = await _context.ProductionLines
                    .Include(l => l.CurrentWorkOrder)
                    .FirstOrDefaultAsync(l => l.Id == workOrder.ProductionLineId.Value);
                if (line == null)
                {
                    ModelState.AddModelError("ProductionLineId", "Производственная линия не найдена");
                    return BadRequest(ModelState);
                }
                else if (line.CurrentWorkOrderId.HasValue && line.CurrentWorkOrderId != 0)
                {
                    ModelState.AddModelError("ProductionLineId", "Эта линия уже назначена другому заказу");
                    return BadRequest(ModelState);
                }
            }

            // Проверка доступности материалов
            var productMaterials = await _context.ProductMaterials
                .Where(pm => pm.ProductId == workOrder.ProductId)
                .Include(pm => pm.Material)
                .ToListAsync();

            foreach (var pm in productMaterials)
            {
                var requiredQuantity = pm.QuantityNeeded * workOrder.Quantity;
                if (pm.Material.Quantity < requiredQuantity)
                {
                    ModelState.AddModelError("Materials", $"Недостаточно материала {pm.Material.Name}: требуется {requiredQuantity}, доступно {pm.Material.Quantity}");
                    return BadRequest(ModelState);
                }
            }

            var productionTimePerUnit = product.ProductionTimePerUnit;
            var totalProductionTime = productionTimePerUnit * workOrder.Quantity;

            if (line != null)
            {
                totalProductionTime = (int)(totalProductionTime * line.EfficiencyFactor);
            }

            if (workOrder.StartDate.Kind == DateTimeKind.Unspecified)
            {
                workOrder.StartDate = DateTime.SpecifyKind(workOrder.StartDate, DateTimeKind.Utc);
            }
            else if (workOrder.StartDate.Kind == DateTimeKind.Local)
            {
                workOrder.StartDate = workOrder.StartDate.ToUniversalTime();
            }

            workOrder.EstimatedEndDate = workOrder.StartDate.AddMinutes(totalProductionTime);
            if (workOrder.EstimatedEndDate.Kind == DateTimeKind.Unspecified)
            {
                workOrder.EstimatedEndDate = DateTime.SpecifyKind(workOrder.EstimatedEndDate, DateTimeKind.Utc);
            }

            workOrder.Status = "Pending";

            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();

            // Синхронизация линии
            if (line != null)
            {
                line.CurrentWorkOrderId = workOrder.Id;
                _context.Update(line);
                await _context.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetWorkOrder), new { id = workOrder.Id }, workOrder);
        }

        // PUT: api/orders/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWorkOrder(int id, WorkOrder workOrder)
        {
            if (id != workOrder.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingOrder = await _context.WorkOrders
                .Include(o => o.ProductionLine)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (existingOrder == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(workOrder.ProductId);
            if (product == null)
            {
                ModelState.AddModelError("ProductId", "Продукт не найден");
                return BadRequest(ModelState);
            }

            ProductionLine line = null;
            if (workOrder.ProductionLineId.HasValue)
            {
                line = await _context.ProductionLines
                    .Include(l => l.CurrentWorkOrder)
                    .FirstOrDefaultAsync(l => l.Id == workOrder.ProductionLineId.Value);
                if (line == null)
                {
                    ModelState.AddModelError("ProductionLineId", "Производственная линия не найдена");
                    return BadRequest(ModelState);
                }
                else if (line.CurrentWorkOrderId.HasValue && line.CurrentWorkOrderId != workOrder.Id)
                {
                    ModelState.AddModelError("ProductionLineId", "Эта линия уже назначена другому заказу");
                    return BadRequest(ModelState);
                }
            }

            // Проверка доступности материалов
            var productMaterials = await _context.ProductMaterials
                .Where(pm => pm.ProductId == workOrder.ProductId)
                .Include(pm => pm.Material)
                .ToListAsync();

            foreach (var pm in productMaterials)
            {
                var requiredQuantity = pm.QuantityNeeded * workOrder.Quantity;
                if (pm.Material.Quantity < requiredQuantity)
                {
                    ModelState.AddModelError("Materials", $"Недостаточно материала {pm.Material.Name}: требуется {requiredQuantity}, доступно {pm.Material.Quantity}");
                    return BadRequest(ModelState);
                }
            }

            var productionTimePerUnit = product.ProductionTimePerUnit;
            var totalProductionTime = productionTimePerUnit * workOrder.Quantity;

            if (line != null)
            {
                totalProductionTime = (int)(totalProductionTime * line.EfficiencyFactor);
            }

            if (workOrder.StartDate.Kind == DateTimeKind.Unspecified)
            {
                workOrder.StartDate = DateTime.SpecifyKind(workOrder.StartDate, DateTimeKind.Utc);
            }
            else if (workOrder.StartDate.Kind == DateTimeKind.Local)
            {
                workOrder.StartDate = workOrder.StartDate.ToUniversalTime();
            }

            workOrder.EstimatedEndDate = workOrder.StartDate.AddMinutes(totalProductionTime);
            if (workOrder.EstimatedEndDate.Kind == DateTimeKind.Unspecified)
            {
                workOrder.EstimatedEndDate = DateTime.SpecifyKind(workOrder.EstimatedEndDate, DateTimeKind.Utc);
            }

            workOrder.Status = existingOrder.Status;

            _context.Entry(existingOrder).CurrentValues.SetValues(workOrder);

            // Синхронизация линии
            if (line != null)
            {
                line.CurrentWorkOrderId = workOrder.Id;
                _context.Update(line);
            }
            else if (existingOrder.ProductionLineId.HasValue)
            {
                var oldLine = await _context.ProductionLines
                    .Include(l => l.CurrentWorkOrder)
                    .FirstOrDefaultAsync(l => l.Id == existingOrder.ProductionLineId.Value);
                if (oldLine != null && oldLine.CurrentWorkOrderId == existingOrder.Id)
                {
                    oldLine.CurrentWorkOrderId = null;
                    _context.Update(oldLine);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.WorkOrders.AnyAsync(e => e.Id == id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        // PUT: api/orders/5/progress
        [HttpPut("{id}/progress")]
        public async Task<IActionResult> UpdateProgress(int id, [FromBody] int percent)
        {
            if (percent < 0 || percent > 100)
            {
                return BadRequest("Процент должен быть от 0 до 100");
            }

            var workOrder = await _context.WorkOrders.FindAsync(id);
            if (workOrder == null)
            {
                return NotFound();
            }

            if (percent == 100)
            {
                workOrder.Status = "Completed";
            }
            else if (percent > 0 && workOrder.Status == "Pending")
            {
                workOrder.Status = "InProgress";
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.WorkOrders.AnyAsync(e => e.Id == id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }
    }
}