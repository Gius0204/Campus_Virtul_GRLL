using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Campus_Virtul_GRLL.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Campus_Virtul_GRLL.Controllers
{
    public class LoginController : Controller
    {
        private readonly SupabaseRepository _repo;
        private readonly ILogger<LoginController> _logger;

        public LoginController(SupabaseRepository repo, ILogger<LoginController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        /// Muestra el formulario de login
        [HttpGet]
        public IActionResult Index(string? rol)
        {
            // Soporte para login específico por rol desde el selector de inicio
            if (!string.IsNullOrWhiteSpace(rol))
            {
                var r = rol.Trim();
                // Normalizar a nombres de rol conocidos
                if (string.Equals(r, "Administrador", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Profesor", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Practicante", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Colaborador", StringComparison.OrdinalIgnoreCase))
                {
                    ViewBag.RolEsperado = char.ToUpper(r[0]) + r.Substring(1).ToLower();
                }
            }
            return View("Login");
        }

        /// Mostrar formulario de registro (solicitud de cuenta)
        [HttpGet]
        public IActionResult Registro()
        {
            return View();
        }

        /// Procesar solicitud de registro para aprobación de admin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(string NombreCompleto, string Correo, int? IdRol, string? Apellidos, string? DNI, string? Telefono, string? Area)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(NombreCompleto) || string.IsNullOrWhiteSpace(Correo))
                {
                    TempData["Error"] = "Complete nombre y correo.";
                    return View();
                }
                Correo = Correo.Trim().ToLower();
                if (!Correo.Contains("@") || !Correo.Contains("."))
                {
                    TempData["Error"] = "Correo inválido.";
                    return View();
                }

                // Sin contraseña en registro: se generará una temporal al aprobar

                // Si ya existe usuario, informar
                var existente = await _repo.GetUserByEmailAsync(Correo);
                if (existente != null)
                {
                    TempData["Error"] = "Este correo ya está registrado.";
                    return View();
                }

                // Crear usuario inactivo en 'usuarios' con password hash, para que al aprobar solo activemos
                try
                {
                    // Resolver rol
                    var roles = await _repo.GetRolesAsync();
                    Guid rolId = roles.FirstOrDefault(r =>
                        (IdRol == 1 && r.nombre.Equals("Profesor", StringComparison.OrdinalIgnoreCase)) ||
                        (IdRol == 2 && r.nombre.Equals("Practicante", StringComparison.OrdinalIgnoreCase))
                    ).id;
                    if (rolId == Guid.Empty)
                    {
                        var rolUsuario = roles.FirstOrDefault(r => r.nombre.Equals("Usuario", StringComparison.OrdinalIgnoreCase));
                        rolId = rolUsuario.id != Guid.Empty ? rolUsuario.id : roles.First().id;
                    }

                    await _repo.CreateUserAsync(NombreCompleto.Trim(), Correo, rolId, activo: false, plainPassword: null, apellidos: Apellidos, dni: DNI, telefono: Telefono);
                }
                catch
                {
                    // Si falla creación de usuario, continuamos con solicitud igualmente
                }

                // Crear solicitud en tabla public.solicitudes (si existe). Si no, solo quedará usuario inactivo esperando aprobación.
                // Intentar registrar la solicitud vinculada al usuario
                try
                {
                    using var conn = new Npgsql.NpgsqlConnection(new Npgsql.NpgsqlConnectionStringBuilder
                    {
                        Host = Environment.GetEnvironmentVariable("SUPABASE_DB_HOST"),
                        Port = int.TryParse(Environment.GetEnvironmentVariable("SUPABASE_DB_PORT"), out var p) ? p : 5432,
                        Database = Environment.GetEnvironmentVariable("SUPABASE_DB_NAME") ?? "postgres",
                        Username = Environment.GetEnvironmentVariable("SUPABASE_DB_USER") ?? "postgres",
                        Password = Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD"),
                        SslMode = Npgsql.SslMode.Require,
                    }.ConnectionString);
                    await conn.OpenAsync();
                    using var cmd = new Npgsql.NpgsqlCommand("insert into public.solicitudes (id, usuario_id, tipo, detalle, estado, creada_en) select gen_random_uuid(), u.id, 'registro', null, 'pendiente', now() from public.usuarios u where lower(u.correo)=lower(@c) on conflict do nothing", conn);
                    cmd.Parameters.AddWithValue("c", Correo);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch
                {
                    // Si no existe la tabla, continuamos solo con el usuario inactivo
                }

                TempData["Mensaje"] = "Solicitud enviada. Un administrador revisará su cuenta.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar solicitud");
                TempData["Error"] = "Error al enviar solicitud.";
                return View();
            }
        }

        /// Procesa el login del usuario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string DNI, string Contrasena, string? RolEsperado)
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

                // Nota: ahora usamos correo electrónico en lugar de DNI
                if (!DNI.Contains("@") || !DNI.Contains("."))
                {
                    TempData["Error"] = "Ingrese su correo electrónico válido en el campo DNI.";
                    return View("Login");
                }

                _logger.LogInformation($"Intento de login para correo: {DNI}");

                // ============================================
                // 2. BUSCAR USUARIO EN LA BASE DE DATOS
                // ============================================
                var usuarioDb = await _repo.GetUserByEmailAsync(DNI.ToLower());

                // Validar que el usuario existe
                if (usuarioDb == null)
                {
                    _logger.LogWarning($"Usuario no encontrado - correo: {DNI}");
                    TempData["Error"] = "El correo ingresado no está registrado en el sistema.";
                    return View("Login");
                }

                // Validar que el usuario esté activo (Estado = 1 o true)
                if (!usuarioDb.Value.activo)
                {
                    _logger.LogWarning($"Usuario inactivo - correo: {DNI}, ID: {usuarioDb.Value.id}");
                    TempData["Error"] = "Su cuenta está inactiva. Contacte al administrador.";
                    return View("Login");
                }

                // Si se espera un rol específico desde el selector, validar coincidencia
                if (!string.IsNullOrWhiteSpace(RolEsperado))
                {
                    var esperado = RolEsperado.Trim();
                    if (!string.Equals(usuarioDb.Value.rolNombre, esperado, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning($"Rol incorrecto en acceso específico. Esperado: {esperado}, Actual: {usuarioDb.Value.rolNombre}");
                        TempData["Error"] = $"Este acceso es solo para {esperado}.";
                        ViewBag.RolEsperado = esperado;
                        return View("Login");
                    }
                }

                // ============================================
                // 3. VERIFICAR CONTRASEÑA
                // ============================================
                var contrasenaCorrecta = usuarioDb.Value.passwordHash != null && PasswordHelper.Verify(Contrasena, usuarioDb.Value.passwordHash);

                if (!contrasenaCorrecta)
                {
                    _logger.LogWarning($"Contraseña incorrecta - correo: {DNI}");
                    TempData["Error"] = "Correo o contraseña incorrectos.";
                    return View("Login");
                }

                // ============================================
                // 4. CREAR CLAIMS (Información del usuario)
                // ============================================
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, usuarioDb.Value.id.ToString()),
                    new Claim(ClaimTypes.Name, usuarioDb.Value.nombres),
                    new Claim(ClaimTypes.Email, usuarioDb.Value.correo ?? ""),
                    new Claim(ClaimTypes.Role, usuarioDb.Value.rolNombre),
                    new Claim("RolId", usuarioDb.Value.rolId.ToString())
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

                _logger.LogInformation($"✅ Usuario autenticado exitosamente - ID: {usuarioDb.Value.id}, Nombre: {usuarioDb.Value.nombres}");

                // ============================================
                // 6. REDIRECCIONAR SEGÚN EL ESTADO DEL USUARIO
                // ============================================
                // Enforzar cambio de contraseña si está marcado
                var requireChange = await _repo.GetRequirePasswordChangeAsync(usuarioDb.Value.id);
                if (requireChange)
                {
                    TempData["MensajeModal"] = "Debes cambiar tu contraseña temporal por una nueva.";
                    TempData["EsPrimerInicio"] = true;
                    TempData["NombreUsuario"] = usuarioDb.Value.nombres;
                    return RedirectToAction("CambiarContrasena");
                }

                // Login exitoso normal
                _logger.LogInformation($"Redirigiendo al Dashboard - Usuario ID: {usuarioDb.Value.id}");
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

        /// Mostrar formulario de cambio de contraseña (PRG)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CambiarContrasena()
        {
            try
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                Guid.TryParse(userIdStr, out var userId);

                var requiereCambio = userId != Guid.Empty && await _repo.GetRequirePasswordChangeAsync(userId);

                ViewBag.EsPrimerInicio = TempData.ContainsKey("EsPrimerInicio") ? true : requiereCambio;
                ViewBag.NombreUsuario = TempData.ContainsKey("NombreUsuario") ? (TempData["NombreUsuario"]?.ToString() ?? User.Identity?.Name ?? "Usuario") : (User.Identity?.Name ?? "Usuario");

                return View("CambiarContrasena");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al preparar vista de cambio de contraseña");
                TempData["Error"] = "No se pudo cargar el formulario de cambio de contraseña.";
                return RedirectToAction("Index");
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
                var usuarioDb = await _repo.GetUserByEmailAsync(CorreoElectronico);

                // Si el correo NO existe en la base de datos
                if (usuarioDb == null)
                {
                    _logger.LogWarning($"❌ Correo no encontrado en recuperación: {CorreoElectronico}");
                    TempData["ErrorModal"] = "El correo electrónico no se encuentra registrado en el sistema.";
                    return View("Login");
                }

                // Validar que el usuario esté activo
                if (!usuarioDb.Value.activo)
                {
                    _logger.LogWarning($"Usuario inactivo intenta recuperar contraseña: {CorreoElectronico}");
                    TempData["ErrorModal"] = "Su cuenta está inactiva. Contacte al administrador.";
                    return View("Login");
                }

                // ============================================
                // 3. Generar contraseña temporal y forzar cambio
                // ============================================
                var tempPassword = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                    .Replace("=","")
                    .Replace("+","")
                    .Replace("/","")
                    .Substring(0, 12);
                await _repo.UpdateUserPasswordAsync(usuarioDb.Value.id, tempPassword);
                await _repo.SetRequirePasswordChangeAsync(usuarioDb.Value.id, true);

                // ============================================
                // 4. ENVIAR CORREO DE RECUPERACIÓN
                // ============================================
                // Enviar correo con la contraseña temporal
                var emailSvc = HttpContext.RequestServices.GetService<Campus_Virtul_GRLL.Services.EmailService>();
                if (emailSvc != null && emailSvc.IsConfigured)
                {
                    var body = $"<p>Has solicitado recuperar tu contraseña.</p><p>Tu contraseña temporal es: <b>{tempPassword}</b></p><p>Inicia sesión y se te pedirá cambiarla por una nueva.</p>";
                    await emailSvc.SendAsync(usuarioDb.Value.correo ?? CorreoElectronico, "Recuperación de contraseña", body);
                }

                TempData["MensajeModal"] = "Se envió una contraseña temporal a tu correo. Inicia sesión y cámbiala.";
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
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
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
                    return View("CambiarContrasena");
                }

                // Validar que la nueva contraseña coincida con la confirmación
                if (ContrasenaNueva.Trim() != ConfirmarContrasena.Trim())
                {
                    TempData["Error"] = "La nueva contraseña y la confirmación no coinciden.";
                    return View("CambiarContrasena");
                }

                // Validar longitud de la nueva contraseña
                if (ContrasenaNueva.Trim().Length < 6)
                {
                    TempData["Error"] = "La nueva contraseña debe tener al menos 6 caracteres.";
                    return View("CambiarContrasena");
                }

                // ============================================
                // 2. VERIFICAR CONTRASEÑA ACTUAL
                // ============================================
                var usuarioDb = await _repo.GetUserByEmailAsync(User.FindFirst(ClaimTypes.Email)?.Value ?? "");
                var contrasenaActualCorrecta = usuarioDb != null && usuarioDb.Value.passwordHash != null && PasswordHelper.Verify(ContrasenaActual.Trim(), usuarioDb.Value.passwordHash);

                if (!contrasenaActualCorrecta)
                {
                    TempData["Error"] = "La contraseña actual es incorrecta.";
                    _logger.LogWarning($"Intento fallido de cambio de contraseña - Usuario ID: {userId}");
                    return View("CambiarContrasena");
                }

                // ============================================
                // 3. ACTUALIZAR CONTRASEÑA
                // ============================================
                await _repo.UpdateUserPasswordAsync(userId, ContrasenaNueva.Trim());
                await _repo.SetRequirePasswordChangeAsync(userId, false);
                _logger.LogInformation($"✅ Contraseña actualizada exitosamente - Usuario ID: {userId}");

                // ============================================
                // 4. ACTUALIZAR CLAIMS (Cookie de autenticación)
                // ============================================
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Name, usuarioDb.HasValue ? usuarioDb.Value.nombres : "Usuario"),
                    new Claim(ClaimTypes.Email, usuarioDb.HasValue ? (usuarioDb.Value.correo ?? "") : ""),
                    new Claim(ClaimTypes.Role, usuarioDb.HasValue ? usuarioDb.Value.rolNombre : "Usuario"),
                    new Claim("RolId", (usuarioDb.HasValue ? usuarioDb.Value.rolId : Guid.Empty).ToString())
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

                TempData["ToastSuccess"] = "Contraseña actualizada exitosamente.";
                return RedirectToAction("Index", "Dashboard");
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

                return RedirectToAction("Index", "Home");
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