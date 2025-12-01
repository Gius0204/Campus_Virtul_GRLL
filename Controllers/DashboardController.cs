using Campus_Virtul_GRLL.Helpers;
using Campus_Virtul_GRLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Campus_Virtul_GRLL.Controllers
{
    [Authorize]
    public class DashboardController:Controller
    {
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ILogger<DashboardController> logger)
        {
            _logger = logger;
        }

        /// Dashboard principal 
        public IActionResult Index()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "-";
            var userName = User.GetUserName();
            var userRole = User.GetUserRole();

            _logger.LogInformation($"Usuario {userName} (ID: {userId}, Rol: {userRole}) accedió al Dashboard");

            return userRole switch
            {
                "Administrador" => RedirectToAction("PanelAdministrador", "Administrador"),
                "Colaborador" => RedirectToAction("PanelColaborador"),
                "Profesor" => RedirectToAction("PanelProfesor"),
                "Practicante" => RedirectToAction("PanelPracticante"),
                _ => View("DashboardGeneral")
            };
        }

        /// Panel de administración - Solo para Administradores
        [Authorize(Roles = "Administrador")]
        public IActionResult PanelAdministrador()
        {
            // Obtener datos del usuario autenticado
            ViewBag.NombreCompleto = User.GetUserName();
            ViewBag.Rol = User.GetUserRole();
            ViewBag.Area = User.GetUserArea();
            ViewBag.DNI = User.GetUserDNI();

            _logger.LogInformation($"Administrador {User.GetUserName()} accedió al panel de administración");

            return View();
        }

        /// Panel de Colaborador
        [Authorize(Roles = "Colaborador")]
        public IActionResult PanelColaborador()
        {
            ViewBag.NombreColaborador = User.GetUserName();
            ViewBag.Area = User.GetUserArea();
            ViewBag.Email = User.GetUserEmail();
            ViewBag.Telefono = User.GetUserTelefono();
            ViewBag.Rol = "Colaborador";

            _logger.LogInformation($"Colaborador {User.GetUserName()} accedió a su panel");

            return View();
        }

        [Authorize(Roles = "Profesor")]
        public IActionResult PanelProfesor()
        {
            var nombreProfesor = User.GetUserName();
            var areaProfesor = User.GetUserArea();
            var emailProfesor = User.GetUserEmail();
            var dniProfesor = User.GetUserDNI();

            ViewBag.NombreProfesor = nombreProfesor;
            ViewBag.Area = areaProfesor;
            ViewBag.Email = emailProfesor;
            ViewBag.DNI = dniProfesor;
            ViewBag.Rol = "Profesor";

            ViewBag.MensajeBienvenida = $"Bienvenido, Profesor {User.GetNombres()}";

            _logger.LogInformation($"Profesor {nombreProfesor} (Área: {areaProfesor}) accedió a su panel");

            return View();
        }

        /// Panel exclusivo para Practicantes
        [Authorize(Roles = "Practicante")]
        public IActionResult PanelPracticante()
        {
            ViewBag.NombrePracticante = User.GetUserName();
            ViewBag.Area = User.GetUserArea();
            ViewBag.Email = User.GetUserEmail();
            ViewBag.Rol = "Practicante";

            _logger.LogInformation($"Practicante {User.GetUserName()} accedió a su panel");

            return View();
        }

        /// Gestión de cursos - Accesible para Administrador y Profesor
        [Authorize(Roles = "Administrador,Profesor")]
        public IActionResult GestionCursos()
        {
            // Verificar qué rol tiene el usuario
            var esAdministrador = User.IsInRole("Administrador");
            var esProfesor = User.IsInRole("Profesor");

            // Enviar información a la vista
            ViewBag.EsAdministrador = esAdministrador;
            ViewBag.EsProfesor = esProfesor;
            ViewBag.NombreUsuario = User.GetUserName();
            ViewBag.Rol = User.GetUserRole();

            // Los administradores tienen permisos completos
            // Los profesores solo pueden ver sus propios cursos
            ViewBag.PermisosCompletos = esAdministrador;

            return View();
        }

        /// Página de reportes - No accesible para Practicantes
        [Authorize(Roles = "Administrador,Profesor,Colaborador")]
        public IActionResult Reportes()
        {
            ViewBag.NombreUsuario = User.GetUserName();
            ViewBag.Rol = User.GetUserRole();
            ViewBag.EsAdministrador = User.IsInRole("Administrador");

            return View();
        }
    }
}