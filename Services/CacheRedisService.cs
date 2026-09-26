using System.Text.Json;
using EXAMENPARCIAL.Models;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace EXAMENPARCIAL.Services;

public sealed class RedisConexion
{
    public IConnectionMultiplexer? Cliente { get; }

    public RedisConexion(IConfiguration configuracion, ILogger<RedisConexion> logger)
    {
        var cadena = configuracion["REDIS_URL"] ?? configuracion["REDIS_CONNECTION"];

        if (string.IsNullOrWhiteSpace(cadena))
        {
            logger.LogWarning(
                "REDIS_URL no esta definida. El listado general se servira siempre desde la base de datos.");
            return;
        }

        try
        {
            Cliente = ConnectionMultiplexer.Connect(CrearOpciones(cadena));
            logger.LogInformation(
                "Conexion con Redis establecida con {Total} endpoint(s).",
                Cliente.GetEndPoints().Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible conectar con Redis. Se usara la base de datos.");
            Cliente = null;
        }
    }

    private static ConfigurationOptions CrearOpciones(string cadena)
    {
        if (cadena.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) ||
            cadena.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(cadena);

            var opciones = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)
            };

            opciones.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var partes = uri.UserInfo.Split(':', 2);

                if (partes.Length == 2)
                {
                    opciones.User = Uri.UnescapeDataString(partes[0]);
                    opciones.Password = Uri.UnescapeDataString(partes[1]);
                }
                else
                {
                    opciones.Password = Uri.UnescapeDataString(partes[0]);
                }
            }

            return opciones;
        }

        var parseada = ConfigurationOptions.Parse(cadena);
        parseada.AbortOnConnectFail = false;
        return parseada;
    }
}

public sealed record ResultadoListado(List<Incidencia> Items, string Origen);

public class CacheRedisService
{
    public const string ClaveListadoAbierto = "incidencias:abiertas:listado";
    public static readonly TimeSpan Duracion = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly RedisConexion _conexion;
    private readonly ILogger<CacheRedisService> _logger;

    public CacheRedisService(RedisConexion conexion, ILogger<CacheRedisService> logger)
    {
        _conexion = conexion;
        _logger = logger;
    }

    private IConnectionMultiplexer? Cliente =>
        _conexion.Cliente is { IsConnected: true } cliente ? cliente : null;

    public async Task<ResultadoListado> ObtenerListadoAbiertoAsync(
        Func<Task<List<Incidencia>>> consultarBase)
    {
        var redis = Cliente;

        if (redis is null)
        {
            _logger.LogWarning(
                "Cache Redis no disponible. Lectura desde la BASE de datos.");
            return new ResultadoListado(await consultarBase(), "Base de datos");
        }

        try
        {
            var valor = await redis.GetDatabase().StringGetAsync(ClaveListadoAbierto);

            if (valor.HasValue)
            {
                var enCache = JsonSerializer.Deserialize<List<Incidencia>>(valor.ToString()!, Json)
                              ?? new List<Incidencia>();

                _logger.LogInformation(
                    "Lectura desde REDIS: {Cantidad} incidencias (clave {Clave}).",
                    enCache.Count, ClaveListadoAbierto);

                return new ResultadoListado(enCache, "Redis");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al leer Redis. Lectura desde la BASE de datos.");
            return new ResultadoListado(await consultarBase(), "Base de datos");
        }

        var desdeBase = await consultarBase();

        try
        {
            var json = JsonSerializer.Serialize(desdeBase, Json);
            await redis.GetDatabase().StringSetAsync(ClaveListadoAbierto, json, Duracion);

            _logger.LogInformation(
                "Lectura desde la BASE: {Cantidad} incidencias. Clave {Clave} guardada por {Segundos} s.",
                desdeBase.Count, ClaveListadoAbierto, (int)Duracion.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo escribir en Redis. Se omite el guardado.");
        }

        return new ResultadoListado(desdeBase, "Base de datos");
    }

    public async Task InvalidarListadoAsync()
    {
        var redis = Cliente;

        if (redis is null)
        {
            _logger.LogWarning("Cache Redis no disponible. Nada que invalidar.");
            return;
        }

        try
        {
            var borrada = await redis.GetDatabase().KeyDeleteAsync(ClaveListadoAbierto);

            _logger.LogInformation(
                "Clave {Clave} invalidada: {Borrada}.",
                ClaveListadoAbierto, borrada ? "si" : "no (ya expirada)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo invalidar la clave {Clave}.", ClaveListadoAbierto);
        }
    }
}
