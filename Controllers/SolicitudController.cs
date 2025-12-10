using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Campus_Virtul_GRLL.Controllers
{
    public class SolicitudController : Controller
    {
        private readonly SupabaseRepository _repo;
        private readonly ILogger<SolicitudController> _logger;
        private readonly IMemoryCache _cache;

        public SolicitudController(SupabaseRepository repo, ILogger<SolicitudController> logger, IMemoryCache cache)
        {
            _repo = repo;
            _logger = logger;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> Solicitud()
        {
            var roles = await _repo.GetRolesAsync();
            ViewBag.Roles = roles;
            var areas = await _repo.GetAreasAsync();
            ViewBag.Areas = areas;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Solicitud(Solicitud modelo)
        {
            try
            {
                // Remover validación de la propiedad de navegación y campos que se parsean manualmente
                ModelState.Remove("Rol");
                ModelState.Remove("IdRol");
                ModelState.Remove("Area");

                // Validar el ModelState
                if (!ModelState.IsValid)
                {
                    var errores = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    TempData["Error"] = $"Errores de validación: {errores}";
                    var roles = await _repo.GetRolesAsync();
                    ViewBag.Roles = roles;
                    var areas = await _repo.GetAreasAsync();
                    ViewBag.Areas = areas;
                    return View(modelo);
                }

                // Validar rol seleccionado (desde combo por Guid)
                    // Verificar que el rol exista usando el repositorio
                    var rolSeleccionado = Request.Form["IdRol"].FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(rolSeleccionado) || !Guid.TryParse(rolSeleccionado, out var rolId))
                    {
                        TempData["Error"] = "Debe seleccionar un tipo de usuario válido.";
                        return View(modelo);
                    }

                // Validar todos los campos duplicandos a la vez
                var camposDuplicados = new List<string>();

                // Verificar Nombres + Apellidos
                    /* TODO: Validaciones duplicadas contra DB (correo/dni). Por ahora omitimos InMemory */
                    /* if (_store.Solicitudes.Values.Any(s =>
                        s.Nombres.ToLower() == modelo.Nombres.Trim().ToLower() &&
                        s.Apellidos.ToLower() == modelo.Apellidos.Trim().ToLower()))
                    {
                        camposDuplicados.Add($"Nombre completo ({modelo.Nombres} {modelo.Apellidos})");
                    } */

                // Verificar DNI
                    // Validar duplicados en DB
                    if (await _repo.ExisteUsuarioPorDniAsync(modelo.DNI.Trim()))
                    {
                        camposDuplicados.Add($"DNI ({modelo.DNI})");
                    }

                // Verificar Teléfono
                    // TODO: Validar Teléfono duplicado en DB

                // Verificar Correo
                    if (await _repo.ExisteUsuarioPorCorreoAsync(modelo.CorreoElectronico.Trim().ToLower()))
                    {
                        camposDuplicados.Add($"Correo electrónico ({modelo.CorreoElectronico})");
                    }

                // Si hay duplicados, mostrar todos
                if (camposDuplicados.Any())
                {
                    var mensajeDuplicados = string.Join(", ", camposDuplicados);
                    TempData["Error"] = $"Los siguientes datos ya están registrados en la base de datos: {mensajeDuplicados}. Por favor, verifique los datos ingresados.";
                    return View(modelo);
                }

                // Paso de verificación: generar código y enviar por correo
                Guid? areaId = null;
                var areaSeleccionada = Request.Form["Area"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(areaSeleccionada) && Guid.TryParse(areaSeleccionada, out var aid)) areaId = aid;

                var correo = modelo.CorreoElectronico?.Trim().ToLower();
                correo = correo ?? string.Empty;
                if (string.IsNullOrWhiteSpace(correo))
                {
                    TempData["Error"] = "Ingrese un correo electrónico válido.";
                    var roles2 = await _repo.GetRolesAsync(); ViewBag.Roles = roles2;
                    var areas2 = await _repo.GetAreasAsync(); ViewBag.Areas = areas2;
                    return View(modelo);
                }

                var code = new Random().Next(100000, 999999).ToString();
                var cacheKey = $"verif:{correo}";
                _cache.Set(cacheKey, new PendingSolicitud
                {
                    Codigo = code,
                    Expira = DateTime.UtcNow.AddMinutes(15),
                    Datos = new PendingSolicitudData
                    {
                        Nombres = modelo.Nombres.Trim(),
                        Apellidos = modelo.Apellidos.Trim(),
                        DNI = modelo.DNI.Trim(),
                        Telefono = modelo.Telefono.Trim(),
                        Correo = correo,
                        RolId = rolId,
                        AreaId = areaId
                    }
                }, TimeSpan.FromMinutes(15));

                // Enviar código por correo
                var emailSvc = HttpContext.RequestServices.GetService<Campus_Virtul_GRLL.Services.EmailService>();
                if (emailSvc != null && emailSvc.IsConfigured)
                {
                    var body = $"<p>Verificación de correo para solicitud de cuenta.</p><p>Tu código es: <b>{code}</b></p><p>Válido por 15 minutos.</p>";
                    await emailSvc.SendAsync(correo, "Verificación de correo", body);
                }

                ViewBag.CorreoVerificacion = correo;
                return View("VerificacionCodigo");
            }

            catch (Exception ex)
            {
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = $"Error inesperado: {errorMessage}";
                var roles = await _repo.GetRolesAsync();
                ViewBag.Roles = roles;
                var areas = await _repo.GetAreasAsync();
                ViewBag.Areas = areas;
                return View(modelo);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerificarCodigo(string Correo, string Codigo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Correo) || string.IsNullOrWhiteSpace(Codigo))
                {
                    TempData["Error"] = "Correo y código son obligatorios.";
                    ViewBag.CorreoVerificacion = Correo;
                    return View("VerificacionCodigo");
                }

                var cacheKey = $"verif:{Correo.Trim().ToLower()}";
                var pending = _cache.Get<PendingSolicitud>(cacheKey);
                if (pending == null)
                {
                    TempData["Error"] = "El código ha expirado o no existe. Solicite nuevamente.";
                    ViewBag.CorreoVerificacion = Correo;
                    return View("VerificacionCodigo");
                }

                if (pending.Expira < DateTime.UtcNow)
                {
                    TempData["Error"] = "El código ha expirado. Solicite nuevamente.";
                    _cache.Remove(cacheKey);
                    ViewBag.CorreoVerificacion = Correo;
                    return View("VerificacionCodigo");
                }

                if (!string.Equals(pending.Codigo, Codigo.Trim()))
                {
                    TempData["Error"] = "Código incorrecto. Verifique y vuelva a intentar.";
                    ViewBag.CorreoVerificacion = Correo;
                    return View("VerificacionCodigo");
                }

                // Código correcto: crear usuario y solicitud
                var d = pending.Datos;
                var nuevoUsuarioId = await _repo.CreateUserAsync(
                    nombres: d.Nombres,
                    correo: d.Correo,
                    rolId: d.RolId,
                    activo: false,
                    plainPassword: null,
                    apellidos: d.Apellidos,
                    dni: d.DNI,
                    telefono: d.Telefono,
                    areaId: d.AreaId
                );

                await _repo.CreateSolicitudAsync(nuevoUsuarioId, "registro", "Solicitud de registro de usuario");
                _cache.Remove(cacheKey);

                TempData["Mensaje"] = "Correo verificado. Tu solicitud fue enviada al administrador.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar código de correo");
                TempData["Error"] = "Error inesperado al verificar el código.";
                ViewBag.CorreoVerificacion = Correo;
                return View("VerificacionCodigo");
            }
        }

        private class PendingSolicitud
        {
            public string Codigo { get; set; } = string.Empty;
            public DateTime Expira { get; set; }
            public PendingSolicitudData Datos { get; set; } = new PendingSolicitudData();
        }

        private class PendingSolicitudData
        {
            public string Nombres { get; set; } = string.Empty;
            public string Apellidos { get; set; } = string.Empty;
            public string DNI { get; set; } = string.Empty;
            public string Telefono { get; set; } = string.Empty;
            public string Correo { get; set; } = string.Empty;
            public Guid RolId { get; set; }
            public Guid? AreaId { get; set; }
        }
    }
}