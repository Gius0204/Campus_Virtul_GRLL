namespace Campus_Virtul_GRLL.Models
{
    public class Rol
    {
        
        public int IdRol { get; set; }

        public required string NombreRol { get; set; }

        public required string Descripcion { get; set; }

        public bool Estado { get; set; }
    }
}
