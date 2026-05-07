# Valoración de Plataforma: ARIA MC — Project Control & Risk Management

**Versión del documento:** 1.0  
**Fecha:** Abril 2026  
**Clasificación:** Interno — Uso Restringido  

---

## Resumen Ejecutivo

**ARIA MC** es una plataforma corporativa de *Project Control & Risk Management* desarrollada sobre tecnología .NET 8 con base de datos relacional PostgreSQL, autenticación multi-usuario empresarial y motor de inteligencia artificial integrado. No es una herramienta de hoja de cálculo ni un plugin de terceros: es un sistema de software propietario, de arquitectura multicapa, diseñado específicamente para el análisis probabilístico de costos y riesgos en proyectos de capital de gran envergadura.

El sistema ejecuta simulaciones Monte Carlo de 10.000 iteraciones en seis módulos analíticos independientes, con soporte de correlaciones estadísticas (Cópula Gaussiana + descomposición de Cholesky), análisis EAT combinado (Estimado Al Término), gestión de portafolio multi-proyecto y generación de informes PDF corporativos.

---

## 1. Arquitectura Tecnológica

### Stack Principal

| Capa | Tecnología | Rol |
|---|---|---|
| Backend | ASP.NET Core 8 (C#) | Lógica de negocio, APIs internas, simulación MC |
| Base de datos | PostgreSQL (EF Core) | Persistencia relacional con migraciones versionadas |
| Frontend | Razor Pages + JavaScript | UI interactiva con gráficos y tablas dinámicas |
| Autenticación | ASP.NET Identity | Multi-usuario, roles, sesiones seguras |
| IA conversacional | Ollama (LLM local) | Asistente ARIA integrado, contexto de análisis |
| Generación PDF | Razor Views dedicadas | Reportes `TallerCostosPdf`, `TallerCostosPdfResumen`, `TallerCostosPdfRiesgos` |

### Características de Infraestructura

- **Multi-usuario real:** Cada usuario gestiona su portafolio de proyectos de forma aislada. Los datos están particionados por `UserId` en todas las tablas principales.
- **Base de datos relacional normalizada:** Esquema con 10+ entidades (`TallerProyecto`, `TallerContrato`, `TallerItem`, `TallerFamilia`, `TallerRiesgo`, `TallerRevisionRiesgos`, `TallerAnalisis`, `TallerMcResultado`, `MontecarloProject`, etc.) con relaciones, constraints e índices.
- **Migraciones versionadas:** 12+ migraciones de base de datos documentadas, permitiendo evolución controlada del esquema en producción sin pérdida de datos.
- **Motor de simulación en proceso:** El motor Monte Carlo corre en el servidor C# (`MonteCarloService`) sin dependencia de servicios externos, garantizando disponibilidad y velocidad de respuesta.
- **API externa opcional:** Integración con `MontecarloApiClient` para escenarios de delegación de cómputo a microservicio dedicado.
- **Seed determinístico:** Las simulaciones usan semilla derivada del ID de proyecto, garantizando reproducibilidad de resultados en auditorías.

---

## 2. Módulos Funcionales

### 2.1 Motor Monte Carlo — Plataforma de Análisis (6 módulos)

Accesible desde `/Montecarlo`, la plataforma ofrece seis tipos de análisis probabilístico independientes, más correlaciones e IA:

#### Módulo 1 — Análisis de Riesgos en Costo (Risk Analysis)
- Matriz de riesgos con tipo (Amenaza / Oportunidad), origen, código, descripción, causa y plan de respuesta.
- Distribuciones soportadas: **Triangular, Normal, PERT, Uniforme**.
- Modelo estocástico: **Bernoulli × Triangular** — combina probabilidad de ocurrencia con incertidumbre de magnitud.
- Métricas de salida: P5, P10, P25, P50, P75, P80, P90, P95, Media, StdDev, Skewness, Kurtosis.
- Desglose automático Amenazas vs. Oportunidades con percentiles propios.
- Histograma de distribución de resultados.

#### Módulo 2 — Estimación Probabilística de Costos (Cost Estimation)
- Componentes de costo con distribución triangular (mín / más probable / máx).
- Soporte de eventos Bernoulli adicionales por componente: oportunidad de ahorro + amenaza de sobrecosto.
- Percentiles del total acumulado del portafolio de costos.

#### Módulo 3 — Análisis de Cronograma (Schedule)
- Tareas con duraciones probabilísticas (mín / más probable / máx en días).
- **Red de dependencias:** cada tarea declara sus predecesoras (texto libre parseado a lista).
- Cálculo de ruta crítica probabilística por simulación.
- Percentiles de duración total del proyecto.

#### Módulo 4 — SRA: Schedule Risk Analysis
- Combina el motor de cronograma con riesgos de programa (impacto en días).
- Riesgos con probabilidad de ocurrencia + distribución triangular de impacto en tiempo.
- `GlobalPlannedDays` como referencia base de comparación.

#### Módulo 5 — CRA: Cost Risk Analysis Combinado
- Integra componentes de costo + riesgos en costo en una única simulación.
- Produce el **EAT combinado** (estimado al término con riesgos incluidos) como distribución de probabilidad.

#### Módulo 6 — VaR: Value at Risk Financiero
- Cartera de activos con valor inicial, retorno anual esperado y volatilidad.
- Modelo de precio log-normal (movimiento Browniano geométrico).
- Horizonte temporal configurable (días) y nivel de confianza configurable.
- Métricas: VaR_α, CVaR (Expected Shortfall), distribución de pérdidas.

#### Módulo 7 — Correlaciones (Cópula Gaussiana)
- Variables con distribuciones independientes (Triangular, Normal, PERT, Uniforme).
- **Matriz de correlación de Spearman** configurable por pares de variables.
- Algoritmo: Cholesky (Σ = LLᵀ) → muestras correlacionadas → transformación inversa por distribución.
- Captura dependencias entre riesgos que la simulación independiente subestima (ej.: sobrecosto en mano de obra correlacionado con sobrecosto en equipos).

#### Módulo 8 — IA: Asistente ARIA
- LLM local (Ollama) con contexto inyectado de los resultados MC activos.
- Interpreta percentiles, distribuciones y métricas en lenguaje natural.
- Sugiere matrices de correlación con justificación estadística (`CorrMatrixAiRequest`).
- Historial de conversación en sesión + continuación de consultas.

#### Módulo 9 — Fórmulas y Fundamentos Estadísticos
- Documentación interactiva de las fórmulas empleadas: distribución triangular (PDF e inversa), Bernoulli×Triangular, EAT combinado, CVaR, Cholesky, percentiles.
- Referencia técnica para auditores, revisores y nuevos usuarios del equipo.

---

### 2.2 Taller de Costos — Módulo de Control de Proyecto

El módulo `/TallerCostos` es el sistema integrado de control de costos y riesgos de proyecto. Implementa la metodología de análisis de contingencias utilizada en proyectos de capital de gran escala.

#### Estructura de Datos del Proyecto

```
TallerProyecto
├── Código, Nombre, Organización, Fecha de Ejercicio, Estado
├── TallerFamilia[]          → Agrupadores de contratos
├── TallerContrato[]         → Paquetes de trabajo (CAPEX USD, comprometido, EAT)
│   ├── TasaCambio CLP/USD   → Conversión monetaria integrada
│   ├── Factor de ajuste     → Escalamiento de costos
│   └── TallerItem[]         → Ítems de costo desagregados
│       ├── Bloque A: Certeza (costos comprometidos)
│       ├── Bloque B: Incertidumbre (distribución triangular)
│       ├── Bloque C: Contingencia calculada
│       └── Bloque D: Ítems vía riesgo
├── TallerRevisionRiesgos[]  → Revisiones versionadas de la matriz de riesgos
│   └── TallerRiesgo[]       → Riesgos/Oportunidades con distribución triangular
└── TallerAnalisis[]         → Ejecuciones MC históricas con resultados JSON
```

#### Funcionalidades del Taller

- **Bloques A/B/C/D:** Separación metodológica entre costos comprometidos (certeza), rangos de incertidumbre (triangular), y exposición vía riesgos explícitos.
- **Multi-contrato con familias:** Agrupación jerárquica de paquetes de trabajo, con consolidación automática por familia y por proyecto total.
- **Conversión monetaria:** Factor de tasa de cambio CLP/USD por contrato con fecha de referencia. Factor de ajuste decimal de alta precisión (15 decimales).
- **Análisis EAT Combinado:** Simulación que integra ítems de incertidumbre de todos los contratos + riesgos de la revisión activa. Produce curva de probabilidad del EAT total del proyecto.
- **Revisiones versionadas de riesgos:** El historial de matrices de riesgo queda preservado con fecha y ejecutor. Se puede comparar evolución entre revisiones.
- **Resultados auditables:** `TallerAnalisis` almacena `SemillaAleatoria` + `ResultadosJson` completo, permitiendo reproducción exacta de cualquier análisis histórico.
- **Resumen Global:** Dashboard ejecutivo con:
  - Tabla de certeza (EAT base por contrato).
  - Distribución probabilística de incertidumbre (P50/P80/P90).
  - Distribución probabilística de riesgos (con SR como fuente de verdad).
  - EAT Combinado con Δ respecto a línea base.
  - Análisis Tornado (sensibilidad de variables).
  - Oportunidades — Ahorro Potencial.

#### Reportes PDF Corporativos

El sistema genera tres variantes de reporte PDF:
- **`TallerCostosPdf`** — Detalle completo por contrato e ítem.
- **`TallerCostosPdfRiesgos`** — Matriz de riesgos con distribuciones y métricas.
- **`TallerCostosPdfResumen`** — Informe ejecutivo con EAT combinado y curvas de probabilidad.

---

### 2.3 ARIA Chat — Asistente Inteligente

`/Chat` es una interfaz conversacional con el LLM de Ollama, especializada en Monte Carlo y análisis de riesgos:
- Contexto de análisis MC inyectado automáticamente desde la sesión activa.
- Contexto de Taller de Costos (`TcContextJson`) disponible para el asistente.
- Historial de conversación persistente en sesión.
- Generación de sugerencias de matrices de correlación con justificación técnica.

---

### 2.4 Integraciones Externas

| Integración | Propósito |
|---|---|
| **RitWeb API** | Gestión de eventos de seguridad y fichas de personal |
| **API Ritweb** | Proyectos, contratos y datos corporativos desde sistema ERP externo |
| **MiroFish** | Servicio especializado de datos de proyectos |
| **Montecarlo API** | Microservicio externo MC como alternativa al motor interno |
| **Ollama (LLM local)** | Inferencia de IA en infraestructura propia (sin datos a la nube) |

---

## 3. Comparación con Soluciones Comerciales

| Solución | Tipo | Precio estimado/año | Multi-usuario nativo | IA integrada | On-Premises | Integración ERP |
|---|---|---|---|---|---|---|
| **ARIA MC (este sistema)** | Plataforma web propia | Costo de desarrollo | ✅ Nativo | ✅ LLM local | ✅ Total | ✅ API propia |
| @Risk (Lumivero) | Plugin Excel | USD 2.500–4.000/usuario | ❌ Archivo Excel | ❌ | ❌ Cloud | ❌ |
| Oracle Primavera Risk Analysis | Desktop + servidor | USD 15.000–40.000/usuario | Limitado | ❌ | Parcial | Oracle suite |
| Acumen Risk (Deltek) | Desktop + servidor | USD 8.000–20.000/usuario | Limitado | ❌ | Parcial | Deltek suite |
| Safran Risk | Web Enterprise | USD 20.000–60.000/año | ✅ | ❌ | Parcial | Limitada |
| ARM (Active Risk Manager) | Web Enterprise | USD 30.000–100.000/año | ✅ | ❌ | Parcial | SAP/Oracle |
| Crystal Ball (Oracle) | Plugin Excel | USD 2.000–5.000/usuario | ❌ Archivo Excel | ❌ | ❌ | ❌ |

> **Nota de precios:** Valores de referencia de mercado (2025). Las licencias enterprise incluyen soporte, actualizaciones y en algunos casos formación. No incluyen costos de implementación, servidores ni consultoría.

### Ventajas Diferenciales de ARIA MC

1. **Datos 100% on-premises:** El LLM corre en infraestructura propia (Ollama). Ningún dato de proyecto sale de la red corporativa. Las soluciones cloud (SaaS) exponen datos sensibles de CAPEX y riesgos a terceros.

2. **Metodología integrada:** La mayoría de herramientas son genéricas. ARIA MC implementa la metodología específica de análisis de contingencias con bloques A/B/C/D, familias de contratos y revisiones versionadas de riesgos.

3. **EAT Combinado con correlaciones:** La simulación CRA (costos + riesgos correlacionados con Cópula Gaussiana) es una capacidad que requiere licencias enterprise de $50K+/año en soluciones comerciales.

4. **Portafolio multi-proyecto en una sesión:** El usuario gestiona N proyectos simultáneamente con historia completa de análisis. @Risk y Crystal Ball operan por archivo Excel individual.

5. **Trazabilidad de auditoría:** Cada simulación guarda semilla, fecha, usuario y JSON completo de resultados. Permite reproducción exacta ante auditorías internas o externas.

6. **Evolución controlada:** Arquitectura con migraciones de base de datos versionadas permite agregar módulos y métricas sin interrumpir operación.

---

## 4. Módulos MC — Capacidades Estadísticas Detalladas

### Distribuciones implementadas

| Distribución | Módulos que la usan | Casos de uso |
|---|---|---|
| **Triangular** | Todos | Estimación de costos, duración de tareas, impacto de riesgos |
| **PERT** | Riesgos, Costos, Cronograma | Cuando el experto tiene mayor confianza en el valor más probable |
| **Normal** | Riesgos, Costos | Variables con comportamiento simétrico bien definido |
| **Uniforme** | Todos | Incertidumbre total entre un rango (máxima entropía) |
| **Bernoulli × Triangular** | Riesgos, CRA | Eventos probabilísticos con magnitud variable |
| **Log-normal** | VaR | Precios de activos financieros |

### Métricas de Salida

- **Percentiles:** P5, P10, P25, P50, P75, P80, P90, P95 — estándar CODELCO y PMI.
- **CVaR / Expected Shortfall:** Pérdida esperada más allá del umbral de confianza. Más conservador que VaR para colas pesadas.
- **Skewness / Kurtosis:** Caracterización de asimetría y curtosis de la distribución de resultados.
- **Análisis Tornado:** Ranking de variables por impacto en el resultado total (swing analysis).
- **Probabilidad de cualquier riesgo:** P(al menos un riesgo ocurre) en la simulación.
- **Coeficiente de Variación (CV):** Dispersión relativa para comparabilidad entre proyectos de distinto tamaño.

---

## 5. Modelo de Datos — Resumen de Entidades

```
Entidades de Taller de Costos          Entidades de Monte Carlo
─────────────────────────────          ────────────────────────
TallerProyecto (1)                     MontecarloProject (1)
  └── TallerFamilia (N)                  └── TabsJson (JSON)
  └── TallerContrato (N)                      ├── risks  → RiskMcResponse
        └── TallerItem (N)                    ├── costs  → CostEstimationResult
  └── TallerRevisionRiesgos (N)              ├── schedule → ScheduleResult
        └── TallerRiesgo (N)                 ├── sra    → SraResult
  └── TallerAnalisis (N)                     ├── cra    → CraResult
        └── ResultadosJson (JSON)            ├── var    → VaRResult
        └── SemillaAleatoria (int)           └── corr   → CorrelationResult
```

---

## 6. Valoración Económica Estimada

### Costo de Reposición por Licencias Comerciales Equivalentes

Para replicar las capacidades de ARIA MC con soluciones comerciales disponibles en el mercado se requeriría:

| Capacidad | Solución comercial equivalente | Costo anual estimado (por usuario) |
|---|---|---|
| Simulación MC costos + riesgos | @Risk Professional | USD 3.500 |
| Análisis de cronograma probabilístico | Primavera Risk Analysis | USD 20.000 |
| Correlaciones Gaussianas (Cholesky) | @Risk Industrial | USD 5.500 |
| Gestión de portafolio multi-proyecto | ARM Enterprise | USD 15.000 |
| Asistente IA con contexto de análisis | Módulo IA enterprise (ARM/Deltek) | USD 10.000 |
| Generación de reportes corporativos | Crystal Reports / Power BI Premium | USD 2.000 |
| On-premises + integración ERP propia | Consultoría + licencias SAP GRC | USD 40.000+ |

**Costo comercial equivalente estimado: USD 40.000–96.000 por usuario / año.**

Para un equipo de 10 analistas de riesgo y control de proyectos, la diferencia entre adquirir herramientas comerciales equivalentes y operar ARIA MC representa un ahorro operacional de **USD 400.000–960.000 anuales en licencias**, sin contar costos de implementación, migración y formación de herramientas de terceros.

### Valor Estratégico Adicional (No Monetizable Directamente)

- **Propiedad intelectual:** El sistema, la metodología y los datos son 100% propiedad de la organización. No hay dependencia de proveedores externos ni riesgo de descontinuación de producto.
- **Confidencialidad de datos:** Los datos de CAPEX, costos y riesgos de proyectos de capital son sensibles. La operación on-premises elimina la superficie de exposición de información confidencial inherente a soluciones SaaS.
- **Alineación metodológica:** El sistema implementa exactamente la metodología de análisis de contingencias corporativa. No se requiere adaptar los procesos a las limitaciones de una herramienta genérica.
- **Evolución ágil:** Nuevas métricas, módulos o integraciones se desarrollan e implementan en días/semanas, sin depender de roadmaps de proveedores externos.
- **Historial de auditoría:** La trazabilidad completa (semilla + resultados + usuario + fecha) cumple con requisitos de auditoría interna y externa sin configuración adicional.

---

## 7. Madurez y Calidad del Sistema

### Indicadores de Madurez Técnica

| Indicador | Estado |
|---|---|
| Migraciones de base de datos versionadas | ✅ 12+ migraciones aplicadas |
| Autenticación y autorización | ✅ ASP.NET Identity con sesiones seguras |
| Separación de datos por usuario | ✅ `UserId` en todas las entidades principales |
| Reproducibilidad de simulaciones | ✅ Seed determinístico por proyecto |
| Generación de reportes PDF | ✅ 3 variantes de reporte corporativo |
| Integración de IA local | ✅ Ollama on-premises |
| Motor MC propio (sin dependencia externa) | ✅ `MonteCarloService` in-process |
| Correlaciones estadísticas avanzadas | ✅ Cópula Gaussiana + Cholesky |
| Consistencia de resultados cross-módulo | ✅ Fuente de verdad única (SR) en Resumen Global |

### Módulos en Producción

| Módulo | Estado |
|---|---|
| Análisis Monte Carlo (6 tipos) | ✅ Producción |
| Taller de Costos (Bloques A-D + EAT) | ✅ Producción |
| Resumen Global + Tornado | ✅ Producción |
| Revisiones versionadas de riesgos | ✅ Producción |
| PDF Resumen + PDF Riesgos | ✅ Producción |
| Asistente IA ARIA | ✅ Producción |
| Correlaciones (Cópula Gaussiana) | ✅ Producción |
| Integración RitWeb / API externa | ✅ Producción |
| Fórmulas y fundamentos estadísticos | ✅ Producción |

---

## 8. Conclusión

ARIA MC no es un plugin de Excel ni una herramienta de escritorio de uso individual. Es una **plataforma web corporativa de Project Control & Risk Management** con:

- Motor estadístico propio de nivel enterprise (Monte Carlo, Cholesky, CVaR, Tornado).
- Base de datos relacional multi-usuario con historial auditable de análisis.
- Metodología de control de costos integrada (Bloques A/B/C/D, Taller de Costos).
- Asistente de inteligencia artificial on-premises con contexto de análisis.
- Generación de informes corporativos PDF.
- Integración con sistemas externos (ERP, API corporativa).

El costo de reposición de funcionalidades equivalentes en el mercado comercial supera los **USD 40.000 por usuario al año**. El sistema opera sobre infraestructura propia, garantizando confidencialidad de datos sensibles de CAPEX y eliminando dependencias de proveedores externos.

La plataforma representa una ventaja competitiva sostenible: combina capacidades analíticas de nivel corporativo con total alineación a la metodología interna y propiedad intelectual completa de la organización.

---

*Documento generado para valorización y toma de decisiones internas. Versión 1.0 — Abril 2026.*
