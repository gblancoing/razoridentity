#!/usr/bin/env bash
set -euo pipefail

APP_BASE_URL="${APP_BASE_URL:-https://app.comunaclic.cl}"
ACL_BASE_URL="${ACL_BASE_URL:-https://acl.comunaclic.cl}"
API_BASE_URL="${API_BASE_URL:-https://api.comunaclic.cl}"

QA_EMAIL="${QA_EMAIL:-}"
QA_PASSWORD="${QA_PASSWORD:-}"
QA_RECAPTCHA_TOKEN="${QA_RECAPTCHA_TOKEN:-}"
STRICT_AUTH_CHECKS="${STRICT_AUTH_CHECKS:-0}"

CURL_BIN="${CURL_BIN:-curl}"
CURL_OPTS=(--silent --show-error --location --max-time 30)

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

PASS_COUNT=0
SKIP_COUNT=0

log() { printf '[INFO] %s\n' "$*"; }
pass() { PASS_COUNT=$((PASS_COUNT + 1)); printf '[PASS] %s\n' "$*"; }
skip() { SKIP_COUNT=$((SKIP_COUNT + 1)); printf '[SKIP] %s\n' "$*"; }
fail() { printf '[FAIL] %s\n' "$*" >&2; exit 1; }

usage() {
  cat <<EOF
Smoke test post-deploy para ComunaClic.

Variables opcionales:
  APP_BASE_URL            URL base app            (default: https://app.comunaclic.cl)
  ACL_BASE_URL            URL base ACL            (default: https://acl.comunaclic.cl)
  API_BASE_URL            URL base API            (default: https://api.comunaclic.cl)
  QA_EMAIL                usuario QA para checks autenticados opcionales
  QA_PASSWORD             password QA para checks autenticados opcionales
  QA_RECAPTCHA_TOKEN      token reCAPTCHA si el login real lo exige
  STRICT_AUTH_CHECKS=1    falla si no hay credenciales QA

Ejemplos:
  bash tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh
  QA_EMAIL=owner@comunaclic.test QA_PASSWORD=test123 bash tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh
EOF
}

http_request() {
  local name="$1"
  local method="$2"
  local url="$3"
  local body="${4:-}"
  shift 4 || true

  local headers_file="$WORK_DIR/${name}.headers"
  local body_file="$WORK_DIR/${name}.body"
  local status_file="$WORK_DIR/${name}.status"

  local -a args=("${CURL_BIN}" "${CURL_OPTS[@]}" -D "$headers_file" -o "$body_file" -X "$method")
  while (($#)); do
    args+=("$1")
    shift
  done
  if [[ -n "$body" ]]; then
    args+=(-H "Content-Type: application/json" --data "$body")
  fi
  args+=(-w "%{http_code}" "$url")

  local status
  status="$("${args[@]}")" || fail "Request failed: ${method} ${url}"
  printf '%s' "$status" > "$status_file"
}

status_of() { cat "$WORK_DIR/$1.status"; }
body_of() { cat "$WORK_DIR/$1.body"; }
headers_of() { cat "$WORK_DIR/$1.headers"; }

assert_status() {
  local name="$1"
  local expected="$2"
  local actual
  actual="$(status_of "$name")"
  [[ "$actual" == "$expected" ]] || fail "$name devolvió HTTP $actual y se esperaba $expected"
}

assert_body_contains() {
  local name="$1"
  local needle="$2"
  grep -F "$needle" "$WORK_DIR/$name.body" >/dev/null || fail "$name no contiene '$needle'"
}

assert_body_contains_any() {
  local name="$1"
  shift
  for needle in "$@"; do
    if grep -F "$needle" "$WORK_DIR/$name.body" >/dev/null; then
      return 0
    fi
  done
  fail "$name no contiene ninguno de: $*"
}

extract_first_guid() {
  local name="$1"
  sed -n 's/.*"id":"\([0-9a-fA-F-]\{36\}\)".*/\1/p' "$WORK_DIR/$name.body" | head -n 1
}

extract_first_value() {
  local name="$1"
  local field="$2"
  sed -n "s/.*\"${field}\":\"\\([^\"]*\\)\".*/\\1/p" "$WORK_DIR/$name.body" | head -n 1
}

extract_token() {
  local name="$1"
  sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p' "$WORK_DIR/$name.body" | head -n 1
}

check_app_public() {
  log "Validando app pública en ${APP_BASE_URL}"

  http_request "app_home" GET "${APP_BASE_URL}/" ""
  assert_status "app_home" 200
  assert_body_contains "app_home" "ComunaClic"
  grep -Fi "content-security-policy:" "$WORK_DIR/app_home.headers" >/dev/null || fail "app_home no devolvió Content-Security-Policy"
  pass "Home pública responde 200 con branding y CSP"

  http_request "app_login" GET "${APP_BASE_URL}/login" ""
  assert_status "app_login" 200
  assert_body_contains_any "app_login" "Persona natural" "Cuenta personal" "Individual"
  assert_body_contains "app_login" "Registrar negocio"
  pass "Login pública responde 200 con CTAs esperados"

  http_request "app_register_business" GET "${APP_BASE_URL}/register/business" ""
  assert_status "app_register_business" 200
  assert_body_contains "app_register_business" "Registro de Negocio"
  pass "Registro de negocio responde 200"
}

check_acl_public() {
  log "Validando ACL en ${ACL_BASE_URL}"

  http_request "acl_login_invalid" POST "${ACL_BASE_URL}/v1/auth/login" '{"email":"","password":"","tenantId":null,"partnerId":null,"recaptchaToken":""}'
  local status
  status="$(status_of "acl_login_invalid")"
  if [[ "$status" != "400" && "$status" != "401" ]]; then
    fail "acl_login_invalid devolvió HTTP $status y se esperaba 400 o 401"
  fi
  pass "ACL responde en login con validación antiabuso/credenciales"

  http_request "acl_register_invalid" POST "${ACL_BASE_URL}/v1/auth/register" '{"name":"","email":"","password":"123","recaptchaToken":""}'
  status="$(status_of "acl_register_invalid")"
  if [[ "$status" != "400" && "$status" != "409" ]]; then
    fail "acl_register_invalid devolvió HTTP $status y se esperaba 400 o 409"
  fi
  pass "ACL responde en register con validación de payload"
}

check_api_public() {
  log "Validando API pública en ${API_BASE_URL}"

  http_request "api_countries" GET "${API_BASE_URL}/v1/public/geo/countries" ""
  assert_status "api_countries" 200
  assert_body_contains "api_countries" "["
  local country_id
  country_id="$(extract_first_guid "api_countries")"
  [[ -n "$country_id" ]] || fail "No se pudo extraer countryId desde api_countries"
  pass "Geo countries responde 200 con al menos un país"

  http_request "api_regions" GET "${API_BASE_URL}/v1/public/geo/regions?countryId=${country_id}" ""
  assert_status "api_regions" 200
  local region_id
  region_id="$(extract_first_guid "api_regions")"
  [[ -n "$region_id" ]] || fail "No se pudo extraer regionId desde api_regions"
  pass "Geo regions responde 200 con al menos una región"

  http_request "api_comunas" GET "${API_BASE_URL}/v1/public/geo/comunas?regionId=${region_id}" ""
  assert_status "api_comunas" 200
  local comuna_id
  comuna_id="$(extract_first_guid "api_comunas")"
  [[ -n "$comuna_id" ]] || fail "No se pudo extraer comunaId desde api_comunas"
  pass "Geo comunas responde 200 con al menos una comuna"

  http_request "api_tenant_by_comuna_get" GET "${API_BASE_URL}/v1/public/geo/tenant-by-comuna/${comuna_id}" ""
  assert_status "api_tenant_by_comuna_get" 200
  assert_body_contains "api_tenant_by_comuna_get" "tenantId"
  pass "tenant-by-comuna GET responde 200"

  http_request "api_tenant_by_comuna_post" POST "${API_BASE_URL}/v1/public/geo/tenant-by-comuna/${comuna_id}" ""
  assert_status "api_tenant_by_comuna_post" 200
  assert_body_contains "api_tenant_by_comuna_post" "tenantId"
  pass "tenant-by-comuna POST responde 200"

  http_request "api_categories" GET "${API_BASE_URL}/v1/public/catalog/categories" ""
  assert_status "api_categories" 200
  local category_code
  category_code="$(extract_first_value "api_categories" "code")"
  [[ -n "$category_code" ]] || fail "No se pudo extraer categoryCode desde api_categories"
  pass "Categorías públicas responden 200"

  http_request "api_discovery" GET "${API_BASE_URL}/v1/public/catalog/discovery/${category_code}" ""
  assert_status "api_discovery" 200
  assert_body_contains "api_discovery" "category"
  pass "Discovery público responde 200"
}

check_authenticated_flow() {
  if [[ -z "$QA_EMAIL" || -z "$QA_PASSWORD" ]]; then
    if [[ "$STRICT_AUTH_CHECKS" == "1" ]]; then
      fail "STRICT_AUTH_CHECKS=1 exige QA_EMAIL y QA_PASSWORD"
    fi
    skip "Checks autenticados omitidos: faltan QA_EMAIL/QA_PASSWORD"
    return
  fi

  log "Validando flujo autenticado opcional"

  local login_payload
  login_payload=$(cat <<EOF
{"email":"${QA_EMAIL}","password":"${QA_PASSWORD}","tenantId":null,"partnerId":null,"recaptchaToken":"${QA_RECAPTCHA_TOKEN}"}
EOF
)

  http_request "acl_login_real" POST "${ACL_BASE_URL}/v1/auth/login" "$login_payload"
  local status
  status="$(status_of "acl_login_real")"
  if [[ "$status" != "200" ]]; then
    fail "acl_login_real devolvió HTTP $status"
  fi

  local token
  token="$(extract_token "acl_login_real")"
  [[ -n "$token" ]] || fail "No se pudo extraer accessToken desde acl_login_real"
  pass "Login autenticado devuelve accessToken"

  http_request "api_partners_mine" GET "${API_BASE_URL}/v1/partners/mine" "" -H "Authorization: Bearer ${token}"
  status="$(status_of "api_partners_mine")"
  if [[ "$status" != "200" && "$status" != "204" ]]; then
    fail "api_partners_mine devolvió HTTP $status"
  fi
  pass "Partners/mine responde con token válido"

  http_request "app_my_businesses" GET "${APP_BASE_URL}/my-businesses" "" -H "Authorization: Bearer ${token}"
  status="$(status_of "app_my_businesses")"
  if [[ "$status" != "200" && "$status" != "302" ]]; then
    fail "app_my_businesses devolvió HTTP $status"
  fi
  pass "Ruta /my-businesses responde en post-login"
}

main() {
  if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    usage
    exit 0
  fi

  check_app_public
  check_acl_public
  check_api_public
  check_authenticated_flow

  printf '\n[OK] Smoke test terminado. Checks OK: %d | Skipped: %d\n' "$PASS_COUNT" "$SKIP_COUNT"
}

main "$@"
