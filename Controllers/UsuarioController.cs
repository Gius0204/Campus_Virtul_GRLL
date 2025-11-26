/*using Campus_Virtul_GRLL.Data;
using Campus_Virtul_GRLL.Helpers;
using Campus_Virtul_GRLL.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize]
    public class UsuarioController : Controller
    {
        private readonly AppDBContext _appContext;
        private readonly ILogger<UsuarioController> _logger;

        public UsuarioController(AppDBContext appContext, ILogger<UsuarioController> logger)
        {
            _appContext = appContext;
            _logger = logger;
        }

        /// Mostrar formulario para cambiar contraseña
        [HttpGet]
        public async Task<IActionResult> CambiarContrasena()
        {
            var userId = User.GetUserId();
            var usuario = await _appContext.Usuarios.FindAsync(userId);

            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado.";
                return RedirectToAction("Index", "Login");
            }

            // Pasar información a la vista
            ViewBag.NombreUsuario = User.GetUserName();
            ViewBag.EsPrimerInicio = usuario.PrimerInicio;

            return View();
        }

        /// Procesar cambio de contraseña
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasena(
            string ContrasenaActual,
            string ContrasenaNueva,
            string ConfirmarContrasena)
        {
            try
            {
                var userId = User.GetUserId();
                var usuario = await _appContext.Usuarios
                    .Include(u => u.Rol)
                    .FirstOrDefaultAsync(u => u.IdUsuario == userId);

                if (usuario == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction("Index", "Login");
                }

                // ============================================
                // 1. VALIDACIONES
                // ============================================
                if (string.IsNullOrWhiteSpace(ContrasenaActual) ||
                    string.IsNullOrWhiteSpace(ContrasenaNueva) ||
                    string.IsNullOrWhiteSpace(ConfirmarContrasena))
                {
                    TempData["Error"] = "Todos los campos son obligatorios.";
                    return View();
                }

                // Validar que la nueva contraseña coincida con la confirmación
                if (ContrasenaNueva != ConfirmarContrasena)
                {
                    TempData["Error"] = "La nueva contraseña y la confirmación no coinciden.";
                    return View();
                }

                // Validar longitud de la nueva contraseña
                if (ContrasenaNueva.Length < 6)
                {
                    TempData["Error"] = "La nueva contraseña debe tener al menos 6 caracteres.";
                    return View();
                }

                // ============================================
                // 2. VERIFICAR CONTRASEÑA ACTUAL
                // ============================================
                bool contrasenaActualCorrecta = false;

                if (usuario.PrimerInicio && ContrasenaActual == usuario.ClaveTemporal)
                {
                    contrasenaActualCorrecta = true;
                }
                else if (!usuario.PrimerInicio && ContrasenaActual == usuario.ClavePermanente)
                {
                    contrasenaActualCorrecta = true;
                }

                if (!contrasenaActualCorrecta)
                {
                    TempData["Error"] = "La contraseña actual es incorrecta.";
                    _logger.LogWarning($"Intento fallido de cambio de contraseña - Usuario ID: {userId}");
                    return View();
                }

                // ============================================
                // 3. ACTUALIZAR CONTRASEÑA
                // ============================================
                usuario.ClavePermanente = ContrasenaNueva;
                usuario.PrimerInicio = false; // Ya no es primer inicio
                usuario.FechaActualizacion = DateOnly.FromDateTime(DateTime.Now);

                _appContext.Usuarios.Update(usuario);
                await _appContext.SaveChangesAsync();

                _logger.LogInformation($"✅ Contraseña actualizada exitosamente - Usuario ID: {userId}");

                // ============================================
                // 4. ACTUALIZAR CLAIMS (Cookie de autenticación)
                // ============================================
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
                    new Claim(ClaimTypes.Name, $"{usuario.Nombres} {usuario.Apellidos}"),
                    new Claim(ClaimTypes.Email, usuario.CorreoElectronico),
                    new Claim(ClaimTypes.Role, usuario.Rol?.NombreRol ?? "Usuario"),
                    new Claim("DNI", usuario.DNI),
                    new Claim("RolId", usuario.IdRol.ToString()),
                    new Claim("Area", usuario.Area),
                    new Claim("Nombres", usuario.Nombres),
                    new Claim("Apellidos", usuario.Apellidos),
                    new Claim("Telefono", usuario.Telefono),
                    new Claim("PrimerInicio", "False") 
                };

                var claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme
                );

                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true
                };

                // Actualizar cookie de autenticación
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    claimsPrincipal,
                    authProperties
                );

                TempData["Mensaje"] = "Contraseña actualizada exitosamente.";
                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al cambiar contraseña");
                TempData["Error"] = "Error al cambiar la contraseña. Intente nuevamente.";
                return View();
            }
        }

        /// Ver perfil del usuario
        [HttpGet]
        public async Task<IActionResult> Perfil()
        {
            var userId = User.GetUserId();
            var usuario = await _appContext.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.IdUsuario == userId);

            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado.";
                return RedirectToAction("Index", "Dashboard");
            }

            return View(usuario);
        }
    }
}*/