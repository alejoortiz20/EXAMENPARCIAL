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
    private readonly AlgoliaService _algolia;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext db,
        AlgoliaService algolia,
        ILogger<OperacionesController> logger)
    {
        _db = db;
        _algolia = algolia;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Incidencias(string? q)
    {
        var termino = q?.Trim();

        if (string.IsNullOrEmpty(termino))
        {
            var abiertas = await _db.Incidencias
                .Where(i => i.Estado == "Abierta")
                .OrderByDescending(i => i.Prioridad)
                .ThenBy(i => i.Id)
                .ToListAsync();

            ViewData["Busqueda"] = string.Empty;
            return View(abiertas);
        }

        var ids = await _algolia.BuscarIdsAsync(termino);

        var encontradas = await _db.Incidencias
            .Where(i => i.Estado == "Abierta" && ids.Contains(i.Id))
            .ToListAsync();

        var ordenadas = ids
            .Where(id => encontradas.Any(e => e.Id == id))
            .Select(id => encontradas.First(e => e.Id == id))
            .ToList();

        ViewData["Busqueda"] = termino;

        return View(ordenadas);
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

        return RedirectToAction(nameof(Incidencias), new { q = Request.Query["q"] });
    }
}
