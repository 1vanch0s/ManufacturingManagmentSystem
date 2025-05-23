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

                ProductionLine? line = null;
                if (order.ProductionLineId.HasValue)
                {
                    line = await _context.ProductionLines.FindAsync(order.ProductionLineId.Value);
                    if (line == null)
                    {
                        ModelState.AddModelError("ProductionLineId", "Производственная линия не найдена");
                        Console.WriteLine("Production line not found");
                        ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
                        ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name");
                        return View(order);
                    }
                    else if (line.CurrentWorkOrderId != null && line.CurrentWorkOrderId != 0)
                    {
                        ModelState.AddModelError("ProductionLineId", "Эта линия уже назначена другому заказу");
                        Console.WriteLine("Line already assigned");
                        ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
                        ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name");
                        return View(order);
                    }
                    line.CurrentWorkOrderId = order.Id; // Синхронизация
                    _context.Update(line);
                }

                var productionTimePerUnit = product.ProductionTimePerUnit;
                var totalProductionTime = productionTimePerUnit * order.Quantity;

                if (line != null)
                {
                    totalProductionTime = (int)(totalProductionTime * line.EfficiencyFactor);
                }

                // Конвертируем StartDate в UTC
                if (order.StartDate.Kind == DateTimeKind.Unspecified)
                {
                    order.StartDate = DateTime.SpecifyKind(order.StartDate, DateTimeKind.Utc);
                }
                else if (order.StartDate.Kind == DateTimeKind.Local)
                {
                    order.StartDate = order.StartDate.ToUniversalTime();
                }

                order.EstimatedEndDate = order.StartDate.AddMinutes(totalProductionTime);
                // Убедимся, что EstimatedEndDate тоже в UTC
                if (order.EstimatedEndDate.Kind == DateTimeKind.Unspecified)
                {
                    order.EstimatedEndDate = DateTime.SpecifyKind(order.EstimatedEndDate, DateTimeKind.Utc);
                }

                order.Status = "Pending";

                Console.WriteLine($"Before SaveChanges - Order: {order.Id}, EstimatedEndDate: {order.EstimatedEndDate}");
                _context.Add(order);
                await _context.SaveChangesAsync();
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

                ProductionLine? line = null;
                if (order.ProductionLineId.HasValue)
                {
                    line = await _context.ProductionLines.FindAsync(order.ProductionLineId.Value);
                    if (line == null)
                    {
                        ModelState.AddModelError("ProductionLineId", "Производственная линия не найдена");
                        Console.WriteLine("Production line not found");
                        ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", order.ProductId);
                        ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name", order.ProductionLineId);
                        return View(order);
                    }
                    else if (line.CurrentWorkOrderId != null && line.CurrentWorkOrderId != order.Id)
                    {
                        ModelState.AddModelError("ProductionLineId", "Эта линия уже назначена другому заказу");
                        Console.WriteLine("Line already assigned");
                        ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", order.ProductId);
                        ViewBag.ProductionLines = new SelectList(await _context.ProductionLines.ToListAsync(), "Id", "Name", order.ProductionLineId);
                        return View(order);
                    }
                    line.CurrentWorkOrderId = order.Id; // Синхронизация
                    _context.Update(line);
                }
                else if (existingOrder.ProductionLineId.HasValue)
                {
                    var oldLine = await _context.ProductionLines.FindAsync(existingOrder.ProductionLineId.Value);
                    if (oldLine != null && oldLine.CurrentWorkOrderId == existingOrder.Id)
                    {
                        oldLine.CurrentWorkOrderId = null;
                        _context.Update(oldLine);
                    }
                }

                var productionTimePerUnit = product.ProductionTimePerUnit;
                var totalProductionTime = productionTimePerUnit * order.Quantity;

                if (line != null)
                {
                    totalProductionTime = (int)(totalProductionTime * line.EfficiencyFactor);
                }

                // Конвертируем StartDate в UTC
                if (order.StartDate.Kind == DateTimeKind.Unspecified)
                {
                    order.StartDate = DateTime.SpecifyKind(order.StartDate, DateTimeKind.Utc);
                }
                else if (order.StartDate.Kind == DateTimeKind.Local)
                {
                    order.StartDate = order.StartDate.ToUniversalTime();
                }

                order.EstimatedEndDate = order.StartDate.AddMinutes(totalProductionTime);
                // Убедимся, что EstimatedEndDate тоже в UTC
                if (order.EstimatedEndDate.Kind == DateTimeKind.Unspecified)
                {
                    order.EstimatedEndDate = DateTime.SpecifyKind(order.EstimatedEndDate, DateTimeKind.Utc);
                }

                order.Status = existingOrder.Status;

                Console.WriteLine($"Before SaveChanges - Order: {order.Id}, EstimatedEndDate: {order.EstimatedEndDate}");
                _context.Entry(existingOrder).CurrentValues.SetValues(order);
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
    }
}