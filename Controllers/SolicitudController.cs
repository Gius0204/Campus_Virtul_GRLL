using Campus_Virtul_GRLL.Services;
using Campus_Virtul_GRLL.Models;
using Microsoft.AspNetCore.Mvc;

namespace Campus_Virtul_GRLL.Controllers
{
    public class SolicitudController : Controller
    {
        private readonly SupabaseRepository _repo;

        public SolicitudController(SupabaseRepository repo)
        {
            _repo = repo;
        }

        [HttpGet]
        public async Task<IActionResult> Solicitud()
        {
            var roles = await _repo.GetRolesAsync();
            ViewBag.Roles = roles;
            var areas = await _repo.GetAreasAsync();
            ViewBag.Areas = areas;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Solicitud(Solicitud modelo)
        {
            try
            {
                // Remover validación de la propiedad de navegación y campos que se parsean manualmente
                ModelState.Remove("Rol");
                ModelState.Remove("IdRol");
                ModelState.Remove("Area");

                // Validar el ModelState
                if (!ModelState.IsValid)
                {
                    var errores = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    TempData["Error"] = $"Errores de validación: {errores}";
                    var roles = await _repo.GetRolesAsync();
                    ViewBag.Roles = roles;
                    var areas = await _repo.GetAreasAsync();
                    ViewBag.Areas = areas;
                    return View(modelo);
                }

                // Validar rol seleccionado (desde combo por Guid)
                    // Verificar que el rol exista usando el repositorio
                    var rolSeleccionado = Request.Form["IdRol"].FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(rolSeleccionado) || !Guid.TryParse(rolSeleccionado, out var rolId))
                    {
                        TempData["Error"] = "Debe seleccionar un tipo de usuario válido.";
                        return View(modelo);
                    }

                // Validar todos los campos duplicandos a la vez
                var camposDuplicados = new List<string>();

                // Verificar Nombres + Apellidos
                    /* TODO: Validaciones duplicadas contra DB (correo/dni). Por ahora omitimos InMemory */
                    /* if (_store.Solicitudes.Values.Any(s =>
                        s.Nombres.ToLower() == modelo.Nombres.Trim().ToLower() &&
                        s.Apellidos.ToLower() == modelo.Apellidos.Trim().ToLower()))
                    {
                        camposDuplicados.Add($"Nombre completo ({modelo.Nombres} {modelo.Apellidos})");
                    } */

                // Verificar DNI
                    // Validar duplicados en DB
                    if (await _repo.ExisteUsuarioPorDniAsync(modelo.DNI.Trim()))
                    {
                        camposDuplicados.Add($"DNI ({modelo.DNI})");
                    }

                // Verificar Teléfono
                    // TODO: Validar Teléfono duplicado en DB

                // Verificar Correo
                    if (await _repo.ExisteUsuarioPorCorreoAsync(modelo.CorreoElectronico.Trim().ToLower()))
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

                // Crear usuario inactivo en DB con rol y área seleccionada
                // Convertir Area a areaId (la vista se actualizará para enviar Guid)
                Guid? areaId = null;
                    var areaSeleccionada = Request.Form["Area"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(areaSeleccionada) && Guid.TryParse(areaSeleccionada, out var aid)) areaId = aid;
                var nuevoUsuarioId = await _repo.CreateUserAsync(
                    nombres: modelo.Nombres.Trim(),
                    correo: modelo.CorreoElectronico.Trim().ToLower(),
                        rolId: rolId,
                    activo: false,
                        plainPassword: null,
                    apellidos: modelo.Apellidos.Trim(),
                    dni: modelo.DNI.Trim(),
                    telefono: modelo.Telefono.Trim(),
                    areaId: areaId
                );

                // Crear solicitud vinculada
                await _repo.CreateSolicitudAsync(nuevoUsuarioId, "registro", "Solicitud de registro de usuario");

                TempData["Mensaje"] = "Tu solicitud ha sido enviada correctamente. Espera la revisión del administrador.";
                return RedirectToAction("Index", "Login");
            }

            catch (Exception ex)
            {
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = $"Error inesperado: {errorMessage}";
                var roles = await _repo.GetRolesAsync();
                ViewBag.Roles = roles;
                var areas = await _repo.GetAreasAsync();
                ViewBag.Areas = areas;
                return View(modelo);
            }
        }
    }
}