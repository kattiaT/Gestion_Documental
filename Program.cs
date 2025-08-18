using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// --- MVC ---
builder.Services.AddControllersWithViews();

// --- Autenticación con Google ---
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

    // Scopes necesarios
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("email");
    options.Scope.Add("profile");
    options.Scope.Add("https://www.googleapis.com/auth/drive.file");

    options.SaveTokens = true; // guarda tokens

    // 👇 Aquí forzamos access_type=offline y prompt=consent
    options.Events.OnRedirectToAuthorizationEndpoint = context =>
    {
        var redirectUri = context.RedirectUri + "&access_type=offline&prompt=consent";
        context.Response.Redirect(redirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddHttpContextAccessor();

// --- Inyección de Google Drive ---
builder.Services.AddScoped<DriveService>(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
    var accessToken = httpContext.GetTokenAsync("access_token").GetAwaiter().GetResult();

    if (string.IsNullOrEmpty(accessToken))
        throw new Exception("No hay access_token. ¿El usuario ya inició sesión con Google?");

    var credential = GoogleCredential.FromAccessToken(accessToken);

    return new DriveService(new BaseClientService.Initializer
    {
        HttpClientInitializer = credential,
        ApplicationName = "Gestion_Documental"
    });
});

var app = builder.Build();

// --- Middleware ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // primero autenticación
app.UseAuthorization();  // luego autorización

// --- Rutas ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Documento}/{action=Index}/{id?}");

app.Run();
