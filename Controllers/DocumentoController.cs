using Microsoft.AspNetCore.Mvc;

public class DocumentoController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    private static readonly List<(string Titulo, string Url, string Tipo)> _demo = new()
    {
        
    };
}
