using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Campus_Virtul_GRLL.Services;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class UsuariosController : Controller
    {
        private readonly SupabaseRepository _repo;
        private readonly ILogger<UsuariosController> _logger;

        public UsuariosController(SupabaseRepository repo, ILogger<UsuariosController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? rol)
        {
            var usuarios = await _repo.GetUsuariosAsync();
            var roles = await _repo.GetRolesAsync();
            if (!string.IsNullOrWhiteSpace(rol))
            {
                usuarios = usuarios.Where(u => string.Equals(u.rolNombre, rol, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            var vm = usuarios.Select(u => new Models.DbUsuarioViewModel
            {
                Id = u.id,
                Nombre = u.nombres,
                Correo = u.correo,
                Activo = u.activo,
                RolId = u.rolId,
                RolNombre = u.rolNombre
            }).ToList();
            ViewBag.Roles = roles; // lista de (Guid id, string nombre)
            ViewBag.FiltroRol = rol;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarRol(Guid idUsuario, Guid idRol)
        {
            try
            {
                await _repo.CambiarRolUsuarioAsync(idUsuario, idRol);
                TempData["Mensaje"] = "Rol actualizado";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cambiando rol usuario {UsuarioId}", idUsuario);
                TempData["Error"] = "Error al cambiar rol";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEstado(Guid idUsuario)
        {
            try
            {
                await _repo.ToggleEstadoUsuarioAsync(idUsuario);
                TempData["Mensaje"] = "Estado alternado";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error alternando estado usuario {UsuarioId}", idUsuario);
                TempData["Error"] = "Error al cambiar estado";
            }
            return RedirectToAction("Index", new { rol = Request.Query["rol"].FirstOrDefault() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(Guid idUsuario)
        {
            try
            {
                await _repo.DeleteUsuarioByIdAsync(idUsuario);
                TempData["Mensaje"] = "Usuario eliminado";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando usuario {UsuarioId}", idUsuario);
                TempData["Error"] = "Error al eliminar usuario";
            }
            return RedirectToAction("Index", new { rol = Request.Query["rol"].FirstOrDefault() });
        }
    }
}
