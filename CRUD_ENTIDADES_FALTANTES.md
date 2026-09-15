# Guia de implementacion: CRUD de Group y Match

## 1. Objetivo

Completar la API de Tournament Services con altas, consultas, cambios y bajas para las entidades que faltan:

- `Group`.
- `Match`.

`Team` ya tiene CRUD completo y debe reutilizarse tal como esta. `Tournament` funciona como entidad padre y debe considerarse una dependencia ya disponible para validar relaciones.

La implementacion debe conservar la arquitectura existente:

```text
API -> Delegate -> Repository -> Domain
```

No se deben colocar reglas de negocio en las rutas ni llamadas directas de las rutas a los repositorios.

## 2. Estado actual que se debe conservar

El proyecto ya cuenta con:

- `TournamentServices.Api` con Minimal APIs.
- `TournamentServices.Delegates`.
- `TournamentServices.Repositories`.
- `TournamentServices.Domain`.
- CRUD de `Team` en `TeamRoutes`, `TeamDelegate` y `TeamRepository`.
- FluentValidation.
- Serializacion camelCase y enums como texto.
- Pruebas xUnit con `WebApplicationFactory`.
- Repositorios en memoria con `ConcurrentDictionary`.

Usar como referencia estos archivos existentes:

```text
src/TournamentServices.Domain/Team.cs
src/TournamentServices.Repositories/ITeamRepository.cs
src/TournamentServices.Repositories/TeamRepository.cs
src/TournamentServices.Delegates/ITeamDelegate.cs
src/TournamentServices.Delegates/TeamDelegate.cs
src/TournamentServices.Api/Dtos/TeamDtos.cs
src/TournamentServices.Api/Routes/TeamRoutes.cs
tests/TournamentServices.Api.Tests/Routes/TeamRoutesTests.cs
```

## 3. Alcance de la entrega

| Entidad | Alta | Consulta | Cambio | Baja |
|---|---|---|---|---|
| `Group` | `POST` | `GET` lista y por ID | `PUT` y asignacion de equipos | `DELETE` |
| `Match` | `POST` | `GET` lista y por ID | `PATCH` del marcador | `DELETE` |

El `Tournament` no se implementa en esta guia. Solo se consulta para confirmar que existe y obtener reglas como `maxTeamsPerGroup`.

## 4. Contrato HTTP

### 4.1 Grupos

```text
GET    /tournaments/{tournamentId}/groups
GET    /tournaments/{tournamentId}/groups/{groupId}
POST   /tournaments/{tournamentId}/groups
PUT    /tournaments/{tournamentId}/groups/{groupId}
DELETE /tournaments/{tournamentId}/groups/{groupId}
PATCH  /tournaments/{tournamentId}/groups/{groupId}/teams
```

Respuesta `GroupDto`:

```json
{
  "id": "group-a",
  "name": "Grupo A",
  "tournamentId": "tournament-1",
  "teams": []
}
```

Alta y cambio:

```json
{
  "name": "Grupo A"
}
```

Asignacion de equipos:

```json
{
  "teamIds": ["team-1", "team-2"]
}
```

### 4.2 Partidos

```text
GET    /tournaments/{tournamentId}/matches
GET    /tournaments/{tournamentId}/matches/{matchId}
POST   /tournaments/{tournamentId}/matches
PATCH  /tournaments/{tournamentId}/matches/{matchId}/score
DELETE /tournaments/{tournamentId}/matches/{matchId}
```

Respuesta `MatchDto`:

```json
{
  "id": "match-1",
  "tournamentId": "tournament-1",
  "groupId": "group-a",
  "homeTeamId": "team-1",
  "visitorTeamId": "team-2",
  "homeTeam": null,
  "visitorTeam": null,
  "score": {
    "homeTeamScore": 0,
    "visitorTeamScore": 0
  },
  "winner": null,
  "isCompleted": false
}
```

Alta de partido:

```json
{
  "groupId": "group-a",
  "homeTeamId": "team-1",
  "visitorTeamId": "team-2"
}
```

Actualizacion del marcador:

```json
{
  "homeTeamScore": 2,
  "visitorTeamScore": 1
}
```

## 5. Paso a paso de implementacion

### Paso 1: Crear el dominio

Crear en `src/TournamentServices.Domain`:

```text
Group.cs
Match.cs
Score.cs
Enums/Winner.cs
Exceptions/GroupNotFoundException.cs
Exceptions/MatchNotFoundException.cs
Exceptions/DuplicateGroupNameException.cs
```

Relaciones minimas:

- `Group` tiene `Id`, `Name`, `TournamentId` y una coleccion de `TeamIds`.
- `Match` tiene `Id`, `TournamentId`, `GroupId` opcional, `HomeTeamId`, `VisitorTeamId` y `Score`.
- `Score` tiene `HomeTeamScore` y `VisitorTeamScore`.
- `Winner` puede ser `HOME`, `VISITOR` o `null`.

Reglas de dominio:

- Todos los IDs usan el patron de `Team.IdPattern`.
- Un grupo siempre pertenece a un torneo existente.
- No puede haber grupos con el mismo nombre dentro del mismo torneo.
- Un equipo solo puede asignarse si existe y pertenece al torneo.
- No se permiten equipos repetidos en la asignacion.
- La cantidad de equipos no puede superar `maxTeamsPerGroup`.
- Un partido no puede enfrentar un equipo contra si mismo.
- Los equipos de un partido deben existir y pertenecer al torneo.
- Si se proporciona `groupId`, el grupo debe pertenecer al torneo.
- Los marcadores no pueden ser negativos.
- El ganador se calcula al actualizar el marcador; en empate queda `null`.
- `IsCompleted` debe reflejar que el partido ya tiene marcador registrado.

Las entidades deben exponer operaciones controladas para actualizar nombre, equipos y marcador. Evitar setters publicos cuando permitan saltarse invariantes.

### Paso 2: Crear DTOs

Crear:

```text
src/TournamentServices.Api/Dtos/GroupDtos.cs
src/TournamentServices.Api/Dtos/MatchDtos.cs
```

Contratos sugeridos:

```csharp
public record GroupDto(
    string Id,
    string Name,
    string TournamentId,
    IReadOnlyList<TeamDto> Teams);

public record CreateGroupDto(string Name);
public record UpdateGroupDto(string Name);
public record AssignTeamsDto(IReadOnlyList<string> TeamIds);

public record ScoreDto(int HomeTeamScore, int VisitorTeamScore);

public record MatchDto(
    string Id,
    string TournamentId,
    string? GroupId,
    string HomeTeamId,
    string VisitorTeamId,
    TeamDto? HomeTeam,
    TeamDto? VisitorTeam,
    ScoreDto Score,
    Winner? Winner,
    bool IsCompleted);

public record CreateMatchDto(
    string? GroupId,
    string HomeTeamId,
    string VisitorTeamId);

public record UpdateScoreDto(int HomeTeamScore, int VisitorTeamScore);
```

### Paso 3: Crear validadores

Crear en `src/TournamentServices.Api/Validators`:

```text
CreateGroupDtoValidator.cs
UpdateGroupDtoValidator.cs
AssignTeamsDtoValidator.cs
CreateMatchDtoValidator.cs
UpdateScoreDtoValidator.cs
```

Validar en esta capa:

- Nombres obligatorios y con longitud valida.
- IDs no vacios.
- Lista de equipos no vacia cuando aplique.
- Marcadores mayores o iguales a cero.
- Campos obligatorios del partido.

Validar en los delegates, porque requieren consultar datos:

- Torneo, grupo o equipo inexistente.
- Nombre duplicado.
- Equipo fuera del torneo.
- Grupo fuera del torneo.
- Exceso o duplicidad de equipos.
- Equipos iguales en un partido.

### Paso 4: Crear repositorios

Crear en `src/TournamentServices.Repositories`:

```text
IGroupRepository.cs
GroupRepository.cs
IMatchRepository.cs
MatchRepository.cs
```

Los contratos deben seguir el estilo de `ITeamRepository`:

```csharp
Task<IReadOnlyList<Group>> GetByTournamentIdAsync(
    string tournamentId,
    CancellationToken ct = default);

Task<Group?> GetByIdAsync(
    string tournamentId,
    string groupId,
    CancellationToken ct = default);

Task<Group?> GetByNameAsync(
    string tournamentId,
    string name,
    CancellationToken ct = default);

Task AddAsync(Group group, CancellationToken ct = default);
Task UpdateAsync(Group group, CancellationToken ct = default);
Task DeleteAsync(string groupId, CancellationToken ct = default);
```

El repositorio de partidos debe ofrecer las mismas operaciones filtradas por torneo:

```csharp
Task<IReadOnlyList<Match>> GetByTournamentIdAsync(
    string tournamentId,
    CancellationToken ct = default);

Task<Match?> GetByIdAsync(
    string tournamentId,
    string matchId,
    CancellationToken ct = default);

Task<IReadOnlyList<Match>> GetByGroupIdAsync(
    string groupId,
    CancellationToken ct = default);

Task AddAsync(Match match, CancellationToken ct = default);
Task UpdateAsync(Match match, CancellationToken ct = default);
Task DeleteAsync(string matchId, CancellationToken ct = default);
```

Como en `TeamRepository`, devolver copias de las entidades para impedir modificaciones directas del estado almacenado.

### Paso 5: Implementar delegates

Crear:

```text
src/TournamentServices.Delegates/IGroupDelegate.cs
src/TournamentServices.Delegates/GroupDelegate.cs
src/TournamentServices.Delegates/IMatchDelegate.cs
src/TournamentServices.Delegates/MatchDelegate.cs
```

#### `GroupDelegate`

Debe:

1. Confirmar que el torneo existe mediante `ITournamentRepository`.
2. Crear el grupo con un ID nuevo.
3. Rechazar nombres duplicados dentro del torneo.
4. Consultar grupos por torneo.
5. Actualizar el nombre.
6. Validar y asignar equipos mediante `ITeamRepository`.
7. Comprobar `maxTeamsPerGroup` usando el torneo padre.
8. Eliminar el grupo y limpiar los partidos relacionados si esa es la regla acordada.

#### `MatchDelegate`

Debe:

1. Confirmar que el torneo existe.
2. Confirmar que los equipos existen y pertenecen al torneo.
3. Confirmar que el grupo opcional pertenece al torneo.
4. Rechazar el mismo equipo como local y visitante.
5. Crear el partido con marcador inicial.
6. Consultar partidos por torneo y por ID.
7. Actualizar el marcador y calcular `Winner` e `IsCompleted`.
8. Eliminar el partido y devolver `MatchNotFoundException` si no existe.

Las rutas deben depender de `IGroupDelegate` e `IMatchDelegate`, nunca de los repositorios.

### Paso 6: Crear las rutas

Crear:

```text
src/TournamentServices.Api/Routes/GroupRoutes.cs
src/TournamentServices.Api/Routes/MatchRoutes.cs
```

Seguir el patron de `TeamRoutes`:

- Validar todos los IDs antes de llamar al delegate.
- Usar `TypedResults`.
- Mapear dominio a DTOs.
- Traducir excepciones conocidas.
- Responder `201 Created` con `Location` al crear.
- Responder `204 NoContent` al eliminar.

Respuestas esperadas:

| Situacion | Status |
|---|---|
| Alta correcta | `201` |
| Consulta correcta | `200` |
| Cambio correcto | `200` |
| Baja correcta | `204` |
| ID invalido o DTO invalido | `400` |
| Recurso inexistente | `404` |
| Regla de negocio incumplida | `422` |

Registrar en `Program.cs`:

```csharp
app.MapTeamRoutes();
app.MapGroupRoutes();
app.MapMatchRoutes();
```

### Paso 7: Registrar dependencias

Actualizar `src/TournamentServices.Api/Program.cs`:

```csharp
builder.Services.AddSingleton<IGroupRepository, GroupRepository>();
builder.Services.AddSingleton<IMatchRepository, MatchRepository>();

builder.Services.AddScoped<IGroupDelegate, GroupDelegate>();
builder.Services.AddScoped<IMatchDelegate, MatchDelegate>();
```

Registrar tambien los cinco validadores nuevos con el mismo patron usado para `Team`.

Los repositorios en memoria deben ser `Singleton` para conservar datos durante la ejecucion de la API.

### Paso 8: Definir eliminacion de relaciones

Al eliminar un grupo:

1. Eliminar sus partidos asociados, si esa es la politica elegida.
2. Eliminar el grupo.
3. No eliminar los equipos; deben quedar disponibles para otro grupo.

Si los partidos se conservan al eliminar el grupo, deben quedar con `groupId` nulo. Elegir una sola politica y cubrirla con pruebas. Para esta primera implementacion se recomienda eliminar los partidos asociados para evitar referencias invalidas.

El `Tournament` queda fuera de esta entrega; por tanto no se implementa aqui el borrado en cascada del torneo completo.

### Paso 9: Agregar pruebas

Crear pruebas de dominio:

```text
tests/TournamentServices.Domain.Tests/GroupTests.cs
tests/TournamentServices.Domain.Tests/MatchTests.cs
tests/TournamentServices.Domain.Tests/ScoreTests.cs
```

Crear pruebas de repositorios:

```text
tests/TournamentServices.Repositories.Tests/GroupRepositoryTests.cs
tests/TournamentServices.Repositories.Tests/MatchRepositoryTests.cs
```

Crear pruebas de delegates:

```text
tests/TournamentServices.Delegates.Tests/GroupDelegateTests.cs
tests/TournamentServices.Delegates.Tests/MatchDelegateTests.cs
```

Casos minimos:

- Obtener listas vacias y pobladas.
- Obtener entidades existentes y devolver `null` cuando no existen.
- Crear y generar ID.
- Rechazar nombres duplicados.
- Rechazar torneo, grupo o equipo inexistente.
- Rechazar equipos fuera del torneo.
- Rechazar equipos repetidos o exceso de equipos.
- Crear partido valido.
- Rechazar partido contra el mismo equipo.
- Rechazar marcador negativo.
- Calcular ganador y empate.
- Actualizar y eliminar correctamente.
- Verificar limpieza de partidos al eliminar un grupo.

Crear pruebas de API:

```text
tests/TournamentServices.Api.Tests/Routes/GroupRoutesTests.cs
tests/TournamentServices.Api.Tests/Routes/MatchRoutesTests.cs
```

Cubrir por cada recurso:

- `GET` con lista vacia y con datos.
- `GET` por ID existente, inexistente e invalido.
- `POST` valido e invalido.
- `PUT` valido e inexistente.
- `PATCH` valido e inexistente.
- `DELETE` exitoso, inexistente e invalido.
- `201` con encabezado `Location`.
- `204` sin contenido.
- Errores `400`, `404` y `422`.
- Relaciones entre torneo, grupos, equipos y partidos.

Reutilizar `TournamentApiTests` y ampliar su fabrica para registrar repositorios limpios de grupos y partidos en cada prueba.

### Paso 10: Ejecutar la validacion final

Desde la carpeta que contiene `TournamentServices.sln`:

```powershell
dotnet build TournamentServices.sln
dotnet test TournamentServices.sln
dotnet test TournamentServices.sln --collect:"XPlat Code Coverage"
```

La implementacion queda lista cuando las pruebas nuevas y las pruebas existentes de `Team` pasan juntas.

## 6. Criterios de aceptacion

- `Team` conserva su CRUD y sus pruebas actuales.
- `Group` tiene altas, consultas, cambios, bajas y asignacion de equipos.
- `Match` tiene altas, consultas, cambio de marcador y bajas.
- Las rutas solo adaptan HTTP.
- Los delegates concentran las reglas y validan relaciones.
- Los repositorios abstraen el almacenamiento.
- Los IDs invalidos devuelven `400`.
- Los recursos inexistentes devuelven `404`.
- Las reglas de negocio incumplidas devuelven `422`.
- Las altas devuelven `201` y `Location`.
- Las bajas exitosas devuelven `204`.
- Los marcadores calculan correctamente ganador, empate y estado completado.
- No quedan partidos invalidos al eliminar un grupo.
- `dotnet build` y `dotnet test` terminan correctamente.

## 7. Decisiones que deben confirmarse

Antes de programar, acordar:

1. Si al borrar un grupo se eliminan sus partidos o se conserva el partido con `groupId` nulo. Se recomienda eliminarlos.
2. Si un partido sin grupo es valido. El contrato lo permite porque `groupId` es opcional.
3. Si un empate cuenta como partido completado. Se recomienda que si.
4. Los limites exactos de `maxTeamsPerGroup`.
5. Si las reglas de negocio se responderan con `422`. Se recomienda usar `422` y reservar `400` para entrada invalida.

## 8. Resultado esperado

Al finalizar, la API debe conservar el CRUD existente de equipos y sumar:

```text
/tournaments/{tournamentId}/groups
/tournaments/{tournamentId}/groups/{groupId}
/tournaments/{tournamentId}/matches
/tournaments/{tournamentId}/matches/{matchId}
```

La entrega queda limitada a `Group` y `Match`; `Team` se reutiliza y `Tournament` se trata como dependencia padre existente.
