# MundialNube

API de torneos con .NET 10 y SQLite. La rama `cambios-2` incorpora equipos nuevos por torneo, campeones persistentes, reporte de los últimos 20 resultados y Grafana con Prometheus.

## Iniciar con Grafana

Requisitos: Docker con Compose. Para ejecutar sin contenedores: SDK .NET 10.

```sh
cp .env.example .env
# Editar .env y establecer GRAFANA_ADMIN_PASSWORD con una contraseña propia.
docker compose up --build -d
```

Servicios predeterminados (solo accesibles desde el equipo local):

- API: http://localhost:8080
- Grafana: http://localhost:3000 — usuario `admin` y la contraseña de `.env`.
- Prometheus: http://localhost:9090

Grafana carga automáticamente la fuente `Mundial Prometheus` y el dashboard **MundialNube · Torneos y campeones**. Incluye disponibilidad, solicitudes, errores, latencia, total de torneos, total de campeones, último ganador y tabla de los últimos 20 campeones. Prometheus consulta `/metrics` cada 15 segundos; el dashboard se actualiza automáticamente. Al principio, sin resultados registrados, la tabla estará vacía.

Los puertos se pueden cambiar con `API_PORT`, `GRAFANA_PORT` y `PROMETHEUS_PORT` en `.env`. No se incluyen credenciales en el repositorio. La contraseña de Grafana se aplica al crear su volumen por primera vez; cambiarla posteriormente requiere actualizarla también dentro de Grafana.

Los volúmenes conservan SQLite, Grafana y Prometheus al reiniciar o ejecutar `docker compose down`. **`docker compose down -v` elimina esos datos.** Para una base existente, respalda el archivo y cópialo al volumen de la API con los servicios detenidos. El arranque añade las tablas nuevas sin borrar los torneos anteriores.

### Conectar un Grafana existente

La configuración incluida crea una instancia nueva y local. Para una instancia que ya exista, añade una fuente de datos Prometheus cuya URL sea accesible **desde el servidor de Grafana** e importa `monitoring/grafana/dashboards/mundial-nube.json`. La fuente debe tener UID `mundial-prometheus` (el usado por el dashboard). Dentro de esta red Compose, Prometheus está en `http://prometheus:9090`. Una instancia externa necesita acceso de red a Prometheus; los puertos locales predeterminados no lo permiten. No se ha configurado ningún servidor externo sin su URL y acceso.

## Equipos y campeones

Crear un torneo mediante el endpoint existente crea, en la misma transacción, **32 equipos nuevos** (8 grupos × 4 participantes, según el formato actual). Cada equipo tiene ID y nombre propios; no se reutilizan los del torneo anterior. Los nombres iniciales `Equipo 01 - <id-del-torneo>` se pueden editar con `PUT /teams/{id}`. Los participantes también funcionan en las rutas existentes de equipos, grupos y partidos.

Los torneos anteriores a este cambio conservan sus datos; no se les inventan participantes ni campeones. La generación ocurre cada vez que se crea un torneo nuevo, no en un proceso que genere torneos indefinidamente.

| Método y ruta | Resultado |
| --- | --- |
| `POST /tournaments` | Crea torneo y sus 32 equipos. |
| `GET /tournaments/{id}/teams` | Lista los participantes persistentes de ese torneo. |
| `POST /tournaments/{id}/champion` | Registra su campeón con `{ "teamId": "..." }`. |
| `GET /reports/champions/latest` | Último campeón registrado; `404` si no hay ninguno. |
| `GET /reports/champions` | Hasta 20 campeones, del más reciente al más antiguo. |
| `GET /reports/champions.csv` | Descarga `ultimos-20-campeones.csv`. |
| `GET /metrics` | Métricas para Prometheus. |
| `GET /health` | Comprobación de respuesta de la API. |

El registro del campeón es **explícito**: no se selecciona aleatoriamente ni se deduce de un partido cualquiera. Debe pertenecer al torneo. Repetir el mismo resultado devuelve el registro original; intentar sustituirlo por otro campeón devuelve `409`. Un equipo ajeno al torneo devuelve `400`; un torneo inexistente, `404`.

El reporte consulta SQLite y devuelve ID/nombre del torneo, ID/nombre del equipo y fecha de finalización UTC. Ordena por fecha descendente y, en caso de empate, por ID del resultado descendente. Si solo existen 5 campeones, devuelve 5. Los nombres del resultado quedan guardados como evidencia histórica: renombrar o eliminar el torneo no cambia ni borra el reporte. Eliminar el torneo sí elimina sus participantes.

Los torneos, equipos generados y campeones persisten en SQLite. Los equipos manuales del catálogo general y los grupos/partidos conservan el almacenamiento en memoria que tenía el proyecto; esta entrega no cambia esos módulos a persistencia completa ni implementa una simulación automática de partidos.

## Ejemplo de uso

Crear un torneo:

```sh
curl -s http://localhost:8080/tournaments \
  -H 'Content-Type: application/json' \
  -d '{"name":"Torneo del lunes","format":{"type":"ROUND_ROBIN","maxGroups":8,"maxTeamsPerGroup":4}}'
```

Usar el `id` devuelto para consultar los equipos:

```sh
curl -s http://localhost:8080/tournaments/ID_TORNEO/teams
```

Cuando se conozca el ganador, registrar su ID y consultar el reporte:

```sh
curl -s http://localhost:8080/tournaments/ID_TORNEO/champion \
  -H 'Content-Type: application/json' -d '{"teamId":"ID_EQUIPO"}'
curl -s http://localhost:8080/reports/champions/latest
curl -f http://localhost:8080/reports/champions.csv -o ultimos-20-campeones.csv
```

## Desarrollo y verificación

```sh
dotnet restore TournamentServices.sln
dotnet test TournamentServices.sln
dotnet run --project src/TournamentServices.Api --urls http://localhost:8080
```

En Development está disponible `/swagger`. La cadena de conexión se configura con `ConnectionStrings__TournamentsConnection`; de forma predeterminada se usa `tournaments.db`. El inicializador admite tanto bases nuevas como el esquema SQLite anterior creado mediante `EnsureCreated`.

Las pruebas cubren generación independiente de equipos, pertenencia al torneo, validación del campeón, idempotencia, orden y límite del reporte, CSV, persistencia, actualización de bases anteriores e integración de métricas.

Para revisar la entrega del lunes 23 de noviembre de 2026: iniciar los servicios, crear dos torneos y comprobar equipos distintos, registrar los ganadores reales, descargar el reporte y comprobar que Grafana muestra el último ganador después del siguiente muestreo. No se incluyen campeones ficticios en la base de la aplicación.
