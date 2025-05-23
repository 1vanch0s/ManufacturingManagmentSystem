using Microsoft.AspNetCore.Mvc;
using ManufacturingManagementSystem.Data;
using ManufacturingManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManufacturingManagementSystem.Controllers
{
    public class LinesController : Controller
    {
        private readonly ProductionContext _context;

        public LinesController(ProductionContext context)
        {
            _context = context;
        }

        // GET: /Lines
        public async Task<IActionResult> Index()
        {
            var lines = await _context.ProductionLines
                .Include(l => l.CurrentWorkOrder)
                .ThenInclude(wo => wo.Product)
                .ToListAsync();

            ViewBag.LineCount = lines.Count;
            return View(lines);
        }

        // GET: /Lines/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.WorkOrders = new SelectList(await _context.WorkOrders.ToListAsync(), "Id", "Id", null);
            return View();
        }

        // POST: /Lines/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductionLineCreateDto lineDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                ModelState.AddModelError(string.Empty, "Ошибки валидации: " + string.Join("; ", errors));
                ViewBag.WorkOrders = new SelectList(await _context.WorkOrders.ToListAsync(), "Id", "Id", lineDto.CurrentWorkOrderId);
                return View(lineDto);
            }

            try
            {
                var line = new ProductionLine
                {
                    Name = lineDto.Name,
                    Status = lineDto.Status,
                    EfficiencyFactor = lineDto.EfficiencyFactor,
                    CurrentWorkOrderId = lineDto.CurrentWorkOrderId
                };

                if (lineDto.CurrentWorkOrderId.HasValue)
                {
                    var workOrder = await _context.WorkOrders.FindAsync(lineDto.CurrentWorkOrderId.Value);
                    if (workOrder != null)
                    {
                        workOrder.ProductionLineId = line.Id; // Синхронизация обратной связи
                        _context.Update(workOrder);
                    }
                }

                _context.Add(line);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Ошибка при сохранении: {ex.Message}");
                ViewBag.WorkOrders = new SelectList(await _context.WorkOrders.ToListAsync(), "Id", "Id", lineDto.CurrentWorkOrderId);
                return View(lineDto);
            }
        }

        // GET: /Lines/Edit/{id}
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var line = await _context.ProductionLines
                .Include(l => l.CurrentWorkOrder)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (line == null)
                return NotFound();

            ViewBag.Products = await _context.Products.ToListAsync();
            ViewBag.WorkOrders = new SelectList(await _context.WorkOrders.ToListAsync(), "Id", "Id", line.CurrentWorkOrderId);
            return View(line);
        }

        // POST: /Lines/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Status,EfficiencyFactor,CurrentWorkOrderId")] ProductionLine line)
        {
            if (id != line.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingLine = await _context.ProductionLines
                        .Include(l => l.CurrentWorkOrder)
                        .FirstOrDefaultAsync(l => l.Id == id);

                    if (existingLine != null)
                    {
                        // Обновляем обратную связь, если CurrentWorkOrderId изменился
                        if (existingLine.CurrentWorkOrderId != line.CurrentWorkOrderId)
                        {
                            if (existingLine.CurrentWorkOrderId.HasValue)
                            {
                                var oldWorkOrder = await _context.WorkOrders.FindAsync(existingLine.CurrentWorkOrderId.Value);
                                if (oldWorkOrder != null)
                                {
                                    oldWorkOrder.ProductionLineId = null;
                                    _context.Update(oldWorkOrder);
                                }
                            }
                            if (line.CurrentWorkOrderId.HasValue)
                            {
                                var newWorkOrder = await _context.WorkOrders.FindAsync(line.CurrentWorkOrderId.Value);
                                if (newWorkOrder != null)
                                {
                                    newWorkOrder.ProductionLineId = line.Id;
                                    _context.Update(newWorkOrder);
                                }
                            }
                        }

                        existingLine.Name = line.Name;
                        existingLine.Status = line.Status;
                        existingLine.EfficiencyFactor = line.EfficiencyFactor;
                        existingLine.CurrentWorkOrderId = line.CurrentWorkOrderId;

                        _context.Update(existingLine);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.ProductionLines.Any(e => e.Id == line.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Products = await _context.Products.ToListAsync();
            ViewBag.WorkOrders = new SelectList(await _context.WorkOrders.ToListAsync(), "Id", "Id", line.CurrentWorkOrderId);
            return View(line);
        }
    }
}