using Google.Apis.Docs.v1;
using Google.Apis.Drive.v3;
using Google.Apis.Auth.AspNetCore3; 
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http; 
using System.Collections.Generic;
using System.IO; 
using System.Threading.Tasks;

public class DocumentoController : Controller
{
    private readonly DocsService _docsService;
    private readonly DriveService _driveService;
    private readonly IGoogleAuthProvider _auth;

    public DocumentoController(DocsService docsService, DriveService driveService, IGoogleAuthProvider auth)
    {
        _docsService = docsService;
        _driveService = driveService;
        _auth = auth;
    }

    public async Task<IActionResult> Index()
    {
        var documentos = await _driveService.Files.List().ExecuteAsync();
        ViewBag.Files = documentos.Files; // Asegúrate de que esto esté configurado correctamente
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> GuardarCambios(IFormFile documento)
    {
        if (!User.Identity.IsAuthenticated)
        {
            return Challenge(new AuthenticationProperties { RedirectUri = Url.Action("SubirDocumento") });
        }

        if (documento != null && documento.Length > 0)
        {
            using (var stream = new MemoryStream())
            {
                await documento.CopyToAsync(stream);
                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = documento.FileName,
                    MimeType = documento.ContentType
                };

                var request = _driveService.Files.Create(fileMetadata, stream, documento.ContentType);
                request.Fields = "id";
                var file = await request.UploadAsync();
            }
        }

        return RedirectToAction("Index");
    }

    public IActionResult SubirDocumento()
    {
        return View();
    }
}
