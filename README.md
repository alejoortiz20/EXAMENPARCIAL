# Examen Parcial — Plataforma de Incidencias (OperBici)

Aplicación ASP.NET Core MVC con Identity, EF Core y SQLite que registra averías en
estaciones de bicicletas compartidas. Resuelve las tres preguntas del examen sobre
**búsqueda con Algolia**, **caché con Redis** y **actualización en tiempo real con
PieHost (PieSocket)**, y se despliega en **Render.com**.

| | |
|---|---|
| **Repositorio** | https://github.com/alejoortiz20/EXAMENPARCIAL |
| **Producción (Render)** | https://examen-parcial-gbhc.onrender.com |
| **Rama principal** | `main` |
| **Usuario de prueba** | `supervisor` / `Supervisor2026` |
| **Pantalla principal** | `/Operaciones/Incidencias` |

---

## 1. Ramas y pull requests

Las tres ramas nacen del **mismo commit inicial de `main` (`63ffe80`)**, antes de
integrar cualquiera de ellas, y cada una se entrega mediante un pull request:

| Pregunta | Rama | PR | Commit de la rama |
|---|---|---|---|
| 1 — Búsqueda con Algolia | `feature/busqueda-algolia` | [#1](https://github.com/alejoortiz20/EXAMENPARCIAL/pull/1) | `0d93986` |
| 2 — Caché con Redis | `feature/cache-redis` | [#2](https://github.com/alejoortiz20/EXAMENPARCIAL/pull/2) | `02e447a` |
| 3 — Actualización con PieHost | `feature/websocket-piehost` | [#3](https://github.com/alejoortiz20/EXAMENPARCIAL/pull/3) | `b534200` |
| Despliegue en Render | `feature/render-deploy` | [#4](https://github.com/alejoortiz20/EXAMENPARCIAL/pull/4) | `65f7995` |
| Dockerfile para Render | `feature/render-docker` | [#5](https://github.com/alejoortiz20/EXAMENPARCIAL/pull/5) | — |

`main` nunca recibió trabajo directo: solo merges de pull requests.

### Fusiones y resolución de conflictos

Se fusionó **A, luego B, finalmente C**. Antes de integrar cada una se incorporó
`main` a la rama con `git merge`, generando el conflicto en la línea del título
compartido `<h1>Incidencias abiertas</h1>`, que cada rama cambian con un texto
distinto. Los **commits de resolución se conservaron** (sin *force push* ni *squash*):

- `5cb73fc` — primer conflicto: conserva **búsqueda (Algolia) y caché (Redis)**.
- `d249d63` — segundo conflicto: conserva **búsqueda, caché y WebSocket**.

```
$ git log --graph --oneline --all
*   8f545aa Merge pull request #3 from alejoortiz20/feature/websocket-piehost
|\
| *   d249d63 Resuelve el conflicto con main conservando busqueda (Algolia), cache (Redis) y tiempo real (PieHost)
| |\
| |/
|/|
* |   34637fa Merge pull request #2 from alejoortiz20/feature/cache-redis
|\
| * \   5cb73fc Resuelve el conflicto con main conservando busqueda (Algolia) y cache (Redis)
| |\
| |/ /
|/ | /
* | |   d285584 Merge pull request #1 from alejoortiz20/feature/busqueda-algolia
|\ \ \
| * | |   0d93986 Busqueda con Algolia: consulta desde servidor, filtra abiertas existentes y cambia el titulo
|/ / /
| * / 02e447a Cache Redis de 60s para el listado general, invalidacion al cerrar y log de origen
|/ /
| * b534200 Tiempo real con PieHost: publica IncidenciaActualizada desde el servidor y actualiza la lista sin recargar
|/
* 63ffe80 Base: proyecto MVC con Identity, EF Core/SQLite y /Operaciones/Incidencias   <-- ancestro comun
* 2f01bfb Scaffold: project structure, env template and integration notes
```

Títulos por rama (la línea compartida que genera los conflictos):

| Rama | `<h1>` |
|---|---|
| `main` (base) | `Incidencias abiertas` |
| A — Algolia | `Incidencias abiertas encontradas` |
| B — Redis | `Incidencias abiertas con consulta rápida` |
| C — PieHost | `Incidencias abiertas en tiempo real` |
| `main` final | `Incidencias abiertas encontradas con consulta rápida en tiempo real` |

---

## 2. Qué hace cada parte

### Pregunta 1 — Búsqueda con Algolia (`Services/AlgoliaService.cs`)

El **servidor** consulta Algolia y devuelve solo las incidencias **abiertas que existen
en la base de datos**. La búsqueda vacía devuelve el listado habitual.

- Nunca se expone la clave de administración de Algolia en el navegador: la página
  solo renderiza resultados, las consultas salen del servidor.
- El índice `incidencias` contiene 6 objetos con `objectID` 1..6, y la búsqueda filtra
  por estación o descripción.

> **Trampa documentada:** el host de Algolia debe llevar el sufijo `-dsn`
> (`gjd7rwze72-dsn.algolia.net`). La forma sin sufijo que aparece en la documentación
> no resuelve por DNS y el error parece una clave inválida.

### Pregunta 2 — Caché con Redis (`Services/CacheRedisService.cs`)

- El **listado general** de incidencias abiertas se cachea en Redis con **60 segundos**
  de vigencia (clave `incidencias:abiertas:listado`).
- **La búsqueda con texto de Algolia se consulta directamente, sin usar esta caché.**
- Al cerrar una incidencia, la clave del listado se **invalida antes de volver a
  consultarlo**.
- Los logs indican si la lectura fue de Redis o de la base:
  `Lectura desde REDIS: N incidencias` / `Lectura desde la BASE: N incidencias`.
- La pantalla muestra el origen con una pastilla: *Listado servido desde Redis* o
  *Listado servido desde Base de datos*.

### Pregunta 3 — Actualización con PieHost (`Services/PieHostPublicador.cs`)

Cuando el supervisor cierra una incidencia, el servidor **guarda primero el estado** y
**después publica** el evento en el canal WebSocket:

```json
{ "event": "IncidenciaActualizada", "data": { "Id": 1, "Estado": "Cerrada" } }
```

La pantalla (`wwwroot/js/tiemporeal.js`) está conectada al canal y **actualiza la lista
sin recargar la página**: retira la tarjeta afectada, recalcula los contadores y muestra
un aviso. Al reconectar (y en la primera conexión) consulta el estado vigente en
`/Operaciones/Panel`. El chip superior indica *conectado / conectando / desconectado*.

> **Nota técnica importante:** el endpoint REST `POST https://{cluster}.piesocket.com/api/publish`
> responde `{"success":true}` y valida las credenciales (403 si son incorrectas), pero
> **no entrega el mensaje a los suscriptores**. Se comprobó con dos clientes WebSocket
> conectados al mismo canal: al publicar por REST ninguno recibía nada, mientras que un
> evento enviado desde un cliente WebSocket sí llegaba al resto. Por eso el servidor
> publica **por conexión WebSocket**.
>
> La URL de conexión requiere `presence=1` y un `uuid`: sin `uuid` el handshake se
> acepta pero el servidor queda mudo, sin error ni timeout.

---

## 3. Pruebas realizadas

Todas las pruebas son de solo lectura sobre los servicios reales.

**Login y listado**
```
POST /Account/Login  ->  302 -> /Operaciones/Incidencias   (supervisor / Supervisor2026)
<h1>Incidencias abiertas encontradas con consulta rapida en tiempo real</h1>
```

**Búsqueda con Algolia (pregunta 1)**
```
?q=freno   -> 1 resultado  (Estacion Centro)
?q=llanta  -> 1 resultado  (Estacion Plaza Mayor)
clave de administracion de Algolia en el HTML: no aparece
```

**Caché con Redis (pregunta 2)** — tras borrar la clave y pedir el listado:
```
1.er GET -> Base de datos   (miss, guarda 60 s)
2.do GET -> Redis            (hit)
Cerrar    -> "Clave incidencias:abiertas:listado invalidada: si"
            -> Base de datos, 5 tarjetas, ttl 60
siguiente GET -> Redis
?q=llanta  -> origen Algolia (la busqueda no pasa por la cache)
```

**Tiempo real con PieHost (pregunta 3)** — navegador real, dos sesiones:
```
sesion 1 (pantalla abierta): estado=conectado, 6 tarjetas
sesion 2 cierra la #001 por HTTP
sesion 1 sin recargar:  5 tarjetas, ids [5,3,6,2,4]
                        stats 5 -> 4 abiertas, 1 critica, 2 altas
                        aviso "Incidencia #001 cerrada en otra sesion"
```

**Secuencia final (pregunta 4)**
```
1. Cierre en la base de datos            SaveChangesAsync
2. Invalidacion de Redis                 la clave del listado se borra
3. Publicacion en PieHost                evento IncidenciaActualizada
4. La busqueda en Algolia ya no muestra la incidencia cerrada
   (el filtro Estado == "Abierta" se aplica sobre los ids que devuelve Algolia)
```

---

## 4. Configuración por variables de entorno

**No hay ninguna clave en el repositorio.** `.env` está en `.gitignore` y en
`.dockerignore`; el repositorio solo publica `.env.example` con los nombres.

En Render se configuran como *Environment Variables* del servicio:

| Variable | Para qué sirve |
|---|---|
| `ALGOLIA_APP_ID` | Application ID de Algolia |
| `ALGOLIA_SEARCH_API_KEY` | Search API key (solo lectura, es la que usa la app) |
| `ALGOLIA_INDEX` | Nombre del índice (`incidencias`) |
| `REDIS_URL` | Cadena de conexión a Redis |
| `PIESOCKET_API_KEY` | API key del canal (viaja al navegador para conectar el WebSocket) |
| `PIESOCKET_API_SECRET` | API secret: **solo en el servidor**, nunca en el HTML |
| `PIESOCKET_CHANNEL` | Nombre del canal |
| `PIESOCKET_CLUSTER_ID` | Cluster de PieSocket |

`Program.cs` lee estas variables de dos fuentes, en este orden: las variables de entorno
del proceso y, si no existen, el archivo `.env` del directorio de ejecución.

### Desarrollo local

```bash
dotnet run
# http://localhost:5000
```

La base SQLite se crea sola (`EnsureCreated`) con 6 incidencias de prueba y el usuario
`supervisor` / `Supervisor2026`.

---

## 5. Despliegue en Render

- **Servicio:** Web Service conectado a `main` de este repositorio, plan gratuito.
- **URL:** https://examen-parcial-gbhc.onrender.com
- **Runtime:** Docker. La cuenta de Render no tiene el runtime nativo `dotnet`, así que
  el servicio usa el `Dockerfile` multietapa de la raíz (SDK 10 para compilar, imagen
  `aspnet:10` para ejecutar). PR #5.
- **Puerto:** Render inyecta `PORT`; `Program.cs` hace que Kestrel escuche en él. PR #4.
- **Commit desplegado:** el `main` final (`2c383da` con el fix de puerto y `ca9e802` con
  el `Dockerfile`), que es el mismo código de `main`.

> El plan gratuito de Render apaga el servicio tras unos minutos de inactividad, así que
> la primera carga puede tardar. La base SQLite vive en el sistema de archivos efímero del
> contenedor: en cada despliegue se vuelve a crear con la semilla de 6 incidencias.

---

## 6. Estructura

```
Controllers/OperacionesController.cs   Incidencias (listado + busqueda), Panel, Cerrar
Controllers/AccountController.cs       Login / Logout
Services/AlgoliaService.cs             Consulta al indice de Algolia
Services/CacheRedisService.cs          Cache de 60 s e invalidacion
Services/PieHostPublicador.cs          Publica el evento en el canal WebSocket
Models/Incidencia.cs                   Estacion, Descripcion, Prioridad, Estado
Views/Operaciones/Incidencias.cshtml   Pantalla: buscador, origen, estado, panel
Views/Operaciones/_Panel.cshtml        Listado reutilizable para la resincronizacion
wwwroot/js/tiemporeal.js               Conexion WebSocket y actualizacion sin recargar
wwwroot/css/site.css                   Diseno, estados y responsive
```
