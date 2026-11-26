# MundialNube

API y simulador de Mundial con .NET 10, PostgreSQL 16 o SQLite. La rama `max-v3`, basada en `maxv2`, añade persistencia completa, simulación de grupos y eliminatorias, mensajería con ActiveMQ Artemis, trazas con Tempo y despliegue Kubernetes.

## Novedades de max-v3

- Página de simulación en `/`: crea el torneo, genera y llena grupos, crea y simula partidos, consulta la tabla y juega la eliminatoria.
- Ocho grupos de cuatro: 48 partidos de grupos y 15 de eliminatoria. Los empates se conservan en grupos; en eliminatoria siempre hay ganador. La tabla ordena por victorias, goles a favor, goles en contra y nombre.
- Equipos propios y nuevos por torneo. Se mantiene el contrato `ROUND_ROBIN` de los clientes existentes; esta rama no cambia sus IDs ni elimina los campos antiguos.
- Equipos manuales, grupos, participantes y partidos ahora se guardan en la misma base que los torneos y campeones. PostgreSQL permite compartirlos entre réplicas.
- `/knockout` registra el campeón automáticamente y conserva la fecha UTC y los nombres históricos. Repetir la petición devuelve el mismo campeón.
- Cada resultado crea un evento durable `champion.completed` en la misma transacción. El publicador reintenta hasta recibir confirmación de Artemis y el consumidor registra cada evento una sola vez antes de reconocerlo.
- Métricas de los comandos EF realmente ejecutados y de las solicitudes HTTP; dashboard comparativo y trazas OTLP a Tempo. La duración SQL incluye el tiempo observado desde la API, incluido el viaje de red; no pretende medir exclusivamente CPU del servidor PostgreSQL.
- Migración independiente con `--migrate`, worker con `--consume`, readiness `/ready`, identificación de réplica `X-Pod-Name`, Ingress, HPA y escenarios Locust reproducibles.

## Iniciar v3 con PostgreSQL, mensajería y trazas

```sh
cp .env.v3.example .env.v3
# Establecer POSTGRES_PASSWORD, BROKER_PASSWORD y GRAFANA_ADMIN_PASSWORD.
docker compose --env-file .env.v3 -f compose.v3.yaml up --build -d
```

La migración debe finalizar correctamente antes de iniciar la API y el consumidor. Este entorno usa volúmenes y puertos separados del demo anterior:

- Simulador y Swagger: http://localhost:8183 y http://localhost:8183/swagger
- Grafana: http://localhost:3103
- Prometheus: http://localhost:9193

El dashboard **MundialNube v3 - API, PostgreSQL y replicas** compara latencias en la misma ventana. Tempo está provisionado como otra fuente de Grafana. Las credenciales de `.env.v3` no se versionan.

El despliegue en kind, los Secrets, el Job de migración, los recursos y los ensayos con/sin Ingress se detallan en [Infraestructura v3](docs/INFRAESTRUCTURA_V3.md). Los resultados realmente ejecutados se distinguen de los procedimientos pendientes en [Verificación de la entrega](evidencias/v3/README.md).

El valor solicitado de CPU es **100m = 0,1 núcleo**. Un cuarto de núcleo sería 250m. La configuración base fija 56Mi de memoria; su viabilidad depende de las pruebas de arranque y carga, no solo del valor del manifiesto. La documentación de evidencias indica el resultado obtenido en este equipo.

## Cargar torneos de demostración

Con la API v3 encendida, Python 3 puede crear 20 torneos de prueba completos:

```sh
python3 scripts/seed-demo.py --base-url http://localhost:8183 --count 20
```

Cada ejecución **añade** 20 torneos nuevos, 640 equipos propios y 1.260 partidos simulados; registra los campeones de sus finales. No borra ni reinicia datos. Los nombres llevan `DEMO V3` y un identificador único de ejecución. `--count` cambia la cantidad y `--base-url` el destino. Si falla una petición, el programa se detiene, informa el torneo creado y conserva los datos para revisarlos. Grafana los mostrará después del siguiente muestreo de Prometheus.

## API de simulación

Tras `POST /tournaments`, ejecutar en orden:

1. `POST /tournaments/{id}/groups/generate`
2. `POST /tournaments/{id}/groups/fill`
3. `POST /tournaments/{id}/matches/generate`
4. `POST /tournaments/{id}/matches/simulate`
5. `GET /tournaments/{id}/standings`
6. `POST /tournaments/{id}/knockout`

Las escrituras de cada fase son transaccionales; PostgreSQL bloquea el torneo mientras una fase cambia sus datos. Si falla, se revierten todos sus cambios. Las pruebas verifican también que las llamadas repetidas no dupliquen partidos ni campeones.

## Compatibilidad local con SQLite y Grafana

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

El endpoint manual de registro del campeón sigue disponible; la simulación lo registra automáticamente al terminar la eliminatoria completa. Debe pertenecer al torneo. Repetir el mismo resultado devuelve el registro original; intentar sustituirlo por otro campeón devuelve `409`. Un equipo ajeno al torneo devuelve `400`; un torneo inexistente, `404`.

El reporte consulta la base configurada (PostgreSQL o SQLite) y devuelve ID/nombre del torneo, ID/nombre del equipo y fecha de finalización UTC. Ordena por fecha descendente y, en caso de empate, por ID del resultado descendente. Si solo existen 5 campeones, devuelve 5. Los nombres del resultado quedan guardados como evidencia histórica: renombrar o eliminar el torneo no cambia ni borra el reporte. Eliminar el torneo sí elimina sus participantes.

Los torneos, equipos, grupos, participantes, partidos, campeones y eventos persisten en la base configurada. Las bases SQLite del demo anterior se actualizan sin borrar sus datos. Esa actualización no recupera grupos o partidos que las versiones anteriores solo guardaban en memoria. Los equipos manuales siguen disponibles como catálogo general; la simulación automática usa los participantes propios de cada torneo.

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

Las pruebas cubren generación independiente de equipos, simulación y reversión transaccional, pertenencia al torneo, validación del campeón, idempotencia, orden y límite del reporte, CSV, persistencia, actualización de bases anteriores, transporte STOMP e integración de métricas. La integración PostgreSQL se activa con `MUNDIAL_TEST_POSTGRES`; usa un esquema aislado y lo elimina al terminar.

Para revisar la entrega del lunes 23 de noviembre de 2026: iniciar los servicios, crear dos torneos y comprobar equipos distintos, registrar los ganadores reales, descargar el reporte y comprobar que Grafana muestra el último ganador después del siguiente muestreo. No se incluyen campeones ficticios en la base de la aplicación.

## Reporte PDF de demostración

[Descargar el reporte de los últimos 20 torneos](reports/reporte_20_torneos_mundialnube.pdf).

Documento de cuatro páginas con los 20 campeones, las estadísticas de 640 equipos y la distribución por nombre de selección. Corresponde a los datos de prueba locales con corte al 20 de noviembre de 2026. Es un reporte estático: no incluye marcadores ni estadísticas de partidos, porque no había partidos registrados al generarlo.
