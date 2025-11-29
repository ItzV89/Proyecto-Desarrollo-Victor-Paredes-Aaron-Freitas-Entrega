// Archivo eliminado: anteriormente contenía un subscriber de RabbitMQ usado para reemitir
// eventos al `SeatHub` de SignalR. Se mantenía deshabilitado con `#if false` y provocó
// problemas de compilación al intentar usar tipos de RabbitMQ en distintas versiones.
//
// Se conserva el historial en el control de versiones; si se desea reimplementar el
// subscriber en producción, crear un servicio que use `IConnection`/`IModel` de forma
// explícita y garantice la compatibilidad de versiones con el paquete `RabbitMQ.Client`.

