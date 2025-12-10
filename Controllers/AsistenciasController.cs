using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Helpers;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize]
    public class AsistenciasController : Controller
    {
        private readonly SupabaseRepository _repo;
        private readonly ILogger<AsistenciasController> _logger;

        public AsistenciasController(SupabaseRepository repo, ILogger<AsistenciasController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        // Profesor: tomar asistencia
        [Authorize(Roles="Profesor")]
        [HttpGet]
        public async Task<IActionResult> Tomar(Guid idCurso, DateOnly? fecha)
        {
            var cursoId = idCurso;
            var f = fecha ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var participantes = await _repo.GetParticipantesCursoAsync(cursoId);
            ViewBag.CursoId = cursoId;
            ViewBag.Fecha = f;
            ViewBag.Participantes = participantes;
            return View();
        }

        [Authorize(Roles="Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(Guid idCurso, DateOnly fecha, TimeOnly? horaInicio, TimeOnly? horaFin, List<Guid> usuarios, List<string> estados, List<string?> justificaciones)
        {
            try
            {
                var profesorId = User.GetUserIdGuid()!.Value;
                var asistenciaId = await _repo.UpsertAsistenciaAsync(idCurso, profesorId, fecha, horaInicio, horaFin);
                for (int i = 0; i < usuarios.Count; i++)
                {
                    var uid = usuarios[i];
                    var est = i < estados.Count ? estados[i] : "ausente";
                    var jus = (i < justificaciones.Count ? justificaciones[i] : null);
                    await _repo.SetAsistenciaDetalleAsync(asistenciaId, uid, est, jus);
                }
                TempData["Mensaje"] = "Asistencia registrada";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando asistencia");
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Tomar", new { idCurso, fecha });
        }

        // Practicante/Colaborador: ver sus asistencias por curso
        [Authorize(Roles="Practicante,Colaborador")]
        [HttpGet]
        public async Task<IActionResult> MisCursos()
        {
            var usuarioId = User.GetUserIdGuid()!.Value;
            var rol = User.GetUserRole();
            List<(Guid id, string titulo, string? descripcion, string estado, DateTime creadoEn)> cursos;
            if (rol == "Practicante")
                cursos = await _repo.GetCursosPorPracticanteAsync(usuarioId);
            else
                cursos = await _repo.GetCursosPorColaboradorAsync(usuarioId);
            ViewBag.Cursos = cursos;
            return View();
        }

        [Authorize(Roles="Practicante,Colaborador")]
        [HttpGet]
        public async Task<IActionResult> Resumen(Guid idCurso)
        {
            var usuarioId = User.GetUserIdGuid()!.Value;
            var resumen = await _repo.GetResumenAsistenciaUsuarioAsync(idCurso, usuarioId);
            ViewBag.CursoId = idCurso;
            ViewBag.Resumen = resumen;
            return View();
        }

        // Practicante/Colaborador: historial día a día
        [Authorize(Roles="Practicante,Colaborador")]
        [HttpGet]
        public async Task<IActionResult> Historial(Guid idCurso)
        {
            var usuarioId = User.GetUserIdGuid()!.Value;
            var lista = await _repo.ListarAsistenciasPorUsuarioAsync(idCurso, usuarioId);
            ViewBag.CursoId = idCurso;
            ViewBag.Historial = lista;
            return View();
        }
    }
}
