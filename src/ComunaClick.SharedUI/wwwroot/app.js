window.comunaclic = window.comunaclic || {};
window.comunaclic._leafletMaps = window.comunaclic._leafletMaps || {};

window.comunaclic.configureLeafletIcons = function () {
  if (!window.L || !window.L.Icon || !window.L.Icon.Default) {
    return;
  }

  var iconBase =
    "_content/ComunaClick.SharedUI/lib/leaflet/images/";
  delete window.L.Icon.Default.prototype._getIconUrl;
  window.L.Icon.Default.mergeOptions({
    iconRetinaUrl: iconBase + "marker-icon-2x.png",
    iconUrl: iconBase + "marker-icon.png",
    shadowUrl: iconBase + "marker-shadow.png"
  });
};

window.comunaclic.configureLeafletIcons();

window.comunaclic.escapeHtml = function (value) {
  if (value === null || value === undefined) {
    return "";
  }
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/\"/g, "&quot;")
    .replace(/'/g, "&#039;");
};

window.comunaclic.safeImageUrl = function (value) {
  if (!value) {
    return "";
  }
  const s = String(value).trim();
  if (s.startsWith("/") || s.startsWith("http://") || s.startsWith("https://")) {
    return s;
  }
  return "";
};

window.comunaclic.getLang = function () {
  try {
    return localStorage.getItem("comunaclic.lang") || "";
  } catch {
    return "";
  }
};

window.comunaclic.setLang = function (lang) {
  try {
    localStorage.setItem("comunaclic.lang", lang);
  } catch {
    // Ignore storage errors
  }
};

// Hardening de sesión: el ACCESS token vive solo en sessionStorage (por pestaña, sin refresh token).
// El REFRESH token nunca toca JS: viaja en la cookie HttpOnly __Host-cc_rt emitida por el host.
window.comunaclic._SESSION_TOKENS_KEY = "comunaclic.session.tokens";
window.comunaclic._SESSION_FLAG_KEY = "comunaclic.session"; // marcador no sensible (¿hubo sesión?)

// Migración: elimina cualquier token viejo (incl. refresh) que quedó en localStorage.
try {
  localStorage.removeItem("comunaclic.tokens");
} catch {
  // Ignore storage errors
}

window.comunaclic.getTokens = function () {
  try {
    return sessionStorage.getItem(window.comunaclic._SESSION_TOKENS_KEY) || "";
  } catch {
    return "";
  }
};

window.comunaclic.setTokens = function (json) {
  try {
    sessionStorage.setItem(window.comunaclic._SESSION_TOKENS_KEY, json);
    localStorage.setItem(window.comunaclic._SESSION_FLAG_KEY, "1");
  } catch {
    // Ignore storage errors
  }
};

window.comunaclic.clearTokens = function () {
  try {
    sessionStorage.removeItem(window.comunaclic._SESSION_TOKENS_KEY);
    localStorage.removeItem(window.comunaclic._SESSION_FLAG_KEY);
  } catch {
    // Ignore storage errors
  }
};

window.comunaclic.hadSession = function () {
  try {
    return localStorage.getItem(window.comunaclic._SESSION_FLAG_KEY) === "1";
  } catch {
    return false;
  }
};

// Helpers de sesión: hacen el fetch same-origin a /auth/session/* para que la cookie HttpOnly
// se emita/adjunte sola. Devuelven el JSON (string) para que el circuito lo hidrate. El header
// X-CC-Session fuerza preflight (defensa CSRF junto con SameSite=Strict + validación de Origin).
window.comunaclic._sessionFetch = async function (path, body) {
  try {
    const response = await fetch(path, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json",
        "X-CC-Session": "1"
      },
      body: JSON.stringify(body || {})
    });

    if (!response.ok) {
      return "";
    }

    return await response.text();
  } catch {
    return "";
  }
};

window.comunaclic.sessionLogin = function (email, password, tenantId, partnerId, recaptchaToken) {
  return window.comunaclic._sessionFetch("/auth/session/login", {
    email: email,
    password: password,
    tenantId: tenantId || null,
    partnerId: partnerId || null,
    recaptchaToken: recaptchaToken || null
  });
};

window.comunaclic.sessionRegister = function (name, email, password, recaptchaToken) {
  return window.comunaclic._sessionFetch("/auth/session/register", {
    name: name,
    email: email,
    password: password,
    recaptchaToken: recaptchaToken || null
  });
};

window.comunaclic.sessionGoogle = function (idToken, tenantId, partnerId) {
  return window.comunaclic._sessionFetch("/auth/session/google", {
    idToken: idToken,
    tenantId: tenantId || null,
    partnerId: partnerId || null
  });
};

window.comunaclic.sessionRefresh = function (tenantId, partnerId) {
  return window.comunaclic._sessionFetch("/auth/session/refresh", {
    tenantId: tenantId || null,
    partnerId: partnerId || null
  });
};

window.comunaclic.sessionLogout = function () {
  return window.comunaclic._sessionFetch("/auth/session/logout", {});
};

window.comunaclic.getCustomerId = function () {
  try {
    return localStorage.getItem("comunaclic.customerId") || "";
  } catch {
    return "";
  }
};

window.comunaclic.setCustomerId = function (customerId) {
  try {
    localStorage.setItem("comunaclic.customerId", customerId || "");
  } catch {
    // Ignore storage errors
  }
};

/** JSON string: { "orderId": "…", "bookingId": "…" } (optional keys) for buyer "Mis compras" quick recall. */
/** JSON global de favoritos: { "version":1, "users": { "correo@x.com": { "places":[], "businesses":[] } } } } */
window.comunaclic.getFavoritesData = function () {
  try {
    return localStorage.getItem("comunaclic.favoritesData") || "";
  } catch {
    return "";
  }
};

window.comunaclic.setFavoritesData = function (json) {
  try {
    if (!json) {
      localStorage.removeItem("comunaclic.favoritesData");
    } else {
      localStorage.setItem("comunaclic.favoritesData", json);
    }
  } catch {
    // ignore
  }
};

window.comunaclic.getLastTracking = function () {
  try {
    return localStorage.getItem("comunaclic.lastTracking") || "";
  } catch {
    return "";
  }
};

window.comunaclic.setLastTracking = function (json) {
  try {
    if (!json) {
      localStorage.removeItem("comunaclic.lastTracking");
    } else {
      localStorage.setItem("comunaclic.lastTracking", json);
    }
  } catch {
    // Ignore storage errors
  }
};

/**
 * Guest cart v2 (multi-negocio): { version: 2, groups: [{ tenantId, partnerId,
 * partnerName, currency, items:[{productId,name,quantity,unitPrice,maxAvailable}] }] }
 * El shape v1 (un solo negocio en la raíz) se migra de forma lazy al leer/escribir.
 */
window.comunaclic._migrateGuestCart = function (raw) {
  try {
    if (!raw) return null;
    var parsed = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (!parsed) return null;
    if (Array.isArray(parsed.groups)) {
      parsed.version = 2;
      parsed.groups = parsed.groups.filter(function (g) {
        return g && g.partnerId && Array.isArray(g.items) && g.items.length > 0;
      });
      return parsed.groups.length > 0 ? parsed : null;
    }
    if (parsed.partnerId && Array.isArray(parsed.items) && parsed.items.length > 0) {
      return { version: 2, groups: [parsed] };
    }
    return null;
  } catch {
    return null;
  }
};

window.comunaclic._saveGuestCart = function (cart) {
  if (!cart || !Array.isArray(cart.groups) || cart.groups.length === 0) {
    window.comunaclic.setGuestCart(null);
  } else {
    cart.version = 2;
    window.comunaclic.setGuestCart(JSON.stringify(cart));
  }
};

window.comunaclic.getGuestCart = function () {
  try {
    return localStorage.getItem("comunaclic.guestCart") || "";
  } catch {
    return "";
  }
};

window.comunaclic.setGuestCart = function (json) {
  try {
    if (!json) {
      localStorage.removeItem("comunaclic.guestCart");
    } else {
      localStorage.setItem("comunaclic.guestCart", json);
    }
  } catch {
    // ignore
  }
};

window.comunaclic.clearGuestCart = function () {
  window.comunaclic.setGuestCart(null);
};

window.comunaclic.addGuestCartItem = function (itemJson) {
  try {
    var item = typeof itemJson === "string" ? JSON.parse(itemJson) : itemJson;
    if (!item || !item.productId || !item.partnerId || !item.tenantId) return "invalid";
    var cart = window.comunaclic._migrateGuestCart(window.comunaclic.getGuestCart())
      || { version: 2, groups: [] };

    var group = cart.groups.find(function (g) { return g.partnerId === item.partnerId; });
    var isNewGroup = false;
    if (!group) {
      isNewGroup = cart.groups.length > 0;
      group = {
        tenantId: item.tenantId,
        partnerId: item.partnerId,
        partnerName: item.partnerName || "",
        currency: item.currency || "CLP",
        items: []
      };
      cart.groups.push(group);
    }

    var existing = group.items.find(function (x) { return x.productId === item.productId; });
    var qty = Math.max(1, parseInt(item.quantity, 10) || 1);
    if (existing) {
      existing.quantity = Math.min((existing.maxAvailable || 99), existing.quantity + qty);
    } else {
      group.items.push({
        productId: item.productId,
        name: item.name || "Producto",
        quantity: qty,
        unitPrice: item.unitPrice || 0,
        maxAvailable: item.maxAvailable || 99
      });
    }
    window.comunaclic._saveGuestCart(cart);
    return isNewGroup ? "ok_new_group" : "ok";
  } catch {
    return "error";
  }
};

/** Quita el grupo de un negocio (p. ej. cuando su pedido ya fue creado). */
window.comunaclic.removeGuestCartGroup = function (partnerId) {
  try {
    var cart = window.comunaclic._migrateGuestCart(window.comunaclic.getGuestCart());
    if (!cart) return;
    cart.groups = cart.groups.filter(function (g) { return g.partnerId !== partnerId; });
    window.comunaclic._saveGuestCart(cart);
  } catch {
    // ignore
  }
};

/** Total de unidades en el carrito (para el contador "Ver carrito (N)"). */
window.comunaclic.getGuestCartCount = function () {
  try {
    var cart = window.comunaclic._migrateGuestCart(window.comunaclic.getGuestCart());
    if (!cart) return 0;
    return cart.groups.reduce(function (sum, g) {
      return sum + g.items.reduce(function (s, x) { return s + (parseInt(x.quantity, 10) || 0); }, 0);
    }, 0);
  } catch {
    return 0;
  }
};

/**
 * Pending MP v2 (varios pagos en paralelo, uno por negocio):
 * { version: 2, payments: [{ orderId?, bookingId?, customerId, createdAt, checkoutUrl?, partnerName? }] }
 * El shape v1 (un solo objeto en la raíz) se migra de forma lazy.
 */
window.comunaclic._migratePendingMp = function (raw) {
  try {
    if (!raw) return null;
    var parsed = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (!parsed) return null;
    if (Array.isArray(parsed.payments)) {
      parsed.version = 2;
      return parsed.payments.length > 0 ? parsed : null;
    }
    if (parsed.orderId || parsed.bookingId) {
      return { version: 2, payments: [parsed] };
    }
    return null;
  } catch {
    return null;
  }
};

window.comunaclic._savePendingMp = function (pending) {
  if (!pending || !Array.isArray(pending.payments) || pending.payments.length === 0) {
    window.comunaclic.setPendingMpPayment(null);
  } else {
    pending.version = 2;
    window.comunaclic.setPendingMpPayment(JSON.stringify(pending));
  }
};

window.comunaclic.getPendingMpPayment = function () {
  try {
    return localStorage.getItem("comunaclic.pendingMpPayment") || "";
  } catch {
    return "";
  }
};

window.comunaclic.setPendingMpPayment = function (json) {
  try {
    if (!json) {
      localStorage.removeItem("comunaclic.pendingMpPayment");
    } else {
      localStorage.setItem("comunaclic.pendingMpPayment", json);
    }
  } catch {
    // ignore
  }
};

/** Agrega o reemplaza (por orderId/bookingId) un pago pendiente. */
window.comunaclic.addPendingMpPayment = function (entryJson) {
  try {
    var entry = typeof entryJson === "string" ? JSON.parse(entryJson) : entryJson;
    if (!entry || (!entry.orderId && !entry.bookingId)) return;
    var pending = window.comunaclic._migratePendingMp(window.comunaclic.getPendingMpPayment())
      || { version: 2, payments: [] };
    pending.payments = pending.payments.filter(function (p) {
      if (entry.orderId) return p.orderId !== entry.orderId;
      return p.bookingId !== entry.bookingId;
    });
    pending.payments.push(entry);
    window.comunaclic._savePendingMp(pending);
  } catch {
    // ignore
  }
};

/** Remueve solo el pago de una orden/reserva (el resto sigue pendiente). */
window.comunaclic.removePendingMpPayment = function (orderId, bookingId) {
  try {
    var pending = window.comunaclic._migratePendingMp(window.comunaclic.getPendingMpPayment());
    if (!pending) return;
    pending.payments = pending.payments.filter(function (p) {
      if (orderId && p.orderId === orderId) return false;
      if (bookingId && p.bookingId === bookingId) return false;
      return true;
    });
    window.comunaclic._savePendingMp(pending);
  } catch {
    // ignore
  }
};

window.comunaclic.getDeviceType = function () {
  try {
    const width = window.innerWidth || 0;
    if (width > 0 && width < 768) return "mobile";
    if (width >= 768 && width < 1024) return "tablet";
    return "desktop";
  } catch {
    return "unknown";
  }
};

window.comunaclic.executeRecaptcha = function (action) {
  if (!window.comunaclicRecaptchaEnabled || !window.comunaclicRecaptchaSiteKey) {
    return Promise.resolve("");
  }

  return new Promise((resolve, reject) => {
    if (!window.grecaptcha || typeof window.grecaptcha.ready !== "function") {
      reject(new Error("reCAPTCHA is not available."));
      return;
    }

    window.grecaptcha.ready(() => {
      window.grecaptcha
        .execute(window.comunaclicRecaptchaSiteKey, { action: action || "submit" })
        .then(resolve)
        .catch(reject);
    });
  });
};

window.comunaclic.setAccountRole = function (role) {
  try {
    const normalized = (role || "natural").toLowerCase();
    if (normalized === "commerce" || normalized === "professional" || normalized === "natural") {
      localStorage.setItem("comunaclic.accountRole", normalized);
    }
  } catch {
    // Ignore storage errors.
  }
};

window.comunaclic.getAccountRole = function () {
  try {
    return localStorage.getItem("comunaclic.accountRole") || "";
  } catch {
    return "";
  }
};

window.comunaclic.getPartnerSidebarCollapsed = function () {
  try {
    return localStorage.getItem("comunaclic.partnerSidebarCollapsed") === "1";
  } catch {
    return false;
  }
};

window.comunaclic.setPartnerSidebarCollapsed = function (collapsed) {
  try {
    localStorage.setItem("comunaclic.partnerSidebarCollapsed", collapsed ? "1" : "0");
  } catch {
    // Ignore storage errors
  }
};

window.comunaclic.setRegisterIntent = function (role, returnUrl) {
  try {
    const normalized = (role || "natural").toLowerCase();
    window.comunaclic.setAccountRole(normalized);
    sessionStorage.setItem(
      "comunaclic.registerIntent",
      JSON.stringify({
        role: normalized,
        returnUrl: returnUrl || "",
        savedAt: Date.now()
      })
    );
  } catch {
    // Ignore storage errors; callback will fall back to URL query role.
  }
};

window.comunaclic.consumeRegisterIntent = function () {
  try {
    const raw = sessionStorage.getItem("comunaclic.registerIntent");
    sessionStorage.removeItem("comunaclic.registerIntent");
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw);
    return {
      role: (parsed.role || "natural").toLowerCase(),
      returnUrl: parsed.returnUrl || ""
    };
  } catch {
    return null;
  }
};

/** Persist returnUrl before Google OAuth (redirect_uri must be path-only for Google Console). */
window.comunaclic.setGoogleLoginReturnUrl = function (returnUrl) {
  try {
    const value = (returnUrl || "").trim();
    if (value) {
      sessionStorage.setItem("comunaclic.google.returnUrl", value);
    } else {
      sessionStorage.removeItem("comunaclic.google.returnUrl");
    }
  } catch {
    // Ignore storage errors.
  }
};

window.comunaclic.consumeGoogleLoginReturnUrl = function () {
  try {
    const value = sessionStorage.getItem("comunaclic.google.returnUrl") || "";
    sessionStorage.removeItem("comunaclic.google.returnUrl");
    return value;
  } catch {
    return "";
  }
};

window.comunaclic.beginGoogleSignIn = function (redirectUrl) {
  const clientId = (window.comunaclicGoogleClientId || "").trim();
  if (!clientId) {
    throw new Error("Google sign-in is not configured.");
  }

  let callbackUrl = (redirectUrl || window.location.href || "").split("#")[0];
  try {
    const parsed = new URL(callbackUrl, window.location.origin);
    callbackUrl = `${parsed.origin}${parsed.pathname}`;
  } catch {
    // Keep callbackUrl as provided if URL parsing fails.
  }
  if (!callbackUrl) {
    throw new Error("Unable to resolve callback URL for Google sign-in.");
  }

  const random = (length) => {
    const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    const bytes = new Uint8Array(length);
    window.crypto.getRandomValues(bytes);
    let output = "";
    for (let index = 0; index < bytes.length; index += 1) {
      output += alphabet[bytes[index] % alphabet.length];
    }
    return output;
  };

  const state = random(32);
  const nonce = random(32);

  try {
    sessionStorage.setItem("comunaclic.google.state", state);
    sessionStorage.setItem("comunaclic.google.nonce", nonce);
  } catch {
    // Continue without session storage; callback validation will fail closed.
  }

  const query = new URLSearchParams({
    client_id: clientId,
    redirect_uri: callbackUrl,
    response_type: "id_token",
    scope: "openid email profile",
    nonce,
    state,
    prompt: "select_account"
  });

  window.location.assign(`https://accounts.google.com/o/oauth2/v2/auth?${query.toString()}`);
};

window.comunaclic.consumeGoogleIdTokenFromHash = function () {
  const hash = window.location.hash || "";
  if (!hash || hash.length <= 1) {
    return "";
  }

  const fragment = new URLSearchParams(hash.slice(1));
  const idToken = fragment.get("id_token") || "";
  const returnedState = fragment.get("state") || "";

  if (!idToken) {
    return "";
  }

  let expectedState = "";
  try {
    expectedState = sessionStorage.getItem("comunaclic.google.state") || "";
    sessionStorage.removeItem("comunaclic.google.state");
    sessionStorage.removeItem("comunaclic.google.nonce");
  } catch {
    // Ignore storage access errors.
  }

  if (!expectedState || expectedState !== returnedState) {
    return "";
  }

  try {
    window.history.replaceState(null, "", `${window.location.pathname}${window.location.search}`);
  } catch {
    // Ignore history errors.
  }

  return idToken;
};

/**
 * @param { { forceFresh?: boolean, maximumAge?: number, timeout?: number } } [request]
 * forceFresh: true = solicitud nueva (mejor al pulsar "Activar ubicación" para que vuelva el aviso del navegador)
 */
window.comunaclic.getCurrentPosition = function (request) {
  return new Promise((resolve, reject) => {
    if (!navigator.geolocation) {
      reject(new Error("Geolocation is not supported by this browser."));
      return;
    }

    // Exige contexto "seguro" (HTTPS, localhost, 127.0.0.1). Con http://192.168.x.x el navegador suele bloquear el GPS.
    if (typeof isSecureContext !== "undefined" && isSecureContext === false) {
      reject(
        new Error(
          "En esta URL el navegador no permite el GPS (sitio no seguro). Probalo con https, con https://localhost:puerto, o accediendo desde la misma PC con localhost en lugar de la IP de la red."
        )
      );
      return;
    }

    const opts = request && typeof request === "object" ? request : {};
    const forceFresh = !!opts.forceFresh;
    const maximumAge = typeof opts.maximumAge === "number" ? opts.maximumAge : forceFresh ? 0 : 300000;
    const timeout = typeof opts.timeout === "number" ? opts.timeout : forceFresh ? 20000 : 10000;

    navigator.geolocation.getCurrentPosition(
      (position) => {
        // camelCase: el DTO C# mapea con [JsonPropertyName] (p. ej. "latitude" -> Latitude)
        resolve({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracy: position.coords.accuracy != null && !isNaN(position.coords.accuracy) ? position.coords.accuracy : 0
        });
      },
      (error) => {
        const code = error && error.code;
        const messages = {
          1: "Permiso de ubicación denegado. Permití el acceso al GPS en el icono de la barra de direcciones o en ajustes del sitio, y reintentá.",
          2: "Ubicación no disponible (sin señal / sensor).",
          3: "Se agotó el tiempo al pedir el GPS. Reintentá o comprobá la conexión."
        };
        const msg =
          code && messages[code] ? messages[code] : (error && error.message ? error.message : "No se pudo obtener la ubicación.");
        reject(new Error(msg));
      },
      {
        enableHighAccuracy: true,
        timeout: timeout,
        maximumAge: maximumAge
      }
    );
  });
};

window.comunaclic.scrollToElement = function (elementId) {
  if (!elementId) {
    return;
  }
  const el = document.getElementById(elementId);
  if (el && el.scrollIntoView) {
    el.scrollIntoView({ behavior: "smooth", block: "start" });
  }
};

/** Capas base Leaflet: calles (OSM) y satélite (Esri + etiquetas). */
window.comunaclic.applyLeafletBaseLayers = function (map, layerLabels) {
  const labels = layerLabels || {};
  const mapaLabel = labels.mapa || labels.street || "Mapa";
  const satLabel = labels.satelite || labels.satellite || "Satélite";

  const street = window.L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: "&copy; OpenStreetMap contributors"
  });

  const satellite = window.L.tileLayer(
    "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
    {
      maxZoom: 19,
      attribution: "Tiles &copy; Esri, Maxar, Earthstar Geographics"
    }
  );

  const satelliteLabels = window.L.tileLayer(
    "https://services.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}",
    {
      maxZoom: 19,
      pane: "overlayPane"
    }
  );

  const satelliteHybrid = window.L.layerGroup([satellite, satelliteLabels]);
  const baseLayers = {};
  baseLayers[mapaLabel] = street;
  baseLayers[satLabel] = satelliteHybrid;

  street.addTo(map);
  window.L.control
    .layers(baseLayers, null, {
      position: "topright",
      collapsed: window.innerWidth && window.innerWidth < 640
    })
    .addTo(map);

  return { street: street, satellite: satelliteHybrid };
};

window.comunaclic.renderCategoryNearbyMap = function (elementId, userLocation, businesses, returnPath, layerLabels) {
  if (!window.L) {
    throw new Error("Leaflet is not available.");
  }

  const element = document.getElementById(elementId);
  if (!element) {
    return;
  }

  const existingMap = window.comunaclic._leafletMaps[elementId];
  if (existingMap) {
    existingMap.remove();
    delete window.comunaclic._leafletMaps[elementId];
  }

  // No fijar minHeight aquí: compite con las clases del contenedor (mapa demasiado bajo en móvil).

  const map = window.L.map(elementId, {
    zoomControl: true,
    scrollWheelZoom: false,
    tap: true,
    dragging: !(window.innerWidth && window.innerWidth < 640)
  });

  window.comunaclic._leafletMaps[elementId] = map;
  window.comunaclic.applyLeafletBaseLayers(map, layerLabels);

  const markers = [];
  const useClusters = Array.isArray(businesses) && businesses.length >= 8 && typeof window.L.markerClusterGroup === "function";
  const businessLayer = useClusters
    ? window.L.markerClusterGroup({
        showCoverageOnHover: false,
        spiderfyOnMaxZoom: true,
        disableClusteringAtZoom: 16,
        maxClusterRadius: window.innerWidth && window.innerWidth < 640 ? 48 : 64
      })
    : null;
  const userLat = userLocation?.latitude ?? userLocation?.Latitude;
  const userLng = userLocation?.longitude ?? userLocation?.Longitude;
  const hasUserCoords =
    typeof userLat === "number" &&
    typeof userLng === "number" &&
    isFinite(userLat) &&
    isFinite(userLng);
  if (hasUserCoords) {
    const userMarker = window.L.circleMarker([userLat, userLng], {
      radius: 10,
      color: "#ffffff",
      weight: 3,
      fillColor: "#2d9e4f",
      fillOpacity: 1
    }).addTo(map);

    userMarker.bindPopup("<strong>Tu ubicación aproximada</strong>");
    markers.push(userMarker);
  }

  (businesses || []).forEach((business) => {
    const latitude = business.latitude ?? business.Latitude;
    const longitude = business.longitude ?? business.Longitude;
    if (typeof latitude !== "number" || typeof longitude !== "number") {
      return;
    }

    const rawName = business.name ?? business.Name ?? "Negocio";
    const rawComunaName = business.comunaName ?? business.ComunaName ?? "";
    const rawAddress = business.address ?? business.Address ?? "";
    const name = window.comunaclic.escapeHtml(rawName);
    const comunaName = window.comunaclic.escapeHtml(rawComunaName);
    const address = window.comunaclic.escapeHtml(rawAddress);
    const distanceKm = business.distanceKm ?? business.DistanceKm;
    const usesExactLocation = business.usesExactLocation ?? business.UsesExactLocation;
    const rawLogoUrl = business.logoUrl ?? business.LogoUrl ?? "";
    const logoUrl = window.comunaclic.safeImageUrl(rawLogoUrl);
    const returnSuffix =
      typeof returnPath === "string" && returnPath.length > 0
        ? `?return=${encodeURIComponent(returnPath)}`
        : "";
    const href = `/buyer/detail/partner/${business.id ?? business.Id}${returnSuffix}`;
    const logo =
      logoUrl && logoUrl.length > 0
        ? `<img src="${logoUrl}" alt="" style="width:44px;height:44px;border-radius:12px;object-fit:cover;border:2px solid rgba(255,255,255,0.8);background:#fff;box-shadow:0 10px 26px -16px rgba(0,0,0,0.45);flex:0 0 auto;" />`
        : "";
    const popup = [
      `<div style="min-width:220px">`,
      `<div style="display:flex;gap:10px;align-items:center">`,
      logo,
      `<div style="min-width:0">`,
      `<strong style="display:block;line-height:1.15">${name}</strong>`,
      comunaName ? `<div style="margin-top:4px;color:#595c5d">${comunaName}</div>` : "",
      address ? `<div style="margin-top:4px;color:#595c5d">${address}</div>` : "",
      `</div>`,
      `</div>`,
      `<div style="margin-top:4px;color:#595c5d">${usesExactLocation ? "Ubicación del negocio" : "Referencia por comuna"}</div>`,
      typeof distanceKm === "number" ? `<div style="margin-top:6px;color:#2d9e4f;font-weight:700">${distanceKm.toFixed(1)} km aprox.</div>` : "",
      `<a href="${href}" style="display:inline-block;margin-top:8px;color:#2d9e4f;font-weight:700;text-decoration:none">Ver negocio</a>`,
      `</div>`
    ].join("");

    const marker = window.L.marker([latitude, longitude]);
    marker.bindPopup(popup);

    if (businessLayer) {
      businessLayer.addLayer(marker);
    } else {
      marker.addTo(map);
    }

    markers.push(marker);
  });

  if (businessLayer) {
    map.addLayer(businessLayer);
  }

  if (markers.length > 0) {
    try {
      const group = window.L.featureGroup(markers);
      const bounds = group.getBounds();
      if (bounds.isValid()) {
        map.fitBounds(bounds.pad(0.18));
      } else if (hasUserCoords) {
        map.setView([userLat, userLng], 12);
      }
    } catch (e) {
      if (hasUserCoords) {
        map.setView([userLat, userLng], 12);
      }
    }
  } else if (hasUserCoords) {
    map.setView([userLat, userLng], 12);
  }

  setTimeout(() => {
    map.invalidateSize();
  }, 150);
  setTimeout(() => {
    map.invalidateSize();
  }, 500);
};

window.comunaclic.destroyCategoryNearbyMap = function (elementId) {
  const existingMap = window.comunaclic._leafletMaps[elementId];
  if (existingMap) {
    existingMap.remove();
    delete window.comunaclic._leafletMaps[elementId];
  }
};

window.comunaclic.renderSearchResultsMap = function (elementId, userLocation, markers, layerLabels) {
  if (!window.L) {
    throw new Error("Leaflet is not available.");
  }

  const element = document.getElementById(elementId);
  if (!element) {
    return;
  }

  const existingMap = window.comunaclic._leafletMaps[elementId];
  if (existingMap) {
    existingMap.remove();
    delete window.comunaclic._leafletMaps[elementId];
  }

  const map = window.L.map(elementId, {
    zoomControl: true,
    scrollWheelZoom: false,
    tap: true,
    dragging: !(window.innerWidth && window.innerWidth < 640)
  });

  window.comunaclic._leafletMaps[elementId] = map;
  window.comunaclic.applyLeafletBaseLayers(map, layerLabels);

  const leafletMarkers = [];
  const useClusters = Array.isArray(markers) && markers.length >= 8 && typeof window.L.markerClusterGroup === "function";
  const markerLayer = useClusters
    ? window.L.markerClusterGroup({
        showCoverageOnHover: false,
        spiderfyOnMaxZoom: true,
        disableClusteringAtZoom: 16,
        maxClusterRadius: window.innerWidth && window.innerWidth < 640 ? 48 : 64
      })
    : null;

  const userLat = userLocation.latitude ?? userLocation.Latitude;
  const userLng = userLocation.longitude ?? userLocation.Longitude;
  if (typeof userLat === "number" && typeof userLng === "number") {
    const userMarker = window.L.circleMarker([userLat, userLng], {
      radius: 10,
      color: "#ffffff",
      weight: 3,
      fillColor: "#2d9e4f",
      fillOpacity: 1
    }).addTo(map);
    userMarker.bindPopup("<strong>Tu ubicación aproximada</strong>");
    leafletMarkers.push(userMarker);
  }

  (markers || []).forEach((entry) => {
    const latitude = entry.latitude ?? entry.Latitude;
    const longitude = entry.longitude ?? entry.Longitude;
    if (typeof latitude !== "number" || typeof longitude !== "number") {
      return;
    }

    const rawName = entry.name ?? entry.Name ?? "Resultado";
    const rawType = entry.typeLabel ?? entry.TypeLabel ?? "";
    const rawCategory = entry.category ?? entry.Category ?? "";
    const name = window.comunaclic.escapeHtml(rawName);
    const typeLabel = window.comunaclic.escapeHtml(rawType);
    const category = window.comunaclic.escapeHtml(rawCategory);
    const distanceKm = entry.distanceKm ?? entry.DistanceKm;
    const href = entry.href ?? entry.Href ?? "#";
    const rawLogoUrl = entry.logoUrl ?? entry.LogoUrl ?? "";
    const logoUrl = window.comunaclic.safeImageUrl(rawLogoUrl);
    const logo =
      logoUrl && logoUrl.length > 0
        ? `<img src="${logoUrl}" alt="" style="width:44px;height:44px;border-radius:12px;object-fit:cover;border:2px solid rgba(255,255,255,0.8);background:#fff;box-shadow:0 10px 26px -16px rgba(0,0,0,0.45);flex:0 0 auto;" />`
        : "";
    const popup = [
      `<div style="min-width:220px">`,
      `<div style="display:flex;gap:10px;align-items:flex-start">`,
      logo,
      `<div style="min-width:0;flex:1">`,
      `<strong style="display:block;line-height:1.2">${name}</strong>`,
      typeLabel ? `<div style="margin-top:4px;color:#2d9e4f;font-weight:700;font-size:12px">${typeLabel}</div>` : "",
      category ? `<div style="margin-top:4px;color:#595c5d;font-size:12px">${category}</div>` : "",
      typeof distanceKm === "number" ? `<div style="margin-top:6px;color:#2d9e4f;font-weight:700">${distanceKm.toFixed(1)} km</div>` : "",
      `<a href="${href}" style="display:inline-block;margin-top:8px;color:#2d9e4f;font-weight:700;text-decoration:none">Ver detalle</a>`,
      `</div>`,
      `</div>`,
      `</div>`
    ].join("");

    const marker = window.L.marker([latitude, longitude]);
    marker.bindPopup(popup);
    if (markerLayer) {
      markerLayer.addLayer(marker);
    } else {
      marker.addTo(map);
    }
    leafletMarkers.push(marker);
  });

  if (markerLayer) {
    map.addLayer(markerLayer);
  }

  if (leafletMarkers.length > 0) {
    const group = window.L.featureGroup(leafletMarkers);
    map.fitBounds(group.getBounds().pad(0.18));
  } else if (typeof userLat === "number" && typeof userLng === "number") {
    map.setView([userLat, userLng], 12);
  }

  setTimeout(() => {
    map.invalidateSize();
  }, 150);
  setTimeout(() => {
    map.invalidateSize();
  }, 500);
};

window.comunaclic.destroySearchResultsMap = function (elementId) {
  const existingMap = window.comunaclic._leafletMaps[elementId];
  if (existingMap) {
    existingMap.remove();
    delete window.comunaclic._leafletMaps[elementId];
  }
};

window.comunaclic.invalidateLeafletMap = function (elementId) {
  const map = window.comunaclic._leafletMaps[elementId];
  if (!map) {
    return;
  }
  setTimeout(function () {
    map.invalidateSize();
  }, 120);
  setTimeout(function () {
    map.invalidateSize();
  }, 400);
};

/** Cuando el elemento entra al viewport (o timeout), invoca el callback .NET una sola vez. */
window.comunaclic.observeOnceVisible = function (element, dotNetHelper) {
  if (!element || !dotNetHelper) {
    return;
  }
  let fired = false;
  let obs = null;
  let fallbackTimer = null;

  const run = function () {
    if (fired) {
      return;
    }
    fired = true;
    try {
      if (obs) {
        obs.disconnect();
      }
    } catch {
      // ignore
    }
    try {
      if (fallbackTimer !== null) {
        clearTimeout(fallbackTimer);
      }
    } catch {
      // ignore
    }
    dotNetHelper.invokeMethodAsync("OnStatsBarVisible");
  };

  if (window.IntersectionObserver) {
    obs = new IntersectionObserver(
      function (entries) {
        for (let i = 0; i < entries.length; i++) {
          if (entries[i].isIntersecting) {
            run();
            return;
          }
        }
      },
      { threshold: 0.12, rootMargin: "0px 0px -8% 0px" }
    );
    obs.observe(element);
    fallbackTimer = window.setTimeout(run, 7000);
  } else {
    run();
  }
};

window.comunaclic.prefersReducedMotion = function () {
  try {
    return !!(window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches);
  } catch {
    return false;
  }
};

window.comunaclic._profileAddressPickers = window.comunaclic._profileAddressPickers || {};

window.comunaclic.initProfileAddressPicker = function (elementId, dotNetHelper, latitude, longitude, autoLocate) {
  if (!window.L) {
    throw new Error("Leaflet is not available.");
  }

  const element = document.getElementById(elementId);
  if (!element) {
    return;
  }

  window.comunaclic.destroyProfileAddressPicker(elementId);

  const defaultLat = -33.4489;
  const defaultLng = -70.6693;
  const hasSaved =
    typeof latitude === "number" &&
    !isNaN(latitude) &&
    typeof longitude === "number" &&
    !isNaN(longitude);

  const map = window.L.map(elementId, {
    zoomControl: true,
    scrollWheelZoom: true
  });

  window.L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: "&copy; OpenStreetMap contributors"
  }).addTo(map);

  let marker = null;
  const pickerState = {
    map: map,
    dotNetHelper: dotNetHelper,
    userAdjusted: false
  };

  const notifyCoordsOnly = function (pickedLat, pickedLng) {
    if (!dotNetHelper) {
      return Promise.resolve();
    }

    return dotNetHelper.invokeMethodAsync("OnMapMarkerDragged", pickedLat, pickedLng);
  };

  const attachMarkerDrag = function () {
    if (!marker) {
      return;
    }

    marker.off("dragstart");
    marker.off("dragend");
    marker.on("dragstart", function () {
      pickerState.userAdjusted = true;
      if (dotNetHelper) {
        dotNetHelper.invokeMethodAsync("OnMapMarkerAdjustStarted");
      }
    });
    marker.on("dragend", function () {
      const pos = marker.getLatLng();
      pickerState.userAdjusted = true;
      pickerState.lastCoords = { latitude: pos.lat, longitude: pos.lng };
      notifyCoordsOnly(pos.lat, pos.lng);
    });
    if (marker.dragging) {
      marker.dragging.enable();
    }
    if (marker._icon) {
      marker._icon.style.cursor = "grab";
    }
  };

  const setMarker = function (coords) {
    if (marker) {
      marker.setLatLng(coords);
      attachMarkerDrag();
    } else {
      marker = window.L.marker(coords, {
        draggable: true,
        autoPan: true,
        title: "Arrastra el marcador para afinar la ubicación"
      }).addTo(map);
      attachMarkerDrag();
      if (marker._icon) {
        marker._icon.style.cursor = "grab";
      }
    }
    pickerState.marker = marker;
  };

  const notify = function (pickedLat, pickedLng) {
    if (!dotNetHelper) {
      return Promise.resolve();
    }

    return fetch(
      "https://nominatim.openstreetmap.org/reverse?format=json&addressdetails=1&lat=" +
        pickedLat +
        "&lon=" +
        pickedLng,
      { headers: { "Accept-Language": "es" } }
    )
      .then(function (response) {
        return response.ok ? response.json() : null;
      })
      .then(function (data) {
        const addr = data && data.address ? data.address : {};
        const label = data && data.display_name ? data.display_name : "";
        return dotNetHelper.invokeMethodAsync(
          "OnMapLocationPicked",
          pickedLat,
          pickedLng,
          label,
          addr.country || "",
          addr.state || "",
          addr.city || addr.town || "",
          addr.municipality || "",
          addr.suburb || addr.neighbourhood || "",
          addr.road || "",
          addr.house_number || ""
        );
      })
      .catch(function () {
        return dotNetHelper.invokeMethodAsync(
          "OnMapLocationPicked",
          pickedLat,
          pickedLng,
          "",
          "",
          "",
          "",
          "",
          "",
          "",
          ""
        );
      });
  };

  const applyCoords = function (pickedLat, pickedLng, zoom, forceMove) {
    if (pickerState.userAdjusted && forceMove !== true) {
      map.setView([pickedLat, pickedLng], zoom || map.getZoom());
      return;
    }

    setMarker([pickedLat, pickedLng]);
    map.setView([pickedLat, pickedLng], zoom || 16);
  };

  map.on("click", function (event) {
    pickerState.userAdjusted = true;
    pickerState.lastCoords = { latitude: event.latlng.lat, longitude: event.latlng.lng };
    setMarker(event.latlng);
    notifyCoordsOnly(event.latlng.lat, event.latlng.lng);
  });

  pickerState.map = map;
  pickerState.setMarker = setMarker;
  pickerState.notify = notify;
  pickerState.applyCoords = applyCoords;
  pickerState.refreshLayout = function () {
    const el = document.getElementById(elementId);
    if (!el || !map) {
      return;
    }

    try {
      const container = map.getContainer && map.getContainer();
      if (!container || !document.body.contains(container)) {
        return;
      }

      map.invalidateSize({ animate: false, pan: false });

      let center = null;
      if (marker) {
        try {
          center = marker.getLatLng();
        } catch (markerErr) {
          center = null;
        }
      }

      if (!center && pickerState.lastCoords) {
        center = {
          lat: pickerState.lastCoords.latitude,
          lng: pickerState.lastCoords.longitude
        };
      }

      if (!center) {
        return;
      }

      const zoom = typeof map.getZoom === "function" ? map.getZoom() : 16;
      map.setView(center, zoom || 16, { animate: false });
    } catch (layoutErr) {
      // Leaflet puede fallar si el mapa aún no terminó de montarse (p. ej. durante arrastre).
    }
  };
  pickerState.getMarkerCoords = function () {
    if (marker) {
      try {
        const pos = marker.getLatLng();
        pickerState.lastCoords = { latitude: pos.lat, longitude: pos.lng };
        return {
          latitude: pos.lat,
          longitude: pos.lng
        };
      } catch (markerErr) {
        // fallback abajo
      }
    }

    if (pickerState.lastCoords) {
      return pickerState.lastCoords;
    }

    return null;
  };
  window.comunaclic._profileAddressPickers[elementId] = pickerState;

  if (hasSaved) {
    applyCoords(latitude, longitude, 16);
  } else {
    map.setView([defaultLat, defaultLng], 12);
    if (autoLocate !== false) {
      if (dotNetHelper) {
        dotNetHelper.invokeMethodAsync("OnGeolocationStarted");
      }
      window.comunaclic
        .getCurrentPosition({ maximumAge: 120000, timeout: 15000 })
        .then(function (pos) {
          applyCoords(pos.latitude, pos.longitude, 16);
          return notify(pos.latitude, pos.longitude);
        })
        .then(function () {
          if (dotNetHelper) {
            return dotNetHelper.invokeMethodAsync("OnGeolocationFinished", true, "");
          }
        })
        .catch(function (err) {
          const msg = err && err.message ? err.message : "No se pudo obtener la ubicación.";
          if (dotNetHelper) {
            return dotNetHelper.invokeMethodAsync("OnGeolocationFinished", false, msg);
          }
        });
    }
  }

  if (typeof pickerState.refreshLayout === "function") {
    pickerState.refreshLayout();
    setTimeout(function () {
      pickerState.refreshLayout();
    }, 200);
  }
};

window.comunaclic.resetProfileAddressManualAdjust = function (elementId) {
  const picker = window.comunaclic._profileAddressPickers[elementId];
  if (picker) {
    picker.userAdjusted = false;
  }
};

window.comunaclic.profileAddressFocusMap = function (elementId, latitude, longitude, zoom, moveMarker, forceMove) {
  const picker = window.comunaclic._profileAddressPickers[elementId];
  if (!picker || typeof latitude !== "number" || isNaN(latitude) || typeof longitude !== "number" || isNaN(longitude)) {
    return;
  }

  const level = typeof zoom === "number" && !isNaN(zoom) ? zoom : 13;
  if (moveMarker === false) {
    picker.map.setView([latitude, longitude], level);
    setTimeout(function () {
      picker.map.invalidateSize();
    }, 100);
    return;
  }

  picker.applyCoords(latitude, longitude, level, forceMove === true);
};

window.comunaclic._parseNominatimCoords = function (items) {
  if (!items || !items.length) {
    return null;
  }

  const first = items[0];
  const lat = parseFloat(first.lat);
  const lng = parseFloat(first.lon);
  if (isNaN(lat) || isNaN(lng)) {
    return null;
  }

  return { latitude: lat, longitude: lng };
};

window.comunaclic._nominatimFetch = function (params) {
  params.set("format", "json");
  params.set("limit", "1");
  params.set("countrycodes", "cl");

  return fetch("https://nominatim.openstreetmap.org/search?" + params.toString(), {
    headers: { "Accept-Language": "es" }
  })
    .then(function (response) {
      return response.ok ? response.json() : [];
    })
    .then(window.comunaclic._parseNominatimCoords)
    .catch(function () {
      return null;
    });
};

window.comunaclic.geocodeAddressQuery = function (query) {
  const q = (query || "").trim();
  if (!q) {
    return Promise.resolve(null);
  }

  const normalized = q.toLowerCase();
  const hasChileHint =
    normalized.includes("chile") ||
    normalized.includes("santiago") ||
    normalized.includes("región") ||
    normalized.includes("region");
  const searchQ = hasChileHint ? q : q + ", Chile";

  return window.comunaclic._nominatimFetch(new URLSearchParams({ q: searchQ, countrycodes: "cl" }));
};

/** Geocodifica calle + número con contexto (comuna/región), estilo Google Maps. */
window.comunaclic.geocodeStructuredAddress = function (street, number, comuna, region, country) {
  const s = (street || "").trim();
  const n = (number || "").trim();
  const city = (comuna || "").trim();
  const state = (region || "").trim();
  const countryName = (country || "Chile").trim();

  if (!s) {
    return Promise.resolve(null);
  }

  const streetLine = n ? s + " " + n : s;

  function tryStructured() {
    const params = new URLSearchParams({
      street: streetLine,
      country: countryName
    });
    if (city) {
      params.set("city", city);
    }
    if (state) {
      params.set("state", state);
    }
    return window.comunaclic._nominatimFetch(params);
  }

  function tryFreeTextQueries() {
    const parts = [streetLine];
    if (city) {
      parts.push(city);
    }
    if (state && state !== city) {
      parts.push(state);
    }
    parts.push(countryName);

    const variants = [parts.join(", ")];
    if (n) {
      variants.push(s + ", " + n + ", " + (city || state || countryName));
      variants.push(n + " " + s + ", " + (city || countryName));
    }
    if (city) {
      variants.push(streetLine + ", " + city + ", " + countryName);
    }

    const seen = {};
    const unique = variants.filter(function (v) {
      const key = v.toLowerCase();
      if (seen[key]) {
        return false;
      }
      seen[key] = true;
      return true;
    });

    function next(i) {
      if (i >= unique.length) {
        return Promise.resolve(null);
      }
      return window.comunaclic.geocodeAddressQuery(unique[i]).then(function (coords) {
        return coords || next(i + 1);
      });
    }

    return next(0);
  }

  return tryStructured().then(function (coords) {
    return coords || tryFreeTextQueries();
  });
};

window.comunaclic.profileAddressUseCurrentLocation = function (elementId) {
  const picker = window.comunaclic._profileAddressPickers[elementId];
  if (!picker) {
    return Promise.reject(new Error("El mapa aún no está listo."));
  }

  const dotNetHelper = picker.dotNetHelper;
  if (dotNetHelper) {
    dotNetHelper.invokeMethodAsync("OnGeolocationStarted");
  }

  return window.comunaclic
    .getCurrentPosition({ forceFresh: true, timeout: 15000 })
    .then(function (pos) {
      picker.userAdjusted = false;
      picker.applyCoords(pos.latitude, pos.longitude, 16, true);
      return picker.notify(pos.latitude, pos.longitude).then(function () {
        if (dotNetHelper) {
          return dotNetHelper.invokeMethodAsync("OnGeolocationFinished", true, "");
        }
      });
    })
    .catch(function (err) {
      const msg = err && err.message ? err.message : "No se pudo obtener la ubicación.";
      if (dotNetHelper) {
        return dotNetHelper.invokeMethodAsync("OnGeolocationFinished", false, msg);
      }
      throw err;
    });
};

window.comunaclic.getProfileAddressMarkerCoords = function (elementId) {
  const picker = window.comunaclic._profileAddressPickers[elementId];
  if (!picker || typeof picker.getMarkerCoords !== "function") {
    return null;
  }

  return picker.getMarkerCoords();
};

window.comunaclic.invalidateProfileAddressMap = function (elementId) {
  const picker = window.comunaclic._profileAddressPickers[elementId];
  if (!picker || typeof picker.refreshLayout !== "function") {
    return;
  }

  const run = function () {
    try {
      picker.refreshLayout();
    } catch (layoutErr) {
      // ignorar durante transiciones de layout
    }
  };

  requestAnimationFrame(run);
  setTimeout(run, 120);
  setTimeout(run, 320);
};

window.comunaclic.destroyProfileAddressPicker = function (elementId) {
  const existing = window.comunaclic._profileAddressPickers[elementId];
  if (existing && existing.map) {
    existing.map.remove();
  }
  delete window.comunaclic._profileAddressPickers[elementId];
  const legacy = window.comunaclic._leafletMaps[elementId];
  if (legacy) {
    legacy.remove();
    delete window.comunaclic._leafletMaps[elementId];
  }
};

window.comunaclic.canNativeShare = function () {
  return !!(navigator.share && window.isSecureContext);
};

window.comunaclic.shareNative = function (url, title, text) {
  if (!window.comunaclic.canNativeShare()) {
    return Promise.reject(new Error("Native share unavailable"));
  }

  return navigator.share({
    url: url || undefined,
    title: title || undefined,
    text: text || undefined
  });
};

window.comunaclic.copyText = function (text) {
  if (!text) {
    return Promise.resolve(false);
  }

  if (navigator.clipboard && window.isSecureContext) {
    return navigator.clipboard.writeText(text).then(function () {
      return true;
    }).catch(function () {
      return window.comunaclic._copyTextFallback(text);
    });
  }

  return Promise.resolve(window.comunaclic._copyTextFallback(text));
};

window.comunaclic._copyTextFallback = function (text) {
  try {
    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.setAttribute("readonly", "");
    textarea.style.position = "absolute";
    textarea.style.left = "-9999px";
    document.body.appendChild(textarea);
    textarea.select();
    const ok = document.execCommand("copy");
    document.body.removeChild(textarea);
    return ok;
  } catch (err) {
    return false;
  }
};

window.comunaclic.openShareWindow = function (url) {
  if (!url) {
    return;
  }

  window.open(url, "_blank", "noopener,noreferrer,width=640,height=720");
};

window.comunaclic._splineScenes = window.comunaclic._splineScenes || {};
window.comunaclic._splineRuntimePromise = window.comunaclic._splineRuntimePromise || null;

window.comunaclic._loadSplineRuntime = function () {
  if (window.comunaclic._splineRuntimePromise) {
    return window.comunaclic._splineRuntimePromise;
  }

  window.comunaclic._splineRuntimePromise = import(
    "https://unpkg.com/@splinetool/runtime@1.9.82/build/runtime.js"
  ).then(function (mod) {
    return mod.Application;
  });

  return window.comunaclic._splineRuntimePromise;
};

/**
 * Escena Spline con progreso ligado al scroll (variable en el editor, p. ej. "scroll").
 * mode: "scroll" | "inline" (inline = parallax suave con el scroll de la página).
 */
window.comunaclic.initSplineScrollScene = function (options) {
  const elementId = options && options.elementId;
  const sceneUrl = options && options.sceneUrl;
  const scrollVariable = (options && options.scrollVariable) || "scroll";
  const mode = (options && options.mode) || "scroll";
  const trackElementId = options && options.trackElementId;

  if (!elementId || !sceneUrl || window.comunaclic.prefersReducedMotion()) {
    return Promise.resolve({ ok: false, reason: "disabled" });
  }

  const canvas = document.getElementById(elementId);
  if (!canvas) {
    return Promise.resolve({ ok: false, reason: "no-canvas" });
  }

  window.comunaclic.destroySplineScrollScene(elementId);

  return window.comunaclic
    ._loadSplineRuntime()
    .then(function (Application) {
      const app = new Application(canvas);
      return app.load(sceneUrl).then(function () {
        const state = {
          app: app,
          mode: mode,
          scrollVariable: scrollVariable,
          onScroll: null,
          onMouse: null,
          track: null
        };

        const setProgress = function (value) {
          try {
            if (typeof app.setVariable === "function") {
              app.setVariable(scrollVariable, value);
            }
          } catch {
            // ignore
          }
        };

        if (mode === "scroll" && trackElementId) {
          const track = document.getElementById(trackElementId);
          if (track) {
            const update = function () {
              const rect = track.getBoundingClientRect();
              const scrollable = Math.max(1, track.offsetHeight - window.innerHeight);
              const progress = Math.min(1, Math.max(0, -rect.top / scrollable));
              setProgress(progress);
            };
            state.onScroll = update;
            state.track = track;
            window.addEventListener("scroll", update, { passive: true });
            window.addEventListener("resize", update, { passive: true });
            update();
          }
        } else {
          const updateInline = function () {
            const scrollY = window.scrollY || 0;
            const vh = Math.max(window.innerHeight, 1);
            const progress = Math.min(1, scrollY / (vh * 0.85));
            setProgress(progress * 0.35);
          };
          state.onScroll = updateInline;
          window.addEventListener("scroll", updateInline, { passive: true });
          window.addEventListener("resize", updateInline, { passive: true });
          updateInline();

          const onMouse = function (event) {
            const nx = (event.clientX / Math.max(window.innerWidth, 1) - 0.5) * 2;
            const ny = (event.clientY / Math.max(window.innerHeight, 1) - 0.5) * 2;
            try {
              if (typeof app.setVariable === "function") {
                app.setVariable("mouseX", nx);
                app.setVariable("mouseY", ny);
              }
            } catch {
              // ignore
            }
          };
          state.onMouse = onMouse;
          window.addEventListener("mousemove", onMouse, { passive: true });
        }

        window.comunaclic._splineScenes[elementId] = state;
        return { ok: true };
      });
    })
    .catch(function () {
      return { ok: false, reason: "load-failed" };
    });
};

window.comunaclic.destroySplineScrollScene = function (elementId) {
  const state = window.comunaclic._splineScenes[elementId];
  if (!state) {
    return;
  }

  if (state.onScroll) {
    window.removeEventListener("scroll", state.onScroll);
    window.removeEventListener("resize", state.onScroll);
  }

  if (state.onMouse) {
    window.removeEventListener("mousemove", state.onMouse);
  }

  try {
    if (state.app && typeof state.app.dispose === "function") {
      state.app.dispose();
    }
  } catch {
    // ignore
  }

  delete window.comunaclic._splineScenes[elementId];
};

/** Revelado suave de bloques con data-cc-reveal dentro de un contenedor. */
window.comunaclic.initScrollReveal = function (rootId) {
  const root = rootId ? document.getElementById(rootId) : document;
  if (!root) {
    return;
  }

  const nodes = root.querySelectorAll("[data-cc-reveal]");
  if (!nodes.length) {
    return;
  }

  if (window.comunaclic.prefersReducedMotion()) {
    nodes.forEach(function (node) {
      node.classList.add("cc-reveal-visible");
    });
    return;
  }

  if (window.comunaclic._scrollRevealObserver) {
    try {
      window.comunaclic._scrollRevealObserver.disconnect();
    } catch {
      // ignore
    }
  }

  const observer = new IntersectionObserver(
    function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add("cc-reveal-visible");
          observer.unobserve(entry.target);
        }
      });
    },
    { threshold: 0.12, rootMargin: "0px 0px -8% 0px" }
  );

  nodes.forEach(function (node) {
    observer.observe(node);
  });

  window.comunaclic._scrollRevealObserver = observer;
};

window.comunaclic.destroyScrollReveal = function () {
  if (!window.comunaclic._scrollRevealObserver) {
    return;
  }

  try {
    window.comunaclic._scrollRevealObserver.disconnect();
  } catch {
    // ignore
  }

  window.comunaclic._scrollRevealObserver = null;
};

/**
 * Parallax suave por capas (data-cc-parallax + data-cc-parallax-speed).
 * Respeta prefers-reduced-motion.
 */
window.comunaclic.initHomeParallax = function (rootId) {
  const root = rootId ? document.getElementById(rootId) : document;
  if (!root || window.comunaclic.prefersReducedMotion()) {
    return;
  }

  window.comunaclic.destroyHomeParallax();

  const layers = root.querySelectorAll("[data-cc-parallax]");
  if (!layers.length) {
    return;
  }

  const hero = root.querySelector(".cc-hero-parallax");
  const pointerLayers = hero
    ? hero.querySelectorAll("[data-cc-parallax-pointer]")
    : [];
  let pointerX = 0;
  let pointerY = 0;
  let ticking = false;

  const update = function () {
    ticking = false;
    const scrollY = window.scrollY || 0;
    const vh = Math.max(window.innerHeight, 1);
    let heroScrollRatio = 0;

    if (hero) {
      const heroTop = hero.offsetTop;
      const heroHeight = Math.max(hero.offsetHeight, 1);
      heroScrollRatio = Math.min(1, Math.max(0, scrollY / heroHeight));
    }

    layers.forEach(function (el) {
      const speed = parseFloat(el.getAttribute("data-cc-parallax-speed") || "0.2");
      const axis = el.getAttribute("data-cc-parallax-axis") || "y";
      const rect = el.getBoundingClientRect();
      const elCenter = rect.top + rect.height * 0.5;
      const viewportOffset = (elCenter - vh * 0.5) / vh;
      const shift = viewportOffset * vh * speed;
      const inHero = hero && hero.contains(el);
      const heroBoost = inHero ? heroScrollRatio * 72 * speed : 0;
      const pointerBoost = el.hasAttribute("data-cc-parallax-pointer")
        ? {
            x: pointerX * parseFloat(el.getAttribute("data-cc-parallax-pointer-x") || "14"),
            y: pointerY * parseFloat(el.getAttribute("data-cc-parallax-pointer-y") || "14"),
          }
        : { x: 0, y: 0 };

      if (axis === "x") {
        el.style.transform =
          "translate3d(" + (shift + heroBoost + pointerBoost.x) + "px, " + pointerBoost.y + "px, 0)";
      } else if (axis === "scale") {
        const scale =
          1.04 +
          Math.min(0.14, Math.abs(viewportOffset) * Math.abs(speed) * 0.22) +
          heroScrollRatio * 0.06;
        el.style.transform =
          "translate3d(" +
          pointerBoost.x +
          "px, " +
          (shift + heroBoost + pointerBoost.y) +
          "px, 0) scale(" +
          scale +
          ")";
      } else {
        el.style.transform =
          "translate3d(" +
          pointerBoost.x +
          "px, " +
          (shift + heroBoost + pointerBoost.y) +
          "px, 0)";
      }
    });

    if (hero) {
      const heroRect = hero.getBoundingClientRect();
      const heroProgress = Math.min(
        1,
        Math.max(0, (vh - heroRect.top) / (heroRect.height + vh * 0.35))
      );
      hero.style.setProperty("--cc-hero-scroll", heroProgress.toFixed(4));
    }

    const splineCopy = root.querySelector(".cc-spline-scroll-copy");
    const track = root.querySelector(".cc-spline-scroll-track");
    if (splineCopy && track) {
      const rect = track.getBoundingClientRect();
      const scrollable = Math.max(1, track.offsetHeight - vh);
      const progress = Math.min(1, Math.max(0, -rect.top / scrollable));
      const opacity = 1 - Math.min(1, progress * 1.35);
      const ty = (1 - opacity) * 28;
      splineCopy.style.opacity = String(opacity);
      splineCopy.style.transform = "translate3d(0, " + ty + "px, 0)";
    }

    const storyPanels = root.querySelectorAll(".cc-parallax-story-panel");
    storyPanels.forEach(function (panel) {
      const rect = panel.getBoundingClientRect();
      const progress = Math.min(1, Math.max(0, 1 - (rect.top - vh * 0.35) / (vh * 0.65)));
      panel.style.setProperty("--cc-story-progress", progress.toFixed(4));
    });
  };

  const onScroll = function () {
    if (!ticking) {
      ticking = true;
      window.requestAnimationFrame(update);
    }
  };

  const onPointerMove = function (event) {
    if (!hero || !pointerLayers.length) {
      return;
    }

    const rect = hero.getBoundingClientRect();
    if (
      event.clientY < rect.top - 40 ||
      event.clientY > rect.bottom + 40 ||
      event.clientX < rect.left - 40 ||
      event.clientX > rect.right + 40
    ) {
      return;
    }

    pointerX = (event.clientX / Math.max(window.innerWidth, 1) - 0.5) * 2;
    pointerY = (event.clientY / Math.max(window.innerHeight, 1) - 0.5) * 2;

    if (!ticking) {
      ticking = true;
      window.requestAnimationFrame(update);
    }
  };

  window.addEventListener("scroll", onScroll, { passive: true });
  window.addEventListener("resize", onScroll, { passive: true });
  if (pointerLayers.length) {
    window.addEventListener("pointermove", onPointerMove, { passive: true });
  }
  update();

  window.comunaclic._homeParallaxState = {
    onScroll: onScroll,
    onPointerMove: pointerLayers.length ? onPointerMove : null,
  };
};

window.comunaclic.destroyHomeParallax = function () {
  const state = window.comunaclic._homeParallaxState;
  if (!state) {
    return;
  }

  window.removeEventListener("scroll", state.onScroll);
  window.removeEventListener("resize", state.onScroll);
  if (state.onPointerMove) {
    window.removeEventListener("pointermove", state.onPointerMove);
  }
  window.comunaclic._homeParallaxState = null;
};
