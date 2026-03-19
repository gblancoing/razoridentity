window.comunaclic = window.comunaclic || {};

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
