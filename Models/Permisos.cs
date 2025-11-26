namespace Campus_Virtul_GRLL.Models
{
    public class Permisos
    {
        public int IdPermisos { get; set; }
        public int IdRol { get; set; }
        public int IdModulo { get; set; }

        public bool Crear { get; set; }
        public bool Editar { get; set; }
        public bool Revisar { get; set; }
        public bool Aprobar { get; set; }
        public bool Visualizar { get; set; }

        public Rol Rol { get; set; } = null!;
        public Modulo Modulos { get; set; } = null!;
    }
}
