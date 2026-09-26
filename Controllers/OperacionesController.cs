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
    private readonly CacheRedisService _cache;
    private readonly PieHostPublicador _piehost;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext db,
        AlgoliaService algolia,
        CacheRedisService cache,
        PieHostPublicador piehost,
        ILogger<OperacionesController> logger)
    {
        _db = db;
        _algolia = algolia;
        _cache = cache;
        _piehost = piehost;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Incidencias(string? q)
    {
        var termino = q?.Trim();

        // Sin texto de busqueda: el listado general sale de la cache de Redis.
        if (string.IsNullOrEmpty(termino))
        {
            var resultado = await _cache.ObtenerListadoAbiertoAsync(ConsultarAbiertas);

            ViewData["OrigenListado"] = resultado.Origen;
            ViewData["Busqueda"] = string.Empty;

            return View(resultado.Items);
        }

        // Con texto: se consulta Algolia directo, sin pasar por la cache.
        var ids = await _algolia.BuscarIdsAsync(termino);

        var encontradas = await _db.Incidencias
            .Where(i => i.Estado == "Abierta" && ids.Contains(i.Id))
            .ToListAsync();

        var ordenadas = ids
            .Where(id => encontradas.Any(e => e.Id == id))
            .Select(id => encontradas.First(e => e.Id == id))
            .ToList();

        ViewData["Busqueda"] = termino;
        ViewData["OrigenListado"] = "Algolia";

        return View(ordenadas);
    }

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

            // 2) invalidar la cache del listado antes de volver a consultarlo
            await _cache.InvalidarListadoAsync();

            // 3) recien entonces publicar desde el servidor
            await _piehost.PublicarAsync(
                "IncidenciaActualizada",
                new { Id = incidencia.Id, Estado = incidencia.Estado });
        }

        return RedirectToAction(nameof(Incidencias), new { q = Request.Query["q"] });
    }

    private async Task<List<Incidencia>> ConsultarAbiertas() =>
        await _db.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderByDescending(i => i.Prioridad)
            .ThenBy(i => i.Id)
            .ToListAsync();
}
