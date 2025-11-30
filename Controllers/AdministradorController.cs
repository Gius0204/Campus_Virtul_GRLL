using Microsoft.AspNetCore.Authorization;
using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using Microsoft.AspNetCore.Authorization;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class AdministradorController : Controller
    {
        private readonly InMemoryDataStore _store;

        public AdministradorController(InMemoryDataStore store)
        {
            _store = store;
        }

        [HttpGet]
        public IActionResult PanelAdministrador()
        {
            return View();
        }

        [HttpGet]
        public IActionResult PanelSolicitudes()
        {
            try
            {
                var solicitudes = _store.Solicitudes.Values
                    .OrderByDescending(s => s.FechaSolicitud)
                    .ToList();

                return View(solicitudes);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar las solicitudes: {ex.Message}";
                return View(new List<Solicitud>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles="Administrador")]
        public IActionResult AprobarSolicitud(int id)
        {
            try
            {
                if (!_store.Solicitudes.TryGetValue(id, out var solicitud))
                {
                    return Json(new { success = false, message = "Solicitud no encontrada." });
                }
                _store.AprobarSolicitud(id);

                return Json(new { success = true, message = "Solicitud aprobada correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles="Administrador")]
        public IActionResult RechazarSolicitud(int id)
        {
            try
            {
                if (!_store.Solicitudes.TryGetValue(id, out var solicitud))
                {
                    return Json(new { success = false, message = "Solicitud no encontrada." });
                }
                _store.RechazarSolicitud(id);

                return Json(new { success = true, message = "Solicitud rechazada correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles="Administrador")]
        public IActionResult EliminarSolicitud(int id)
        {
            try
            {
                if (!_store.Solicitudes.TryGetValue(id, out var solicitud))
                {
                    return Json(new { success = false, message = "Solicitud no encontrada." });
                }
                _store.Solicitudes.TryRemove(id, out _);

                return Json(new { success = true, message = "Solicitud eliminada correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
        // Crear curso (GET)
        [Authorize(Roles="Administrador")]
        [HttpGet]
        public IActionResult CrearCurso()
        {
            ViewBag.Profesores = _store.Usuarios.Values.Where(u => u.Rol?.NombreRol == "Profesor").ToList();
            return View();
        }

        // Crear curso (POST)
        [Authorize(Roles="Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearCurso(string titulo, string descripcion, int idProfesor)
        {
            if (string.IsNullOrWhiteSpace(titulo))
            {
                TempData["Error"] = "El título es obligatorio";
                return RedirectToAction("CrearCurso");
            }
            if (!_store.Usuarios.ContainsKey(idProfesor) || _store.Usuarios[idProfesor].Rol?.NombreRol != "Profesor")
            {
                TempData["Error"] = "Profesor inválido";
                return RedirectToAction("CrearCurso");
            }
            _store.AddCurso(titulo.Trim(), descripcion?.Trim() ?? string.Empty, _store.Usuarios[idProfesor].IdRol, idProfesor);
            TempData["Mensaje"] = "Curso creado";
            return RedirectToAction("PanelAdministrador");
        }
    }
}