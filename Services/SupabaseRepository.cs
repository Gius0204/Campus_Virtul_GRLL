using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Campus_Virtul_GRLL.Helpers;

namespace Campus_Virtul_GRLL.Services
{
    public class SupabaseRepository
    {
        private readonly string? _host;
        private readonly string _dbName;
        private readonly string _user;
        private readonly string? _password;
        private readonly int _port;

        public SupabaseRepository()
        {
            _host = Environment.GetEnvironmentVariable("SUPABASE_DB_HOST");
            _dbName = Environment.GetEnvironmentVariable("SUPABASE_DB_NAME") ?? "postgres";
            _user = Environment.GetEnvironmentVariable("SUPABASE_DB_USER") ?? "postgres";
            _password = Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD");
            _port = int.TryParse(Environment.GetEnvironmentVariable("SUPABASE_DB_PORT"), out var p) ? p : 5432;
        }

        private NpgsqlConnection CreateConnection()
        {
            if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_password))
                throw new InvalidOperationException("Variables de entorno de Supabase DB incompletas.");
            var csb = new NpgsqlConnectionStringBuilder
            {
                Host = _host,
                Port = _port,
                Database = _dbName,
                Username = _user,
                Password = _password,
                SslMode = SslMode.Require,
                TrustServerCertificate = true
            };
            return new NpgsqlConnection(csb.ConnectionString);
        }

        public async Task<int> GetRolesCountAsync()
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("select count(*) from public.roles", conn);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task EnsureDemoUserAsync()
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            // Buscar rol Administrador
            Guid? rolAdminId = null;
            using (var cmdRol = new NpgsqlCommand("select id from public.roles where nombre = 'Administrador' limit 1", conn))
            {
                var r = await cmdRol.ExecuteScalarAsync();
                if (r != null && r is Guid g) rolAdminId = g;
            }
            if (rolAdminId == null) return; // No hay rol admin

            // Verificar si existe un usuario demo
            using (var cmdCheck = new NpgsqlCommand("select 1 from public.usuarios where correo = 'admin@demo.local'", conn))
            {
                var exists = await cmdCheck.ExecuteScalarAsync();
                if (exists != null)
                    return; // Ya existe
            }

            using (var cmdIns = new NpgsqlCommand(@"insert into public.usuarios (nombres, correo, activo, rol_id) values ('Admin Demo','admin@demo.local', true, @rid)", conn))
            {
                cmdIns.Parameters.AddWithValue("rid", rolAdminId);
                await cmdIns.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<(Guid id, string nombres, string correo, bool activo, Guid rolId, string rolNombre)>> GetUsuariosAsync()
        {
            var list = new List<(Guid, string, string, bool, Guid, string)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select u.id, u.nombres, coalesce(u.correo,''), u.activo, u.rol_id, r.nombre from public.usuarios u join public.roles r on r.id = u.rol_id order by u.creado_en desc", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetGuid(0);
                var nombres = reader.GetString(1);
                var correo = reader.GetString(2);
                var activo = reader.GetBoolean(3);
                var rolId = reader.GetGuid(4);
                var rolNombre = reader.GetString(5);
                list.Add((id, nombres, correo, activo, rolId, rolNombre));
            }
            return list;
        }

        public async Task<List<(Guid id, string nombre)>> GetRolesAsync()
        {
            var list = new List<(Guid, string)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("select id, nombre from public.roles order by nombre", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetGuid(0), reader.GetString(1)));
            }
            return list;
        }

        public async Task<List<(Guid id, string nombre)>> GetAreasAsync()
        {
            var list = new List<(Guid, string)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("select id, nombre from public.areas order by nombre", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetGuid(0), reader.GetString(1)));
            }
            return list;
        }

        public async Task CambiarRolUsuarioAsync(Guid usuarioId, Guid nuevoRolId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("update public.usuarios set rol_id=@r, actualizado_en=now() where id=@id", conn);
            cmd.Parameters.AddWithValue("r", nuevoRolId);
            cmd.Parameters.AddWithValue("id", usuarioId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ToggleEstadoUsuarioAsync(Guid usuarioId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("update public.usuarios set activo = not activo, actualizado_en=now() where id=@id", conn);
            cmd.Parameters.AddWithValue("id", usuarioId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task InitializeAsync()
        {
            await EnsureDemoUserAsync();
            await EnsureAdminFromEnvAsync();
        }

        public async Task<(Guid id, string nombres, string? apellidos, string? dni, string? telefono, Guid? areaId, string correo, string? passwordHash, bool activo, Guid rolId, string rolNombre)?> GetUserByEmailAsync(string correo)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("select u.id, u.nombres, u.apellidos, u.dni, u.telefono, u.area_id, u.correo, u.password_hash, u.activo, u.rol_id, r.nombre from public.usuarios u join public.roles r on r.id=u.rol_id where u.correo=@c limit 1", conn);
            cmd.Parameters.AddWithValue("c", correo);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5),
                    reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.GetBoolean(8),
                    reader.GetGuid(9),
                    reader.GetString(10)
                );
            }
            return null;
        }

        public async Task<Guid> CreateUserAsync(string nombres, string correo, Guid rolId, bool activo, string? plainPassword, string? apellidos = null, string? dni = null, string? telefono = null, Guid? areaId = null)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            var hash = plainPassword != null ? PasswordHelper.Hash(plainPassword) : null;
            using var cmd = new NpgsqlCommand(@"insert into public.usuarios (id, nombres, apellidos, dni, telefono, area_id, correo, activo, rol_id, password_hash) values (gen_random_uuid(), @n, @ap, @dni, @tel, @arid, @c, @a, @r, @p) returning id", conn);
            cmd.Parameters.AddWithValue("n", nombres);
            cmd.Parameters.AddWithValue("ap", (object?)apellidos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("dni", (object?)dni ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tel", (object?)telefono ?? DBNull.Value);
            cmd.Parameters.AddWithValue("arid", (object?)areaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("c", (object?)correo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("a", activo);
            cmd.Parameters.AddWithValue("r", rolId);
            cmd.Parameters.AddWithValue("p", (object?)hash ?? DBNull.Value);
            var idObj = await cmd.ExecuteScalarAsync();
            return (Guid)idObj!;
        }

        public async Task UpdateUserPasswordAsync(Guid userId, string newPlainPassword)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            var hash = PasswordHelper.Hash(newPlainPassword);
            using var cmd = new NpgsqlCommand("update public.usuarios set password_hash=@p, actualizado_en=now() where id=@id", conn)
            {
            };
            cmd.Parameters.AddWithValue("p", hash);
            cmd.Parameters.AddWithValue("id", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task EnsureAdminFromEnvAsync()
        {
            var adminEmail = Environment.GetEnvironmentVariable("SUPABASE_ADMIN_EMAIL");
            var adminPassword = Environment.GetEnvironmentVariable("SUPABASE_ADMIN_PASSWORD");
            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword)) return;

            using var conn = CreateConnection();
            await conn.OpenAsync();
            // Rol Admin
            Guid? rolAdminId = null;
            using (var cmdRol = new NpgsqlCommand("select id from public.roles where nombre = 'Administrador' limit 1", conn))
            {
                var r = await cmdRol.ExecuteScalarAsync();
                if (r is Guid g) rolAdminId = g;
            }
            if (rolAdminId == null) return;

            using (var cmdCheck = new NpgsqlCommand("select 1 from public.usuarios where correo=@c", conn))
            {
                cmdCheck.Parameters.AddWithValue("c", adminEmail);
                var exists = await cmdCheck.ExecuteScalarAsync();
                if (exists != null) return;
            }

            var hash = PasswordHelper.Hash(adminPassword);
            using (var cmdIns = new NpgsqlCommand(@"insert into public.usuarios (id, nombres, correo, activo, rol_id, password_hash) values (gen_random_uuid(), 'Administrador', @c, true, @rid, @p)", conn))
            {
                cmdIns.Parameters.AddWithValue("c", adminEmail);
                cmdIns.Parameters.AddWithValue("rid", rolAdminId);
                cmdIns.Parameters.AddWithValue("p", hash);
                await cmdIns.ExecuteNonQueryAsync();
            }
        }

        // ----- Solicitudes -----
        public async Task<List<(Guid id, string correo, string estado, DateTime creadoEn)>> GetSolicitudesAsync()
        {
            // Devuelve SOLO solicitudes de registro pendientes vinculadas a usuarios inactivos
            var list = new List<(Guid, string, string, DateTime)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"
                select s.id,
                       coalesce(u.correo, '' ) as correo,
                       coalesce(s.estado, 'pendiente') as estado,
                       s.creada_en
                from public.solicitudes s
                join public.usuarios u on u.id = s.usuario_id
                where lower(s.tipo) = 'registro'
                  and coalesce(s.estado,'pendiente') = 'pendiente'
                  and coalesce(u.activo, false) = false
                order by s.creada_en desc", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetGuid(0);
                var correo = reader.GetString(1);
                var estado = reader.GetString(2);
                var creadaEn = reader.GetDateTime(3);
                list.Add((id, correo, estado, creadaEn));
            }
            return list;
        }

        public async Task UpdateSolicitudEstadoAsync(Guid solicitudId, string nuevoEstado)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("update public.solicitudes set estado=@e where id=@id", conn);
            cmd.Parameters.AddWithValue("e", nuevoEstado);
            cmd.Parameters.AddWithValue("id", solicitudId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteSolicitudAsync(Guid solicitudId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("delete from public.solicitudes where id=@id", conn);
            cmd.Parameters.AddWithValue("id", solicitudId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<Guid> CreateSolicitudAsync(Guid usuarioId, string tipo, string? detalle)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"insert into public.solicitudes (id, usuario_id, tipo, detalle, estado, creada_en) values (gen_random_uuid(), @uid, @t, @d, 'pendiente', now()) returning id", conn);
            cmd.Parameters.AddWithValue("uid", usuarioId);
            cmd.Parameters.AddWithValue("t", tipo);
            cmd.Parameters.AddWithValue("d", (object?)detalle ?? DBNull.Value);
            var idObj = await cmd.ExecuteScalarAsync();
            return (Guid)idObj!;
        }

        // ----- Cursos -----
        public async Task<List<(Guid id, string titulo, string? descripcion, string estado, DateTime creadoEn)>> GetCursosAsync()
        {
            var list = new List<(Guid, string, string?, string, DateTime)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select id, titulo, descripcion, coalesce(estado,'borrador'), creado_en from public.cursos order by creado_en desc", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetGuid(0);
                var titulo = reader.GetString(1);
                var descripcion = reader.IsDBNull(2) ? null : reader.GetString(2);
                var estado = reader.GetString(3);
                var creadoEn = reader.GetDateTime(4);
                list.Add((id, titulo, descripcion, estado, creadoEn));
            }
            return list;
        }

        public async Task<List<(Guid profesorId, string nombres, string correo)>> GetCursoProfesoresAsync(Guid cursoId)
        {
            var list = new List<(Guid, string, string)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select u.id, u.nombres, u.correo
                                               from public.curso_profesores cp
                                               join public.usuarios u on u.id = cp.profesor_id
                                               where cp.curso_id=@cid
                                               order by u.nombres", conn);
            cmd.Parameters.AddWithValue("cid", cursoId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
            }
            return list;
        }

        public async Task AssignProfesorACursoAsync(Guid cursoId, Guid profesorId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"insert into public.curso_profesores (curso_id, profesor_id) values (@c,@p) on conflict do nothing", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            cmd.Parameters.AddWithValue("p", profesorId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task RemoveProfesorDeCursoAsync(Guid cursoId, Guid profesorId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"delete from public.curso_profesores where curso_id=@c and profesor_id=@p", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            cmd.Parameters.AddWithValue("p", profesorId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<(Guid id, string titulo, string? descripcion, string estado, DateTime creadoEn)>> GetCursosPorProfesorAsync(Guid profesorId)
        {
            var list = new List<(Guid, string, string?, string, DateTime)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select c.id, c.titulo, c.descripcion, coalesce(c.estado,'borrador'), c.creado_en
                                               from public.curso_profesores cp
                                               join public.cursos c on c.id = cp.curso_id
                                               where cp.profesor_id=@pid
                                               order by c.creado_en desc", conn);
            cmd.Parameters.AddWithValue("pid", profesorId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2)? null: reader.GetString(2), reader.GetString(3), reader.GetDateTime(4)));
            }
            return list;
        }

        public async Task<Guid> CreateCursoAsync(string titulo, string? descripcion, string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) estado = "borrador";
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"insert into public.cursos (id, titulo, descripcion, estado, creado_en) values (gen_random_uuid(), @t, @d, @e, now()) returning id", conn);
            cmd.Parameters.AddWithValue("t", titulo);
            cmd.Parameters.AddWithValue("d", (object?)descripcion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("e", estado);
            var idObj = await cmd.ExecuteScalarAsync();
            return (Guid)idObj!;
        }

        public async Task<(int cursos, int profesores, int practicantes)> GetDashboardCountsAsync()
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            int cursos = 0, profesores = 0, practicantes = 0;
            using (var cmd = new NpgsqlCommand("select count(*) from public.cursos where lower(coalesce(estado,'borrador'))='publicado'", conn))
            {
                cursos = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            using (var cmd = new NpgsqlCommand("select count(*) from public.usuarios u join public.roles r on r.id=u.rol_id where lower(r.nombre)='profesor' and u.activo=true", conn))
            {
                profesores = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            using (var cmd = new NpgsqlCommand("select count(*) from public.usuarios u join public.roles r on r.id=u.rol_id where lower(r.nombre)='practicante' and u.activo=true", conn))
            {
                practicantes = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            return (cursos, profesores, practicantes);
        }

        public async Task DeleteUsuarioByIdAsync(Guid usuarioId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("delete from public.usuarios where id=@id", conn);
            cmd.Parameters.AddWithValue("id", usuarioId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateCursoEstadoAsync(Guid cursoId, string nuevoEstado)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("update public.cursos set estado=@e where id=@id", conn);
            cmd.Parameters.AddWithValue("e", nuevoEstado);
            cmd.Parameters.AddWithValue("id", cursoId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteCursoAsync(Guid cursoId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("delete from public.cursos where id=@id", conn);
            cmd.Parameters.AddWithValue("id", cursoId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ActivateUserByCorreoAsync(string correo)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            // Intentar activar usuario existente; si no existe, crear uno básico activo=true con rol Usuario
            using (var cmdUpd = new NpgsqlCommand("update public.usuarios set activo=true where correo=@c", conn))
            {
                cmdUpd.Parameters.AddWithValue("c", correo);
                var rows = await cmdUpd.ExecuteNonQueryAsync();
                if (rows > 0) return;
            }
            // Crear con rol Usuario
            Guid rolUsuarioId = Guid.Empty;
            using (var cmdRol = new NpgsqlCommand("select id from public.roles where lower(nombre)='usuario' limit 1", conn))
            {
                var r = await cmdRol.ExecuteScalarAsync();
                if (r is Guid g) rolUsuarioId = g;
            }
            if (rolUsuarioId == Guid.Empty)
            {
                throw new InvalidOperationException("Rol 'Usuario' no encontrado");
            }
            using (var cmdIns = new NpgsqlCommand(@"insert into public.usuarios (id, nombres, correo, activo, rol_id) values (gen_random_uuid(), split_part(@c,'@',1), @c, true, @r)", conn))
            {
                cmdIns.Parameters.AddWithValue("c", correo);
                cmdIns.Parameters.AddWithValue("r", rolUsuarioId);
                await cmdIns.ExecuteNonQueryAsync();
            }
        }

        public async Task DeleteUserByCorreoAsync(string correo)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("delete from public.usuarios where correo=@c", conn);
            cmd.Parameters.AddWithValue("c", correo);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> ExisteUsuarioPorCorreoAsync(string correo)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("select 1 from public.usuarios where lower(correo)=lower(@c) limit 1", conn);
            cmd.Parameters.AddWithValue("c", correo);
            var r = await cmd.ExecuteScalarAsync();
            return r != null;
        }

        public async Task<bool> ExisteUsuarioPorDniAsync(string dni)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("select 1 from public.usuarios where dni=@d limit 1", conn);
            cmd.Parameters.AddWithValue("d", dni);
            var r = await cmd.ExecuteScalarAsync();
            return r != null;
        }

        // ----- Sesiones y Subsecciones -----
        public async Task<List<(Guid id, string titulo, int orden)>> GetSesionesPorCursoAsync(Guid cursoId)
        {
            var list = new List<(Guid, string, int)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("select id, titulo, orden from public.sesiones where curso_id=@cid order by orden, creado_en", conn);
            cmd.Parameters.AddWithValue("cid", cursoId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2)));
            }
            return list;
        }

        public async Task<Guid> CreateSesionAsync(Guid cursoId, string titulo, int? orden = null)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("insert into public.sesiones (id, curso_id, titulo, orden) values (gen_random_uuid(), @c, @t, coalesce(@o,1)) returning id", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            cmd.Parameters.AddWithValue("t", titulo);
            cmd.Parameters.AddWithValue("o", (object?)orden ?? DBNull.Value);
            var idObj = await cmd.ExecuteScalarAsync();
            return (Guid)idObj!;
        }

        public async Task UpdateSesionAsync(Guid sesionId, string? nuevoTitulo = null, int? nuevoOrden = null)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"update public.sesiones set 
                                                titulo = coalesce(@t, titulo),
                                                orden = coalesce(@o, orden),
                                                actualizado_en = now()
                                              where id=@id", conn);
            cmd.Parameters.AddWithValue("t", (object?)nuevoTitulo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("o", (object?)nuevoOrden ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", sesionId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteSesionAsync(Guid sesionId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("delete from public.sesiones where id=@id", conn);
            cmd.Parameters.AddWithValue("id", sesionId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<(Guid id, string titulo, string tipo, string estado, int orden)>> GetSubseccionesPorSesionAsync(Guid sesionId)
        {
            var list = new List<(Guid, string, string, string, int)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("select id, titulo, tipo, estado, orden from public.subsecciones where sesion_id=@sid order by orden, creado_en", conn);
            cmd.Parameters.AddWithValue("sid", sesionId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4)));
            }
            return list;
        }

        public async Task<Guid> CreateSubseccionAsync(Guid sesionId, string titulo, string tipo, string? textoContenido, string? archivoUrl, string? videoUrl, int? maxPuntaje, string estado = "borrador", int? orden = null, DateTimeOffset? fechaLimite = null)
        {
            if (string.IsNullOrWhiteSpace(tipo) || !(new[]{"contenido","video","tarea"}.Contains(tipo))) tipo = "contenido";
            if (string.IsNullOrWhiteSpace(estado) || !(new[]{"borrador","publicado"}.Contains(estado))) estado = "borrador";
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"insert into public.subsecciones (id, sesion_id, titulo, tipo, texto_contenido, archivo_url, video_url, max_puntaje, estado, orden, fecha_limite)
                                               values (gen_random_uuid(), @s, @t, @tp, @txt, @aurl, @vurl, @mp, @est, coalesce(@o, 1), @flim) returning id", conn);
            cmd.Parameters.AddWithValue("s", sesionId);
            cmd.Parameters.AddWithValue("t", titulo);
            cmd.Parameters.AddWithValue("tp", tipo);
            cmd.Parameters.AddWithValue("txt", (object?)textoContenido ?? DBNull.Value);
            cmd.Parameters.AddWithValue("aurl", (object?)archivoUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("vurl", (object?)videoUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("mp", (object?)maxPuntaje ?? DBNull.Value);
            cmd.Parameters.AddWithValue("est", estado);
            cmd.Parameters.AddWithValue("o", (object?)orden ?? DBNull.Value);
            cmd.Parameters.AddWithValue("flim", (object?)(fechaLimite?.UtcDateTime) ?? DBNull.Value);
            var idObj = await cmd.ExecuteScalarAsync();
            return (Guid)idObj!;
        }

        public async Task UpdateSubseccionAsync(
            Guid subseccionId,
            string? nuevoTitulo = null,
            string? nuevoTipo = null,
            string? nuevoTextoContenido = null,
            string? nuevoArchivoUrl = null,
            string? nuevoArchivoMime = null,
            long? nuevoArchivoSizeBytes = null,
            string? nuevoVideoUrl = null,
            string? nuevoVideoMime = null,
            long? nuevoVideoSizeBytes = null,
            int? nuevoVideoDuracionSegundos = null,
            int? nuevoMaxPuntaje = null,
            int? nuevoOrden = null,
            DateTimeOffset? nuevaFechaLimite = null)
        {
            // Validar tipo si se provee
            if (nuevoTipo != null && !(new[]{"contenido","video","tarea"}.Contains(nuevoTipo))) nuevoTipo = null;

            using var conn = CreateConnection();
            await conn.OpenAsync();
                        using var cmd = new NpgsqlCommand(@"update public.subsecciones set
                                                titulo = coalesce(@t, titulo),
                                                tipo = coalesce(@tp, tipo),
                                                texto_contenido = coalesce(@txt, texto_contenido),
                                                archivo_url = coalesce(@aurl, archivo_url),
                                                archivo_mime = coalesce(@amime, archivo_mime),
                                                archivo_size_bytes = coalesce(@asize, archivo_size_bytes),
                                                video_url = coalesce(@vurl, video_url),
                                                video_mime = coalesce(@vmime, video_mime),
                                                video_size_bytes = coalesce(@vsize, video_size_bytes),
                                                video_duracion_segundos = coalesce(@vdur, video_duracion_segundos),
                                                max_puntaje = coalesce(@mp, max_puntaje),
                                                orden = coalesce(@o, orden),
                                                                                                fecha_limite = coalesce(@flim, fecha_limite),
                                                actualizado_en = now()
                                              where id=@id", conn);
            cmd.Parameters.AddWithValue("t", (object?)nuevoTitulo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tp", (object?)nuevoTipo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("txt", (object?)nuevoTextoContenido ?? DBNull.Value);
            cmd.Parameters.AddWithValue("aurl", (object?)nuevoArchivoUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("amime", (object?)nuevoArchivoMime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("asize", (object?)nuevoArchivoSizeBytes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("vurl", (object?)nuevoVideoUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("vmime", (object?)nuevoVideoMime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("vsize", (object?)nuevoVideoSizeBytes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("vdur", (object?)nuevoVideoDuracionSegundos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("mp", (object?)nuevoMaxPuntaje ?? DBNull.Value);
            cmd.Parameters.AddWithValue("o", (object?)nuevoOrden ?? DBNull.Value);
            cmd.Parameters.AddWithValue("flim", (object?)(nuevaFechaLimite?.UtcDateTime) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", subseccionId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteSubseccionAsync(Guid subseccionId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("delete from public.subsecciones where id=@id", conn);
            cmd.Parameters.AddWithValue("id", subseccionId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateSubseccionEstadoAsync(Guid subseccionId, string nuevoEstado)
        {
            if (string.IsNullOrWhiteSpace(nuevoEstado) || !(new[]{"borrador","publicado"}.Contains(nuevoEstado))) nuevoEstado = "borrador";
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("update public.subsecciones set estado=@e where id=@id", conn);
            cmd.Parameters.AddWithValue("e", nuevoEstado);
            cmd.Parameters.AddWithValue("id", subseccionId);
            await cmd.ExecuteNonQueryAsync();
        }

        // ----- Participantes (profesores y practicantes) -----
        public async Task<List<(Guid id, string nombres, string correo, string rol)>> GetParticipantesCursoAsync(Guid cursoId)
        {
            var list = new List<(Guid,string,string,string)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            // Profesores
            using (var cmdProf = new NpgsqlCommand(@"select u.id, u.nombres, u.correo, 'Profesor' as rol
                                                     from public.curso_profesores cp
                                                     join public.usuarios u on u.id = cp.profesor_id
                                                     where cp.curso_id=@cid", conn))
            {
                cmdProf.Parameters.AddWithValue("cid", cursoId);
                using var r = await cmdProf.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add((r.GetGuid(0), r.GetString(1), r.IsDBNull(2)?"":r.GetString(2), r.GetString(3)));
                }
            }
            // Practicantes
            using (var cmdPract = new NpgsqlCommand(@"select u.id, u.nombres, u.correo, 'Practicante' as rol
                                                      from public.curso_practicantes cp
                                                      join public.usuarios u on u.id = cp.practicante_id
                                                      join public.roles r on r.id = u.rol_id
                                                      where cp.curso_id=@cid and lower(r.nombre)='practicante'", conn))
            {
                cmdPract.Parameters.AddWithValue("cid", cursoId);
                using var r2 = await cmdPract.ExecuteReaderAsync();
                while (await r2.ReadAsync())
                {
                    list.Add((r2.GetGuid(0), r2.GetString(1), r2.IsDBNull(2)?"":r2.GetString(2), r2.GetString(3)));
                }
            }
            return list;
        }

        public async Task AssignPracticanteACursoAsync(Guid cursoId, Guid practicanteId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("insert into public.curso_practicantes (curso_id, practicante_id) values (@c,@p) on conflict do nothing", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            cmd.Parameters.AddWithValue("p", practicanteId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
