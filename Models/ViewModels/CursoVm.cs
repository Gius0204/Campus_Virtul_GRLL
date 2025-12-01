using System;

namespace Campus_Virtul_GRLL.Models.ViewModels
{
    public class CursoVm
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string Estado { get; set; } = "borrador";
    }
}
