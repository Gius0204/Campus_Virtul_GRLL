namespace Campus_Virtul_GRLL.Models
{
    public class Solicitud
    {
        public int IdSolicitud { get; set; }

        public int IdRol { get; set; }

        public required string Nombres { get; set; }

        public required string Apellidos { get; set; }

        public required string DNI { get; set; }

        public required string Telefono { get; set; }

        public required string CorreoElectronico { get; set; }

        public required string Area { get; set; }

        public DateOnly FechaSolicitud { get; set; } =DateOnly.FromDateTime(DateTime.Now);

        public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Enviada;

        public Rol Rol { get; set; } = null!;

        public string? NombreRol => Rol?.NombreRol;
    }
        public enum EstadoSolicitud
        {
            Enviada,
            EnRevision,
            Aprobada,
            Rechazada
    }
}
