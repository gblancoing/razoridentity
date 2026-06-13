window.comunaclicAdmin = window.comunaclicAdmin || {};

window.comunaclicAdmin.downloadWithAuth = async function (url, token, filename) {
  const response = await fetch(url, { headers: { Authorization: "Bearer " + token } });
  if (!response.ok) throw new Error("Descarga fallida (" + response.status + ")");
  const blob = await response.blob();
  const objectUrl = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = objectUrl;
  anchor.download = filename || "export.csv";
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(objectUrl);
};

window.comunaclicAdmin.getTokens = function () {
  try {
    return localStorage.getItem("payments.tokens") || "";
  } catch {
    return "";
  }
};

window.comunaclicAdmin.setTokens = function (json) {
  try {
    localStorage.setItem("payments.tokens", json);
  } catch {
  }
};

window.comunaclicAdmin.clearTokens = function () {
  try {
    localStorage.removeItem("payments.tokens");
  } catch {
  }
};

window.comunaclicAdmin.executeRecaptcha = function (action) {
  if (!window.comunaclicAdminRecaptchaEnabled || !window.comunaclicAdminRecaptchaSiteKey) {
    return Promise.resolve("");
  }

  return new Promise((resolve, reject) => {
    if (!window.grecaptcha || typeof window.grecaptcha.ready !== "function") {
      reject(new Error("reCAPTCHA is not available."));
      return;
    }

    window.grecaptcha.ready(() => {
      window.grecaptcha
        .execute(window.comunaclicAdminRecaptchaSiteKey, { action: action || "login" })
        .then(resolve)
        .catch(reject);
    });
  });
};
