\# InscripcionUCE



Sistema de inscripción de asignaturas en microservicios.

Proyecto evaluable — Ingeniería de Software, UCE Punta Cana.



3 servicios de dominio + gateway · 2 runtimes · 2 bases de datos · RabbitMQ



\## Arquitectura



```

&#x20;                        ┌──────────────┐

&#x20;      cliente ─────────>│   GATEWAY    │  :8080  (único puerto publicado)

&#x20;                        │  .NET + YARP │

&#x20;                        └──────┬───────┘

&#x20;                    ┌──────────┼──────────┐

&#x20;                    v          v          v

&#x20;             ┌───────────┐ ┌──────────┐ ┌────────────────┐

&#x20;             │ ACADEMICO │ │INSCRIPC. │ │ NOTIFICACIONES │

&#x20;             │  .NET 8   │ │ .NET 8   │ │ Python/FastAPI │

&#x20;             └─────┬─────┘ └────┬─────┘ └───────┬────────┘

&#x20;                   │            │               │

&#x20;             ┌─────v─────┐ ┌────v─────┐         │

&#x20;             │academico- │ │inscripc- │         │

&#x20;             │    db     │ │   db     │         │

&#x20;             │ Postgres  │ │ Postgres │         │

&#x20;             └───────────┘ └────┬─────┘         │

&#x20;                                │               │

&#x20;                           publica          consume

&#x20;                                │               │

&#x20;                                v               v

&#x20;                          ┌─────────────────────────┐

&#x20;                          │  RabbitMQ  uce.eventos  │

&#x20;                          └─────────────────────────┘



&#x20; ───>  HTTP sincrónico (con timeout, reintento y circuit breaker)

&#x20; - - >  evento asincrónico

```



Inscripciones llama a Academico de forma \*\*sincrónica\*\* para reservar cupo,

y publica un evento \*\*asincrónico\*\* que Notificaciones consume.



\## Catálogo de eventos



\### `inscripcion.confirmada`



| | |

|---|---|

| Exchange | `uce.eventos` (topic, durable) |

| Productor | inscripciones |

| Consumidores | notificaciones (cola `notificaciones.inscripcion-confirmada`) |

| Entrega | at-least-once, persistente, con `MessageId` para idempotencia |



```json

{

&#x20; "inscripcionId": "9c1f0e3a-5b2d-4f77-9a10-2b8e6d4c1a55",

&#x20; "matricula": "2021-0345",

&#x20; "nombreAsignatura": "Programación Avanzada",

&#x20; "creditos": 4,

&#x20; "docente": "Richard Jiménez",

&#x20; "ocurridoEn": "2026-08-05T18:42:11.204Z"

}

```



El JSON se serializa con `JsonSerializerDefaults.Web` (camelCase) porque el

consumidor está escrito en Python y no comparte tipos con el productor.



\## Ejecución



Requisitos: Docker Desktop con Compose v2 y al menos 4 GB asignados.



```bash

docker compose up --build -d

docker compose ps          # todos deben decir running / healthy

```



Consola de RabbitMQ: http://localhost:15672 (usuario `uce`, clave `uce123`)



\### Recorrer el flujo



```bash

\# 1. Ver la oferta de asignaturas

curl -s http://localhost:8080/api/asignaturas



\# 2. Inscribir un estudiante

curl -s -X POST http://localhost:8080/api/inscripciones \\

&#x20; -H "Content-Type: application/json" \\

&#x20; -d '{"matricula":"2021-0345","codigoAsignatura":"INF-033"}'



\# 3. Ver que Notificaciones reaccionó (otro lenguaje, otro contenedor)

curl -s http://localhost:8080/api/notificaciones



\# 4. Confirmar que el cupo bajó

curl -s http://localhost:8080/api/asignaturas/INF-033



\# 5. Intentar una asignatura llena → 409 Conflict

curl -s -X POST http://localhost:8080/api/inscripciones \\

&#x20; -H "Content-Type: application/json" \\

&#x20; -d '{"matricula":"2021-0345","codigoAsignatura":"INF-041"}'

```



\### Detener



```bash

docker compose down       # detiene y borra los contenedores

docker compose down -v    # ...y también los datos

```



\## Decisiones de diseño



\- \*\*Base de datos por servicio.\*\* Dos contenedores de PostgreSQL separados,

&#x20; no dos esquemas. Hace físicamente imposible el JOIN entre servicios.

\- \*\*Duplicación deliberada.\*\* Inscripciones congela nombre, créditos y docente

&#x20; de la asignatura. Es historial académico, no caché. Detalle en

&#x20; `docs/limites-del-dominio.md`.

\- \*\*Patrón Outbox.\*\* El evento se guarda en la misma transacción que la

&#x20; inscripción y un `BackgroundService` lo publica después. Sin esto, si el

&#x20; proceso muere entre guardar y publicar, el evento se pierde para siempre.

\- \*\*Gateway tonto.\*\* Solo enruta. Sin lógica de negocio, para que no se

&#x20; convierta en el nuevo monolito.

\- \*\*Configuración por entorno.\*\* Cero credenciales en el código; todo entra

&#x20; por variables de entorno (Twelve-Factor, principio III).



\## Documentación



| Archivo | Contenido |

|---|---|

| `docs/limites-del-dominio.md` | Justificación de límites, comunicación y duplicación |

| `docs/demo-resiliencia.md` | Guion de la demostración |

| `docs/bitacora.md` | Errores encontrados y cómo se resolvieron |





