namespace Campus_Virtul_GRLL.Models
{
    public enum EstadoEntrega
    {
        Entregada,
        Calificada,
        FueraDeTiempo
    }

    public class EntregaTarea
    {
        public int IdEntregaTarea { get; set; }
        public int IdTarea { get; set; }
        public int IdUsuario { get; set; }
        public string RutaArchivo { get; set; } = string.Empty;
        public DateTime FechaEntrega { get; set; } = DateTime.Now;
        public int? Nota { get; set; }
        public EstadoEntrega Estado { get; set; } = EstadoEntrega.Entregada;
    }
}
