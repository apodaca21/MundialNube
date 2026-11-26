# Verificación de max-v3

Pruebas ejecutadas el 25 de noviembre de 2026 (Tijuana; los archivos usan UTC). Esta entrega parte de `maxv2` de MundialNube. No reutiliza como resultados las cifras del inventario de otro repositorio.

## Aplicación y datos

- **241 pruebas aprobadas, 0 fallos, 0 omitidas**: 23 de dominio, 48 de delegados, 33 de repositorios y 137 de API. Incluye PostgreSQL 16 real en un esquema temporal aislado, además de SQLite y migración del esquema anterior.
- Simulación completa por HTTP contra el entorno Compose v3: ocho grupos, 32 participantes propios, 48 partidos de grupos, 15 de eliminatoria y campeón persistido.
- ActiveMQ Artemis real: se comprobó en PostgreSQL un campeón, su evento publicado y su recepción única en `ConsumedEvents`.
- Prometheus devolvió simultáneamente series HTTP y de comandos PostgreSQL reales. Tempo devolvió trazas `database.select`. La página del simulador se revisó visualmente en el navegador.
- Compilación Release para Linux ARM64 e imagen de contenedor completadas. Las credenciales locales permanecen fuera del repositorio.

Resumen verificable: [application-verification.json](application-verification.json). El script `scripts/seed-demo.py` permite añadir torneos completos para repetir la demostración; cada ejecución agrega datos y conserva los existentes.

## Demostración con 20 torneos

Se ejecutó el script de carga contra la imagen final: añadió 19 torneos al torneo de integración existente. La base PostgreSQL v3 quedó con **20 torneos, 640 equipos propios, 1.260 partidos completados y 20 campeones**. El script comprobó el ganador de cada final y la independencia de los participantes. [Detalle de la carga y campeones](demo-20-tournaments.json). La demostración SQLite anterior permanece separada.

## Carga sobre Compose

[compose-locust-smoke.json](compose-locust-smoke.json) proviene de Locust real contra la API v3: 4 usuarios, 417 solicitudes, 0 fallos, aproximadamente 29,53 solicitudes/s, p95 de 43 ms y p99 de 1.200 ms. El escenario mezcla `GET /health` y `GET /tournaments` y registra qué instancia respondió.

Es una comprobación breve sobre Compose, **no** una medición de Kubernetes, Ingress, HPA ni del límite de 56 MiB. No se extrapola su rendimiento a otros entornos.

## Límite de recursos

La configuración solicitada está en los manifiestos: request y límite de **100m CPU y 56Mi memoria**. `100m` equivale a 0,1 núcleo.

En una comprobación preliminar de un contenedor separado con esos límites, `/health` devolvió 200 en 18,55 segundos, pero `/ready` agotó 30 segundos. El consumo observado alcanzó 54,64 MiB. Por tanto, esa comprobación **no cumple** el criterio de disponibilidad y no acredita que 56 MiB sean suficientes. Los resultados de Kubernetes se documentan por separado.

## Reproducción y alcance

[Infraestructura v3](../../docs/INFRAESTRUCTURA_V3.md) explica Secrets, migración mediante Job, Ingress, HPA, mediciones por Pod y escenarios de uno o varios nodos. Los scripts guardan usuarios, RPS, fallos, percentiles, recursos y distribución real de respuestas.

El workflow incluye pruebas con PostgreSQL, publicación de imagen y despliegue manual. Su ejecución remota y el despliegue automático dependen de GitHub Actions y del runner configurado; no se presentan como verificados solo por existir el archivo.

## Kubernetes: ejecución limitada por el host

Se creó realmente un clúster kind de **un nodo, Kubernetes 1.34**, con namespace aislado. PostgreSQL quedó listo y el Job `tournaments-migrate` terminó correctamente antes de desplegar la API. Traefik también llegó a Ready. La API con **100m CPU / 56Mi** seguía sin Ready a los 7 minutos y 15 segundos; el rollout agotó su espera. En esa captura tenía 0 reinicios, 95m de CPU y 14Mi de memoria. Estos datos **no demuestran un OOM** ni permiten atribuir todo el fallo al límite de memoria.

El host compartido de Docker tenía 3,828 GiB y sufrió saturación y pérdidas de conexión con Kubernetes durante descargas/arranque. Parar temporalmente solo el nuevo entorno Compose recuperó el acceso, pero no produjo un despliegue saludable dentro de la ventana del ensayo. Se retiró únicamente el clúster de prueba y se restauró la demostración Compose. Las aplicaciones anteriores se conservaron.

Evidencia: [resumen](kubernetes-single/summary.json), [Pods](kubernetes-single/pods.txt), [recursos](kubernetes-single/resources.txt), [eventos](kubernetes-single/events.txt), [Job de migración](kubernetes-single/migration-job.json) y [despliegue](kubernetes-single/deployment.txt). La imagen probada en kind era anterior a la revisión final; la revisión final sí se compiló y probó por separado.

**Pendiente de ejecución en un host con recursos suficientes:** disponibilidad de API a 56Mi, comparación Locust sin/con Ingress y 1/3 réplicas, escalado HPA y topología de varios nodos. Hay configuraciones y scripts reproducibles para esos ensayos; no se incluyen cifras inventadas ni se presentan como pruebas aprobadas.
