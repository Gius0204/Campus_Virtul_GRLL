using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Campus_Virtul_GRLL.Data;
using Campus_Virtul_GRLL.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Campus_Virtul_GRLL.Controllers
{
    public class PracticanteController : Controller
    {
        private readonly AppDBContext _db;

        public PracticanteController(AppDBContext db)
        {
            _db = db;
        }

        // GET: /Practicante/Catalogo
        public async Task<IActionResult> Catalogo()
        {
            var modulos = await _db.Modulos.AsNoTracking().ToListAsync();
            return View("~/Views/Practicante/Index.cshtml", modulos);
        }

        // GET: /Practicante/MisCursos
        public async Task<IActionResult> MisCursos()
        {
            // No hay relación de cursos en el modelo actual; mostramos los mismos módulos como accesibles
            var modulos = await _db.Modulos.AsNoTracking().ToListAsync();
            return View("~/Views/Practicante/MisCursos.cshtml", modulos);
        }

        // GET: /Practicante/Detalle/{id}
        public async Task<IActionResult> Detalle(int id)
        {
            var modulo = await _db.Modulos.AsNoTracking().FirstOrDefaultAsync(m => m.IdModulo == id);
            if (modulo == null) return NotFound();
            return View("~/Views/Practicante/Detalle.cshtml", modulo);
        }

        // POST: /Practicante/SolicitarInscripcion/{cursoId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SolicitarInscripcion(int moduloId)
        {
            // El flujo de inscripción no está modelado en esta base; dejamos como no-op por ahora
            return RedirectToAction(nameof(Catalogo));
        }
    }
}
