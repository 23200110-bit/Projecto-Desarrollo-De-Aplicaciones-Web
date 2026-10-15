-- Solo lectura. Revisar antes de desplegar el cambio que conserva Aforo como capacidad total.
-- La propuesta solo es válida si no hubo edición posterior de aforo ni eliminación de compras.
SELECT e.Id, e.Nombre, e.Aforo AS AforoAlmacenado,
       COALESCE(SUM(CAST(t.Cantidad AS bigint)), 0) AS CantidadCompradaHistorica,
       CAST(e.Aforo AS bigint) + COALESCE(SUM(CAST(t.Cantidad AS bigint)), 0) AS CapacidadPropuesta
FROM Eventos e LEFT JOIN Tickets t ON t.EventoId = e.Id
GROUP BY e.Id, e.Nombre, e.Aforo;
