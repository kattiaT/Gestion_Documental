using Microsoft.AspNetCore.Mvc;
using Google.Apis.Drive.v3;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

public class DocumentoController : Controller
{
    private readonly DriveService _drive;

    public DocumentoController(DriveService drive)
    {
        _drive = drive;
    }

    public async Task<IActionResult> Index()
    {
        var req = _drive.Files.List();
        req.PageSize = 10;
        req.Fields = "files(id, name, mimeType)";
        var result = await req.ExecuteAsync();  // << Ya NO debe lanzar Forbidden
        return View(result.Files);
    }

    [HttpPost]
    public async Task<IActionResult> Subir(IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest("Archivo vacío.");

        var fileMeta = new Google.Apis.Drive.v3.Data.File
        {
            Name = archivo.FileName
        };

        using var stream = archivo.OpenReadStream();
        var req = _drive.Files.Create(fileMeta, stream, archivo.ContentType);
        req.Fields = "id, name";
        var uploaded = await req.UploadAsync();

        if (uploaded.Status != Google.Apis.Upload.UploadStatus.Completed)
            throw uploaded.Exception ?? new Exception("Falló la subida.");

        var created = req.ResponseBody;
        return Ok(new { created.Id, created.Name });
    }
}
