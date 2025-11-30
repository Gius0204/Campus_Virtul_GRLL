using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Campus_Virtul_GRLL.Helpers;
using System.Linq;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize(Roles="Profesor")]
    public class InscripcionesController : Controller
    {
        private readonly InMemoryDataStore _store;
        private readonly ILogger<InscripcionesController> _logger;

        public InscripcionesController(InMemoryDataStore store, ILogger<InscripcionesController> logger)
        {
            _store = store;
            _logger = logger;
        }

        // Selector de cursos del profesor con pendientes
        [HttpGet]
        public IActionResult Index()
        {
            var userId = User.GetUserId();
            var cursosProfesor = _store.Cursos.Values.Where(c => c.IdProfesor == userId).ToList();
            var resumen = cursosProfesor.Select(c => new
            {
                Curso = c,
                Pendientes = _store.Inscripciones.Values.Count(i => i.IdCurso == c.IdCurso && i.Estado == EstadoInscripcion.Pendiente)
            }).ToList();
            ViewBag.Resumen = resumen;
            return View();
        }

        // Lista de solicitudes de inscripción pendientes para un curso del profesor
        [HttpGet]
        public IActionResult Pendientes(int idCurso)
        {
            if (!_store.Cursos.TryGetValue(idCurso, out var curso)) return NotFound();
            if (User.GetUserRole() == "Profesor" && curso.IdProfesor != User.GetUserId()) return Forbid();
            var pendientes = _store.Inscripciones.Values.Where(i => i.IdCurso == idCurso && i.Estado == EstadoInscripcion.Pendiente).ToList();
            ViewBag.Curso = curso;
            return View(pendientes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Aprobar(int idInscripcion)
        {
            if (!_store.Inscripciones.TryGetValue(idInscripcion, out var ins)) return NotFound();
            if (!_store.Cursos.TryGetValue(ins.IdCurso, out var curso)) return NotFound();
            if (User.GetUserRole() == "Profesor" && curso.IdProfesor != User.GetUserId()) return Forbid();
            _store.CambiarEstadoInscripcion(idInscripcion, EstadoInscripcion.Aprobada);
            TempData["Mensaje"] = "Inscripción aprobada";
            return RedirectToAction("Pendientes", new { idCurso = ins.IdCurso });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Rechazar(int idInscripcion)
        {
            if (!_store.Inscripciones.TryGetValue(idInscripcion, out var ins)) return NotFound();
            if (!_store.Cursos.TryGetValue(ins.IdCurso, out var curso)) return NotFound();
            if (User.GetUserRole() == "Profesor" && curso.IdProfesor != User.GetUserId()) return Forbid();
            _store.CambiarEstadoInscripcion(idInscripcion, EstadoInscripcion.Rechazada);
            TempData["Mensaje"] = "Inscripción rechazada";
            return RedirectToAction("Pendientes", new { idCurso = ins.IdCurso });
        }
    }
}
