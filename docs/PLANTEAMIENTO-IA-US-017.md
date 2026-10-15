# Planteamiento formal de inteligencia artificial — US-017

**Proyecto:** EventosPeruIA
**Responsabilidad:** Persona 5, asistente IA y recuperación aumentada por generación (RAG)
**Fecha:** 15 de octubre de 2026
**Estado:** propuesta implementada en rama local, pendiente de validación con servicios reales y aprobación para subir.

## 1. Problema y objetivo

El usuario necesita descubrir y comparar eventos mediante preguntas en español. Una respuesta basada únicamente en el conocimiento del modelo puede inventar eventos, precios o cupos. US-017 emplea RAG para recuperar eventos del catálogo y redactar una respuesta sustentada en datos actuales.

El objetivo es recomendar solamente eventos publicados, futuros y con entradas disponibles, respetando filtros reconocidos de categoría, ubicación y presupuesto. La respuesta incluye texto en español y una lista estructurada para el cliente.

## 2. Alcance y decisiones

No se entrena ni ajusta un modelo. Ollama ejecuta el modelo conversacional (`llama3.2`, configurable) y el modelo de embeddings (`nomic-embed-text`, configurable). Qdrant almacena los vectores; SQL Server es la fuente de verdad de eventos, precios, estados y entradas. No se envían contraseñas, correos, identidades de compradores ni códigos QR al modelo.

La búsqueda reconoce las categorías y ubicaciones ya implementadas. Las expresiones de precio reconocidas son `hasta`, `máximo`, `no más de`, `desde`, `entre A y B`, `menos de` y `menor de`, con decimales mediante punto o coma y símbolo opcional S/. Los límites de hasta/máximo/entre/desde son inclusivos; menos/menor son estrictos. Consultas con filtros contradictorios no producen recomendaciones. No existe un analizador general de fechas, negaciones, múltiples ciudades o toda la gramática española.

## 3. Flujo de consulta

1. Validar una consulta no vacía de hasta 2000 caracteres; rechazar solicitudes HTTP inválidas con 400.
2. Consultar eventos publicados y futuros en SQL; calcular sus entradas disponibles.
3. Aplicar los filtros exactos reconocidos antes de limitar la recuperación semántica.
4. Obtener el embedding de la pregunta y recuperar hasta diez IDs en Qdrant, restringidos a candidatos elegibles.
5. Volver a leer SQL después de las llamadas externas, recalcular cupos y revalidar filtros; mantener el orden semántico, eliminar duplicados y conservar hasta cinco eventos.
6. Construir el contexto con nombre, descripción, categoría, fecha, ubicación, precio, estado y entradas disponibles. Las instrucciones se envían como mensaje de sistema; la consulta y el catálogo como datos de usuario.
7. Generar la respuesta y devolver los mismos eventos estructurados, con `EntradasDisponibles` calculado.

Si no hay candidatos o resultados, se responde sin invocar al modelo conversacional. Si falla el proveedor HTTP/gRPC o expira su operación, los endpoints responden 503 con mensaje genérico, sin revelar detalles internos. Las instrucciones reducen la posibilidad de alucinación e inyección, pero no constituyen una garantía matemática sobre el texto generado; el cliente debe usar los eventos estructurados como enlaces y datos autorizados.

## 4. Disponibilidad e integración imprescindible

La regla única es:

`disponibles = máximo(0, aforoTotal − suma(Cantidad de tickets no anulados))`

Los tickets utilizados siguen ocupando una entrada vendida. Los anulados se excluyen. Se suman cantidades, no filas de compras. Una compra conserva el aforo total y valida cupos dentro de su transacción serializable existente. El detalle del evento también suma cantidades. Estas son las únicas integraciones funcionales ajenas al módulo de IA y son necesarias para evitar descontar dos veces.

### Datos históricos: requisito previo al despliegue

La versión anterior reducía `Aforo` en cada compra, mientras el detalle lo interpretaba como capacidad total. Antes de usar esta versión sobre una base con compras existentes, el responsable de datos debe conciliar la capacidad total con la fuente del organizador. Si ningún aforo se editó después de vender ni se borraron compras, la capacidad anterior se puede reconstruir como `Aforo actual + suma de todas las cantidades compradas`; aquí se incluyen anuladas porque la compra anterior también las había descontado y no existe devolución de cupos en el código revisado.

No se automatiza esa reconstrucción: los datos no permiten distinguir una capacidad corregida manualmente de un saldo de cupos. `revision-aforos.sql` genera una propuesta de solo lectura. Revisar con respaldo y corregir los aforos verificados antes del despliegue, con compras detenidas durante el cambio. No se ha modificado ninguna base real ni aplicado migraciones.

## 5. Sincronización y limpieza

Un servicio en segundo plano inicia la reconciliación al arrancar la API y la repite cada 60 segundos por defecto. El intervalo `AI:ReindexIntervalSeconds` se limita a 10–3600 segundos. Cada ciclo relee SQL, elimina puntos que ya no corresponden a eventos elegibles y vuelve a generar/actualizar los embeddings de los eventos activos. La limpieza recorre Qdrant por páginas de 256 IDs, incluyendo el caso de catálogo vacío y eventos eliminados de SQL.

Las modificaciones de nombre, descripción, categoría, ubicación, precio y fecha llegan al índice en el siguiente ciclo exitoso. Cancelaciones, borradores, eventos pasados y agotados se eliminan. Si falla la generación después de limpiar, el ciclo posterior reintenta. El trabajador registra el error sin detener el servidor. Un semáforo serializa reindexaciones manuales y automáticas dentro del mismo proceso.

La consistencia del índice es eventual: una publicación nueva puede tardar hasta el siguiente ciclo exitoso en aparecer. La elegibilidad se comprueba con SQL durante cada chat, por lo que un vector obsoleto no autoriza recomendar un evento cancelado o agotado. Los cupos son una instantánea informativa; la compra los vuelve a validar.

Para este prototipo se regenera todo el catálogo cada ciclo. Esto aumenta el coste en catálogos grandes; una evolución debe persistir huellas/versiones de contenido, usar una cola transaccional y coordinar varias instancias mediante un bloqueo distribuido. Esta propuesta presupone una instancia del trabajador y una colección exclusiva de eventos; la limpieza no debe apuntar a una colección compartida con otros documentos.

## 6. Seguridad de la reindexación

`POST /api/ai/reindex` exige JWT válido y una política `AIReindex` con IDs de operadores explícitamente autorizados en configuración de servidor. Sin lista, ningún usuario queda autorizado. Una solicitud anónima recibe 401; una identidad no incluida recibe 403. No se confía en el rol elegido al registrarse, porque el registro actual acepta el rol enviado por el cliente.

Ejemplo de configuración de servidor (sustituir por IDs verificados, no por IDs de ejemplo en producción):

```json
{
  "AI": {
    "ReindexIntervalSeconds": 60,
    "ReindexOperatorIds": ["ID_VERIFICADO"]
  }
}
```

También puede usarse `AI__ReindexOperatorIds__0`. Mantener la clave JWT fuera del repositorio y reemplazar el valor de desarrollo en el entorno real. Corregir el modelo general de autorización/registro queda fuera de esta historia. El chat conserva el acceso público existente.

## 7. Criterios de aceptación y evaluación

- Las recomendaciones estructuradas contienen únicamente eventos elegibles recuperados.
- La cantidad disponible respeta compras de varias entradas, utilizadas, anuladas y sobreocupación histórica.
- Reindexar refleja ediciones y limpia cancelados, pasados, borradores, agotados y eliminados, incluso con catálogo vacío.
- Los límites inclusivos no excluyen un evento cuyo precio coincide con el presupuesto.
- Consultas vacías o excesivas no consumen llamadas a proveedores.
- La reindexación no es ejecutable por visitantes ni por roles autodeclarados.
- Los errores transitorios del proveedor reciben un mensaje controlado y permiten reintentos.

Las pruebas automatizadas usan SQLite relacional para la lógica de catálogo y dobles de los proveedores de IA/Qdrant. Las pruebas HTTP ejecutan la API en memoria y validan el JWT real, la política y los códigos HTTP. No equivalen a una prueba de Ollama, Qdrant o SQL Server desplegados.

Para evaluar la calidad del modelo, preparar un catálogo controlado y preguntas de descubrimiento, comparación, presupuesto, falta de resultados e intentos de cambiar instrucciones. Registrar precisión de eventos elegibles, exactitud de precio/cupos, fidelidad al contexto, tiempo de respuesta y fallos. La meta determinista de elegibilidad es 100%; no se declara una precisión semántica ni una latencia medida sin evaluación con modelos reales.

## 8. Ejecución y límites de validación

```powershell
dotnet restore EventosPeruIA.Tests/EventosPeruIA.Tests.csproj
dotnet build EventosPeruIA.Api/EventosPeruIA.Api.csproj --no-restore
dotnet test EventosPeruIA.Tests/EventosPeruIA.Tests.csproj --no-restore
```

Antes del despliegue, verificar SQL Server, Ollama y Qdrant; descargar los modelos configurados; conciliar los aforos históricos; configurar operadores; ejecutar un ciclo con más de 256 puntos para comprobar paginación y limpieza; cancelar/agotar un evento y confirmar que deja de recomendarse; publicar y editar otro y comprobar el siguiente ciclo. El modelo de embeddings debe conservar la dimensión de la colección; cambiarlo requiere una colección nueva y reindexar.

Los resultados locales concretos se registran en `RESULTADOS-US-017.md`. La rama no se sube ni se crea un PR sin confirmación del usuario.
