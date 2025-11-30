using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Campus_Virtul_GRLL.Services;
using System.Linq;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class UsuariosController : Controller
    {
        private readonly InMemoryDataStore _store;
        private readonly ILogger<UsuariosController> _logger;

        public UsuariosController(InMemoryDataStore store, ILogger<UsuariosController> logger)
        {
            _store = store;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var usuarios = _store.Usuarios.Values.OrderBy(u => u.Apellidos).ThenBy(u => u.Nombres).ToList();
            ViewBag.Roles = _store.Roles.Values.OrderBy(r => r.NombreRol).ToList();
            return View(usuarios);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarRol(int idUsuario, int idRol)
        {
            if (!_store.Usuarios.TryGetValue(idUsuario, out var usuario)) { TempData["Error"] = "Usuario no encontrado"; return RedirectToAction("Index"); }
            if (!_store.Roles.TryGetValue(idRol, out var rol)) { TempData["Error"] = "Rol no válido"; return RedirectToAction("Index"); }

            usuario.IdRol = idRol;
            usuario.Rol = rol;
            usuario.FechaActualizacion = DateOnly.FromDateTime(DateTime.Now);
            TempData["Mensaje"] = $"Rol actualizado a '{rol.NombreRol}'";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleEstado(int idUsuario)
        {
            if (!_store.Usuarios.TryGetValue(idUsuario, out var usuario)) { TempData["Error"] = "Usuario no encontrado"; return RedirectToAction("Index"); }
            usuario.Estado = !usuario.Estado;
            usuario.FechaActualizacion = DateOnly.FromDateTime(DateTime.Now);
            TempData["Mensaje"] = usuario.Estado ? "Cuenta activada" : "Cuenta desactivada";
            return RedirectToAction("Index");
        }
    }
}
