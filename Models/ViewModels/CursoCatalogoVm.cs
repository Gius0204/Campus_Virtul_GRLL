using System;

namespace Campus_Virtul_GRLL.Models.ViewModels
{
    public class CursoCatalogoVm
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string Estado { get; set; } = string.Empty;
        public bool EstaInscrito { get; set; }
        public bool TieneSolicitudPendiente { get; set; }
        public System.Collections.Generic.List<(System.Guid profesorId, string nombre, string correo)> Profesores { get; set; } = new();
    }
}
