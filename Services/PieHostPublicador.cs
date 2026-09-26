using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EXAMENPARCIAL.Services;

public class PieHostOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ClusterId { get; set; } = "free.blr2";

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ChannelId);

    public string ConectarUrl()
    {
        var sesion = Guid.NewGuid().ToString("N");

        return string.Format(
            "wss://{0}.piesocket.com/v4/{1}?api_key={2}&notify_self=1&source=server&v=4.2.3&presence=1&uuid={3}",
            ClusterId,
            Uri.EscapeDataString(ChannelId),
            Uri.EscapeDataString(ApiKey),
            sesion);
    }
}

public sealed class PieHostPublicador : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly PieHostOptions _opciones;
    private readonly ILogger<PieHostPublicador> _logger;
    private readonly SemaphoreSlim _candado = new(1, 1);

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _corte;
    private Task? _receptor;

    public PieHostPublicador(PieHostOptions opciones, ILogger<PieHostPublicador> logger)
    {
        _opciones = opciones;
        _logger = logger;
    }

    public async Task<bool> PublicarAsync(string evento, object datos)
    {
        if (!_opciones.Configurado)
        {
            _logger.LogWarning(
                "PieHost no esta configurado. Evento {Evento} no publicado.", evento);
            return false;
        }

        var mensaje = JsonSerializer.Serialize(new { @event = evento, data = datos }, Json);

        await _candado.WaitAsync();

        try
        {
            if (!await AsegurarConexionAsync())
            {
                return false;
            }

            var bytes = Encoding.UTF8.GetBytes(mensaje);

            await _socket!.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            _logger.LogInformation(
                "PieHost: evento {Evento} publicado en el canal {Canal} -> {Payload}",
                evento, _opciones.ChannelId, mensaje);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PieHost: fallo al publicar {Evento}", evento);
            LiberarSocket();
            return false;
        }
        finally
        {
            _candado.Release();
        }
    }

    private async Task<bool> AsegurarConexionAsync()
    {
        if (_socket is { State: WebSocketState.Open })
        {
            return true;
        }

        LiberarSocket();

        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        using var tiempo = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        try
        {
            await socket.ConnectAsync(new Uri(_opciones.ConectarUrl()), tiempo.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PieHost: no se pudo abrir el canal {Canal}", _opciones.ChannelId);
            socket.Dispose();
            return false;
        }

        _socket = socket;
        _corte = new CancellationTokenSource();
        _receptor = RecibirAsync(socket, _corte.Token);

        _logger.LogInformation(
            "PieHost: canal {Canal} conectado desde el servidor.", _opciones.ChannelId);

        return true;
    }

    private async Task RecibirAsync(ClientWebSocket socket, CancellationToken corte)
    {
        var buffer = new byte[8192];

        try
        {
            while (socket.State == WebSocketState.Open && !corte.IsCancellationRequested)
            {
                var recibido = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), corte);

                if (recibido.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PieHost: receptor detenido");
        }
    }

    private void LiberarSocket()
    {
        try { _corte?.Cancel(); } catch { }
        try { _socket?.Dispose(); } catch { }

        _corte = null;
        _socket = null;
        _receptor = null;
    }

    public async ValueTask DisposeAsync()
    {
        LiberarSocket();
        _candado.Dispose();
        await Task.CompletedTask;
    }
}
