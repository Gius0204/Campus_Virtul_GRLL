using Microsoft.AspNetCore.Authorization;
using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Microsoft.AspNetCore.Mvc;
using System;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class AdministradorController : Controller
    {
        private readonly SupabaseRepository _repo;

        public AdministradorController(SupabaseRepository repo)
        {
            _repo = repo;
        }

        [HttpGet]
        public async Task<IActionResult> PanelAdministrador(string? vista)
        {
            // Datos del usuario autenticado
            var correo = User?.Claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            if (!string.IsNullOrWhiteSpace(correo))
            {
                var u = await _repo.GetUserByEmailAsync(correo);
                if (u.HasValue)
                {
                    ViewBag.Nombres = u.Value.nombres;
                    ViewBag.Apellidos = u.Value.apellidos;
                    ViewBag.DNI = u.Value.dni;
                    ViewBag.Telefono = u.Value.telefono;
                    // Resolver nombre de área si tiene areaId
                    if (u.Value.areaId.HasValue)
                    {
                        var areas = await _repo.GetAreasAsync();
                        var areaNombre = areas.FirstOrDefault(a => a.id == u.Value.areaId.Value).nombre;
                        ViewBag.Area = areaNombre;
                    }
                }
            }

            var counts = await _repo.GetDashboardCountsAsync();
            ViewBag.TotalCursos = counts.cursos;
            ViewBag.TotalProfesores = counts.profesores;
            ViewBag.TotalPracticantes = counts.practicantes;

            ViewBag.Vista = string.IsNullOrWhiteSpace(vista) ? "cursos" : vista.ToLower();
            if (ViewBag.Vista == "cursos")
            {
                var cursos = await _repo.GetCursosAsync();
                ViewBag.Cursos = cursos;
                // Pre-cargar profesores asignados por curso en un diccionario para la vista
                var dict = new Dictionary<Guid, List<(Guid profesorId, string nombres, string? apellidos, string? telefono, string correo, string? area)>>();
                foreach (var c in cursos)
                {
                    var asignadosExt = await _repo.GetCursoProfesoresAsync(c.id);
                    // Proyectar a la tupla COMPLETA usada por la vista
                    dict[c.id] = asignadosExt.Select(p => (p.profesorId, p.nombres, p.apellidos, p.telefono, p.correo, p.areaNombre)).ToList();
                }
                // Exponer delegado para recuperar lista fácilmente (tupla completa)
                ViewBag.ProfesoresAsignadosPorCurso = new Func<Guid, IEnumerable<(Guid profesorId, string nombres, string? apellidos, string? telefono, string correo, string? area)>>(
                    cid => dict.ContainsKey(cid) ? dict[cid] : Enumerable.Empty<(Guid, string, string?, string?, string, string?)>()
                );
            }
            else
            {
                var usuarios = await _repo.GetUsuariosAsync();
                if (ViewBag.Vista == "profesores")
                {
                    var profs = usuarios.Where(u => u.activo && string.Equals(u.rolNombre, "Profesor", StringComparison.OrdinalIgnoreCase)).ToList();
                    ViewBag.Usuarios = profs;
                }
                else if (ViewBag.Vista == "practicantes")
                {
                    var prac = usuarios.Where(u => u.activo && string.Equals(u.rolNombre, "Practicante", StringComparison.OrdinalIgnoreCase)).ToList();
                    ViewBag.Usuarios = prac;
                }
                else
                {
                    ViewBag.Vista = "cursos";
                    var cursos = await _repo.GetCursosAsync();
                    ViewBag.Cursos = cursos;
                }
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublicarCurso(Guid id)
        {
            await _repo.UpdateCursoEstadoAsync(id, "publicado");
            TempData["Mensaje"] = "Curso publicado";
            return RedirectToAction("PanelAdministrador");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BorradorCurso(Guid id)
        {
            await _repo.UpdateCursoEstadoAsync(id, "borrador");
            TempData["Mensaje"] = "Curso en borrador";
            return RedirectToAction("PanelAdministrador");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCurso(Guid id)
        {
            await _repo.DeleteCursoAsync(id);
            TempData["Mensaje"] = "Curso eliminado";
            return RedirectToAction("PanelAdministrador");
        }

        [HttpGet]
        public async Task<IActionResult> PanelSolicitudes()
        {
            try
            {
                var solicitudesDb = await _repo.GetSolicitudesAsync();
                ViewBag.SolicitudesRaw = solicitudesDb;
                // Enriquecer datos buscando usuario por correo
                var lista = new List<Solicitud>();
                foreach (var s in solicitudesDb)
                {
                    var u = await _repo.GetUserByEmailAsync(s.correo);
                    var areaNombre = string.Empty;
                    if (u.HasValue && u.Value.areaId.HasValue)
                    {
                        var areas = await _repo.GetAreasAsync();
                        areaNombre = areas.FirstOrDefault(a => a.id == u.Value.areaId.Value).nombre;
                    }
                    lista.Add(new Solicitud
                    {
                        IdSolicitud = 0,
                        Nombres = u.HasValue ? u.Value.nombres : string.Empty,
                        Apellidos = u.HasValue ? (u.Value.apellidos ?? "") : string.Empty,
                        DNI = u.HasValue ? (u.Value.dni ?? "") : string.Empty,
                        Telefono = u.HasValue ? (u.Value.telefono ?? "") : string.Empty,
                        CorreoElectronico = s.correo,
                        Area = areaNombre,
                        FechaSolicitud = DateOnly.FromDateTime(s.creadoEn),
                        Estado = s.estado?.ToLower() switch
                        {
                            "aprobada" => EstadoSolicitud.Aprobada,
                            "rechazada" => EstadoSolicitud.Rechazada,
                            "en revision" => EstadoSolicitud.EnRevision,
                            _ => EstadoSolicitud.Enviada
                        },
                        Rol = new Rol { IdRol = 0, NombreRol = u.HasValue ? u.Value.rolNombre : "", Descripcion = "", Estado = true }
                    });
                }

                return View(lista);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar las solicitudes: {ex.Message}";
                return View(new List<Solicitud>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles="Administrador")]
        public async Task<IActionResult> AprobarSolicitud(Guid id, string correo)
        {
            try
            {
                await _repo.UpdateSolicitudEstadoAsync(id, "aprobada");
                if (!string.IsNullOrWhiteSpace(correo))
                {
                    await _repo.ActivateUserByCorreoAsync(correo);
                    var usuarioDb = await _repo.GetUserByEmailAsync(correo);
                    if (usuarioDb.HasValue)
                    {
                        // Generar contraseña temporal segura
                        var tempPassword = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                            .Replace("=","")
                            .Replace("+","")
                            .Replace("/","")
                            .Substring(0, 12);
                        await _repo.UpdateUserPasswordAsync(usuarioDb.Value.id, tempPassword);
                        await _repo.SetRequirePasswordChangeAsync(usuarioDb.Value.id, true);

                        // Enviar correo con la contraseña temporal
                        var emailSvc = HttpContext.RequestServices.GetService<Campus_Virtul_GRLL.Services.EmailService>();
                        if (emailSvc != null && emailSvc.IsConfigured)
                        {
                            var body = $"<p>Hola {usuarioDb.Value.nombres},</p><p>Tu solicitud fue aprobada.</p><p>Tu contraseña temporal es: <b>{tempPassword}</b></p><p>Inicia sesión y se te pedirá cambiarla por una nueva.</p>";
                            await emailSvc.SendAsync(correo, "Cuenta aprobada - Contraseña temporal", body);
                        }
                    }
                }
                TempData["ToastSuccess"] = "Solicitud aprobada correctamente.";
                return RedirectToAction("PanelSolicitudes");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al aprobar solicitud: {ex.Message}";
                return RedirectToAction("PanelSolicitudes");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles="Administrador")]
        public async Task<IActionResult> RechazarSolicitud(Guid id, string? correo)
        {
            try
            {
                await _repo.UpdateSolicitudEstadoAsync(id, "rechazada");
                if (!string.IsNullOrWhiteSpace(correo))
                {
                    await _repo.DeleteUserByCorreoAsync(correo);
                }
                TempData["ToastSuccess"] = "Solicitud rechazada correctamente.";
                return RedirectToAction("PanelSolicitudes");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al rechazar solicitud: {ex.Message}";
                return RedirectToAction("PanelSolicitudes");
            }
        }

        // Crear curso (GET)
        [Authorize(Roles="Administrador")]
        [HttpGet]
        public IActionResult CrearCurso()
        {
            return View();
        }

        // Crear curso (POST)
        [Authorize(Roles="Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearCurso(string titulo, string? descripcion, string estado)
        {
            if (string.IsNullOrWhiteSpace(titulo))
            {
                TempData["Error"] = "El título es obligatorio";
                return RedirectToAction("CrearCurso");
            }
            var estadoVal = string.IsNullOrWhiteSpace(estado) ? "borrador" : estado.Trim().ToLower();
            if (estadoVal != "borrador" && estadoVal != "publicado") estadoVal = "borrador";
            await _repo.CreateCursoAsync(titulo.Trim(), string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(), estadoVal);
            TempData["Mensaje"] = "Curso creado";
            return RedirectToAction("PanelAdministrador");
        }
    }
}