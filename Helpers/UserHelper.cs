using System.Security.Claims;

namespace Campus_Virtul_GRLL.Helpers
{
    /// Métodos de extensión para acceder fácilmente a los datos del usuario autenticado
    public static class UserHelper
    {
        // ============================================
        // INFORMACIÓN BÁSICA DEL USUARIO
        // ============================================

        /// Obtiene el ID del usuario autenticado
        public static int GetUserId(this ClaimsPrincipal user)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userId, out var id) ? id : 0;
        }

        /// Obtiene el nombre completo del usuario
        public static string GetUserName(this ClaimsPrincipal user)
        {
            return user.Identity?.Name ?? "Anónimo";
        }

        /// Obtiene solo el nombre (sin apellidos)
        public static string GetNombres(this ClaimsPrincipal user)
        {
            return user.FindFirst("Nombres")?.Value ?? "";
        }

        /// Obtiene los apellidos
        public static string GetApellidos(this ClaimsPrincipal user)
        {
            return user.FindFirst("Apellidos")?.Value ?? "";
        }

        /// Obtiene el DNI del usuario
        public static string GetUserDNI(this ClaimsPrincipal user)
        {
            return user.FindFirst("DNI")?.Value ?? "";
        }

        /// Obtiene el correo electrónico
        public static string GetUserEmail(this ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.Email)?.Value ?? "";
        }

        /// Obtiene el teléfono
        public static string GetUserTelefono(this ClaimsPrincipal user)
        {
            return user.FindFirst("Telefono")?.Value ?? "";
        }

        /// Obtiene el área del usuario
        public static string GetUserArea(this ClaimsPrincipal user)
        {
            return user.FindFirst("Area")?.Value ?? "";
        }

        // ============================================
        // INFORMACIÓN DE ROL
        // ============================================

        /// Obtiene el rol del usuario
        public static string GetUserRole(this ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.Role)?.Value ?? "Usuario";
        }

        /// Obtiene el ID del rol
        public static int GetRolId(this ClaimsPrincipal user)
        {
            var rolId = user.FindFirst("RolId")?.Value;
            return int.TryParse(rolId, out var id) ? id : 0;
        }

        // ============================================
        // VERIFICACIÓN DE ROLES ESPECÍFICOS
        // ============================================

        /// Verifica si el usuario tiene un rol específico
        public static bool TieneRol(this ClaimsPrincipal user, string rol)
        {
            return user.IsInRole(rol);
        }

        /// Verifica si el usuario es administrador
        public static bool EsAdministrador(this ClaimsPrincipal user)
        {
            return user.IsInRole("Administrador");
        }

        /// Verifica si el usuario es Profesor
        public static bool EsProfesor(this ClaimsPrincipal user)
        {
            return user.IsInRole("Profesor");
        }

        /// Verifica si el usuario es Colaborador
        public static bool EsColaborador(this ClaimsPrincipal user)
        {
            return user.IsInRole("Colaborador");
        }

        /// Verifica si el usuario es Practicante
        public static bool EsPracticante(this ClaimsPrincipal user)
        {
            return user.IsInRole("Practicante");
        }

        // ============================================
        // VERIFICACIONES DE PERMISOS COMBINADOS
        // ============================================

        /// Verifica si el usuario puede gestionar usuarios
        public static bool PuedeGestionarUsuarios(this ClaimsPrincipal user)
        {
            return user.IsInRole("Administrador");
        }

        // ============================================
        // INFORMACIÓN DE SESIÓN
        // ============================================

        /// Verifica si es el primer inicio del usuario
        public static bool EsPrimerInicio(this ClaimsPrincipal user)
        {
            var primerInicio = user.FindFirst("PrimerInicio")?.Value;
            return bool.TryParse(primerInicio, out var result) && result;
        }
    }
}
