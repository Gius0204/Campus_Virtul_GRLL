using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Helpers;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize]
    public class PerfilController : Controller
    {
        private readonly SupabaseRepository _repo;
        private readonly ILogger<PerfilController> _logger;

        public PerfilController(SupabaseRepository repo, ILogger<PerfilController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var correo = User.GetUserEmail();
            if (string.IsNullOrWhiteSpace(correo)) return RedirectToAction("Index", "Dashboard");
            var u = await _repo.GetUserByEmailAsync(correo);
            if (!u.HasValue) return RedirectToAction("Index", "Dashboard");
            ViewBag.User = u.Value;
            // Cargar áreas para selector
            var areas = await _repo.GetAreasAsync();
            ViewBag.Areas = areas;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarDatos(string? nombres, string? apellidos, string? dni, string? telefono, Guid? areaId)
        {
            var correo = User.GetUserEmail();
            var u = await _repo.GetUserByEmailAsync(correo ?? "");
            if (!u.HasValue)
            {
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction("Index");
            }
            try
            {
                // Actualizar campos permitidos
                using var conn = new Npgsql.NpgsqlConnection(new Npgsql.NpgsqlConnectionStringBuilder {
                    Host = Environment.GetEnvironmentVariable("SUPABASE_DB_HOST"),
                    Port = int.TryParse(Environment.GetEnvironmentVariable("SUPABASE_DB_PORT"), out var p) ? p : 5432,
                    Database = Environment.GetEnvironmentVariable("SUPABASE_DB_NAME") ?? "postgres",
                    Username = Environment.GetEnvironmentVariable("SUPABASE_DB_USER") ?? "postgres",
                    Password = Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD"),
                    SslMode = Npgsql.SslMode.Require,
                    TrustServerCertificate = true
                }.ConnectionString);
                await conn.OpenAsync();
                using var cmd = new Npgsql.NpgsqlCommand("update public.usuarios set nombres=@n, apellidos=@ap, dni=@dni, telefono=@tel, area_id=@ar, actualizado_en=now() where id=@id", conn);
                cmd.Parameters.AddWithValue("n", (object?)nombres ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ap", (object?)apellidos ?? DBNull.Value);
                cmd.Parameters.AddWithValue("dni", (object?)dni ?? DBNull.Value);
                cmd.Parameters.AddWithValue("tel", (object?)telefono ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ar", (object?)areaId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("id", u.Value.id);
                await cmd.ExecuteNonQueryAsync();
                TempData["Mensaje"] = "Datos actualizados";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando datos de perfil");
                TempData["Error"] = "No se pudo actualizar";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasena(string actual, string nueva, string confirmar)
        {
            // Validación de fuerza mínima
            bool cumpleFuerza = !string.IsNullOrWhiteSpace(nueva) && nueva.Length >= 8 && nueva.Any(char.IsUpper) && nueva.Any(char.IsLower) && nueva.Any(char.IsDigit);
            if (!cumpleFuerza)
            {
                TempData["Error"] = "La contraseña debe tener al menos 8 caracteres con mayúsculas, minúsculas y números";
                return RedirectToAction("Index");
            }
            if (nueva != confirmar)
            {
                TempData["Error"] = "La confirmación no coincide";
                return RedirectToAction("Index");
            }
            var correo = User.GetUserEmail();
            var u = await _repo.GetUserByEmailAsync(correo ?? "");
            if (!u.HasValue)
            {
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction("Index");
            }
            try
            {
                // Validar contraseña actual si existe hash
                if (!string.IsNullOrWhiteSpace(u.Value.passwordHash))
                {
                    if (!PasswordHelper.Verify(actual ?? string.Empty, u.Value.passwordHash!))
                    {
                        TempData["Error"] = "La contraseña actual es incorrecta";
                        return RedirectToAction("Index");
                    }
                }
                await _repo.UpdateUserPasswordAsync(u.Value.id, nueva);
                TempData["Mensaje"] = "Contraseña actualizada";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cambiando contraseña");
                TempData["Error"] = "No se pudo actualizar la contraseña";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DarDeBaja()
        {
            var correo = User.GetUserEmail();
            var u = await _repo.GetUserByEmailAsync(correo ?? "");
            if (!u.HasValue)
            {
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction("Index");
            }
            try
            {
                await _repo.DeleteUsuarioByIdAsync(u.Value.id);
                // Cerrar sesión después de eliminar
                return RedirectToAction("Logout", "Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al dar de baja la cuenta");
                TempData["Error"] = "No se pudo eliminar la cuenta";
                return RedirectToAction("Index");
            }
        }
    }
}