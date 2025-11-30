namespace Campus_Virtul_GRLL.Models
{
    public enum EstadoTarea
    {
        Activa,
        Expirada
    }

    public class Tarea
    {
        public int IdTarea { get; set; }
        public DateTime FechaLimite { get; set; }
        public EstadoTarea Estado { get; set; } = EstadoTarea.Activa;
    }
}
