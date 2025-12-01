using System;

namespace Campus_Virtul_GRLL.Models
{
    public class DbUsuarioViewModel
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public Guid RolId { get; set; }
        public string RolNombre { get; set; } = string.Empty;
    }
}
