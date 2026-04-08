window.comunaclicAdmin = window.comunaclicAdmin || {};

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
