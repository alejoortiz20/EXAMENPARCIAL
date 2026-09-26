using EXAMENPARCIAL.Models;
using EXAMENPARCIAL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXAMENPARCIAL.Controllers;

[Authorize]
public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PieHostPublicador _piehost;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext db,
        PieHostPublicador piehost,
        ILogger<OperacionesController> logger)
    {
        _db = db;
        _piehost = piehost;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Incidencias() => View(await ConsultarAbiertas());

    [HttpGet]
    public async Task<IActionResult> Panel() => PartialView("_Panel", await ConsultarAbiertas());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id)
    {
        var incidencia = await _db.Incidencias.FindAsync(id);

        if (incidencia is not null && incidencia.Estado == "Abierta")
        {
            // 1) persistir primero
            incidencia.Estado = "Cerrada";
            await _db.SaveChangesAsync();
            _logger.LogInformation("Incidencia {Id} cerrada", id);

            // 2) recién entonces publicar desde el servidor
            await _piehost.PublicarAsync(
                "IncidenciaActualizada",
                new { Id = incidencia.Id, Estado = incidencia.Estado });
        }

        return RedirectToAction(nameof(Incidencias));
    }

    private async Task<List<Incidencia>> ConsultarAbiertas() =>
        await _db.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderByDescending(i => i.Prioridad)
            .ThenBy(i => i.Id)
            .ToListAsync();
}
