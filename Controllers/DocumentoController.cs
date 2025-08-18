using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;


// 👇 Alias para evitar conflicto con System.IO.File
using DriveFile = Google.Apis.Drive.v3.Data.File;
using DrivePermission = Google.Apis.Drive.v3.Data.Permission;

public class DocumentoController : Controller
{
    private readonly DriveService _drive;

    public DocumentoController(DriveService drive)
    {
        _drive = drive;
    }

    // 👉 Acción GET que devuelve la vista con el formulario
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Subir(IFormFile archivo)
    {
        try
        {
            if (archivo == null || archivo.Length == 0)
                return BadRequest("Archivo vacío.");

            var meta = new DriveFile { Name = archivo.FileName };

            using var stream = archivo.OpenReadStream();
            var create = _drive.Files.Create(meta, stream, archivo.ContentType ?? "application/octet-stream");
            create.Fields = "id, name";
            var upload = await create.UploadAsync();

            if (upload.Status != UploadStatus.Completed)
                throw upload.Exception ?? new Exception("Falló la subida.");

            var created = create.ResponseBody;

            var permiso = new DrivePermission { Type = "anyone", Role = "reader" };
            await _drive.Permissions.Create(permiso, created.Id).ExecuteAsync();

            var previewUrl = $"https://drive.google.com/file/d/{created.Id}/preview";
            return Ok(new { created.Id, created.Name, previewUrl });
        }
        catch (Exception ex)
        {
            // 👇 Devuelve detalle al navegador para depuración
            return StatusCode(500, $"Error interno: {ex.Message}");
        }
    }

    // 👉 Acción para iniciar sesión con Google
    public IActionResult Login(string returnUrl = "/")
    {
        return Challenge(new AuthenticationProperties { RedirectUri = returnUrl }, "Google");
    }

    // 👉 Acción para cerrar sesión
    public IActionResult Logout()
    {
        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            "Google");
    }

    // 👉 Ejemplo de acción que necesita estar logueado
    [Authorize]
    public IActionResult Upload()
    {
        return View();
    }

}
