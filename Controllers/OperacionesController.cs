using EXAMENPARCIAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXAMENPARCIAL.Controllers;

[Authorize]
public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(ApplicationDbContext db, ILogger<OperacionesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Incidencias()
    {
        var abiertas = await _db.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderByDescending(i => i.Prioridad)
            .ThenBy(i => i.Id)
            .ToListAsync();

        _logger.LogInformation("Listado de incidencias abiertas: {Cantidad} resultados desde la base de datos", abiertas.Count);

        return View(abiertas);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id)
    {
        var incidencia = await _db.Incidencias.FindAsync(id);

        if (incidencia is not null && incidencia.Estado == "Abierta")
        {
            incidencia.Estado = "Cerrada";
            await _db.SaveChangesAsync();
            _logger.LogInformation("Incidencia {Id} cerrada", id);
        }

        return RedirectToAction(nameof(Incidencias));
    }
}
