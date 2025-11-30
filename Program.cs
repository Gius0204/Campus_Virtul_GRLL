using Microsoft.AspNetCore.Authentication.Cookies;
using Campus_Virtul_GRLL.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1. REGISTRAR ALMACÉN EN MEMORIA (Sin BD)
// ============================================
builder.Services.AddSingleton<InMemoryDataStore>();

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

var app = builder.Build();

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

// ============================================
// 5. CONFIGURAR RUTAS
// ============================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();
