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

        // Ensure schema changes required by new flows
        public async Task EnsureSchemaAdjustmentsAsync()
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            // Add require_password_change boolean if missing
            try
            {
                using var cmd = new NpgsqlCommand("ALTER TABLE public.usuarios ADD COLUMN IF NOT EXISTS require_password_change boolean DEFAULT false", conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* ignore */ }
        }

        public async Task SetRequirePasswordChangeAsync(Guid userId, bool require)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("update public.usuarios set require_password_change=@r, actualizado_en=now() where id=@id", conn);
            cmd.Parameters.AddWithValue("r", require);
            cmd.Parameters.AddWithValue("id", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> GetRequirePasswordChangeAsync(Guid userId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("select coalesce(require_password_change, false) from public.usuarios where id=@id", conn);
            cmd.Parameters.AddWithValue("id", userId);
            var r = await cmd.ExecuteScalarAsync();
            return r != null && r is bool b && b;
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

        public async Task<List<(Guid profesorId, string nombres, string? apellidos, string? telefono, string correo, string? areaNombre)>> GetCursoProfesoresAsync(Guid cursoId)
        {
            var list = new List<(Guid, string, string?, string?, string, string?)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select u.id, u.nombres, u.apellidos, u.telefono, coalesce(u.correo,''), a.nombre as area_nombre
                                               from public.curso_profesores cp
                                               join public.usuarios u on u.id = cp.profesor_id
                                               left join public.areas a on a.id = u.area_id
                                               where cp.curso_id=@cid
                                               order by u.nombres", conn);
            cmd.Parameters.AddWithValue("cid", cursoId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5)
                ));
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

        public async Task<(int cursos, int profesores, int practicantes, int colaboradores)> GetDashboardCountsAsync()
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            int cursos = 0, profesores = 0, practicantes = 0, colaboradores = 0;
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
            using (var cmd = new NpgsqlCommand("select count(*) from public.usuarios u join public.roles r on r.id=u.rol_id where lower(r.nombre)='colaborador' and u.activo=true", conn))
            {
                colaboradores = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            return (cursos, profesores, practicantes, colaboradores);
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

        public async Task<List<(Guid id, string titulo, string tipo, string estado, int orden, DateTimeOffset? fechaLimite)>> GetSubseccionesPorSesionAsync(Guid sesionId)
        {
            var list = new List<(Guid, string, string, string, int, DateTimeOffset?)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("select id, titulo, tipo, estado, orden, fecha_limite from public.subsecciones where sesion_id=@sid order by orden, creado_en", conn);
            cmd.Parameters.AddWithValue("sid", sesionId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var fecha = reader.IsDBNull(5) ? (DateTimeOffset?)null : new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero);
                list.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), fecha));
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
            // Colaboradores
            using (var cmdColab = new NpgsqlCommand(@"select u.id, u.nombres, u.correo, 'Colaborador' as rol
                                                      from public.curso_colaboradores cc
                                                      join public.usuarios u on u.id = cc.colaborador_id
                                                      join public.roles r on r.id = u.rol_id
                                                      where cc.curso_id=@cid and lower(r.nombre)='colaborador'", conn))
            {
                cmdColab.Parameters.AddWithValue("cid", cursoId);
                using var r3 = await cmdColab.ExecuteReaderAsync();
                while (await r3.ReadAsync())
                {
                    list.Add((r3.GetGuid(0), r3.GetString(1), r3.IsDBNull(2)?"":r3.GetString(2), r3.GetString(3)));
                }
            }
            // Fallback: colaboradores mal insertados en curso_practicantes
            using (var cmdFallbackCol = new NpgsqlCommand(@"select u.id, u.nombres, u.correo, 'Colaborador' as rol
                                                           from public.curso_practicantes cp
                                                           join public.usuarios u on u.id = cp.practicante_id
                                                           join public.roles r on r.id = u.rol_id
                                                           where cp.curso_id=@cid and lower(r.nombre)='colaborador'", conn))
            {
                cmdFallbackCol.Parameters.AddWithValue("cid", cursoId);
                using var rf = await cmdFallbackCol.ExecuteReaderAsync();
                while (await rf.ReadAsync())
                {
                    var id = rf.GetGuid(0);
                    if (!list.Any(x => x.Item1 == id && x.Item4 == "Colaborador"))
                        list.Add((id, rf.GetString(1), rf.IsDBNull(2)?"":rf.GetString(2), rf.GetString(3)));
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

        // ----- Inscripciones (solicitudes de practicantes a cursos) -----
        public async Task<List<(Guid solicitudId, Guid cursoId, string cursoTitulo, Guid practicanteId, string practicanteNombre, string practicanteCorreo, DateTime creadaEn)>> GetInscripcionesPendientesPorProfesorAsync(Guid profesorId)
        {
            var list = new List<(Guid, Guid, string, Guid, string, string, DateTime)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select s.id as solicitud_id,
                                                       s.detalle::uuid as curso_id,
                                                       c.titulo as curso_titulo,
                                                       u.id as practicante_id,
                                                       u.nombres as practicante_nombre,
                                                       coalesce(u.correo,'') as practicante_correo,
                                                       s.creada_en
                                                from public.solicitudes s
                                                join public.curso_profesores cp on cp.curso_id = s.detalle::uuid
                                                join public.cursos c on c.id = cp.curso_id
                                                join public.usuarios u on u.id = s.usuario_id
                                                where lower(s.tipo) = 'inscripcion'
                                                  and coalesce(s.estado, 'pendiente') = 'pendiente'
                                                  and cp.profesor_id = @pid
                                                order by s.creada_en desc", conn);
            cmd.Parameters.AddWithValue("pid", profesorId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetGuid(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetDateTime(6)
                ));
            }
            return list;
        }

        public async Task ApproveInscripcionAsync(Guid solicitudId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();
            try
            {
                // Determinar rol del usuario de la solicitud
                string rolUsuario = "";
                using (var cmdRol = new NpgsqlCommand(@"select lower(r.nombre)
                                                       from public.solicitudes s
                                                       join public.usuarios u on u.id = s.usuario_id
                                                       join public.roles r on r.id = u.rol_id
                                                       where s.id=@sid
                                                       limit 1", conn, (NpgsqlTransaction)tx))
                {
                    cmdRol.Parameters.AddWithValue("sid", solicitudId);
                    var r = await cmdRol.ExecuteScalarAsync();
                    if (r is string sr) rolUsuario = sr.Trim().ToLowerInvariant();
                }

                if (rolUsuario == "colaborador")
                {
                    using var cmdInsCol = new NpgsqlCommand(@"insert into public.curso_colaboradores (curso_id, colaborador_id)
                                                             select s.detalle::uuid, s.usuario_id
                                                             from public.solicitudes s
                                                             where s.id = @sid
                                                             on conflict do nothing", conn, (NpgsqlTransaction)tx);
                    cmdInsCol.Parameters.AddWithValue("sid", solicitudId);
                    await cmdInsCol.ExecuteNonQueryAsync();
                }
                else
                {
                    // Default a practicante (incluye cuando rol es 'practicante')
                    using var cmdInsPrac = new NpgsqlCommand(@"insert into public.curso_practicantes (curso_id, practicante_id)
                                                              select s.detalle::uuid, s.usuario_id
                                                              from public.solicitudes s
                                                              where s.id = @sid
                                                              on conflict do nothing", conn, (NpgsqlTransaction)tx);
                    cmdInsPrac.Parameters.AddWithValue("sid", solicitudId);
                    await cmdInsPrac.ExecuteNonQueryAsync();
                }

                // Marcar solicitud como aprobada
                using (var cmdUpd = new NpgsqlCommand("update public.solicitudes set estado='aprobada' where id=@sid", conn, (NpgsqlTransaction)tx))
                {
                    cmdUpd.Parameters.AddWithValue("sid", solicitudId);
                    await cmdUpd.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<List<(Guid id, string titulo, string? descripcion, string estado, DateTime creadoEn)>> GetCursosPorPracticanteAsync(Guid practicanteId)
        {
            // Incluye cursos asignados por tabla correcta (curso_practicantes)
            // y fallback por registros mal insertados en curso_colaboradores con rol 'practicante'
            var list = new List<(Guid, string, string?, string, DateTime)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"
                (
                    select c.id, c.titulo, c.descripcion, coalesce(c.estado,'borrador') as estado, c.creado_en
                    from public.curso_practicantes cp
                    join public.cursos c on c.id = cp.curso_id
                    where cp.practicante_id = @uid
                )
                union
                (
                    select c.id, c.titulo, c.descripcion, coalesce(c.estado,'borrador') as estado, c.creado_en
                    from public.curso_colaboradores cc
                    join public.usuarios u on u.id = cc.colaborador_id
                    join public.roles r on r.id = u.rol_id
                    join public.cursos c on c.id = cc.curso_id
                    where cc.colaborador_id = @uid and lower(r.nombre)='practicante'
                )
                order by creado_en desc", conn);
            cmd.Parameters.AddWithValue("uid", practicanteId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2)? null: reader.GetString(2), reader.GetString(3), reader.GetDateTime(4)));
            }
            return list;
        }

        public async Task<List<(Guid id, string titulo, string? descripcion, string estado, DateTime creadoEn)>> GetCursosPorColaboradorAsync(Guid colaboradorId)
        {
            // Incluye cursos asignados por tabla correcta (curso_colaboradores)
            // y fallback por registros mal insertados en curso_practicantes con rol 'colaborador'
            var list = new List<(Guid, string, string?, string, DateTime)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"
                (
                    select c.id, c.titulo, c.descripcion, coalesce(c.estado,'borrador') as estado, c.creado_en
                    from public.curso_colaboradores cc
                    join public.cursos c on c.id = cc.curso_id
                    where cc.colaborador_id = @uid
                )
                union
                (
                    select c.id, c.titulo, c.descripcion, coalesce(c.estado,'borrador') as estado, c.creado_en
                    from public.curso_practicantes cp
                    join public.usuarios u on u.id = cp.practicante_id
                    join public.roles r on r.id = u.rol_id
                    join public.cursos c on c.id = cp.curso_id
                    where cp.practicante_id = @uid and lower(r.nombre)='colaborador'
                )
                order by creado_en desc", conn);
            cmd.Parameters.AddWithValue("uid", colaboradorId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2)? null: reader.GetString(2), reader.GetString(3), reader.GetDateTime(4)));
            }
            return list;
        }

        public async Task<bool> HasProfesorAsignadoAsync(Guid cursoId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("select exists(select 1 from public.curso_profesores where curso_id=@c)", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            var r = await cmd.ExecuteScalarAsync();
            return r is bool b && b;
        }

        public async Task<(int profesores, int practicantes, int colaboradores)> GetParticipanteCountsAsync(Guid cursoId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            int profesores = 0, practicantes = 0, colaboradores = 0;
            using (var c1 = new NpgsqlCommand("select count(*) from public.curso_profesores where curso_id=@c", conn))
            { c1.Parameters.AddWithValue("c", cursoId); profesores = Convert.ToInt32(await c1.ExecuteScalarAsync()); }
            // Practicantes reales + practicantes mal insertados en curso_colaboradores
            using (var c2 = new NpgsqlCommand(@"select count(*) from public.curso_practicantes cp
                                               join public.usuarios u on u.id = cp.practicante_id
                                               join public.roles r on r.id = u.rol_id
                                               where cp.curso_id=@c and lower(r.nombre)='practicante'", conn))
            { c2.Parameters.AddWithValue("c", cursoId); practicantes = Convert.ToInt32(await c2.ExecuteScalarAsync()); }
            using (var c2b = new NpgsqlCommand(@"select count(*) from public.curso_colaboradores cc
                                                join public.usuarios u on u.id = cc.colaborador_id
                                                join public.roles r on r.id = u.rol_id
                                                where cc.curso_id=@c and lower(r.nombre)='practicante'", conn))
            { c2b.Parameters.AddWithValue("c", cursoId); practicantes += Convert.ToInt32(await c2b.ExecuteScalarAsync()); }
            // Colaboradores reales + colaboradores mal insertados en curso_practicantes
            using (var c3 = new NpgsqlCommand(@"select count(*) from public.curso_colaboradores cc
                                               join public.usuarios u on u.id = cc.colaborador_id
                                               join public.roles r on r.id = u.rol_id
                                               where cc.curso_id=@c and lower(r.nombre)='colaborador'", conn))
            { c3.Parameters.AddWithValue("c", cursoId); colaboradores = Convert.ToInt32(await c3.ExecuteScalarAsync()); }
            using (var c3b = new NpgsqlCommand(@"select count(*) from public.curso_practicantes cp
                                                join public.usuarios u on u.id = cp.practicante_id
                                                join public.roles r on r.id = u.rol_id
                                                where cp.curso_id=@c and lower(r.nombre)='colaborador'", conn))
            { c3b.Parameters.AddWithValue("c", cursoId); colaboradores += Convert.ToInt32(await c3b.ExecuteScalarAsync()); }
            return (profesores, practicantes, colaboradores);
        }

        public async Task<List<(Guid cursoId, string estado)>> GetSolicitudesInscripcionPorUsuarioAsync(Guid usuarioId)
        {
            var list = new List<(Guid, string)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select detalle::uuid as curso_id, coalesce(estado,'pendiente') as estado
                                               from public.solicitudes
                                               where usuario_id=@uid and lower(tipo)='inscripcion'", conn);
            cmd.Parameters.AddWithValue("uid", usuarioId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetGuid(0), reader.GetString(1)));
            }
            return list;
        }

        // ----- Tareas y Entregas -----
        public async Task<(Guid tareaId, Guid subseccionId, string titulo, DateTimeOffset? fechaEntrega)?> GetTareaPorSubseccionAsync(Guid subseccionId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select t.id, t.subseccion_id, t.titulo, t.fecha_entrega
                                               from public.tareas t
                                               where t.subseccion_id=@sid
                                               limit 1", conn);
            cmd.Parameters.AddWithValue("sid", subseccionId);
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                var fecha = r.IsDBNull(3) ? (DateTimeOffset?)null : new DateTimeOffset(r.GetDateTime(3), TimeSpan.Zero);
                return (r.GetGuid(0), r.GetGuid(1), r.GetString(2), fecha);
            }
            return null;
        }

        public async Task<Guid> EnsureTareaParaSubseccionAsync(Guid subseccionId, string titulo, DateTimeOffset? fechaEntrega)
        {
            // Crea la tarea si no existe y devuelve su id
            using var conn = CreateConnection();
            await conn.OpenAsync();
            // Buscar existente
            Guid? existente = null;
            using (var cmdFind = new NpgsqlCommand("select id from public.tareas where subseccion_id=@sid limit 1", conn))
            {
                cmdFind.Parameters.AddWithValue("sid", subseccionId);
                var o = await cmdFind.ExecuteScalarAsync();
                if (o is Guid g) existente = g;
            }
            if (existente != null) return existente.Value;
            // Crear
            using var cmdIns = new NpgsqlCommand(@"insert into public.tareas (id, subseccion_id, titulo, fecha_entrega)
                                                  values (gen_random_uuid(), @sid, @t, @f)
                                                  returning id", conn);
            cmdIns.Parameters.AddWithValue("sid", subseccionId);
            cmdIns.Parameters.AddWithValue("t", titulo);
            cmdIns.Parameters.AddWithValue("f", (object?)(fechaEntrega?.UtcDateTime) ?? DBNull.Value);
            var idObj = await cmdIns.ExecuteScalarAsync();
            return (Guid)idObj!;
        }

        public async Task<(Guid entregaId, Guid tareaId, Guid usuarioId, string? urlArchivo, string? enlaceUrl, decimal? calificacion, string estado, DateTimeOffset entregadoEn, DateTimeOffset? calificadoEn)?>
            GetEntregaAsync(Guid tareaId, Guid usuarioId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select id, tarea_id, usuario_id, url_archivo, enlace_url, calificacion, coalesce(estado,'entregado'), entregado_en, calificado_en
                                               from public.entregas_tareas
                                               where tarea_id=@tid and usuario_id=@uid
                                               limit 1", conn);
            cmd.Parameters.AddWithValue("tid", tareaId);
            cmd.Parameters.AddWithValue("uid", usuarioId);
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return (
                    r.GetGuid(0),
                    r.GetGuid(1),
                    r.GetGuid(2),
                    r.IsDBNull(3) ? null : r.GetString(3),
                    r.IsDBNull(4) ? null : r.GetString(4),
                    r.IsDBNull(5) ? (decimal?)null : r.GetDecimal(5),
                    r.GetString(6),
                    new DateTimeOffset(r.GetDateTime(7), TimeSpan.Zero),
                    r.IsDBNull(8) ? (DateTimeOffset?)null : new DateTimeOffset(r.GetDateTime(8), TimeSpan.Zero)
                );
            }
            return null;
        }

        public async Task<Guid> UpsertEntregaAsync(Guid tareaId, Guid usuarioId, string? urlArchivo, string? enlaceUrl, DateTimeOffset? fechaLimiteUtc)
        {
            // Bloqueos: si fecha límite vencida o ya calificado, no permitir actualizar
            using var conn = CreateConnection();
            await conn.OpenAsync();

            // Revisar entrega existente
            var existente = await GetEntregaAsync(tareaId, usuarioId);
            if (existente != null)
            {
                if (existente.Value.calificacion != null || existente.Value.calificadoEn != null || string.Equals(existente.Value.estado, "calificado", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("La entrega ya fue calificada y no puede modificarse.");
            }
            if (fechaLimiteUtc != null && DateTimeOffset.UtcNow > fechaLimiteUtc.Value)
                throw new InvalidOperationException("La fecha límite ya venció.");

            // Upsert
            using var cmd = new NpgsqlCommand(@"insert into public.entregas_tareas (id, tarea_id, usuario_id, url_archivo, enlace_url, estado, entregado_en)
                                              values (gen_random_uuid(), @t, @u, @url, @enl, 'entregado', now())
                                              on conflict (tarea_id, usuario_id)
                                              do update set url_archivo = EXCLUDED.url_archivo,
                                                            enlace_url = EXCLUDED.enlace_url,
                                                            estado = 'entregado',
                                                            actualizado_en = now()
                                              returning id", conn);
            cmd.Parameters.AddWithValue("t", tareaId);
            cmd.Parameters.AddWithValue("u", usuarioId);
            cmd.Parameters.AddWithValue("url", (object?)urlArchivo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("enl", (object?)enlaceUrl ?? DBNull.Value);
            var idObj = await cmd.ExecuteScalarAsync();
            return (Guid)idObj!;
        }

        public async Task CalificarEntregaAsync(Guid entregaId, decimal calificacion)
        {
            if (calificacion < 0 || calificacion > 20) throw new ArgumentOutOfRangeException(nameof(calificacion), "La calificación debe estar entre 0 y 20.");
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"update public.entregas_tareas
                                               set calificacion=@c,
                                                   estado='calificado',
                                                   calificado_en=now(),
                                                   actualizado_en=now()
                                               where id=@id", conn);
            cmd.Parameters.AddWithValue("c", calificacion);
            cmd.Parameters.AddWithValue("id", entregaId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<(Guid entregaId, Guid usuarioId, string practicanteNombre, string? urlArchivo, string? enlaceUrl, decimal? calificacion, string estado, DateTimeOffset entregadoEn, DateTimeOffset? calificadoEn)>>
            ListarEntregasPorTareaAsync(Guid tareaId)
        {
            var list = new List<(Guid, Guid, string, string?, string?, decimal?, string, DateTimeOffset, DateTimeOffset?)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select e.id, e.usuario_id, u.nombres, e.url_archivo, e.enlace_url, e.calificacion, coalesce(e.estado,'entregado'), e.entregado_en, e.calificado_en
                                               from public.entregas_tareas e
                                               join public.usuarios u on u.id = e.usuario_id
                                               where e.tarea_id=@t
                                               order by e.entregado_en desc", conn);
            cmd.Parameters.AddWithValue("t", tareaId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add((
                    r.GetGuid(0),
                    r.GetGuid(1),
                    r.GetString(2),
                    r.IsDBNull(3)? null: r.GetString(3),
                    r.IsDBNull(4)? null: r.GetString(4),
                    r.IsDBNull(5)? (decimal?)null: r.GetDecimal(5),
                    r.GetString(6),
                    new DateTimeOffset(r.GetDateTime(7), TimeSpan.Zero),
                    r.IsDBNull(8)? (DateTimeOffset?)null: new DateTimeOffset(r.GetDateTime(8), TimeSpan.Zero)
                ));
            }
            return list;
        }

        // ===== Asistencias =====
        public async Task<Guid> UpsertAsistenciaAsync(Guid cursoId, Guid profesorId, DateOnly fecha, TimeOnly? horaInicio, TimeOnly? horaFin)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"insert into public.asistencias (id, curso_id, profesor_id, fecha, hora_inicio, hora_fin)
                                              values (gen_random_uuid(), @c, @p, @f, @hi, @hf)
                                              on conflict (curso_id, fecha)
                                              do update set profesor_id = EXCLUDED.profesor_id,
                                                            hora_inicio = EXCLUDED.hora_inicio,
                                                            hora_fin = EXCLUDED.hora_fin,
                                                            creada_en = now()
                                              returning id", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            cmd.Parameters.AddWithValue("p", profesorId);
            cmd.Parameters.AddWithValue("f", fecha.ToDateTime(TimeOnly.MinValue));
            cmd.Parameters.AddWithValue("hi", (object?)(horaInicio?.ToTimeSpan()) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("hf", (object?)(horaFin?.ToTimeSpan()) ?? DBNull.Value);
            var idObj = await cmd.ExecuteScalarAsync();
            return (Guid)idObj!;
        }

        public async Task SetAsistenciaDetalleAsync(Guid asistenciaId, Guid usuarioId, string estado, string? justificacion)
        {
            estado = (estado ?? "ausente").Trim().ToLowerInvariant();
            if (!(new[]{"presente","tardanza","ausente","justificado"}).Contains(estado)) estado = "ausente";
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"insert into public.asistencias_detalle (id, asistencia_id, usuario_id, estado, justificacion)
                                              values (gen_random_uuid(), @a, @u, @e, @j)
                                              on conflict (asistencia_id, usuario_id)
                                              do update set estado = EXCLUDED.estado,
                                                            justificacion = EXCLUDED.justificacion,
                                                            marcada_en = now()", conn);
            cmd.Parameters.AddWithValue("a", asistenciaId);
            cmd.Parameters.AddWithValue("u", usuarioId);
            cmd.Parameters.AddWithValue("e", estado);
            cmd.Parameters.AddWithValue("j", (object?)justificacion ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<(int presente, int tardanza, int ausente, int justificado)> GetResumenAsistenciaUsuarioAsync(Guid cursoId, Guid usuarioId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            int presente = 0, tardanza = 0, ausente = 0, justificado = 0;
            using var cmd = new NpgsqlCommand(@"select lower(coalesce(ad.estado,'ausente')) as est, count(*)
                                               from public.asistencias a
                                               join public.asistencias_detalle ad on ad.asistencia_id = a.id
                                               where a.curso_id=@c and ad.usuario_id=@u
                                               group by 1", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            cmd.Parameters.AddWithValue("u", usuarioId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var est = r.GetString(0); var cnt = r.GetInt32(1);
                switch(est){
                    case "presente": presente = cnt; break;
                    case "tardanza": tardanza = cnt; break;
                    case "justificado": justificado = cnt; break;
                    default: ausente = cnt; break;
                }
            }
            return (presente, tardanza, ausente, justificado);
        }

        public async Task<List<(DateOnly fecha, TimeOnly? horaInicio, TimeOnly? horaFin, string estado, string? justificacion)>>
            ListarAsistenciasPorUsuarioAsync(Guid cursoId, Guid usuarioId)
        {
            var list = new List<(DateOnly, TimeOnly?, TimeOnly?, string, string?)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select a.fecha, a.hora_inicio, a.hora_fin, lower(coalesce(ad.estado,'ausente')) as estado, ad.justificacion
                                               from public.asistencias a
                                               left join public.asistencias_detalle ad on ad.asistencia_id = a.id and ad.usuario_id=@u
                                               where a.curso_id=@c
                                               order by a.fecha desc", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            cmd.Parameters.AddWithValue("u", usuarioId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var fecha = DateOnly.FromDateTime(r.GetDateTime(0));
                TimeOnly? hi = r.IsDBNull(1) ? (TimeOnly?)null : TimeOnly.FromTimeSpan(r.GetTimeSpan(1));
                TimeOnly? hf = r.IsDBNull(2) ? (TimeOnly?)null : TimeOnly.FromTimeSpan(r.GetTimeSpan(2));
                var estado = r.IsDBNull(3) ? "ausente" : r.GetString(3);
                var just = r.IsDBNull(4) ? null : r.GetString(4);
                list.Add((fecha, hi, hf, estado, just));
            }
            return list;
        }

        // ----- Horarios por curso -----
        public async Task<List<(string dia, TimeSpan inicio, TimeSpan fin)>> GetHorarioCursoAsync(Guid cursoId)
        {
            var list = new List<(string, TimeSpan, TimeSpan)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select 
                        CASE 
                          WHEN dia_semana=1 THEN 'lunes'
                          WHEN dia_semana=2 THEN 'martes'
                          WHEN dia_semana=3 THEN 'miercoles'
                          WHEN dia_semana=4 THEN 'jueves'
                          WHEN dia_semana=5 THEN 'viernes'
                          WHEN dia_semana=6 THEN 'sabado'
                          WHEN dia_semana=7 THEN 'domingo'
                          ELSE 'lunes'
                        END as dia,
                        hora_inicio, hora_fin 
                     from public.curso_horarios where curso_id=@c order by dia_semana, hora_inicio", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var dia = r.GetString(0);
                var hi = r.GetTimeSpan(1);
                var hf = r.GetTimeSpan(2);
                list.Add((dia, hi, hf));
            }
            return list;
        }

        public async Task ReplaceHorarioCursoAsync(Guid cursoId, string[] dias, string[] inicio, string[] fin)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using (var tx = await conn.BeginTransactionAsync())
            {
                using (var del = new NpgsqlCommand("delete from public.curso_horarios where curso_id=@c", conn, (NpgsqlTransaction)tx))
                {
                    del.Parameters.AddWithValue("c", cursoId);
                    await del.ExecuteNonQueryAsync();
                }
                for (int i = 0; i < dias.Length; i++)
                {
                    using var ins = new NpgsqlCommand(@"insert into public.curso_horarios(curso_id, dia_semana, hora_inicio, hora_fin)
                                                         values(@c, @d, @hi, @hf)", conn, (NpgsqlTransaction)tx);
                    ins.Parameters.AddWithValue("c", cursoId);
                    ins.Parameters.AddWithValue("d", DiaTextoAEntero(dias[i]));
                    ins.Parameters.AddWithValue("hi", TimeSpan.Parse(inicio[i]));
                    ins.Parameters.AddWithValue("hf", TimeSpan.Parse(fin[i]));
                    await ins.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
            }
        }

        private static int DiaTextoAEntero(string dia)
        {
            dia = (dia ?? "lunes").Trim().ToLowerInvariant();
            return dia switch
            {
                "lunes" => 1,
                "martes" => 2,
                "miercoles" => 3,
                "jueves" => 4,
                "viernes" => 5,
                "sabado" => 6,
                "domingo" => 7,
                _ => 1
            };
        }

        // ----- Asistencias por curso -----
        public async Task<List<(Guid id, DateTime fecha, string dia)>> GetAsistenciasCursoAsync(Guid cursoId)
        {
            var list = new List<(Guid, DateTime, string)>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select id, fecha, dia_semana from public.asistencias where curso_id=@c order by fecha desc", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add((r.GetGuid(0), r.GetDateTime(1), r.GetString(2)));
            }
            return list;
        }

        // Obtener una asistencia por id
        public async Task<(DateOnly fecha, string dia)?> GetAsistenciaPorIdAsync(Guid asistenciaId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select fecha, dia_semana from public.asistencias where id=@id limit 1", conn);
            cmd.Parameters.AddWithValue("id", asistenciaId);
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                var f = DateOnly.FromDateTime(r.GetDateTime(0));
                var dia = r.IsDBNull(1) ? "" : r.GetString(1);
                return (f, dia);
            }
            return null;
        }

        public async Task<Guid> CrearAsistenciaAsync(Guid cursoId, DateTime fecha, Guid profesorId)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"insert into public.asistencias(id, curso_id, profesor_id, fecha)
                                               values(gen_random_uuid(), @c, @p, @f)
                                               on conflict (curso_id, fecha) do nothing
                                               returning id", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            cmd.Parameters.AddWithValue("p", profesorId);
            cmd.Parameters.AddWithValue("f", fecha.Date);
            var idObj = await cmd.ExecuteScalarAsync();
            if (idObj == null)
            {
                // Already exists; fetch existing id
                using var get = new NpgsqlCommand("select id from public.asistencias where curso_id=@c and fecha=@f limit 1", conn);
                get.Parameters.AddWithValue("c", cursoId);
                get.Parameters.AddWithValue("f", fecha.Date);
                var existing = await get.ExecuteScalarAsync();
                return (Guid)existing!;
            }
            return (Guid)idObj!;
        }

        // Lista de días (texto) que ya tienen asistencias registradas para el curso
        public async Task<HashSet<string>> GetDiasConAsistenciasAsync(Guid cursoId)
        {
            var set = new HashSet<string>();
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"select distinct lower(dia_semana) from public.asistencias where curso_id=@c", conn);
            cmd.Parameters.AddWithValue("c", cursoId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var dia = r.IsDBNull(0) ? null : r.GetString(0);
                if (!string.IsNullOrWhiteSpace(dia)) set.Add(dia.Trim().ToLowerInvariant());
            }
            return set;
        }
    }
}
