using Campus_Virtul_GRLL.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1. CONFIGURAR BASE DE DATOS
// ============================================
builder.Services.AddDbContext<AppDBContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConexionSQL"));
});

// ============================================
// 2. CONFIGURAR AUTENTICACIÓN CON COOKIES
// ============================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index"; // Página de login
        options.LogoutPath = "/Login/Logout"; // Cerrar sesión
        options.AccessDeniedPath = "/Login/AccesoDenegado"; // Sin permisos
        options.ExpireTimeSpan = TimeSpan.FromSeconds(1); // Cookie válida por 8 horas
        options.SlidingExpiration = true; // Renovar automáticamente
        options.Cookie.Name = "CampusVirtualAuth";
        options.Cookie.HttpOnly = true; // Protección contra XSS
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
    pattern: "{controller=Solicitud}/{action=Solicitud}/{id?}");

app.Run();
