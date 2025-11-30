using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Campus_Virtul_GRLL.Helpers;
using System.Linq;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize]
    public class TareasController : Controller
    {
        private readonly InMemoryDataStore _store;
        private readonly ILogger<TareasController> _logger;

        public TareasController(InMemoryDataStore store, ILogger<TareasController> logger)
        {
            _store = store;
            _logger = logger;
        }

        // Formulario de entrega (Practicante)
        [Authorize(Roles="Practicante")]
        [HttpGet]
        public IActionResult Entregar(int idTarea)
        {
            if (!_store.Tareas.TryGetValue(idTarea, out var tarea)) return NotFound();
            ViewBag.Tarea = tarea;
            return View();
        }

        [Authorize(Roles="Practicante")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Entregar(int idTarea, string nombreArchivo)
        {
            if (!_store.Tareas.TryGetValue(idTarea, out var tarea)) return NotFound();
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                TempData["Error"] = "Debe proporcionar un nombre de archivo.";
                return RedirectToAction("Entregar", new { idTarea });
            }
            _store.EntregarArchivo(idTarea, User.GetUserId(), nombreArchivo.Trim());
            TempData["Mensaje"] = "Entrega registrada";
            return RedirectToAction("Entregar", new { idTarea });
        }

        // Listado de entregas (Profesor)
        [Authorize(Roles="Profesor,Administrador")]
        [HttpGet]
        public IActionResult Entregas(int idTarea)
        {
            if (!_store.Tareas.TryGetValue(idTarea, out var tarea)) return NotFound();
            var entregas = _store.EntregasTarea.Values.Where(e => e.IdTarea == idTarea).ToList();
            ViewBag.Tarea = tarea;
            return View(entregas);
        }

        [Authorize(Roles="Profesor,Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Calificar(int idEntrega, int nota)
        {
            if (nota < 0 || nota > 20)
            {
                TempData["Error"] = "La nota debe estar entre 0 y 20.";
                return Redirect(Request.Headers["Referer"].ToString());
            }
            if (!_store.EntregasTarea.TryGetValue(idEntrega, out var entrega)) return NotFound();
            _store.CalificarEntrega(idEntrega, nota);
            TempData["Mensaje"] = "Entrega calificada";
            return RedirectToAction("Entregas", new { idTarea = entrega.IdTarea });
        }
    }
}
