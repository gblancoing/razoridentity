# Requerimiento: Panel de Administración Separado para ComunaClic

## 1. Objetivo

Separar completamente el panel de administración de ComunaClic del sitio público y operativo disponible en `app.comunaclic.cl`, creando una aplicación dedicada para superadministradores y administradores de plataforma.

Propuesta de nuevo acceso:

- Sitio público y operativo: `https://app.comunaclic.cl`
- Panel administrativo: `https://admin.comunaclic.cl`

## 2. Justificación

Actualmente, el sitio público, la operación de buyer/partner y las capacidades administrativas tienden a convivir dentro del mismo ecosistema visual y funcional. Esto genera varios problemas:

- mezcla de navegación pública con navegación sensible
- mayor riesgo de exponer funciones administrativas
- dificultad para aplicar permisos, layouts y auditoría diferenciada
- complejidad de mantenimiento y despliegue
- experiencia poco clara para cuentas de superadministrador

Separar el panel admin permite:

- endurecer seguridad y control de acceso
- diseñar una UX operativa específica para gestión
- desacoplar despliegues del sitio público
- ordenar responsabilidades técnicas por aplicación

## 3. Alcance

Se propone crear una nueva aplicación:

- `src/ComunaClick.Admin`

Esta aplicación será de uso exclusivo para:

- `platform_admin`
- `super_admin`

El panel no reemplaza `app.comunaclic.cl`, sino que lo complementa.

## 4. Objetivos Funcionales

El panel debe permitir que una cuenta superadministradora pueda:

- administrar usuarios administrativos
- revisar y modificar tenants
- revisar, aprobar, suspender o destacar partners
- gestionar categorías y subcategorías visibles
- modificar contenido principal del sitio
- administrar banners, hero, noticias y bloques destacados
- revisar actividad operativa general
- revisar pagos, suscripciones y estados globales
- acceder a auditoría de cambios

## 5. Principios de Diseño

El panel admin debe ser:

- completamente separado del frontend público
- más sobrio y operativo que comercial
- centrado en tablas, filtros, formularios y trazabilidad
- seguro por defecto
- preparado para crecimiento modular

## 6. Roles

### 6.1 Superadministrador

Permisos globales:

- acceso total al panel
- gestión de usuarios admin
- gestión de tenants y partners
- edición de contenido del sitio
- configuración global
- acceso a auditoría

### 6.2 Administrador de plataforma

Permisos operativos:

- gestión de partners
- gestión de categorías
- revisión de contenidos
- consulta de pagos
- acceso acotado según policy

## 7. Módulos del Panel

### 7.1 Dashboard General

Debe mostrar:

- cantidad de usuarios registrados
- cantidad de partners activos
- cantidad de negocios publicados
- actividad reciente
- leads, reservas y órdenes del ecosistema
- indicadores de pagos y suscripciones

### 7.2 Gestión de Usuarios

Debe permitir:

- listar usuarios
- buscar por email, nombre o rol
- activar/desactivar cuentas
- asignar o remover roles
- resetear contraseña
- revisar scopes

### 7.3 Gestión de Tenants

Debe permitir:

- listar tenants
- revisar su configuración base
- ver estado de activación
- consultar partners asociados

### 7.4 Gestión de Partners

Debe permitir:

- listar partners por estado
- filtrar por categoría, comuna o tipo
- aprobar, suspender o despublicar
- editar datos visibles
- revisar ownership y staff
- destacar negocios en home o categorías

### 7.5 Gestión de Categorías

Debe permitir:

- crear, editar y ordenar categorías
- administrar subcategorías
- definir labels visibles
- definir imágenes fallback
- activar/desactivar visibilidad pública

### 7.6 Gestión de Contenido Público

Debe permitir editar:

- hero principal
- textos destacados
- banners
- noticias
- negocios locales destacados
- links institucionales
- footer y redes sociales

### 7.7 Pagos y Suscripciones

Debe integrarse con:

- `Payments.Gateway.Api`
- `Payments.App`

Debe permitir:

- ver transacciones
- ver suscripciones
- revisar estado por proveedor
- detectar pagos fallidos
- consultar callbacks/webhooks

### 7.8 Auditoría

Debe permitir:

- ver quién hizo cambios
- cuándo los hizo
- qué entidad fue modificada
- valor anterior y nuevo cuando aplique

## 8. Arquitectura Propuesta

### 8.1 Frontend

Nueva app:

- `src/ComunaClick.Admin`

Características:

- layout administrativo propio
- login dedicado
- navegación lateral fija
- módulos desacoplados
- guardas de autorización por rol

### 8.2 Backend

Se recomienda exponer endpoints administrativos en:

- `https://api.comunaclic.cl/admin/...`

Ejemplos:

- `/admin/users`
- `/admin/partners`
- `/admin/catalog`
- `/admin/content`
- `/admin/tenants`
- `/admin/audit`

### 8.3 Seguridad

Requisitos:

- autenticación vía ACL
- solo roles admin pueden entrar
- políticas explícitas por módulo
- sesión separada del sitio público
- logging y auditoría
- rate limiting en endpoints críticos

## 9. Navegación Propuesta

Menú lateral inicial:

- Dashboard
- Usuarios
- Tenants
- Partners
- Categorías
- Contenido
- Noticias
- Pagos
- Auditoría
- Configuración

## 10. Casos de Uso Iniciales

### Caso 1: Aprobar un partner

Un `platform_admin` entra al panel, revisa un nuevo partner, valida sus datos y lo cambia a visible/publicado.

### Caso 2: Modificar el hero del sitio

Un `super_admin` actualiza título, subtítulo, CTA e imagen principal de la home pública.

### Caso 3: Destacar un negocio

Un admin selecciona un partner activo y lo marca como destacado para la home.

### Caso 4: Resetear contraseña de un admin

Un `super_admin` restablece la contraseña de una cuenta administrativa desde el módulo de usuarios.

## 11. Fases de Implementación

### Fase 1: Base del Panel

- crear `ComunaClick.Admin`
- login admin
- layout administrativo
- protección por roles
- dashboard base

### Fase 2: Gestión Operativa

- módulo usuarios
- módulo partners
- módulo categorías
- listado y filtros

### Fase 3: Gestión de Contenido

- home
- noticias
- destacados
- footer

### Fase 4: Auditoría y Configuración

- registro de acciones
- configuración global
- trazabilidad

### Fase 5: Integración con Pagos

- resumen de pagos
- suscripciones
- estados por proveedor

## 12. Criterios de Aceptación

Se considerará cumplido este requerimiento cuando:

- exista una app separada en `admin.comunaclic.cl`
- el acceso esté restringido a cuentas administrativas
- el sitio público no mezcle navegación administrativa
- un `super_admin` pueda gestionar usuarios, partners, categorías y contenido
- exista trazabilidad mínima de cambios

## 13. Recomendación Final

Sí se recomienda separar el panel administrativo de `app.comunaclic.cl`.

La solución propuesta es construir `ComunaClick.Admin` como una aplicación independiente, con subdominio propio, permisos estrictos y módulos de gestión orientados a superadministradores.
