# Resultados y revisión de US-017 IA/RAG

Fecha: 15 de octubre de 2026. Rama local: `fix/us-017-ia-rag`.
Base: `main`, commit `71285660d65216ec8f6ea207ec01a9150eddc43c`.

## Cambios revisados

- Reconciliación automática al iniciar y cada 60 segundos configurables; reintento después de fallos y exclusión mutua dentro del proceso.
- Limpieza paginada de vectores cancelados, pasados, borradores, eliminados o agotados; incluye catálogo vacío.
- Cupos calculados por suma de cantidades de tickets no anulados; los utilizados siguen contando. La respuesta estructurada incluye `EntradasDisponibles` y el contexto usa ese mismo dato.
- Candidatos filtrados antes de la búsqueda semántica y datos SQL revalidados después de llamadas externas para detectar compras, cambios de precio o cancelaciones durante la recuperación.
- `POST /api/ai/reindex` exige JWT y una identidad incluida en la lista de operadores del servidor, con denegación por defecto.
- Precios inclusivos para hasta/máximo/no más de/desde/entre y estrictos para menos/menor de. Consultas vacías y de más de 2000 caracteres se rechazan; fallos transitorios de proveedores devuelven 503.
- Mensajes de sistema separados de datos del catálogo y consulta.
- Integración imprescindible: la compra conserva el aforo total y valida la disponibilidad; el detalle suma cantidades en vez de contar compras. La solución incluye el proyecto de pruebas y se ignoran resultados generados.

Se revisaron manualmente los archivos nuevos y el diff de todos los archivos modificados, el alcance, las condiciones de precio, la autorización, la concurrencia local del indexador y la coherencia entre compra, detalle y chat. Durante la revisión se añadió la segunda lectura de SQL para evitar que la latencia de proveedores dejara pasar cambios recientes. `git diff --check` terminó sin errores de espacios. `main` y `origin/main` mantienen el commit base; no se ha hecho push ni creado PR.

## Validación ejecutada

| Comprobación | Resultado |
|---|---|
| Restauración de dependencias | Correcta |
| Compilación de la API | Correcta, 0 errores |
| Pruebas de la solución | 40 aprobadas, 0 fallidas, 0 omitidas |
| Duración informada por el ejecutor | 10 segundos (incluye reintento del trabajador) |
| Revisión de espacios del diff | Correcta |

Comandos finales:

```powershell
dotnet restore EventosPeruIA.Tests/EventosPeruIA.Tests.csproj
dotnet build EventosPeruIA.Api/EventosPeruIA.Api.csproj --no-restore --nologo
dotnet test EventosPeruIA.Api/EventosPeruIA.Api.slnx --no-restore --nologo --logger 'trx;LogFileName=us-017.trx'
```

El resultado detallado permanece local en `EventosPeruIA.Tests/TestResults/us-017.trx` y se excluye del commit. El ejecutor de pruebas quedó detenido dentro del aislamiento; al ejecutarlo con el permiso correspondiente fuera del aislamiento, completó todas las pruebas.

## Cobertura

- Cantidades múltiples, tickets utilizados y anulados, y cupos mínimos de cero.
- Reindexación tras edición; limpieza de cancelados, pasados, borradores, eliminados, agotados y catálogo vacío.
- Fallo de embeddings seguido de recuperación, y limpieza previa al fallo.
- Límites exactos y decimales de precios, rangos contradictorios y límites inclusivos/estrictos.
- Duplicados e IDs obsoletos; candidatos elegibles y contexto del modelo.
- Compras que conservan capacidad, rechazo de sobreventa y estados no disponibles, coherencia del detalle con las cantidades.
- Compras, cancelaciones y cambios de precio ocurridos durante la llamada de embeddings.
- Consultas vacías, excesivas y normalización; detección de IA y ausencia de falso filtro de arte en “compartir”.
- HTTP 401 para anónimos, 403 para identidades no permitidas, 200 para operador permitido, denegación con lista vacía y rechazo de rol autodeclarado.
- HTTP 400 de consultas inválidas y 503 de fallos de proveedor sin detalles internos.
- Inicio automático y reintento real del trabajador tras un fallo simulado.

## Límites y condiciones antes de desplegar

1. Las pruebas de catálogo usan SQLite y dobles de Qdrant/Ollama. No se conectaron los servicios reales ni SQL Server. La paginación del cliente Qdrant se revisó y compiló, pero debe validarse con una colección real de más de 256 puntos. No se declara una calidad semántica medida del modelo.
2. La versión anterior disminuía `Aforo` al vender. Si existen datos históricos, conciliar la capacidad total antes del cambio; no es seguro reconstruirla automáticamente si hubo ediciones de aforo o compras borradas. `revision-aforos.sql` es de solo lectura y el planteamiento detalla la transición. Ninguna base fue modificada.
3. Configurar `AI:ReindexOperatorIds` con identidades verificadas. En un entorno real, sustituir la clave JWT de desarrollo por una secreta externa al repositorio. No se cambió el registro general de usuarios.
4. La sincronización es eventual y de una instancia; vuelve a generar todo el catálogo. Una operación con varias instancias o un catálogo grande requiere coordinación distribuida e indexación incremental persistente.
5. La compilación informa advertencias preexistentes: uso obsoleto de `QdrantClient.SearchAsync` y dependencia transitiva `System.Security.Cryptography.Pkcs` 9.0.11 con avisos NU1902/NU1903. No se cambiaron las dependencias de producción para ampliar el alcance de esta historia. Los avisos y versiones corregidas se describen en [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) y [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx).

La propuesta está lista para revisión y para subir la rama si el usuario lo confirma. La aprobación para subir no equivale a desplegar ni a fusionar en `main`.
