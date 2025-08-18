using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Docs.v1;
using Google.Apis.Drive.v3;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Autenticación + OAuth Google
builder.Services
  .AddAuthentication(o =>
  {
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
  })
  .AddCookie()
  .AddGoogleOpenIdConnect(options =>
  {
    options.ClientId = "428769162918-6l5b97b76sqeitpb8fb1ln4158koidf8.apps.googleusercontent.com"; // del JSON
    options.ClientSecret = "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"; // del JSON}
    options.CallbackPath = "/signin-google";   // explícito
    options.Scope.Add(DriveService.Scope.Drive);
    options.Scope.Add(DocsService.Scope.Documents);
  });

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<DocsService>();
builder.Services.AddScoped<DriveService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Documento}/{action=Index}/{id?}"); // Asegúrate de que esta línea esté presente
app.Run();
