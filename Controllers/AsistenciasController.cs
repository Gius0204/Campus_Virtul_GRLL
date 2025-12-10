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
        public async Task<IActionResult> Tomar(Guid idCurso, DateOnly? fecha, Guid? asistenciaId)
        {
            var cursoId = idCurso;
            // Si viene asistenciaId, obtener su fecha exacta
            DateOnly f;
            if (asistenciaId.HasValue)
            {
                var asis = await _repo.GetAsistenciaPorIdAsync(asistenciaId.Value);
                f = asis?.fecha ?? (fecha ?? DateOnly.FromDateTime(DateTime.UtcNow.Date));
            }
            else
            {
                f = fecha ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
            }
            var participantesAll = await _repo.GetParticipantesCursoAsync(cursoId);
            var participantes = participantesAll.Where(p =>
                string.Equals(p.rol, "Practicante", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.rol, "Colaborador", StringComparison.OrdinalIgnoreCase)
            ).ToList();
            ViewBag.CursoId = cursoId;
            ViewBag.Fecha = f;
            ViewBag.Participantes = participantes;
            // Pasar horario del día seleccionado para visualización
            var horario = await _repo.GetHorarioCursoAsync(cursoId);
            var diaNombre = DiaTextoDeFecha(f);
            var h = horario.FirstOrDefault(x => string.Equals(x.dia, diaNombre, StringComparison.OrdinalIgnoreCase));
            if (h.inicio != default && h.fin != default)
            {
                ViewBag.HorarioInicio = h.inicio;
                ViewBag.HorarioFin = h.fin;
            }
            return View();
        }

        [Authorize(Roles="Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(Guid idCurso, DateOnly fecha, TimeOnly? horaInicio, TimeOnly? horaFin, List<Guid> usuarios, List<string> estados, List<string?> justificaciones)
        {
            Guid? asistenciaId = null;
            try
            {
                var profesorId = User.GetUserIdGuid()!.Value;
                // Si no se especifican horas, tomar del horario del curso para ese día
                if (horaInicio == null || horaFin == null)
                {
                    var horario = await _repo.GetHorarioCursoAsync(idCurso);
                    var diaNombre = DiaTextoDeFecha(fecha);
                    var h = horario.FirstOrDefault(x => string.Equals(x.dia, diaNombre, StringComparison.OrdinalIgnoreCase));
                    if (h.inicio != default && h.fin != default)
                    {
                        horaInicio ??= TimeOnly.FromTimeSpan(h.inicio);
                        horaFin ??= TimeOnly.FromTimeSpan(h.fin);
                    }
                }
                asistenciaId = await _repo.UpsertAsistenciaAsync(idCurso, profesorId, fecha, horaInicio, horaFin);
                for (int i = 0; i < usuarios.Count; i++)
                {
                    var uid = usuarios[i];
                    var est = i < estados.Count ? estados[i] : "ausente";
                    var jus = (i < justificaciones.Count ? justificaciones[i] : null);
                    await _repo.SetAsistenciaDetalleAsync(asistenciaId.Value, uid, est, jus);
                }
                TempData["Mensaje"] = "Asistencia registrada";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando asistencia");
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Tomar", new { idCurso, asistenciaId });
        }

        private static string DiaTextoDeFecha(DateOnly fecha)
        {
            // Mapear al texto en español para coincidir con horario
            return fecha.DayOfWeek switch
            {
                DayOfWeek.Monday => "lunes",
                DayOfWeek.Tuesday => "martes",
                DayOfWeek.Wednesday => "miercoles",
                DayOfWeek.Thursday => "jueves",
                DayOfWeek.Friday => "viernes",
                DayOfWeek.Saturday => "sabado",
                DayOfWeek.Sunday => "domingo",
                _ => "lunes"
            };
        }

        // ---- Configurar horario del curso ----
        [Authorize(Roles="Profesor")]
        [HttpGet]
        public async Task<IActionResult> Configurar(Guid idCurso)
        {
            ViewBag.CursoId = idCurso;
            var horario = await _repo.GetHorarioCursoAsync(idCurso);
            ViewBag.Horario = horario;
            return View();
        }

        [Authorize(Roles="Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarHorario(Guid idCurso, string[] dias, string[] inicio, string[] fin)
        {
            try
            {
                if (dias == null || inicio == null || fin == null || dias.Length != inicio.Length || dias.Length != fin.Length)
                    throw new ArgumentException("Entradas de horario inválidas");
                // Validar: no permitir quitar días que ya tienen asistencias
                var diasNuevos = dias.Select(d => d?.Trim().ToLowerInvariant()).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToHashSet();
                var diasConAsistencias = await _repo.GetDiasConAsistenciasAsync(idCurso);
                var eliminadosProhibidos = diasConAsistencias.Where(d => !diasNuevos.Contains(d)).ToList();
                if (eliminadosProhibidos.Any())
                {
                    TempData["Error"] = "No se puede quitar estos días porque ya tienen asistencias: " + string.Join(", ", eliminadosProhibidos);
                    return RedirectToAction("DetalleProfesor", "Cursos", new { id = idCurso });
                }

                await _repo.ReplaceHorarioCursoAsync(idCurso, dias, inicio, fin);
                TempData["Mensaje"] = "Horario actualizado";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando horario");
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("DetalleProfesor", "Cursos", new { id = idCurso });
        }

        // Crear asistencia (fecha debe corresponder a un día permitido)
        [Authorize(Roles="Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Guid idCurso, DateTime fecha)
        {
            try
            {
                var profesorId = User.GetUserIdGuid()!.Value;
                await _repo.CrearAsistenciaAsync(idCurso, fecha, profesorId);
                TempData["Mensaje"] = "Asistencia creada";
            }
            catch (Npgsql.PostgresException pgx) when (pgx.SqlState == "P0001")
            {
                _logger.LogWarning(pgx, "Intento de crear asistencia en día no permitido");
                TempData["Error"] = "No se pudo crear la asistencia: la fecha escogida no coincide con los días del horario del curso.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando asistencia");
                TempData["Error"] = "Ocurrió un error creando la asistencia.";
            }
            return RedirectToAction("DetalleProfesor", "Cursos", new { id = idCurso, tab = "asistencias" });
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
