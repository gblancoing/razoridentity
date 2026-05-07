# API Monte Carlo — Documentación Técnica

> **Base URL (desarrollo):** `http://localhost:5080`
> **Swagger UI:** `http://localhost:5080/swagger`
> **Stack:** .NET 8 Minimal API · EF Core 8 · PostgreSQL · Swashbuckle

---

## Índice

1. [Arquitectura general](#arquitectura-general)
2. [Distribuciones disponibles](#distribuciones-disponibles)
3. [Módulo General — Endpoints `/api/montecarlo`](#módulo-general)
   - [POST /risks — Análisis de Riesgos en Costo](#post-risks)
   - [POST /costs — Estimación de Costos](#post-costs)
   - [POST /schedule — Estimación de Cronograma](#post-schedule)
   - [POST /schedule-risks — Riesgos de Cronograma](#post-schedule-risks)
   - [POST /sra — Schedule Risk Analysis (combinado)](#post-sra)
   - [POST /cra — Cost Risk Analysis (combinado)](#post-cra)
   - [POST /var — Value at Risk](#post-var)
   - [GET /history — Historial paginado](#get-history)
   - [GET /history/{id} — Simulación por ID](#get-historyid)
4. [Módulo Mining — Endpoints `/api/mining`](#módulo-mining)
   - [POST /capex — Análisis CAPEX](#post-capex)
   - [POST /opex — Análisis OPEX (C1 Cash Cost)](#post-opex)
   - [POST /schedule — Cronograma Minero](#post-schedule-mining)
   - [POST /financial — Análisis Financiero VAN/TIR/VaR](#post-financial)
   - [POST /risks — Registro de Riesgos](#post-risks-mining)
   - [POST /stress-test — Stress Testing](#post-stress-test)
   - [POST /project — Análisis Integral del Proyecto](#post-project)
5. [Respuestas: estructura común](#respuestas-estructura-común)
6. [Persistencia e historial](#persistencia-e-historial)
7. [Análisis previos sugeridos](#análisis-previos-sugeridos)

---

## Arquitectura general

```
montecarlo/
├── Program.cs                        ← registro de endpoints (Minimal API)
├── DTOs/
│   ├── MonteCarloRequests.cs         ← DTOs de entrada módulo general
│   └── MonteCarloResponses.cs        ← DTOs de salida comunes
├── MonteCarlo/
│   └── MonteCarloService.cs          ← motor de simulación general
├── MiningMonteCarlo/
│   ├── DTOs/MiningDtos.cs            ← DTOs entrada/salida módulo minero
│   ├── Engine/
│   │   ├── MiningMonteCarloEngine.cs ← motor de simulación minera
│   │   ├── StatisticalDistributions.cs
│   │   └── CholeskyDecomposition.cs  ← correlaciones entre variables
│   ├── Analysis/                     ← módulos de análisis individuales
│   │   ├── CapexOpexAnalysis.cs
│   │   ├── ScheduleRiskAnalysis.cs
│   │   ├── FinancialRiskAnalysis.cs
│   │   ├── RiskRegisterAnalysis.cs
│   │   └── StressTestAnalysis.cs
│   ├── Services/MiningRiskService.cs ← orquestador del módulo minero
│   ├── Models/MiningProjectModels.cs ← modelos internos
│   └── Guides/                       ← guías de interpretación generadas
├── Repositories/                     ← patrón repositorio (historial)
├── Services/                         ← servicios de historial
└── Data/AppDbContext.cs              ← EF Core + PostgreSQL
```

Toda la serialización usa **camelCase** (`System.Text.Json.JsonNamingPolicy.CamelCase`).

---

## Distribuciones disponibles

### Módulo General

| Valor | Parámetros usados |
|-------|-------------------|
| `"Triangular"` | min, mostLikely, max |
| `"Normal"` | mean=(min+max)/2, stdDev derivado |
| `"PERT"` | min, mostLikely, max (λ=4 por defecto) |
| `"Uniform"` | min, max |

### Módulo Mining

| Valor | Descripción |
|-------|-------------|
| `"BetaPERT"` | PERT generalizada con λ configurable (default λ=4) |
| `"Triangular"` | Igual que módulo general |
| `"Uniform"` | Igual que módulo general |

---

## Módulo General

### POST /risks

**Análisis de riesgos en costo.** Aplica Bernoulli sobre cada riesgo: la probabilidad activa la distribución de impacto y suma la contribución al total.

#### Request

```json
{
  "risks": [
    {
      "cause": "string",
      "riskEvent": "string",
      "consequence": "string",
      "probability": 0.3,
      "minImpact": 10000,
      "mostLikelyImpact": 25000,
      "maxImpact": 50000,
      "distribution": "Triangular"
    }
  ],
  "simulations": 10000
}
```

| Campo | Tipo | Requerido | Descripción |
|-------|------|-----------|-------------|
| `risks` | array | Sí | Lista de riesgos |
| `risks[].cause` | string | No | Causa raíz del riesgo |
| `risks[].riskEvent` | string | No | Descripción del evento |
| `risks[].consequence` | string | No | Consecuencia si ocurre |
| `risks[].probability` | double [0–1] | Sí | Probabilidad de ocurrencia |
| `risks[].minImpact` | double | Sí | Impacto mínimo (misma unidad que costos) |
| `risks[].mostLikelyImpact` | double | Sí | Impacto más probable |
| `risks[].maxImpact` | double | Sí | Impacto máximo |
| `risks[].distribution` | string | No | Default: `"Triangular"` |
| `simulations` | int | No | Default: 10000 |

#### Response → `SimulationResult` (ver [Respuestas comunes](#respuestas-estructura-común))

---

### POST /costs

**Estimación de costos con incertidumbre.** Cada componente tiene una distribución base más riesgos de oportunidad (reducen costo) y amenaza (aumentan costo) modelados como Bernoulli.

#### Request

```json
{
  "components": [
    {
      "name": "Obras Civiles",
      "minCost": 800000,
      "mostLikelyCost": 1000000,
      "maxCost": 1400000,
      "distribution": "Triangular",
      "baseCost": 950000,
      "estimationClass": "Clase 3",
      "opportunities": [
        {
          "name": "Oportunidad",
          "probability": 0.2,
          "minImpact": 70000,
          "mostLikelyImpact": 100000,
          "maxImpact": 130000,
          "distribution": "Triangular"
        }
      ],
      "threats": [
        {
          "name": "Amenaza",
          "probability": 0.3,
          "minImpact": 140000,
          "mostLikelyImpact": 200000,
          "maxImpact": 260000,
          "distribution": "Triangular"
        }
      ]
    }
  ],
  "simulations": 10000
}
```

> **Nota importante:** `opportunities` y `threats` son **arrays de objetos `CostRisk`**, no valores numéricos. Cada oportunidad reduce el costo (impacto negativo) y cada amenaza lo aumenta (impacto positivo). El frontend de RazorIdentity convierte los campos `OpportunityProbability`/`OpportunityAmount` y `ThreatProbability`/`ThreatAmount` a este formato antes de llamar a la API.

| Campo en `CostRisk` | Tipo | Descripción |
|---------------------|------|-------------|
| `name` | string | Nombre del riesgo |
| `probability` | double [0–1] | Probabilidad de ocurrencia |
| `minImpact` | double | Impacto mínimo en valor absoluto |
| `mostLikelyImpact` | double | Impacto más probable |
| `maxImpact` | double | Impacto máximo |
| `distribution` | string | Default: `"Triangular"` |

#### Response → `SimulationResult`

`AdditionalMetrics` incluye desglose por componente con su media y desviación estándar individual.

---

### POST /schedule

**Estimación de duración de cronograma** con red de dependencias (ruta crítica estocástica).

#### Request

```json
{
  "tasks": [
    {
      "name": "Ingeniería Básica",
      "minDays": 60,
      "mostLikelyDays": 90,
      "maxDays": 130,
      "dependencies": [],
      "distribution": "Triangular",
      "plannedDays": 85
    },
    {
      "name": "Ingeniería de Detalle",
      "minDays": 90,
      "mostLikelyDays": 120,
      "maxDays": 180,
      "dependencies": ["Ingeniería Básica"],
      "distribution": "PERT",
      "plannedDays": 115
    }
  ],
  "simulations": 10000,
  "plannedDays": 205
}
```

| Campo | Tipo | Requerido | Descripción |
|-------|------|-----------|-------------|
| `tasks[].name` | string | Sí | Nombre único de la tarea |
| `tasks[].minDays` | double | Sí | Duración optimista (días) |
| `tasks[].mostLikelyDays` | double | Sí | Duración más probable |
| `tasks[].maxDays` | double | Sí | Duración pesimista |
| `tasks[].dependencies` | string[] | No | Nombres de tareas predecesoras |
| `tasks[].distribution` | string | No | Default: `"Triangular"` |
| `tasks[].plannedDays` | double? | No | Duración planificada de la tarea |
| `simulations` | int | No | Default: 10000 |
| `plannedDays` | double? | No | Duración total planificada del proyecto |

#### Response → `SimulationResult`

`AdditionalMetrics` incluye probabilidad de cumplir el plazo planificado (`P(cumple plazo)`), ruta crítica identificada y análisis de holgura por tarea.

---

### POST /schedule-risks

**Riesgos de cronograma.** Igual que `/risks` pero las unidades de impacto son **días** (no dinero). Útil para cuantificar riesgos que afectan la ruta crítica.

#### Request — idéntico a `/risks` con impactos en días

```json
{
  "risks": [
    {
      "cause": "Huelga de contratistas",
      "riskEvent": "Paralización de obras",
      "consequence": "Retraso en hito de puesta en marcha",
      "probability": 0.15,
      "minImpact": 15,
      "mostLikelyImpact": 30,
      "maxImpact": 60,
      "distribution": "Triangular"
    }
  ],
  "simulations": 10000
}
```

#### Response → `SimulationResult` (estadísticas en días)

---

### POST /sra

**Schedule Risk Analysis combinado.** Suma iteración a iteración el cronograma base con los riesgos del programa, produciendo la distribución total del proyecto incluyendo ambas fuentes de incertidumbre.

#### Request — **estructura anidada obligatoria**

```json
{
  "schedule": {
    "tasks": [ /* array de ScheduleTask */ ],
    "simulations": 10000,
    "plannedDays": 365
  },
  "scheduleRisks": {
    "risks": [ /* array de RiskItem en días */ ],
    "simulations": 10000
  },
  "simulations": 10000
}
```

> **Advertencia:** La API rechaza (HTTP 400) si se envía estructura plana. El campo `simulations` raíz se usa como override global; `simulations` dentro de `schedule` y `scheduleRisks` puede repetir el mismo valor.

#### Response → `SimulationResult`

---

### POST /cra

**Cost Risk Analysis combinado.** Suma iteración a iteración la estimación de costos base con los riesgos en costo.

#### Request — **estructura anidada obligatoria**

```json
{
  "costs": {
    "components": [ /* array de CostComponent con opportunities/threats */ ],
    "simulations": 10000
  },
  "risks": {
    "risks": [ /* array de RiskItem en dinero */ ],
    "simulations": 10000
  },
  "simulations": 10000
}
```

#### Response → `SimulationResult`

---

### POST /var

**Value at Risk (VaR)** para portafolios de activos financieros. Usa simulación de Movimiento Browniano Geométrico (GBM).

#### Request

```json
{
  "assets": [
    {
      "name": "Proyecto A",
      "initialValue": 5000000,
      "expectedAnnualReturn": 0.12,
      "annualVolatility": 0.25
    }
  ],
  "horizonDays": 252,
  "confidenceLevel": 0.95,
  "simulations": 10000
}
```

| Campo | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `assets[].name` | string | — | Nombre del activo o proyecto |
| `assets[].initialValue` | double | — | Valor inicial (USD u otra moneda) |
| `assets[].expectedAnnualReturn` | double | — | Retorno anual esperado (ej: 0.12 = 12%) |
| `assets[].annualVolatility` | double | — | Volatilidad anual (ej: 0.25 = 25%) |
| `horizonDays` | int | 252 | Horizonte de riesgo en días hábiles (252 = 1 año) |
| `confidenceLevel` | double | 0.95 | Nivel de confianza (0.90, 0.95, 0.99) |
| `simulations` | int | 10000 | Iteraciones |

#### Response → `SimulationResult`

`AdditionalMetrics` incluye el VaR absoluto y relativo al nivel de confianza especificado, y el CVaR (Expected Shortfall).

---

### GET /history

**Historial paginado** de simulaciones guardadas en PostgreSQL.

#### Query params

| Param | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `page` | int | 1 | Página (base 1) |
| `pageSize` | int | 20 | Registros por página |
| `simulationType` | string | null | Filtro por tipo: `"RiskAnalysis"`, `"CostEstimation"`, `"ScheduleEstimation"`, `"VaRAnalysis"`, `"ScheduleRiskAnalysis"`, `"SRA"`, `"CRA"` |

#### Response

```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 150,
  "items": [
    {
      "id": "uuid",
      "simulationType": "CRA",
      "createdAt": "2026-03-26T10:00:00Z",
      "simulations": 10000,
      "requestJson": "...",
      "resultJson": "..."
    }
  ]
}
```

---

### GET /history/{id}

Devuelve una simulación específica por su UUID. Retorna **404** si no existe.

---

## Módulo Mining

Endpoints especializados para proyectos mineros. Usan distribución **BetaPERT** por defecto (más conservadora que la triangular estándar) y soportan **correlaciones entre variables** mediante Descomposición de Cholesky.

### POST /capex

**Análisis CAPEX probabilístico** siguiendo estándares AACE (Clase 1–5).

#### Request

```json
{
  "items": [
    {
      "name": "Planta Concentradora",
      "category": "Proceso",
      "wbsCode": "1.2.3",
      "min": 80000000,
      "mostLikely": 100000000,
      "max": 140000000,
      "lambda": 4.0,
      "distribution": "BetaPERT",
      "aaceClass": 3,
      "escalationPerYear": 0.03,
      "yearsToExpenditure": 2
    }
  ],
  "correlations": [
    {
      "variable1": "Planta Concentradora",
      "variable2": "Obras Civiles",
      "coefficient": 0.65
    }
  ],
  "iterations": 50000
}
```

| Campo en `CapexItemDto` | Tipo | Default | Descripción |
|-------------------------|------|---------|-------------|
| `name` | string | — | Nombre del ítem |
| `category` | string | — | Categoría (Proceso, Civil, Eléctrico, etc.) |
| `wbsCode` | string | — | Código WBS |
| `min` / `mostLikely` / `max` | double | — | Rango de estimación |
| `lambda` | double | 4.0 | Parámetro λ de BetaPERT (mayor = más peso al valor más probable) |
| `distribution` | string | `"BetaPERT"` | Distribución por ítem |
| `aaceClass` | int [1–5] | 3 | Clase de estimación AACE |
| `escalationPerYear` | double | 0.03 | Factor de escalación anual |
| `yearsToExpenditure` | int | 1 | Años hasta el desembolso |

---

### POST /opex

**Análisis OPEX probabilístico — C1 Cash Cost.** Modela incertidumbre en throughput, ley, recuperación metalúrgica, costos de minería, procesamiento y G&A.

#### Request (todos los campos en rango min/mostLikely/max)

```json
{
  "throughputTpdMin": 45000,
  "throughputTpdMostLikely": 50000,
  "throughputTpdMax": 55000,
  "gradeMin": 0.45,
  "gradeMostLikely": 0.55,
  "gradeMax": 0.65,
  "recoveryMin": 0.85,
  "recoveryMostLikely": 0.88,
  "recoveryMax": 0.91,
  "miningCostPerTMovedMin": 2.5,
  "miningCostPerTMovedMostLikely": 3.0,
  "miningCostPerTMovedMax": 3.8,
  "processingCostPerTMin": 4.0,
  "processingCostPerTMostLikely": 5.0,
  "processingCostPerTMax": 6.5,
  "gAndAPerTMin": 0.8,
  "gAndAPerTMostLikely": 1.0,
  "gAndAPerTMax": 1.5,
  "stripRatioMin": 1.5,
  "stripRatioMostLikely": 2.5,
  "stripRatioMax": 4.0,
  "dilutionPercent": 5.0,
  "iterations": 50000
}
```

---

### POST /schedule (mining)

**Análisis de riesgo de cronograma minero** con fases (Ingeniería, Procura, Construcción, Comisionamiento) y hitos.

#### Request

```json
{
  "activities": [
    {
      "name": "Ingeniería de Detalle",
      "phase": "Ingeniería",
      "minDays": 180,
      "mostLikelyDays": 240,
      "maxDays": 320,
      "lambda": 4.0,
      "distribution": "BetaPERT",
      "dependencies": [],
      "isMilestone": false,
      "milestoneType": "",
      "resourceCalendar": 1.0
    }
  ],
  "iterations": 50000
}
```

| Campo extra vs general | Descripción |
|------------------------|-------------|
| `phase` | Fase del proyecto (Ingeniería, Procura, Construcción, etc.) |
| `lambda` | Parámetro λ BetaPERT |
| `isMilestone` | Marca la actividad como hito |
| `milestoneType` | Tipo de hito (ej: "First Blast", "Mechanical Completion") |
| `resourceCalendar` | Factor de disponibilidad de recursos [0–1] |

---

### POST /financial

**Análisis financiero VAN/TIR/VaR** con opciones reales (expansión).

#### Request (campos principales)

```json
{
  "mineLifeYears": 20,
  "discountRate": 0.08,
  "corporateTaxRate": 0.27,
  "royaltyPercent": 0.03,
  "commodityType": "Cu",
  "priceMin": 3.5,
  "priceMostLikely": 4.2,
  "priceMax": 5.5,
  "priceDist": "Triangular",
  "fxRateMin": 900,
  "fxRateMostLikely": 950,
  "fxRateMax": 1050,
  "annualProductionTfmin": 180000,
  "annualProductionTfMostLikely": 200000,
  "annualProductionTfMax": 220000,
  "annualOpexMin": 45000000,
  "annualOpexMostLikely": 55000000,
  "annualOpexMax": 70000000,
  "depreciationYears": 15,
  "includeExpansionOption": true,
  "expansionTriggerPrice": 5.0,
  "expansionCapex": 200000000,
  "expansionProductionFactor": 1.5,
  "capexItems": [ /* array CapexItemDto */ ],
  "iterations": 50000
}
```

`AdditionalMetrics` en la respuesta incluye: distribución de VAN, TIR, Payback, VaR del proyecto, valor de la opción de expansión.

---

### POST /risks (mining)

**Análisis cuantitativo del registro de riesgos mineros** con cálculo de EMV, cascada de riesgos y estrategias de respuesta.

#### Request

```json
{
  "risks": [
    {
      "description": "Caída precio del cobre",
      "category": "Mercado",
      "owner": "Gerencia Comercial",
      "probability": 0.35,
      "costImpactMin": 10000000,
      "costImpactMostLikely": 30000000,
      "costImpactMax": 80000000,
      "costImpactDist": "Triangular",
      "scheduleImpactMinDays": 0,
      "scheduleImpactMostLikelyDays": 0,
      "scheduleImpactMaxDays": 0,
      "responseStrategy": "Mitigar",
      "residualProbabilityFactor": 0.6,
      "residualImpactFactor": 0.7,
      "correlatedRisks": ["Depreciación CLP"],
      "correlationCoefficient": 0.6
    }
  ],
  "iterations": 50000
}
```

| Campo | Descripción |
|-------|-------------|
| `responseStrategy` | `"Mitigar"`, `"Transferir"`, `"Aceptar"`, `"Evitar"` |
| `residualProbabilityFactor` | Factor multiplicador sobre probabilidad post-respuesta [0–1] |
| `residualImpactFactor` | Factor multiplicador sobre impacto post-respuesta [0–1] |
| `correlatedRisks` | Nombres de otros riesgos correlacionados |
| `correlationCoefficient` | Coeficiente de Pearson [-1, 1] |

---

### POST /stress-test

**Stress testing** con 5 escenarios adversos predefinidos + semáforo ejecutivo. Extiende `FinancialAnalysisRequest` con escenarios opcionales personalizados.

#### Request

```json
{
  /* todos los campos de FinancialAnalysisRequest */
  "customScenarios": [
    {
      "name": "Colapso precio + atraso",
      "description": "Precio -40%, CAPEX +30%, 6 meses de atraso",
      "priceFactor": 0.60,
      "capexFactor": 1.30,
      "opexFactor": 1.10,
      "gradeFactor": 0.95,
      "recoveryFactor": 0.95,
      "scheduleDelayMonths": 6,
      "fxFactor": 1.15
    }
  ]
}
```

Los 5 escenarios predefinidos cubren: colapso de precio, sobrecosto de CAPEX, deterioro geotécnico, huelga prolongada, crisis cambiaria.

---

### POST /project

**Análisis integral del proyecto minero.** Ejecuta todos los módulos en una sola llamada y devuelve resultados consolidados.

#### Request

```json
{
  "projectName": "Proyecto Esperanza Sur",
  "projectType": "Open Pit",
  "commodity": "Cu",
  "country": "Chile",
  "studyPhase": "Prefactibilidad",
  "iterations": 50000,
  "capex": { /* CapexAnalysisRequest */ },
  "opex":  { /* OpexAnalysisRequest */ },
  "schedule": { /* ScheduleRiskRequest */ },
  "financial": { /* FinancialAnalysisRequest */ },
  "risks": { /* RiskRegisterRequest */ },
  "runStressTest": true
}
```

Todos los sub-objetos son opcionales. Si se omite alguno, ese módulo se salta.

---

## Respuestas: estructura común

### `SimulationResult` (módulo general)

```json
{
  "simulationType": "CRA",
  "totalSimulations": 10000,
  "statistics": {
    "min": 1200000,
    "max": 3800000,
    "mean": 2100000,
    "stdDev": 380000,
    "variance": 144400000000,
    "skewness": 0.45,
    "kurtosis": 3.1,
    "mode": 2050000,
    "errors": 0,
    "p5": 1560000,
    "p10": 1680000,
    "p15": 1750000,
    "p20": 1820000,
    "p25": 1880000,
    "p30": 1940000,
    "p35": 1990000,
    "p40": 2040000,
    "p45": 2090000,
    "p50": 2130000,
    "p55": 2175000,
    "p60": 2220000,
    "p65": 2270000,
    "p70": 2330000,
    "p75": 2390000,
    "p80": 2470000,
    "p85": 2580000,
    "p90": 2730000,
    "p95": 2960000
  },
  "histogram": [
    { "rangeMin": 1200000, "rangeMax": 1400000, "count": 120, "frequency": 0.012 }
  ],
  "additionalMetrics": {
    "contingency_p80": 370000,
    "contingency_p90": 630000
  }
}
```

### `StatsSummaryDto` (módulo mining)

```json
{
  "mean": 145000000,
  "stdDev": 18000000,
  "cv": 0.124,
  "min": 95000000,
  "max": 210000000,
  "p10": 120000000,
  "p50": 143000000,
  "p80": 163000000,
  "p90": 172000000,
  "p95": 182000000,
  "n": 50000
}
```

### Errores

| Código | Causa | Estructura |
|--------|-------|------------|
| 400 | Validación fallida (input inválido, estructura incorrecta) | `{ "error": "mensaje" }` |
| 500 | Error interno del servidor | ProblemDetails estándar |

---

## Persistencia e historial

Cada llamada exitosa a cualquier endpoint de simulación guarda automáticamente:

- `simulationType` — nombre del tipo de análisis
- `requestJson` — request completo serializado
- `resultJson` — resultado completo serializado
- `simulations` — número de iteraciones
- `createdAt` — timestamp UTC

**Base de datos:** PostgreSQL. Cadena de conexión en `appsettings.json` → `"Connection"`.

---

## Análisis previos sugeridos

Antes de implementar nuevas funcionalidades, conviene revisar los siguientes puntos del estado actual:

### 1. Endpoints del módulo Mining sin UI en RazorIdentity

Los 7 endpoints de `/api/mining` no tienen página asociada en RazorIdentity. Representan la mayor área de expansión disponible. Candidatos por prioridad:

| Prioridad | Endpoint | Complejidad UI |
|-----------|----------|----------------|
| Alta | `/api/mining/capex` | Media — tabla de ítems similar a Costos |
| Alta | `/api/mining/financial` | Alta — muchos parámetros, resultados VAN/TIR |
| Media | `/api/mining/risks` | Baja — similar a Riesgos existente |
| Media | `/api/mining/schedule` | Media — similar a Cronograma existente |
| Baja | `/api/mining/project` | Alta — requiere combinar todos los módulos |

### 2. Módulo SRA/CRA — validar coherencia de simulations

El endpoint `/sra` y `/cra` reciben `simulations` en tres lugares (raíz + dentro de cada sub-objeto). Verificar que el servicio usa el valor raíz como override y no mezcla iteraciones entre sub-análisis.

### 3. Historial — uso actual

El historial guarda `requestJson` y `resultJson` completos pero la UI solo muestra una lista básica. Opciones de mejora:
- Reproducir una simulación pasada (cargar el request en el formulario)
- Comparar dos simulaciones del mismo tipo
- Exportar CSV/Excel de resultados históricos

### 4. Distribuciones — cobertura en frontend

El frontend de RazorIdentity expone `Triangular`, `Normal`, `PERT`, `Uniform`. La API del módulo mining usa además `BetaPERT` con λ configurable. Si se agrega UI para mining, hay que exponer `BetaPERT` y el campo `lambda`.

### 5. Correlaciones — no expuestas en UI general

El módulo mining implementa correlaciones via Cholesky (`CorrelationPairDto`). El módulo general no las soporta. Si se necesita modelar correlación entre componentes de costo (ej: acero + mano de obra), hay que evaluar si agregar al motor general o derivar al módulo mining.

### 6. AdditionalMetrics — documentar por tipo

Cada tipo de simulación devuelve métricas adicionales diferentes en `additionalMetrics`. Esta información no está documentada en los DTOs. Conviene revisar el `MonteCarloService.cs` para listar qué claves devuelve cada endpoint y así construir la UI de resultados correctamente.
