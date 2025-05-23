using Microsoft.AspNetCore.Mvc;
using ManufacturingManagementSystem.Data;
using ManufacturingManagementSystem.Models;
using ManufacturingManagementSystem.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace ManufacturingManagementSystem.Controllers
{
    public class LinesController : Controller
    {
        private readonly ProductionContext _context;

        public LinesController(ProductionContext context)
        {
            _context = context;
        }

        // GET: /Lines/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var lines = await _context.ProductionLines
                .Include(l => l.CurrentWorkOrder)
                .ThenInclude(wo => wo.Product)
                .ToListAsync();

            // Проверяем завершение заказов
            foreach (var line in lines)
            {
                if (line.CurrentWorkOrder != null && DateTime.UtcNow >= line.CurrentWorkOrder.EstimatedEndDate && line.CurrentWorkOrder.Status == "InProgress")
                {
                    line.CurrentWorkOrder.Status = "Completed";
                    line.CurrentWorkOrder.ProductionLineId = null;
                    line.Status = "Stopped";
                    line.CurrentWorkOrderId = null;
                }
            }
            await _context.SaveChangesAsync();

            return View(lines);
        }

        // GET: /Lines/Schedule/{id}
        public async Task<IActionResult> Schedule(int id)
        {
            var line = await _context.ProductionLines
                .Include(l => l.WorkOrders)
                .ThenInclude(wo => wo.Product)
                .FirstOrDefaultAsync(l => l.Id == id);
            if (line == null)
            {
                return NotFound();
            }
            return View(line);
        }

        // POST: /Lines/Reschedule/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule(int id, DateTime StartDate)
        {
            var order = await _context.WorkOrders
                .Include(o => o.Product)
                .Include(o => o.ProductionLine)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            if (StartDate.Kind == DateTimeKind.Unspecified)
            {
                StartDate = DateTime.SpecifyKind(StartDate, DateTimeKind.Utc);
            }
            else if (StartDate.Kind == DateTimeKind.Local)
            {
                StartDate = StartDate.ToUniversalTime();
            }

            order.StartDate = StartDate;

            var totalProductionTime = order.Product.ProductionTimePerUnit * order.Quantity;
            if (order.ProductionLine != null)
            {
                totalProductionTime = (int)(totalProductionTime * order.ProductionLine.EfficiencyFactor);
            }

            order.EstimatedEndDate = order.StartDate.AddMinutes(totalProductionTime);
            if (order.EstimatedEndDate.Kind == DateTimeKind.Unspecified)
            {
                order.EstimatedEndDate = DateTime.SpecifyKind(order.EstimatedEndDate, DateTimeKind.Utc);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Schedule), new { id = order.ProductionLineId });
        }

        // GET: /Lines/StartLine/{id}
        public async Task<IActionResult> StartLine(int id)
        {
            var line = await _context.ProductionLines.FindAsync(id);
            if (line == null)
            {
                return NotFound();
            }

            var products = await _context.Products.ToListAsync();
            var model = new StartLineModel
            {
                LineId = id,
                Products = products.Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name }).ToList()
            };
            return View(model);
        }

        // POST: /Lines/StartLine/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartLine(int id, [Bind("ProductId,Quantity")] StartLineModel model)
        {
            var line = await _context.ProductionLines
                .Include(l => l.CurrentWorkOrder)
                .FirstOrDefaultAsync(l => l.Id == id);
            if (line == null)
            {
                return NotFound();
            }

            Console.WriteLine($"StartLine POST - LineId: {id}, ProductId: {model.ProductId}, Quantity: {model.Quantity}");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.WriteLine($"ModelState validation failed: {string.Join("; ", errors)}");
                var products = await _context.Products.ToListAsync();
                model.Products = products.Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name }).ToList();
                model.LineId = id;
                return View(model);
            }

            var product = await _context.Products.FindAsync(model.ProductId);
            if (product == null)
            {
                Console.WriteLine("Product not found");
                ModelState.AddModelError("ProductId", "Продукт не найден");
                var products = await _context.Products.ToListAsync();
                model.Products = products.Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name }).ToList();
                model.LineId = id;
                return View(model);
            }

            // Проверка доступности материалов
            Console.WriteLine($"Checking materials for ProductId: {model.ProductId}");
            var productMaterials = await _context.ProductMaterials
                .Where(pm => pm.ProductId == model.ProductId)
                .Include(pm => pm.Material)
                .ToListAsync();

            foreach (var pm in productMaterials)
            {
                var requiredQuantity = pm.QuantityNeeded * model.Quantity;
                Console.WriteLine($"Material: {pm.Material?.Name}, Required: {requiredQuantity}, Available: {pm.Material?.Quantity}");
                if (pm.Material == null || pm.Material.Quantity < requiredQuantity)
                {
                    ModelState.AddModelError("Materials", $"Недостаточно материала {pm.Material?.Name ?? "Неизвестный материал"}: требуется {requiredQuantity}, доступно {pm.Material?.Quantity ?? 0}");
                    var products = await _context.Products.ToListAsync();
                    model.Products = products.Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name }).ToList();
                    model.LineId = id;
                    return View(model);
                }
            }

            try
            {
                // Завершаем текущий заказ, если он есть
                if (line.CurrentWorkOrder != null)
                {
                    Console.WriteLine($"Terminating existing order: {line.CurrentWorkOrder.Id}");
                    if (DateTime.UtcNow >= line.CurrentWorkOrder.EstimatedEndDate)
                    {
                        line.CurrentWorkOrder.Status = "Completed";
                        line.CurrentWorkOrder.ProductionLineId = null;
                    }
                    else
                    {
                        line.CurrentWorkOrder.Status = "Cancelled";
                        line.CurrentWorkOrder.ProductionLineId = null;
                    }
                    line.CurrentWorkOrderId = null;
                    line.Status = "Stopped";
                    await _context.SaveChangesAsync();
                }

                // Создаем новый заказ
                var order = new WorkOrder
                {
                    ProductId = model.ProductId,
                    ProductionLineId = id,
                    Quantity = model.Quantity,
                    StartDate = DateTime.UtcNow,
                    Status = "InProgress"
                };

                var totalProductionTime = product.ProductionTimePerUnit * model.Quantity * line.EfficiencyFactor;
                order.EstimatedEndDate = order.StartDate.AddMinutes(totalProductionTime);

                Console.WriteLine($"Creating new order - ProductId: {order.ProductId}, Quantity: {order.Quantity}, StartDate: {order.StartDate}, EstimatedEndDate: {order.EstimatedEndDate}");
                _context.WorkOrders.Add(order);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Order created with Id: {order.Id}");

                // Обновляем линию
                line.Status = "Active";
                line.CurrentWorkOrderId = order.Id;
                Console.WriteLine($"Updating line - Status: {line.Status}, CurrentWorkOrderId: {line.CurrentWorkOrderId}");
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in StartLine: {ex}");
                ModelState.AddModelError("", $"Произошла ошибка при запуске линии: {ex.Message}");
                var products = await _context.Products.ToListAsync();
                model.Products = products.Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name }).ToList();
                model.LineId = id;
                return View(model);
            }
        }

        // POST: /Lines/StopLine/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StopLine(int id)
        {
            var line = await _context.ProductionLines
                .Include(l => l.CurrentWorkOrder)
                .FirstOrDefaultAsync(l => l.Id == id);
            if (line == null)
            {
                return NotFound();
            }

            if (line.CurrentWorkOrder != null)
            {
                line.CurrentWorkOrder.Status = "Cancelled";
                line.CurrentWorkOrder.ProductionLineId = null;
            }

            line.Status = "Stopped";
            line.CurrentWorkOrderId = null;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Dashboard));
        }

        // POST: /Lines/UpdateEfficiency/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEfficiency(int id, float EfficiencyFactor)
        {
            var line = await _context.ProductionLines
                .Include(l => l.CurrentWorkOrder)
                .ThenInclude(wo => wo.Product)
                .FirstOrDefaultAsync(l => l.Id == id);
            if (line == null)
            {
                return NotFound();
            }

            if (EfficiencyFactor < 0.5 || EfficiencyFactor > 2.0)
            {
                TempData["Error"] = "Коэффициент эффективности должен быть от 0.5 до 2.0";
                return RedirectToAction(nameof(Dashboard));
            }

            line.EfficiencyFactor = EfficiencyFactor;

            // Пересчитываем EstimatedEndDate текущего заказа
            if (line.CurrentWorkOrder != null)
            {
                var order = line.CurrentWorkOrder;
                var totalProductionTime = order.Product.ProductionTimePerUnit * order.Quantity * line.EfficiencyFactor;
                order.EstimatedEndDate = order.StartDate.AddMinutes(totalProductionTime);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Dashboard));
        }

        // GET: /Lines
        public async Task<IActionResult> Index()
        {
            var lines = await _context.ProductionLines
                .Include(l => l.CurrentWorkOrder)
                .ThenInclude(wo => wo.Product)
                .ToListAsync();
            return View(lines);
        }

        // GET: /Lines/Create
        public IActionResult Create()
        {
            return View(new ProductionLineCreateDto());
        }

        // POST: /Lines/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Status,EfficiencyFactor,CurrentWorkOrderId")] ProductionLineCreateDto dto)
        {
            if (ModelState.IsValid)
            {
                var line = new ProductionLine
                {
                    Name = dto.Name,
                    Status = dto.Status,
                    EfficiencyFactor = dto.EfficiencyFactor,
                    CurrentWorkOrderId = dto.CurrentWorkOrderId
                };
                _context.Add(line);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(dto);
        }

        // GET: /Lines/Edit/{id}
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var line = await _context.ProductionLines
                .Include(l => l.CurrentWorkOrder)
                .ThenInclude(wo => wo.Product)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (line == null)
            {
                return NotFound();
            }

            var dto = new ProductionLineUpdateDto
            {
                Id = line.Id,
                Name = line.Name,
                Status = line.Status.ToString(),
                EfficiencyFactor = line.EfficiencyFactor,
                CurrentWorkOrderId = line.CurrentWorkOrderId
            };

            // Заполняем список заказов для выпадающего меню
            var workOrders = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "Нет заказа" }
            };
            if (line.CurrentWorkOrder != null)
            {
                workOrders.Add(new SelectListItem
                {
                    Value = line.CurrentWorkOrder.Id.ToString(),
                    Text = $"Заказ #{line.CurrentWorkOrder.Id} ({line.CurrentWorkOrder.Product?.Name})",
                    Selected = true
                });
            }
            ViewBag.WorkOrders = workOrders;

            return View(dto);
        }

        // POST: /Lines/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Status,EfficiencyFactor,CurrentWorkOrderId")] ProductionLineUpdateDto dto)
        {
            if (id != dto.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingLine = await _context.ProductionLines
                        .Include(l => l.CurrentWorkOrder)
                        .ThenInclude(wo => wo.Product)
                        .FirstOrDefaultAsync(l => l.Id == id);
                    if (existingLine == null)
                    {
                        return NotFound();
                    }

                    // Обновляем линию
                    existingLine.Name = dto.Name;
                    existingLine.Status = dto.Status;
                    existingLine.EfficiencyFactor = dto.EfficiencyFactor;

                    // Обработка текущего заказа
                    if (dto.CurrentWorkOrderId.HasValue)
                    {
                        var order = await _context.WorkOrders.FindAsync(dto.CurrentWorkOrderId.Value);
                        if (order != null)
                        {
                            if (order.ProductionLineId != dto.Id)
                            {
                                // Отвязываем от предыдущей линии, если есть
                                if (existingLine.CurrentWorkOrder != null && existingLine.CurrentWorkOrderId != dto.CurrentWorkOrderId)
                                {
                                    existingLine.CurrentWorkOrder.ProductionLineId = null;
                                    existingLine.CurrentWorkOrder.Status = "Cancelled";
                                }
                                order.ProductionLineId = dto.Id;
                                order.Status = "InProgress";
                                existingLine.Status = "Active";
                            }
                        }
                    }
                    else if (existingLine.CurrentWorkOrderId.HasValue)
                    {
                        var oldOrder = await _context.WorkOrders.FindAsync(existingLine.CurrentWorkOrderId.Value);
                        if (oldOrder != null && oldOrder.ProductionLineId == dto.Id)
                        {
                            oldOrder.ProductionLineId = null;
                            oldOrder.Status = "Cancelled";
                        }
                        existingLine.Status = "Stopped";
                    }

                    existingLine.CurrentWorkOrderId = dto.CurrentWorkOrderId;

                    _context.Update(existingLine);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Dashboard));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.ProductionLines.AnyAsync(e => e.Id == id))
                    {
                        return NotFound();
                    }
                    throw;
                }
            }
            return View(dto);
        }
    }

    public class StartLineModel
    {
        [Required]
        public int LineId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public int Quantity { get; set; }

        public List<SelectListItem> Products { get; set; }
    }
}