# Implementacion del Core del Mundial: Tournaments

## 1. Objetivo

Implementar la vertical de torneos como el nucleo que define las reglas generales de la copa:

- Entidad de dominio `Tournament`.
- Value Object `TournamentFormat`, inicialmente para `ROUND_ROBIN`.
- Reglas de limites y configuracion de grupos.
- `ITournamentRepository` y su implementacion.
- `TournamentDelegate` como orquestador de casos de uso.
- Endpoints REST bajo `/tournaments`.
- Actualizacion parcial mediante `PATCH`.
- Eliminacion en cascada de grupos y partidos relacionados.
- Pruebas unitarias del delegado y cobertura completa de los endpoints.

La implementacion debe seguir el patron que ya existe para `Team`, sin mezclar reglas de dominio dentro de las rutas.

## 2. Estado actual del repositorio

### Ya implementado y reutilizable

- La solucion `TournamentServices.sln` ya separa los proyectos `Api`, `Delegates`, `Domain` y `Repositories`.
- `Team` en `src/TournamentServices.Domain/Team.cs` sirve como referencia para una entidad sencilla con `Id` y `Name`.
- `TeamDelegate` muestra el patron de inyeccion de un repositorio, validacion de existencia y traduccion de reglas a excepciones.
- `ITeamRepository` y `TeamRepository` muestran el contrato CRUD y el almacenamiento actual en memoria.
- `TeamRoutes` muestra el patron de Minimal APIs, `TypedResults`, validacion con FluentValidation y mapeo a DTOs.
- `Program.cs` ya registra dependencias, serializa enums como texto y expone Swagger en desarrollo.
- Las pruebas API ya usan `WebApplicationFactory<Program>` y reemplazan repositorios por implementaciones aisladas para cada test.
- Los proyectos de pruebas ya tienen xUnit, Moq, `Microsoft.NET.Test.Sdk` y Coverlet Collector.

### No aparece implementado

- No existe `Tournament`, `TournamentFormat` ni una excepcion especifica de torneos.
- No existen DTOs, validadores ni rutas `/tournaments`.
- No existen `ITournamentDelegate`, `TournamentDelegate`, `ITournamentRepository` ni `TournamentRepository`.
- No existen entidades o repositorios de `Group` y `Match` en la solucion actual.
- No existe configuracion de persistencia relacional ni transacciones: el repositorio actual es en memoria.
- No hay pruebas de endpoints ni del delegado para torneos.
- La solucion no puede demostrar cascada real sobre grupos y partidos hasta que esas entidades y sus relaciones existan.

Por tanto, esta tarea no es solamente agregar rutas: requiere crear una nueva vertical y definir el contrato con las futuras verticales de grupos y partidos.

## 3. Decisiones que deben cerrarse antes de programar

1. **Limites de grupos**: confirmar los valores exactos de minimo y maximo. Si aun no hay una regla oficial, centralizarlos en `TournamentFormat` para que cambiar la regla no obligue a modificar las rutas.
2. **Cantidad de equipos**: decidir si el torneo guarda `TeamIds`, si los equipos se asignan desde otra vertical o si solo se valida la cantidad de grupos en esta primera entrega.
3. **Forma de `PATCH`**: se recomienda un DTO con propiedades anulables y una semantica clara de "campo omitido" frente a "campo enviado como null". No conviene reutilizar ciegamente el DTO de `PUT`.
4. **Campos editables**: definir si se pueden actualizar `Name`, `Format` y `GroupCount`, y si alguno queda bloqueado despues de crear grupos o partidos.
5. **Borrado en cascada**: confirmar que `ITournamentRepository.DeleteAsync` sera el limite transaccional de la cascada o, si grupos/partidos tienen repositorios separados, definir una unidad de trabajo. En ambos casos el borrado debe ser atomico en una base de datos real.
6. **Formato de identificadores**: reutilizar la convencion alfanumerica con guiones de `Team.IdPattern` o declarar un patron comun para todas las entidades.

## 4. Paso a paso de implementacion

### Paso 1: Definir el modelo de dominio

Crear en `src/TournamentServices.Domain`:

- `Tournament.cs`.
- `TournamentFormat.cs` o una carpeta `ValueObjects`.
- Excepciones como `TournamentNotFoundException`, `DuplicateTournamentNameException` y `InvalidTournamentFormatException` si las reglas necesitan distinguir esos casos.

Una forma coherente con el codigo actual seria:

```csharp
public sealed class Tournament
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; private set; } = string.Empty;
    public TournamentFormat Format { get; private set; } = TournamentFormat.RoundRobin;
    public int GroupCount { get; private set; }
}
```

`TournamentFormat` debe encapsular la regla del formato, no ser solamente un string libre. Puede ser un enum (`RoundRobin`) acompañado por un Value Object que valide el numero de grupos, o un Value Object con una propiedad `Type` y metodos como `ValidateGroupCount`.

Reglas minimas recomendadas para `ROUND_ROBIN`:

- El formato recibido debe ser exactamente uno soportado.
- `GroupCount` debe estar dentro de los limites oficiales.
- El nombre no debe ser vacio ni superar la longitud acordada.
- El `Id` debe cumplir el patron usado por la API.
- Una instancia invalida no debe poder llegar al repositorio.

La entidad debe exponer metodos de cambio que mantengan invariantes, por ejemplo `UpdateConfiguration(...)`, en vez de dejar que la ruta asigne cualquier valor directamente.

### Paso 2: Crear DTOs y validadores

En `src/TournamentServices.Api/Dtos` crear, como minimo:

```csharp
public record TournamentDto(
    string Id,
    string Name,
    TournamentFormat Format,
    int GroupCount);

public record CreateTournamentDto(
    string Name,
    TournamentFormat Format,
    int GroupCount);

public record PatchTournamentDto(
    string? Name,
    TournamentFormat? Format,
    int? GroupCount);
```

En `src/TournamentServices.Api/Validators` agregar validadores para:

- Nombre requerido, longitud y formato.
- `Format` soportado.
- `GroupCount` dentro del rango de `TournamentFormat`.
- Reglas de combinacion entre formato y grupos.

Para `PATCH`, el validador debe validar solo los campos presentes, pero tambien debe validar el estado final resultante. Por ejemplo, no basta con aceptar un `GroupCount` valido aisladamente si combinado con el formato actual deja una configuracion invalida.

### Paso 3: Definir el repositorio

Crear `ITournamentRepository` en `src/TournamentServices.Repositories` con operaciones equivalentes a las de equipos:

```csharp
Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken ct = default);
Task<Tournament?> GetByIdAsync(string id, CancellationToken ct = default);
Task<Tournament?> GetByNameAsync(string name, CancellationToken ct = default);
Task AddAsync(Tournament tournament, CancellationToken ct = default);
Task UpdateAsync(Tournament tournament, CancellationToken ct = default);
Task DeleteAsync(string id, CancellationToken ct = default);
```

`DeleteAsync` debe documentarse como una eliminacion del agregado completo. Su responsabilidad es eliminar:

1. El torneo.
2. Los grupos cuyo `TournamentId` coincide.
3. Los partidos pertenecientes a esos grupos o al torneo.

En la implementacion actual en memoria, esto exige introducir tambien colecciones de grupos y partidos o un repositorio de agregado que tenga acceso a ellas. Si esas entidades seran responsabilidad de otros repositorios, el contrato debe cambiar a una unidad de trabajo o a un servicio de persistencia transaccional. No se debe simular la cascada eliminando solo el torneo y dejando datos huerfanos.

Para una base de datos posterior, configurar claves foraneas con `ON DELETE CASCADE` y ejecutar la eliminacion dentro de una transaccion. Los tests en memoria deben verificar el mismo comportamiento observable.

### Paso 4: Implementar `TournamentDelegate`

Crear `ITournamentDelegate` y `TournamentDelegate` en `src/TournamentServices.Delegates`, siguiendo `TeamDelegate`.

Casos de uso esperados:

- `GetAllAsync`.
- `GetByIdAsync`.
- `CreateAsync`.
- `UpdateAsync` para reemplazo completo si se necesita mantener simetria con `Team`.
- `PatchAsync` para actualizacion parcial.
- `DeleteAsync`.

Responsabilidades del delegado:

- Consultar el torneo actual.
- Aplicar validacion de unicidad del nombre.
- Construir la entidad a traves de sus reglas de dominio.
- Calcular el estado final de un `PATCH` combinando valores actuales y valores enviados.
- Rechazar cambios invalidos con excepciones de dominio.
- Delegar la persistencia a `ITournamentRepository`.
- Invocar una sola operacion de borrado del agregado para garantizar la cascada.

Las rutas no deben hacer llamadas directas a `ITournamentRepository`; deben depender de `ITournamentDelegate`, igual que `TeamRoutes` depende de `ITeamDelegate`.

### Paso 5: Registrar las dependencias

Actualizar `src/TournamentServices.Api/Program.cs`:

- Registrar `ITournamentRepository` con la misma duracion que el almacenamiento utilizado.
- Registrar `ITournamentDelegate` como `Scoped`, siguiendo `ITeamDelegate`.
- Registrar los validadores de crear y actualizar.
- Registrar las rutas con `app.MapTournamentRoutes()`.

Si los grupos y partidos ya existen cuando se implemente esta tarea, registrar tambien las dependencias necesarias para que `DeleteAsync` pueda ejecutar la cascada.

### Paso 6: Implementar las rutas `/tournaments`

Crear `src/TournamentServices.Api/Routes/TournamentRoutes.cs` con, como minimo:

| Metodo | Ruta | Resultado esperado |
|---|---|---|
| `GET` | `/tournaments` | `200` y lista, incluso vacia |
| `GET` | `/tournaments/{tournamentId}` | `200` o `404` |
| `POST` | `/tournaments` | `201` con `Location` |
| `PATCH` | `/tournaments/{tournamentId}` | `200` con el estado final |
| `DELETE` | `/tournaments/{tournamentId}` | `204` y cascada completa |

Comportamiento recomendado:

- ID invalido: `400` con error asociado a `tournamentId`.
- JSON o campos invalidos: `400` mediante `ValidationProblem`.
- Torneo inexistente: `404`.
- Nombre duplicado: `400` o `409`, pero elegir una sola convencion y aplicarla tambien a futuras verticales.
- Creacion correcta: `201 Created` y URL `/tournaments/{id}`.
- `PATCH` sin campos: rechazar con `400` para evitar una operacion ambigua.
- `DELETE` correcto: `204 No Content`.

El mapeo a `TournamentDto` debe impedir que se expongan detalles internos del repositorio o de grupos/partidos.

### Paso 7: Resolver correctamente el `PATCH`

El punto mas delicado es distinguir estas situaciones:

- Propiedad omitida: conservar el valor actual.
- Propiedad enviada con valor: reemplazarla.
- Propiedad enviada como `null`: rechazarla si el campo no admite null.

El delegado debe obtener el torneo actual, construir la configuracion resultante y validarla como un todo antes de llamar a `UpdateAsync`. La secuencia recomendada es:

1. Validar el ID en la ruta.
2. Buscar el torneo.
3. Combinar valores actuales con valores recibidos.
4. Validar unicidad y reglas de `TournamentFormat`.
5. Actualizar la entidad.
6. Persistir una sola vez.
7. Devolver el estado actualizado.

Si se permite cambiar formato o numero de grupos despues de crear grupos o partidos, se deben agregar reglas explicitas para no invalidar datos existentes. La opcion mas segura es bloquear esos cambios una vez iniciado el torneo.

### Paso 8: Implementar la cascada de borrado

Antes de escribir el endpoint `DELETE`, acordar quien posee las relaciones:

- `Group` debe tener `TournamentId`.
- `Match` debe tener `GroupId` o `TournamentId`.
- El borrado debe respetar el orden de dependencias si no existe cascade en la base de datos: partidos, grupos y finalmente torneo.
- Una falla intermedia debe dejar la operacion completa revertida en persistencia transaccional.

El test de API debe crear un torneo, sus grupos y sus partidos, ejecutar `DELETE /tournaments/{id}` y comprobar que ninguna de las entidades relacionadas puede recuperarse. Con el estado actual del repositorio, este test no puede implementarse honestamente hasta que existan `Group` y `Match`.

### Paso 9: Agregar pruebas unitarias del delegado

Crear `tests/TournamentServices.Delegates.Tests/TournamentDelegateTests.cs` usando Moq, replicando el estilo de `TeamDelegateTests`.

Casos minimos:

- Obtener todos los torneos.
- Obtener un torneo existente.
- Obtener `null` cuando no existe.
- Crear un torneo valido y persistirlo.
- Rechazar nombre duplicado.
- Rechazar formato no soportado.
- Rechazar cantidad de grupos fuera de limites.
- Actualizar configuracion completa.
- Aplicar `PATCH` conservando campos omitidos.
- Rechazar `PATCH` que deja una configuracion invalida.
- Rechazar actualizacion de un torneo inexistente.
- Eliminar un torneo existente y verificar una llamada al repositorio.
- Rechazar eliminacion de un torneo inexistente.
- Propagar o traducir correctamente la falla de persistencia.

Verificar tambien que el delegado pase el mismo `CancellationToken` al repositorio y que no haga llamadas redundantes.

### Paso 10: Agregar pruebas de endpoints

Crear `tests/TournamentServices.Api.Tests/Routes/TournamentRoutesTests.cs`, usando la infraestructura existente de `TournamentApiTests`.

La fabrica de pruebas debe reemplazar `ITournamentRepository` por una instancia limpia por test, igual que ya hace con `ITeamRepository`.

La matriz de cobertura debe incluir:

- `GET` de lista vacia y con datos.
- `GET` por ID existente, inexistente e invalido.
- `POST` valido, cuerpo incompleto, nombre vacio, enum invalido, grupos fuera de rango y nombre duplicado.
- `PATCH` con cada campo por separado, varios campos juntos, cuerpo vacio, ID inexistente, ID invalido y estado final invalido.
- `DELETE` exitoso, inexistente e ID invalido.
- `DELETE` que comprueba la eliminacion de grupos y partidos relacionados.
- Verificacion del status code, cuerpo, errores de validacion y encabezado `Location`.

El objetivo de "100% de cobertura en endpoints" debe entenderse como cubrir todas las ramas relevantes de las rutas y sus adaptadores, no solo ejecutar una peticion feliz por endpoint. Ademas de cobertura de lineas, revisar cobertura de ramas para los `if`, `catch` y validaciones.

## 5. Orden recomendado de trabajo

1. Confirmar limites de grupos, campos y politica de cambios despues de iniciar el torneo.
2. Crear `TournamentFormat`, `Tournament`, excepciones y pruebas de dominio.
3. Crear DTOs y validadores.
4. Definir `ITournamentRepository` y la estrategia de cascada.
5. Implementar `TournamentRepository` y sus pruebas.
6. Crear `ITournamentDelegate` y `TournamentDelegate`.
7. Escribir primero las pruebas unitarias del delegado y hacerlas pasar.
8. Registrar dependencias en `Program.cs`.
9. Crear `TournamentRoutes`.
10. Agregar las pruebas de integracion de todos los endpoints.
11. Integrar grupos y partidos para probar el borrado en cascada real.
12. Ejecutar cobertura y corregir ramas no cubiertas.
13. Ejecutar la solucion completa y revisar Swagger/manualmente los contratos.

## 6. Validacion final

Desde la carpeta que contiene `TournamentServices.sln`:

```powershell
dotnet build TournamentServices.sln
dotnet test TournamentServices.sln
dotnet test TournamentServices.sln --collect:"XPlat Code Coverage"
```

Para un resultado mas util, generar el reporte con la herramienta de cobertura adoptada por el equipo y comprobar por separado:

- Cobertura de `TournamentRoutes`.
- Cobertura de `TournamentDelegate`.
- Ramas de validacion y excepciones.
- Casos de borrado en cascada.

La entrega esta completa cuando el modelo protege sus invariantes, el delegado concentra la orquestacion, las rutas solo adaptan HTTP, el repositorio elimina el agregado completo y las pruebas demuestran tanto respuestas HTTP como reglas internas.

## 7. Riesgos y pendientes visibles

- La solucion actual no tiene base de datos: la cascada en memoria no representa aun una transaccion real.
- No existen `Group` ni `Match`, por lo que el requisito de eliminarlos en cascada necesita coordinacion con sus responsables.
- Los limites exactos de grupos no estan especificados en el repositorio inspeccionado.
- El proyecto usa `net10.0`; el SDK correspondiente debe estar instalado para compilar y ejecutar las pruebas.
- El contrato de `PATCH` debe definirse antes de fijar los DTOs, porque un record con propiedades anulables no resuelve por si solo la diferencia entre campo omitido y `null`.
