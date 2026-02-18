# Migración a solo Tailwind CSS

Archivos que hay que tocar para que el proyecto use **únicamente** Tailwind (sin Bootstrap).

---

## 1. Layout y recursos globales

### `Pages/Shared/_Layout.cshtml`
- **Quitar:** `<link>` a `bootstrap.min.css` y `<script>` de `bootstrap.bundle.min.js`.
- **Dejar:** `tailwind.css`, `site.css`, `RazorIdentity.styles.css`; jQuery y `site.js` (el menú móvil puede hacerse con JS propio).
- **Sustituir clases Bootstrap del layout por Tailwind:**
  - `navbar navbar-expand-sm ...` → barra de navegación con Tailwind (flex, bg-white, border-b, etc.).
  - `container` → `max-w-7xl mx-auto px-4 sm:px-6 lg:px-8` (o similar).
  - `navbar-collapse`, `navbar-toggler` → menú móvil con Tailwind + un pequeño script para abrir/cerrar (o `details/summary`).
  - `navbar-nav`, `nav-item`, `nav-link` → clases Tailwind (flex, gap, py-2, etc.).
  - `footer` → clases Tailwind (border-t, text-sm text-gray-500, py-4, etc.).
- **Nota:** Sin Bootstrap JS, el botón “hamburguesa” debe implementarse con JavaScript propio (por ejemplo en `site.js`) usando clases Tailwind para mostrar/ocultar el menú.

### `wwwroot/css/site.css`
- **Quitar:** reglas que dependen de Bootstrap (`.btn:focus`, `.form-control:focus`, `.form-check-input:focus`, etc.).
- **Mantener o adaptar:** `html`/`body` (tamaño de fuente, min-height, margin-bottom) si quieres; con Tailwind puedes llevarlo a utilidades en el layout.

### `wwwroot/js/site.js`
- **Añadir:** lógica para el menú móvil (toggle del menú en pantallas pequeñas) si se usa un botón “hamburguesa” con Tailwind. Por ejemplo: al hacer clic, alternar una clase que muestre/oculte el menú (p.ej. `hidden` / `block` o `max-h-0` / `max-h-96`).

---

## 2. Partial de login (navbar)

### `Pages/Shared/_LoginPartial.cshtml`
- **Sustituir:** `navbar-nav` → flex con Tailwind (`flex items-center gap-4` o similar).
- **Sustituir:** `nav-item`, `nav-link` → enlaces con Tailwind (ej. `text-gray-700 hover:text-gray-900`).
- **Sustituir:** botón “Cerrar sesión” `btn btn-link` → estilo Tailwind (ej. `text-gray-700 hover:underline` o como botón secundario).
- **Sustituir:** `form-inline` → solo flex/gap si hace falta.

---

## 3. Páginas principales (Pages)

### `Pages/Index.cshtml`
- **Sustituir:** `text-center`, `display-4` (Bootstrap) por Tailwind: `text-center`, `text-4xl font-bold` (o similar).

### `Pages/Privacy.cshtml`
- Sin clases Bootstrap; opcional: añadir contenedor o tipografía con Tailwind.

### `Pages/Error.cshtml`
- **Sustituir:** `text-danger` → `text-red-600` (o `text-red-700`).

### `Pages/Protegido.cshtml`
- Sin Bootstrap; opcional: añadir contenedor/título con Tailwind.

---

## 4. Identity – Account

### `Areas/Identity/Pages/Account/Login.cshtml`
- **Estructura:** `row` / `col-md-4` / `col-md-6` → grid/flex Tailwind (ej. `grid grid-cols-1 md:grid-cols-3 gap-6`).
- **Formulario:** `form-floating mb-3` → contenedor con `mb-4` y estilos Tailwind.
- **Inputs:** `form-control` → `w-full rounded border border-gray-300 px-3 py-2 focus:ring-2 focus:ring-blue-500 focus:border-blue-500`.
- **Labels:** `form-label` → `block text-sm font-medium text-gray-700 mb-1`.
- **Validación:** `text-danger` → `text-red-600 text-sm`.
- **Checkbox:** `form-check-input` → checkbox con Tailwind (bordes, tamaño).
- **Botón:** `btn btn-lg btn-primary` → `w-full py-2 px-4 bg-blue-600 text-white font-medium rounded-lg hover:bg-blue-700 focus:ring-2 focus:ring-blue-500`.
- **Botones externos:** `btn btn-primary` → mismas clases de botón primario Tailwind.

### `Areas/Identity/Pages/Account/Register.cshtml`
- Mismos cambios que en Login: grid, form-floating → bloques con Tailwind, `form-control` → inputs Tailwind, `form-label`, `text-danger`, `btn btn-lg btn-primary` y `btn btn-primary` → botones Tailwind.

### `Areas/Identity/Pages/Account/Logout.cshtml`
- **Sustituir:** `form-inline`, `nav-link btn btn-link text-dark` por clases Tailwind para el formulario y el botón (ej. `inline-block text-gray-700 hover:underline` o botón secundario).

### `Areas/Identity/Pages/Account/AccessDenied.cshtml`
- **Sustituir:** `text-danger` → `text-red-600` (o la clase de error que uses en el resto del sitio).

---

## 5. Identity – Manage

### `Areas/Identity/Pages/Account/Manage/Index.cshtml`
- **Sustituir:** `row`, `col-md-6` → grid/flex Tailwind.
- **Sustituir:** `form-floating mb-3`, `form-control`, `form-label`, `text-danger`, botón `btn btn-lg btn-primary` por las mismas convenciones Tailwind que en Login/Register.
- **Partial:** `_StatusMessage` sin cambios de Bootstrap si no tiene clases; si las tiene, sustituir por Tailwind.

### `Areas/Identity/Pages/Account/Manage/_ManageNav.cshtml`
- **Sustituir:** `nav nav-pills flex-column` → lista con Tailwind (ej. `space-y-1`).
- **Sustituir:** `nav-item`, `nav-link` → enlaces con Tailwind.
- **Activo:** `ManageNavPages.IndexNavClass(ViewContext)` devuelve `"active"` o `null`. Añadir estilos Tailwind cuando sea activo, por ejemplo:
  - `class="block px-3 py-2 rounded @(ManageNavPages.IndexNavClass(ViewContext) == "active" ? "font-semibold bg-blue-100 text-blue-700" : "text-gray-600 hover:bg-gray-100")"` (y equivalente para cada enlace).

---

## 6. Archivos que NO hay que tocar (solo Tailwind/Bootstrap)

- **`_ValidationScriptsPartial.cshtml`** (Pages y Areas): solo jQuery Validation; sin clases Bootstrap.
- **`_ViewImports.cshtml`**, **`_ViewStart.cshtml`**: sin estilos.
- **`ManageNavPages.cs`**: sigue devolviendo `"active"` o `null`; la presentación se resuelve en `_ManageNav.cshtml` con Tailwind.

---

## 7. Resumen por tipo de cambio

| Archivo | Quitar Bootstrap | Sustituir clases por Tailwind |
|---------|-------------------|-------------------------------|
| `_Layout.cshtml` | CSS + JS | Navbar, container, footer, menú móvil |
| `site.css` | Reglas .btn, .form-control, etc. | Opcional: base mínima |
| `site.js` | — | Añadir toggle menú móvil |
| `_LoginPartial.cshtml` | — | nav, links, botón logout |
| `Index.cshtml` | — | text-center, display-4 |
| `Error.cshtml` | — | text-danger |
| `Login.cshtml` | — | row/col, form, inputs, btn |
| `Register.cshtml` | — | Idem Login |
| `Logout.cshtml` | — | form, btn |
| `AccessDenied.cshtml` | — | text-danger |
| `Manage/Index.cshtml` | — | row/col, form, inputs, btn |
| `Manage/_ManageNav.cshtml` | — | nav, nav-link, active |

---

## 8. Orden sugerido de intervención

1. **Layout y global:** `_Layout.cshtml`, `site.css`, `site.js` (y quitar referencia a Bootstrap).
2. **Partial:** `_LoginPartial.cshtml`.
3. **Páginas públicas:** `Index`, `Privacy`, `Error`, `Protegido`.
4. **Identity Account:** `Login`, `Register`, `Logout`, `AccessDenied`.
5. **Identity Manage:** `Manage/Index`, `Manage/_ManageNav`.

Así el sitio pasa a depender solo de Tailwind; jQuery puede seguir para la validación de formularios si lo usas.
