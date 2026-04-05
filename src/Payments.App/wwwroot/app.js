window.paymentsApp = window.paymentsApp || {};

window.paymentsApp.getTokens = function () {
  try {
    return localStorage.getItem("payments.tokens") || "";
  } catch {
    return "";
  }
};

window.paymentsApp.setTokens = function (json) {
  try {
    localStorage.setItem("payments.tokens", json);
  } catch {
  }
};

window.paymentsApp.clearTokens = function () {
  try {
    localStorage.removeItem("payments.tokens");
  } catch {
  }
};

window.paymentsApp.executeRecaptcha = function (action) {
  if (!window.paymentsAppRecaptchaEnabled || !window.paymentsAppRecaptchaSiteKey) {
    return Promise.resolve("");
  }

  return new Promise((resolve, reject) => {
    if (!window.grecaptcha || typeof window.grecaptcha.ready !== "function") {
      reject(new Error("reCAPTCHA is not available."));
      return;
    }

    window.grecaptcha.ready(() => {
      window.grecaptcha
        .execute(window.paymentsAppRecaptchaSiteKey, { action: action || "login" })
        .then(resolve)
        .catch(reject);
    });
  });
};
