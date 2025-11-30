namespace Campus_Virtul_GRLL.Models
{
    public enum EstadoCurso
    {
        Borrador,
        Publicado,
        Archivado
    }

    public class Curso
    {
        public int IdCurso { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int IdProfesor { get; set; }
        public EstadoCurso Estado { get; set; } = EstadoCurso.Borrador;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime? FechaPublicacion { get; set; }
    }
}
