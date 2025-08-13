using System.Web;
using Microsoft.AspNetCore.Mvc;

public class DocumentoController : Controller
{
    //vista con el btn de filepicker
    public IActionResult Index() => View();

    //visor generico con iframe
    public IActionResult Previsualizacion(string src, string titulo = "Vista Previa")
    {
        if (string.IsNullOrWhiteSpace(src)) return RedirectToAction(nameof(Index));//si no hay archivo, redirige a la vista principal

        //si viene un docx/xlsx/pptx directo, lo vamos a envolver con office online
        if (!src.Contains("view.officeapps.live.com") && //q no este en formato de visualizacion de office online
            (src.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ||
            src.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            src.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase)))

        {
            var urlEnc = HttpUtility.UrlEncode(src); // se codifica la url para q sea segura para la web
            src = $"https://view.officeapps.live.com/op/embed.aspx?src={urlEnc}";// le decimos al visor q archivo abrir, lo muestra incrustado dentro de un iframe
            //visor de office online
        }

        ViewBag.Titulo = titulo; //manda el titulo para mostrar en la vista
        ViewBag.Src = src; //manda la url final
        return View();

    }


    // private static readonly List<(string Titulo, string Url, string Tipo)> _demo = new()
    // {

    // };
}
