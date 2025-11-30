using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Campus_Virtul_GRLL.Helpers;
using System.Linq;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize]
    public class CursosController : Controller
    {
        private readonly InMemoryDataStore _store;
        private readonly ILogger<CursosController> _logger;

        public CursosController(InMemoryDataStore store, ILogger<CursosController> logger)
        {
            _store = store;
            _logger = logger;
        }

        // Catálogo general
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Index()
        {
            var cursos = _store.Cursos.Values.ToList();
            return View(cursos);
        }

        // Mis cursos (Profesor o Practicante)
        [Authorize(Roles = "Administrador,Profesor,Practicante")]
        [HttpGet]
        public IActionResult MisCursos()
        {
            var userId = User.GetUserId();
            var rol = User.GetUserRole();
            if (rol == "Profesor")
            {
                var propios = _store.Cursos.Values.Where(c => c.IdProfesor == userId).ToList();
                return View("MisCursos", propios);
            }
            if (rol == "Practicante")
            {
                var aprobadas = _store.Inscripciones.Values.Where(i => i.IdUsuario == userId && i.Estado == EstadoInscripcion.Aprobada).Select(i => i.IdCurso).ToHashSet();
                var cursos = _store.Cursos.Values.Where(c => aprobadas.Contains(c.IdCurso)).ToList();
                return View("MisCursos", cursos);
            }
            return View("MisCursos", new List<Curso>());
        }

        // Detalle curso
        [HttpGet]
        public IActionResult Detalle(int id)
        {
            if (!_store.Cursos.TryGetValue(id, out var curso)) return NotFound();
            var sesiones = _store.Sesiones.Values.Where(s => s.IdCurso == id).OrderBy(s => s.Orden).ToList();
            var subSecciones = _store.SubSecciones.Values.Where(ss => sesiones.Select(s => s.IdSesion).Contains(ss.IdSesion)).ToList();
            ViewBag.Sesiones = sesiones;
            ViewBag.SubSecciones = subSecciones;
            ViewBag.PuedeEditar = User.GetUserRole() == "Administrador" || curso.IdProfesor == User.GetUserId();
            return View(curso);
        }

        // Solicitar inscripción (Practicante)
        [Authorize(Roles = "Practicante")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SolicitarInscripcion(int idCurso)
        {
            if (!_store.Cursos.ContainsKey(idCurso)) return NotFound();
            var ins = _store.SolicitarInscripcion(idCurso, User.GetUserId());
            TempData["Mensaje"] = ins.Estado == EstadoInscripcion.Pendiente ? "Solicitud enviada" : "Ya existe una solicitud";
            return RedirectToAction("Detalle", new { id = idCurso });
        }

        // Publicar curso
        [Authorize(Roles = "Profesor,Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Publicar(int idCurso)
        {
            if (!_store.Cursos.TryGetValue(idCurso, out var curso)) return NotFound();
            if (User.GetUserRole() == "Profesor" && curso.IdProfesor != User.GetUserId())
            {
                return Forbid();
            }
            curso.Estado = EstadoCurso.Publicado;
            curso.FechaPublicacion = DateTime.Now;
            TempData["Mensaje"] = "Curso publicado";
            return RedirectToAction("Detalle", new { id = idCurso });
        }
    }
}
