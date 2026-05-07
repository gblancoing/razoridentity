using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models.TallerCostos;
using System.Security.Claims;
using System.Text.Json;

namespace RazorIdentity.Pages;

[Authorize]
public class TallerCostosPdfModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public TallerCostosPdfModel(ApplicationDbContext db) => _db = db;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    // ── Datos del reporte ────────────────────────────────────────────────────
    public TallerProyecto  Proyecto   { get; private set; } = null!;
    public TallerContrato  Contrato   { get; private set; } = null!;
    public List<TallerItem> Items     { get; private set; } = new();
    public TallerMcResultado? McResultado { get; private set; }

    // Datos deserializados para las vistas
    public List<double> ItemMedias    { get; private set; } = new();
    public List<double> HistLabels    { get; private set; } = new();
    public List<int>    HistCounts    { get; private set; } = new();
    public List<CdfPunto> CdfPuntos   { get; private set; } = new();

    public record CdfPunto(double X, double Y);

    public async Task<IActionResult> OnGetAsync(Guid contratoId)
    {
        var contrato = await _db.TallerContratos
            .Include(c => c.Proyecto)
            .Include(c => c.Items.OrderBy(i => i.Orden))
            .FirstOrDefaultAsync(c => c.Id == contratoId);

        if (contrato is null || contrato.Proyecto.UserId != UserId)
            return Forbid();

        Contrato = contrato;
        Proyecto = contrato.Proyecto;
        Items    = contrato.Items.ToList();

        McResultado = await _db.TallerMcResultados
            .Where(r => r.ContratoId == contratoId)
            .OrderByDescending(r => r.FechaCalculo)
            .FirstOrDefaultAsync();

        if (McResultado is not null)
        {
            try
            {
                ItemMedias = JsonSerializer.Deserialize<List<double>>(McResultado.ItemMediasJson) ?? new();
                var hist   = JsonSerializer.Deserialize<JsonElement>(McResultado.HistogramaJson);
                if (hist.TryGetProperty("labels", out var lbls))
                    HistLabels = lbls.Deserialize<List<double>>() ?? new();
                if (hist.TryGetProperty("counts", out var cnts))
                    HistCounts = cnts.Deserialize<List<int>>() ?? new();
                var cdf = JsonSerializer.Deserialize<JsonElement>(McResultado.CdfJson);
                foreach (var pt in cdf.EnumerateArray())
                    CdfPuntos.Add(new CdfPunto(pt.GetProperty("x").GetDouble(), pt.GetProperty("y").GetDouble()));
            }
            catch { /* datos corruptos — se ignoran */ }
        }

        return Page();
    }

    // Helpers de formato para la vista
    public string FmtUsd(decimal v)  => $"${v:N0} USD";
    public string FmtKusd(decimal v) => $"{v:N2} USD$";
    public string FmtKusd(double v)  => $"{v:N2} USD$";
    public string FmtPct(decimal? v) => v.HasValue ? $"{v.Value:N1}%" : "—";
}
