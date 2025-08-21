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
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using System.Security.Claims;


public class DocumentoController : Controller
{
    private const string NombreArchivoDrive = "Gestion_Documental"; // carpeta fija en el Drive

    private readonly string _googleClientId = "428769162918-6l5b97b76sqeitpb8fb1ln4158koidf8.apps.googleusercontent.com";
    private readonly string _googleClientSecret = "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";

    //===== Helpers =====

    //obtiene un cliente de google drive
    private async Task<DriveService?> GetDriveAsync() // da null si no hay token, sino está logueado
    {
        // tokens guardados en la cookie por options.SaveTokens = true
        var auth = await HttpContext.AuthenticateAsync();
        if (!auth.Succeeded) return null;

        // leemos los tokens desde la cookie de autenticación
        var accessToken = auth.Properties.GetTokenValue("access_token");
        var refreshToken = auth.Properties.GetTokenValue("refresh_token");
        var expiracion = auth.Properties.GetTokenValue("expires_at"); // viene por SaveTokens

        if (string.IsNullOrEmpty(accessToken)) return null; // null si no hay token

        // Si NO hay refresh y el access_token ya venció, pedimos re-login (devolvemos null)
        if (string.IsNullOrEmpty(refreshToken) &&
            DateTimeOffset.TryParse(expiracion, out var expiresAt) &&
            expiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return null; // se fuerza relogin xq no hay access token activo y no hay refresh token
        }

        // es de tipo interfaz
        ICredential credenciales;

        if (!string.IsNullOrEmpty(refreshToken))  // se renueva el token cuando vence
        {
            // Flujo que sabe renovar con el refresh_token
            var flujoAutorizacion = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _googleClientId,
                    ClientSecret = _googleClientSecret
                },
                Scopes = new[] { DriveService.Scope.DriveFile }
            });

            var token = new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                //ExpiresInSeconds = 10 // simula que expira en 10 segundos


            };

            var idUsuario = User.FindFirstValue("sub")
                       ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? "user";


            var uc = new UserCredential(flujoAutorizacion, idUsuario, token); // se renueva solo si hace falta

            // Forzamos refresh inmediato si el access_token está vencido o por vencer
            await uc.RefreshTokenAsync(CancellationToken.None);

            // Persistimos el access_token renovado y su nueva expiración en la cookie
            var nuevoAccess = uc.Token.AccessToken;
            var expSeg = uc.Token.ExpiresInSeconds ?? 3600;
            var nuevaExpiracionUtc = DateTimeOffset.UtcNow.AddSeconds(expSeg);

            // // logs de prueba
            // Console.WriteLine($"[REFRESH] Nuevo access_token recibido: {nuevoAccess.Substring(0, 15)}...");
            // Console.WriteLine($"[REFRESH] Expira en: {nuevaExpiracionUtc}");

            var tokens = auth.Properties.GetTokens()?.ToList() ?? new List<AuthenticationToken>();
            tokens.RemoveAll(t => t.Name == "access_token" || t.Name == "expires_at");

            tokens.Add(new AuthenticationToken { Name = "access_token", Value = nuevoAccess });
            tokens.Add(new AuthenticationToken { Name = "refresh_token", Value = refreshToken });
            tokens.Add(new AuthenticationToken { Name = "expires_at", Value = nuevaExpiracionUtc.UtcDateTime.ToString("o") }); // ISO 8601

            auth.Properties.StoreTokens(tokens);

            // Guardamos nuevamente la cookie con los tokens actualizados
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                auth.Principal!,   // mismo usuario actual
                auth.Properties
            );

            credenciales = uc; // este es el HttpClientInitializer
        }
        else // se usa access token, sin refresh token
        {
            // Sin refresh_token, funciona mientras no expire
            credenciales = GoogleCredential.FromAccessToken(accessToken);
        }

        return new DriveService(new BaseClientService.Initializer // se crea cliente de google drive, quien hace las llamadas a la api
        {
            HttpClientInitializer = credenciales, // le damos las credenciales
            ApplicationName = "Gestion_Documental" // google lo guarda en los registros para saber quién usa la api
        });
    }

    //busca carpeta sino la crea y retorna su id 
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

    //consulta a drive y da la lista en la carpeta en formato json
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Listar()
    {
        try
        {
            var drive = await GetDriveAsync(); //cliente de google drive
            if (drive is null)
                //en el front se redirige 
                return Unauthorized("Tu sesión con Google venció. Vuelve a iniciar sesión.");

            var idCarpeta = await crearCarpeta(drive);

            var req = drive.Files.List(); //request a la api de drive para obtener lista de archivos de drive si los hay
            req.Q = $"'{idCarpeta}' in parents and trashed=false"; //trae solo los archivos de la carpeta y q no esten en la papelera
            req.Fields = "files(id,name,mimeType,modifiedTime)"; //propiedades que devuelve cada archivo
            req.OrderBy = "modifiedTime desc";//ordena por fecha de modificación descendentemente
            var resultados = await req.ExecuteAsync(); //ejecuta la petición a la API de Drive

            return Ok(resultados.Files?.Select(f => new { f.Id, f.Name, f.MimeType, f.ModifiedTimeDateTimeOffset })); //lista con 4 propiedades, la devuelve en json al navegador
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return Unauthorized("Tu sesión con Google venció. Vuelve a iniciar sesión.");
        }
    }

    //recibe el archivo desde la app y lo guarda en la carpeta de drive
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Subir(IFormFile archivo)
    {
        try
        {
            if (archivo == null || archivo.Length == 0)
            {
                return BadRequest("Archivo vacío.");
            }

            var drive = await GetDriveAsync();
            if (drive is null)
                return Unauthorized("Tu sesión con Google venció. Vuelve a iniciar sesión.");

            var idCarpeta = await crearCarpeta(drive);

            var meta = new DriveFile { Name = archivo.FileName, Parents = new[] { idCarpeta } };

            using var stream = archivo.OpenReadStream(); //acceso al contenido del archivo
            var crear = drive.Files.Create(meta, stream, archivo.ContentType ?? "application/octet-stream");
            crear.Fields = "id,name"; //propiedades que devuelve el archivo creado
            var subir = await crear.UploadAsync(); //resultado del proceso, exitoso o no

            if (subir.Status != UploadStatus.Completed)
                throw subir.Exception ?? new Exception("Falló la subida.");

            var datosArchivoCreado = crear.ResponseBody; //metadatos del archivo creado

            // compartir por enlace para preview anónimo
            var permiso = new DrivePermission { Type = "anyone", Role = "reader" };
            await drive.Permissions.Create(permiso, datosArchivoCreado.Id).ExecuteAsync();

            var previewUrl = $"https://drive.google.com/file/d/{datosArchivoCreado.Id}/preview";
            return Ok(new { datosArchivoCreado.Id, datosArchivoCreado.Name, previewUrl }); //da un json al navegador con esos datos
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return Unauthorized("Tu sesión con Google venció. Vuelve a iniciar sesión.");
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
