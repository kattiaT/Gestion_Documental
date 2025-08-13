using System.Diagnostics;
using Gestion_Documental.Models;
using Microsoft.AspNetCore.Mvc;

namespace Gestion_Documental.Controllers
{
    public class DocumentoController : Controller
    {
        private readonly ILogger<DocumentoController> _logger;

        public DocumentoController(ILogger<DocumentoController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
