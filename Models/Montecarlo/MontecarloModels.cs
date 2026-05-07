namespace RazorIdentity.Models.Montecarlo;

// Distribuciones disponibles: Triangular | Normal | PERT | Uniform

// ── Riesgos en Costo ─────────────────────────────────────────────────────────

public class RiskItem
{
    public string Tipo { get; set; } = "Amenaza";  // "Amenaza" | "Oportunidad"
    public string Origin { get; set; } = "";
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string Cause { get; set; } = "";
    public string ResponsePlan { get; set; } = "";
    public double Probability { get; set; }
    public double MinImpact { get; set; }
    public double MostLikelyImpact { get; set; }
    public double MaxImpact { get; set; }
    public string Distribution { get; set; } = "Triangular";
    public string EstimationBase { get; set; } = "";
    public string Opportunity { get; set; } = "";
    public string Threat { get; set; } = "";
}

public class RiskAnalysisRequest
{
    public List<RiskItem> Risks { get; set; } = new();
    public int Simulations { get; set; } = 10000;
}

// ── Riesgos AJAX (mismo contrato que RiskAnalysisRequest, camelCase friendly) ─
public class RisksAjaxRequest
{
    public List<RiskItem> Risks { get; set; } = new();
    public int Simulations { get; set; } = 10000;
}

// ── Costos ───────────────────────────────────────────────────────────────────

public class CostComponent
{
    public string Name { get; set; } = "";
    public double BaseCost { get; set; }
    public string EstimationClass { get; set; } = "";
    public double MinCost { get; set; }
    public double MostLikelyCost { get; set; }
    public double MaxCost { get; set; }
    // Bernoulli: probability + most-likely savings amount
    public double OpportunityProbability { get; set; }
    public double OpportunityAmount { get; set; }
    // Bernoulli: probability + most-likely extra cost
    public double ThreatProbability { get; set; }
    public double ThreatAmount { get; set; }
    public string Distribution { get; set; } = "Triangular";
    public string Observations { get; set; } = "";
}

public class CostEstimationRequest
{
    public List<CostComponent> Components { get; set; } = new();
    public int Simulations { get; set; } = 10000;
}

// ── Cronograma ───────────────────────────────────────────────────────────────

public class ScheduleTaskInput
{
    public string Name { get; set; } = "";
    public double MinDays { get; set; }
    public double MostLikelyDays { get; set; }
    public double MaxDays { get; set; }
    public double PlannedDays { get; set; }
    public string DependenciesText { get; set; } = "";
    public double Opportunities { get; set; }
    public double Threats { get; set; }
    public string Distribution { get; set; } = "Triangular";
    public string Observations { get; set; } = "";
}

public class ScheduleEstimationRequest
{
    public List<ScheduleTaskApiItem> Tasks { get; set; } = new();
    public int Simulations { get; set; } = 10000;
    public double? GlobalPlannedDays { get; set; }
}

public class ScheduleTaskApiItem
{
    public string Name { get; set; } = "";
    public double MinDays { get; set; }
    public double MostLikelyDays { get; set; }
    public double MaxDays { get; set; }
    public double PlannedDays { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public double Opportunities { get; set; }
    public double Threats { get; set; }
    public string Distribution { get; set; } = "Triangular";
}

// ── Riesgos del Programa (impacto en días) ───────────────────────────────────

public class ScheduleRiskItem
{
    public string Cause { get; set; } = "";
    public string RiskEvent { get; set; } = "";
    public string Consequence { get; set; } = "";
    public double Probability { get; set; }
    public double MinImpact { get; set; }
    public double MostLikelyImpact { get; set; }
    public double MaxImpact { get; set; }
    public string Distribution { get; set; } = "Triangular";
    public string Observations { get; set; } = "";
}

public class ScheduleRiskAnalysisRequest
{
    public List<ScheduleRiskItem> Risks { get; set; } = new();
    public int Simulations { get; set; } = 10000;
}

// ── SRA: Cronograma + Riesgos Programa ───────────────────────────────────────

public class SraRequest
{
    public List<ScheduleTaskApiItem> Tasks { get; set; } = new();
    public List<ScheduleRiskItem> Risks { get; set; } = new();
    public int Simulations { get; set; } = 10000;
    public double? GlobalPlannedDays { get; set; }
}

// ── CRA: Costos + Riesgos ─────────────────────────────────────────────────────

public class CraRequest
{
    public List<CostComponent> Components { get; set; } = new();
    public List<RiskItem> Risks { get; set; } = new();
    public int Simulations { get; set; } = 10000;
}

// ── VaR ──────────────────────────────────────────────────────────────────────

public class AssetItem
{
    public string Name { get; set; } = "";
    public double InitialValue { get; set; }
    public double ExpectedAnnualReturn { get; set; }
    public double AnnualVolatility { get; set; }
}

public class VaRRequest
{
    public List<AssetItem> Assets { get; set; } = new();
    public int HorizonDays { get; set; } = 252;
    public double ConfidenceLevel { get; set; } = 0.95;
    public int Simulations { get; set; } = 10000;
}

// ── Correlaciones ────────────────────────────────────────────────────────────

public class CorrelationVarInputModel
{
    public string Name         { get; set; } = "";
    public string Unit         { get; set; } = "";
    public string Distribution { get; set; } = "Triangular";
    public double P1 { get; set; }
    public double P2 { get; set; }
    public double P3 { get; set; }
}

public class CorrelationInputPairModel
{
    public int    IndexA         { get; set; }
    public int    IndexB         { get; set; }
    public double SpearmanTarget { get; set; }
}

public class CorrelationAnalysisRequestModel
{
    public List<CorrelationVarInputModel>   Variables   { get; set; } = new();
    public List<CorrelationInputPairModel>  Pairs       { get; set; } = new();
    public int                              Simulations { get; set; } = 10000;
}

// ── IA: Consulta al asistente experto Monte Carlo ────────────────────────────

public class McAiConsultRequest
{
    public string Question      { get; set; } = "";
    public string? ContextJson   { get; set; }   // JSON resumido de resultados MC disponibles
    public string? TcContextJson { get; set; }   // JSON análisis Taller de Costos (tiempo real)
    public string? TabId         { get; set; }   // pestaña activa cuando se consultó
    public string? ProjectName   { get; set; }   // nombre del proyecto (si fue definido)
}

// ── Proyecto de sesión (persistencia multi-tab) ───────────────────────────────

public class ProjectSessionSaveRequest
{
    public Guid? Id { get; set; }
    public string ProjectName { get; set; } = "";
    public string Status { get; set; } = "InProgress";
    public Dictionary<string, ProjectTabData> Tabs { get; set; } = new();
}

public class ProjectTabData
{
    public string InputJson { get; set; } = "{}";
    public string ResultJson { get; set; } = "";
    public DateTime? CompletedAt { get; set; }
}

public class DeleteProjectRequest
{
    public Guid Id { get; set; }
}

public class ProjectTabSaveRequest
{
    public Guid?           Id          { get; set; }
    public string          ProjectName { get; set; } = "";
    public string          TabId       { get; set; } = "";
    public ProjectTabData  TabData     { get; set; } = new();
}

// ── IA: Sugerencia automática de matriz de correlación ───────────────────────

public class CorrMatrixAiRequest
{
    public List<CorrAiVarDto> Variables     { get; set; } = new();
    public string?            ProjectName   { get; set; }
    public string?            Source        { get; set; }  // "contracts" | "risks" | "both" | "manual"
    public bool               WithJustification { get; set; } = true;
}

public class CorrAiVarDto
{
    public string Name         { get; set; } = "";
    public string Unit         { get; set; } = "USD";
    public string Distribution { get; set; } = "Triangular";
    public double P1 { get; set; }
    public double P2 { get; set; }
    public double P3 { get; set; }
}

// ── Respuesta local: Simulación de Riesgos ───────────────────────────────────
// Formato compatible con renderMcResult() del frontend

public class RiskMcStatistics
{
    public double Mean     { get; set; }
    public double StdDev   { get; set; }
    public double Min      { get; set; }
    public double Max      { get; set; }
    public double P5       { get; set; }
    public double P10      { get; set; }
    public double P25      { get; set; }
    public double P50      { get; set; }
    public double P75      { get; set; }
    public double P80      { get; set; }
    public double P90      { get; set; }
    public double P95      { get; set; }
    public double Skewness { get; set; }
    public double Kurtosis { get; set; }
}

public class RiskMcHistogramBin
{
    public double RangeMin  { get; set; }
    public double RangeMax  { get; set; }
    public double Frequency { get; set; }
}

public class RiskMcAdditionalMetrics
{
    public double  ProbabilityOfAnyRisk { get; set; }
    public double? ThreatP50            { get; set; }
    public double? ThreatP80            { get; set; }
    public double? ThreatP90            { get; set; }
    public double? OpportunityP50       { get; set; }
    public double? OpportunityP80       { get; set; }
    public int     ThreatCount          { get; set; }
    public int     OpportunityCount     { get; set; }
}

public class RiskMcResponse
{
    public RiskMcStatistics          Statistics        { get; set; } = new();
    public List<RiskMcHistogramBin>  Histogram         { get; set; } = new();
    public string                    SimulationType    { get; set; } = "RiskAnalysis";
    public RiskMcAdditionalMetrics   AdditionalMetrics { get; set; } = new();
}
