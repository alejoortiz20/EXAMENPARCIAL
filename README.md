# EXAMENPARCIAL

Examen parcial — USMP, Programación I.

## Servicios que usa este proyecto

| Servicio | Rol tipico | Libreria (Python) | Libreria (Node) |
|---|---|---|---|
| Redis Cloud | Cache / pub-sub | `redis` | `ioredis` |
| CloudAMQP | Cola de mensajes | `pika` | `amqplib` |
| PieSocket | WebSocket tiempo real | `websockets` | `piesocket-js` |
| Algolia | Busqueda | `algoliasearch` | `algoliasearch` |
| Render | Deploy | — | — |
| GitHub | Repositorio / CI | — | — |

## Configuracion

```bash
copy .env.example .env
```

Luego completa `.env` con los valores reales. **`.env` esta en `.gitignore` y nunca se commitea**, porque este repositorio es publico.

El token de GitHub ya existe como variable de entorno del usuario de Windows (`GITHUB_PERSONAL_ACCESS_TOKEN`), por eso no se duplica en `.env`.

## Trampas verificadas

Estas tres se debugearon a mano y cuestan tiempo si se redescubren:

**1. El host de Algolia lleva el sufijo `-dsn`.**

```
GJD7RWZE72-dsn.algolia.net   -> resuelve
GJD7RWZE72.algolia.net        -> NXDOMAIN
```

La documentacion de Algolia usa la forma sin `-dsn`, que aqui no existe. Un error de DNS en esa forma parece una API key invalida, pero la key esta perfecta.

**2. El WebSocket de PieSocket necesita `uuid` y `presence=1`.**

```
wss://free.blr2.piesocket.com/v4/CHANNEL_ID?api_key=KEY&presence=1&uuid=UUID
```

Sin `uuid`, el servidor acepta el handshake (`onopen` se dispara) y despues queda mudo: sin error, sin `system::connected`, sin timeout claro. El SDK `piesocket-js` agrega `uuid` por su cuenta, asi que usando el SDK no aparece el problema; armando la URL a mano, si.

Para distinguir una key invalida de un canal mal configurado: una key desconocida responde de inmediato `{"event":"system::error","data":"Unkown API Key"}`. El silencio significa que la key es valida.

**3. La app de Algolia no tiene indices.**

`GET /1/indexes` devuelve 0 elementos. Si el proyecto necesita buscar datos reales, hay que crear el indice y poblarlo; no es solo configuracion.

## Estructura de ramas

Una rama por pregunta del examen, saliendo de `main`:

```
main
 q01-<slug>
 q02-<slug>
 q03-<slug>
```

Al terminar cada pregunta se revisa y se fusiona a `main`.

## Servicios y limites

Redis Cloud y CloudAMQP estan en plan gratuito. Si una prueba falla de forma intermitente, sospecha de limites de conexiones o brokers que se cierran por inactividad antes que de la credencial.
