using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Microsoft.AspNetCore.Mvc;

namespace Campus_Virtul_GRLL.Controllers
{
    public class SolicitudController : Controller
    {
        private readonly InMemoryDataStore _store;

        public SolicitudController(InMemoryDataStore store)
        {
            _store = store;
        }

        [HttpGet]
        public IActionResult Solicitud()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Solicitud(Solicitud modelo)
        {
            try
            {
                // Remover validación de la propiedad de navegación Rol
                ModelState.Remove("Rol");

                // Validar el ModelState
                if (!ModelState.IsValid)
                {
                    var errores = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    TempData["Error"] = $"Errores de validación: {errores}";
                    return View(modelo);
                }

                if (modelo.IdRol == 0 || (modelo.IdRol != 1 && modelo.IdRol != 2))
                {
                    TempData["Error"] = "Debe seleccionar un tipo de usuario valido.";
                    return View(modelo);
                }

                // Verificar que el rol existe en la base de datos
                if (!_store.Roles.ContainsKey(modelo.IdRol))
                {
                    TempData["Error"] = "El Tipo de usuario seleccionado no existe.";
                    return View(modelo);
                }

                // Validar todos los campos duplicandos a la vez
                var camposDuplicados = new List<string>();

                // Verificar Nombres + Apellidos
                if (_store.Solicitudes.Values.Any(s =>
                    s.Nombres.ToLower() == modelo.Nombres.Trim().ToLower() &&
                    s.Apellidos.ToLower() == modelo.Apellidos.Trim().ToLower()))
                {
                    camposDuplicados.Add($"Nombre completo ({modelo.Nombres} {modelo.Apellidos})");
                }

                // Verificar DNI
                if (_store.Solicitudes.Values.Any(s => s.DNI == modelo.DNI.Trim()))
                {
                    camposDuplicados.Add($"DNI ({modelo.DNI})");
                }

                // Verificar Teléfono
                if (_store.Solicitudes.Values.Any(s => s.Telefono == modelo.Telefono.Trim()))
                {
                    camposDuplicados.Add($"Teléfono ({modelo.Telefono})");
                }

                // Verificar Correo
                if (_store.Solicitudes.Values.Any(s => s.CorreoElectronico == modelo.CorreoElectronico.Trim()))
                {
                    camposDuplicados.Add($"Correo electrónico ({modelo.CorreoElectronico})");
                }

                // Si hay duplicados, mostrar todos
                if (camposDuplicados.Any())
                {
                    var mensajeDuplicados = string.Join(", ", camposDuplicados);
                    TempData["Error"] = $"Los siguientes datos ya están registrados en la base de datos: {mensajeDuplicados}. Por favor, verifique los datos ingresados.";
                    return View(modelo);
                }

                // Crear y guardar la solicitud
                _store.AddSolicitud(modelo.IdRol, modelo.Nombres.Trim(), modelo.Apellidos.Trim(), modelo.DNI.Trim(), modelo.Telefono.Trim(), modelo.CorreoElectronico.Trim(), modelo.Area.Trim());

                TempData["Mensaje"] = "Tu solicitud ha sido enviada correctamente. Espera la revisión del administrador.";
                return View();
            }

            catch (Exception ex)
            {
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = $"Error inesperado: {errorMessage}";
                return View(modelo);
            }
        }
    }
}