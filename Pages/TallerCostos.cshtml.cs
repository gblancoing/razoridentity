using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Data;
using RazorIdentity.Models.Montecarlo;
using RazorIdentity.Models.TallerCostos;
using RazorIdentity.Services;
using System.Security.Claims;
using System.Text.Json;

namespace RazorIdentity.Pages;

[Authorize]
public class TallerCostosModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IMontecarloApiClient _api;
    private readonly MonteCarloService _mc;
    private readonly MiroFishService _miroFish;
    private readonly ILogger<TallerCostosModel> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions _caseInsensitiveOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TallerCostosModel(ApplicationDbContext db, IMontecarloApiClient api,
        MonteCarloService mc, MiroFishService miroFish, ILogger<TallerCostosModel> logger)
    {
        _db       = db;
        _api      = api;
        _mc       = mc;
        _miroFish = miroFish;
        _logger   = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    public void OnGet() { }

    // ════════════════════════════════════════════════════════════════════════
    // PROYECTOS
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnGetProyectosJsonAsync()
    {
        var lista = await _db.TallerProyectos
            .Where(p => p.UserId == UserId)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new
            {
                p.Id, p.Nombre, p.Codigo,
                fechaEjercicio = p.FechaEjercicio.ToString("dd-MM-yy"),
                p.Organizacion, p.Estado,
                updatedAt = p.UpdatedAt.ToString("dd/MM/yyyy HH:mm"),
                totalContratos = p.Contratos.Count()
            })
            .ToListAsync();
        return new JsonResult(lista, _jsonOpts);
    }

    public async Task<IActionResult> OnGetProyectoJsonAsync(Guid id)
    {
        var p = await _db.TallerProyectos
            .Where(x => x.Id == id && x.UserId == UserId)
            .Select(x => new
            {
                x.Id, x.Nombre, x.Codigo,
                fechaEjercicio = x.FechaEjercicio.ToString("yyyy-MM-dd"),
                x.Organizacion, x.Estado
            })
            .FirstOrDefaultAsync();
        if (p is null) return NotFound();
        return new JsonResult(p, _jsonOpts);
    }

    public async Task<IActionResult> OnPostProyectoAsync([FromBody] GuardarProyectoDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Datos inválidos" });

        if (!DateOnly.TryParseExact(dto.FechaEjercicio, "yyyy-MM-dd", out var fechaEjercicio))
            return BadRequest(new { error = "Formato de fecha inválido. Use yyyy-MM-dd." });

        if (dto.Id == Guid.Empty)
        {
            var p = new TallerProyecto
            {
                UserId         = UserId,
                Nombre         = dto.Nombre.Trim(),
                Codigo         = dto.Codigo.Trim().ToUpper(),
                FechaEjercicio = fechaEjercicio,
                Organizacion   = dto.Organizacion?.Trim() ?? "",
                Estado           = "Activo"
            };
            _db.TallerProyectos.Add(p);
            await _db.SaveChangesAsync();
            return new JsonResult(new { ok = true, id = p.Id }, _jsonOpts);
        }
        else
        {
            var p = await _db.TallerProyectos
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.UserId == UserId);
            if (p is null) return NotFound();
            p.Nombre         = dto.Nombre.Trim();
            p.Codigo         = dto.Codigo.Trim().ToUpper();
            p.FechaEjercicio = fechaEjercicio;
            p.Organizacion   = dto.Organizacion?.Trim() ?? "";
            p.UpdatedAt       = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return new JsonResult(new { ok = true, id = p.Id }, _jsonOpts);
        }
    }

    public async Task<IActionResult> OnDeleteProyectoAsync(Guid id)
    {
        var p = await _db.TallerProyectos
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (p is null) return NotFound();
        _db.TallerProyectos.Remove(p);
        await _db.SaveChangesAsync();
        return new JsonResult(new { ok = true }, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // FAMILIAS
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnGetFamiliasJsonAsync(Guid proyectoId)
    {
        if (!await OwnProject(proyectoId)) return Forbid();
        var lista = await _db.TallerFamilias
            .Where(f => f.ProyectoId == proyectoId)
            .OrderBy(f => f.Orden)
            .Select(f => new
            {
                f.Id, f.Nombre, f.Descripcion, f.Orden,
                totalContratos = f.Contratos.Count()
            })
            .ToListAsync();
        return new JsonResult(lista, _jsonOpts);
    }

    public async Task<IActionResult> OnPostFamiliaAsync([FromBody] GuardarFamiliaDto dto)
    {
        if (!await OwnProject(dto.ProyectoId)) return Forbid();

        if (dto.Id == Guid.Empty)
        {
            var orden = await _db.TallerFamilias
                .Where(f => f.ProyectoId == dto.ProyectoId)
                .CountAsync() + 1;
            var f = new TallerFamilia
            {
                ProyectoId  = dto.ProyectoId,
                Nombre      = dto.Nombre.Trim(),
                Descripcion = dto.Descripcion?.Trim(),
                Orden       = orden
            };
            _db.TallerFamilias.Add(f);
            await _db.SaveChangesAsync();
            return new JsonResult(new { ok = true, id = f.Id, f.Nombre, f.Orden }, _jsonOpts);
        }
        else
        {
            var f = await _db.TallerFamilias
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.ProyectoId == dto.ProyectoId);
            if (f is null) return NotFound();
            f.Nombre      = dto.Nombre.Trim();
            f.Descripcion = dto.Descripcion?.Trim();
            f.UpdatedAt   = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return new JsonResult(new { ok = true, id = f.Id }, _jsonOpts);
        }
    }

    public async Task<IActionResult> OnDeleteFamiliaAsync(Guid id)
    {
        var f = await _db.TallerFamilias
            .Include(x => x.Proyecto)
            .Include(x => x.Contratos)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (f is null || f.Proyecto.UserId != UserId) return Forbid();
        if (f.Contratos.Any())
            return BadRequest(new { error = $"La familia '{f.Nombre}' tiene {f.Contratos.Count} contrato(s) asignado(s). Reasigna los contratos antes de eliminarla." });
        _db.TallerFamilias.Remove(f);
        await _db.SaveChangesAsync();
        return new JsonResult(new { ok = true }, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // EMPRESAS CONTRATISTAS (por proyecto)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnGetEmpresasJsonAsync(Guid proyectoId)
    {
        if (!await OwnProject(proyectoId)) return Forbid();
        var lista = await _db.TallerEmpresas
            .Where(e => e.ProyectoId == proyectoId)
            .OrderBy(e => e.Orden)
            .Select(e => new
            {
                e.Id, e.Rut, e.Nombre, e.Contacto, e.Email, e.Telefono, e.Notas, e.Orden,
                totalPaquetes = e.Contratos.Count()
            })
            .ToListAsync();
        return new JsonResult(lista, _jsonOpts);
    }

    public async Task<IActionResult> OnPostEmpresaAsync([FromBody] GuardarEmpresaDto dto)
    {
        if (!await OwnProject(dto.ProyectoId)) return Forbid();

        var nombre = (dto.Nombre ?? "").Trim();
        var rut    = (dto.Rut ?? "").Trim();
        if (string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(rut))
            return BadRequest(new { error = "RUT y nombre o razón social son obligatorios." });

        var contacto  = string.IsNullOrWhiteSpace(dto.Contacto) ? null : dto.Contacto!.Trim();
        var email     = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email!.Trim();
        var telefono  = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono!.Trim();
        var notas     = string.IsNullOrWhiteSpace(dto.Notas) ? null : dto.Notas!.Trim();

        var rutDuplicado = await _db.TallerEmpresas
            .AnyAsync(x => x.ProyectoId == dto.ProyectoId && x.Rut == rut && x.Id != dto.Id);
        if (rutDuplicado)
            return BadRequest(new { error = "Ya existe una empresa con ese RUT en este proyecto." });

        if (dto.Id == Guid.Empty)
        {
            var orden = await _db.TallerEmpresas
                .Where(e => e.ProyectoId == dto.ProyectoId)
                .CountAsync() + 1;
            var e = new TallerEmpresa
            {
                ProyectoId = dto.ProyectoId,
                Rut        = rut,
                Nombre     = nombre,
                Contacto   = contacto,
                Email      = email,
                Telefono   = telefono,
                Notas      = notas,
                Orden      = orden
            };
            _db.TallerEmpresas.Add(e);
            await _db.SaveChangesAsync();
            return new JsonResult(new { ok = true, id = e.Id, e.Nombre, e.Orden }, _jsonOpts);
        }

        var ex = await _db.TallerEmpresas
            .FirstOrDefaultAsync(x => x.Id == dto.Id && x.ProyectoId == dto.ProyectoId);
        if (ex is null) return NotFound();
        ex.Rut        = rut;
        ex.Nombre     = nombre;
        ex.Contacto   = contacto;
        ex.Email      = email;
        ex.Telefono   = telefono;
        ex.Notas     = notas;
        ex.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new JsonResult(new { ok = true, id = ex.Id }, _jsonOpts);
    }

    public async Task<IActionResult> OnDeleteEmpresaAsync(Guid id)
    {
        var e = await _db.TallerEmpresas
            .Include(x => x.Proyecto)
            .Include(x => x.Contratos)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (e is null || e.Proyecto.UserId != UserId) return Forbid();
        if (e.Contratos.Any())
            return BadRequest(new { error = $"La empresa «{e.Nombre}» tiene {e.Contratos.Count} paquete(s) asignado(s). Reasigna o elimina esos contratos primero." });
        _db.TallerEmpresas.Remove(e);
        await _db.SaveChangesAsync();
        return new JsonResult(new { ok = true }, _jsonOpts);
    }

    // Asignar/desasignar contrato a familia
    public async Task<IActionResult> OnPostAsignarFamiliaAsync([FromBody] AsignarFamiliaDto dto)
    {
        var c = await _db.TallerContratos
            .Include(x => x.Proyecto)
            .FirstOrDefaultAsync(x => x.Id == dto.ContratoId);
        if (c is null || c.Proyecto.UserId != UserId) return Forbid();

        // Verificar que la familia pertenece al mismo proyecto (si no es null)
        if (dto.FamiliaId.HasValue)
        {
            var familiaExists = await _db.TallerFamilias
                .AnyAsync(f => f.Id == dto.FamiliaId && f.ProyectoId == c.ProyectoId);
            if (!familiaExists) return BadRequest(new { error = "Familia no encontrada en este proyecto." });
        }

        c.FamiliaId = dto.FamiliaId;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new JsonResult(new { ok = true }, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // CONTRATOS
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnGetContratosJsonAsync(Guid proyectoId)
    {
        if (!await OwnProject(proyectoId)) return Forbid();
        var lista = await _db.TallerContratos
            .Where(c => c.ProyectoId == proyectoId)
            .OrderBy(c => c.Orden)
            .Select(c => new
            {
                c.Id, c.Codigo, c.Orden, c.NombrePaquete,
                c.CapexUsd, c.CompometidoUsd, c.PorComprometidoUsd, c.EstimadoTerminoUsd,
                c.TasaCambio, c.Factor,
                fechaTasaCambio = c.FechaTasaCambio.HasValue ? c.FechaTasaCambio.Value.ToString("yyyy-MM-dd") : null,
                totalItems = c.Items.Count(),
                itemsIncertidumbre = c.Items.Count(i => !i.EsCerteza),
                familiaId   = c.FamiliaId,
                familiaNombre = c.Familia != null ? c.Familia.Nombre : null,
                empresaId   = c.EmpresaId,
                empresaNombre = c.Empresa != null ? c.Empresa.Nombre : null
            })
            .ToListAsync();
        return new JsonResult(lista, _jsonOpts);
    }

    public async Task<IActionResult> OnPostContratoAsync([FromBody] GuardarContratoDto dto)
    {
        if (!await OwnProject(dto.ProyectoId)) return Forbid();

        DateOnly? fechaTasaCambio = null;
        if (!string.IsNullOrEmpty(dto.FechaTasaCambio))
        {
            if (!DateOnly.TryParseExact(dto.FechaTasaCambio, "yyyy-MM-dd", out var parsedFecha))
                return BadRequest(new { error = "Formato de fecha de tasa de cambio inválido. Use yyyy-MM-dd." });
            fechaTasaCambio = parsedFecha;
        }

        Guid? empresaIdFinal = dto.EmpresaId;
        if (empresaIdFinal.HasValue)
        {
            var okEmpresa = await _db.TallerEmpresas
                .AnyAsync(e => e.Id == empresaIdFinal.Value && e.ProyectoId == dto.ProyectoId);
            if (!okEmpresa) return BadRequest(new { error = "La empresa seleccionada no pertenece a este proyecto." });
        }

        if (dto.Id == Guid.Empty)
        {
            var orden = await _db.TallerContratos
                .Where(c => c.ProyectoId == dto.ProyectoId)
                .CountAsync() + 1;
            var codigo = $"CC{orden:D3}";
            var c = new TallerContrato
            {
                ProyectoId         = dto.ProyectoId,
                Codigo             = !string.IsNullOrWhiteSpace(dto.Codigo) ? dto.Codigo : codigo,
                Orden              = orden,
                NombrePaquete      = dto.NombrePaquete?.Trim() ?? "",
                CapexUsd           = dto.CapexUsd,
                CompometidoUsd     = dto.CompometidoUsd,
                PorComprometidoUsd = dto.PorComprometidoUsd,
                EstimadoTerminoUsd = dto.EstimadoTerminoUsd,
                TasaCambio         = dto.TasaCambio,
                Factor             = dto.Factor,
                FechaTasaCambio    = fechaTasaCambio,
                EmpresaId          = empresaIdFinal
            };
            _db.TallerContratos.Add(c);
            await _db.SaveChangesAsync();
            return new JsonResult(new { ok = true, id = c.Id, codigo = c.Codigo }, _jsonOpts);
        }
        else
        {
            var c = await _db.TallerContratos
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.ProyectoId == dto.ProyectoId);
            if (c is null) return NotFound();
            c.NombrePaquete      = dto.NombrePaquete?.Trim() ?? "";
            c.CapexUsd           = dto.CapexUsd;
            c.CompometidoUsd     = dto.CompometidoUsd;
            c.PorComprometidoUsd = dto.PorComprometidoUsd;
            c.EstimadoTerminoUsd = dto.EstimadoTerminoUsd;
            c.TasaCambio         = dto.TasaCambio;
            c.Factor             = dto.Factor;
            c.FechaTasaCambio    = fechaTasaCambio;
            c.EmpresaId          = empresaIdFinal;
            c.UpdatedAt          = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return new JsonResult(new { ok = true, id = c.Id }, _jsonOpts);
        }
    }

    public async Task<IActionResult> OnDeleteContratoAsync(Guid id)
    {
        var c = await _db.TallerContratos
            .Include(x => x.Proyecto)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c is null || c.Proyecto.UserId != UserId) return Forbid();
        _db.TallerContratos.Remove(c);
        await _db.SaveChangesAsync();
        return new JsonResult(new { ok = true }, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // ÍTEMS (guardado masivo por contrato)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnGetItemsJsonAsync(Guid contratoId)
    {
        var contrato = await _db.TallerContratos
            .Include(c => c.Proyecto)
            .FirstOrDefaultAsync(c => c.Id == contratoId);
        if (contrato is null || contrato.Proyecto.UserId != UserId) return Forbid();

        var items = await _db.TallerItems
            .Where(i => i.ContratoId == contratoId)
            .OrderBy(i => i.Orden)
            .ToListAsync();

        var result = items.Select(i => new
        {
            i.Id, i.Orden, i.CodigoItem, i.Descripcion, i.Unidad, i.SubPartida,
            i.EsCerteza, i.EsPorComprometer, i.CostoUsd,
            i.ClaseEstimacion, i.Consideraciones,
            i.MinPct, i.MinKusd,
            probablePct  = i.ProbablePct  ?? 100m,
            probableKusd = i.ProbableKusd ?? (i.CostoUsd / 1000m),
            i.MaxPct, i.MaxKusd, i.ViaRiesgo,
            i.Oportunidades, i.Amenazas, i.Clase, i.Peso,
            calculoClase = i.CalculoClase,
            i.CostoClpOverride
        });
        return new JsonResult(result, _jsonOpts);
    }

    public async Task<IActionResult> OnPostItemsAsync([FromBody] GuardarItemsDto dto)
    {
        var contrato = await _db.TallerContratos
            .Include(c => c.Proyecto)
            .FirstOrDefaultAsync(c => c.Id == dto.ContratoId);
        if (contrato is null || contrato.Proyecto.UserId != UserId) return Forbid();

        // Borrar todos los ítems existentes y reemplazar (estrategia simple)
        var existentes = await _db.TallerItems
            .Where(i => i.ContratoId == dto.ContratoId)
            .ToListAsync();
        _db.TallerItems.RemoveRange(existentes);

        var nuevos = dto.Items.Select((item, idx) => new TallerItem
        {
            ContratoId     = dto.ContratoId,
            Orden          = idx + 1,
            CodigoItem     = item.CodigoItem?.Trim() ?? "",
            Descripcion    = item.Descripcion?.Trim() ?? "",
            Unidad         = item.Unidad?.Trim() ?? "USD",
            SubPartida     = item.SubPartida?.Trim() ?? "",
            EsCerteza      = item.EsCerteza,
            EsPorComprometer = item.EsPorComprometer,
            CostoUsd       = item.CostoUsd,
            ClaseEstimacion = item.ClaseEstimacion ?? "Clase 3",
            Consideraciones = item.Consideraciones?.Trim() ?? "",
            MinPct         = item.MinPct,
            MinKusd        = item.MinKusd,
            ProbablePct    = item.ProbablePct ?? 100m,
            ProbableKusd   = item.ProbableKusd ?? (item.CostoUsd / 1000m),
            MaxPct         = item.MaxPct,
            MaxKusd        = item.MaxKusd,
            ViaRiesgo      = item.ViaRiesgo,
            Oportunidades  = item.Oportunidades?.Trim() ?? "",
            Amenazas       = item.Amenazas?.Trim() ?? "",
            Clase          = item.Clase,
            Peso           = item.Peso,
            CostoClpOverride = item.CostoClpOverride
        }).ToList();

        _db.TallerItems.AddRange(nuevos);
        contrato.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new JsonResult(new { ok = true, count = nuevos.Count }, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // REVISIONES DE RIESGOS
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnGetRevisionesJsonAsync(Guid proyectoId)
    {
        if (!await OwnProject(proyectoId)) return Forbid();
        var lista = await _db.TallerRevisionesRiesgos
            .Where(r => r.ProyectoId == proyectoId)
            .OrderByDescending(r => r.NumeroRevision)
            .Select(r => new
            {
                r.Id, r.NumeroRevision, r.Descripcion,
                fechaCreacion = r.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                totalRiesgos = r.Riesgos.Count(),
                totalOportunidades = r.Riesgos.Count(x => x.EsOportunidad)
            })
            .ToListAsync();
        return new JsonResult(lista, _jsonOpts);
    }

    public async Task<IActionResult> OnGetRevisionJsonAsync(Guid id)
    {
        var rev = await _db.TallerRevisionesRiesgos
            .Include(r => r.Proyecto)
            .Include(r => r.Riesgos.OrderBy(x => x.Orden))
            .FirstOrDefaultAsync(r => r.Id == id);
        if (rev is null || rev.Proyecto.UserId != UserId) return Forbid();

        var result = new
        {
            rev.Id, rev.NumeroRevision, rev.Descripcion,
            fechaCreacion = rev.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
            riesgos = rev.Riesgos.Select(r => new
            {
                r.Id, r.Orden, r.Origen, r.CodigoRiesgo, r.Titulo, r.Descripcion,
                r.ImpactoProableUsd, r.ImpactoMinUsd, r.ImpactoMaxUsd,
                probabilidad = r.Probabilidad ?? 100m,
                r.EsOportunidad, r.NotasCambio, r.ItemViaRiesgoId
            })
        };
        return new JsonResult(result, _jsonOpts);
    }

    public async Task<IActionResult> OnPostRevisionAsync([FromBody] GuardarRevisionDto dto)
    {
        if (!await OwnProject(dto.ProyectoId)) return Forbid();

        var maxRev = await _db.TallerRevisionesRiesgos
            .Where(r => r.ProyectoId == dto.ProyectoId)
            .MaxAsync(r => (int?)r.NumeroRevision) ?? 0;

        var nueva = new TallerRevisionRiesgos
        {
            ProyectoId      = dto.ProyectoId,
            NumeroRevision  = maxRev + 1,
            Descripcion     = dto.Descripcion?.Trim() ?? $"Rev{maxRev + 1}",
            FechaCreacion   = DateTime.UtcNow,
            UsuarioId       = UserId
        };
        _db.TallerRevisionesRiesgos.Add(nueva);

        // Copiar riesgos de la revisión anterior si existe
        if (dto.CopiarDeRevisionId.HasValue)
        {
            var anterior = await _db.TallerRevisionesRiesgos
                .Include(r => r.Riesgos)
                .FirstOrDefaultAsync(r => r.Id == dto.CopiarDeRevisionId.Value);
            if (anterior is not null)
            {
                foreach (var r in anterior.Riesgos.OrderBy(x => x.Orden))
                {
                    _db.TallerRiesgos.Add(new TallerRiesgo
                    {
                        RevisionId          = nueva.Id,
                        Orden               = r.Orden,
                        Origen              = r.Origen,
                        CodigoRiesgo        = r.CodigoRiesgo,
                        Titulo              = r.Titulo,
                        Descripcion         = r.Descripcion,
                        ImpactoProableUsd   = r.ImpactoProableUsd,
                        ImpactoMinUsd       = r.ImpactoMinUsd,
                        ImpactoMaxUsd       = r.ImpactoMaxUsd,
                        EsOportunidad       = r.EsOportunidad,
                        NotasCambio         = "",
                        ItemViaRiesgoId     = r.ItemViaRiesgoId
                    });
                }
            }
        }

        await _db.SaveChangesAsync();
        return new JsonResult(new { ok = true, id = nueva.Id, numero = nueva.NumeroRevision }, _jsonOpts);
    }

    public async Task<IActionResult> OnDeleteRevisionAsync(Guid id)
    {
        var rev = await _db.TallerRevisionesRiesgos
            .Include(r => r.Proyecto)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (rev is null || rev.Proyecto.UserId != UserId) return Forbid();
        _db.TallerRevisionesRiesgos.Remove(rev);
        await _db.SaveChangesAsync();
        return new JsonResult(new { ok = true }, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // RIESGOS
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostRiesgoAsync([FromBody] GuardarRiesgoDto dto)
    {
        var rev = await _db.TallerRevisionesRiesgos
            .Include(r => r.Proyecto)
            .FirstOrDefaultAsync(r => r.Id == dto.RevisionId);
        if (rev is null || rev.Proyecto.UserId != UserId) return Forbid();

        if (dto.Id == Guid.Empty)
        {
            var orden = await _db.TallerRiesgos.Where(r => r.RevisionId == dto.RevisionId).CountAsync() + 1;
            var r = new TallerRiesgo
            {
                RevisionId        = dto.RevisionId,
                Orden             = orden,
                Origen            = dto.Origen?.Trim() ?? "Proyecto",
                CodigoRiesgo      = dto.CodigoRiesgo?.Trim() ?? "",
                Titulo            = dto.Titulo?.Trim() ?? "",
                Descripcion       = dto.Descripcion?.Trim() ?? "",
                ImpactoProableUsd = dto.ImpactoProableUsd,
                ImpactoMinUsd     = dto.ImpactoMinUsd,
                ImpactoMaxUsd     = dto.ImpactoMaxUsd,
                Probabilidad      = dto.Probabilidad,
                EsOportunidad     = dto.EsOportunidad,
                NotasCambio       = dto.NotasCambio?.Trim() ?? "",
                ItemViaRiesgoId   = dto.ItemViaRiesgoId
            };
            _db.TallerRiesgos.Add(r);
            await _db.SaveChangesAsync();
            return new JsonResult(new { ok = true, id = r.Id }, _jsonOpts);
        }
        else
        {
            var r = await _db.TallerRiesgos.FirstOrDefaultAsync(x => x.Id == dto.Id && x.RevisionId == dto.RevisionId);
            if (r is null) return NotFound();
            r.Origen            = dto.Origen?.Trim() ?? r.Origen;
            r.CodigoRiesgo      = dto.CodigoRiesgo?.Trim() ?? r.CodigoRiesgo;
            r.Titulo            = dto.Titulo?.Trim() ?? r.Titulo;
            r.Descripcion       = dto.Descripcion?.Trim() ?? r.Descripcion;
            r.ImpactoProableUsd = dto.ImpactoProableUsd;
            r.ImpactoMinUsd     = dto.ImpactoMinUsd;
            r.ImpactoMaxUsd     = dto.ImpactoMaxUsd;
            r.Probabilidad      = dto.Probabilidad;
            r.EsOportunidad     = dto.EsOportunidad;
            r.NotasCambio       = dto.NotasCambio?.Trim() ?? "";
            r.ItemViaRiesgoId   = dto.ItemViaRiesgoId;
            r.UpdatedAt         = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return new JsonResult(new { ok = true, id = r.Id }, _jsonOpts);
        }
    }

    public async Task<IActionResult> OnPostRiesgosBulkAsync([FromBody] GuardarRiesgosBulkDto dto)
    {
        var rev = await _db.TallerRevisionesRiesgos
            .Include(r => r.Proyecto)
            .FirstOrDefaultAsync(r => r.Id == dto.RevisionId);
        if (rev is null || rev.Proyecto.UserId != UserId) return Forbid();

        var existentes = await _db.TallerRiesgos.Where(r => r.RevisionId == dto.RevisionId).ToListAsync();
        _db.TallerRiesgos.RemoveRange(existentes);

        var nuevos = dto.Riesgos.Select((r, idx) => new TallerRiesgo
        {
            RevisionId        = dto.RevisionId,
            Orden             = idx + 1,
            Origen            = r.Origen?.Trim() ?? "Proyecto",
            CodigoRiesgo      = r.CodigoRiesgo?.Trim() ?? "",
            Titulo            = r.Titulo?.Trim() ?? "",
            Descripcion       = r.Descripcion?.Trim() ?? "",
            ImpactoProableUsd = r.ImpactoProableUsd,
            ImpactoMinUsd     = r.ImpactoMinUsd,
            ImpactoMaxUsd     = r.ImpactoMaxUsd,
            Probabilidad      = r.Probabilidad,
            EsOportunidad     = r.EsOportunidad,
            NotasCambio       = r.NotasCambio?.Trim() ?? "",
            ItemViaRiesgoId   = r.ItemViaRiesgoId
        }).ToList();

        _db.TallerRiesgos.AddRange(nuevos);
        await _db.SaveChangesAsync();
        return new JsonResult(new { ok = true, count = nuevos.Count }, _jsonOpts);
    }

    public async Task<IActionResult> OnDeleteRiesgoAsync(Guid id)
    {
        var r = await _db.TallerRiesgos
            .Include(x => x.Revision).ThenInclude(x => x.Proyecto)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (r is null || r.Revision.Proyecto.UserId != UserId) return Forbid();
        _db.TallerRiesgos.Remove(r);
        await _db.SaveChangesAsync();
        return new JsonResult(new { ok = true }, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // MONTE CARLO — EJECUCIÓN
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostEjecutarMontecarloAsync([FromBody] EjecutarMcDto dto)
    {
        var proyecto = await _db.TallerProyectos
            .FirstOrDefaultAsync(p => p.Id == dto.ProyectoId && p.UserId == UserId);
        if (proyecto is null) return Forbid();

        // Cargar todos los contratos con ítems
        var contratos = await _db.TallerContratos
            .Include(c => c.Items)
            .Where(c => c.ProyectoId == dto.ProyectoId)
            .ToListAsync();

        // Ítems de incertidumbre al CRA: mismo criterio que Bloque F / resumen (Por comprometer, sin vía riesgo)
        var itemsMc = contratos
            .SelectMany(c => c.Items)
            .Where(i => !i.EsCerteza && !i.ViaRiesgo && i.EsPorComprometer)
            .ToList();

        // Certeza «Por comprometer» (fija en el CRA)
        var certeza = contratos.SelectMany(c => c.Items).Where(i => i.EsCerteza && i.EsPorComprometer).ToList();

        // Comprometido total (de Bloque A de todos los contratos)
        decimal comprometidoTotal = contratos.Sum(c => c.CompometidoUsd);

        // Certeza total (solo ítems que cuentan en EAT del resumen)
        decimal certezaTotal = certeza.Sum(i => i.CostoUsd);

        // EAT determinístico = comprometido + todos los ítems «Por comprometer» al probable (alineado con Resumen global)
        decimal eatDeterministico = comprometidoTotal
            + contratos.SelectMany(c => c.Items).Where(i => i.EsPorComprometer).Sum(i => i.CostoUsd);

        decimal capexTotal = contratos.Sum(c => c.CapexUsd);

        // Cargar riesgos de la revisión activa
        List<TallerRiesgo> riesgos = new();
        if (dto.RevisionId.HasValue)
        {
            riesgos = await _db.TallerRiesgos
                .Where(r => r.RevisionId == dto.RevisionId.Value)
                .ToListAsync();
        }

        // Construir request para CRA
        var craRequest = new
        {
            costs = new
            {
                components = itemsMc.Select(i => new
                {
                    name          = $"{i.CodigoItem} — {i.Descripcion}",
                    baseCost      = (double)i.CostoUsd,
                    estimationClass = i.ClaseEstimacion == "Clase 2" ? 2 : 3,
                    minCost       = (double)(i.MinKusd.HasValue ? i.MinKusd.Value * 1000m : i.CostoUsd * 0.8m),
                    likelyCost    = (double)i.CostoUsd,
                    maxCost       = (double)(i.MaxKusd.HasValue ? i.MaxKusd.Value * 1000m : i.CostoUsd * 1.2m),
                    distribution  = "Triangular",
                    opportunities = Array.Empty<object>(),
                    threats       = Array.Empty<object>()
                }).ToList(),
                simulations = dto.Iteraciones
            },
            risks = new
            {
                risks = riesgos.Select(r => new
                {
                    cause       = r.Origen,
                    riskEvent   = r.Titulo,
                    consequence = r.Descripcion,
                    probability = 1.0,
                    minImpact   = (double)(r.ImpactoMinUsd ?? r.ImpactoProableUsd * 0.8m) * (r.EsOportunidad ? -1 : 1),
                    likelyImpact= (double)r.ImpactoProableUsd * (r.EsOportunidad ? -1 : 1),
                    maxImpact   = (double)(r.ImpactoMaxUsd ?? r.ImpactoProableUsd * 1.2m) * (r.EsOportunidad ? -1 : 1),
                    distribution= "Triangular"
                }).ToList(),
                simulations = dto.Iteraciones
            },
            simulations = dto.Iteraciones
        };

        string apiResult;
        try
        {
            apiResult = await _api.PostRawAsync("api/montecarlo/cra", craRequest);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error llamando CRA para proyecto {Id}", dto.ProyectoId);
            return new JsonResult(new { error = ex.Message }, _jsonOpts);
        }

        // Parsear resultado para calcular EAT con percentiles
        JsonDocument doc;
        try { doc = JsonDocument.Parse(apiResult); }
        catch { return Content(apiResult, "application/json"); }

        var root = doc.RootElement;

        // El resultado CRA tiene: combinedResult.mean, .p50, .p80, .p90, etc.
        var combined = root.TryGetProperty("combinedResult", out var cr) ? cr :
                       root.TryGetProperty("combined", out var cb) ? cb : root;

        double p50 = GetDouble(combined, "p50");
        double p80 = GetDouble(combined, "p80");
        double p90 = GetDouble(combined, "p90");
        double mean = GetDouble(combined, "mean");
        double stdDev = GetDouble(combined, "stdDev");
        double cv = GetDouble(combined, "coefficientOfVariation");

        // Base determinística (sin incertidumbre ni riesgos) = comprometido + certeza + probable de incertidumbre
        double eatDet = (double)eatDeterministico;
        double capex = (double)capexTotal;

        var resultado = new
        {
            capexApi           = capex,
            eatDeterministico  = eatDet,
            eatP50             = eatDet + p50,
            contingenciaP50    = p50,
            crecimientoP50Pct  = capex > 0 ? ((eatDet + p50 - capex) / capex * 100) : 0,
            eatP80             = eatDet + p80,
            contingenciaP80    = p80,
            crecimientoP80Pct  = capex > 0 ? ((eatDet + p80 - capex) / capex * 100) : 0,
            eatP90             = eatDet + p90,
            contingenciaP90    = p90,
            crecimientoP90Pct  = capex > 0 ? ((eatDet + p90 - capex) / capex * 100) : 0,
            mediaIncertidumbre = mean,
            desviacion         = stdDev,
            cv,
            iteraciones        = dto.Iteraciones,
            detalle            = root,
            comprometidoTotal  = (double)comprometidoTotal,
            certezaTotal       = (double)certezaTotal,
            totalItemsMc       = itemsMc.Count,
            totalRiesgos       = riesgos.Count
        };

        // Persistir resultado
        var analisis = new TallerAnalisis
        {
            ProyectoId       = dto.ProyectoId,
            RevisionRiesgosId= dto.RevisionId,
            NombreProyecto   = proyecto.Nombre,
            CodigoProyecto   = proyecto.Codigo,
            FechaEjercicio   = proyecto.FechaEjercicio,
            Iteraciones      = dto.Iteraciones,
            ResultadosJson   = JsonSerializer.Serialize(resultado, _jsonOpts),
            FechaEjecucion   = DateTime.UtcNow,
            UsuarioId        = UserId
        };
        _db.TallerAnalisis.Add(analisis);
        await _db.SaveChangesAsync();

        return new JsonResult(resultado, _jsonOpts);
    }

    public async Task<IActionResult> OnGetResultadosJsonAsync(Guid proyectoId)
    {
        if (!await OwnProject(proyectoId)) return Forbid();
        var lista = await _db.TallerAnalisis
            .Where(a => a.ProyectoId == proyectoId)
            .OrderByDescending(a => a.FechaEjecucion)
            .Take(20)
            .Select(a => new
            {
                a.Id, a.NombreProyecto, a.CodigoProyecto,
                fechaEjercicio = a.FechaEjercicio.ToString("dd-MM-yy"),
                a.Iteraciones, a.ResultadosJson,
                fechaEjecucion = a.FechaEjecucion.ToString("dd/MM/yyyy HH:mm")
            })
            .ToListAsync();
        return new JsonResult(lista, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // MONTE CARLO — RESULTADO BLOQUE F (guardar / cargar por contrato)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnGetMcResultadoJsonAsync(Guid contratoId)
    {
        var contrato = await _db.TallerContratos
            .Include(c => c.Proyecto)
            .FirstOrDefaultAsync(c => c.Id == contratoId);
        if (contrato is null || contrato.Proyecto.UserId != UserId) return Forbid();

        var r = await _db.TallerMcResultados
            .Where(x => x.ContratoId == contratoId)
            .OrderByDescending(x => x.FechaCalculo)
            .FirstOrDefaultAsync();

        if (r is null) return new JsonResult(null, _jsonOpts);

        return new JsonResult(new
        {
            r.P10, r.P50, r.P80, r.P90,
            r.Media, r.DesvStd, r.Iteraciones,
            r.ItemMediasJson, r.HistogramaJson, r.CdfJson,
            fechaCalculo = r.FechaCalculo.ToString("dd/MM/yyyy HH:mm")
        }, _jsonOpts);
    }

    public async Task<IActionResult> OnPostGuardarMcResultadoAsync([FromBody] GuardarMcResultadoDto dto)
    {
        var contrato = await _db.TallerContratos
            .Include(c => c.Proyecto)
            .FirstOrDefaultAsync(c => c.Id == dto.ContratoId);
        if (contrato is null || contrato.Proyecto.UserId != UserId) return Forbid();

        // Upsert: eliminar anterior y guardar el nuevo
        var anteriores = await _db.TallerMcResultados
            .Where(x => x.ContratoId == dto.ContratoId)
            .ToListAsync();
        _db.TallerMcResultados.RemoveRange(anteriores);

        var nuevo = new TallerMcResultado
        {
            ContratoId     = dto.ContratoId,
            FechaCalculo   = DateTime.UtcNow,
            P10            = dto.P10,
            P50            = dto.P50,
            P80            = dto.P80,
            P90            = dto.P90,
            Media          = dto.Media,
            DesvStd        = dto.DesvStd,
            Iteraciones    = dto.Iteraciones,
            ItemMediasJson = dto.ItemMediasJson ?? "[]",
            HistogramaJson = dto.HistogramaJson ?? "{}",
            CdfJson        = dto.CdfJson        ?? "[]"
        };
        _db.TallerMcResultados.Add(nuevo);
        await _db.SaveChangesAsync();

        return new JsonResult(new { ok = true, fechaCalculo = nuevo.FechaCalculo.ToString("dd/MM/yyyy HH:mm") }, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // RESUMEN GLOBAL POR PROYECTO
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnGetResumenGlobalJsonAsync(Guid proyectoId)
    {
        if (!await OwnProject(proyectoId)) return Forbid();

        var proyecto = await _db.TallerProyectos
            .FirstOrDefaultAsync(p => p.Id == proyectoId);
        if (proyecto is null) return NotFound();

        var contratos = await _db.TallerContratos
            .Include(c => c.Items)
            .Include(c => c.Familia)
            .Where(c => c.ProyectoId == proyectoId)
            .OrderBy(c => c.Orden)
            .ToListAsync();

        var familias = await _db.TallerFamilias
            .Where(f => f.ProyectoId == proyectoId)
            .OrderBy(f => f.Orden)
            .Select(f => new { id = f.Id, nombre = f.Nombre })
            .ToListAsync();

        // Sin contratos: respuesta vacía válida
        if (!contratos.Any())
        {
            return new JsonResult(new
            {
                proyecto = new { id = proyecto.Id, nombre = proyecto.Nombre, codigo = proyecto.Codigo, organizacion = proyecto.Organizacion, fechaEjercicio = proyecto.FechaEjercicio.ToString("MM/yyyy") },
                contratos = Array.Empty<object>(),
                totales   = new { capex=0d,comprometido=0d,porComprometer=0d,eat=0d,slack=0d,pctEAT=0d,pctComprometido=0d,certeza=0d,incertidumbre=0d,pctCerteza=0d,totalItems=0,contratosMcOk=0,contratosSinMc=0,mcP10=(double?)null,mcP50=(double?)null,mcP80=(double?)null,mcP90=(double?)null,eatP10=(double?)null,eatP50=(double?)null,eatP80=(double?)null,eatP90=(double?)null }
            }, _jsonOpts);
        }

        var contratoIds = contratos.Select(c => c.Id).ToList();

        // Solo cargar campos numéricos — los JSON (histograma/CDF) pueden ser MBs
        var mcPorContrato = await _db.TallerMcResultados
            .Where(r => contratoIds.Contains(r.ContratoId))
            .Select(r => new {
                r.ContratoId, r.FechaCalculo,
                r.P10, r.P50, r.P80, r.P90,
                r.Media, r.DesvStd, r.Iteraciones
            })
            .ToListAsync();

        var mcLatest = mcPorContrato
            .GroupBy(r => r.ContratoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.FechaCalculo).First());

        // Construir datos por contrato
        var contratoData = new List<object>();
        foreach (var c in contratos)
        {
            var items       = c.Items.ToList();
            var certItems   = items.Where(i => i.EsCerteza).ToList();
            // Incertidumbre que entra al MC / rangos: solo ítems marcados «Por comprometer» (B), alineado con Bloque A/B.
            var incertItems = items.Where(i => !i.EsCerteza && i.EsPorComprometer).ToList();
            var porCompSum  = items.Where(i => i.EsPorComprometer).Sum(i => (double)i.CostoUsd);
            var eat         = (double)c.CompometidoUsd + porCompSum;
            var capex       = (double)c.CapexUsd;
            mcLatest.TryGetValue(c.Id, out var mc);

            var clases = items
                .GroupBy(i => string.IsNullOrWhiteSpace(i.ClaseEstimacion) ? "Sin clase" : i.ClaseEstimacion)
                .Select(g => new { clase = g.Key, count = g.Count(), costoUsd = g.Sum(i2 => (double)i2.CostoUsd) })
                .OrderBy(x => x.clase)
                .ToList();

            var topRisk = incertItems
                .Where(i => i.MaxKusd.HasValue && i.MinKusd.HasValue)
                .Select(i => new
                {
                    codigoItem  = i.CodigoItem,
                    descripcion = i.Descripcion,
                    spread      = (double)(i.MaxKusd!.Value - i.MinKusd!.Value),
                    costoUsd    = (double)i.CostoUsd,
                    minUsd      = (double)i.MinKusd!.Value,
                    maxUsd      = (double)i.MaxKusd!.Value,
                    probUsd     = i.ProbableKusd.HasValue ? (double)i.ProbableKusd.Value : (double)i.CostoUsd
                })
                .OrderByDescending(x => x.spread)
                .Take(5)
                .ToList();

            contratoData.Add(new
            {
                id              = c.Id,
                codigo          = c.Codigo,
                nombre          = c.NombrePaquete,
                capex,
                comprometido    = (double)c.CompometidoUsd,
                porComprometer  = porCompSum,
                eat,
                slack           = capex - eat,
                pctEAT          = capex > 0 ? eat / capex * 100 : 0d,
                pctComprometido = capex > 0 ? (double)c.CompometidoUsd / capex * 100 : 0d,
                totalItems      = items.Count,
                itemsCerteza    = certItems.Count,
                itemsIncert     = incertItems.Count,
                certTotal       = certItems.Where(i => i.EsPorComprometer).Sum(i => (double)i.CostoUsd),
                // Suma probable solo de ítems que entran al MC del contrato (excl. vía riesgo), para Δ vs mcP50
                incertTotal     = incertItems.Where(i => !i.ViaRiesgo).Sum(i => (double)i.CostoUsd),
                clases,
                topRisk,
                hasMc           = mc != null,
                mcP10           = mc?.P10,
                mcP50           = mc?.P50,
                mcP80           = mc?.P80,
                mcP90           = mc?.P90,
                mcMedia         = mc?.Media,
                mcDesvStd       = mc?.DesvStd,
                mcCV            = (mc != null && mc.Media > 0) ? (double?)(mc.DesvStd / mc.Media * 100) : (double?)null,
                mcFecha         = mc?.FechaCalculo.ToString("dd/MM/yyyy HH:mm"),
                familiaId       = c.FamiliaId,
                familiaNombre   = c.Familia?.Nombre
            });
        }

        var totCapex     = contratos.Sum(c => (double)c.CapexUsd);
        var totComp      = contratos.Sum(c => (double)c.CompometidoUsd);
        var totPorCompItems = contratos.Sum(c => c.Items.Where(i => i.EsPorComprometer).Sum(i => (double)i.CostoUsd));
        var totEAT       = totComp + totPorCompItems;
        // Certeza / incertidumbre solo entre ítems «Por comprometer» (coherente con Bloque B y totPorCompItems)
        var totCerteza   = contratos.Sum(c => c.Items.Where(i => i.EsCerteza && i.EsPorComprometer).Sum(i => (double)i.CostoUsd));
        var totIncert    = contratos.Sum(c => c.Items.Where(i => !i.EsCerteza && i.EsPorComprometer).Sum(i => (double)i.CostoUsd));
        var totViaRiesgoPorComp = contratos.Sum(c => c.Items.Where(i => !i.EsCerteza && i.EsPorComprometer && i.ViaRiesgo).Sum(i => (double)i.CostoUsd));
        var baseFijoCombinado = totComp + totCerteza;

        var withMc  = mcLatest.Count;
        var withoutMc = contratos.Count - withMc;

        double? mcP10Agg = withMc > 0 ? (double?)mcLatest.Values.Sum(r => r.P10) : null;
        double? mcP50Agg = withMc > 0 ? (double?)mcLatest.Values.Sum(r => r.P50) : null;
        double? mcP80Agg = withMc > 0 ? (double?)mcLatest.Values.Sum(r => r.P80) : null;
        double? mcP90Agg = withMc > 0 ? (double?)mcLatest.Values.Sum(r => r.P90) : null;

        // ── Riesgos: fuente primaria = Montecarlo "Riesgos" tab; fallback = TallerRevisionesRiesgos ──

        // 1. Intentar cargar riesgos desde MontecarloProject asociado al proyecto
        var mcProjectKey = $"tc:{proyectoId}";
        var mcProj = await _db.MontecarloProjects
            .FirstOrDefaultAsync(p => p.UserId == UserId && p.ProjectName == mcProjectKey);

        var mcRiskItems = new List<RiskItem>();
        DateTime? mcUpdatedAt = null;
        RiskMcResponse? savedRisksSimulation = null;
        if (mcProj != null)
        {
            mcUpdatedAt = mcProj.UpdatedAt;
            try
            {
                var tabs = JsonSerializer.Deserialize<Dictionary<string, ProjectTabData>>(
                               mcProj.TabsJson ?? "{}", _caseInsensitiveOpts)
                           ?? new Dictionary<string, ProjectTabData>();
                if (tabs.TryGetValue("risks", out var risksTab))
                {
                    if (!string.IsNullOrEmpty(risksTab.InputJson) && risksTab.InputJson != "{}")
                        mcRiskItems = ParseMcRisksFromInputJson(risksTab.InputJson);
                    if (!string.IsNullOrEmpty(risksTab.ResultJson) && risksTab.ResultJson != "{}")
                    {
                        try { savedRisksSimulation = JsonSerializer.Deserialize<RiskMcResponse>(risksTab.ResultJson, _caseInsensitiveOpts); }
                        catch { /* ignore malformed */ }
                    }
                }
            }
            catch { /* ignore */ }
        }
        bool useMcRisks = mcRiskItems.Count > 0;

        // Revision Taller mas reciente: cargar siempre si existe (fusion MC + filas solo en BD).
        var revisionInfo = await _db.TallerRevisionesRiesgos
            .Where(r => r.ProyectoId == proyectoId)
            .OrderByDescending(r => r.NumeroRevision)
            .Select(r => new {
                r.Id, r.NumeroRevision, r.Descripcion, r.FechaCreacion,
                totalRiesgos = r.Riesgos.Count()
            })
            .FirstOrDefaultAsync();

        CombinedEATResult? combinadoResult = null;
        CombinedEATResult? combinadoSoloItems = null;
        int nRiesgosLoaded = 0;
        var risks = new List<CombinedEATRisk>();
        var riskDisplayItems = new List<(string Codigo, string Titulo, string Notas, double ProbPct, double Min, double Moda, double Max)>();

        try
        {
            // Ítems no-certeza y «Por comprometer» (misma regla que Bloque B y columna Por COMP. del resumen)
            var incertItemsCombinado = contratos
                .SelectMany(c => c.Items)
                .Where(i => !i.EsCerteza && i.EsPorComprometer)
                .Select(i => new CombinedEATItem
                {
                    Min      = i.MinKusd.HasValue      ? (double)i.MinKusd.Value      : (double)i.CostoUsd * 0.8,
                    Probable = i.ProbableKusd.HasValue  ? (double)i.ProbableKusd.Value : (double)i.CostoUsd,
                    Max      = i.MaxKusd.HasValue       ? (double)i.MaxKusd.Value      : (double)i.CostoUsd * 1.2
                })
                .ToList();

            if (useMcRisks)
            {
                // Fuente: riesgos de Montecarlo (pestaña "Riesgos")
                foreach (var r in mcRiskItems)
                {
                    McRiskFormTriangular.ToSignedVertices(r, out var triMin, out var triMode, out var triMax);
                    risks.Add(new CombinedEATRisk
                    {
                        Probability = r.Probability / 100.0,
                        Min  = triMin,
                        Mode = triMode,
                        Max  = triMax
                    });
                    riskDisplayItems.Add((r.Code, r.Description, r.ResponsePlan,
                        r.Probability, triMin, triMode, triMax));
                }
            }
            else if (revisionInfo != null)
            {
                await AppendTallerRiesgosRevisionAsync(revisionInfo.Id, risks, riskDisplayItems, codigosMcExistentes: null);
            }

            // Riesgos en MC + oportunidades (o filas) solo en revisión Taller: fusionar sin duplicar código.
            if (useMcRisks && revisionInfo != null)
            {
                var mcCodes = new HashSet<string>(
                    mcRiskItems.Select(r => (r.Code ?? "").Trim()).Where(s => s.Length > 0),
                    StringComparer.OrdinalIgnoreCase);
                await AppendTallerRiesgosRevisionAsync(revisionInfo.Id, risks, riskDisplayItems, mcCodes);
            }

            nRiesgosLoaded = risks.Count;

            if (incertItemsCombinado.Any() || risks.Any())
            {
                var mcSeed = Math.Abs(proyectoId.GetHashCode());
                var coupled = await Task.Run(() =>
                    _mc.RunCoupledSoloIncertidumbreYConRiesgos(baseFijoCombinado, incertItemsCombinado, risks, 10_000, mcSeed));
                combinadoSoloItems = coupled.SoloIncertidumbre;
                combinadoResult    = coupled.ConRiesgos;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en simulación combinada para proyecto {Id}", proyectoId);
        }

        // ── Tornado Chart (determinístico) ─────────────────────────────────────
        TornadoResult? tornadoResult = null;
        try
        {
            var incertItemsRaw = contratos.SelectMany(c => c.Items).Where(i => !i.EsCerteza && i.EsPorComprometer).ToList();
            var itemNames  = incertItemsRaw.Select(i => $"{i.CodigoItem} {i.Descripcion}".Trim()).ToList();
            var riskNames  = riskDisplayItems.Select(r => $"{r.Codigo} {r.Titulo}".Trim()).ToList();
            if (incertItemsRaw.Any() || risks.Any())
            {
                var incertForTornado = incertItemsRaw.Select(i => new CombinedEATItem
                {
                    Min      = i.MinKusd.HasValue      ? (double)i.MinKusd.Value      : (double)i.CostoUsd * 0.8,
                    Probable = i.ProbableKusd.HasValue  ? (double)i.ProbableKusd.Value : (double)i.CostoUsd,
                    Max      = i.MaxKusd.HasValue       ? (double)i.MaxKusd.Value      : (double)i.CostoUsd * 1.2
                }).ToList();
                tornadoResult = new TornadoService().RunTornadoAnalysis(
                    baseFijoCombinado, incertForTornado, risks, itemNames, riskNames, topN: 10);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en análisis Tornado para proyecto {Id}", proyectoId);
        }

        // ── What-If Escenarios (determinístico) ───────────────────────────────
        EscenariosResult? escenariosResult = null;
        try
        {
            if (risks.Any() || contratos.SelectMany(c => c.Items).Any(i => !i.EsCerteza && i.EsPorComprometer))
            {
                escenariosResult = new EscenarioService().RunEscenariosAnalysis(
                    baseFijoCombinado, totCapex,
                    contratos.SelectMany(c => c.Items).Where(i => !i.EsCerteza && i.EsPorComprometer)
                        .Select(i => new CombinedEATItem
                        {
                            Min      = i.MinKusd.HasValue      ? (double)i.MinKusd.Value      : (double)i.CostoUsd * 0.8,
                            Probable = i.ProbableKusd.HasValue  ? (double)i.ProbableKusd.Value : (double)i.CostoUsd,
                            Max      = i.MaxKusd.HasValue       ? (double)i.MaxKusd.Value      : (double)i.CostoUsd * 1.2
                        }).ToList(),
                    risks);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en análisis de escenarios para proyecto {Id}", proyectoId);
        }

        // ── Display data para Sección 3 — Análisis de Riesgos ──────────────────
        var amenazasDisp   = riskDisplayItems.Where(r => r.Moda >= 0).ToList();
        var oportunDisp    = riskDisplayItems.Where(r => r.Moda < 0).ToList();
        var topRiskDisplay = riskDisplayItems
            .Select(r => new {
                codigo         = r.Codigo,
                descripcion    = r.Titulo,
                responsePlan   = r.Notas,
                probabilidad   = r.ProbPct,
                min            = r.Min,
                moda           = r.Moda,
                max            = r.Max,
                ve             = r.ProbPct / 100.0 * r.Moda,
                nivel          = GetCodelcoNivel(r.ProbPct),
                nivelLabel     = GetCodelcoLabel(GetCodelcoNivel(r.ProbPct)),
                nivelColor     = GetCodelcoColor(GetCodelcoNivel(r.ProbPct)),
                esAmenaza      = r.Moda >= 0,
                tieneRespuesta = !string.IsNullOrWhiteSpace(r.Notas)
            })
            .OrderByDescending(r => Math.Abs(r.ve))
            .ToList();
        var distribucionNiveles = riskDisplayItems
            .GroupBy(r => GetCodelcoNivel(r.ProbPct))
            .Select(g => new {
                nivel = g.Key,
                label = GetCodelcoLabel(g.Key),
                color = GetCodelcoColor(g.Key),
                count = g.Count()
            })
            .OrderByDescending(x => x.nivel)
            .ToList();

        // Serializar histograma para el cliente (bins de frecuencia relativa)
        object? histogramaJson = combinadoResult?.Histograma
            .Select(b => new { desde = b.RangeMin, hasta = b.RangeMax, frecuencia = b.Frequency })
            .ToList();
        object? histogramaRiesgoNetoJson = (combinadoResult != null && nRiesgosLoaded > 0 && combinadoResult.HistogramaImpactoRiesgosNeto.Count > 0)
            ? combinadoResult.HistogramaImpactoRiesgosNeto
                .Select(b => new { desde = b.RangeMin, hasta = b.RangeMax, frecuencia = b.Frequency })
                .ToList()
            : null;

        return new JsonResult(new
        {
            proyecto = new
            {
                id             = proyecto.Id,
                nombre         = proyecto.Nombre,
                codigo         = proyecto.Codigo,
                organizacion   = proyecto.Organizacion,
                fechaEjercicio = proyecto.FechaEjercicio.ToString("MM/yyyy")
            },
            contratos = contratoData,
            familias  = familias,
            totales   = new
            {
                capex           = totCapex,
                comprometido    = totComp,
                porComprometer  = totPorCompItems,
                eat             = totEAT,
                slack           = totCapex - totEAT,
                pctEAT          = totCapex > 0 ? totEAT / totCapex * 100 : 0d,
                pctComprometido = totCapex > 0 ? totComp / totCapex * 100 : 0d,
                certeza         = totCerteza,
                incertidumbre   = totIncert,
                pctCerteza      = (totCerteza + totIncert) > 0 ? totCerteza / (totCerteza + totIncert) * 100 : 0d,
                totalItems      = contratos.Sum(c => c.Items.Count),
                contratosMcOk   = withMc,
                contratosSinMc  = withoutMc,
                mcP10           = mcP10Agg,
                mcP50           = mcP50Agg,
                mcP80           = mcP80Agg,
                mcP90           = mcP90Agg,
                // MC agregado = solo tramos simulados en Bloque F; vía riesgo queda al probable fijo
                eatP10          = mcP10Agg.HasValue ? totComp + totCerteza + totViaRiesgoPorComp + mcP10Agg.Value : (double?)null,
                eatP50          = mcP50Agg.HasValue ? totComp + totCerteza + totViaRiesgoPorComp + mcP50Agg.Value : (double?)null,
                eatP80          = mcP80Agg.HasValue ? totComp + totCerteza + totViaRiesgoPorComp + mcP80Agg.Value : (double?)null,
                eatP90          = mcP90Agg.HasValue ? totComp + totCerteza + totViaRiesgoPorComp + mcP90Agg.Value : (double?)null
            },
            riesgosDisplay = new {
                nRiesgos               = riskDisplayItems.Count,
                nAmenazas              = amenazasDisp.Count,
                nOportunidades         = oportunDisp.Count,
                valorEsperadoAmenazas  = amenazasDisp.Sum(r => r.ProbPct / 100.0 * r.Moda),
                valorEsperadoOportunidades = oportunDisp.Sum(r => r.ProbPct / 100.0 * r.Moda),
                valorEsperadoNeto      = riskDisplayItems.Sum(r => r.ProbPct / 100.0 * r.Moda),
                distribucionNiveles,
                topRiesgos             = topRiskDisplay.Take(12).ToList(),
                // Fuente de datos: "montecarlo" | "taller" | "sin_datos"
                fuente        = useMcRisks ? "montecarlo" : (revisionInfo != null ? "taller" : "sin_datos"),
                mcFuente      = useMcRisks ? (object)new {
                    actualizado  = mcUpdatedAt?.ToString("dd/MM/yyyy HH:mm"),
                    nRiesgos     = mcRiskItems.Count
                } : null,
                // Revisión Taller (solo como fuente principal si no hay MC; si hay MC igual puede haberse fusionado)
                hayRevision   = !useMcRisks && revisionInfo != null,
                revision      = (useMcRisks || revisionInfo == null) ? null : (object)new {
                    numero       = revisionInfo.NumeroRevision,
                    descripcion  = revisionInfo.Descripcion,
                    fecha        = revisionInfo.FechaCreacion.ToString("dd/MM/yyyy"),
                    totalRiesgos = revisionInfo.totalRiesgos
                }
            },
            eatCombinado = combinadoResult == null ? null : new
            {
                // Valores en la misma escala que eatP50/P80 (sin comprometido), para comparación
                p10            = combinadoResult.P10,
                p50            = combinadoResult.P50,
                p80            = combinadoResult.P80,
                p90            = combinadoResult.P90,
                media          = combinadoResult.Media,
                min            = combinadoResult.Min,
                max            = combinadoResult.Max,
                spreadP10P90   = combinadoResult.SpreadP10P90,
                // baseFijoCombinado ya incluye comprometido + ítems en certeza «Por comprometer»
                eatP10         = combinadoResult.P10,
                eatP50         = combinadoResult.P50,
                eatP80         = combinadoResult.P80,
                eatP90         = combinadoResult.P90,
                eatMedia       = combinadoResult.Media,
                eatDeterministico = totEAT,
                // Misma corrida MC: solo triángulos de ítems (comprometido + certeza + variabilidad ítems), sin Bernoulli riesgos
                eatSoloItemsP10 = combinadoSoloItems?.P10,
                eatSoloItemsP15 = combinadoSoloItems?.P15,
                eatSoloItemsP50 = combinadoSoloItems?.P50,
                eatSoloItemsP80 = combinadoSoloItems?.P80,
                eatSoloItemsP90 = combinadoSoloItems?.P90,
                eatSoloItemsMedia = combinadoSoloItems?.Media,
                nSimulaciones               = combinadoResult.NSimulaciones,
                nItems                      = combinadoResult.NItems,
                nRiesgos                    = nRiesgosLoaded,
                tieneRiesgos                = nRiesgosLoaded > 0,
                contingenciaAmenazasP10     = combinadoResult.ContingenciaAmenazasP10,
                contingenciaAmenazasP50     = combinadoResult.ContingenciaAmenazasP50,
                contingenciaAmenazasP80     = combinadoResult.ContingenciaAmenazasP80,
                contingenciaAmenazasP90     = combinadoResult.ContingenciaAmenazasP90,
                contingenciaAmenazasMedia   = combinadoResult.ContingenciaAmenazasMedia,
                impactoOportunidadesP50     = combinadoResult.ImpactoOportunidadesP50,
                impactoOportunidadesP80     = combinadoResult.ImpactoOportunidadesP80,
                cvar90                      = combinadoResult.CVaR90,
                cvar80                      = combinadoResult.CVaR80,
                excessP90                   = combinadoResult.ExcessP90,
                histograma                  = histogramaJson,
                histogramaRiesgoNeto        = histogramaRiesgoNetoJson,
                deltaRiesgosP10             = nRiesgosLoaded > 0 ? combinadoResult.DeltaRiesgosP10 : (double?)null,
                deltaRiesgosP50             = nRiesgosLoaded > 0 ? combinadoResult.DeltaRiesgosP50 : (double?)null,
                deltaRiesgosP80             = nRiesgosLoaded > 0 ? combinadoResult.DeltaRiesgosP80 : (double?)null,
                deltaRiesgosP90             = nRiesgosLoaded > 0 ? combinadoResult.DeltaRiesgosP90 : (double?)null,
                // Estadísticos distribución EAT total (referencia interpretación tipo informes @RISK)
                desvEst                     = combinadoResult.DesviacionEstandar,
                skewness                    = combinadoResult.Skewness,
                kurtosisExceso              = combinadoResult.KurtosisExceso,
                modaEstimada                = combinadoResult.ModaEstimada,
                p1                          = combinadoResult.P1,
                p5                          = combinadoResult.P5,
                p95                         = combinadoResult.P95,
                p99                         = combinadoResult.P99,
                ic90Semirango               = combinadoResult.Ic90Semirango,
                percentilesTabla            = combinadoResult.PercentilesTabla.Select(x => new { p = x.P, v = x.V }).ToList(),
                soloEstadisticasMc          = combinadoSoloItems == null
                    ? null
                    : new
                    {
                        min            = combinadoSoloItems.Min,
                        max            = combinadoSoloItems.Max,
                        media          = combinadoSoloItems.Media,
                        mediana        = combinadoSoloItems.P50,
                        desvEst        = combinadoSoloItems.DesviacionEstandar,
                        skewness       = combinadoSoloItems.Skewness,
                        kurtosisExceso = combinadoSoloItems.KurtosisExceso,
                        modaEstimada   = combinadoSoloItems.ModaEstimada,
                        p5             = combinadoSoloItems.P5,
                        p95            = combinadoSoloItems.P95,
                        ic90Semirango  = combinadoSoloItems.Ic90Semirango,
                        percentilesTabla = combinadoSoloItems.PercentilesTabla.Select(x => new { p = x.P, v = x.V }).ToList()
                    }
            },
            // Resultado guardado de la última depuración en pestaña Riesgos (RunRiskAnalysis)
            // Estos valores son los que vio el usuario al guardar, y son la fuente de verdad
            // para el card "Distribución Probabilística de Riesgos"
            savedRisksSimulation = savedRisksSimulation == null ? null : new
            {
                threatP50  = savedRisksSimulation.AdditionalMetrics.ThreatP50,
                threatP80  = savedRisksSimulation.AdditionalMetrics.ThreatP80,
                threatP90  = savedRisksSimulation.AdditionalMetrics.ThreatP90,
                oppP50     = savedRisksSimulation.AdditionalMetrics.OpportunityP50,
                oppP80     = savedRisksSimulation.AdditionalMetrics.OpportunityP80,
                netP50     = savedRisksSimulation.Statistics.P50,
                netP80     = savedRisksSimulation.Statistics.P80,
                netP90     = savedRisksSimulation.Statistics.P90,
                netMedia   = savedRisksSimulation.Statistics.Mean
            },
            tornado = tornadoResult == null ? null : new
            {
                eatBase  = tornadoResult.EatBase,
                nItems   = tornadoResult.NItems,
                nRiesgos = tornadoResult.NRiesgos,
                barras   = tornadoResult.Barras.Select(b => new
                {
                    nombre  = b.Nombre,
                    esRiesgo= b.EsRiesgo,
                    eatBase = b.EatBase,
                    eatMin  = b.EatMin,
                    eatMax  = b.EatMax,
                    swing   = b.Swing
                }).ToList()
            },
            escenarios = escenariosResult == null ? null : new
            {
                optimista = new
                {
                    nombre         = escenariosResult.Optimista.Nombre,
                    descripcion    = escenariosResult.Optimista.Descripcion,
                    color          = escenariosResult.Optimista.Color,
                    eat            = escenariosResult.Optimista.EAT,
                    eatItemsDelta  = escenariosResult.Optimista.EATItemsDelta,
                    eatRiesgosDelta= escenariosResult.Optimista.EATRiesgosDelta,
                    vsBase         = escenariosResult.Optimista.VsBase,
                    pctCapex       = escenariosResult.Optimista.PctCapex
                },
                @base = new
                {
                    nombre         = escenariosResult.Base.Nombre,
                    descripcion    = escenariosResult.Base.Descripcion,
                    color          = escenariosResult.Base.Color,
                    eat            = escenariosResult.Base.EAT,
                    eatItemsDelta  = escenariosResult.Base.EATItemsDelta,
                    eatRiesgosDelta= escenariosResult.Base.EATRiesgosDelta,
                    vsBase         = escenariosResult.Base.VsBase,
                    pctCapex       = escenariosResult.Base.PctCapex
                },
                pesimista = new
                {
                    nombre         = escenariosResult.Pesimista.Nombre,
                    descripcion    = escenariosResult.Pesimista.Descripcion,
                    color          = escenariosResult.Pesimista.Color,
                    eat            = escenariosResult.Pesimista.EAT,
                    eatItemsDelta  = escenariosResult.Pesimista.EATItemsDelta,
                    eatRiesgosDelta= escenariosResult.Pesimista.EATRiesgosDelta,
                    vsBase         = escenariosResult.Pesimista.VsBase,
                    pctCapex       = escenariosResult.Pesimista.PctCapex
                }
            }
        }, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // MIROFISH — Motor de enjambre multiagente
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Verifica disponibilidad del servidor MiroFish.</summary>
    public async Task<IActionResult> OnGetMiroFishPingAsync()
    {
        var ok = await _miroFish.IsAvailableAsync();
        return new JsonResult(new { disponible = ok }, _jsonOpts);
    }

    /// <summary>Inicia un análisis MiroFish en background y retorna jobId.</summary>
    public async Task<IActionResult> OnPostMiroFishIniciarAsync([FromBody] MiroFishIniciarDto req)
    {
        if (!await OwnProject(req.ProyectoId)) return Forbid();

        var jobId = _miroFish.IniciarAnalisis(req);
        return new JsonResult(new { jobId }, _jsonOpts);
    }

    /// <summary>Retorna el estado actual de un job MiroFish.</summary>
    public IActionResult OnGetMiroFishStatusAsync(string jobId)
    {
        var job = _miroFish.GetJob(jobId);
        if (job is null) return NotFound();

        return new JsonResult(new
        {
            jobId       = job.JobId,
            status      = job.Status.ToString().ToLower(),
            stepLabel   = job.StepLabel,
            stepNum     = job.StepNum,
            totalSteps  = job.TotalSteps,
            progress    = job.Progress,
            error       = job.ErrorMessage,
            completedAt = job.CompletedAt?.ToString("dd/MM/yyyy HH:mm"),
            // Solo cuando completado:
            result = job.Status == MiroFishJobStatus.Completado ? new
            {
                reportMarkdown    = job.ReportMarkdown,
                escenarioBase     = job.EscenarioBase,
                escenarioPesimista= job.EscenarioPesimista,
                escenarioOptimista= job.EscenarioOptimista,
                hayDivergencia    = job.HayDivergencia,
                riesgosAjustados  = job.RiesgosAjustados.Select(a => new
                {
                    a.Codigo, a.Descripcion,
                    probOriginal  = a.ProbOriginal,
                    probAjustada  = a.ProbAjustada,
                    variacion     = a.Variacion,
                    esDivergente  = a.EsDivergente,
                    narrativaIA   = a.NarrativaIA
                }).ToList()
            } : null
        }, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // CORRELACIÓN — Simulación Monte Carlo con Cópula Gaussiana
    // ════════════════════════════════════════════════════════════════════════

    public class CorrelacionDto
    {
        public Guid      ProyectoId  { get; set; }
        public double[][]? Matriz    { get; set; }  // n×n; null = identidad
        public int       Iteraciones { get; set; } = 10_000;
        /// <summary>"riesgos" | "contratos" | "ambos"</summary>
        public string    VarSource   { get; set; } = "riesgos";
    }

    public async Task<IActionResult> OnPostCorrelacionAsync([FromBody] CorrelacionDto dto)
    {
        if (!await OwnProject(dto.ProyectoId)) return Forbid();

        var contratos = await _db.TallerContratos
            .Include(c => c.Items)
            .Where(c => c.ProyectoId == dto.ProyectoId)
            .ToListAsync();

        // Misma lógica que ResumenGlobalJson (ítems «Por comprometer» en B)
        var totCerteza  = (double)contratos.SelectMany(c => c.Items).Where(i => i.EsCerteza && i.EsPorComprometer).Sum(i => i.CostoUsd);
        var totComp     = (double)contratos.Sum(c => c.CompometidoUsd);

        var incertItemsRaw = contratos.SelectMany(c => c.Items).Where(i => !i.EsCerteza && i.EsPorComprometer).ToList();
        var incertItems = incertItemsRaw.Select(i => new CombinedEATItem
        {
            Min      = i.MinKusd.HasValue      ? (double)i.MinKusd.Value      : (double)i.CostoUsd * 0.8,
            Probable = i.ProbableKusd.HasValue  ? (double)i.ProbableKusd.Value : (double)i.CostoUsd,
            Max      = i.MaxKusd.HasValue       ? (double)i.MaxKusd.Value      : (double)i.CostoUsd * 1.2
        }).ToList();

        // Cargar riesgos (misma lógica que ResumenGlobalJson: MC + revisión Taller no duplicada por código)
        var risks = new List<CombinedEATRisk>();
        var proyectoId = dto.ProyectoId;
        var revCorr = await _db.TallerRevisionesRiesgos
            .Where(r => r.ProyectoId == proyectoId)
            .OrderByDescending(r => r.NumeroRevision)
            .FirstOrDefaultAsync();
        var mcProjectKey = $"tc:{proyectoId}";
        var mcProj = await _db.MontecarloProjects.FirstOrDefaultAsync(p => p.UserId == UserId && p.ProjectName == mcProjectKey);
        List<RiskItem>? mcParsed = null;
        if (mcProj != null)
        {
            try
            {
                var tabs = JsonSerializer.Deserialize<Dictionary<string, ProjectTabData>>(mcProj.TabsJson ?? "{}", _caseInsensitiveOpts) ?? new();
                if (tabs.TryGetValue("risks", out var rt) && !string.IsNullOrEmpty(rt.InputJson) && rt.InputJson != "{}")
                {
                    mcParsed = ParseMcRisksFromInputJson(rt.InputJson);
                    foreach (var r in mcParsed)
                    {
                        McRiskFormTriangular.ToSignedVertices(r, out var triMin, out var triMode, out var triMax);
                        risks.Add(new CombinedEATRisk
                        {
                            Probability = r.Probability / 100.0,
                            Min  = triMin,
                            Mode = triMode,
                            Max  = triMax
                        });
                    }
                }
            }
            catch { /* ignore */ }
        }

        if (mcParsed is { Count: > 0 } && revCorr != null)
        {
            var mcCodes = new HashSet<string>(
                mcParsed.Select(r => (r.Code ?? "").Trim()).Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
            var dummyDisplay = new List<(string Codigo, string Titulo, string Notas, double ProbPct, double Min, double Moda, double Max)>();
            await AppendTallerRiesgosRevisionAsync(revCorr.Id, risks, dummyDisplay, mcCodes);
        }
        else if (!risks.Any() && revCorr != null)
        {
            var dummyDisplay = new List<(string Codigo, string Titulo, string Notas, double ProbPct, double Min, double Moda, double Max)>();
            await AppendTallerRiesgosRevisionAsync(revCorr.Id, risks, dummyDisplay, codigosMcExistentes: null);
        }

        // Determinar dimensión de la matriz según fuente de variables
        var varSource = dto.VarSource is "contratos" or "ambos" or "riesgos" ? dto.VarSource : "riesgos";
        int nForMatrix = varSource switch {
            "contratos" => incertItems.Count,
            "ambos"     => incertItems.Count + risks.Count,
            _           => risks.Count
        };

        // Convertir matriz JS (jagged) a double[,]
        double[,]? corrMatrix = null;
        if (dto.Matriz != null && dto.Matriz.Length == nForMatrix && nForMatrix > 0)
        {
            corrMatrix = new double[nForMatrix, nForMatrix];
            for (int i = 0; i < nForMatrix; i++)
                for (int j = 0; j < nForMatrix; j++)
                    corrMatrix[i, j] = (dto.Matriz[i]?.Length > j) ? dto.Matriz[i][j] : (i == j ? 1.0 : 0.0);
        }

        var iter = Math.Clamp(dto.Iteraciones, 1_000, 50_000);
        CorrelacionResult result;
        CombinedEATResult refSim;
        try
        {
            var baseFixed = totComp + totCerteza;
            // Referencia: simulación independiente para calcular deltas
            refSim = _mc.RunCombinedEATAnalysis(baseFixed, incertItems, risks, iter);
            result = await Task.Run(() =>
                new CorrelacionService().RunCorrelacionAnalysis(
                    baseFixed, incertItems, risks, corrMatrix,
                    varSource: varSource,
                    refP50: refSim.P50, refP90: refSim.P90, simulations: iter));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en simulación correlacionada para proyecto {Id}", dto.ProyectoId);
            return new JsonResult(new { error = ex.Message }, _jsonOpts);
        }

        return new JsonResult(new
        {
            p10     = result.P10,
            p50     = result.P50,
            p80     = result.P80,
            p90     = result.P90,
            media   = result.Media,
            cvar90  = result.CVaR90,
            spreadP10P90 = result.SpreadP10P90,
            nSimulaciones= result.NSimulaciones,
            deltaP50  = result.DeltaP50,
            deltaP90  = result.DeltaP90,
            deltaP10  = refSim.P10  > 0 ? (result.P10  - refSim.P10)  / refSim.P10  * 100 : 0,
            deltaP80  = refSim.P80  > 0 ? (result.P80  - refSim.P80)  / refSim.P80  * 100 : 0,
            deltaCvar90 = refSim.CVaR90 > 0 ? (result.CVaR90 - refSim.CVaR90) / refSim.CVaR90 * 100 : 0,
            // Valores de referencia exactos (sin correlación) para tabla comparativa
            refP10   = refSim.P10,
            refP50   = refSim.P50,
            refP80   = refSim.P80,
            refP90   = refSim.P90,
            refCvar90= refSim.CVaR90,
            varSource    = varSource,
            nContratos   = incertItems.Count,
            nRiesgos     = risks.Count,
            histograma   = result.Histograma.Select(b => new { desde = b.RangeMin, hasta = b.RangeMax, frecuencia = b.Frequency }).ToList()
        }, _jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private async Task<bool> OwnProject(Guid proyectoId) =>
        await _db.TallerProyectos.AnyAsync(p => p.Id == proyectoId && p.UserId == UserId);

    private static double GetDouble(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetDouble();
        return 0;
    }

    private static int GetCodelcoNivel(double pct) =>
        pct >= 75 ? 5 : pct >= 65 ? 4 : pct >= 50 ? 3 : pct >= 25 ? 2 : 1;

    /// <summary>Tipo de riesgo MC: oportunidad si coincide con "Oportunidad" (ignora mayúsculas y espacios).</summary>
    private static bool McTipoEsOportunidad(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) &&
        tipo.Trim().Equals("Oportunidad", StringComparison.OrdinalIgnoreCase);

    /// <summary>JSON plano del tab MC: claves insensibles a mayúsculas (p. ej. risks[0].tipo).</summary>
    private static Dictionary<string, string> McFlatInputToDictionary(string inputJson)
    {
        var raw = string.IsNullOrWhiteSpace(inputJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(inputJson) ?? new();
        var n = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in raw)
            n[kv.Key] = kv.Value;
        return n;
    }

    /// <summary>
    /// Elimina filas con el mismo código (revisión Taller sustituye la fila MC cuando comparten <c>CodigoRiesgo</c>).
    /// </summary>
    private static void RemoveRiskRowsByCodigo(
        List<CombinedEATRisk> risks,
        List<(string Codigo, string Titulo, string Notas, double ProbPct, double Min, double Moda, double Max)> rows,
        string codigo)
    {
        if (string.IsNullOrEmpty(codigo)) return;
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (string.Equals((rows[i].Codigo ?? "").Trim(), codigo, StringComparison.OrdinalIgnoreCase))
            {
                rows.RemoveAt(i);
                risks.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Agrega riesgos de una revisión Taller. Si <paramref name="codigosMcExistentes"/> no es null,
    /// las filas con código ya usado en MC sustituyen a la fila MC (no se omiten), para respetar p. ej. <c>EsOportunidad</c> en Taller.
    /// </summary>
    private async Task AppendTallerRiesgosRevisionAsync(
        Guid revisionId,
        List<CombinedEATRisk> risks,
        List<(string Codigo, string Titulo, string Notas, double ProbPct, double Min, double Moda, double Max)> riskDisplayItems,
        HashSet<string>? codigosMcExistentes)
    {
        var riskRows = await _db.TallerRiesgos
            .Where(r => r.RevisionId == revisionId && r.ImpactoProableUsd != 0)
            .OrderBy(r => r.Orden)
            .Select(r => new {
                r.Id, r.CodigoRiesgo, r.Titulo, r.NotasCambio,
                r.ImpactoProableUsd, r.ImpactoMinUsd, r.ImpactoMaxUsd,
                r.EsOportunidad
            })
            .ToListAsync();

        if (riskRows.Count == 0) return;

        var probMap = new Dictionary<Guid, decimal>();
        try
        {
            var probRows = await _db.TallerRiesgos
                .Where(r => r.RevisionId == revisionId)
                .Select(r => new { r.Id, r.Probabilidad })
                .ToListAsync();
            foreach (var p in probRows)
                probMap[p.Id] = p.Probabilidad ?? 100m;
        }
        catch { /* ignore */ }

        foreach (var r in riskRows)
        {
            var code = (r.CodigoRiesgo ?? "").Trim();
            if (codigosMcExistentes != null && code.Length > 0 && codigosMcExistentes.Contains(code))
                RemoveRiskRowsByCodigo(risks, riskDisplayItems, code);

            var prob   = probMap.TryGetValue(r.Id, out var pv) ? (double)pv : 100.0;
            var impAbs = (double)r.ImpactoProableUsd;
            var minAbs = r.ImpactoMinUsd.HasValue ? (double)r.ImpactoMinUsd.Value : impAbs * 0.8;
            var maxAbs = r.ImpactoMaxUsd.HasValue ? (double)r.ImpactoMaxUsd.Value : impAbs * 1.2;
            var triMin  = r.EsOportunidad ? -maxAbs : minAbs;
            var triMode = r.EsOportunidad ? -impAbs : impAbs;
            var triMax  = r.EsOportunidad ? -minAbs : maxAbs;
            risks.Add(new CombinedEATRisk { Probability = prob / 100.0, Min = triMin, Mode = triMode, Max = triMax });
            riskDisplayItems.Add((r.CodigoRiesgo ?? "", r.Titulo, r.NotasCambio ?? "", prob, triMin, triMode, triMax));
        }
    }

    /// <summary>
    /// Parsea el inputJson plano de un tab de Montecarlo (formato {"Risks[0].Field":"value"})
    /// y devuelve la lista de RiskItem con datos reales (impacto probable != 0).
    /// </summary>
    private static List<RiskItem> ParseMcRisksFromInputJson(string inputJson)
    {
        var result = new List<RiskItem>();
        try
        {
            var dict = McFlatInputToDictionary(inputJson);

            var maxIdx = -1;
            foreach (var key in dict.Keys)
            {
                var m = System.Text.RegularExpressions.Regex.Match(key, @"Risks\[(\d+)\]",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var idx))
                    maxIdx = Math.Max(maxIdx, idx);
            }

            for (var i = 0; i <= maxIdx; i++)
            {
                var likely = GetMcDouble(dict, $"Risks[{i}].MostLikelyImpact");
                if (likely == 0) continue; // fila vacía, omitir

                var minV = GetMcDouble(dict, $"Risks[{i}].MinImpact");
                var maxV = GetMcDouble(dict, $"Risks[{i}].MaxImpact");
                var rawTipo = dict.GetValueOrDefault($"Risks[{i}].Tipo") ?? "Amenaza";

                result.Add(new RiskItem
                {
                    Tipo             = McTipoEsOportunidad(rawTipo) ? "Oportunidad" : "Amenaza",
                    Code             = dict.GetValueOrDefault($"Risks[{i}].Code")          ?? "",
                    Description      = dict.GetValueOrDefault($"Risks[{i}].Description")   ?? "",
                    Cause            = dict.GetValueOrDefault($"Risks[{i}].Cause")          ?? "",
                    ResponsePlan     = dict.GetValueOrDefault($"Risks[{i}].ResponsePlan")   ?? "",
                    Probability      = GetMcDouble(dict, $"Risks[{i}].Probability", 50),
                    MinImpact        = minV  == 0 ? likely * 0.8 : minV,
                    MostLikelyImpact = likely,
                    MaxImpact        = maxV  == 0 ? likely * 1.2 : maxV,
                    Distribution     = dict.GetValueOrDefault($"Risks[{i}].Distribution")     ?? "Triangular",
                    EstimationBase   = dict.GetValueOrDefault($"Risks[{i}].EstimationBase")   ?? "",
                    Opportunity      = dict.GetValueOrDefault($"Risks[{i}].Opportunity")      ?? "",
                    Threat           = dict.GetValueOrDefault($"Risks[{i}].Threat")           ?? "",
                });
            }
        }
        catch { /* ignore parse errors */ }
        return result;
    }

    private static double GetMcDouble(Dictionary<string, string> d, string key, double def = 0)
    {
        if (!d.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v)) return def;
        v = v.Trim();
        var ns  = System.Globalization.NumberStyles.Float;
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        // Formato chileno/español: coma = decimal, punto = separador de miles.
        // Ejemplos: "992.466" → 992466 | "992.466,50" → 992466.5 | "992,47" → 992.47
        if (v.Contains(','))
        {
            // Hay coma: punto es miles, coma es decimal
            var normalized = v.Replace(".", "").Replace(",", ".");
            if (double.TryParse(normalized, ns, inv, out var r1)) return r1;
        }
        else if (v.Contains('.'))
        {
            var parts = v.Split('.');
            // Si el último grupo tiene exactamente 3 dígitos → punto es separador de miles
            if (parts[^1].Length == 3 && parts[^1].All(char.IsDigit))
            {
                var stripped = v.Replace(".", "");
                if (double.TryParse(stripped, ns, inv, out var r2)) return r2;
            }
            else
            {
                // Punto decimal normal (1-2 dígitos decimales)
                if (double.TryParse(v, ns, inv, out var r3)) return r3;
            }
        }
        else
        {
            if (double.TryParse(v, ns, inv, out var r4)) return r4;
        }
        return def;
    }

    private static string GetCodelcoLabel(int n) => n switch {
        5 => "Casi Seguro", 4 => "Muy Probable", 3 => "Probable",
        2 => "Poco Probable", _ => "Remoto"
    };

    private static string GetCodelcoColor(int n) => n switch {
        5 => "red", 4 => "orange", 3 => "amber", 2 => "blue", _ => "slate"
    };

    // ════════════════════════════════════════════════════════════════════════
    // DTOs
    // ════════════════════════════════════════════════════════════════════════

    public record GuardarProyectoDto(
        Guid Id, string Nombre, string Codigo,
        string FechaEjercicio, string? Organizacion);

    public record GuardarFamiliaDto(
        Guid Id, Guid ProyectoId, string Nombre, string? Descripcion);

    public record GuardarEmpresaDto(
        Guid Id,
        Guid ProyectoId,
        string Rut,
        string Nombre,
        string? Contacto = null,
        string? Email = null,
        string? Telefono = null,
        string? Notas = null);

    public record AsignarFamiliaDto(
        Guid ContratoId, Guid? FamiliaId);

    public record GuardarContratoDto(
        Guid Id, Guid ProyectoId, string? Codigo, string? NombrePaquete,
        decimal CapexUsd, decimal CompometidoUsd, decimal PorComprometidoUsd, decimal EstimadoTerminoUsd,
        decimal TasaCambio = 900m, decimal Factor = 1m, string? FechaTasaCambio = null,
        Guid? EmpresaId = null);

    public class GuardarItemsDto
    {
        public Guid ContratoId { get; set; }
        public List<ItemDto> Items { get; set; } = new();
    }

    public class ItemDto
    {
        public string? CodigoItem     { get; set; }
        public string? Descripcion    { get; set; }
        public string? Unidad         { get; set; }
        public string? SubPartida     { get; set; }
        public bool    EsCerteza      { get; set; }
        public bool    EsPorComprometer { get; set; } = true;
        public decimal CostoUsd       { get; set; }
        public string? ClaseEstimacion { get; set; }
        public string? Consideraciones { get; set; }
        public decimal? MinPct        { get; set; }
        public decimal? MinKusd       { get; set; }
        public decimal? ProbablePct   { get; set; }
        public decimal? ProbableKusd  { get; set; }
        public decimal? MaxPct        { get; set; }
        public decimal? MaxKusd       { get; set; }
        public bool    ViaRiesgo      { get; set; }
        public string? Oportunidades  { get; set; }
        public string? Amenazas       { get; set; }
        public decimal? Clase         { get; set; }
        public decimal? Peso          { get; set; }
        public decimal? CostoClpOverride { get; set; }
    }

    public record GuardarRevisionDto(
        Guid ProyectoId, string? Descripcion, Guid? CopiarDeRevisionId);

    public class GuardarRiesgoDto
    {
        public Guid Id { get; set; }
        public Guid RevisionId { get; set; }
        public string? Origen { get; set; }
        public string? CodigoRiesgo { get; set; }
        public string? Titulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal ImpactoProableUsd { get; set; }
        public decimal? ImpactoMinUsd { get; set; }
        public decimal? ImpactoMaxUsd { get; set; }
        public decimal? Probabilidad { get; set; }
        public bool EsOportunidad { get; set; }
        public string? NotasCambio { get; set; }
        public Guid? ItemViaRiesgoId { get; set; }
    }

    public class GuardarRiesgosBulkDto
    {
        public Guid RevisionId { get; set; }
        public List<GuardarRiesgoDto> Riesgos { get; set; } = new();
    }

    public record EjecutarMcDto(
        Guid ProyectoId, Guid? RevisionId, int Iteraciones = 10000);

    public class GuardarMcResultadoDto
    {
        public Guid    ContratoId     { get; set; }
        public double  P10            { get; set; }
        public double  P50            { get; set; }
        public double  P80            { get; set; }
        public double  P90            { get; set; }
        public double  Media          { get; set; }
        public double  DesvStd        { get; set; }
        public int     Iteraciones    { get; set; }
        public string? ItemMediasJson { get; set; }
        public string? HistogramaJson { get; set; }
        public string? CdfJson        { get; set; }
    }

    public class SimularClasificacionDto
    {
        public List<ItemMcSimDto> Items       { get; set; } = new();
        public int                Iteraciones { get; set; } = 10_000;
    }

    public class ItemMcSimDto
    {
        public double  Minimo   { get; set; }
        public double  Probable { get; set; }
        public double  Maximo   { get; set; }
        public string? Codigo   { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════════
    // SIMULACIÓN LOCAL — BLOQUE F CLASIFICACIÓN
    // ════════════════════════════════════════════════════════════════════════

    public IActionResult OnPostSimularClasificacion([FromBody] SimularClasificacionDto dto)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            return new JsonResult(new { error = "Sin ítems de incertidumbre para simular." }, _jsonOpts);

        var items = dto.Items.Select(i => new MonteCarloItem
        {
            Minimo   = i.Minimo,
            Probable = i.Probable,
            Maximo   = i.Maximo
        }).ToList();

        var result = _mc.RunSimulation(items, dto.Iteraciones);
        return new JsonResult(result, _jsonOpts);
    }
}
