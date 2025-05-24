using Microsoft.AspNetCore.Mvc;
using ManufacturingManagementSystem.Data;
using ManufacturingManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace ManufacturingManagementSystem.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ProductionContext _context;

        public ProductsController(ProductionContext context)
        {
            _context = context;
        }

        // GET: /Products
        public async Task<IActionResult> Index(string category, string searchString)
        {
            var productsQuery = _context.Products
                .Include(p => p.ProductMaterials)
                .ThenInclude(pm => pm.Material)
                .AsQueryable();

            // Получаем уникальные категории
            var categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .ToListAsync();
            ViewData["Categories"] = categories;
            ViewData["SelectedCategory"] = category;
            ViewData["SearchString"] = searchString;

            // Фильтрация по категории
            if (!string.IsNullOrEmpty(category))
            {
                productsQuery = productsQuery.Where(p => p.Category == category);
            }

            // Поиск по названию
            if (!string.IsNullOrEmpty(searchString))
            {
                productsQuery = productsQuery.Where(p => p.Name.Contains(searchString));
            }

            var products = await productsQuery.ToListAsync();
            return View(products);
        }

        // GET: /Products/Create
        public async Task<IActionResult> Create()
        {
            ViewData["Materials"] = await _context.Materials.ToListAsync();
            return View(new Product());
        }

        // POST: /Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Specifications,Category,MinimalStock,ProductionTimePerUnit")] Product product, List<ProductMaterial> ProductMaterials)
        {
            Console.WriteLine($"Create POST - Name: {product.Name}, Category: {product.Category}, MinimalStock: {product.MinimalStock}, ProductionTimePerUnit: {product.ProductionTimePerUnit}");

            if (string.IsNullOrEmpty(product.Name) || string.IsNullOrEmpty(product.Category) || string.IsNullOrEmpty(product.Description) || string.IsNullOrEmpty(product.Specifications) || product.MinimalStock <= 0 || product.ProductionTimePerUnit <= 0)
            {
                ModelState.AddModelError(string.Empty, "Все поля обязательны для заполнения, и значения должны быть больше нуля.");
                Console.WriteLine("Validation failed due to empty or invalid fields.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.WriteLine($"ModelState validation failed: {string.Join("; ", errors)}");
                ViewData["Materials"] = await _context.Materials.ToListAsync();
                return View(product);
            }

            try
            {
                Console.WriteLine("Adding product to context...");
                _context.Add(product);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Product saved with Id: {product.Id}");

                // Добавляем связанные материалы
                if (ProductMaterials != null && ProductMaterials.Any())
                {
                    Console.WriteLine($"Processing {ProductMaterials.Count} ProductMaterials...");
                    foreach (var pm in ProductMaterials)
                    {
                        if (pm.MaterialId != 0 && pm.QuantityNeeded > 0)
                        {
                            Console.WriteLine($"Adding ProductMaterial - MaterialId: {pm.MaterialId}, QuantityNeeded: {pm.QuantityNeeded}");
                            pm.ProductId = product.Id;
                            _context.ProductMaterials.Add(pm);
                        }
                    }
                    await _context.SaveChangesAsync();
                    Console.WriteLine("ProductMaterials saved.");
                }
                else
                {
                    Console.WriteLine("No ProductMaterials provided.");
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in Create: {ex}");
                ModelState.AddModelError(string.Empty, $"Ошибка при создании продукта: {ex.Message}");
                ViewData["Materials"] = await _context.Materials.ToListAsync();
                return View(product);
            }
        }

        // GET: /Products/Edit/{id}
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.ProductMaterials)
                .ThenInclude(pm => pm.Material)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            ViewData["Materials"] = await _context.Materials.ToListAsync();
            return View(product);
        }

        // POST: /Products/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Specifications,Category,MinimalStock,ProductionTimePerUnit")] Product product, List<ProductMaterial> ProductMaterials)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.WriteLine($"ModelState validation failed in Edit: {string.Join("; ", errors)}");
                ModelState.AddModelError(string.Empty, "Ошибки валидации: " + string.Join("; ", errors));
                ViewData["Materials"] = await _context.Materials.ToListAsync();
                return View(product);
            }

            try
            {
                // Обновляем продукт
                var existingProduct = await _context.Products
                    .Include(p => p.ProductMaterials)
                    .FirstOrDefaultAsync(p => p.Id == id);
                if (existingProduct == null)
                {
                    return NotFound();
                }

                existingProduct.Name = product.Name;
                existingProduct.Description = product.Description;
                existingProduct.Specifications = product.Specifications;
                existingProduct.Category = product.Category;
                existingProduct.MinimalStock = product.MinimalStock;
                existingProduct.ProductionTimePerUnit = product.ProductionTimePerUnit;

                // Удаляем старые связи с материалами
                _context.ProductMaterials.RemoveRange(existingProduct.ProductMaterials);
                await _context.SaveChangesAsync();

                // Добавляем новые связи
                if (ProductMaterials != null && ProductMaterials.Any())
                {
                    foreach (var pm in ProductMaterials)
                    {
                        if (pm.MaterialId != 0 && pm.QuantityNeeded > 0)
                        {
                            pm.ProductId = product.Id;
                            _context.ProductMaterials.Add(pm);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                _context.Update(existingProduct);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Console.WriteLine($"Concurrency Exception in Edit: {ex}");
                if (!await _context.Products.AnyAsync(e => e.Id == id))
                {
                    return NotFound();
                }
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in Edit: {ex}");
                ModelState.AddModelError(string.Empty, $"Ошибка при редактировании продукта: {ex.Message}");
                ViewData["Materials"] = await _context.Materials.ToListAsync();
                return View(product);
            }
        }
    }
}