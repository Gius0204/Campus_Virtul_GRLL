namespace Campus_Virtul_GRLL.Models
{
    public class Sesion
    {
        public int IdSesion { get; set; }
        public int IdCurso { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int Orden { get; set; }
    }
}
