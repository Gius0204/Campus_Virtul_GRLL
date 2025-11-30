namespace Campus_Virtul_GRLL.Models
{
    public enum TipoSubSeccion
    {
        Contenido,
        Video,
        Tarea
    }

    public class SubSeccion
    {
        public int IdSubSeccion { get; set; }
        public int IdSesion { get; set; }
        public TipoSubSeccion Tipo { get; set; }
        public string Titulo { get; set; } = string.Empty;
        // Contenido
        public string? Texto { get; set; }
        public string? RutaArchivo { get; set; }
        // Video
        public string? RutaVideo { get; set; }
        // Tarea
        public int? IdTarea { get; set; }
    }
}
