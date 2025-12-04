using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Campus_Virtul_GRLL.Helpers;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net.Mime;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize]
    public class CursosController : Controller
    {
        private readonly SupabaseRepository _repo;
        private readonly ILogger<CursosController> _logger;
        private readonly StorageService _storage;

        public CursosController(SupabaseRepository repo, ILogger<CursosController> logger, StorageService storage)
        {
            _repo = repo;
            _logger = logger;
            _storage = storage;
        }

        private string GetDetalleActionForCurrentUser()
        {
            var rol = User.GetUserRole();
            return string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase)
                ? "DetalleAdministrador"
                : "DetalleProfesor";
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
            var sesionesConSub = new List<(Guid sesionId, string titulo, int orden, List<(Guid id, string titulo, string tipo, string estado, int orden, DateTimeOffset? fechaLimite)> subsecciones)>();
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
            var sesionesConSub = new List<(Guid sesionId, string titulo, int orden, List<(Guid id, string titulo, string tipo, string estado, int orden, DateTimeOffset? fechaLimite)> subsecciones)>();
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

        [Authorize(Roles = "Profesor,Administrador")]
        [HttpGet]
        public async Task<IActionResult> VerSubseccion(Guid idCurso, Guid subseccionId)
        {
            // Cargar curso para encabezado y validar pertenencia
            var cursos = await _repo.GetCursosAsync();
            var curso = cursos.FirstOrDefault(c => c.id == idCurso);
            if (curso.id == Guid.Empty) return NotFound();
            ViewBag.Curso = curso;

            // Buscar subsección y su sesión
            // Para eficiencia, consultar directamente
            Guid sesionId = Guid.Empty;
            string titulo = "";
            string tipo = "contenido";
            string estado = "borrador";
            string? texto = null;
            string? archivoUrl = null; string? archivoMime = null; long? archivoSize = null;
            string? videoUrl = null; string? videoMime = null; long? videoSize = null; int? videoDuracion = null; DateTimeOffset? fechaLimite = null; int? maxPuntaje = null;

            try
            {
                using var conn = new Npgsql.NpgsqlConnection(new Npgsql.NpgsqlConnectionStringBuilder
                {
                    Host = Environment.GetEnvironmentVariable("SUPABASE_DB_HOST"),
                    Database = Environment.GetEnvironmentVariable("SUPABASE_DB_NAME") ?? "postgres",
                    Username = Environment.GetEnvironmentVariable("SUPABASE_DB_USER") ?? "postgres",
                    Password = Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD"),
                    SslMode = Npgsql.SslMode.Require,
                }.ConnectionString);
                await conn.OpenAsync();
                     using var cmd = new Npgsql.NpgsqlCommand(@"select s.sesion_id, s.titulo, s.tipo, s.estado, s.texto_contenido,
                                                                                 s.archivo_url, s.archivo_mime, s.archivo_size_bytes,
                                                                                 s.video_url, s.video_mime, s.video_size_bytes, s.video_duracion_segundos,
                                                                                 s.fecha_limite, s.max_puntaje
                                                                             from public.subsecciones s where s.id=@id limit 1", conn);
                cmd.Parameters.AddWithValue("id", subseccionId);
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return NotFound();
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
                fechaLimite = r.IsDBNull(12) ? (DateTimeOffset?)null : new DateTimeOffset(r.GetDateTime(12), TimeSpan.Zero);
                maxPuntaje = r.IsDBNull(13) ? (int?)null : r.GetInt32(13);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo subsección {SubId}", subseccionId);
                return StatusCode(500);
            }

            ViewBag.Subseccion = (subseccionId, sesionId, titulo, tipo, estado, texto, archivoUrl, archivoMime, archivoSize, videoUrl, videoMime, videoSize, videoDuracion);
            ViewBag.FechaLimite = fechaLimite;
            ViewBag.MaxPuntaje = maxPuntaje;

            // Generar signed URLs si hay recursos
            string? archivoSigned = null;
            if (!string.IsNullOrWhiteSpace(archivoUrl))
            {
                var su = await _storage.GetSignedUrlAsync("course-assets", archivoUrl);
                if (su.ok) archivoSigned = su.signedUrl;
                // Fallback a URL externa directa si no es un objeto de Storage
                if (string.IsNullOrWhiteSpace(archivoSigned) && (archivoUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || archivoUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    archivoSigned = archivoUrl;
                }
            }
            string? videoSigned = null;
            if (!string.IsNullOrWhiteSpace(videoUrl))
            {
                var su = await _storage.GetSignedUrlAsync("course-videos", videoUrl);
                if (su.ok) videoSigned = su.signedUrl;
            }
            ViewBag.ArchivoSigned = archivoSigned;
            ViewBag.VideoSigned = videoSigned;

            // Elegir vista de detalle
            return View("SubseccionDetalle");
        }

        // Solicitar inscripción (Practicante)
        [Authorize(Roles = "Practicante")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SolicitarInscripcion(Guid idCurso)
        {
            // TODO: implementar inscripciones en Supabase
            TempData["Mensaje"] = "Solicitud enviada";
            return RedirectToAction("MisCursos");
        }

        // Publicar curso
        [Authorize(Roles = "Profesor,Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publicar(Guid idCurso)
        {
            await _repo.UpdateCursoEstadoAsync(idCurso, "publicado");
            TempData["Mensaje"] = "Curso publicado";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        [Authorize(Roles = "Profesor,Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Borrador(Guid idCurso)
        {
            await _repo.UpdateCursoEstadoAsync(idCurso, "borrador");
            TempData["Mensaje"] = "Curso marcado como borrador";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarProfesor(Guid idCurso, Guid profesorId)
        {
            await _repo.AssignProfesorACursoAsync(idCurso, profesorId);
            TempData["Mensaje"] = "Profesor asignado";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetirarProfesor(Guid idCurso, Guid profesorId)
        {
            await _repo.RemoveProfesorDeCursoAsync(idCurso, profesorId);
            TempData["Mensaje"] = "Profesor retirado";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearSesion(Guid idCurso, string titulo, int? orden)
        {
            if (string.IsNullOrWhiteSpace(titulo))
            {
                TempData["Error"] = "El título de la sesión es obligatorio";
                return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
            }
            await _repo.CreateSesionAsync(idCurso, titulo.Trim(), orden);
            TempData["Mensaje"] = "Sesión creada";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearSubseccion(Guid idCurso, Guid sesionId, string titulo, string tipo, string? textoContenido, List<IFormFile>? archivos, IFormFile? video, int? maxPuntaje)
        {
            if (string.IsNullOrWhiteSpace(titulo))
            {
                TempData["Error"] = "El título de la subsección es obligatorio";
                return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
            }
            var tipoValRaw = tipo ?? "contenido";
            var tipoVal = tipoValRaw.Trim().ToLowerInvariant();
            // Normalizar posibles variantes (acentos, mayúsculas, espacios extras)
            tipoVal = tipoVal switch
            {
                "contenido" => "contenido",
                "video" => "video",
                "tarea" => "tarea",
                _ => "contenido"
            };

            // Soporte de múltiples adjuntos para tipos no-video
            var adjuntos = new List<(string url, string? mime, long? size)>();

            string? videoUrl = null;
            string? videoMime = null;
            long? videoSize = null;
            int? videoDuracionSegundos = null; // Si luego calculamos duración

            // Validaciones y subida a Storage (múltiples adjuntos para contenido/tarea)
            if (archivos != null && archivos.Count > 0)
            {
                if (tipoVal == "video")
                {
                    TempData["Error"] = "Para tipo 'video' no adjuntes archivos; usa el campo de video.";
                    return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
                }
                foreach (var archivo in archivos.Where(a => a != null && a.Length > 0))
                {
                    var archivoMime = archivo.ContentType;
                    var archivoSize = archivo.Length;
                    if (archivoSize > 50_000_000) // 50MB por archivo
                    {
                        TempData["Error"] = "Uno de los archivos excede 50MB.";
                        return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
                    }
                    var objPath = $"{idCurso}/sesiones/{sesionId}/assets/{Guid.NewGuid()}_{Path.GetFileName(archivo.FileName)}";
                    using var stream = archivo.OpenReadStream();
                    var up = await _storage.UploadAsync("course-assets", objPath, stream, archivoMime ?? MediaTypeNames.Application.Octet);
                    if (!up.ok)
                    {
                        TempData["Error"] = up.error ?? "Error subiendo archivo";
                        return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
                    }
                    adjuntos.Add((objPath, archivoMime, archivoSize));
                }
            }

            if (video != null && video.Length > 0)
            {
                if (tipoVal != "video")
                {
                    TempData["Error"] = "Solo puede subir video cuando el tipo es 'video'.";
                    return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
                }
                videoMime = video.ContentType;
                videoSize = video.Length;
                if (!(new[]{"video/mp4","video/webm"}).Contains(videoMime ?? ""))
                {
                    TempData["Error"] = "Solo se permiten videos MP4 o WebM.";
                    return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
                }
                if (videoSize > 200_000_000) // 200MB
                {
                    TempData["Error"] = "El video excede 200MB.";
                    return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
                }
                var objPath = $"{idCurso}/sesiones/{sesionId}/videos/{Guid.NewGuid()}_{Path.GetFileName(video.FileName)}";
                using var stream = video.OpenReadStream();
                var up = await _storage.UploadAsync("course-videos", objPath, stream, videoMime ?? MediaTypeNames.Application.Octet);
                if (!up.ok)
                {
                    TempData["Error"] = up.error ?? "Error subiendo video";
                    return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
                }
                videoUrl = objPath;
            }

            // Restricción de puntaje
            if (tipoVal != "tarea") maxPuntaje = null;

            // Para compatibilidad, si hay adjuntos, guardamos el primero en las columnas de subsecciones
            string? archivoUrlPrimario = adjuntos.FirstOrDefault().url;
            string? archivoMimePrimario = adjuntos.FirstOrDefault().mime;
            long? archivoSizePrimario = adjuntos.FirstOrDefault().size;

            if (tipoVal == "tarea" && (!maxPuntaje.HasValue || maxPuntaje.Value <= 0))
            {
                maxPuntaje = 20;
            }
            var subseccionId = await _repo.CreateSubseccionAsync(sesionId, titulo.Trim(), tipoVal, textoContenido, archivoUrlPrimario, videoUrl, maxPuntaje, "borrador", null, null);
            // Actualizar metadata de archivo/video
            await _repo.UpdateSubseccionAsync(subseccionId,
                nuevoArchivoUrl: archivoUrlPrimario,
                nuevoArchivoMime: archivoMimePrimario,
                nuevoArchivoSizeBytes: archivoSizePrimario,
                nuevoVideoUrl: videoUrl,
                nuevoVideoMime: videoMime,
                nuevoVideoSizeBytes: videoSize,
                nuevoVideoDuracionSegundos: videoDuracionSegundos);

            // Insertar adjuntos adicionales en tabla subseccion_adjuntos
            if (adjuntos.Count > 0)
            {
                try
                {
                    using var conn = new Npgsql.NpgsqlConnection(new Npgsql.NpgsqlConnectionStringBuilder
                    {
                        Host = Environment.GetEnvironmentVariable("SUPABASE_DB_HOST"),
                        Database = Environment.GetEnvironmentVariable("SUPABASE_DB_NAME") ?? "postgres",
                        Username = Environment.GetEnvironmentVariable("SUPABASE_DB_USER") ?? "postgres",
                        Password = Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD"),
                        SslMode = Npgsql.SslMode.Require,
                    }.ConnectionString);
                    await conn.OpenAsync();
                    foreach (var a in adjuntos)
                    {
                        using var cmd = new Npgsql.NpgsqlCommand("INSERT INTO public.subseccion_adjuntos (subseccion_id, objeto_url, mime, size_bytes) VALUES (@sid, @url, @mime, @size)", conn);
                        cmd.Parameters.AddWithValue("sid", subseccionId);
                        cmd.Parameters.AddWithValue("url", a.url);
                        cmd.Parameters.AddWithValue("mime", (object?)a.mime ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("size", (object?)a.size ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error insertando adjuntos adicionales para subsección {SubId}", subseccionId);
                }
            }

            TempData["Mensaje"] = "Subsección creada (borrador)";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarSesion(Guid idCurso, Guid sesionId, string? titulo, int? orden)
        {
            await _repo.UpdateSesionAsync(sesionId, string.IsNullOrWhiteSpace(titulo)? null : titulo.Trim(), orden);
            TempData["Mensaje"] = "Sesión actualizada";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSesion(Guid idCurso, Guid sesionId)
        {
            await _repo.DeleteSesionAsync(sesionId);
            TempData["Mensaje"] = "Sesión eliminada";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarSubseccion(Guid idCurso, Guid subseccionId, string? titulo, string? tipo, string? textoContenido, int? maxPuntaje, int? orden)
        {
            var tipoVal = string.IsNullOrWhiteSpace(tipo)? null : tipo!.Trim().ToLowerInvariant();
            if (tipoVal != null && !(new[]{"contenido","video","tarea"}).Contains(tipoVal)) tipoVal = null; // Dejar null para no cambiar si es inválido
            if (tipoVal != "tarea") maxPuntaje = null;
            await _repo.UpdateSubseccionAsync(subseccionId, nuevoTitulo: string.IsNullOrWhiteSpace(titulo)? null : titulo!.Trim(), nuevoTipo: tipoVal, nuevoTextoContenido: textoContenido, nuevoMaxPuntaje: maxPuntaje, nuevoOrden: orden);
            TempData["Mensaje"] = "Subsección actualizada";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSubseccion(Guid idCurso, Guid subseccionId, string? tipo)
        {
            // Borrar objetos del Storage asociados (archivo(s) y/o video) antes de eliminar la fila
            try
            {
                // 1) Obtener archivo_url y video_url primarios de subsecciones
                string? archivoUrl = null; string? videoUrl = null;
                using (var conn = new Npgsql.NpgsqlConnection(new Npgsql.NpgsqlConnectionStringBuilder
                {
                    Host = Environment.GetEnvironmentVariable("SUPABASE_DB_HOST"),
                    Database = Environment.GetEnvironmentVariable("SUPABASE_DB_NAME") ?? "postgres",
                    Username = Environment.GetEnvironmentVariable("SUPABASE_DB_USER") ?? "postgres",
                    Password = Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD"),
                    SslMode = Npgsql.SslMode.Require,
                }.ConnectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new Npgsql.NpgsqlCommand("SELECT archivo_url, video_url FROM public.subsecciones WHERE id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("id", subseccionId);
                        using var r = await cmd.ExecuteReaderAsync();
                        if (await r.ReadAsync())
                        {
                            archivoUrl = r.IsDBNull(0) ? null : r.GetString(0);
                            videoUrl = r.IsDBNull(1) ? null : r.GetString(1);
                        }
                    }

                    // 2) Obtener adjuntos adicionales
                    var adjuntos = new List<string>();
                    using (var cmd2 = new Npgsql.NpgsqlCommand("SELECT objeto_url FROM public.subseccion_adjuntos WHERE subseccion_id=@id", conn))
                    {
                        cmd2.Parameters.AddWithValue("id", subseccionId);
                        using var r2 = await cmd2.ExecuteReaderAsync();
                        while (await r2.ReadAsync())
                        {
                            if (!r2.IsDBNull(0)) adjuntos.Add(r2.GetString(0));
                        }
                    }

                    // 3) Borrar assets del bucket correspondiente
                    // Primario
                    if (!string.IsNullOrWhiteSpace(archivoUrl))
                    {
                        await _storage.DeleteAsync("course-assets", archivoUrl);
                    }
                    // Adjuntos
                    foreach (var aurl in adjuntos)
                    {
                        await _storage.DeleteAsync("course-assets", aurl);
                    }
                    // Video
                    if (!string.IsNullOrWhiteSpace(videoUrl))
                    {
                        await _storage.DeleteAsync("course-videos", videoUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando objetos de Storage para subsección {SubId}", subseccionId);
            }

            // Finalmente borramos la fila; adjuntos se eliminan por cascada
            await _repo.DeleteSubseccionAsync(subseccionId);
            TempData["Mensaje"] = "Subsección eliminada";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstadoSubseccion(Guid idCurso, Guid subseccionId, string nuevoEstado)
        {
            await _repo.UpdateSubseccionEstadoAsync(subseccionId, nuevoEstado);
            TempData["Mensaje"] = "Estado de subsección actualizado";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        // Crear sesión rápida (título y orden automáticos)
        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearSesionRapida(Guid idCurso)
        {
            var sesiones = await _repo.GetSesionesPorCursoAsync(idCurso);
            var nextOrden = (sesiones.Count == 0) ? 1 : sesiones.Max(s => s.orden) + 1;
            var titulo = $"Sesión {nextOrden}";
            await _repo.CreateSesionAsync(idCurso, titulo, nextOrden);
            TempData["Mensaje"] = "Sesión creada";
            return RedirectToAction(GetDetalleActionForCurrentUser(), new { id = idCurso });
        }

        // Crear subsección rápida y redirigir al detalle para edición
        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearSubseccionRapida(Guid idCurso, Guid sesionId, string tipo)
        {
            var tipoVal = (tipo ?? "contenido").Trim().ToLowerInvariant();
            if (!(new[] { "contenido", "video", "tarea" }).Contains(tipoVal)) tipoVal = "contenido";
            var existentes = await _repo.GetSubseccionesPorSesionAsync(sesionId);
            var nextOrden = (existentes.Count == 0) ? 1 : existentes.Max(s => s.orden) + 1;
            var titulo = $"Subsección {nextOrden}";
            var defaultMax = tipoVal == "tarea" ? 20 : (int?)null;
            var subseccionId = await _repo.CreateSubseccionAsync(sesionId, titulo, tipoVal, null, null, null, defaultMax, "borrador", nextOrden, null);
            TempData["Mensaje"] = "Subsección creada";
            return RedirectToAction("VerSubseccion", new { idCurso, subseccionId });
        }

        // Revisar y calificar entregas de una subsección tipo tarea
        [Authorize(Roles="Profesor,Administrador")]
        [HttpGet]
        public async Task<IActionResult> RevisarTarea(Guid idCurso, Guid subseccionId)
        {
            // Validar curso
            var cursos = await _repo.GetCursosAsync();
            var curso = cursos.FirstOrDefault(c => c.id == idCurso);
            if (curso.id == Guid.Empty) return NotFound();
            ViewBag.Curso = curso;

            // Obtener tarea asociada (crear si falta)
            var tarea = await _repo.GetTareaPorSubseccionAsync(subseccionId);
            Guid tareaId;
            if (tarea == null)
            {
                tareaId = await _repo.EnsureTareaParaSubseccionAsync(subseccionId, "Entrega", null);
            }
            else
            {
                tareaId = tarea.Value.tareaId;
                ViewBag.FechaEntrega = tarea.Value.fechaEntrega;
                ViewBag.TareaTitulo = tarea.Value.titulo;
            }

            // Listar entregas
            var entregas = await _repo.ListarEntregasPorTareaAsync(tareaId);
            // Firmar URLs de archivos para que el botón "Abrir" funcione fuera de localhost
            var entregasFirmadas = new List<(Guid entregaId, Guid usuarioId, string practicanteNombre, string? urlArchivo, string? enlaceUrl, decimal? calificacion, string estado, DateTimeOffset entregadoEn, DateTimeOffset? calificadoEn)>();
            foreach (var e in entregas)
            {
                string? urlArchivoFirmada = e.urlArchivo;
                if (!string.IsNullOrWhiteSpace(e.urlArchivo) && !e.urlArchivo.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !e.urlArchivo.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var su = await _storage.GetSignedUrlAsync("course-assets", e.urlArchivo);
                    if (su.ok) urlArchivoFirmada = su.signedUrl;
                }
                entregasFirmadas.Add((e.entregaId, e.usuarioId, e.practicanteNombre, urlArchivoFirmada, e.enlaceUrl, e.calificacion, e.estado, e.entregadoEn, e.calificadoEn));
            }
            ViewBag.Entregas = entregasFirmadas;
            ViewBag.SubseccionId = subseccionId;
            return View("RevisarTarea");
        }

        [Authorize(Roles="Profesor,Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalificarEntrega(Guid idCurso, Guid subseccionId, Guid entregaId, decimal calificacion)
        {
            try
            {
                await _repo.CalificarEntregaAsync(entregaId, calificacion);
                TempData["Mensaje"] = "Entrega calificada";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("RevisarTarea", new { idCurso, subseccionId });
        }

        // Guardar cambios integrados desde el detalle de subsección
        [Authorize(Roles="Administrador,Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarSubseccion(
            Guid idCurso,
            Guid subseccionId,
            string? titulo,
            string? textoContenido,
            IFormFile? archivo,
            string? enlace,
            string? accionArchivo,
            IFormFile? video,
            DateTimeOffset? fechaLimite,
            int? maxPuntaje)
        {
            Guid sesionId; string tipo; string? archivoUrlAnterior; string? videoUrlAnterior;
            string? archivoMimeAnterior; long? archivoSizeAnterior; string? videoMimeAnterior; long? videoSizeAnterior; int? videoDuracionAnterior;
            try
            {
                using var conn = new Npgsql.NpgsqlConnection(new Npgsql.NpgsqlConnectionStringBuilder
                {
                    Host = Environment.GetEnvironmentVariable("SUPABASE_DB_HOST"),
                    Database = Environment.GetEnvironmentVariable("SUPABASE_DB_NAME") ?? "postgres",
                    Username = Environment.GetEnvironmentVariable("SUPABASE_DB_USER") ?? "postgres",
                    Password = Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD"),
                    SslMode = Npgsql.SslMode.Require,
                }.ConnectionString);
                await conn.OpenAsync();
                using var cmd = new Npgsql.NpgsqlCommand(@"select sesion_id, tipo, archivo_url, archivo_mime, archivo_size_bytes, video_url, video_mime, video_size_bytes, video_duracion_segundos from public.subsecciones where id=@id", conn);
                cmd.Parameters.AddWithValue("id", subseccionId);
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return NotFound();
                sesionId = r.GetGuid(0);
                tipo = r.GetString(1);
                archivoUrlAnterior = r.IsDBNull(2) ? null : r.GetString(2);
                archivoMimeAnterior = r.IsDBNull(3) ? null : r.GetString(3);
                archivoSizeAnterior = r.IsDBNull(4) ? (long?)null : r.GetInt64(4);
                videoUrlAnterior = r.IsDBNull(5) ? null : r.GetString(5);
                videoMimeAnterior = r.IsDBNull(6) ? null : r.GetString(6);
                videoSizeAnterior = r.IsDBNull(7) ? (long?)null : r.GetInt64(7);
                videoDuracionAnterior = r.IsDBNull(8) ? (int?)null : r.GetInt32(8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando subsección {SubId}", subseccionId);
                TempData["Error"] = "No se pudo cargar la subsección";
                return RedirectToAction("VerSubseccion", new { idCurso, subseccionId });
            }

            string? nuevoArchivoUrl = archivoUrlAnterior; string? nuevoArchivoMime = archivoMimeAnterior; long? nuevoArchivoSize = archivoSizeAnterior;
            string? nuevoVideoUrl = videoUrlAnterior; string? nuevoVideoMime = videoMimeAnterior; long? nuevoVideoSize = videoSizeAnterior; int? nuevoVideoDuracion = videoDuracionAnterior;

            if (tipo == "contenido" || tipo == "tarea")
            {
                if (!string.IsNullOrWhiteSpace(accionArchivo))
                {
                    if (accionArchivo == "eliminar")
                    {
                        if (!string.IsNullOrWhiteSpace(archivoUrlAnterior) && archivoUrlAnterior!.Contains("/sesiones/") && !archivoUrlAnterior.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            await _storage.DeleteAsync("course-assets", archivoUrlAnterior);
                        }
                        nuevoArchivoUrl = null; nuevoArchivoMime = null; nuevoArchivoSize = null;
                    }
                    else if (accionArchivo == "reemplazarArchivo")
                    {
                        if (archivo == null || archivo.Length == 0)
                        {
                            TempData["Error"] = "Selecciona un PDF para reemplazar.";
                            return RedirectToAction("VerSubseccion", new { idCurso, subseccionId });
                        }
                        if (!string.IsNullOrWhiteSpace(archivoUrlAnterior) && archivoUrlAnterior!.Contains("/sesiones/") && !archivoUrlAnterior.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            await _storage.DeleteAsync("course-assets", archivoUrlAnterior);
                        }
                        var mime = archivo.ContentType; var size = archivo.Length;
                        if (size > 50_000_000)
                        {
                            TempData["Error"] = "El archivo excede 50MB.";
                            return RedirectToAction("VerSubseccion", new { idCurso, subseccionId });
                        }
                        var objPath = $"{idCurso}/sesiones/{sesionId}/assets/{Guid.NewGuid()}_{Path.GetFileName(archivo.FileName)}";
                        using var stream = archivo.OpenReadStream();
                        var up = await _storage.UploadAsync("course-assets", objPath, stream, mime ?? MediaTypeNames.Application.Octet);
                        if (!up.ok)
                        {
                            TempData["Error"] = up.error ?? "Error subiendo archivo";
                            return RedirectToAction("VerSubseccion", new { idCurso, subseccionId });
                        }
                        nuevoArchivoUrl = objPath; nuevoArchivoMime = mime; nuevoArchivoSize = size;
                    }
                    else if (accionArchivo == "reemplazarEnlace")
                    {
                        if (string.IsNullOrWhiteSpace(enlace))
                        {
                            TempData["Error"] = "Ingresa un enlace válido.";
                            return RedirectToAction("VerSubseccion", new { idCurso, subseccionId });
                        }
                        if (!string.IsNullOrWhiteSpace(archivoUrlAnterior) && archivoUrlAnterior!.Contains("/sesiones/") && !archivoUrlAnterior.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            await _storage.DeleteAsync("course-assets", archivoUrlAnterior);
                        }
                        nuevoArchivoUrl = enlace!.Trim(); nuevoArchivoMime = null; nuevoArchivoSize = null;
                    }
                }
            }

            if (tipo == "video")
            {
                if (!string.IsNullOrWhiteSpace(accionArchivo))
                {
                    if (accionArchivo == "eliminar" && !string.IsNullOrWhiteSpace(videoUrlAnterior))
                    {
                        await _storage.DeleteAsync("course-videos", videoUrlAnterior!);
                        nuevoVideoUrl = null; nuevoVideoMime = null; nuevoVideoSize = null; nuevoVideoDuracion = null;
                    }
                    else if (accionArchivo == "reemplazarArchivo")
                    {
                        if (video == null || video.Length == 0)
                        {
                            TempData["Error"] = "Selecciona un video para reemplazar.";
                            return RedirectToAction("VerSubseccion", new { idCurso, subseccionId });
                        }
                        if (!string.IsNullOrWhiteSpace(videoUrlAnterior))
                        {
                            await _storage.DeleteAsync("course-videos", videoUrlAnterior!);
                        }
                        var mime = video.ContentType; var size = video.Length;
                        if (!(new[]{"video/mp4","video/webm"}).Contains(mime ?? ""))
                        {
                            TempData["Error"] = "Solo se permiten videos MP4 o WebM.";
                            return RedirectToAction("VerSubseccion", new { idCurso, subseccionId });
                        }
                        if (size > 200_000_000)
                        {
                            TempData["Error"] = "El video excede 200MB.";
                            return RedirectToAction("VerSubseccion", new { idCurso, subseccionId });
                        }
                        var objPath = $"{idCurso}/sesiones/{sesionId}/videos/{Guid.NewGuid()}_{Path.GetFileName(video.FileName)}";
                        using var stream = video.OpenReadStream();
                        var up = await _storage.UploadAsync("course-videos", objPath, stream, mime ?? MediaTypeNames.Application.Octet);
                        if (!up.ok)
                        {
                            TempData["Error"] = up.error ?? "Error subiendo video";
                            return RedirectToAction("VerSubseccion", new { idCurso, subseccionId });
                        }
                        nuevoVideoUrl = objPath; nuevoVideoMime = mime; nuevoVideoSize = size;
                    }
                }
            }

            // Solo tareas tienen max puntaje y fecha límite
            if (tipo != "tarea") { maxPuntaje = null; fechaLimite = null; }

            await _repo.UpdateSubseccionAsync(
                subseccionId,
                nuevoTitulo: string.IsNullOrWhiteSpace(titulo)? null : titulo!.Trim(),
                nuevoTextoContenido: textoContenido,
                nuevoArchivoUrl: nuevoArchivoUrl,
                nuevoArchivoMime: nuevoArchivoMime,
                nuevoArchivoSizeBytes: nuevoArchivoSize,
                nuevoVideoUrl: nuevoVideoUrl,
                nuevoVideoMime: nuevoVideoMime,
                nuevoVideoSizeBytes: nuevoVideoSize,
                nuevoVideoDuracionSegundos: nuevoVideoDuracion,
                nuevoMaxPuntaje: maxPuntaje,
                nuevaFechaLimite: fechaLimite
            );

            TempData["Mensaje"] = "Subsección guardada";
            return RedirectToAction("VerSubseccion", new { idCurso, subseccionId });
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
            var cursoId = await _repo.CreateCursoAsync(titulo.Trim(), string.IsNullOrWhiteSpace(descripcion)? null : descripcion.Trim(), estadoVal);
            // Crear 14 sesiones por defecto
            for (int i = 1; i <= 14; i++)
            {
                await _repo.CreateSesionAsync(cursoId, $"Sesión {i}", i);
            }
            TempData["Mensaje"] = "Curso creado con 14 sesiones";
            return RedirectToAction("Index");
        }
    }
}
