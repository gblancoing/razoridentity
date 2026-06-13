-- Limpieza puntual (2026-06-11): órdenes con pin de destino incompleto o fuera
-- de Chile (ej. longitud NULL por el bug de binding del picker) dibujaban el
-- marcador del tracking en el océano. El API ya no persiste destinos
-- implausibles; esto corrige datos históricos.
UPDATE core.orders
SET destination_lat = NULL,
    destination_lng = NULL
WHERE (destination_lat IS NOT NULL OR destination_lng IS NOT NULL)
  AND (
    destination_lat IS NULL
    OR destination_lng IS NULL
    OR NOT (
      destination_lat BETWEEN -56.5 AND -17.0
      AND destination_lng BETWEEN -110.0 AND -66.0
    )
  );
