using Microsoft.AspNetCore.Mvc;
using ManufacturingManagementSystem.Data;
using ManufacturingManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManufacturingManagementSystem.Controllers
{
    public class OrdersController : Controller
    {
        private readonly ProductionContext _context;

        public OrdersController(ProductionContext context)
        {
            _context = context;
        }

        // GET: /Orders
        public async Task<IActionResult> Index()
        {
            var orders = await _context.WorkOrders
                .Include(wo => wo.Product)
                .Include(wo => wo.ProductionLine)
                .ToListAsync();
            return View(orders);
        }

        // GET: /Orders/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
            ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name");
            return View();
        }

        // POST: /Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductId,ProductionLineId,Quantity,StartDate")] WorkOrder order)
        {
            Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.WriteLine($"Validation errors: {string.Join("; ", errors)}");
                ModelState.AddModelError(string.Empty, "Ошибки валидации: " + string.Join("; ", errors));
                ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
                ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name");
                return View(order);
            }

            try
            {
                Console.WriteLine($"ProductId: {order.ProductId}, ProductionLineId: {order.ProductionLineId}, Quantity: {order.Quantity}, StartDate: {order.StartDate}");
                var product = await _context.Products.FindAsync(order.ProductId);
                if (product == null)
                {
                    ModelState.AddModelError("ProductId", "Продукт не найден");
                    Console.WriteLine("Product not found");
                    ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
                    ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name");
                    return View(order);
                }

                ProductionLine line = null;
                if (order.ProductionLineId.HasValue)
                {
                    line = await _context.ProductionLines
                        .Include(l => l.CurrentWorkOrder)
                        .FirstOrDefaultAsync(l => l.Id == order.ProductionLineId.Value);
                    if (line == null)
                    {
                        ModelState.AddModelError("ProductionLineId", "Производственная линия не найдена");
                        Console.WriteLine("Production line not found");
                        ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
                        ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name");
                        return View(order);
                    }
                    else if (line.CurrentWorkOrderId.HasValue && line.CurrentWorkOrderId != 0)
                    {
                        ModelState.AddModelError("ProductionLineId", "Эта линия уже назначена другому заказу");
                        Console.WriteLine("Line already assigned");
                        ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
                        ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name");
                        return View(order);
                    }
                }

                // Проверка доступности материалов
                var productMaterials = await _context.ProductMaterials
                    .Where(pm => pm.ProductId == order.ProductId)
                    .Include(pm => pm.Material)
                    .ToListAsync();

                foreach (var pm in productMaterials)
                {
                    var requiredQuantity = pm.QuantityNeeded * order.Quantity;
                    if (pm.Material == null || pm.Material.Quantity < requiredQuantity)
                    {
                        ModelState.AddModelError("Materials", $"Недостаточно материала {pm.Material?.Name ?? "Неизвестный материал"}: требуется {requiredQuantity}, доступно {pm.Material?.Quantity ?? 0}");
                        ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
                        ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name");
                        return View(order);
                    }
                }

                var productionTimePerUnit = product.ProductionTimePerUnit;
                var totalProductionTime = productionTimePerUnit * order.Quantity;

                if (line != null)
                {
                    totalProductionTime = (int)(totalProductionTime * line.EfficiencyFactor);
                }

                if (order.StartDate.Kind == DateTimeKind.Unspecified)
                {
                    order.StartDate = DateTime.SpecifyKind(order.StartDate, DateTimeKind.Utc);
                }
                else if (order.StartDate.Kind == DateTimeKind.Local)
                {
                    order.StartDate = order.StartDate.ToUniversalTime();
                }

                order.EstimatedEndDate = order.StartDate.AddMinutes(totalProductionTime);
                if (order.EstimatedEndDate.Kind == DateTimeKind.Unspecified)
                {
                    order.EstimatedEndDate = DateTime.SpecifyKind(order.EstimatedEndDate, DateTimeKind.Utc);
                }

                order.Status = "Pending";

                Console.WriteLine($"Before SaveChanges - Order: {order.Id}, EstimatedEndDate: {order.EstimatedEndDate}");
                _context.Add(order);
                await _context.SaveChangesAsync();

                // Синхронизация линии
                if (line != null)
                {
                    line.CurrentWorkOrderId = order.Id;
                    line.Status = "Stopped"; // Остается Stopped, пока заказ не запущен через Start
                    _context.Update(line);
                    await _context.SaveChangesAsync();
                }

                Console.WriteLine($"After SaveChanges - Order saved with Id: {order.Id}");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
                ModelState.AddModelError(string.Empty, $"Ошибка при сохранении: {ex.Message}");
                ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
                ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name");
                return View(order);
            }
        }

        // GET: /Orders/Edit/{id}
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.WorkOrders
                .Include(o => o.Product)
                .Include(o => o.ProductionLine)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", order.ProductId);
            ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name", order.ProductionLineId);
            return View(order);
        }

        // POST: /Orders/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ProductId,ProductionLineId,Quantity,StartDate")] WorkOrder order)
        {
            Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
            if (id != order.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.WriteLine($"Validation errors: {string.Join("; ", errors)}");
                ModelState.AddModelError(string.Empty, "Ошибки валидации: " + string.Join("; ", errors));
                ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", order.ProductId);
                ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name", order.ProductionLineId);
                return View(order);
            }

            try
            {
                var existingOrder = await _context.WorkOrders
                    .Include(o => o.ProductionLine)
                    .Include(o => o.Product)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (existingOrder == null)
                {
                    return NotFound();
                }

                var product = await _context.Products.FindAsync(order.ProductId);
                if (product == null)
                {
                    ModelState.AddModelError("ProductId", "Продукт не найден");
                    Console.WriteLine("Product not found");
                    ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", order.ProductId);
                    ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name", order.ProductionLineId);
                    return View(order);
                }

                ProductionLine line = null;
                if (order.ProductionLineId.HasValue)
                {
                    line = await _context.ProductionLines
                        .Include(l => l.CurrentWorkOrder)
                        .FirstOrDefaultAsync(l => l.Id == order.ProductionLineId.Value);
                    if (line == null)
                    {
                        ModelState.AddModelError("ProductionLineId", "Производственная линия не найдена");
                        Console.WriteLine("Production line not found");
                        ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", order.ProductId);
                        ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name", order.ProductionLineId);
                        return View(order);
                    }
                    else if (line.CurrentWorkOrderId.HasValue && line.CurrentWorkOrderId != order.Id)
                    {
                        ModelState.AddModelError("ProductionLineId", "Эта линия уже назначена другому заказу");
                        Console.WriteLine("Line already assigned");
                        ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", order.ProductId);
                        ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name", order.ProductionLineId);
                        return View(order);
                    }
                }

                // Проверка доступности материалов
                var productMaterials = await _context.ProductMaterials
                    .Where(pm => pm.ProductId == order.ProductId)
                    .Include(pm => pm.Material)
                    .ToListAsync();

                foreach (var pm in productMaterials)
                {
                    var requiredQuantity = pm.QuantityNeeded * order.Quantity;
                    if (pm.Material == null || pm.Material.Quantity < requiredQuantity)
                    {
                        ModelState.AddModelError("Materials", $"Недостаточно материала {pm.Material?.Name ?? "Неизвестный материал"}: требуется {requiredQuantity}, доступно {pm.Material?.Quantity ?? 0}");
                        ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", order.ProductId);
                        ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name", order.ProductionLineId);
                        return View(order);
                    }
                }

                var productionTimePerUnit = product.ProductionTimePerUnit;
                var totalProductionTime = productionTimePerUnit * order.Quantity;

                if (line != null)
                {
                    totalProductionTime = (int)(totalProductionTime * line.EfficiencyFactor);
                }

                if (order.StartDate.Kind == DateTimeKind.Unspecified)
                {
                    order.StartDate = DateTime.SpecifyKind(order.StartDate, DateTimeKind.Utc);
                }
                else if (order.StartDate.Kind == DateTimeKind.Local)
                {
                    order.StartDate = order.StartDate.ToUniversalTime();
                }

                order.EstimatedEndDate = order.StartDate.AddMinutes(totalProductionTime);
                if (order.EstimatedEndDate.Kind == DateTimeKind.Unspecified)
                {
                    order.EstimatedEndDate = DateTime.SpecifyKind(order.EstimatedEndDate, DateTimeKind.Utc);
                }

                order.Status = existingOrder.Status;

                Console.WriteLine($"Before SaveChanges - Order: {order.Id}, EstimatedEndDate: {order.EstimatedEndDate}");
                _context.Entry(existingOrder).CurrentValues.SetValues(order);

                // Синхронизация линии
                if (line != null)
                {
                    line.CurrentWorkOrderId = order.Id;
                    if (order.Status == "InProgress")
                    {
                        line.Status = "Active";
                    }
                    else
                    {
                        line.Status = "Stopped";
                    }
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
                        oldLine.Status = "Stopped";
                        _context.Update(oldLine);
                    }
                }

                existingOrder.ProductionLineId = order.ProductionLineId;

                await _context.SaveChangesAsync();
                Console.WriteLine($"After SaveChanges - Order updated with Id: {order.Id}");
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Console.WriteLine($"Concurrency Exception: {ex}");
                ModelState.AddModelError(string.Empty, $"Ошибка при обновлении: {ex.Message}");
                ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", order.ProductId);
                ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name", order.ProductionLineId);
                return View(order);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
                ModelState.AddModelError(string.Empty, $"Неизвестная ошибка: {ex.Message}");
                ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", order.ProductId);
                ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name", order.ProductionLineId);
                return View(order);
            }
        }

        // POST: /Orders/Start/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id)
        {
            var order = await _context.WorkOrders
                .Include(o => o.ProductionLine)
                .Include(o => o.Product)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != "Pending")
            {
                TempData["Error"] = "Заказ можно запустить только со статусом Pending";
                return RedirectToAction(nameof(Index));
            }

            order.Status = "InProgress";
            if (order.ProductionLine != null)
            {
                order.ProductionLine.Status = "Active";
                var totalProductionTime = order.Product.ProductionTimePerUnit * order.Quantity * order.ProductionLine.EfficiencyFactor;
                order.StartDate = DateTime.UtcNow;
                order.EstimatedEndDate = order.StartDate.AddMinutes(totalProductionTime);
                _context.Update(order.ProductionLine);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: /Orders/Cancel/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var order = await _context.WorkOrders
                .Include(o => o.ProductionLine)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            if (order.Status == "Completed" || order.Status == "Cancelled")
            {
                TempData["Error"] = "Заказ уже завершен или отменен";
                return RedirectToAction(nameof(Index));
            }

            order.Status = "Cancelled";
            if (order.ProductionLine != null && order.ProductionLine.CurrentWorkOrderId == order.Id)
            {
                order.ProductionLine.CurrentWorkOrderId = null;
                order.ProductionLine.Status = "Stopped";
                _context.Update(order.ProductionLine);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}