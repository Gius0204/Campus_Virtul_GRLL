using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Campus_Virtul_GRLL.Helpers;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize]
    public class CursosController : Controller
    {
        private readonly SupabaseRepository _repo;
        private readonly ILogger<CursosController> _logger;

        public CursosController(SupabaseRepository repo, ILogger<CursosController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        // Catálogo general
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cursos = await _repo.GetCursosAsync();
            // Proyectar a un modelo simple para la vista existente si fuese necesario
            ViewBag.Cursos = cursos;
            return View();
        }

        // Mis cursos (Profesor o Practicante)
        [Authorize(Roles = "Administrador,Profesor,Practicante")]
        [HttpGet]
        public async Task<IActionResult> MisCursos()
        {
            var rol = User.GetUserRole();
            var userGuid = User.GetUserIdGuid();
            if (rol == "Profesor" && userGuid.HasValue)
            {
                var propios = await _repo.GetCursosPorProfesorAsync(userGuid.Value);
                ViewBag.Cursos = propios;
                return View("MisCursos");
            }
            if (rol == "Practicante")
            {
                // TODO: filtrar por inscripciones aprobadas cuando se migre esa tabla
                ViewBag.Cursos = new List<(Guid id, string titulo, string? descripcion, string estado, DateTime creadoEn)>();
                return View("MisCursos");
            }
            ViewBag.Cursos = new List<(Guid id, string titulo, string? descripcion, string estado, DateTime creadoEn)>();
            return View("MisCursos");
        }

        // Detalle curso (Administrador)
        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> DetalleAdministrador(Guid id)
        {
            var cursos = await _repo.GetCursosAsync();
            var curso = cursos.FirstOrDefault(c => c.id == id);
            if (curso.id == Guid.Empty) return NotFound();
            ViewBag.Curso = curso;
            // Puede editar: Admin o Profesor asignado
            var asignados = await _repo.GetCursoProfesoresAsync(id);
            bool esProfesorAsignado = User.GetUserRole() == "Profesor" && User.GetUserIdGuid().HasValue && asignados.Any(a => a.profesorId == User.GetUserIdGuid().Value);
            ViewBag.PuedeEditar = User.GetUserRole() == "Administrador" || esProfesorAsignado;
            ViewBag.ProfesoresAsignados = asignados;
            // Profesores activos no asignados
            var todosUsuarios = await _repo.GetUsuariosAsync();
            var disponibles = todosUsuarios
                .Where(u => u.activo && string.Equals(u.rolNombre, "Profesor", StringComparison.OrdinalIgnoreCase) && !asignados.Any(a => a.profesorId == u.id))
                .Select(u => (u.id, u.nombres, u.correo))
                .ToList();
            ViewBag.ProfesoresDisponibles = disponibles;
            // Sesiones y subsecciones para pestaña "Contenido del Curso"
            var sesiones = await _repo.GetSesionesPorCursoAsync(id);
            var sesionesConSub = new List<(Guid sesionId, string titulo, int orden, List<(Guid id, string titulo, string tipo, string estado, int orden)> subsecciones)>();
            foreach (var s in sesiones)
            {
                var subs = await _repo.GetSubseccionesPorSesionAsync(s.id);
                sesionesConSub.Add((s.id, s.titulo, s.orden, subs));
            }
            ViewBag.Sesiones = sesionesConSub;
            // Participantes (profesores + practicantes)
            var participantes = await _repo.GetParticipantesCursoAsync(id);
            ViewBag.Participantes = participantes;
            return View("DetalleAdministrador");
        }

        // Detalle curso (Profesor)
        [Authorize(Roles = "Profesor")]
        [HttpGet]
        public async Task<IActionResult> DetalleProfesor(Guid id)
        {
            var cursos = await _repo.GetCursosPorProfesorAsync(User.GetUserIdGuid()!.Value);
            var curso = cursos.FirstOrDefault(c => c.id == id);
            if (curso.id == Guid.Empty) return NotFound();
            ViewBag.Curso = curso;
            // Puede editar: solo si el profesor está asignado
            var asignados = await _repo.GetCursoProfesoresAsync(id);
            bool esProfesorAsignado = User.GetUserIdGuid().HasValue && asignados.Any(a => a.profesorId == User.GetUserIdGuid().Value);
            ViewBag.PuedeEditar = esProfesorAsignado;
            ViewBag.ProfesoresAsignados = asignados;
            // Sesiones y subsecciones
            var sesiones = await _repo.GetSesionesPorCursoAsync(id);
            var sesionesConSub = new List<(Guid sesionId, string titulo, int orden, List<(Guid id, string titulo, string tipo, string estado, int orden)> subsecciones)>();
            foreach (var s in sesiones)
            {
                var subs = await _repo.GetSubseccionesPorSesionAsync(s.id);
                sesionesConSub.Add((s.id, s.titulo, s.orden, subs));
            }
            ViewBag.Sesiones = sesionesConSub;
            // Participantes (profesores + practicantes)
            var participantes = await _repo.GetParticipantesCursoAsync(id);
            ViewBag.Participantes = participantes;
            return View("DetalleProfesor");
        }

        // Solicitar inscripción (Practicante)
        [Authorize(Roles = "Practicante")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SolicitarInscripcion(Guid idCurso)
        {
            // TODO: implementar inscripciones en Supabase
            TempData["Mensaje"] = "Solicitud enviada";
            return RedirectToAction("Detalle", new { id = idCurso });
        }

        // Publicar curso
        [Authorize(Roles = "Profesor,Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publicar(Guid idCurso)
        {
            await _repo.UpdateCursoEstadoAsync(idCurso, "publicado");
            TempData["Mensaje"] = "Curso publicado";
            return RedirectToAction("Detalle", new { id = idCurso });
        }

        [Authorize(Roles = "Profesor,Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Borrador(Guid idCurso)
        {
            await _repo.UpdateCursoEstadoAsync(idCurso, "borrador");
            TempData["Mensaje"] = "Curso marcado como borrador";
            return RedirectToAction("Detalle", new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarProfesor(Guid idCurso, Guid profesorId)
        {
            await _repo.AssignProfesorACursoAsync(idCurso, profesorId);
            TempData["Mensaje"] = "Profesor asignado";
            return RedirectToAction("Detalle", new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetirarProfesor(Guid idCurso, Guid profesorId)
        {
            await _repo.RemoveProfesorDeCursoAsync(idCurso, profesorId);
            TempData["Mensaje"] = "Profesor retirado";
            return RedirectToAction("Detalle", new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearSesion(Guid idCurso, string titulo, int? orden)
        {
            if (string.IsNullOrWhiteSpace(titulo))
            {
                TempData["Error"] = "El título de la sesión es obligatorio";
                return RedirectToAction("Detalle", new { id = idCurso });
            }
            await _repo.CreateSesionAsync(idCurso, titulo.Trim(), orden);
            TempData["Mensaje"] = "Sesión creada";
            return RedirectToAction("Detalle", new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearSubseccion(Guid idCurso, Guid sesionId, string titulo, string tipo, string? textoContenido, IFormFile? archivo, IFormFile? video, int? maxPuntaje)
        {
            if (string.IsNullOrWhiteSpace(titulo))
            {
                TempData["Error"] = "El título de la subsección es obligatorio";
                return RedirectToAction("Detalle", new { id = idCurso });
            }
            // Placeholder almacenamiento (no guarda físicamente el archivo)
            string? archivoUrl = archivo != null ? $"/uploads/{Guid.NewGuid()}_{archivo.FileName}" : null;
            string? videoUrl = video != null ? $"/videos/{Guid.NewGuid()}_{video.FileName}" : null;
            await _repo.CreateSubseccionAsync(sesionId, titulo.Trim(), tipo?.ToLower() ?? "contenido", textoContenido, archivoUrl, videoUrl, maxPuntaje, "borrador");
            TempData["Mensaje"] = "Subsección creada (borrador)";
            return RedirectToAction("Detalle", new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstadoSubseccion(Guid idCurso, Guid subseccionId, string nuevoEstado)
        {
            await _repo.UpdateSubseccionEstadoAsync(subseccionId, nuevoEstado);
            TempData["Mensaje"] = "Estado de subsección actualizado";
            return RedirectToAction("Detalle", new { id = idCurso });
        }

        // Crear curso desde catálogo (modal)
        [Authorize(Roles="Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearDesdeCatalogo(string titulo, string? descripcion, string estado)
        {
            if (string.IsNullOrWhiteSpace(titulo))
            {
                TempData["Error"] = "El título es obligatorio";
                return RedirectToAction("Index");
            }
            var estadoVal = string.IsNullOrWhiteSpace(estado) ? "borrador" : estado.Trim().ToLower();
            if (estadoVal != "borrador" && estadoVal != "publicado") estadoVal = "borrador";
            await _repo.CreateCursoAsync(titulo.Trim(), string.IsNullOrWhiteSpace(descripcion)? null : descripcion.Trim(), estadoVal);
            TempData["Mensaje"] = "Curso creado";
            return RedirectToAction("Index");
        }
    }
}
