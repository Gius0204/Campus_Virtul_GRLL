using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Campus_Virtul_GRLL.Helpers;
using System.Linq;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize(Roles="Profesor,Administrador")]
    public class SesionesController : Controller
    {
        private readonly InMemoryDataStore _store;
        private readonly ILogger<SesionesController> _logger;

        public SesionesController(InMemoryDataStore store, ILogger<SesionesController> logger)
        {
            _store = store;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(int idCurso, string titulo, string descripcion)
        {
            if (!_store.Cursos.TryGetValue(idCurso, out var curso)) return NotFound();
            if (User.GetUserRole() == "Profesor" && curso.IdProfesor != User.GetUserId()) return Forbid();
            var orden = _store.Sesiones.Values.Count(s => s.IdCurso == idCurso) + 1;
            _store.AddSesion(idCurso, titulo.Trim(), descripcion?.Trim() ?? string.Empty, orden);
            TempData["Mensaje"] = "Sesión creada";
            return RedirectToAction("Detalle", "Cursos", new { id = idCurso });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearContenido(int idSesion, string titulo, string texto)
        {
            if (!_store.Sesiones.TryGetValue(idSesion, out var sesion)) return NotFound();
            if (!_store.Cursos.TryGetValue(sesion.IdCurso, out var curso)) return NotFound();
            if (User.GetUserRole() == "Profesor" && curso.IdProfesor != User.GetUserId()) return Forbid();
            _store.AddSubSeccionContenido(idSesion, titulo.Trim(), texto?.Trim() ?? string.Empty);
            TempData["Mensaje"] = "Contenido agregado";
            return RedirectToAction("Detalle", "Cursos", new { id = sesion.IdCurso });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearVideo(int idSesion, string titulo, string rutaVideo)
        {
            if (!_store.Sesiones.TryGetValue(idSesion, out var sesion)) return NotFound();
            if (!_store.Cursos.TryGetValue(sesion.IdCurso, out var curso)) return NotFound();
            if (User.GetUserRole() == "Profesor" && curso.IdProfesor != User.GetUserId()) return Forbid();
            _store.AddSubSeccionVideo(idSesion, titulo.Trim(), rutaVideo.Trim());
            TempData["Mensaje"] = "Video agregado";
            return RedirectToAction("Detalle", "Cursos", new { id = sesion.IdCurso });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearTarea(int idSesion, string titulo, DateTime fechaLimite)
        {
            if (!_store.Sesiones.TryGetValue(idSesion, out var sesion)) return NotFound();
            if (!_store.Cursos.TryGetValue(sesion.IdCurso, out var curso)) return NotFound();
            if (User.GetUserRole() == "Profesor" && curso.IdProfesor != User.GetUserId()) return Forbid();
            _store.AddSubSeccionTarea(idSesion, titulo.Trim(), fechaLimite);
            TempData["Mensaje"] = "Tarea creada";
            return RedirectToAction("Detalle", "Cursos", new { id = sesion.IdCurso });
        }
    }
}
