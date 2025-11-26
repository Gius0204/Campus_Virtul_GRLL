namespace Campus_Virtul_GRLL.Models
{
    public class Modulo
    {
        
        public int IdModulo { get; set; }

        public required string Titulo { get; set; }

        public required string Descripcion { get; set; }

        public bool Estado { get; set; }
    }
}
