# Migraciones y proveedores

El esquema de producción usa PostgreSQL 16. SQLite se mantiene para pruebas y bases locales previas. La migración inicial `20261125000100_PersistentTournamentStore` tiene SQL explícito para ambos motores; `TournamentDatabaseInitializer.InitializeAsync` adopta los archivos SQLite creados antes de usar migraciones y registra la migración mediante EF. Nunca elimina torneos ni campeones.

Se conservan dos snapshots generados por EF y `ProviderMigrationsAssembly` selecciona el correspondiente al proveedor activo. `dotnet ef` usa PostgreSQL por defecto; con `TOURNAMENT_MIGRATION_PROVIDER=Sqlite` selecciona SQLite. Al cambiar el modelo deben actualizarse ambos snapshots y revisar ambos caminos de cada migración; no se deben ignorar los avisos de cambios pendientes de modelo. `ProviderMigrationTests` comprueba ambos snapshots sin conectar a una base.

La migración se ejecuta con la aplicación `--migrate`, en un Job previo a las réplicas de la API. El rollback destructivo de la migración inicial está deshabilitado: restaurar una copia verificada es una operación explícita.

La prueba PostgreSQL real usa `MUNDIAL_TEST_POSTGRES`, crea un esquema temporal de nombre aleatorio y lo elimina al terminar; no borra la base configurada. Sin esa variable, la prueba se marca omitida, no aprobada.
