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
        private readonly Campus_Virtul_GRLL.Services.StorageService _storage;

        public PracticanteController(SupabaseRepository repo, Campus_Virtul_GRLL.Services.StorageService storage)
        {
            _repo = repo;
            _storage = storage;
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
        public async Task<IActionResult> Detalle(Guid id, Guid? subId)
        {
            var cursos = await _repo.GetCursosAsync();
            var c = cursos.FirstOrDefault(x => x.id == id);
            if (c.id == Guid.Empty) return NotFound();

            // Cargar sesiones y subsecciones (solo publicadas)
            var sesiones = await _repo.GetSesionesPorCursoAsync(id);
            var sesionesVm = new System.Collections.Generic.List<Models.ViewModels.SesionVm>();
            foreach (var s in sesiones.OrderBy(x => x.orden))
            {
                var subs = await _repo.GetSubseccionesPorSesionAsync(s.id);
                var publicados = subs
                    .Where(ss => string.Equals(ss.estado, "publicado", System.StringComparison.OrdinalIgnoreCase))
                    .OrderBy(ss => ss.orden)
                    .ToList();
                var sVm = new Models.ViewModels.SesionVm
                {
                    Id = s.id,
                    Titulo = s.titulo,
                    Orden = s.orden,
                    Subsecciones = publicados.Select(p => new Models.ViewModels.SubResumenVm
                    {
                        Id = p.id,
                        Titulo = p.titulo,
                        Tipo = p.tipo,
                        Orden = p.orden,
                        FechaLimite = p.fechaLimite
                    }).ToList()
                };
                sesionesVm.Add(sVm);
            }

            // Elegir subsección seleccionada (la primera publicada por defecto)
            var firstPublished = sesionesVm
                .OrderBy(s => s.Orden)
                .SelectMany(s => s.Subsecciones.OrderBy(z => z.Orden))
                .FirstOrDefault();
            var selectedId = subId.HasValue && sesionesVm.Any(s => s.Subsecciones.Any(z => z.Id == subId.Value))
                ? subId.Value
                : (firstPublished != null ? firstPublished.Id : Guid.Empty);

            Models.ViewModels.SubDetalleVm? detalle = null;
            if (selectedId != Guid.Empty)
            {
                // Consultar detalle completo de la subsección
                Guid sesionId = Guid.Empty;
                string titulo = string.Empty;
                string tipo = "contenido";
                string estado = "publicado";
                string? texto = null;
                string? archivoUrl = null; string? archivoMime = null; long? archivoSize = null;
                string? videoUrl = null; string? videoMime = null; long? videoSize = null; int? videoDuracion = null; System.DateTimeOffset? fechaLimite = null; int? maxPuntaje = null;

                try
                {
                    var csb = new Npgsql.NpgsqlConnectionStringBuilder
                    {
                        Host = System.Environment.GetEnvironmentVariable("SUPABASE_DB_HOST"),
                        Database = System.Environment.GetEnvironmentVariable("SUPABASE_DB_NAME") ?? "postgres",
                        Username = System.Environment.GetEnvironmentVariable("SUPABASE_DB_USER") ?? "postgres",
                        Password = System.Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD"),
                        SslMode = Npgsql.SslMode.Require,
                        TrustServerCertificate = true
                    };
                    using var conn = new Npgsql.NpgsqlConnection(csb.ConnectionString);
                    await conn.OpenAsync();
                    using var cmd = new Npgsql.NpgsqlCommand(@"select s.sesion_id, s.titulo, s.tipo, s.estado, s.texto_contenido,
                                                                                s.archivo_url, s.archivo_mime, s.archivo_size_bytes,
                                                                                s.video_url, s.video_mime, s.video_size_bytes, s.video_duracion_segundos,
                                                                                s.fecha_limite, s.max_puntaje
                                                                            from public.subsecciones s where s.id=@id limit 1", conn);
                    cmd.Parameters.AddWithValue("id", selectedId);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        sesionId = r.GetGuid(0);
                        titulo = r.GetString(1);
                        tipo = r.GetString(2);
                        estado = r.GetString(3);
                        texto = r.IsDBNull(4) ? null : r.GetString(4);
                        archivoUrl = r.IsDBNull(5) ? null : r.GetString(5);
                        archivoMime = r.IsDBNull(6) ? null : r.GetString(6);
                        archivoSize = r.IsDBNull(7) ? (long?)null : r.GetInt64(7);
                        videoUrl = r.IsDBNull(8) ? null : r.GetString(8);
                        videoMime = r.IsDBNull(9) ? null : r.GetString(9);
                        videoSize = r.IsDBNull(10) ? (long?)null : r.GetInt64(10);
                        videoDuracion = r.IsDBNull(11) ? (int?)null : r.GetInt32(11);
                        fechaLimite = r.IsDBNull(12) ? (System.DateTimeOffset?)null : new System.DateTimeOffset(r.GetDateTime(12), System.TimeSpan.Zero);
                        maxPuntaje = r.IsDBNull(13) ? (int?)null : r.GetInt32(13);
                    }
                }
                catch
                {
                    // Ignorar: se mostrará sin recursos si falla
                }

                // Generar signed URLs si aplica
                string? archivoSigned = null;
                if (!string.IsNullOrWhiteSpace(archivoUrl))
                {
                    var su = await _storage.GetSignedUrlAsync("course-assets", archivoUrl);
                    archivoSigned = su.ok ? su.signedUrl : (archivoUrl.StartsWith("http", System.StringComparison.OrdinalIgnoreCase) ? archivoUrl : null);
                }
                string? videoSigned = null;
                if (!string.IsNullOrWhiteSpace(videoUrl))
                {
                    var su = await _storage.GetSignedUrlAsync("course-videos", videoUrl);
                    videoSigned = su.ok ? su.signedUrl : null;
                }

                detalle = new Models.ViewModels.SubDetalleVm
                {
                    Id = selectedId,
                    SesionId = sesionId,
                    Titulo = titulo,
                    Tipo = tipo,
                    Estado = estado,
                    Texto = texto,
                    ArchivoUrlSigned = archivoSigned,
                    ArchivoMime = archivoMime,
                    ArchivoSize = archivoSize,
                    VideoUrlSigned = videoSigned,
                    VideoMime = videoMime,
                    VideoSize = videoSize,
                    VideoDuracion = videoDuracion,
                    FechaLimite = fechaLimite,
                    MaxPuntaje = maxPuntaje
                };
            }

            var vm = new Campus_Virtul_GRLL.Models.ViewModels.CursoPlayerVm
            {
                CursoId = c.id,
                Titulo = c.titulo,
                Descripcion = c.descripcion,
                Sesiones = sesionesVm,
                Seleccionada = detalle
            };
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
