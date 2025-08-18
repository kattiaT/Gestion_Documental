using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using DriveFile = Google.Apis.Drive.v3.Data.File;
using DrivePermission = Google.Apis.Drive.v3.Data.Permission;

public class DocumentoController : Controller
{
    private const string AppFolderName = "Gestion_Documental"; // carpeta fija en tu Drive

    // ===== Helpers =====
    private DriveService? GetDrive()
    {
        var accessToken = HttpContext.GetTokenAsync("Google", "access_token")
                                     .GetAwaiter().GetResult();
        if (string.IsNullOrEmpty(accessToken)) return null;

        var cred = GoogleCredential.FromAccessToken(accessToken);
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = cred,
            ApplicationName = "Gestion_Documental"
        });
    }

    private async Task<string> EnsureAppFolderAsync(DriveService drive)
    {
        // Buscar carpeta por nombre
        var list = drive.Files.List();
        list.Q = $"mimeType='application/vnd.google-apps.folder' and name='{AppFolderName}' and trashed=false";
        list.Fields = "files(id,name)";
        var res = await list.ExecuteAsync();
        if (res.Files?.Count > 0) return res.Files[0].Id;

        // Crear si no existe
        var meta = new DriveFile { Name = AppFolderName, MimeType = "application/vnd.google-apps.folder" };
        var create = drive.Files.Create(meta);
        create.Fields = "id";
        var folder = await create.ExecuteAsync();
        return folder.Id;
    }

    // ===== UI =====
    [HttpGet]
    [Authorize] // si querés forzar login al entrar
    public IActionResult Index() => View();

    // ===== API =====
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Listar()
    {
        var drive = GetDrive();
        if (drive is null) return Unauthorized("Inicia sesión con Google.");

        var folderId = await EnsureAppFolderAsync(drive);

        var req = drive.Files.List();
        req.Q = $"'{folderId}' in parents and trashed=false";
        req.Fields = "files(id,name,mimeType,modifiedTime)";
        req.OrderBy = "modifiedTime desc";
        var result = await req.ExecuteAsync();

        return Ok(result.Files?.Select(f => new { f.Id, f.Name, f.MimeType, f.ModifiedTime }));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Subir(IFormFile archivo)
    {
        try
        {
            if (archivo == null || archivo.Length == 0)
                return BadRequest("Archivo vacío.");

            var drive = GetDrive();
            if (drive is null) return Unauthorized("Inicia sesión con Google.");

            var folderId = await EnsureAppFolderAsync(drive);

            // metadata + parent folder
            var meta = new DriveFile { Name = archivo.FileName, Parents = new[] { folderId } };

            using var stream = archivo.OpenReadStream();
            var create = drive.Files.Create(meta, stream, archivo.ContentType ?? "application/octet-stream");
            create.Fields = "id,name";
            var upload = await create.UploadAsync();

            if (upload.Status != UploadStatus.Completed)
                throw upload.Exception ?? new Exception("Falló la subida.");

            var created = create.ResponseBody;

            // compartir por enlace para preview anónimo
            var permiso = new DrivePermission { Type = "anyone", Role = "reader" };
            await drive.Permissions.Create(permiso, created.Id).ExecuteAsync();

            var previewUrl = $"https://drive.google.com/file/d/{created.Id}/preview";
            return Ok(new { created.Id, created.Name, previewUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno: {ex.Message}");
        }
    }

    // ===== Auth =====
    [AllowAnonymous]
    public IActionResult Login(string returnUrl = "/") =>
        Challenge(new AuthenticationProperties { RedirectUri = returnUrl }, "Google");

    [AllowAnonymous]
    public IActionResult Logout() =>
        SignOut(new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme, "Google");
}
