# Infraestructura y ensayos v3

Estos archivos corresponden a MundialNube, partiendo de `maxv2`. No copian como evidencia las cifras del inventario de otro repositorio. Los ensayos producen sus propios archivos y fechas.

## PostgreSQL, mensajería y observabilidad con Compose

El archivo `compose.v3.yaml` crea un proyecto separado de la demostración SQLite anterior. Conserva datos en volúmenes propios. No usa ni modifica el contenedor PostgreSQL que pudiera estar escuchando en 5432.

```sh
cp .env.v3.example .env.v3
# Completar POSTGRES_PASSWORD, BROKER_PASSWORD y GRAFANA_ADMIN_PASSWORD.
# Usar contraseñas aleatorias alfanuméricas; nunca subir .env.v3.
docker compose --env-file .env.v3 -f compose.v3.yaml up --build -d
```

| Servicio | Dirección local predeterminada |
| --- | --- |
| API y página | http://localhost:8183 |
| Swagger | http://localhost:8183/swagger |
| Grafana | http://localhost:3103 |
| Prometheus | http://localhost:9193 |
| PostgreSQL | localhost:5433 |
| ActiveMQ Artemis / STOMP | localhost:61613 |

El contenedor `migrate` aplica el esquema una vez y termina. La API y el consumer esperan su salida correcta. Las réplicas normales tienen `Database__MigrateOnStartup=false`. `consumer` ejecuta la misma imagen con `--consume`; el publicador de la API entrega eventos de campeón desde la bandeja de salida persistente y el consumidor confirma su recepción. Los contenedores de infraestructura solo exponen puertos locales; Tempo permanece dentro de la red.

Grafana recibe automáticamente las fuentes Prometheus y Tempo y dos tableros: campeones y **MundialNube v3 - API, PostgreSQL y replicas**. El segundo compara el p95 de los comandos EF reales (`mundial_database_command_duration_seconds`) con el p95 HTTP (`http_request_duration_seconds`) en una misma ventana de 5 segundos de muestreo. Un request puede ejecutar varios comandos SQL: los percentiles no deben restarse como si midieran el mismo conjunto de operaciones. `GET /health` comprueba proceso y `/ready` comprueba disponibilidad de la base.

Para repetir mediciones, ejecutar el mismo escenario Locust dos veces, guardar cada JSON y seleccionar en Grafana sus marcas `started_at_utc` y `ended_at_utc`. Las trazas están en Explore > Mundial Tempo. La configuración monolítica de Tempo no necesita Kafka.

## Recursos solicitados

El manifiesto de la API fija tanto request como límite en **100m CPU y 56Mi memoria**. `100m` significa **0,1 núcleo**, no un cuarto. Incluye ajustes conservadores del GC y un startup probe amplio. Estos valores son una configuración a validar; no garantizan que .NET, EF y la instrumentación puedan funcionar bajo el límite.

Después de desplegar, guardar:

```sh
kubectl --context "$KUBE_CONTEXT" -n mundialnube-v3 get pods -o wide
kubectl --context "$KUBE_CONTEXT" -n mundialnube-v3 top pods
kubectl --context "$KUBE_CONTEXT" -n mundialnube-v3 describe pod -l app=tournaments-api
```

El criterio es arranque, `/ready` correcto, ausencia de reinicios/OOMKilled y estabilidad durante la carga. Si falla en 56Mi, registrar ese resultado. Para investigar puede aplicarse **explícitamente** `kubernetes/api-memory-diagnostic.patch.yaml`, que conserva el request de 56Mi y eleva solo el límite a 128Mi. Un resultado obtenido así **no demuestra** que se cumplió el límite original de 56Mi. El siguiente despliegue normal vuelve a los valores del manifiesto.

## Kubernetes aislado y migración antes del rollout

Requisitos: Docker, `kind`, `kubectl`, Python 3, y la imagen de la API construida. Los ejemplos no cambian el contexto activo del usuario: el helper guarda un kubeconfig específico. No se elimina ningún clúster automáticamente.

```sh
scripts/create-kind.sh single
export KUBECONFIG="$PWD/loadtests/results/mundial-v3-single.kubeconfig"
export KUBE_CONTEXT=kind-mundial-v3-single
scripts/load-kind-images.sh mundial-v3-single mundialnube-api:v3

# Exportar los tres passwords y, opcionalmente, BROKER_USER desde un gestor/local seguro.
scripts/create-kubernetes-secrets.sh "$KUBE_CONTEXT"
scripts/install-kind-metrics.sh "$KUBE_CONTEXT"
scripts/deploy-kubernetes.sh "$KUBE_CONTEXT" mundialnube-api:v3
```

El Secret `mundial-secrets` se crea desde variables del entorno y no está en Git. Contiene credenciales de PostgreSQL, Artemis y Grafana; la cadena PostgreSQL no se guarda en un ConfigMap. Para imágenes privadas de GHCR hay que configurar previamente el `imagePullSecret` del namespace o dar acceso al registro desde el nodo. El helper de despliegue valida el contexto y la imagen, espera PostgreSQL, ejecuta el Job `tournaments-migrate`, exige que termine y solo entonces actualiza API/consumer y el resto del stack. Si el Job falla, no publica la nueva versión de la API.

El despliegue completo se renderiza con:

```sh
kubectl kustomize --load-restrictor LoadRestrictionsNone kubernetes/base
```

El permiso de lectura ampliada solo permite a Kustomize incluir los archivos de `monitoring/` del mismo repositorio. Los YAML no contienen valores de secretos. La API se distribuye por hostname mediante `topologySpreadConstraints`; la cantidad de nodos y de Pods atendiendo tráfico se comprueba en las evidencias, no se presupone.

Traefik v3.8.0 atiende el Ingress `mundial-traefik`, host `mundial.local`, y balancea contra endpoints de los Pods. El puerto del host es 18080 en el clúster de un nodo y 18081 en el de tres nodos. Prueba:

```sh
curl -i -H 'Host: mundial.local' http://127.0.0.1:18080/health
kubectl --context "$KUBE_CONTEXT" -n mundialnube-v3 port-forward service/grafana 3104:3000
```

La respuesta de API incluye `X-Pod-Name` para verificar quién atendió cada petición. Prometheus descubre y consulta cada Pod individual, conservando las etiquetas `pod` y `node`; no consulta solo una IP del Service.

El HPA usa CPU al 40%, entre 1 y 3 réplicas. Metrics Server 0.7.2 admite Kubernetes 1.27+ y cubre el 1.29 del inventario. La excepción TLS de kubelet de su helper solo se aplica a contextos `kind-*`, para laboratorio. Los nuevos clústeres usan por defecto `kindest/node:v1.34.0`, configurable con `KIND_NODE_IMAGE`.

## Locust: comparaciones reproducibles

```sh
python3 -m venv .venv-load
.venv-load/bin/pip install -r loadtests/requirements.txt
export LOCUST_BIN="$PWD/.venv-load/bin/locust"
export USERS=20 SPAWN_RATE=5 DURATION=60s
scripts/run-load-comparison.sh "$KUBE_CONTEXT" http://127.0.0.1:18080 loadtests/results/single-run-1
scripts/run-load-comparison.sh "$KUBE_CONTEXT" http://127.0.0.1:18080 loadtests/results/single-run-2
```

El escenario es idéntico: 25% `GET /health` y 75% `GET /tournaments`, con espera entre 50 y 150 ms. No crea datos durante la carga. Guarda el conjunto de torneos antes/después para detectar cambios. Antes de comparar, usar la misma base y un volumen estable de datos.

Cada ejecución realiza:

1. **Sin Ingress:** port-forward al Service con una réplica. Kubernetes selecciona un Pod para ese túnel; esto no es una medición del balanceo de ClusterIP.
2. **Con Ingress y una réplica:** tráfico por Traefik/NodePort.
3. **Con Ingress y tres réplicas:** mismo tráfico; se exige observar más de un `X-Pod-Name`.

El HPA se retira temporalmente para fijar las réplicas de la comparación y se restaura incluso si el ensayo falla. Sus políticas no se evalúan durante este ensayo; para evaluar autoescalado, ejecutar Locust con el HPA activo por separado y registrar `kubectl get hpa -w`. Los recursos se muestrean antes, durante y después de cada corrida.

Para comparar nodos, repetir el procedimiento con `scripts/create-kind.sh multi`, kubeconfig `mundial-v3-multi.kubeconfig`, contexto `kind-mundial-v3-multi` y puerto 18081. Se crea un control plane y dos workers. Usar una máquina con recursos suficientes para los nodos y los componentes; un Docker de 4 GiB puede no sostener ambos clústeres y Compose simultáneamente. Conservar `nodes.json` y los archivos de Pods para distinguir nodos de réplicas. Un Ingress con 3 Pods en un único nodo no demuestra reparto entre nodos.

Cada carpeta contiene JSON de usuarios configurados/pico, peticiones, RPS, fallos, p50/p95/p99, respuestas por Pod y ventana UTC; CSV/HTML de Locust; estado/ubicación de Pods y muestras de `kubectl top`. `comparison.json` solo se escribe si se validan las condiciones del ensayo. Los resultados de carga están ignorados por Git para evitar subirlos accidentalmente; seleccionar explícitamente las evidencias verificadas que se quieran versionar.

## CI/CD

`.github/workflows/v3-ci.yml` ejecuta pruebas .NET10 y validación de manifiestos, construye la imagen y, en push a `main` o `max-v3`, publica en GHCR con el SHA del commit. Los pull requests construyen sin publicar. El despliegue es manual mediante `workflow_dispatch`, opción `deploy=true`, con el contexto exacto.

Requiere un runner `self-hosted, linux, mundialnube` que alcance el clúster, tenga `kubectl` y su kubeconfig configurados, y un environment `mundialnube-lab`. Configurar las reglas de aprobación del environment según el equipo. Los secretos y el acceso al registro deben existir antes. El workflow utiliza el Job de migración antes del rollout; no almacena credenciales en YAML ni despliega desde un push ordinario.

## Verificación de esta entrega

- Se puede validar sin clúster: `python scripts/validate-infrastructure.py` (requiere PyYAML), `kubectl kustomize`, `docker compose ... config --quiet`, compilación del escenario Locust y sintaxis de scripts.
- Las cifras de un ensayo solo son evidencia cuando provienen de una corrida real. No confundir la verificación de configuración o un servidor de prueba con un ensayo de Kubernetes.
- El informe de validación de la entrega indica por separado las pruebas efectivamente realizadas y las que dependen de un clúster/runner disponible.

Referencias: [Traefik Kubernetes Ingress](https://doc.traefik.io/traefik/reference/routing-configuration/kubernetes/ingress/), [Metrics Server: compatibilidad](https://github.com/kubernetes-sigs/metrics-server#compatibility-matrix), [Tempo monolítico](https://grafana.com/docs/tempo/latest/set-up-for-tracing/setup-tempo/deploy/locally/linux/), [Artemis Docker](https://artemis.apache.org/components/artemis/documentation/latest/docker.html), [Locust headless](https://docs.locust.io/en/stable/running-without-web-ui.html), [kind](https://kind.sigs.k8s.io/docs/user/quick-start/).
