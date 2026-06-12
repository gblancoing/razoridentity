// ============================================================================
// Módulo de delivery con tracking GPS en vivo.
// - comunaclic.deliveryTracking: mapa del comprador (Leaflet + SignalR, con
//   fallback a polling del snapshot si el hub no logra conectar).
// - comunaclic.courierGps: captura GPS del repartidor (watchPosition + envío
//   cada ~5s al API) con Wake Lock para que la pantalla no se apague.
// Sin claves: tiles de OpenStreetMap y hub propio en el API.
// ============================================================================
window.comunaclic = window.comunaclic || {};

window.comunaclic.deliveryTracking = (function () {
  var state = null;

  function createIcon(emoji, bg) {
    // Marcadores diferenciados (origen/destino/repartidor) sin assets extra.
    return L.divIcon({
      className: "",
      html:
        '<div style="width:38px;height:38px;border-radius:50%;background:' + bg + ';' +
        'display:flex;align-items:center;justify-content:center;font-size:20px;' +
        'box-shadow:0 2px 8px rgba(0,0,0,.35);border:2px solid #fff;">' + emoji + "</div>",
      iconSize: [38, 38],
      iconAnchor: [19, 19],
    });
  }

  function isPlausible(lat, lng) {
    // ComunaClic opera en Chile: coordenadas fuera del territorio (ej. 0,0 de
    // un GPS fallido) se descartan para no dibujar marcadores en el océano.
    return lat != null && lng != null && lat >= -56.5 && lat <= -17 && lng >= -110 && lng <= -66;
  }

  function fitMap() {
    if (!state || !state.map) return;
    var points = [];
    if (state.originMarker) points.push(state.originMarker.getLatLng());
    if (state.destinationMarker) points.push(state.destinationMarker.getLatLng());
    if (state.courierMarker) points.push(state.courierMarker.getLatLng());
    if (points.length === 0) {
      state.map.setView([-33.45, -70.66], 12); // Chile central por defecto
      return;
    }
    if (points.length === 1) {
      state.map.setView(points[0], 15);
      return;
    }
    state.map.fitBounds(L.latLngBounds(points).pad(0.25));
  }

  function moveCourier(lat, lng) {
    if (!state || !state.map || !isPlausible(lat, lng)) return;
    var pos = [lat, lng];
    if (!state.courierMarker) {
      state.courierMarker = L.marker(pos, { icon: createIcon("🛵", "#3b6700"), zIndexOffset: 1000 })
        .addTo(state.map)
        .bindPopup(state.labels.courier || "Repartidor");
      fitMap();
    } else {
      state.courierMarker.setLatLng(pos);
      // Mantener al repartidor visible sin re-encuadrar agresivamente.
      if (!state.map.getBounds().contains(pos)) {
        state.map.panTo(pos);
      }
    }
  }

  function notifyStatus(status) {
    if (state && state.dotNetRef) {
      state.dotNetRef.invokeMethodAsync("OnDeliveryStatusChanged", status).catch(function () {});
    }
  }

  function stopPollingFallback() {
    if (state && state.pollTimer) {
      clearInterval(state.pollTimer);
      state.pollTimer = null;
    }
  }

  function startPollingFallback() {
    // SignalR agotó los reintentos: se consulta el snapshot cada ~12s y se
    // sigue intentando reconectar el hub en segundo plano.
    if (!state || state.pollTimer) return;
    state.pollTimer = setInterval(function () {
      if (!state) return;
      fetch(state.snapshotUrl, { cache: "no-store" })
        .then(function (r) { return r.ok ? r.json() : null; })
        .then(function (snap) {
          if (!snap || !state) return;
          if (snap.lastLat != null && snap.lastLng != null) {
            moveCourier(snap.lastLat, snap.lastLng);
          }
          if (snap.deliveryStatus && snap.deliveryStatus !== state.lastStatus) {
            state.lastStatus = snap.deliveryStatus;
            notifyStatus(snap.deliveryStatus);
          }
        })
        .catch(function () {});
      if (state.connection && state.connection.state === "Disconnected") {
        state.connection.start().then(function () {
          return state.connection.invoke("JoinOrderTracking", state.orderId, state.token);
        }).then(stopPollingFallback).catch(function () {});
      }
    }, 12000);
  }

  return {
    // opts: { hubUrl, snapshotUrl, orderId, token, origin {lat,lng}, destination {lat,lng},
    //         courier {lat,lng}, status, labels {origin,destination,courier}, dotNetRef }
    start: function (elementId, opts) {
      this.stop();
      var el = document.getElementById(elementId);
      if (!el || typeof L === "undefined") return;

      if (window.comunaclic.configureLeafletIcons) {
        window.comunaclic.configureLeafletIcons();
      }
      var map = L.map(elementId, { scrollWheelZoom: false });
      window.comunaclic.applyLeafletBaseLayers(map, opts.layerLabels || null);

      state = {
        map: map,
        connection: null,
        pollTimer: null,
        orderId: opts.orderId,
        token: opts.token,
        snapshotUrl: opts.snapshotUrl,
        dotNetRef: opts.dotNetRef || null,
        labels: opts.labels || {},
        lastStatus: opts.status || null,
        originMarker: null,
        destinationMarker: null,
        courierMarker: null,
      };

      if (opts.origin && isPlausible(opts.origin.lat, opts.origin.lng)) {
        state.originMarker = L.marker([opts.origin.lat, opts.origin.lng], { icon: createIcon("🏪", "#2c2f30") })
          .addTo(map)
          .bindPopup(state.labels.origin || "Origen");
      }
      if (opts.destination && isPlausible(opts.destination.lat, opts.destination.lng)) {
        state.destinationMarker = L.marker([opts.destination.lat, opts.destination.lng], { icon: createIcon("🏠", "#b45309") })
          .addTo(map)
          .bindPopup(state.labels.destination || "Destino");
      }
      if (state.originMarker && state.destinationMarker) {
        L.polyline([state.originMarker.getLatLng(), state.destinationMarker.getLatLng()],
          { color: "#3b6700", weight: 3, opacity: 0.5, dashArray: "8 8" }).addTo(map);
      }
      // El repartidor parte de la última ubicación persistida (no del origen).
      if (opts.courier && opts.courier.lat != null) {
        moveCourier(opts.courier.lat, opts.courier.lng);
      }
      fitMap();

      if (typeof signalR === "undefined") {
        startPollingFallback();
        return;
      }

      var connection = new signalR.HubConnectionBuilder()
        .withUrl(opts.hubUrl)
        .withAutomaticReconnect()
        .build();
      state.connection = connection;

      connection.on("CourierLocationChanged", function (p) {
        if (p && p.lat != null) moveCourier(p.lat, p.lng);
      });
      connection.on("DeliveryStatusChanged", function (p) {
        if (p && p.status && state) {
          state.lastStatus = p.status;
          notifyStatus(p.status);
        }
      });
      connection.onreconnected(function () {
        connection.invoke("JoinOrderTracking", opts.orderId, opts.token).catch(function () {});
        stopPollingFallback();
      });
      // Tras agotar los reintentos automáticos cae a polling del snapshot.
      connection.onclose(function () { startPollingFallback(); });

      connection.start()
        .then(function () { return connection.invoke("JoinOrderTracking", opts.orderId, opts.token); })
        .catch(function () { startPollingFallback(); });
    },

    stop: function () {
      if (!state) return;
      stopPollingFallback();
      if (state.connection) {
        try { state.connection.stop(); } catch (e) { /* sin conexión */ }
      }
      if (state.map) {
        try { state.map.remove(); } catch (e) { /* mapa ya destruido */ }
      }
      state = null;
    },
  };
})();

window.comunaclic.courierGps = (function () {
  var state = null;

  function sendLocation(lat, lng, timestamp) {
    if (!state) return;
    // El token también viaja en la query para el rate limit particionado por token.
    fetch(state.endpoint + "?token=" + encodeURIComponent(state.token), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        orderId: state.orderId,
        lat: lat,
        lng: lng,
        timestamp: new Date(timestamp).toISOString(),
        token: state.token,
      }),
    })
      .then(function (r) {
        if (state && state.dotNetRef) {
          state.dotNetRef.invokeMethodAsync("OnGpsSent", r.ok).catch(function () {});
        }
      })
      .catch(function () {
        if (state && state.dotNetRef) {
          state.dotNetRef.invokeMethodAsync("OnGpsSent", false).catch(function () {});
        }
      });
  }

  function requestWakeLock() {
    // Wake Lock API: mantiene la pantalla encendida (watchPosition se pausa
    // con la pantalla apagada). Donde no exista, queda el aviso visual.
    if (!state || !("wakeLock" in navigator)) return;
    navigator.wakeLock.request("screen")
      .then(function (lock) { if (state) state.wakeLock = lock; })
      .catch(function () {});
  }

  function onVisibilityChange() {
    if (state && document.visibilityState === "visible") {
      requestWakeLock();
    }
  }

  return {
    // opts: { endpoint, orderId, token, intervalMs, dotNetRef }
    start: function (opts) {
      this.stop();
      if (!navigator.geolocation) {
        if (opts.dotNetRef) opts.dotNetRef.invokeMethodAsync("OnGpsUnavailable").catch(function () {});
        return;
      }

      state = {
        endpoint: opts.endpoint,
        orderId: opts.orderId,
        token: opts.token,
        intervalMs: Math.max(3000, opts.intervalMs || 5000),
        dotNetRef: opts.dotNetRef || null,
        lastSentAt: 0,
        watchId: null,
        wakeLock: null,
      };

      state.watchId = navigator.geolocation.watchPosition(
        function (pos) {
          if (!state) return;
          var now = Date.now();
          // Throttle: el GPS puede emitir varias veces por segundo.
          if (now - state.lastSentAt < state.intervalMs) return;
          state.lastSentAt = now;
          sendLocation(pos.coords.latitude, pos.coords.longitude, pos.timestamp || now);
        },
        function (err) {
          if (state && state.dotNetRef) {
            state.dotNetRef.invokeMethodAsync("OnGpsError", err && err.message ? err.message : "").catch(function () {});
          }
        },
        { enableHighAccuracy: true, maximumAge: 0, timeout: 20000 }
      );

      requestWakeLock();
      document.addEventListener("visibilitychange", onVisibilityChange);
    },

    stop: function () {
      if (!state) return;
      if (state.watchId != null && navigator.geolocation) {
        navigator.geolocation.clearWatch(state.watchId);
      }
      if (state.wakeLock) {
        try { state.wakeLock.release(); } catch (e) { /* ya liberado */ }
      }
      document.removeEventListener("visibilitychange", onVisibilityChange);
      state = null;
    },
  };
})();
