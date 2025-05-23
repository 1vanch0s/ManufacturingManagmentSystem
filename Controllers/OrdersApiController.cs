using Microsoft.AspNetCore.Mvc;
using ManufacturingManagementSystem.Data;
using ManufacturingManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;

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

        // GET: api/orders
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkOrder>>> GetWorkOrders()
        {
            return await _context.WorkOrders
                .Include(wo => wo.Product)
                .Include(wo => wo.ProductionLine)
                .ToListAsync();
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

            ProductionLine? line = null;
            if (workOrder.ProductionLineId.HasValue)
            {
                line = await _context.ProductionLines.FindAsync(workOrder.ProductionLineId.Value);
                if (line == null)
                {
                    ModelState.AddModelError("ProductionLineId", "Производственная линия не найдена");
                    return BadRequest(ModelState);
                }
            }

            var productionTimePerUnit = product.ProductionTimePerUnit;
            var totalProductionTime = productionTimePerUnit * workOrder.Quantity;

            if (line != null)
            {
                totalProductionTime = (int)(totalProductionTime * line.EfficiencyFactor);
            }

            workOrder.EstimatedEndDate = workOrder.StartDate.AddMinutes(totalProductionTime);
            workOrder.Status = "Pending";

            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();

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

            ProductionLine? line = null;
            if (workOrder.ProductionLineId.HasValue)
            {
                line = await _context.ProductionLines.FindAsync(workOrder.ProductionLineId.Value);
                if (line == null)
                {
                    ModelState.AddModelError("ProductionLineId", "Производственная линия не найдена");
                    return BadRequest(ModelState);
                }
            }

            var productionTimePerUnit = product.ProductionTimePerUnit;
            var totalProductionTime = productionTimePerUnit * workOrder.Quantity;

            if (line != null)
            {
                totalProductionTime = (int)(totalProductionTime * line.EfficiencyFactor);
            }

            workOrder.EstimatedEndDate = workOrder.StartDate.AddMinutes(totalProductionTime);
            workOrder.Status = existingOrder.Status;

            _context.Entry(existingOrder).CurrentValues.SetValues(workOrder);

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