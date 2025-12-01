using Microsoft.AspNetCore.Mvc;
using Campus_Virtul_GRLL.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Campus_Virtul_GRLL.Controllers
{
    public class InscripcionesController : Controller
    {
        private readonly SupabaseRepository _repo;
        public InscripcionesController(SupabaseRepository repo)
        {
            _repo = repo;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rol = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (!string.Equals(rol, "Profesor", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("AccesoDenegado", "Login");

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var profesorId))
                return RedirectToAction("Index", "Login");

            var items = await _repo.GetInscripcionesPendientesPorProfesorAsync(profesorId);
            var vm = items.Select(i => new Models.ViewModels.InscripcionPendienteVm
            {
                SolicitudId = i.solicitudId,
                CursoId = i.cursoId,
                CursoTitulo = i.cursoTitulo,
                PracticanteId = i.practicanteId,
                PracticanteNombre = i.practicanteNombre,
                PracticanteCorreo = i.practicanteCorreo,
                CreadaEn = i.creadaEn
            }).ToList();
            return View("~/Views/Inscripciones/Index.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aprobar(Guid id)
        {
            var rol = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (!string.Equals(rol, "Profesor", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("AccesoDenegado", "Login");

            await _repo.ApproveInscripcionAsync(id);
            TempData["Mensaje"] = "Inscripción aprobada";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rechazar(Guid id)
        {
            var rol = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (!string.Equals(rol, "Profesor", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("AccesoDenegado", "Login");

            await _repo.UpdateSolicitudEstadoAsync(id, "rechazada");
            TempData["Mensaje"] = "Inscripción rechazada";
            return RedirectToAction(nameof(Index));
        }
    }
}
