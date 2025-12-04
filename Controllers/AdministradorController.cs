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
                // Mapear a un modelo simple para la vista existente o ajustar la vista
                var lista = solicitudesDb.Select(s => new Solicitud
                {
                    IdSolicitud = 0, // la vista usa int; mantenemos 0 y mostramos datos principales
                    Nombres = string.Empty,
                    Apellidos = string.Empty,
                    DNI = string.Empty,
                    Telefono = string.Empty,
                    CorreoElectronico = s.correo,
                    Area = string.Empty,
                    FechaSolicitud = DateOnly.FromDateTime(s.creadoEn),
                    Estado = s.estado?.ToLower() switch
                    {
                        "aprobada" => EstadoSolicitud.Aprobada,
                        "rechazada" => EstadoSolicitud.Rechazada,
                        "en revision" => EstadoSolicitud.EnRevision,
                        _ => EstadoSolicitud.Enviada
                    }
                }).ToList();

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
                    // Opcional: establecer una contraseña temporal (enviar por correo en futuro)
                    var usuarioDb = await _repo.GetUserByEmailAsync(correo);
                    if (usuarioDb.HasValue)
                    {
                        await _repo.UpdateUserPasswordAsync(usuarioDb.Value.id, "Cambio123");
                    }
                }
                TempData["Mensaje"] = "Solicitud aprobada correctamente.";
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
                TempData["Mensaje"] = "Solicitud rechazada correctamente.";
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