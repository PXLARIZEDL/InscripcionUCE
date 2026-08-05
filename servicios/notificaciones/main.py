import asyncio
import json
import logging
import os
from collections import deque
from contextlib import asynccontextmanager

import aio_pika
from fastapi import FastAPI

logging.basicConfig(level=logging.INFO,
                    format="%(asctime)s [notificaciones] %(message)s")
log = logging.getLogger("notificaciones")

RABBIT_URI = os.getenv("RABBITMQ_URI", "amqp://uce:uce123@localhost:5672/")
EXCHANGE = "uce.eventos"
COLA = "notificaciones.inscripcion-confirmada"
ROUTING_KEY = "inscripcion.confirmada"

# Bandeja en memoria: solo para ver el resultado en la demo.
enviadas: deque[dict] = deque(maxlen=50)
vistos: set[str] = set()          # idempotencia por MessageId


async def manejar(mensaje: aio_pika.abc.AbstractIncomingMessage) -> None:
    async with mensaje.process():
        if mensaje.message_id and mensaje.message_id in vistos:
            log.info("Mensaje duplicado ignorado: %s", mensaje.message_id)
            return

        evento = json.loads(mensaje.body)

        texto = (f"Hola {evento['matricula']}, quedaste inscrito en "
                 f"{evento['nombreAsignatura']} ({evento['creditos']} créditos) "
                 f"con {evento['docente']}.")

        log.info("ENVIANDO -> %s", texto)
        enviadas.appendleft({
            "inscripcionId": evento["inscripcionId"],
            "mensaje": texto
        })

        if mensaje.message_id:
            vistos.add(mensaje.message_id)


async def consumir() -> None:
    while True:
        try:
            conexion = await aio_pika.connect_robust(RABBIT_URI)
            canal = await conexion.channel()
            await canal.set_qos(prefetch_count=10)

            exchange = await canal.declare_exchange(
                EXCHANGE, aio_pika.ExchangeType.TOPIC, durable=True)

            cola = await canal.declare_queue(COLA, durable=True)
            await cola.bind(exchange, routing_key=ROUTING_KEY)

            log.info("Escuchando '%s' en la cola '%s'", ROUTING_KEY, COLA)
            await cola.consume(manejar)
            await asyncio.Future()

        except asyncio.CancelledError:
            raise
        except Exception as e:
            log.warning("RabbitMQ no disponible (%s). Reintento en 5 s.", e)
            await asyncio.sleep(5)


@asynccontextmanager
async def lifespan(app: FastAPI):
    tarea = asyncio.create_task(consumir())
    yield
    tarea.cancel()


app = FastAPI(title="Notificaciones", version="1.0", lifespan=lifespan)


@app.get("/health")
async def health():
    return {"status": "ok", "enviadas": len(enviadas)}


@app.get("/notificaciones")
async def listar():
    return list(enviadas)