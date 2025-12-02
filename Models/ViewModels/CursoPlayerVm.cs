using System;
using System.Collections.Generic;

namespace Campus_Virtul_GRLL.Models.ViewModels
{
    public class CursoPlayerVm
    {
        public Guid CursoId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public List<SesionVm> Sesiones { get; set; } = new();
        public SubDetalleVm? Seleccionada { get; set; }
    }

    public class SesionVm
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public int Orden { get; set; }
        public List<SubResumenVm> Subsecciones { get; set; } = new();
    }

    public class SubResumenVm
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Tipo { get; set; } = "contenido"; // contenido | video | tarea
        public int Orden { get; set; }
        public DateTimeOffset? FechaLimite { get; set; }
    }

    public class SubDetalleVm
    {
        public Guid Id { get; set; }
        public Guid SesionId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Tipo { get; set; } = "contenido";
        public string Estado { get; set; } = "publicado";
        public string? Texto { get; set; }
        public string? ArchivoUrlSigned { get; set; }
        public string? ArchivoMime { get; set; }
        public long? ArchivoSize { get; set; }
        public string? VideoUrlSigned { get; set; }
        public string? VideoMime { get; set; }
        public long? VideoSize { get; set; }
        public int? VideoDuracion { get; set; }
        public DateTimeOffset? FechaLimite { get; set; }
        public int? MaxPuntaje { get; set; }
    }
}
