using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3; // Importa la librería de Google Drive, nos da constantes como Scope.DriveFile
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies; // Importante para configurar las cookies de autenticación
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Autenticación con Google
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies"; // Se recuerda al usuario con cookies
    options.DefaultChallengeScheme = "Google"; // Proveedor por defecto: Google
})
.AddCookie(o =>
{
    // Configuración de la cookie de autenticación
    o.ExpireTimeSpan = TimeSpan.FromDays(14);   // La cookie dura 14 días antes de expirar
    o.SlidingExpiration = true;                 // Si el usuario usa la app, la cookie se renueva automáticamente
})
.AddGoogle(options =>
{
    options.ClientId = "428769162918-6l5b97b76sqeitpb8fb1ln4158koidf8.apps.googleusercontent.com";
    options.ClientSecret = "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";

    // ===== Scopes: permisos que pedimos al usuario en Google =====
    options.Scope.Clear(); // Borramos los scopes por defecto del middleware
    options.Scope.Add("openid"); // Permite identificar la cuenta
    options.Scope.Add("email");  // Permite obtener el correo
    options.Scope.Add("profile");// Permite obtener nombre y foto
    options.Scope.Add(DriveService.Scope.DriveFile); // Permiso para gestionar archivos creados por la app en Drive

    // ===== Tokens =====
    options.SaveTokens = true; // Guarda access_token, id_token y refresh_token en la cookie

    options.Events.OnRedirectToAuthorizationEndpoint = async context =>
    {
        // ¿Ya tenemos refresh_token guardado en la cookie?
        var auth = await context.HttpContext.AuthenticateAsync();
        var yaTieneRefresh = auth.Succeeded &&
                            !string.IsNullOrEmpty(auth.Properties.GetTokenValue("refresh_token"));

        // Siempre pedimos offline (para mantener el refresh),
        // pero sólo forzamos consent si aún NO tenemos refresh_token.
        var extra = yaTieneRefresh
            ? "&access_type=offline" //Google inicia sesión silenciosa, devuelve access_token, no pide consentimiento
            : "&access_type=offline&prompt=consent";

        context.Response.Redirect(context.RedirectUri + extra);
    };


});

// Permite leer tokens desde HttpContext en los controladores
builder.Services.AddHttpContextAccessor();

// Política global: todas las rutas requieren login por defecto
builder.Services.AddAuthorization(opts =>
{
    opts.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
}); // Si una acción no tiene [Authorize] ni [AllowAnonymous], se aplica esta política

var app = builder.Build();

// Middleware de error en producción
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();   // Obliga a usar HTTPS
app.UseStaticFiles();        // Sirve archivos estáticos (css, js, imágenes)

app.UseRouting();            // Decide a qué controlador/action mandar la petición

app.UseAuthentication();     // Verifica si hay un usuario logueado (con cookies/tokens)
app.UseAuthorization();      // Verifica si tiene permisos para acceder a la ruta

// Ruta por defecto
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Documento}/{action=Index}/{id?}");

app.Run();
