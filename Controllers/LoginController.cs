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
        public IActionResult Index()
        {
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
        public async Task<IActionResult> Registro(string NombreCompleto, string Correo, int? IdRol, string? Apellidos, string? DNI, string? Telefono, string? Area, string? Contrasena, string? ConfirmarContrasena)
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

                // Validar contraseñas
                if (string.IsNullOrWhiteSpace(Contrasena) || string.IsNullOrWhiteSpace(ConfirmarContrasena))
                {
                    TempData["Error"] = "Ingrese y confirme su contraseña.";
                    return View();
                }
                if (Contrasena.Trim().Length < 6)
                {
                    TempData["Error"] = "La contraseña debe tener al menos 6 caracteres.";
                    return View();
                }
                if (Contrasena.Trim() != ConfirmarContrasena.Trim())
                {
                    TempData["Error"] = "Las contraseñas no coinciden.";
                    return View();
                }

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
                        (IdRol == 1 && r.nombre.Equals("Colaborador", StringComparison.OrdinalIgnoreCase)) ||
                        (IdRol == 2 && r.nombre.Equals("Practicante", StringComparison.OrdinalIgnoreCase))
                    ).id;
                    if (rolId == Guid.Empty)
                    {
                        var rolUsuario = roles.FirstOrDefault(r => r.nombre.Equals("Usuario", StringComparison.OrdinalIgnoreCase));
                        rolId = rolUsuario.id != Guid.Empty ? rolUsuario.id : roles.First().id;
                    }

                    await _repo.CreateUserAsync(NombreCompleto.Trim(), Correo, rolId, activo: false, plainPassword: Contrasena.Trim());
                }
                catch
                {
                    // Si falla creación de usuario, continuamos con solicitud igualmente
                }

                // Crear solicitud en tabla public.solicitudes (si existe). Si no, solo quedará usuario inactivo esperando aprobación.
                var creoSolicitud = false;
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
                    using var cmd = new Npgsql.NpgsqlCommand("insert into public.solicitudes (id, nombre, correo, estado, creado_en) values (gen_random_uuid(), @n, @c, 'pendiente', now())", conn);
                    cmd.Parameters.AddWithValue("n", NombreCompleto.Trim());
                    cmd.Parameters.AddWithValue("c", Correo);
                    await cmd.ExecuteNonQueryAsync();
                    creoSolicitud = true;
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
                    TempData["Error"] = "El DNI ingresado no está registrado en el sistema.";
                    return View("Login");
                }

                // Validar que el usuario esté activo (Estado = 1 o true)
                if (!usuarioDb.Value.activo)
                {
                    _logger.LogWarning($"Usuario inactivo - correo: {DNI}, ID: {usuarioDb.Value.id}");
                    TempData["Error"] = "Su cuenta está inactiva. Contacte al administrador.";
                    return View("Login");
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

                // Si es primer inicio, debe cambiar su contraseña
                // flujo de primer inicio removido; usamos contraseña con hash directamente

                // Login exitoso normal
                //TempData["Mensaje"] = $"¡Bienvenido {usuario.Nombres}!";
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
                // 3. GENERAR TOKEN DE RECUPERACIÓN
                // ============================================
                var token = Guid.NewGuid().ToString();
                var fechaExpiracion = DateTime.Now.AddMinutes(15);
                // Nota: si deseas persistir token en DB, agregamos columnas luego.

                // ============================================
                // 4. ENVIAR CORREO DE RECUPERACIÓN
                // ============================================
                var urlRecuperacion = Url.Action(
                    "RestablecerContrasena",
                    "Login",
                    new { token = token },
                    Request.Scheme
                );

                _logger.LogInformation($"✅ Token de recuperación generado para usuario ID: {usuarioDb.Value.id}");
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