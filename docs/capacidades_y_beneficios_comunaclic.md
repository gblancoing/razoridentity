# Capacidades y Beneficios de ComunaClic

## 1. Resumen

**ComunaClic** es una plataforma digital para conectar personas con negocios, servicios y profesionales de su comuna o territorio cercano.  
Combina marketplace, agenda, leads, pagos y panel de gestión en un solo ecosistema.

Su propuesta de valor principal es:

- ayudar a los negocios locales a digitalizar su operación
- facilitar a las personas el descubrimiento y contratación de oferta cercana
- permitir una gestión multi-negocio y multi-tenant por comuna
- centralizar identidad, catálogo, reservas, pedidos, interacción y visibilidad

---

## 2. Capacidades Principales del Sistema

### 2.1 Registro y acceso de usuarios

El sistema permite:

- iniciar sesión con cuenta propia
- crear cuenta de usuario
- mantener sesión autenticada con JWT y refresh token
- resolver acceso por tenant, partner y roles
- separar autenticación/autorización en un servicio ACL dedicado

Capacidades incluidas:

- login de usuario
- registro de cuenta
- refresh de sesión
- manejo de permisos y scopes
- soporte para roles como `platform_admin`, `tenant_admin`, `partner_owner`, `partner_staff`, `customer`

---

### 2.2 Onboarding de negocios

El sistema permite:

- registrar negocios dentro del tenant correspondiente
- crear el negocio después del alta de usuario
- clasificar negocios por vertical de operación
- asociar el negocio al usuario creador
- preparar el negocio para publicación mediante checklist de activación

Tipos de negocio soportados:

- **A**: productos y delivery
- **B**: servicios con agenda
- **C**: profesionales y directorio

Datos de onboarding soportados:

- nombre comercial
- tipo de negocio
- RUT
- dirección
- contacto
- categoría y subcategoría
- geografía asociada al tenant

---

### 2.3 Gestión multi-negocio

El sistema permite:

- que un usuario vea los negocios asociados a su cuenta
- cambiar entre negocios propios
- trabajar con contexto de partner activo
- evitar que usuarios vean negocios ajenos cuando no corresponda

Esto habilita:

- operación de varios negocios por una misma persona
- separación de sesión por negocio
- administración más ordenada en entornos con múltiples marcas o sucursales

---

### 2.4 Catálogo de productos, servicios y profesionales

El sistema permite administrar tres tipos de oferta:

#### Productos

- crear productos
- asociarlos a un negocio
- activar o desactivar visibilidad
- manejar información comercial base
- usar categorías y subcategorías oficiales

#### Servicios

- crear servicios
- definir la oferta de atención
- conectarlos con agenda y reservas

#### Profesionales

- registrar profesionales
- mantener especialidad y datos de contacto
- publicar perfiles para generación de leads

---

### 2.5 Publicación y visibilidad

El sistema permite:

- controlar si un negocio está visible o no
- validar requisitos mínimos antes de publicar
- mostrar estado de activación
- guiar al negocio con checklist de publicación

La lógica de activación evalúa elementos como:

- identidad del negocio
- contacto
- dirección
- información tributaria
- al menos un producto, servicio o profesional según el tipo de partner

---

### 2.6 Búsqueda pública para usuarios finales

El sistema permite a los usuarios finales:

- buscar productos
- buscar servicios
- buscar profesionales
- navegar desde una experiencia pública
- trabajar con contexto geográfico por comuna

La capa pública soporta:

- listados de productos
- listados de servicios
- listados de profesionales
- navegación buyer
- detalle de oferta
- preparación para búsqueda hiper-local

---

### 2.7 Reservas, pedidos y leads

El sistema soporta distintos flujos transaccionales según vertical:

#### Pedidos

- órdenes para negocios de productos
- detalle de ítems
- totales monetarios
- seguimiento de estado

#### Reservas

- bookings para servicios
- slots de agenda
- capacidad y disponibilidad
- asociación a cliente y partner

#### Leads

- captación de interesados en perfiles profesionales
- trazabilidad mínima del contacto
- seguimiento inicial comercial

---

### 2.8 Pagos y liquidaciones

El sistema está preparado para:

- orquestar pagos mediante gateway independiente
- registrar pagos y eventos de pago
- mantener batches y items de payout
- soportar futuras liquidaciones a socios

Capacidades relacionadas:

- separación del gateway de pagos respecto del core de negocio
- soporte conceptual para Transbank
- trazabilidad de pagos
- base para liquidaciones por partner

---

### 2.9 CRM e interacciones

El sistema permite:

- vincular clientes con negocios
- guardar interacciones
- mantener primeras y últimas vistas
- dejar base para seguimiento comercial

Esto ayuda a construir:

- historial básico de cliente
- seguimiento de leads
- continuidad comercial

---

### 2.10 Dashboard y panel de gestión

El panel partner permite:

- ver métricas base
- revisar estado de activación
- navegar rápido a catálogo, agenda, leads, payouts y notificaciones
- visualizar negocios asociados
- operar desde un dashboard unificado

Incluye mejoras UX recientes como:

- loaders con branding
- manejo de errores HTTP amigables
- nuevo diseño editorial del panel
- visualización del usuario logeado

---

### 2.11 Multi-tenant por comuna

El sistema fue diseñado para operar por tenant, donde cada tenant representa una comuna o entorno equivalente.

Esto permite:

- segmentar datos por territorio
- resolver contexto por comuna
- aislar negocios y usuarios dentro de su ámbito
- escalar la plataforma por municipios o zonas

La arquitectura incluye:

- `tenant_id` en entidades core
- resolución de tenant desde claims y contexto
- geografía de país, región y comuna

---

### 2.12 Web y mobile compartidos

El sistema contempla:

- aplicación web con Blazor
- capa UI compartida
- proyecto mobile con MAUI Blazor Hybrid

Beneficios técnicos de esta capacidad:

- reutilización de componentes
- consistencia visual y funcional
- menor costo de evolución entre canales

---

## 3. Beneficios para el Usuario Final

## 3.1 Beneficios para personas que buscan negocios, servicios o profesionales

- Encuentran oferta local en un solo lugar.
- Pueden descubrir negocios más cercanos y relevantes para su comuna.
- Tienen una experiencia más simple para cotizar, reservar o comprar.
- Reducen el tiempo de búsqueda entre múltiples redes sociales, WhatsApp y directorios dispersos.
- Acceden a información más estructurada: nombre, oferta, contacto, estado y disponibilidad.
- Pueden navegar por categorías y subcategorías en lugar de depender de texto libre.

---

## 3.2 Beneficios para dueños de negocios y emprendedores

- Digitalizan su negocio sin tener que armar una plataforma propia.
- Obtienen un panel único para gestionar su operación.
- Pueden publicar productos, servicios o perfiles profesionales según su modelo.
- Tienen una ruta guiada para activar su negocio paso a paso.
- Mejoran su visibilidad digital dentro del ecosistema local.
- Pueden administrar más de un negocio desde una sola cuenta.
- Reducen fricción operativa al centralizar catálogo, agenda, pedidos y leads.

---

## 3.3 Beneficios para negocios de productos

- Pueden exhibir catálogo y productos activos.
- Tienen base para gestionar pedidos y pagos.
- Pueden preparar su vitrina digital para venta local.
- Se les facilita publicar una oferta mínima operativa rápidamente.

---

## 3.4 Beneficios para negocios de servicios

- Pueden definir su oferta de servicios.
- Tienen base para reservas y agenda.
- Pueden ordenar mejor horarios, disponibilidad y atención.
- Aumentan su posibilidad de conversión desde búsqueda a reserva.

---

## 3.5 Beneficios para profesionales independientes

- Pueden mostrar su perfil profesional de forma estructurada.
- Tienen un canal para recibir leads e interés comercial.
- Mejoran su presencia digital sin depender solo de redes sociales.
- Pueden destacar especialidad, contacto y validación del perfil.

---

## 3.6 Beneficios para administradores territoriales o plataforma

- Pueden operar múltiples tenants o comunas.
- Tienen una base más ordenada para impulsar comercio local.
- Se facilita la gobernanza de usuarios, permisos y accesos.
- Se crea infraestructura para escalar el servicio a más territorios.

---

## 4. Beneficios Estratégicos del Sistema

- **Centralización**: concentra múltiples flujos en una sola plataforma.
- **Escalabilidad**: permite crecer por tenant/comuna.
- **Flexibilidad**: soporta productos, servicios y profesionales.
- **Trazabilidad**: registra actividad, órdenes, reservas, leads e interacciones.
- **Seguridad**: separa ACL, roles y scopes de negocio.
- **Extensibilidad**: la arquitectura permite seguir sumando módulos.
- **Experiencia de uso**: combina frontend público y panel de gestión con una UX consistente.

---

## 5. Casos de Uso que ComunaClic Resuelve

- Un usuario crea su cuenta y luego registra su negocio.
- Un emprendedor publica su primer producto o servicio.
- Un partner gestiona varios negocios desde una sola sesión.
- Un vecino busca un servicio o producto cercano.
- Un negocio recibe pedidos, reservas o leads según su vertical.
- Un administrador territorial supervisa el ecosistema local.
- La plataforma prepara liquidaciones y pagos futuros a socios.

---

## 6. Estado de Madurez Funcional

El sistema ya cuenta con una base funcional amplia para:

- autenticación
- registro de usuario
- onboarding de negocio
- catálogo
- búsqueda pública
- dashboard partner
- órdenes, reservas y leads
- estructura de pagos y payouts
- multi-tenant por comuna

Además, su diseño deja preparada la evolución hacia:

- publicación end-to-end más robusta
- búsqueda pública de negocios como entidad principal
- mayor profundidad en CRM
- métricas reales en dashboard
- más automatización comercial y operacional

---

## 7. Conclusión

ComunaClic no es solo un sitio de publicación: es una base operativa para digitalizar comercio local, servicios y profesionales bajo una arquitectura multi-tenant y escalable.

Su principal beneficio para el usuario final es reducir fricción:

- menos pasos para encontrar oferta local
- menos dispersión para administrar un negocio
- más claridad para publicar, vender, reservar o captar clientes

En términos prácticos, transforma procesos informales y fragmentados en una experiencia digital más ordenada, visible y gestionable.
