namespace Campus_Virtul_GRLL.Models
{
    public enum EstadoInscripcion
    {
        Pendiente,
        Aprobada,
        Rechazada
    }

    public class Inscripcion
    {
        public int IdInscripcion { get; set; }
        public int IdCurso { get; set; }
        public int IdUsuario { get; set; }
        public DateTime FechaSolicitud { get; set; } = DateTime.Now;
        public DateTime? FechaRespuesta { get; set; }
        public EstadoInscripcion Estado { get; set; } = EstadoInscripcion.Pendiente;
    }
}
