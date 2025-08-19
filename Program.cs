using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Auth Google
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies"; //como se recuerdad al usuario(cookie)
    options.DefaultChallengeScheme = "Google"; //proveedor por defecto cuando se necesita login
})
.AddCookie()
.AddGoogle(options =>
{
    options.ClientId = "428769162918-6l5b97b76sqeitpb8fb1ln4158koidf8.apps.googleusercontent.com";
    options.ClientSecret = "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";

    // Scopes: q permisos le pedimos al usuario en google
    options.Scope.Clear(); //quita todos los scopes q el middleware de google añade por defecto
    options.Scope.Add("openid"); //identidad
    options.Scope.Add("email"); //correo
    options.Scope.Add("profile"); //nombre, foto
    options.Scope.Add("https://www.googleapis.com/auth/drive.file"); //permisos para acceder a los archivos del usuario en Drive de la app

    options.SaveTokens = true; // guarda access_token / id_token en la cookie

    // Forzar refresh_token y consentimiento
    options.Events.OnRedirectToAuthorizationEndpoint = context =>
    {
        var redirectUri = context.RedirectUri + "&access_type=offline&prompt=consent";
        context.Response.Redirect(redirectUri);
        return Task.CompletedTask;
    };
});

// Para leer tokens desde HttpContext en los controladores
builder.Services.AddHttpContextAccessor();

// Política global: todo requiere login
builder.Services.AddAuthorization(opts =>
{
    opts.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
}); //sino tiene authorize ni allowAnonymous? todas las rutas requieren login

var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();   // middleware que obliga a usar HTTPS
app.UseStaticFiles();        // middleware que sirve archivos estáticos (css, js, imágenes)

app.UseRouting();            // decide a qué controlador/action mandar la petición

app.UseAuthentication();     // middleware de autenticación (verifica si hay un usuario logueado)
app.UseAuthorization();      // middleware de autorización (verifica si tiene permisos/roles)

// Ruta por defecto
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Documento}/{action=Index}/{id?}");

app.Run();
