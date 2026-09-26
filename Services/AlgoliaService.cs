using System.Text;
using System.Text.Json;

namespace EXAMENPARCIAL.Services;

public class AlgoliaOptions
{
    public string ApplicationId { get; set; } = string.Empty;
    public string SearchApiKey { get; set; } = string.Empty;
    public string Index { get; set; } = "incidencias";

    public string Host => $"{ApplicationId}-dsn.algolia.net";
}

public class AlgoliaService
{
    private readonly HttpClient _http;
    private readonly AlgoliaOptions _options;
    private readonly ILogger<AlgoliaService> _logger;

    public AlgoliaService(HttpClient http, AlgoliaOptions options, ILogger<AlgoliaService> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<List<int>> BuscarIdsAsync(string termino, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(termino))
        {
            return [];
        }

        var url = $"https://{_options.Host}/1/indexes/{_options.Index}/query";
        var payload = JsonSerializer.Serialize(new { query = termino, hitsPerPage = 100 });

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Algolia-API-Key", _options.SearchApiKey);
        request.Headers.Add("X-Algolia-Application-Id", _options.ApplicationId);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var ids = new List<int>();

            if (doc.RootElement.TryGetProperty("hits", out var hits))
            {
                foreach (var hit in hits.EnumerateArray())
                {
                    if (hit.TryGetProperty("objectID", out var objectId)
                        && int.TryParse(objectId.GetString(), out var id))
                    {
                        ids.Add(id);
                    }
                }
            }

            _logger.LogInformation("Algolia encontro {Cantidad} resultados para '{Termino}'", ids.Count, termino);
            return ids;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo la consulta a Algolia para '{Termino}'", termino);
            return [];
        }
    }
}
