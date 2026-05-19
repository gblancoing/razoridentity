window.comunaclic = window.comunaclic || {};
window.comunaclic._leafletMaps = window.comunaclic._leafletMaps || {};

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

window.comunaclic.getTokens = function () {
  try {
    return localStorage.getItem("comunaclic.tokens") || "";
  } catch {
    return "";
  }
};

window.comunaclic.setTokens = function (json) {
  try {
    localStorage.setItem("comunaclic.tokens", json);
  } catch {
    // Ignore storage errors
  }
};

window.comunaclic.clearTokens = function () {
  try {
    localStorage.removeItem("comunaclic.tokens");
  } catch {
    // Ignore storage errors
  }
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

window.comunaclic.beginGoogleSignIn = function (redirectUrl) {
  const clientId = (window.comunaclicGoogleClientId || "").trim();
  if (!clientId) {
    throw new Error("Google sign-in is not configured.");
  }

  const callbackUrl = (redirectUrl || window.location.href || "").split("#")[0];
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

window.comunaclic.getCurrentPosition = function () {
  return new Promise((resolve, reject) => {
    if (!navigator.geolocation) {
      reject(new Error("Geolocation is not supported by this browser."));
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        resolve({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracy: position.coords.accuracy || 0
        });
      },
      (error) => {
        reject(new Error(error && error.message ? error.message : "Location permission was denied."));
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 300000
      }
    );
  });
};

window.comunaclic.renderCategoryNearbyMap = function (elementId, userLocation, businesses) {
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

  if (window.innerWidth && window.innerWidth < 640) {
    element.style.minHeight = "320px";
  }

  const map = window.L.map(elementId, {
    zoomControl: true,
    scrollWheelZoom: false,
    tap: true,
    dragging: !(window.innerWidth && window.innerWidth < 640)
  });

  window.comunaclic._leafletMaps[elementId] = map;

  window.L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: "&copy; OpenStreetMap contributors"
  }).addTo(map);

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
  const userLat = userLocation.latitude ?? userLocation.Latitude;
  const userLng = userLocation.longitude ?? userLocation.Longitude;
  const userMarker = window.L.circleMarker([userLat, userLng], {
    radius: 10,
    color: "#ffffff",
    weight: 3,
    fillColor: "#2d9e4f",
    fillOpacity: 1
  }).addTo(map);

  userMarker.bindPopup("<strong>Tu ubicación aproximada</strong>");
  markers.push(userMarker);

  (businesses || []).forEach((business) => {
    const latitude = business.latitude ?? business.Latitude;
    const longitude = business.longitude ?? business.Longitude;
    if (typeof latitude !== "number" || typeof longitude !== "number") {
      return;
    }

    const name = business.name ?? business.Name ?? "Negocio";
    const comunaName = business.comunaName ?? business.ComunaName ?? "";
    const address = business.address ?? business.Address ?? "";
    const distanceKm = business.distanceKm ?? business.DistanceKm;
    const usesExactLocation = business.usesExactLocation ?? business.UsesExactLocation;
    const href = `/buyer/detail/partner/${business.id ?? business.Id}`;
    const popup = [
      `<div style="min-width:200px">`,
      `<strong>${name}</strong>`,
      comunaName ? `<div style="margin-top:4px;color:#595c5d">${comunaName}</div>` : "",
      address ? `<div style="margin-top:4px;color:#595c5d">${address}</div>` : "",
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

  const group = window.L.featureGroup(markers);
  map.fitBounds(group.getBounds().pad(0.18));

  setTimeout(() => {
    map.invalidateSize();
  }, 150);
};

window.comunaclic.destroyCategoryNearbyMap = function (elementId) {
  const existingMap = window.comunaclic._leafletMaps[elementId];
  if (existingMap) {
    existingMap.remove();
    delete window.comunaclic._leafletMaps[elementId];
  }
};
