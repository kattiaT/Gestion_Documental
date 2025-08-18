using Google.Apis.Docs.v1;
using Google.Apis.Drive.v3;
using Google.Apis.Auth.AspNetCore3; // Asegúrate de que este espacio de nombres esté presente
using Google.Apis.Services; // Agregar este espacio de nombres
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

public class DocumentoController : Controller
{
    private readonly DocsService _docsService;
    private readonly DriveService _driveService;
    private readonly IGoogleAuthProvider _auth; // Inyectar el proveedor de autenticación

    // Constructor modificado para recibir los servicios
    public DocumentoController(DocsService docsService, DriveService driveService, IGoogleAuthProvider auth)
    {
        _docsService = docsService;
        _driveService = driveService;
        _auth = auth; // Asignar el proveedor de autenticación
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> ObtenerDocumento(string idDocumento)
    {
        var request = _docsService.Documents.Get(idDocumento);
        var documento = await request.ExecuteAsync();
        return View(documento);
    }

    [HttpPost]
    public async Task<IActionResult> SubirDocumento(IFormFile documento)
    {
        if (!User.Identity.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account"); // Redirigir a la página de inicio de sesión
        }
        var cred = await _auth.GetCredentialAsync(); // Obtener credenciales del usuario
        var drive = new DriveService(new BaseClientService.Initializer {
            HttpClientInitializer = cred,
            ApplicationName = "Gestion-de-Documentos"
        });

        if (documento != null && documento.Length > 0)
        { 
            var metadataArchivo = new Google.Apis.Drive.v3.Data.File
            {
                Name = documento.FileName,
                MimeType = documento.ContentType
            };

            using (var stream = new MemoryStream())
            {
                await documento.CopyToAsync(stream);
                stream.Position = 0;

                var request = drive.Files.Create(metadataArchivo, stream, documento.ContentType);
                request.Fields = "id"; // Pedimos sólo el ID en la respuesta

                await request.UploadAsync();
                var archivo = request.ResponseBody; // Obtener el archivo desde la respuesta
                var idArchivo = archivo.Id;

                ViewBag.Document = new
                {
                    Url = $"https://drive.google.com/file/d/{idArchivo}/view",
                    Id = idArchivo
                };
            }
        }

        return View("Index");
    }
}
