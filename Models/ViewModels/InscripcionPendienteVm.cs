using System;

namespace Campus_Virtul_GRLL.Models.ViewModels
{
    public class InscripcionPendienteVm
    {
        public Guid SolicitudId { get; set; }
        public Guid CursoId { get; set; }
        public string CursoTitulo { get; set; } = string.Empty;
        public Guid PracticanteId { get; set; }
        public string PracticanteNombre { get; set; } = string.Empty;
        public string PracticanteCorreo { get; set; } = string.Empty;
        public DateTime CreadaEn { get; set; }
    }
}
