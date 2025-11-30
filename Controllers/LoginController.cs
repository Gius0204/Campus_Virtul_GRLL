using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Campus_Virtul_GRLL.Controllers
{
    public class LoginController : Controller
    {
        private readonly InMemoryDataStore _store;
        private readonly ILogger<LoginController> _logger;

        public LoginController(InMemoryDataStore store, ILogger<LoginController> logger)
        {
            _store = store;
            _logger = logger;
        }

        /// Muestra el formulario de login
        [HttpGet]
        public IActionResult Index()
        {
            return View("Login");
        }

        /// Procesa el login del usuario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string DNI, string Contrasena)
        {
            try
            {
                // ============================================
                // 1. VALIDACIONES INICIALES
                // ============================================
                if (string.IsNullOrWhiteSpace(DNI) || string.IsNullOrWhiteSpace(Contrasena))
                {
                    TempData["Error"] = "Por favor, complete todos los campos.";
                    return View("Login");
                }

                DNI = DNI.Trim();
                Contrasena = Contrasena.Trim();

                // Validar formato de DNI
                if (DNI.Length != 8 || !DNI.All(char.IsDigit))
                {
                    TempData["Error"] = "El DNI debe contener exactamente 8 dígitos.";
                    return View("Login");
                }

                _logger.LogInformation($"Intento de login para DNI: {DNI}");

                // ============================================
                // 2. BUSCAR USUARIO EN LA BASE DE DATOS
                // ============================================
                var usuario = _store.Usuarios.Values.FirstOrDefault(u => u.DNI == DNI);

                // Validar que el usuario existe
                if (usuario == null)
                {
                    _logger.LogWarning($"Usuario no encontrado - DNI: {DNI}");
                    TempData["Error"] = "El DNI ingresado no está registrado en el sistema.";
                    return View("Login");
                }

                // Validar que el usuario esté activo (Estado = 1 o true)
                if (!usuario.Estado)
                {
                    _logger.LogWarning($"Usuario inactivo - DNI: {DNI}, ID: {usuario.IdUsuario}");
                    TempData["Error"] = "Su cuenta está inactiva. Contacte al administrador.";
                    return View("Login");
                }

                // ============================================
                // 3. VERIFICAR CONTRASEÑA
                // ============================================
                bool contrasenaCorrecta = false;

                // Si es primer inicio, verificar con clave temporal
                if (usuario.PrimerInicio && Contrasena == usuario.ClaveTemporal)
                {
                    contrasenaCorrecta = true;
                    _logger.LogInformation($"Login con clave temporal - Usuario ID: {usuario.IdUsuario}");
                }
                // Si no es primer inicio, verificar con clave permanente
                else if (!usuario.PrimerInicio && Contrasena == usuario.ClavePermanente)
                {
                    contrasenaCorrecta = true;
                    _logger.LogInformation($"Login con clave permanente - Usuario ID: {usuario.IdUsuario}");
                }

                if (!contrasenaCorrecta)
                {
                    _logger.LogWarning($"Contraseña incorrecta - DNI: {DNI}");
                    TempData["Error"] = "DNI o contraseña incorrectos.";
                    return View("Login");
                }

                // ============================================
                // 4. CREAR CLAIMS (Información del usuario)
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
                    new Claim("PrimerInicio", usuario.PrimerInicio.ToString())
                };

                // ============================================
                // 5. CREAR IDENTITY Y AUTENTICAR
                // ============================================
                var claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme
                );

                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true,
                    IssuedUtc = DateTimeOffset.UtcNow
                };

                // Crear Cookie de autenticación
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    claimsPrincipal,
                    authProperties
                );

                _logger.LogInformation($"✅ Usuario autenticado exitosamente - ID: {usuario.IdUsuario}, Nombre: {usuario.Nombres} {usuario.Apellidos}");

                // ============================================
                // 6. REDIRECCIONAR SEGÚN EL ESTADO DEL USUARIO
                // ============================================

                // Si es primer inicio, debe cambiar su contraseña
                if (usuario.PrimerInicio)
                {
                    TempData["Mensaje"] = "Bienvenido. Por seguridad, debe cambiar su contraseña temporal.";
                    _logger.LogInformation($"Redirigiendo a cambio de contraseña - Usuario ID: {usuario.IdUsuario}");

                    ViewBag.NombreUsuario = $"{usuario.Nombres} {usuario.Apellidos}";
                    ViewBag.EsPrimerInicio = usuario.PrimerInicio;
                    return View("CambiarContrasena");
                }

                // Login exitoso normal
                //TempData["Mensaje"] = $"¡Bienvenido {usuario.Nombres}!";
                _logger.LogInformation($"Redirigiendo al Dashboard - Usuario ID: {usuario.IdUsuario}");
                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error crítico durante el proceso de login");
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = $"Error inesperado: {errorMessage}";
                return View("Login");
            }
        }

        /// Procesar solicitud de recuperación de contraseña
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecuperacionContrasena(string CorreoElectronico)
        {
            try
            {
                // ============================================
                // 1. VALIDACIONES INICIALES
                // ============================================
                if (string.IsNullOrWhiteSpace(CorreoElectronico))
                {
                    TempData["ErrorModal"] = "Por favor, ingrese su correo electrónico.";
                    return View("Login");
                }

                CorreoElectronico = CorreoElectronico.Trim().ToLower();

                // Validar formato de correo
                if (!CorreoElectronico.Contains("@") || !CorreoElectronico.Contains("."))
                {
                    TempData["ErrorModal"] = "Por favor, ingrese un correo electrónico válido.";
                    return View("Login");
                }

                _logger.LogInformation($"Solicitud de recuperación de contraseña para: {CorreoElectronico}");

                // ============================================
                // 2. BUSCAR USUARIO POR CORREO
                // ============================================
                var usuario = _store.Usuarios.Values.FirstOrDefault(u => u.CorreoElectronico.ToLower() == CorreoElectronico);

                // Si el correo NO existe en la base de datos
                if (usuario == null)
                {
                    _logger.LogWarning($"❌ Correo no encontrado en recuperación: {CorreoElectronico}");
                    TempData["ErrorModal"] = "El correo electrónico no se encuentra registrado en el sistema.";
                    return View("Login");
                }

                // Validar que el usuario esté activo
                if (!usuario.Estado)
                {
                    _logger.LogWarning($"Usuario inactivo intenta recuperar contraseña: {CorreoElectronico}");
                    TempData["ErrorModal"] = "Su cuenta está inactiva. Contacte al administrador.";
                    return View("Login");
                }

                // ============================================
                // 3. GENERAR TOKEN DE RECUPERACIÓN
                // ============================================
                var token = Guid.NewGuid().ToString();
                var fechaExpiracion = DateTime.Now.AddMinutes(15);

                usuario.TokenRecuperacion = token;
                usuario.FechaExpiracionToken = fechaExpiracion;
                usuario.FechaActualizacion = DateOnly.FromDateTime(DateTime.Now);

                // Persistencia en memoria (ya almacenado en _store)

                // ============================================
                // 4. ENVIAR CORREO DE RECUPERACIÓN
                // ============================================
                var urlRecuperacion = Url.Action(
                    "RestablecerContrasena",
                    "Login",
                    new { token = token },
                    Request.Scheme
                );

                _logger.LogInformation($"✅ Token de recuperación generado para usuario ID: {usuario.IdUsuario}");
                _logger.LogInformation($"URL de recuperación: {urlRecuperacion}");

                // TODO: Aquí deberías integrar un servicio de correo (SendGrid, SMTP, etc.)
                // Por ahora solo mostramos el mensaje de éxito

                TempData["MensajeModal"] = "Se ha enviado un correo electrónico con las instrucciones para el cambio de tu contraseña. Por favor verifica la información enviada.";
                return View("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error crítico en recuperación de contraseña");
                TempData["ErrorModal"] = "Error inesperado. Intente nuevamente más tarde.";
                return View("Login");
            }
        }

        /// Procesar cambio de contraseña 
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasena(
            string ContrasenaActual,
            string ContrasenaNueva,
            string ConfirmarContrasena)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var usuario = _store.Usuarios.Values.FirstOrDefault(u => u.IdUsuario == userId);

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
                    ViewBag.NombreUsuario = $"{usuario.Nombres} {usuario.Apellidos}";
                    ViewBag.EsPrimerInicio = usuario.PrimerInicio;
                    return View("CambiarContrasena");
                }

                // Validar que la nueva contraseña coincida con la confirmación
                if (ContrasenaNueva.Trim() != ConfirmarContrasena.Trim())
                {
                    TempData["Error"] = "La nueva contraseña y la confirmación no coinciden.";
                    ViewBag.NombreUsuario = $"{usuario.Nombres} {usuario.Apellidos}";
                    ViewBag.EsPrimerInicio = usuario.PrimerInicio;
                    return View("CambiarContrasena");
                }

                // Validar longitud de la nueva contraseña
                if (ContrasenaNueva.Trim().Length < 6)
                {
                    TempData["Error"] = "La nueva contraseña debe tener al menos 6 caracteres.";
                    ViewBag.NombreUsuario = $"{usuario.Nombres} {usuario.Apellidos}";
                    ViewBag.EsPrimerInicio = usuario.PrimerInicio;
                    return View("CambiarContrasena");
                }

                // ============================================
                // 2. VERIFICAR CONTRASEÑA ACTUAL
                // ============================================
                bool contrasenaActualCorrecta = false;

                if (usuario.PrimerInicio && ContrasenaActual.Trim() == usuario.ClaveTemporal)
                {
                    contrasenaActualCorrecta = true;
                }
                else if (!usuario.PrimerInicio && ContrasenaActual.Trim() == usuario.ClavePermanente)
                {
                    contrasenaActualCorrecta = true;
                }

                if (!contrasenaActualCorrecta)
                {
                    TempData["Error"] = "La contraseña actual es incorrecta.";
                    _logger.LogWarning($"Intento fallido de cambio de contraseña - Usuario ID: {userId}");
                    ViewBag.NombreUsuario = $"{usuario.Nombres} {usuario.Apellidos}";
                    ViewBag.EsPrimerInicio = usuario.PrimerInicio;
                    return View("CambiarContrasena");
                }

                // ============================================
                // 3. ACTUALIZAR CONTRASEÑA
                // ============================================
                usuario.ClavePermanente = ContrasenaNueva.Trim();
                usuario.PrimerInicio = false;
                usuario.FechaActualizacion = DateOnly.FromDateTime(DateTime.Now);

                // Persistencia en memoria

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
                return RedirectToAction("Index", "Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al cambiar contraseña");
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = $"Error inesperado: {errorMessage}";
                return View("CambiarContrasena");
            }
        }

        /// Cerrar sesión del usuario
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userName = User.Identity?.Name ?? "Usuario desconocido";

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                _logger.LogInformation($"✅ Usuario {userName} cerró sesión exitosamente");

                return RedirectToAction("Index", "Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al cerrar sesión");
                TempData["Error"] = "Error al cerrar sesión.";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        /// Página cuando el usuario no tiene permisos
        [HttpGet]
        public IActionResult AccesoDenegado()
        {
            ViewBag.Mensaje = "No tiene permisos para acceder a esta página.";
            return View();
        }
    }
}