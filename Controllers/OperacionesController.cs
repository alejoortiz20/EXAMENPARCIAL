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
    private readonly CacheRedisService _cache;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext db,
        CacheRedisService cache,
        ILogger<OperacionesController> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Incidencias()
    {
        var resultado = await _cache.ObtenerListadoAbiertoAsync(async () =>
            await _db.Incidencias
                .Where(i => i.Estado == "Abierta")
                .OrderByDescending(i => i.Prioridad)
                .ThenBy(i => i.Id)
                .ToListAsync());

        ViewData["OrigenListado"] = resultado.Origen;

        return View(resultado.Items);
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

            await _cache.InvalidarListadoAsync();
        }

        return RedirectToAction(nameof(Incidencias));
    }
}
