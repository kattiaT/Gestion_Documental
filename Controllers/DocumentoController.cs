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
    private const string NombreArchivoDrive = "Gestion_Documental"; // carpeta fija en el Drive

    // ===== Helpers =====

    //obtiene un cliente de google drive
    private DriveService? GetDrive() //da null si no hay token, sino esta logueado
    {
        var accessToken = HttpContext.GetTokenAsync("Google", "access_token")
                                     .GetAwaiter().GetResult();//se guardan los token

        if (string.IsNullOrEmpty(accessToken)) return null; //null si no hay token

        var credenciales = GoogleCredential.FromAccessToken(accessToken);
        return new DriveService(new BaseClientService.Initializer //se crea cliente de google drive, quien hace las llamadas a la api
        {
            HttpClientInitializer = credenciales, //le damos las credenciales
            ApplicationName = "Gestion_Documental" //google lo guarda en los registros para saber quien usa la api
        });
    }

    //
    private async Task<string> crearCarpeta(DriveService drive)
    {
        // Buscar carpeta por nombre
        var list = drive.Files.List(); //lista de archivos
        list.Q = $"mimeType='application/vnd.google-apps.folder' and name='{NombreArchivoDrive}' and trashed=false"; //filtramos
        list.Fields = "files(id,name)"; //propiedades q devuelve cada archivo
        var respuesta = await list.ExecuteAsync(); //ejecuta
        if (respuesta.Files?.Count > 0) return respuesta.Files[0].Id; //devuelve el identificador único de Drive de esa carpeta, sino es null. si es null pasa a crear la carpeta

        // Crear si no existe
        var meta = new DriveFile { Name = NombreArchivoDrive, MimeType = "application/vnd.google-apps.folder" };
        var crear = drive.Files.Create(meta);//se crea archivo nuevo con los metadatos indicados
        crear.Fields = "id";//solo da el id de la carpeta creada
        var carpeta = await crear.ExecuteAsync();//ejecuta
        return carpeta.Id;
    }

    // ===== UI =====
    [HttpGet]
    [Authorize] //forzar login al entrar
    public IActionResult Index() => View();

    // ===== API =====
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Listar()
    {
        var drive = GetDrive(); //cliente de google drive
        if (drive is null) return Unauthorized("Inicia sesión con Google.");

        var idCarpeta = await crearCarpeta(drive);

        var req = drive.Files.List(); //request a la api de drive para obtener lista de archivos de drive si los hay
        req.Q = $"'{idCarpeta}' in parents and trashed=false";
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

            var folderId = await crearCarpeta(drive);

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
