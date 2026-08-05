\# Justificación de los límites del dominio



\## El sistema



InscripcionUCE resuelve la inscripción de asignaturas en un semestre.

Tres servicios de dominio y un gateway de borde.



| Servicio | Capacidad de negocio | Stack |

|---|---|---|

| academico | Qué asignaturas se ofertan y cuántos cupos quedan | .NET 8 + PostgreSQL |

| inscripciones | Inscribir a un estudiante y dejar constancia | .NET 8 + PostgreSQL + RabbitMQ |

| notificaciones | Avisarle al estudiante | Python 3.12 + FastAPI |

| gateway | Enrutar el tráfico externo (no es dominio, es borde) | .NET 8 + YARP |



\## Por qué estos límites



Los servicios se trazaron por \*\*capacidad de negocio\*\*, no por entidad ni por

capa técnica. La prueba que apliqué a cada uno fue la de los tres minutos:



1\. ¿Puedo desplegarlo solo? Sí: cada uno tiene su propio .csproj, su propia

&#x20;  imagen y su propia base de datos. Ninguno referencia a otro en compilación.

2\. ¿Soy el único dueño de sus datos? Sí: academico es el único que lee o

&#x20;  escribe la tabla de asignaturas; inscripciones el único que toca las suyas.

3\. ¿Puedo explicar su razón de existir sin palabras técnicas? Sí:

&#x20;  "ofertar asignaturas", "inscribir estudiantes", "avisar al estudiante".



\### El lenguaje del negocio los separa solo



Dirección académica habla de "la oferta del semestre". Registro habla de "las

inscripciones". Son dos áreas distintas de la universidad, con dos

vocabularios, y por eso son dos contextos.



\### Cambian por razones distintas (SRP a escala de servicio)



\- A \*\*academico\*\* le piden cambios cada semestre: qué asignaturas se ofertan,

&#x20; qué docente las imparte, cuántos cupos tiene cada sección.

\- A \*\*inscripciones\*\* le piden cambios por reglas de registro: prerrequisitos,

&#x20; tope de créditos por estudiante, ventanas de inscripción.



Si dos áreas del negocio piden cambios al mismo servicio por razones distintas,

el límite está mal trazado. Aquí no ocurre.



\## La división que consideré y descarté



\*\*Alternativa evaluada: fusionar inscripciones dentro de academico.\*\*



Es tentadora. Ambos hablan de asignaturas, ambos tocan el cupo, y la

inscripción no existe sin la asignatura. Un solo servicio sería más simple:

una transacción ACID resolvería cupo e inscripción de una vez, sin llamada de

red, sin consistencia eventual, sin outbox.



\*\*Por qué la descarté:\*\*



1\. \*\*Cadencia de cambio distinta.\*\* La oferta cambia cada semestre. El

&#x20;  histórico de inscripciones no cambia nunca: es un registro permanente. Lo

&#x20;  que cambia junto debe vivir junto, y aquí no cambian junto.



2\. \*\*Ciclo de vida de los datos opuesto.\*\* Una asignatura del semestre pasado

&#x20;  puede eliminarse de la oferta. La inscripción de un estudiante a esa

&#x20;  asignatura no puede desaparecer jamás: es su historial académico.



3\. \*\*Perfil de carga distinto.\*\* La consulta de oferta es masiva durante la

&#x20;  semana de inscripción y luego cae a casi cero. El histórico de inscripciones

&#x20;  se consulta todo el año para certificaciones y récords de notas. Escalarlos

&#x20;  por separado tiene sentido.



\*\*Qué acepté a cambio:\*\* una llamada de red donde antes había una llamada en

memoria, la pérdida de la transacción ACID que cruzaba ambos, y la necesidad

del patrón Outbox. Es un intercambio consciente, no un descuido.



\## Comunicación: por qué cada tipo



\### Sincrónico — inscripciones → academico (reservar cupo)



El cupo es un recurso escaso en disputa. Si dos estudiantes piden el último

asiento al mismo tiempo, solo uno puede quedárselo, y \*\*la respuesta tiene que

ser inmediata y correcta\*\* porque el estudiante está frente a la pantalla

esperando.



Resolverlo con un evento asincrónico significaría confirmarle la inscripción a

los dos y desinscribir a uno después. Inaceptable para el negocio.



Resiliencia aplicada: timeout, 3 reintentos con backoff exponencial y jitter,

y circuit breaker, vía `AddStandardResilienceHandler()` sobre el HttpClient

tipado. Si academico no responde, inscripciones devuelve 503 con un mensaje

claro en vez de colgarse.



\### Asincrónico — evento `inscripcion.confirmada`



Notificar es un efecto secundario. El estudiante ya tiene su cupo; el aviso

puede llegar un segundo o un minuto después sin consecuencia.



Lo decisivo: si el servicio de notificaciones está caído, \*\*la universidad no

puede dejar de inscribir estudiantes\*\*. Una llamada sincrónica ataría la

disponibilidad de la inscripción a la del notificador. El evento rompe esa

atadura: los mensajes se acumulan en la cola durable y se procesan cuando el

servicio vuelva.



\## Duplicación deliberada de datos



`inscripciones` guarda en su propia base de datos tres campos que "pertenecen"

a academico: `NombreAsignatura`, `Creditos` y `Docente`.



\*\*No es caché ni desnormalización por descuido. Es un hecho histórico.\*\*



Si el año que viene INF-022 pasa de 4 a 3 créditos, o cambia de docente, el

historial del semestre pasado no puede cambiar. El estudiante cursó esa

asignatura, con ese docente, por 4 créditos. Un JOIN contra la oferta vigente

reescribiría el pasado cada vez que se consultara.



Es la misma razón por la que una factura guarda el precio de venta y no lo

consulta del catálogo: el precio al que compraste no cambia porque mañana suba

la tarifa.



Además, técnicamente el JOIN es imposible: son dos bases de datos en dos

contenedores distintos. La infraestructura impone lo que el diseño decidió.



\## Poliglotismo: por qué Python en notificaciones



Notificaciones es E/S pura: escucha una cola y despacha avisos. Python con

FastAPI y aio-pika lo resuelve en menos de 100 líneas, y es el ecosistema

natural si mañana hay que sumar plantillas de correo, integración con

proveedores de SMS o mensajería instantánea.



Lo importante no es el lenguaje: es que \*\*estos dos servicios no comparten

absolutamente nada de código\*\*. No hay SDK, no hay librería común, no hay

paquete compartido. Lo único que comparten es la forma del JSON y el nombre de

la routing key (`inscripcion.confirmada`). Eso es un contrato.



Se mantuvieron dos stacks, no cinco: la diversidad tecnológica es sana hasta

que nadie puede cubrir la guardia de un servicio escrito en un lenguaje que no

conoce.

