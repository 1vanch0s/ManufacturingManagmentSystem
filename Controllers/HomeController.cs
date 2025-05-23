using Microsoft.AspNetCore.Mvc;

namespace ManufacturingManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}