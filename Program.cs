using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Auth Google
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "Google";
})
.AddCookie()
.AddGoogle(options =>
{
    options.ClientId = "428769162918-6l5b97b76sqeitpb8fb1ln4158koidf8.apps.googleusercontent.com";
    options.ClientSecret = "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";

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("email");
    options.Scope.Add("profile");
    options.Scope.Add("https://www.googleapis.com/auth/drive.file");

    options.SaveTokens = true;

    // Forzar refresh_token y consentimiento
    options.Events.OnRedirectToAuthorizationEndpoint = context =>
    {
        var redirectUri = context.RedirectUri + "&access_type=offline&prompt=consent";
        context.Response.Redirect(redirectUri);
        return Task.CompletedTask;
    };
});

// Para leer tokens desde HttpContext en tus controladores
builder.Services.AddHttpContextAccessor();

// Política global: todo requiere login (puedes quitarla si prefieres)
builder.Services.AddAuthorization(opts =>
{
    opts.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

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
