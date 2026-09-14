# Integrante 2: Torneos

Implementación en la rama `integrante-2` (Git no admite espacios en los nombres de las ramas).

## Ejecutar la presentación

Requisito: SDK de .NET 10.

```bash
dotnet run --project src/TournamentServices.Api --launch-profile http
```

Abre **http://localhost:8080/swagger**. También puedes usar las solicitudes de
`src/TournamentServices.Api/Tournaments.http`.

La API crea automáticamente las tablas y el archivo SQLite `tournaments.db` en su
directorio de trabajo. La conexión se configura en `appsettings.json`, con la clave
`ConnectionStrings:TournamentsConnection`. Los torneos se conservan al reiniciar.

## Cómo explicar el código

El recorrido de una petición es:

```text
TournamentRoutes → TournamentDelegate → ITournamentRepository → SQLite
```

- **Dominio:** `Tournament` tiene identificador, nombre y formato.
- **Value Object:** `TournamentFormat` es un `record` inmutable. Dos formatos con los
  mismos valores son iguales; para cambiarlo se construye otro formato válido.
- **Delegado:** coordina consultas, creación, actualización y eliminación mediante
  `ITournamentRepository`. No contiene consultas SQL.
- **Repositorio:** usa Entity Framework Core con SQLite para leer y guardar datos.
- **Minimal APIs:** reciben las peticiones y usan FluentValidation para validar los datos.

### Reglas de configuración

Como la consigna no especifica cifras, se adoptaron estos límites para la demostración:

| Campo | Regla |
|---|---|
| `name` | Obligatorio; no puede estar vacío ni contener solo espacios. |
| `format.type` | Solo `ROUND_ROBIN` (todos contra todos dentro del grupo). |
| `format.maxGroups` | Entre 1 y 16 grupos. |
| `format.maxTeamsPerGroup` | Entre 2 y 8 equipos por grupo. |

Los límites están definidos como constantes en `TournamentFormat.cs` y se reutilizan
en los validadores. Son la configuración del torneo; crear un torneo no genera
automáticamente grupos ni un calendario de partidos.

## Rutas

| Método | Ruta | Resultado correcto |
|---|---|---|
| GET | `/tournaments` | `200`, lista de torneos. |
| GET | `/tournaments/{id}` | `200`, un torneo. |
| POST | `/tournaments` | `201`, torneo creado y encabezado `Location`. |
| PUT | `/tournaments/{id}` | `200`, reemplazo de nombre y formato completo. |
| PATCH | `/tournaments/{id}` | `200`, actualización de los campos enviados. |
| DELETE | `/tournaments/{id}` | `204`, torneo eliminado junto con sus grupos y partidos. |

Los datos inválidos producen `400` con errores de validación; los identificadores
bien formados que no existen producen `404`.

### Crear un torneo

Envía este JSON con `POST /tournaments`:

```json
{
  "name": "Mundial demo",
  "format": {
    "type": "ROUND_ROBIN",
    "maxGroups": 12,
    "maxTeamsPerGroup": 4
  }
}
```

Copia el `id` de la respuesta para las siguientes operaciones.

### Actualizar parcialmente

Envía este JSON con `PATCH /tournaments/{id}`:

```json
{
  "format": {
    "maxGroups": 8
  }
}
```

Solo cambia `maxGroups`; se conservan el nombre, `type` y `maxTeamsPerGroup`.
También puedes enviar únicamente `{"name":"Copa demo"}`.

PATCH acepta un objeto JSON parcial (`application/json`). Los campos omitidos o
con `null` se conservan. Se rechazan los cuerpos sin ningún valor para actualizar,
como `{}`, `{"name":null}` o `{"format":{}}`, y los campos desconocidos.
PUT requiere enviar de nuevo el nombre y los tres campos del formato.

### Eliminación en cascada

La base de datos tiene estas relaciones con `ON DELETE CASCADE`:

```text
Tournament → TournamentGroup → TournamentMatch
```

Al borrar un torneo, SQLite elimina sus grupos y los partidos de esos grupos en la
misma operación. Los otros torneos se conservan. Para `ROUND_ROBIN`, cada partido
pertenece a un grupo. Las entidades de grupo y partido son mínimas para establecer
esta relación; sus funcionalidades completas corresponden a los siguientes integrantes.

## Pruebas y cobertura

Verificación de esta entrega: **178 pruebas aprobadas, 0 fallidas**.

| Pruebas nuevas de torneos | Casos aprobados |
|---|---:|
| Endpoints HTTP | 88 |
| Delegado | 33 |
| Dominio | 13 |
| Repositorio SQLite | 10 |

Se conservan las 34 pruebas previas. `TournamentRoutes.cs` obtuvo **100% de cobertura
de líneas (75/75) y ramas (22/22)** con Coverlet. Esta cifra corresponde a las rutas
de torneos, incluyendo los métodos que el compilador genera para sus operaciones
asíncronas.

```bash
dotnet test TournamentServices.sln --nologo
dotnet test tests/TournamentServices.Api.Tests --collect "XPlat Code Coverage" --results-directory TestResults/tournaments
```

El segundo comando genera un archivo `coverage.cobertura.xml` dentro de una carpeta
de resultados. La cobertura exigida se revisa en `TournamentRoutes.cs`: líneas y
ramas de todos los endpoints, incluyendo éxitos, validaciones e inexistentes.

Las pruebas de integración usan SQLite en memoria aislado por prueba. Comprueban
la cascada contra la base de datos real y que otro torneo, sus grupos y sus partidos
sigan existiendo. Las unitarias del delegado usan Moq para comprobar sus decisiones
y llamadas al repositorio. También hay pruebas del Value Object y del repositorio.

Para esta práctica se usa `EnsureCreated` al iniciar. Si posteriormente se necesita
evolucionar una base de datos con datos existentes, se deberán añadir migraciones.

Referencias técnicas: [proveedor SQLite de EF Core](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/),
[eliminación en cascada](https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete) y
[creación inicial de tablas](https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created).
