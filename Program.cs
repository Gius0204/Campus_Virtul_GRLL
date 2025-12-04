using Microsoft.AspNetCore.Authentication.Cookies;
using Campus_Virtul_GRLL.Services;
using Npgsql;
using Campus_Virtul_GRLL.Helpers;
using DotEnv.Core;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1. REGISTRAR ALMACÉN EN MEMORIA (Sin BD)
// ============================================
builder.Services.AddSingleton<InMemoryDataStore>(); // Se dejará mientras migras lógica a Supabase
builder.Services.AddSingleton<SupabaseRepository>();

// ============================================
// 2. CONFIGURAR AUTENTICACI�N CON COOKIES
// ============================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index"; // P�gina de login
        options.LogoutPath = "/Login/Logout"; // Cerrar sesi�n
        options.AccessDeniedPath = "/Login/AccesoDenegado"; // Sin permisos
        options.ExpireTimeSpan = TimeSpan.FromHours(8); // Cookie válida por 8 horas
        options.SlidingExpiration = true; // Renovar autom�ticamente
        options.Cookie.Name = "CampusVirtualAuth";
        options.Cookie.HttpOnly = true; // Protecci�n contra XSS
        options.Cookie.IsEssential = true; // Cookie esencial
    });

// ============================================
// 3. AGREGAR CONTROLADORES Y VISTAS
// ============================================
builder.Services.AddControllersWithViews();

// Cargar variables desde .env si existe
try { new EnvLoader().Load(); } catch { /* Ignorar si no existe */ }

// Registrar StorageService y opciones
var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? "";
var supabaseAnon = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? "";
var supabaseService = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") ?? ""; // Mantener fuera de código fuente
var storageOptions = new StorageOptions(supabaseUrl, supabaseAnon, supabaseService);
builder.Services.AddSingleton(storageOptions);
builder.Services.AddHttpClient<StorageService>();

var app = builder.Build();

// ============================================
// Prueba de conexión a Supabase Postgres
// ============================================
try
{
    var dbHost = Environment.GetEnvironmentVariable("SUPABASE_DB_HOST");
    var dbName = Environment.GetEnvironmentVariable("SUPABASE_DB_NAME") ?? "postgres";
    var dbUser = Environment.GetEnvironmentVariable("SUPABASE_DB_USER") ?? "postgres";
    var dbPassword = Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD");

    if (!string.IsNullOrWhiteSpace(dbHost) && !string.IsNullOrWhiteSpace(dbPassword))
    {
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = dbHost,
            Database = dbName,
            Username = dbUser,
            Password = dbPassword,
            SslMode = SslMode.Require,
        };

        using var conn = new NpgsqlConnection(csb.ConnectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand("select 1", conn);
        var result = cmd.ExecuteScalar();
        app.Logger.LogInformation("Conexión a Supabase Postgres verificada (select 1 -> {Result}).", result);
        conn.Close();
    }
    else
    {
        app.Logger.LogWarning("SUPABASE_DB_* no configuradas, se omite prueba de conexión a Postgres.");
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Fallo la prueba de conexión a Supabase Postgres.");
}

// ============================================
// 4. CONFIGURAR PIPELINE HTTP
// ============================================
if (!app.Environment.IsDevelopment())
{
    //app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Inicializar repositorio Supabase (crear usuario demo si falta)
try
{
    using var scope = app.Services.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<SupabaseRepository>();
    repo.InitializeAsync().GetAwaiter().GetResult();
    var rolesCount = repo.GetRolesCountAsync().GetAwaiter().GetResult();
    app.Logger.LogInformation("Roles en Supabase: {Count}", rolesCount);

    // Debug opcional: imprimir hash de 'admin123' si se define variable OUTPUT_ADMIN123_HASH=1
    var hashTmp = PasswordHelper.Hash("admin123");
    app.Logger.LogInformation("Hash bcrypt para 'admin123': {Hash}", hashTmp);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Error inicializando repositorio Supabase.");
}

// ============================================
// 5. CONFIGURAR RUTAS
// ============================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();
