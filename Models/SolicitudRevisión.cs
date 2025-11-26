namespace Campus_Virtul_GRLL.Models
{
    public class SolicitudRevisión
    {
        public int IdSolicitudRevision { get; set; }

        public int IdSolicitud { get; set; }

        public DateOnly fechaRevision { get; set; }

        public required string observaciones { get; set; }

        public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Enviada;

        public Solicitud Solicitud { get; set; } = null!;

        public Usuario usuario { get; set; } = null!;
    }

    public enum EstadoSolicitudRevision
    {
        Enviada,
        EnRevision,
        Aprobada,
        Rechazada
    }
}
