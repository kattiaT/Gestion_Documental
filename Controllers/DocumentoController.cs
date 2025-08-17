using Google.Apis.Docs.v1;
using Google.Apis.Drive.v3;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

public class DocumentoController : Controller
{
    private readonly DocsService _docsService;
    private readonly DriveService _driveService;

    // Constructor modificado para recibir los servicios
    public DocumentoController(DocsService docsService, DriveService driveService)
    {
        _docsService = docsService;
        _driveService = driveService;
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
        if (documento != null && documento.Length > 0)
        { 
            //se crea objeto con info del archivo q se sube a drive
            var metadataArchivo = new Google.Apis.Drive.v3.Data.File
            {
                Name = documento.FileName,
                MimeType = documento.ContentType
            };

            using (var stream = new MemoryStream())
            {
                // 1) Copiar el archivo subido al stream en memoria
                await documento.CopyToAsync(stream);

                // 2) MUY IMPORTANTE: rebobinar(volver a dejar el puntero al inicio) el stream al inicio
                stream.Position = 0;

                // 3) Preparar la petición de subida
                var request = _driveService.Files.Create(metadataArchivo, stream, documento.ContentType);
                request.Fields = "id"; // Pedimos sólo el ID en la respuesta

                // 4) Subir a drive
                await request.UploadAsync();

                // 5) Obtener el archivo desde la respuesta de la request
                var archivo = request.ResponseBody;      // ESTE sí tiene Id
                var idArchivo = archivo.Id;                 // ahora compila

                // 6) Pasar datos a la vista
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
