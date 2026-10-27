# MundialNube - Configuración local de PostgreSQL

Este documento explica cómo levantar la base de datos PostgreSQL utilizada por **MundialNube** en un entorno local.

Actualmente el proyecto utiliza:

- PostgreSQL 18.6
- Entity Framework Core
- Npgsql
- Podman/Docker Compose
- Base de datos `tournament_db`
- Usuario `tournament_svc`

La aplicación utiliza PostgreSQL mediante la variable de entorno:

`ConnectionStrings__DefaultConnection`

> **Importante:** cada integrante del equipo tendrá su propia instancia local de PostgreSQL. No es necesario conectarse a la base de datos de otro integrante.

---

## 1. Obtener la versión correcta del proyecto

Actualmente la configuración de PostgreSQL se encuentra en la rama:

```bash
ci/jenkins-activity
```

Actualizar las ramas remotas:

```bash
git fetch
```

Cambiar a la rama:

```bash
git switch ci/jenkins-activity
```

Actualizarla:

```bash
git pull
```

Confirmar la rama actual:

```bash
git branch --show-current
```

Debe mostrar:

```text
ci/jenkins-activity
```

> Cuando estos cambios sean integrados a `main`, ya no será necesario cambiar específicamente a esta rama.

---

## 2. Archivos relacionados con PostgreSQL

En la raíz del proyecto deben existir:

```text
MundialNube/
├── compose.yaml
├── .env.example
├── .gitignore
├── TournamentServices.sln
└── src/
    └── TournamentServices.Api/
        ├── Program.cs
        └── appsettings.json
```

El archivo `compose.yaml` define el servidor PostgreSQL.

El archivo `.env.example` contiene un ejemplo de la configuración necesaria.

El archivo `.env` contendrá la configuración local de cada desarrollador y **no debe subirse a GitHub**.

---

## 3. Crear el archivo `.env`

Crear una copia de `.env.example`.

En Git Bash o Linux:

```bash
cp .env.example .env
```

En PowerShell:

```powershell
Copy-Item .env.example .env
```

El archivo tendrá una configuración similar a:

```env
POSTGRES_DB=tournament_db
POSTGRES_USER=tournament_svc
POSTGRES_PASSWORD=change-me
POSTGRES_PORT=5432

ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=tournament_db;Username=tournament_svc;Password=change-me
```

Se recomienda cambiar:

```text
change-me
```

por una contraseña local.

Por ejemplo:

```env
POSTGRES_PASSWORD=my-local-password
```

y actualizar la misma contraseña dentro de:

```env
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=tournament_db;Username=tournament_svc;Password=my-local-password
```

Las dos contraseñas deben coincidir.

### Seguridad

El archivo `.env` ya está incluido en `.gitignore`.

Por lo tanto:

```bash
git status
```

**no debe mostrar `.env` como archivo pendiente para commit.**

Nunca subir contraseñas reales al repositorio.

---

# 4. Levantar PostgreSQL

## Opción A: Podman

Verificar que Podman funcione:

```bash
podman --version
```

Levantar PostgreSQL:

```bash
podman compose up -d
```

Verificar el estado:

```bash
podman compose ps
```

El resultado debe mostrar el servicio PostgreSQL como:

```text
Up ... (healthy)
```

### Si `podman compose` dice que no existe un proveedor

En Fedora se puede instalar `podman-compose`:

```bash
sudo dnf install -y podman-compose
```

Comprobar:

```bash
podman-compose --version
```

Después:

```bash
podman compose up -d
```

---

## Opción B: Docker

Si se utiliza Docker Desktop:

```bash
docker compose up -d
```

Y para revisar:

```bash
docker compose ps
```

---

# 5. Comprobar que PostgreSQL funciona

Con Podman:

```bash
podman compose exec postgres psql -U tournament_svc -d tournament_db
```

Con Docker:

```bash
docker compose exec postgres psql -U tournament_svc -d tournament_db
```

Al entrar debe aparecer:

```text
tournament_db=#
```

Se puede comprobar PostgreSQL con:

```sql
SELECT version();
```

Para listar las bases de datos:

```sql
\l
```

Debe existir:

```text
tournament_db
```

Para salir:

```sql
\q
```

---

# 6. Crear las tablas del proyecto

En una instalación nueva, levantar PostgreSQL **no crea automáticamente las tablas de MundialNube**.

Actualmente la API utiliza Entity Framework Core y ejecuta:

```csharp
Database.EnsureCreatedAsync()
```

cuando arranca.

Por lo tanto, después de levantar PostgreSQL se debe ejecutar la API al menos una vez.

---

## 7. Cargar la connection string y ejecutar la API

El archivo `.env` es utilizado automáticamente por Compose, pero `dotnet run` **no carga automáticamente archivos `.env`**.

### Git Bash / Linux

Desde la raíz de MundialNube:

```bash
set -a
source .env
set +a
```

Después:

```bash
dotnet restore TournamentServices.sln
```

```bash
dotnet run --project src/TournamentServices.Api/TournamentServices.Api.csproj
```

---

### PowerShell

Configurar la variable de entorno para la terminal actual:

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=tournament_db;Username=tournament_svc;Password=TU_PASSWORD"
```

Después:

```powershell
dotnet restore TournamentServices.sln
```

```powershell
dotnet run --project src/TournamentServices.Api/TournamentServices.Api.csproj
```

Reemplazar:

```text
TU_PASSWORD
```

por la misma contraseña configurada en `.env`.

---

## 8. Verificar que las tablas fueron creadas

Después de iniciar correctamente la API, abrir otra terminal.

Con Podman:

```bash
podman compose exec postgres psql -U tournament_svc -d tournament_db
```

Después ejecutar:

```sql
\dt
```

Actualmente deben existir:

```text
Groups
Matches
Teams
Tournaments
```

El resultado debe ser similar a:

```text
 Schema |    Name     | Type  |     Owner
--------+-------------+-------+----------------
 public | Groups      | table | tournament_svc
 public | Matches     | table | tournament_svc
 public | Teams       | table | tournament_svc
 public | Tournaments | table | tournament_svc
```

---

# 9. Inspeccionar una tabla

Para revisar la estructura de `Tournaments`:

```sql
\d "Tournaments"
```

Para `Groups`:

```sql
\d "Groups"
```

Para `Matches`:

```sql
\d "Matches"
```

También se pueden consultar los datos:

```sql
SELECT * FROM "Tournaments";
```

```sql
SELECT * FROM "Groups";
```

```sql
SELECT * FROM "Matches";
```

> PostgreSQL distingue mayúsculas/minúsculas cuando los nombres están entre comillas. Por eso se utilizan nombres como `"Tournaments"`.

---

# 10. Detener PostgreSQL

Para apagar los contenedores sin eliminar los datos:

### Podman

```bash
podman compose down
```

### Docker

```bash
docker compose down
```

Los datos permanecen almacenados en el volumen:

```text
tournament-postgres-data
```

Cuando se vuelva a ejecutar:

```bash
podman compose up -d
```

las tablas y datos anteriores seguirán disponibles.

---

# 11. Reiniciar PostgreSQL

Para volver a trabajar otro día:

```bash
podman compose up -d
```

Verificar:

```bash
podman compose ps
```

Y comprobar las tablas:

```bash
podman compose exec postgres psql -U tournament_svc -d tournament_db -c '\dt'
```

---

# 12. Reiniciar completamente la base de datos

⚠️ **Este procedimiento elimina todas las tablas y datos locales.**

Detener los servicios y eliminar el volumen:

```bash
podman compose down -v
```

Luego crear una base nueva:

```bash
podman compose up -d
```

Después se debe ejecutar nuevamente la API para crear las tablas.

No utilizar `-v` si se desea conservar la información existente.

---

# Arquitectura actual

```text
MundialNube API
       │
       │ Entity Framework Core
       │ Npgsql
       ▼
PostgreSQL 18.6
       │
       ▼
tournament_db
       │
       ├── Tournaments
       ├── Teams
       ├── Groups
       └── Matches
```

La API que corre directamente en la computadora utiliza:

```text
Host=localhost
Port=5432
```

PostgreSQL corre dentro de un contenedor y publica el puerto `5432` hacia la máquina local.

---

# Comandos rápidos

Levantar PostgreSQL:

```bash
podman compose up -d
```

Revisar estado:

```bash
podman compose ps
```

Entrar a PostgreSQL:

```bash
podman compose exec postgres psql -U tournament_svc -d tournament_db
```

Ver tablas:

```sql
\dt
```

Salir:

```sql
\q
```

Apagar PostgreSQL:

```bash
podman compose down
```

---

## Nota sobre migraciones

Si la base ya existia de una version anterior, hay que recrearla porque ahora tambien se guardan equipos, grupos y partidos:

```bash
podman compose down -v
podman compose up -d
```

Actualmente el proyecto utiliza `EnsureCreatedAsync()` para crear el esquema inicial de PostgreSQL.

En una etapa posterior del proyecto se implementarán **EF Core Migrations** para versionar formalmente los cambios de estructura de la base de datos y utilizarlos dentro del proceso de despliegue CI/CD.
