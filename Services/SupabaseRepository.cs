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
    }
}
