using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios al contenedor
builder.Services.AddControllersWithViews();

// Cargar las credenciales y configurar los servicios aquí
GoogleCredential credenciales;

// Cargar las credenciales desde el archivo JSON
using (var stream = new FileStream("client_secret_428769162918-6l5b97b76sqeitpb8fb1ln4158koidf8.apps.googleusercontent.com.json", FileMode.Open, FileAccess.Read))
{
    credenciales = GoogleCredential.FromStream(stream)
        .CreateScoped(DriveService.Scope.Drive);
}

// Configurar el servicio de Google Docs
builder.Services.AddSingleton<DocsService>(sp =>
{
    return new DocsService(new BaseClientService.Initializer()
    {
        HttpClientInitializer = credenciales,
        ApplicationName = "Gestion-de-Documentos",
    });
});

// Configurar el servicio de Google Drive
builder.Services.AddSingleton<DriveService>(sp =>
{
    return new DriveService(new BaseClientService.Initializer()
    {
        HttpClientInitializer = credenciales,
        ApplicationName = "Gestion-de-Documentos",
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Documento}/{action=Index}/{id?}");

app.Run();
