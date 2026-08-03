# 📦 OrderFlow - Sistema Distribuido de Gestión de Pedidos e Inventario

**OrderFlow** es la solución propuesta para la prueba técnica de un sistema distribuido de gestión de pedidos e inventario implementado como **monorepo**.

La solución orquesta **2 microservicios en .NET 8**, un **cliente web en React**, un **broker de mensajería RabbitMQ** y **persistencia relacional en PostgreSQL**, levantándose completamente con **Docker Compose** y configurándose exclusivamente mediante **variables de entorno**.

El flujo principal es **asíncrono y orientado a eventos (EDA)**: la creación de un pedido dispara la reserva de stock en un servicio independiente, y el frontend recibe la actualización de estado en tiempo real.

---



## 🏛️ 1. Arquitectura del Sistema



### Diagrama de componentes

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              CAPA DE PRESENTACIÓN                               │
│  ┌──────────────────────┐                                                       │
│  │   React Frontend     │  Vite + Nginx (puerto 3000)                          │
│  │   Feature-Based UI   │                                                       │
│  └──────────┬───────────┘                                                       │
│             │ REST (HTTP)              WebSockets (SignalR)                     │
└─────────────┼──────────────────────────────┼────────────────────────────────────┘
              │                              │
              ▼                              ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           MICROSERVICIO: ORDERS API                             │
│  ┌──────────────────────────────────────────────────────────────────────────┐ │
│  │  Orders API (.NET 8) — puerto 5051                                       │ │
│  │  • POST /orders  → crea pedido (Pending) + publica OrderCreatedEvent     │ │
│  │  • GET  /orders  → consulta pedidos                                      │ │
│  │  • Hub  /hubs/orders → emite OrderUpdated vía SignalR                    │ │
│  │  • Consume: StockReservedEvent | StockRejectedEvent                      │ │
│  └──────────────────────────────────────────────────────────────────────────┘ │
└─────────────┬───────────────────────────────────────────────────┬───────────────┘
              │ Publish                              Consume       │
              ▼                                                    ▲
┌─────────────────────────────────────────────────────────────────────────────────┐
│                         BROKER: RabbitMQ (puerto 5672)                          │
│                                                                                 │
│   [order-created-event]  ──►  OrderCreated  ──►  Inventory Worker             │
│   [stock-reserved-event] ◄──  StockReserved  ◄──  (respuesta OK)              │
│   [stock-rejected-event] ◄──  StockRejected  ◄──  (respuesta KO)              │
│                                                                                 │
│   Management UI: http://localhost:15672                                         │
└─────────────┬───────────────────────────────────────────────────┬───────────────┘
              │ Consume                              Publish       │
              ▼                                                    │
┌─────────────────────────────────────────────────────────────────────────────────┐
│                        MICROSERVICIO: INVENTORY WORKER                          │
│  ┌──────────────────────────────────────────────────────────────────────────┐ │
│  │  Inventory Worker (.NET 8 Worker Service)                                │ │
│  │  • Consume: OrderCreatedEvent                                            │ │
│  │  • Valida stock + descuenta inventario (transacción atómica)             │ │
│  │  • Garantía de idempotencia vía tabla ProcessedEvents                    │ │
│  │  • Publica: StockReservedEvent | StockRejectedEvent                      │ │
│  └──────────────────────────────────────────────────────────────────────────┘ │
└─────────────┬───────────────────────────────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                    PERSISTENCIA: PostgreSQL 16 (puerto 5432)                    │
│                                                                                 │
│   Tabla Orders          → Orders API                                            │
│   Tabla Stocks          → Inventory Worker                                      │
│   Tabla ProcessedEvents → Inventory Worker (idempotencia)                       │
└─────────────────────────────────────────────────────────────────────────────────┘
```



### Separación de responsabilidades


| Servicio             | Rol                                                      | Responsabilidades                                                                                                                                                                                                          |
| -------------------- | -------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Orders API**       | Orquestador de pedidos / productor-consumidor de estados | Expone la API REST, persiste pedidos, publica `OrderCreatedEvent`, consume respuestas de inventario (`StockReserved` / `StockRejected`), actualiza el estado del pedido y notifica al frontend vía SignalR.                |
| **Inventory Worker** | Procesador de stock / consumidor-productor               | Consume `OrderCreatedEvent`, valida disponibilidad, descuenta stock en transacción atómica, garantiza idempotencia con `ProcessedEvents` y publica el resultado. **No expone HTTP** — opera exclusivamente por mensajería. |




### Flujo de un pedido (EDA)

```
1. Usuario crea pedido          →  POST /orders
2. Orders API persiste          →  Estado: Pending
3. Orders API publica           →  OrderCreatedEvent (RabbitMQ)
4. Inventory Worker consume     →  Reserva o rechaza stock
5. Worker publica               →  StockReservedEvent | StockRejectedEvent
6. Orders API consume respuesta →  Estado: Confirmed | Rejected
7. SignalR emite                →  OrderUpdated al frontend
```

---



## 🛠️ 2. Decisiones de Diseño y Stack Tecnológico



### .NET 8 & Clean Architecture

Ambos servicios backend siguen **Clean Architecture** con capas explícitas:

```
Domain/          → Entidades, enums, reglas de negocio puras
Application/     → DTOs, validadores, consumidores MassTransit, interfaces
Infrastructure/  → EF Core, repositorios, publicación de eventos
Presentation/    → Endpoints REST, Hubs SignalR, middleware (solo Orders API)
```

**Justificación:** separar dominio de infraestructura permite testear la lógica de negocio sin base de datos ni broker, facilita el mantenimiento y refleja las convenciones de equipos .NET enterprise.

---



### MassTransit + RabbitMQ

Se utiliza **MassTransit 8** como abstracción sobre **RabbitMQ** para implementar un patrón **Pub/Sub asíncrono**.

**Justificación:**

- Desacopla la creación de pedidos de la reserva de stock (resiliencia y escalabilidad independiente).
- MassTransit gestiona topología de exchanges/colas, reintentos y serialización.
- RabbitMQ es el estándar de facto para mensajería en arquitecturas distribuidas.

**Contratos de eventos duplicados por servicio:**

Cada microservicio define sus propios contratos en `Application/Contracts/Events/`. Para interoperar entre servicios con tipos en namespaces distintos se aplican:

- `SetEntityName(...)` — alinea el exchange de RabbitMQ entre publicador y consumidor.
- `[MessageUrn("OrderFlow:...")]` — unifica la identidad del mensaje en los headers de transporte.
- `UseRawJsonSerializer(AddTransportHeaders | CopyHeaders)` — serialización JSON interoperable entre servicios .NET independientes.


| Evento               | Dirección                     | Exchange               |
| -------------------- | ----------------------------- | ---------------------- |
| `OrderCreatedEvent`  | Orders API → Inventory Worker | `order-created-event`  |
| `StockReservedEvent` | Inventory Worker → Orders API | `stock-reserved-event` |
| `StockRejectedEvent` | Inventory Worker → Orders API | `stock-rejected-event` |


---



### Entity Framework Core + PostgreSQL

**PostgreSQL 16** como base de datos relacional compartida, accedida vía **EF Core 8** con provider Npgsql.

**Justificación:**

- Transacciones ACID para la reserva de stock (descuento + registro de evento procesado en una sola unidad atómica).
- PostgreSQL es robusto, open-source y ampliamente adoptado en producción.
- `EnsureCreated` / SQL idempotente al arranque simplifica el bootstrap en entornos Docker.

**Datos semilla de inventario:**


| SKU      | Stock inicial | Comportamiento esperado            |
| -------- | ------------- | ---------------------------------- |
| `ABC-01` | 10 unidades   | Reserva exitosa                    |
| `ABC-02` | 5 unidades    | Reserva exitosa hasta agotar stock |
| `ABC-03` | 0 unidades    | Rechazo inmediato                  |


---



### Idempotencia (`ProcessedEvents`)

El consumidor `OrderCreatedConsumer` garantiza **exactly-once semantics** a nivel de negocio:

1. **Pre-check:** consulta si `eventId` ya existe en `ProcessedEvents`.
2. **Transacción atómica:** re-verifica dentro de la transacción (double-check locking pattern).
3. **Registro:** inserta el `eventId` procesado junto con el resultado (`Reserved` / `Rejected`).
4. **Re-publicación segura:** si el evento ya fue procesado, re-emite la respuesta sin volver a descontar stock.

Esto protege contra **entregas duplicadas** de RabbitMQ (at-least-once delivery), evitando doble descuento de inventario.

---



### React (Vite) + Feature-Based Architecture

El frontend usa **React 19 + Vite 8**, organizado por **features** en lugar de por tipo de archivo:

```
src/features/orders/
├── api/          → ordersApi.js
├── components/   → OrderForm, OrderList, OrderStatusBadge
├── hooks/        → useOrders.js
└── pages/        → OrdersPage.jsx
```

**Justificación:** la arquitectura por features agrupa todo lo relacionado a pedidos en un solo módulo cohesivo, facilitando el escalado del frontend cuando se añadan nuevas capacidades (ej. reportes, admin de stock).

---



### SignalR (WebSockets)

Tras consumir `StockReservedEvent` o `StockRejectedEvent`, la Orders API emite `OrderUpdated` al hub `/hubs/orders`.

El hook `useOrders` combina:

- **REST** como carga inicial y fallback.
- **SignalR** para actualizaciones en tiempo real.

**Justificación:** mejora la UX al reflejar el cambio de estado (`Pending` → `Confirmed` / `Rejected`) de forma instantánea.

---



## ⚡ 3. Cómo Ejecutar el Proyecto



### Requisitos previos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) en ejecución.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (opcional — solo necesario para tests locales fuera de Docker).



### Configuración de variables de entorno



# 1. Copiar plantilla de variables en la raíz del monorepo

```bash
cp .env.example .env
```



### Levantar todo el sistema con un solo comando

```bash
docker compose up
```



### URLs de acceso


| Servicio                | URL                                                            | Descripción                                          |
| ----------------------- | -------------------------------------------------------------- | ---------------------------------------------------- |
| **Frontend**            | [http://localhost:3000](http://localhost:3000)                 | Interfaz web de pedidos                              |
| **Orders API**          | [http://localhost:5051](http://localhost:5051)                 | API REST                                             |
| **Swagger UI**          | [http://localhost:5051/swagger](http://localhost:5051/swagger) | Documentación interactiva de la API                  |
| **RabbitMQ Management** | [http://localhost:15672](http://localhost:15672)               | Dashboard del broker (`guest` / `guest` por defecto) |




### Verificar el flujo completo

```bash
# Crear un pedido
curl -X POST http://localhost:5051/orders \
  -H "Content-Type: application/json" \
  -d '{"clienteNombre": "Juan Pérez", "sku": "ABC-01", "cantidad": 2}'

# Consultar estado (esperar 2-3 segundos para procesamiento asíncrono)
curl http://localhost:5051/orders
```

Estado esperado: `"estado": "Confirmed"` (stock disponible) o `"Rejected"` (sin stock).

### Ejecutar tests unitarios

```bash
dotnet test
```

Cobertura principal:

- Validación de entrada (`FluentValidation`) en Orders API.
- Consumidor de inventario: reserva, rechazo e idempotencia.
- Confirmación/rechazo de pedidos vía consumidores de respuesta.

---



## 📁 4. Estructura del Monorepo

```
OrderFlow/
├── docker-compose.yml              # Orquestación de todos los servicios
├── .env.example                    # Plantilla de variables de entorno
├── OrderFlow.sln                   # Solución .NET
│
├── src/
│   ├── orders-api/                 # Microservicio de pedidos (.NET 8 Web API)
│   │   ├── Domain/
│   │   ├── Application/
│   │   ├── Infrastructure/
│   │   └── Presentation/
│   │
│   ├── inventory-worker/           # Worker de inventario (.NET 8)
│   │   ├── Domain/
│   │   ├── Application/
│   │   └── Infrastructure/
│   │
│   └── frontend/                   # Cliente React + Vite + Nginx
│       └── src/features/orders/
│
└── tests/
    └── OrderFlow.Tests/            # Tests de API, worker y consumidores
```

---



## 🛡️ 5. Análisis de Resiliencia y Manejo de Fallos

Esta sección responde explícitamente a escenarios de falla distribuida, un requisito clave en sistemas orientados a eventos.

### Escenarios críticos de falla distribuida



#### ¿Qué pasa si `InventoryWorker` no responde o está caído?

El evento `OrderCreatedEvent` **permanece guardado de forma persistente en la cola de RabbitMQ**. El pedido se mantiene en estado `Pending`. Tan pronto como el servicio `InventoryWorker` se restablece, consume los eventos pendientes y actualiza las órdenes en segundo plano.

> **Garantía:** Consistencia Eventual. El sistema no pierde pedidos; solo difiere la confirmación o rechazo hasta que el worker vuelva a estar disponible.

```
Orders API ──► RabbitMQ (cola persistente) ──X── Inventory Worker (caído)
                     │
                     └── Mensajes retenidos hasta recovery del worker
```



#### ¿Qué pasa si el broker de mensajería (RabbitMQ) está caído cuando `OrdersApi` intenta publicar?

La API **captura la excepción de conexión** y retorna una **respuesta HTTP controlada** (ej. `503 Service Unavailable` o un mensaje claro de error vía `ProblemDetails` RFC 7807) para no dejar peticiones colgadas ni corromper el estado.

El `GlobalExceptionHandlerMiddleware` centraliza el manejo de errores no controlados, evitando respuestas inconsistentes al cliente.

> **Nota de diseño:** En la implementación actual, un fallo de publicación durante `POST /orders` se propaga como error HTTP controlado. Para producción, el patrón **Transactional Outbox** (ver sección 6) eliminaría el riesgo de inconsistencia entre persistencia del pedido y publicación del evento.

---



### Matriz de escenarios adicionales


| Escenario                            | Comportamiento                                                                                                  |
| ------------------------------------ | --------------------------------------------------------------------------------------------------------------- |
| **Stock insuficiente**               | Worker publica `StockRejectedEvent` → pedido pasa a `Rejected`.                                                 |
| **SKU inexistente**                  | Mismo flujo de rechazo con motivo descriptivo.                                                                  |
| **Entrega duplicada del evento**     | Tabla `ProcessedEvents` evita doble descuento; re-publica la respuesta original.                                |
| **Worker caído temporalmente**       | RabbitMQ retiene mensajes en cola; al reiniciar, el worker los procesa.                                         |
| **RabbitMQ caído al publicar**       | Excepción capturada → respuesta HTTP controlada al cliente (sin timeout silencioso).                            |
| **Error no controlado en API**       | `GlobalExceptionHandlerMiddleware` retorna `ProblemDetails` (RFC 7807).                                         |
| **Frontend desconectado de SignalR** | Reconexión automática (`withAutomaticReconnect`); REST como fallback.                                           |
| **Configuración faltante**           | Los servicios .NET fallan al arrancar con mensaje explícito si falta connection string o credenciales RabbitMQ. |




### Estados del pedido

```
Pending ──► Confirmed   (stock reservado exitosamente)
        └──► Rejected   (stock insuficiente o SKU no encontrado)
```

---



## ⚖️ 6. Trade-offs Asumidos


| Decisión                              | Beneficio                                                              | Costo / Limitación                                                                                       |
| ------------------------------------- | ---------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| **Base de datos compartida**          | Simplicidad operativa en prueba técnica; un solo PostgreSQL en Docker. | En producción, cada servicio debería tener su propia BD (Database per Service).                          |
| **Contratos duplicados por servicio** | Autonomía total de cada microservicio; sin librería compartida.        | Requiere sincronización manual de contratos + configuración MassTransit (`MessageUrn`, `SetEntityName`). |
| `EnsureCreated` **vs Migraciones**    | Bootstrap automático sin pasos manuales en Docker.                     | No es ideal para evolución de esquema en producción (usar EF Migrations).                                |
| **At-least-once delivery**            | Simplicidad del broker; estándar en RabbitMQ.                          | Requiere idempotencia en el consumidor (implementada vía `ProcessedEvents`).                             |
| **Sin API Gateway / Service Mesh**    | Menor complejidad infraestructural.                                    | En producción se añadiría un gateway (YARP, Kong, etc.) para enrutamiento y auth.                        |
| **Sin autenticación/autorización**    | Foco en arquitectura distribuida y flujo de eventos.                   | En producción se integraría JWT/OAuth2 y políticas de acceso.                                            |
| **SignalR sin backplane Redis**       | Suficiente para instancia única de API.                                | Con múltiples réplicas de Orders API se requeriría Redis backplane.                                      |


---



## 🚀 7. ¿Qué haría distinto con más tiempo?

Con más tiempo  implementaría las siguientes mejoras arquitectónicas:

### Transactional Outbox Pattern

Para asegurar **atomicidad completa** entre el guardado del pedido en la BD y la publicación del evento en RabbitMQ, evitando pérdidas si el broker cae a mitad de transacción.

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────┐
│  Save Order │ ──► │  Outbox Table    │ ──► │  RabbitMQ   │
│  + Outbox   │     │  (mismo commit)  │     │  (async)    │
└─────────────┘     └──────────────────┘     └─────────────┘
                           ▲
                    Background Processor
                    (lee outbox y publica)
```



### Resiliencia con Polly

Implementación de políticas de **Retry con Exponential Backoff** y **Circuit Breaker** en consumidores MassTransit y llamadas a base de datos, evitando cascadas de fallos y protegiendo servicios downstream.

### Observabilidad y Monitoreo

Integración de **OpenTelemetry**, **Serilog** (structured logging) y dashboards en **Grafana / Jaeger** para trazabilidad distribuida con **Correlation IDs** propagados desde `POST /orders` hasta el descuento de stock en el worker.

### Seguridad / Auth

Implementación de autenticación y autorización mediante **OAuth2 / JWT** (Keycloak / Duende IdentityServer), **Rate Limiting** en la API y validación de scopes por endpoint.

### Paginación en `GET /orders`

El endpoint actual `GET /orders` retorna **todos los pedidos sin límite**, lo cual es aceptable para la prueba técnica pero no escala en producción. Implementaría paginación basada en query params:

```
GET /orders?page=1&pageSize=20&sortBy=creadoEn&sortDir=desc
```

**Backend:**

- DTO de respuesta paginada (`PagedResult<OrderResponse>`) con metadatos: `items`, `totalCount`, `page`, `pageSize`, `totalPages`.
- Consulta EF Core con `Skip` / `Take` e índice en `CreadoEn` para ordenamiento eficiente.
- Opcional: paginación por **cursor** (`?cursor=<lastId>`) para listas de alto volumen con mejor rendimiento que offset.

**Frontend:**

- Adaptar `useOrders` y `OrderList` para cargar páginas bajo demanda (infinite scroll o controles prev/next).
- Mantener SignalR para actualizaciones en tiempo real solo sobre la página visible o invalidar caché al recibir `OrderUpdated`.

---



## 🔧 8. Configuración por Variables de Entorno

Toda configuración sensible se externaliza. **No hay secretos ni connection strings en el código fuente.**


| Variable            | Descripción                              | Consumidor                               |
| ------------------- | ---------------------------------------- | ---------------------------------------- |
| `POSTGRES_DB`       | Nombre de la base de datos               | Docker Compose → PostgreSQL, API, Worker |
| `POSTGRES_USER`     | Usuario de PostgreSQL                    | Docker Compose → PostgreSQL, API, Worker |
| `POSTGRES_PASSWORD` | Contraseña de PostgreSQL                 | Docker Compose → PostgreSQL, API, Worker |
| `POSTGRES_PORT`     | Puerto expuesto de PostgreSQL            | Docker Compose                           |
| `RABBITMQ_USER`     | Usuario del broker                       | Docker Compose → RabbitMQ, API, Worker   |
| `RABBITMQ_PASS`     | Contraseña del broker                    | Docker Compose → RabbitMQ, API, Worker   |
| `RABBITMQ_PORT`     | Puerto AMQP                              | Docker Compose                           |
| `RABBITMQ_UI_PORT`  | Puerto Management UI                     | Docker Compose                           |
| `ORDERS_API_PORT`   | Puerto expuesto de la API                | Docker Compose                           |
| `FRONTEND_PORT`     | Puerto expuesto del frontend             | Docker Compose                           |
| `VITE_API_URL`      | URL de la API para el build del frontend | Docker Compose → Frontend                |


Para desarrollo local fuera de Docker, los servicios .NET leen de `appsettings.Development.json` (gitignored; plantilla en `.example`).

---



## 📡 9. API REST — Referencia Rápida


| Método | Endpoint  | Descripción              |
| ------ | --------- | ------------------------ |
| `POST` | `/orders` | Crear pedido             |
| `GET`  | `/orders` | Listar todos los pedidos |


**Body de creación (**`POST /orders`**):**

```json
{
  "clienteNombre": "Juan Pérez",
  "sku": "ABC-01",
  "cantidad": 2
}
```

**Validaciones:**

- `clienteNombre`: obligatorio.
- `sku`: obligatorio y debe existir en el catálogo de productos (`Stocks`).
- `cantidad`: entero entre 1 y 100.

---

