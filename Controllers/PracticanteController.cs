using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Campus_Virtul_GRLL.Models;
using Microsoft.AspNetCore.Http;
using Campus_Virtul_GRLL.Services;
using System.Security.Claims;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Campus_Virtul_GRLL.Controllers
{
    public class PracticanteController : Controller
    {
        private readonly SupabaseRepository _repo;

        public PracticanteController(SupabaseRepository repo)
        {
            _repo = repo;
        }

        // GET: /Practicante/Catalogo
        public async Task<IActionResult> Catalogo()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? uid = null;
            if (!string.IsNullOrWhiteSpace(userIdStr) && Guid.TryParse(userIdStr, out var g)) uid = g;

            var cursos = (await _repo.GetCursosAsync())
                .Where(c => string.Equals(c.estado, "publicado", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var pendientes = uid.HasValue ? await _repo.GetSolicitudesInscripcionPorUsuarioAsync(uid.Value) : new List<(Guid cursoId, string estado)>();
            var inscritos = uid.HasValue ? await _repo.GetCursosPorPracticanteAsync(uid.Value) : new List<(Guid id, string titulo, string? descripcion, string estado, DateTime creadoEn)>();

            var vm = cursos.Select(c => new Models.ViewModels.CursoCatalogoVm
            {
                Id = c.id,
                Titulo = c.titulo,
                Descripcion = c.descripcion,
                Estado = c.estado,
                EstaInscrito = inscritos.Any(x => x.id == c.id),
                TieneSolicitudPendiente = pendientes.Any(p => p.cursoId == c.id && string.Equals(p.estado, "pendiente", StringComparison.OrdinalIgnoreCase)),
                Profesores = new System.Collections.Generic.List<(System.Guid profesorId, string nombres, string? apellidos, string? telefono, string correo, string? area)>()
            }).ToList();

            // Cargar profesores por curso para el modal de información
            foreach (var item in vm)
            {
                var profesores = await _repo.GetCursoProfesoresAsync(item.Id);
                item.Profesores = profesores.Select(p => (p.profesorId, p.nombres, p.apellidos, p.telefono, p.correo, p.areaNombre)).ToList();
            }
            return View("~/Views/Practicante/Index.cshtml", vm);
        }

        // GET: /Practicante/MisCursos
        public async Task<IActionResult> MisCursos()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var uid))
                return RedirectToAction("Index", "Login");

            var cursos = await _repo.GetCursosPorPracticanteAsync(uid);
            var vm = cursos
                .Select(c => new Models.ViewModels.CursoVm
                {
                    Id = c.id,
                    Titulo = c.titulo,
                    Descripcion = c.descripcion,
                    Estado = c.estado
                }).ToList();
            return View("~/Views/Practicante/MisCursos.cshtml", vm);
        }

        // GET: /Practicante/Detalle/{id}
        public async Task<IActionResult> Detalle(Guid id)
        {
            var cursos = await _repo.GetCursosAsync();
            var c = cursos.FirstOrDefault(x => x.id == id);
            if (c.id == Guid.Empty) return NotFound();
            var vm = new Models.ViewModels.CursoVm { Id = c.id, Titulo = c.titulo, Descripcion = c.descripcion, Estado = c.estado };
            return View("~/Views/Practicante/Detalle.cshtml", vm);
        }

        // POST: /Practicante/SolicitarInscripcion/{cursoId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarInscripcion(Guid cursoId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var uid))
                return RedirectToAction("Index", "Login");

            // Crear solicitud tipo 'inscripcion' con detalle el cursoId
            await _repo.CreateSolicitudAsync(uid, "inscripcion", cursoId.ToString());
            TempData["Mensaje"] = "Solicitud de inscripción enviada.";
            return RedirectToAction(nameof(Catalogo));
        }
    }
}
