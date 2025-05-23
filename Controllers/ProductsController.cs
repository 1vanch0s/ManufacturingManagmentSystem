using Microsoft.AspNetCore.Mvc;
using ManufacturingManagementSystem.Data;
using ManufacturingManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;

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
            var products = _context.Products.AsQueryable();

            // Фильтр по категории
            if (!string.IsNullOrEmpty(category))
            {
                products = products.Where(p => p.Category == category);
            }

            // Поиск по имени
            if (!string.IsNullOrEmpty(searchString))
            {
                products = products.Where(p => p.Name.Contains(searchString));
            }

            // Получение списка категорий для выпадающего списка
            var categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .ToListAsync();
            ViewBag.Categories = new SelectList(categories);

            var productList = await products.ToListAsync();
            return View(productList);
        }

        // GET: /Products/Create
        public async Task<IActionResult> Create()
        {
            // Передаем список материалов для привязки
            ViewBag.Materials = await _context.Materials.ToListAsync();
            return View();
        }

        // POST: /Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Specifications,Category,MinimalStock,ProductionTimePerUnit")] Product product, int[] materialIds, decimal[] quantities)
        {
            if (ModelState.IsValid)
            {
                _context.Add(product);
                await _context.SaveChangesAsync();

                // Привязка материалов к продукту
                if (materialIds != null && quantities != null && materialIds.Length == quantities.Length)
                {
                    for (int i = 0; i < materialIds.Length; i++)
                    {
                        if (quantities[i] > 0)
                        {
                            var productMaterial = new ProductMaterial
                            {
                                ProductId = product.Id,
                                MaterialId = materialIds[i],
                                QuantityNeeded = quantities[i]
                            };
                            _context.ProductMaterials.Add(productMaterial);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Materials = await _context.Materials.ToListAsync();
            return View(product);
        }

        // GET: /Products/Edit/{id}
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var product = await _context.Products
                .Include(p => p.ProductMaterials)
                .ThenInclude(pm => pm.Material)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            ViewBag.Materials = await _context.Materials.ToListAsync();
            return View(product);
        }

        // POST: /Products/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Specifications,Category,MinimalStock,ProductionTimePerUnit")] Product product, int[] materialIds, decimal[] quantities)
        {
            if (id != product.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);

                    // Удаляем старые связи с материалами
                    var existingMaterials = _context.ProductMaterials.Where(pm => pm.ProductId == id);
                    _context.ProductMaterials.RemoveRange(existingMaterials);

                    // Добавляем новые связи с материалами
                    if (materialIds != null && quantities != null && materialIds.Length == quantities.Length)
                    {
                        for (int i = 0; i < materialIds.Length; i++)
                        {
                            if (quantities[i] > 0)
                            {
                                var productMaterial = new ProductMaterial
                                {
                                    ProductId = product.Id,
                                    MaterialId = materialIds[i],
                                    QuantityNeeded = quantities[i]
                                };
                                _context.ProductMaterials.Add(productMaterial);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Products.Any(e => e.Id == product.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Materials = await _context.Materials.ToListAsync();
            return View(product);
        }
    }
}