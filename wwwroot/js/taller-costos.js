/**
 * taller-costos.js
 * Sistema de Análisis de Rango de Costos — CODELCO
 */
'use strict';

// ════════════════════════════════════════════════════════════════════════════
// ESTADO GLOBAL
// ════════════════════════════════════════════════════════════════════════════
const TC = {
    proyectoId:     null,
    contratoId:     null,
    revisionId:     null,
    proyecto:       null,
    contratos:      [],
    items:          [],      // ítems del contrato activo (en memoria)
    revisiones:     [],
    familias:       [],      // familias del proyecto activo
    empresas:       [],      // empresas contratistas del proyecto activo
    riesgos:        [],      // riesgos de la revisión activa (en memoria)
    tasaCambio:     900,
    activeSection:  'proyectos',
    mcColumnValues: null     // valores MC P50 por ítem incertidumbre (KUS$), null = sin calcular
};

function tcContratoById(contratoId) {
    const want = String(contratoId || '').toLowerCase();
    return TC.contratos.find(x => String(x.id).toLowerCase() === want);
}

// Vista de riesgos: 'compact' (ejecutiva, default) | 'edit' (tabla con edición por fila)
let tcRiesgoViewMode = 'compact';
/** Índice de fila en edición dentro de #tc-riesgos-tbody, o null = solo lectura */
let tcRiesgoEditingRowIndex = null;
let tcRiesgoSortAsc  = false; // false = mayor VE primero
/** Índice de fila en edición dentro de Bloque D, o null = solo lectura */
let tcDEditingRowIndex = null;
/** bIdx en edición dentro de Bloque E, o null = solo lectura */
let tcEEditingRowBIdx = null;

// Vista de contratos: 'lista' (tabla plana) | 'familia' (agrupado por familia)
let _tcContratosVista = 'familia';

// Estado colapso de familias en tabla "Estado por Contrato" (Resumen Global)
// key = familiaId | 'sin-fam'  →  true = colapsado
const _tcFamCollapsed = {};

function tcToggleFam(key) {
    _tcFamCollapsed[key] = !_tcFamCollapsed[key];
    const isCol = _tcFamCollapsed[key];
    document.querySelectorAll(`[data-fam="${key}"]`).forEach(el => {
        el.style.display = isCol ? 'none' : '';
    });
    const chev = document.getElementById(`fam-chev-${key}`);
    if (chev) chev.style.transform = isCol ? 'rotate(-90deg)' : 'rotate(0deg)';
}

// ════════════════════════════════════════════════════════════════════════════
// INICIALIZACIÓN
// ════════════════════════════════════════════════════════════════════════════
async function tcInit() {
    tcNav('resumen');
    // Restaurar estado de sidebar
    if (localStorage.getItem('tc-sidebar-collapsed') === '1') {
        document.getElementById('tc-sidebar')?.classList.add('tc-collapsed');
        const icon = document.getElementById('tc-sidebar-icon');
        if (icon) icon.textContent = 'chevron_right';
    }
    await tcCargarProyectos();
    // Restaurar último proyecto seleccionado para que el resumen cargue con datos
    const lastId = localStorage.getItem('tc-last-proy');
    if (lastId) {
        try {
            await tcSeleccionarProyecto(lastId, true);
            tcCargarResumen();
        } catch {
            localStorage.removeItem('tc-last-proy');
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════
// NAVEGACIÓN
// ════════════════════════════════════════════════════════════════════════════
// Detecta si estamos en la página standalone TallerCostos (tiene tc-s-*) o embebida en Montecarlo
function tcIsStandalone() { return !!document.getElementById('tc-s-proyectos'); }

function tcNav(section) {
    if (TC.activeSection === 'bloqueD' && section !== 'bloqueD' && tcDEditingRowIndex !== null) {
        tcSyncDToItems();
        tcDEditingRowIndex = null;
    }
    if (TC.activeSection === 'bloqueE' && section !== 'bloqueE' && tcEEditingRowBIdx !== null) {
        tcSyncEToItems();
        tcEEditingRowBIdx = null;
    }
    TC.activeSection = section;

    if (tcIsStandalone()) {
        // Modo standalone: secciones tc-s-*
        document.querySelectorAll('.tc-section').forEach(s => s.classList.remove('tc-visible'));
        document.querySelectorAll('.step-btn').forEach(b => b.classList.remove('tc-active'));
        const el = document.getElementById('tc-s-' + section);
        if (el) el.classList.add('tc-visible');
        document.querySelectorAll(`[data-section="${section}"]`).forEach(b => b.classList.add('tc-active'));
        // Scroll al inicio del contenido principal para que el encabezado sea visible
        const scrollable = document.querySelector('main.overflow-y-auto') || document.querySelector('main[class*="overflow-y"]');
        if (scrollable) scrollable.scrollTop = 0;
        const bloques = ['bloqueA','bloqueB','bloqueC','bloqueD','bloqueE','bloqueF'];
        if (bloques.includes(section)) {
            document.getElementById('sb-contrato-items')?.classList.remove('hidden');
        }
    } else if (typeof activateTab === 'function') {
        // Modo embebido en Montecarlo: paneles tab-tc-*
        activateTab('tc-' + section);
    }

    // Lógica on-enter (aplica en ambos modos)
    if (section === 'bloqueD') tcRenderBloqueD();
    if (section === 'bloqueE') tcRenderBloqueE();
    if (section === 'bloqueF') { tcRenderBloqueF(); tcCargarMcResultado(); }
    if (section === 'riesgos') { tcRiesgosCargarProyectos(); tcInjectPdfBtn('riesgos'); }
    if (section === 'montecarlo') { tcCargarRevisionesSelectMC(); tcCargarHistorial(); tcActualizarResumenMC(); }
    if (section === 'resumen') { tcPopularSelectResumen(); tcCargarResumen(); tcInjectPdfBtn('resumen'); }
    if (section === 'empresas') { tcCargarPanelEmpresas(); }
    if (section === 'bloqueB') tcValidarBloqueB();
}

// Inyecta barra con botón PDF en la sección indicada.
// Soporta: 'resumen' (tab-tc-resumen), 'risks' (tab-risks), 'riesgos' (tc-s-riesgos standalone)
function tcInjectPdfBtn(section) {
    const barId = 'tc-pdf-bar-' + section;
    var existingBar = document.getElementById(barId);
    if (existingBar) existingBar.remove();

    var fn, anchorId;
    if (section === 'resumen') {
        fn       = 'tcAbrirPdfResumen()';
        anchorId = 'tc-resumen-content';
    } else if (section === 'risks') {
        fn       = 'tcAbrirPdfRiesgos()';
        anchorId = 'risks-form';
    } else {
        fn       = 'tcAbrirPdfRiesgos()';
        anchorId = 'tc-riesgos-panel';
    }

    var html;
    if (section === 'resumen') {
        html =
        '<div id="' + barId + '" style="' +
            'display:flex;align-items:center;justify-content:flex-end;flex-wrap:wrap;' +
            'margin-bottom:14px;padding:8px 14px;gap:8px;' +
            'background:#f0fdf4;border:1px solid #99f6e4;border-radius:8px;' +
        '">' +
            '<span style="flex:1;min-width:140px;font-size:12px;color:#0f766e;font-weight:500">' +
                'Reporte del proyecto seleccionado' +
            '</span>' +
            '<button onclick="tcAbrirPdfResumen()" style="' +
                'display:inline-flex;align-items:center;gap:6px;' +
                'padding:7px 16px;border-radius:8px;font-size:13px;font-weight:600;' +
                'cursor:pointer;border:none;background:#0f9488;color:#ffffff;' +
            '">' +
                '<span class="material-icons" style="font-size:16px">picture_as_pdf</span>' +
                ' Generar Reporte PDF' +
            '</button>' +
            '<button onclick="tcAbrirPdfEjecutivoCod()" title="Informe ejecutivo CODELCO (compacto)" style="' +
                'display:inline-flex;align-items:center;gap:6px;' +
                'padding:7px 14px;border-radius:8px;font-size:12px;font-weight:600;' +
                'cursor:pointer;border:1px solid #e87722;background:linear-gradient(180deg,#fff8f0,#fff);color:#9a3412;' +
            '">' +
                '<span class="material-icons" style="font-size:16px;color:#e87722">description</span>' +
                ' Informe ejecutivo CODELCO' +
            '</button>' +
        '</div>';
    } else {
        html =
        '<div id="' + barId + '" style="' +
            'display:flex;align-items:center;justify-content:flex-end;' +
            'margin-bottom:14px;padding:8px 14px;gap:8px;' +
            'background:#f0fdf4;border:1px solid #99f6e4;border-radius:8px;' +
        '">' +
            '<span style="flex:1;font-size:12px;color:#0f766e;font-weight:500">' +
                'Reporte de riesgos y oportunidades del proyecto' +
            '</span>' +
            '<button onclick="' + fn + '" style="' +
                'display:inline-flex;align-items:center;gap:6px;' +
                'padding:7px 16px;border-radius:8px;font-size:13px;font-weight:600;' +
                'cursor:pointer;border:none;background:#0f9488;color:#ffffff;' +
            '">' +
                '<span class="material-icons" style="font-size:16px">picture_as_pdf</span>' +
                ' Generar Reporte PDF' +
            '</button>' +
        '</div>';
    }

    var anchor = document.getElementById(anchorId);
    if (anchor) {
        anchor.insertAdjacentHTML('beforebegin', html);
        return;
    }

    // Fallback standalone: tc-s-resumen / tc-s-riesgos
    var sectionEl = document.getElementById('tc-s-' + section);
    if (sectionEl) {
        var fc = sectionEl.firstElementChild;
        fc ? fc.insertAdjacentHTML('afterend', html)
           : sectionEl.insertAdjacentHTML('afterbegin', html);
    }
}

// Helper null-safe para setear valores de formulario
function tcSetVal(id, val) { const e = document.getElementById(id); if (e) e.value = val ?? ''; }

function tcToggleSidebar() {
    const sb = document.getElementById('tc-sidebar');
    const icon = document.getElementById('tc-sidebar-icon');
    const collapsed = sb?.classList.toggle('tc-collapsed');
    if (icon) icon.textContent = collapsed ? 'chevron_right' : 'chevron_left';
    localStorage.setItem('tc-sidebar-collapsed', collapsed ? '1' : '0');
}

// ════════════════════════════════════════════════════════════════════════════
// HELPERS HTTP
// ════════════════════════════════════════════════════════════════════════════
function tcToken() {
    const el = document.querySelector('[name="__RequestVerificationToken"]');
    return el ? el.value : '';
}

async function tcGet(handler, params = {}) {
    const qs = new URLSearchParams(params).toString();
    const url = `/TallerCostos?handler=${handler}${qs ? '&' + qs : ''}`;
    const resp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
    if (!resp.ok) throw new Error(tcApiErrorText(await resp.text()));
    return resp.json();
}

function tcApiErrorText(raw) {
    if (!raw) return 'Error desconocido';
    try {
        const j = JSON.parse(raw);
        if (j && typeof j.error === 'string') return j.error;
    } catch (_) { /* cuerpo no JSON */ }
    return raw;
}

async function tcPost(handler, body) {
    const resp = await fetch(`/TallerCostos?handler=${handler}`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': tcToken(),
            'X-Requested-With': 'XMLHttpRequest'
        },
        body: JSON.stringify(body)
    });
    if (!resp.ok) throw new Error(tcApiErrorText(await resp.text()));
    return resp.json();
}

async function tcDelete(handler, params = {}) {
    const qs = new URLSearchParams(params).toString();
    const url = `/TallerCostos?handler=${handler}${qs ? '&' + qs : ''}`;
    const resp = await fetch(url, {
        method: 'DELETE',
        headers: {
            'RequestVerificationToken': tcToken(),
            'X-Requested-With': 'XMLHttpRequest'
        }
    });
    if (!resp.ok) throw new Error(tcApiErrorText(await resp.text()));
    return resp.json();
}

// ════════════════════════════════════════════════════════════════════════════
// FORMATO
// ════════════════════════════════════════════════════════════════════════════
function tcFmt(val, decimals = 0) {
    if (val === null || val === undefined || isNaN(val)) return '—';
    return new Intl.NumberFormat('es-CL', { minimumFractionDigits: decimals, maximumFractionDigits: decimals }).format(val);
}
// Número plano para valores de <input type="number"> (sin formato local)
function tcFmtRaw(val, decimals = 4) {
    if (val === null || val === undefined || isNaN(val)) return '';
    return parseFloat(val).toFixed(decimals);
}
function tcFmtUsd(val) { return '$' + tcFmt(val, 2) + ' USD'; }
function tcFmtPct(val) { return tcFmt(val, 1) + '%'; }
function tcMoneyDecimals(el, fallback = 2) {
    const d = parseInt(el?.dataset?.decimals, 10);
    return Number.isFinite(d) ? d : fallback;
}
function tcSetMoneyInput(id, val, decimals = 0) {
    const el = document.getElementById(id);
    if (!el) return;
    const n = parseFloat(val);
    const safe = isNaN(n) ? 0 : n;
    el.dataset.raw = String(safe);
    el.dataset.decimals = String(decimals);
    // type="number" no acepta separadores de miles (p. ej. 35.854.554) — dejar valor parseable
    if (el.type === 'number') {
        el.value = decimals > 0 ? safe.toFixed(decimals) : String(safe);
        return;
    }
    el.value = tcFmt(safe, decimals);
}

/** Sincroniza Por Comprometer y EAT del Bloque A con la suma de ítems marcados (solo al editar en B). */
function tcSyncBloqueADesdeSumaPorComprometer(sumaPorComprometer) {
    const comp = tcParseInput(document.getElementById('bloqA-comp'));
    const eat = comp + sumaPorComprometer;
    tcSetMoneyInput('bloqA-porcomp', sumaPorComprometer, 0);
    tcSetMoneyInput('bloqA-eat', eat, 0);
    const c = TC.contratoId ? tcContratoById(TC.contratoId) : null;
    if (c) {
        c.porComprometidoUsd = sumaPorComprometer;
        c.estimadoTerminoUsd = eat;
    }
}

// Parsea un string en formato es-CL ("1.234,56") o plano ("1234.56") a float
function tcParseNum(s) {
    if (s == null || s === '') return NaN;
    return parseFloat(String(s).replace(/\./g, '').replace(',', '.'));
}
// Lee el valor numérico de un input formateado (usa data-raw si está disponible)
function tcParseInput(el) {
    const raw = el?.dataset?.raw;
    if (raw !== undefined && raw !== '') return parseFloat(raw) || 0;
    return parseFloat(el?.value) || 0;
}
// Handlers focus/blur/input para inputs numéricos con separadores de miles
function tcNumFocus(el) { el.value = el.dataset.raw || ''; el.select(); }
function tcNumBlur(el) {
    const raw = parseFloat(el.dataset.raw);
    if (!isNaN(raw)) el.value = tcFmt(raw, tcMoneyDecimals(el, 2));
}
function tcNumInput(el) {
    const v = tcParseNum(el.value);
    if (!isNaN(v)) el.dataset.raw = v;
}

// Formato compacto para valores grandes (M/K)
function tcFmtM(v) {
    const abs = Math.abs(v);
    if (abs >= 1e6) return (v / 1e6).toFixed(2) + ' M';
    if (abs >= 1e3) return (v / 1e3).toFixed(1) + ' K';
    return tcFmt(v, 0);
}

// Nivel de probabilidad con colores semáforo
function tcProbNivel(p) {
    if (p >= 70) return { label: 'Casi Seguro', color: '#dc2626', bg: '#fee2e2' };
    if (p >= 40) return { label: 'Probable',    color: '#ea580c', bg: '#ffedd5' };
    if (p >= 20) return { label: 'Posible',     color: '#d97706', bg: '#fef3c7' };
    return         { label: 'Improbable',   color: '#94a3b8', bg: '#f1f5f9' };
}

// Nivel de riesgo compuesto (Prob × Impacto normalizado)
function tcNivelRiesgo(prob, veAbs) {
    const score = (prob / 100) * (veAbs / 1e6);
    if (score >= 0.5)  return { label: 'Crítico', color: '#dc2626', bg: '#fee2e2' };
    if (score >= 0.1)  return { label: 'Alto',    color: '#ea580c', bg: '#ffedd5' };
    if (score >= 0.02) return { label: 'Medio',   color: '#d97706', bg: '#fef3c7' };
    return               { label: 'Bajo',    color: '#059669', bg: '#d1fae5' };
}

// ════════════════════════════════════════════════════════════════════════════
// PROYECTOS
// ════════════════════════════════════════════════════════════════════════════
async function tcCargarProyectos() {
    const contenedor = document.getElementById('tc-proyectos-lista');
    try {
        const lista = await tcGet('ProyectosJson');
        if (!lista.length) {
            contenedor.innerHTML = `<div class="col-span-3 text-center py-12 text-slate-400">
                <span class="material-icons text-5xl mb-2">folder_open</span>
                <p class="text-base font-medium mb-1">Sin proyectos</p>
                <p class="text-sm">Crea tu primer proyecto para comenzar.</p>
            </div>`;
            return;
        }
        contenedor.innerHTML = lista.map(p => `
            <div class="tc-card hover:shadow-md transition-shadow cursor-pointer" onclick="tcSeleccionarProyecto('${p.id}')">
                <div class="flex items-start justify-between mb-2">
                    <div class="min-w-0">
                        <p class="font-bold text-slate-800 dark:text-slate-100 truncate">${esc(p.nombre)}</p>
                        <p class="text-xs text-slate-500 dark:text-slate-400">${esc(p.codigo)} — ${esc(p.fechaEjercicio)}</p>
                    </div>
                    <span class="badge-${p.estado === 'Activo' ? 'verde' : 'amarillo'} flex-shrink-0 ml-2">${esc(p.estado)}</span>
                </div>
                <p class="text-xs text-slate-500 dark:text-slate-400 mb-3 truncate">${esc(p.organizacion || '—')}</p>
                <div class="flex items-center justify-between">
                    <span class="text-xs text-slate-400">${p.totalContratos} contrato(s) · Tasa: ${tcFmt(p.tasaCambio)}</span>
                    <div class="flex gap-1">
                        <button class="btn-secondary py-1 px-2 text-xs" onclick="tcEditarProyecto(event,'${p.id}')">
                            <span class="material-icons text-sm">edit</span>
                        </button>
                        <button class="btn-danger py-1 px-2 text-xs" onclick="tcEliminarProyecto(event,'${p.id}')">
                            <span class="material-icons text-sm">delete</span>
                        </button>
                    </div>
                </div>
                <p class="text-xs text-slate-400 mt-1">Actualizado: ${esc(p.updatedAt)}</p>
            </div>`).join('');
    } catch(e) {
        contenedor.innerHTML = `<div class="col-span-3 text-center py-8 text-red-500 text-sm">${e.message}</div>`;
    }
}

async function tcSeleccionarProyecto(id, silent = false) {
    try {
        const p = await tcGet('ProyectoJson', { id });
        // Resetear estado de revisión al cambiar de proyecto
        if (TC.proyectoId !== p.id) {
            TC.revisionId = null;
            TC.riesgos    = [];
        }
        TC.proyectoId = p.id;
        TC.proyecto   = p;
        localStorage.setItem('tc-last-proy', id);
        // Actualizar header
        const hdr = document.getElementById('tc-proyecto-header');
        if (hdr) hdr.textContent = `${p.codigo} — ${p.nombre}`;
        // Precargar form de datos proyecto (null-safe)
        tcSetVal('proy-nombre', p.nombre);
        tcSetVal('proy-codigo', p.codigo);
        tcSetVal('proy-fecha',  p.fechaEjercicio);
        tcSetVal('proy-org',    p.organizacion);
        // Cargar contratos
        await tcCargarContratos();
        // Pre-cargar revisiones en background (sin await)
        tcCargarRevisionesSelect();
        tcCargarRevisionesSelectMC();
        if (!silent) tcNav('contratos');
    } catch(e) { if (!silent) alert('Error: ' + e.message); else throw e; }
}

function tcAbrirModalProyecto() {
    ['modal-proy-nombre','modal-proy-codigo','modal-proy-org','modal-proy-fecha-tasa'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = '';
    });
    tcSetVal('modal-proy-fecha', new Date().toISOString().substring(0,10));
    const titulo = document.getElementById('modal-proy-titulo');
    if (titulo) titulo.textContent = 'Nuevo Proyecto';
    tcAbrirModal('modal-proyecto');
}

async function tcGuardarProyectoModal() {
    const nombre = document.getElementById('modal-proy-nombre').value.trim();
    const codigo = document.getElementById('modal-proy-codigo').value.trim();
    const fecha  = document.getElementById('modal-proy-fecha').value;
    if (!nombre || !codigo || !fecha) { alert('Nombre, código y fecha son requeridos.'); return; }
    try {
        const r = await tcPost('Proyecto', {
            id: '00000000-0000-0000-0000-000000000000',
            nombre, codigo, fechaEjercicio: fecha,
            organizacion: (document.getElementById('modal-proy-org')?.value ?? '').trim()
        });
        tcCerrarModal('modal-proyecto');
        await tcCargarProyectos();
        await tcSeleccionarProyecto(r.id);
    } catch(e) { alert('Error: ' + e.message); }
}

async function tcGuardarProyecto() {
    if (!TC.proyectoId) { alert('No hay proyecto seleccionado.'); return; }
    const nombre = document.getElementById('proy-nombre').value.trim();
    const codigo = document.getElementById('proy-codigo').value.trim();
    const fecha  = document.getElementById('proy-fecha').value;
    if (!nombre || !codigo || !fecha) { alert('Nombre, código y fecha son requeridos.'); return; }
    try {
        await tcPost('Proyecto', {
            id: TC.proyectoId, nombre, codigo, fechaEjercicio: fecha,
            organizacion: (document.getElementById('proy-org')?.value ?? '').trim()
        });
        document.getElementById('tc-proyecto-header').textContent = `${codigo} — ${nombre}`;
        if (TC.proyecto) {
            TC.proyecto.nombre = nombre;
            TC.proyecto.codigo = codigo;
        }
        alert('Proyecto guardado correctamente.');
    } catch(e) { alert('Error: ' + e.message); }
}

async function tcEditarProyecto(evt, id) {
    evt.stopPropagation();
    await tcSeleccionarProyecto(id);
    tcNav('proyecto');
}

async function tcEliminarProyecto(evt, id) {
    evt.stopPropagation();
    if (!confirm('¿Eliminar proyecto y todos sus datos?')) return;
    try {
        await tcDelete('Proyecto', { id });
        if (TC.proyectoId === id) TC.proyectoId = null;
        tcCargarProyectos();
    } catch(e) { alert('Error: ' + e.message); }
}

// ════════════════════════════════════════════════════════════════════════════
// CONTRATOS
// ════════════════════════════════════════════════════════════════════════════

// Estado de ordenamiento de la tabla
let _tcSortCol = 'orden', _tcSortAsc = true;

async function tcCargarContratos() {
    if (!TC.proyectoId) return;
    const [lista, familias, empresas] = await Promise.all([
        tcGet('ContratosJson',  { proyectoId: TC.proyectoId }),
        tcGet('FamiliasJson',   { proyectoId: TC.proyectoId }),
        tcGet('EmpresasJson',   { proyectoId: TC.proyectoId })
    ]);
    TC.contratos = lista;
    TC.familias  = familias;
    TC.empresas  = empresas;
    const sb = document.getElementById('sb-contratos-count');
    if (sb) sb.textContent = lista.length;
    _tcSortCol = 'orden'; _tcSortAsc = true;
    tcRenderContratos();
}

function tcPoblarSelectsEmpresa(selectedId) {
    const optHtml = (TC.empresas || [])
        .map(e => {
            const r = e.rut ? ` · ${esc(e.rut)}` : '';
            return `<option value="${e.id}">${esc(e.nombre)}${r}</option>`;
        })
        .join('');
    const base = '<option value="">— Sin empresa (opcional) —</option>' + optHtml;
    const selM = document.getElementById('modal-cont-empresa');
    if (selM) {
        selM.innerHTML = (TC.empresas && TC.empresas.length)
            ? base
            : '<option value="">— Sin empresa; puede agregarlas en la pestaña Empresas —</option>';
        selM.value = selectedId || '';
    }
    const selA = document.getElementById('bloqA-empresa');
    if (selA) {
        selA.innerHTML = base;
        selA.value = selectedId || '';
    }
}

async function tcCargarPanelEmpresas() {
    const aviso = document.getElementById('tc-empresas-aviso');
    const cont  = document.getElementById('tc-empresas-lista');
    if (!cont) return;
    if (!TC.proyectoId) {
        if (aviso) { aviso.textContent = 'Selecciona un proyecto en «Mis Proyectos» o en el listado de proyectos.'; aviso.classList.remove('hidden'); }
        cont.innerHTML = '';
        return;
    }
    if (aviso) aviso.classList.add('hidden');
    try {
        TC.empresas = await tcGet('EmpresasJson', { proyectoId: TC.proyectoId });
        tcPoblarSelectsEmpresa();
        cont.innerHTML = (TC.empresas && TC.empresas.length) ? (TC.empresas).map(e => `
            <div class="flex items-center gap-3 py-2 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900">
                <span class="material-icons text-teal-500 flex-shrink-0" style="font-size:20px">business</span>
                <div class="flex-1 min-w-0">
                    <div class="text-sm font-medium text-slate-800 dark:text-slate-100">${esc(e.nombre)} <span class="text-slate-500 font-normal">${esc(e.rut || '')}</span></div>
                    ${[e.contacto, e.email, e.telefono].filter(Boolean).length ? `<div class="text-xs text-slate-500 truncate">${[e.contacto, e.email, e.telefono].filter(Boolean).map(esc).join(' · ')}</div>` : ''}
                    ${e.notas ? `<div class="text-xs text-slate-400 truncate mt-0.5">${esc(e.notas)}</div>` : ''}
                </div>
                <span class="text-xs text-slate-400 flex-shrink-0">${e.totalPaquetes} paquete(s)</span>
                <button type="button" onclick="tcAbrirModalEmpresaEdit('${e.id}')" class="p-1.5 text-slate-400 hover:text-teal-600 rounded" title="Editar">
                    <span class="material-icons" style="font-size:18px">edit</span>
                </button>
                <button type="button" onclick="tcEliminarEmpresa('${e.id}','${esc(e.nombre).replace(/'/g, "\\'")}')" class="p-1.5 text-slate-400 hover:text-red-500 rounded" title="Eliminar">
                    <span class="material-icons" style="font-size:18px">delete</span>
                </button>
            </div>`).join('') : `<p class="text-sm text-slate-400 text-center py-8">No hay empresas registradas. Usa <strong>Nueva empresa</strong>.</p>`;
    } catch (err) {
        cont.innerHTML = `<p class="text-sm text-red-500">${esc(err.message)}</p>`;
    }
}

function tcAbrirModalEmpresaNueva() {
    if (!TC.proyectoId) { alert('Primero selecciona un proyecto.'); return; }
    const tit = document.getElementById('modal-emp-titulo');
    if (tit) tit.textContent = 'Nueva empresa';
    tcSetVal('modal-emp-id', '');
    ['modal-emp-rut','modal-emp-nombre','modal-emp-contacto','modal-emp-email','modal-emp-telefono','modal-emp-notas'].forEach(id => tcSetVal(id, ''));
    tcAbrirModal('modal-empresa');
}

function tcAbrirModalEmpresaEdit(id) {
    const e = (TC.empresas || []).find(x => x.id === id);
    if (!e) return;
    const tit = document.getElementById('modal-emp-titulo');
    if (tit) tit.textContent = 'Editar empresa';
    tcSetVal('modal-emp-id', e.id);
    tcSetVal('modal-emp-rut', e.rut || '');
    tcSetVal('modal-emp-nombre', e.nombre || '');
    tcSetVal('modal-emp-contacto', e.contacto || '');
    tcSetVal('modal-emp-email', e.email || '');
    tcSetVal('modal-emp-telefono', e.telefono || '');
    tcSetVal('modal-emp-notas', e.notas || '');
    tcAbrirModal('modal-empresa');
}

async function tcGuardarModalEmpresa() {
    const rut    = (document.getElementById('modal-emp-rut')?.value || '').trim();
    const nombre = (document.getElementById('modal-emp-nombre')?.value || '').trim();
    if (!rut || !nombre) { alert('RUT y nombre o razón social son obligatorios.'); return; }
    if (!TC.proyectoId) return;
    const idRaw = (document.getElementById('modal-emp-id')?.value || '').trim();
    try {
        await tcPost('Empresa', {
            id: idRaw || '00000000-0000-0000-0000-000000000000',
            proyectoId: TC.proyectoId,
            rut,
            nombre,
            contacto: (document.getElementById('modal-emp-contacto')?.value || '').trim() || null,
            email: (document.getElementById('modal-emp-email')?.value || '').trim() || null,
            telefono: (document.getElementById('modal-emp-telefono')?.value || '').trim() || null,
            notas: (document.getElementById('modal-emp-notas')?.value || '').trim() || null
        });
        tcCerrarModal('modal-empresa');
        TC.empresas = await tcGet('EmpresasJson', { proyectoId: TC.proyectoId });
        tcPoblarSelectsEmpresa();
        if (document.getElementById('tc-empresas-lista')) await tcCargarPanelEmpresas();
        if (TC.contratoId) {
            const c = TC.contratos?.find(x => x.id === TC.contratoId);
            tcPoblarSelectsEmpresa(c?.empresaId);
        }
    } catch (e) { alert('Error: ' + e.message); }
}

async function tcEliminarEmpresa(id, nombre) {
    if (!confirm(`¿Eliminar la empresa «${nombre}»?`)) return;
    try {
        await tcDelete('Empresa', { id });
        TC.empresas = await tcGet('EmpresasJson', { proyectoId: TC.proyectoId });
        tcPoblarSelectsEmpresa(TC.contratoId ? (TC.contratos || []).find(x => x.id === TC.contratoId)?.empresaId : null);
        await tcCargarContratos();
        if (document.getElementById('tc-empresas-lista')) await tcCargarPanelEmpresas();
    } catch (e) { alert(e.message || String(e)); }
}

// ── Gestión de Familias ───────────────────────────────────────────────────

function tcToggleVistaContratos(vista) {
    _tcContratosVista = vista;
    const btnL = document.getElementById('btn-vista-lista');
    const btnF = document.getElementById('btn-vista-familia');
    const actv = 'bg-white dark:bg-slate-700 shadow-sm text-teal-700 dark:text-teal-300';
    const inact = 'text-slate-500 dark:text-slate-400 hover:bg-white/60 dark:hover:bg-slate-600/40';
    if (vista === 'lista') {
        btnL?.setAttribute('class', `flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors ${actv}`);
        btnF?.setAttribute('class', `flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors ${inact}`);
    } else {
        btnF?.setAttribute('class', `flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors ${actv}`);
        btnL?.setAttribute('class', `flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors ${inact}`);
    }
    tcRenderContratos();
}

function tcTogglePanelFamilias() {
    const panel = document.getElementById('tc-familias-panel');
    if (!panel) return;
    panel.classList.toggle('hidden');
    if (!panel.classList.contains('hidden')) tcRenderPanelFamilias();
}

function tcRenderPanelFamilias() {
    const panel = document.getElementById('tc-familias-panel');
    if (!panel) return;
    const fams = TC.familias || [];
    const rows = fams.map(f => `
        <div class="flex items-center gap-2 py-2 border-b border-slate-100 dark:border-slate-800 last:border-0 group">
            <span class="material-icons text-teal-500 flex-shrink-0" style="font-size:16px">folder</span>
            <div class="flex-1 min-w-0">
                <div class="text-sm font-medium text-slate-700 dark:text-slate-200 truncate">${esc(f.nombre)}</div>
                ${f.descripcion ? `<div class="text-xs text-slate-400 truncate">${esc(f.descripcion)}</div>` : ''}
            </div>
            <span class="text-xs text-slate-400 flex-shrink-0">${f.totalContratos} contrato${f.totalContratos!==1?'s':''}</span>
            <button onclick="tcEditarFamilia('${f.id}','${esc(f.nombre).replace(/'/g,"\\'")}','${esc(f.descripcion||'').replace(/'/g,"\\'")}' )"
                class="opacity-0 group-hover:opacity-100 p-1 rounded text-slate-400 hover:text-teal-600 hover:bg-teal-50 dark:hover:bg-teal-900/30 transition-all" title="Editar">
                <span class="material-icons" style="font-size:14px">edit</span>
            </button>
            <button onclick="tcEliminarFamilia('${f.id}','${esc(f.nombre).replace(/'/g,"\\'")}')"
                class="opacity-0 group-hover:opacity-100 p-1 rounded text-slate-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-900/30 transition-all" title="Eliminar">
                <span class="material-icons" style="font-size:14px">delete</span>
            </button>
        </div>`).join('');

    panel.innerHTML = `
    <div class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl shadow-sm mb-4 overflow-hidden">
        <div class="flex items-center justify-between px-4 py-3 bg-slate-50 dark:bg-slate-800/60 border-b border-slate-200 dark:border-slate-700">
            <div class="flex items-center gap-2">
                <span class="material-icons text-teal-500" style="font-size:18px">folder_special</span>
                <span class="text-sm font-semibold text-slate-700 dark:text-slate-200">Familias del Proyecto</span>
                <span class="text-xs text-slate-400">(${fams.length})</span>
            </div>
            <div class="flex items-center gap-2">
                <button onclick="tcNuevaFamilia()"
                    class="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded-lg bg-teal-500 hover:bg-teal-600 text-white transition-colors">
                    <span class="material-icons" style="font-size:14px">add</span> Nueva Familia
                </button>
                <button onclick="tcTogglePanelFamilias()" class="p-1 rounded text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
                    <span class="material-icons" style="font-size:16px">close</span>
                </button>
            </div>
        </div>
        <div class="px-4 py-2 ${fams.length ? '' : 'text-center py-8'}">
            ${fams.length
                ? rows
                : `<p class="text-sm text-slate-400 py-4 text-center">Sin familias. Crea la primera para organizar tus contratos.</p>`}
        </div>
    </div>`;
}

async function tcNuevaFamilia() {
    const nombre = prompt('Nombre de la nueva familia (ej: Construcción, Adquisiciones):');
    if (!nombre?.trim()) return;
    const desc = prompt('Descripción (opcional):') || null;
    try {
        await tcPost('Familia', { id: '00000000-0000-0000-0000-000000000000', proyectoId: TC.proyectoId, nombre: nombre.trim(), descripcion: desc?.trim() || null });
        TC.familias = await tcGet('FamiliasJson', { proyectoId: TC.proyectoId });
        tcRenderPanelFamilias();
        tcRenderContratos();
    } catch(e) { alert('Error: ' + e.message); }
}

async function tcEditarFamilia(id, nombreActual, descActual) {
    const nombre = prompt('Nuevo nombre de la familia:', nombreActual);
    if (!nombre?.trim()) return;
    const desc = prompt('Descripción (opcional):', descActual);
    try {
        await tcPost('Familia', { id, proyectoId: TC.proyectoId, nombre: nombre.trim(), descripcion: desc?.trim() || null });
        TC.familias = await tcGet('FamiliasJson', { proyectoId: TC.proyectoId });
        TC.contratos = await tcGet('ContratosJson', { proyectoId: TC.proyectoId });
        tcRenderPanelFamilias();
        tcRenderContratos();
    } catch(e) { alert('Error: ' + e.message); }
}

async function tcEliminarFamilia(id, nombre) {
    if (!confirm(`¿Eliminar la familia "${nombre}"?\nLos contratos deben estar sin asignar para poder eliminarla.`)) return;
    try {
        await tcDelete('Familia', { id });
        TC.familias = await tcGet('FamiliasJson', { proyectoId: TC.proyectoId });
        tcRenderPanelFamilias();
        tcRenderContratos();
    } catch(e) { alert(e.message); }
}

async function tcAsignarFamiliaContrato(contratoId, familiaId) {
    try {
        await tcPost('AsignarFamilia', { contratoId, familiaId: familiaId || null });
        TC.contratos = await tcGet('ContratosJson', { proyectoId: TC.proyectoId });
        TC.familias  = await tcGet('FamiliasJson',  { proyectoId: TC.proyectoId });
        if (!document.getElementById('tc-familias-panel')?.classList.contains('hidden'))
            tcRenderPanelFamilias();
        tcRenderContratos();
    } catch(e) { alert('Error: ' + e.message); }
}

function tcFiltrarContratos() {
    tcRenderContratos();
}

function tcSortContratos(col) {
    if (_tcSortCol === col) _tcSortAsc = !_tcSortAsc;
    else { _tcSortCol = col; _tcSortAsc = col === 'orden'; }
    tcRenderContratos();
}

function tcRenderContratos() {
    if (_tcContratosVista === 'familia') { tcRenderContratosAgrupados(); return; }

    const lista   = TC.contratos || [];
    const search  = (document.getElementById('tc-contratos-search')?.value || '').toLowerCase().trim();
    const kpiEl   = document.getElementById('tc-contratos-kpi');
    const toolbar = document.getElementById('tc-contratos-toolbar');
    const countEl = document.getElementById('tc-contratos-count-txt');
    const contenedor = document.getElementById('tc-contratos-lista');

    // Filtrar
    const filtered = search
        ? lista.filter(c => (c.codigo||'').toLowerCase().includes(search)
                         || (c.nombrePaquete||'').toLowerCase().includes(search)
                         || (c.empresaNombre||'').toLowerCase().includes(search))
        : [...lista];

    // Ordenar
    const sortFns = {
        orden:           (a,b) => (a.orden??0) - (b.orden??0),
        codigo:          (a,b) => (a.codigo||'').localeCompare(b.codigo||''),
        nombrePaquete:   (a,b) => (a.nombrePaquete||'').localeCompare(b.nombrePaquete||''),
        empresaNombre:   (a,b) => (a.empresaNombre||'').localeCompare(b.empresaNombre||''),
        capexUsd:        (a,b) => (a.capexUsd??0) - (b.capexUsd??0),
        compometidoUsd:  (a,b) => (a.compometidoUsd??0) - (b.compometidoUsd??0),
        porComprometidoUsd:(a,b)=> (a.porComprometidoUsd??0) - (b.porComprometidoUsd??0),
        estimadoTerminoUsd:(a,b)=> (a.estimadoTerminoUsd??0) - (b.estimadoTerminoUsd??0),
        pctEAT:          (a,b) => {
            const pA = a.capexUsd > 0 ? a.estimadoTerminoUsd/a.capexUsd : 0;
            const pB = b.capexUsd > 0 ? b.estimadoTerminoUsd/b.capexUsd : 0;
            return pA - pB;
        },
        totalItems:      (a,b) => (a.totalItems??0) - (b.totalItems??0),
    };
    const fn = sortFns[_tcSortCol] || sortFns.orden;
    filtered.sort((a,b) => _tcSortAsc ? fn(a,b) : fn(b,a));

    // ── KPI bar ──────────────────────────────────────────────────────────────
    if (lista.length) {
        const totCapex = lista.reduce((s,c) => s+(c.capexUsd??0), 0);
        const totComp  = lista.reduce((s,c) => s+(c.compometidoUsd??0), 0);
        const totEAT   = lista.reduce((s,c) => s+(c.estimadoTerminoUsd??0), 0);
        const pctEAT   = totCapex > 0 ? totEAT/totCapex*100 : 0;
        const ragCls   = pctEAT > 115 ? 'text-red-600 dark:text-red-400'
                       : pctEAT > 100 ? 'text-amber-600 dark:text-amber-400'
                       :                'text-teal-600 dark:text-teal-400';
        const kpiItem = (label, val, sub, valCls='') => `
        <div class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl p-3">
            <p class="text-xs text-slate-400 uppercase tracking-wide mb-1">${label}</p>
            <p class="text-base font-bold tabular-nums ${valCls}">${val}</p>
            ${sub ? `<p class="text-xs text-slate-400 mt-0.5">${sub}</p>` : ''}
        </div>`;
        if (kpiEl) {
            kpiEl.innerHTML =
                kpiItem('Contratos', lista.length, `${search?filtered.length+' filtrados':'todos'}`) +
                kpiItem('CAPEX Total', '$'+tcFmt(totCapex,0)+' USD', '') +
                kpiItem('EAT Total', '$'+tcFmt(totEAT,0)+' USD', '') +
                kpiItem('EAT / CAPEX', tcFmt(pctEAT,1)+'%',
                    pctEAT>115?'Sobre presupuesto':pctEAT>100?'En alerta':'Dentro de presupuesto', ragCls);
            kpiEl.classList.remove('hidden');
        }
        if (toolbar) toolbar.classList.remove('hidden');
        if (countEl) countEl.textContent = search ? `${filtered.length} de ${lista.length}` : '';
    } else {
        if (kpiEl) kpiEl.classList.add('hidden');
        if (toolbar) toolbar.classList.add('hidden');
    }

    // ── Sin contratos ─────────────────────────────────────────────────────────
    if (!lista.length) {
        contenedor.innerHTML = `<div class="text-center py-14 text-slate-400">
            <span class="material-icons text-5xl mb-3 block">receipt_long</span>
            <p class="text-sm font-semibold mb-1">Sin contratos registrados</p>
            <p class="text-xs">Haz clic en <strong>Nuevo Contrato</strong> para agregar el primero.</p>
        </div>`;
        return;
    }
    if (!filtered.length) {
        contenedor.innerHTML = `<div class="text-center py-10 text-slate-400">
            <span class="material-icons text-3xl mb-2 block">search_off</span>
            <p class="text-sm">Sin resultados para "<em>${esc(search)}</em>"</p>
        </div>`;
        return;
    }

    // ── Tabla ─────────────────────────────────────────────────────────────────
    const fU  = v => '$' + tcFmt(v??0, 0);
    const rag = (eat, capex) => {
        if (!capex) return '';
        const p = eat/capex*100;
        const dot = p>115?'<span class="text-red-500">●</span>'
                  : p>100?'<span class="text-amber-500">●</span>'
                  :       '<span class="text-teal-500">●</span>';
        return `${dot} <span class="tabular-nums">${tcFmt(p,1)}%</span>`;
    };

    // Helper: encabezado ordenable
    const th = (col, label, cls='') => {
        const active = _tcSortCol === col;
        const arrow  = active ? (_tcSortAsc ? '↑' : '↓') : '';
        const ac     = active ? 'text-teal-600 dark:text-teal-400' : 'text-slate-400 dark:text-slate-500';
        return `<th class="py-2 px-3 text-xs font-semibold uppercase tracking-wide cursor-pointer select-none whitespace-nowrap ${cls} hover:text-teal-600 dark:hover:text-teal-400 ${ac}"
                    onclick="tcSortContratos('${col}')">
                    ${label} <span class="ml-0.5 opacity-70">${arrow}</span>
                </th>`;
    };

    const totCapex2 = filtered.reduce((s,c) => s+(c.capexUsd??0), 0);
    const totComp2  = filtered.reduce((s,c) => s+(c.compometidoUsd??0), 0);
    const totPorC2  = filtered.reduce((s,c) => s+(c.porComprometidoUsd??0), 0);
    const totEAT2   = filtered.reduce((s,c) => s+(c.estimadoTerminoUsd??0), 0);
    const totItms2  = filtered.reduce((s,c) => s+(c.totalItems??0), 0);
    const totIncert2= filtered.reduce((s,c) => s+(c.itemsIncertidumbre??0), 0);
    const pctTot    = totCapex2 > 0 ? totEAT2/totCapex2*100 : 0;

    const rows = filtered.map(c => {
        const eat    = c.estimadoTerminoUsd ?? 0;
        const capex  = c.capexUsd ?? 0;
        const pct    = capex > 0 ? eat/capex*100 : 0;
        const rowBg  = pct > 115 ? 'bg-red-50/40 dark:bg-red-900/10'
                     : pct > 100 ? 'bg-amber-50/40 dark:bg-amber-900/10'
                     : '';
        return `<tr class="border-b border-slate-100 dark:border-slate-800 hover:bg-teal-50/40 dark:hover:bg-teal-900/10 cursor-pointer transition-colors ${rowBg}"
                    onclick="tcAbrirContrato('${c.id}')">
            <td class="py-2.5 px-3 text-xs text-slate-400 tabular-nums">${c.orden??''}</td>
            <td class="py-2.5 px-3">
                <span class="inline-block text-xs font-bold px-2 py-0.5 rounded-md bg-teal-50 dark:bg-teal-900/30 text-teal-700 dark:text-teal-400 font-mono">${esc(c.codigo||'—')}</span>
            </td>
            <td class="py-2.5 px-3 text-sm font-medium text-slate-700 dark:text-slate-200 max-w-[220px]">
                <span class="truncate block" title="${esc(c.nombrePaquete||'')}">${esc(c.nombrePaquete||'—')}</span>
                <span class="text-xs text-slate-400">${c.tasaCambio?tcFmt(c.tasaCambio,0)+' CLP/USD':''} ${c.factor&&c.factor!=1?'· Factor '+c.factor:''}</span>
            </td>
            <td class="py-2.5 px-3 text-xs text-slate-600 dark:text-slate-300 max-w-[160px]">
                <span class="truncate block" title="${esc(c.empresaNombre||'')}">${esc(c.empresaNombre||'—')}</span>
            </td>
            <td class="py-2.5 px-3 text-xs text-right tabular-nums text-slate-600 dark:text-slate-400">${fU(c.capexUsd)}</td>
            <td class="py-2.5 px-3 text-xs text-right tabular-nums text-blue-600 dark:text-blue-400">${fU(c.compometidoUsd)}</td>
            <td class="py-2.5 px-3 text-xs text-right tabular-nums text-emerald-600 dark:text-emerald-400">${fU(c.porComprometidoUsd)}</td>
            <td class="py-2.5 px-3 text-xs text-right tabular-nums font-semibold text-slate-700 dark:text-slate-200">${fU(eat)}</td>
            <td class="py-2.5 px-3 text-xs text-center whitespace-nowrap">${rag(eat, capex)}</td>
            <td class="py-2.5 px-3 text-xs text-center tabular-nums">
                <span class="font-semibold">${c.totalItems??0}</span>
                ${c.itemsIncertidumbre ? `<span class="text-amber-500 ml-1">(${c.itemsIncertidumbre}↕)</span>` : ''}
            </td>
            <td class="py-2.5 px-3 text-right whitespace-nowrap" onclick="event.stopPropagation()">
                <button class="inline-flex items-center justify-center w-7 h-7 rounded-lg text-slate-400 hover:text-teal-600 hover:bg-teal-50 dark:hover:bg-teal-900/30 transition-colors mr-1"
                        onclick="tcAbrirContrato('${c.id}')" title="Editar contrato">
                    <span class="material-icons" style="font-size:16px">edit</span>
                </button>
                <button class="inline-flex items-center justify-center w-7 h-7 rounded-lg text-slate-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-900/30 transition-colors"
                        onclick="tcEliminarContrato('${c.id}')" title="Eliminar contrato">
                    <span class="material-icons" style="font-size:16px">delete</span>
                </button>
            </td>
        </tr>`;
    }).join('');

    contenedor.innerHTML = `
    <div class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl overflow-hidden shadow-sm">
        <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
            <thead class="bg-slate-50 dark:bg-slate-800/60 border-b border-slate-200 dark:border-slate-700">
                <tr>
                    ${th('orden',             '#',            'w-8')}
                    ${th('codigo',            'Código')}
                    ${th('nombrePaquete',      'Nombre del Paquete')}
                    ${th('empresaNombre',      'Empresa')}
                    ${th('capexUsd',           'CAPEX',        'text-right')}
                    ${th('compometidoUsd',     'Comprometido', 'text-right')}
                    ${th('porComprometidoUsd', 'Por Comp.',    'text-right')}
                    ${th('estimadoTerminoUsd', 'EAT',          'text-right')}
                    ${th('pctEAT',             'EAT/CAPEX',    'text-center')}
                    ${th('totalItems',         'Ítems',        'text-center')}
                    <th class="py-2 px-3 text-xs font-semibold uppercase tracking-wide text-slate-400 text-right">Acciones</th>
                </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 dark:divide-slate-800">${rows}</tbody>
            <tfoot class="bg-slate-50 dark:bg-slate-800/40 border-t-2 border-slate-300 dark:border-slate-600 text-xs font-semibold text-slate-600 dark:text-slate-300">
                <tr>
                    <td colspan="4" class="py-2 px-3">
                        TOTALES
                        ${filtered.length < lista.length ? `<span class="font-normal text-teal-600"> (${filtered.length} de ${lista.length})</span>` : `<span class="font-normal text-slate-400"> (${lista.length} contrato${lista.length!==1?'s':''})</span>`}
                    </td>
                    <td class="py-2 px-3 text-right tabular-nums">${fU(totCapex2)}</td>
                    <td class="py-2 px-3 text-right tabular-nums text-blue-600 dark:text-blue-400">${fU(totComp2)}</td>
                    <td class="py-2 px-3 text-right tabular-nums text-emerald-600 dark:text-emerald-400">${fU(totPorC2)}</td>
                    <td class="py-2 px-3 text-right tabular-nums">${fU(totEAT2)}</td>
                    <td class="py-2 px-3 text-center">${rag(totEAT2, totCapex2)}</td>
                    <td class="py-2 px-3 text-center">
                        ${totItms2} <span class="text-amber-500 font-normal">(${totIncert2}↕)</span>
                    </td>
                    <td></td>
                </tr>
            </tfoot>
        </table>
        </div>
        <div class="px-4 py-2 border-t border-slate-100 dark:border-slate-800 flex items-center gap-4 text-xs text-slate-400">
            <span class="flex items-center gap-1"><span class="text-teal-500 font-bold">●</span> EAT ≤ CAPEX</span>
            <span class="flex items-center gap-1"><span class="text-amber-500 font-bold">●</span> EAT 100–115%</span>
            <span class="flex items-center gap-1"><span class="text-red-500 font-bold">●</span> EAT &gt; 115%</span>
            <span class="ml-auto">Haz clic en una fila para editar · <span class="text-teal-500">↑↓</span> ordenable por columna</span>
        </div>
    </div>`;
}

// ── Vista agrupada por familia ─────────────────────────────────────────────
function tcRenderContratosAgrupados() {
    const lista   = TC.contratos || [];
    const fams    = TC.familias  || [];
    const search  = (document.getElementById('tc-contratos-search')?.value || '').toLowerCase().trim();
    const kpiEl   = document.getElementById('tc-contratos-kpi');
    const toolbar = document.getElementById('tc-contratos-toolbar');
    const countEl = document.getElementById('tc-contratos-count-txt');
    const contenedor = document.getElementById('tc-contratos-lista');

    // Filtrar
    const filtered = search
        ? lista.filter(c => (c.codigo||'').toLowerCase().includes(search)
                         || (c.nombrePaquete||'').toLowerCase().includes(search)
                         || (c.empresaNombre||'').toLowerCase().includes(search))
        : [...lista];

    // KPI global (igual que vista lista)
    if (lista.length) {
        const totCapex = lista.reduce((s,c) => s+(c.capexUsd??0), 0);
        const totEAT   = lista.reduce((s,c) => s+(c.estimadoTerminoUsd??0), 0);
        const pctEAT   = totCapex > 0 ? totEAT/totCapex*100 : 0;
        const ragCls   = pctEAT > 115 ? 'text-red-600 dark:text-red-400'
                       : pctEAT > 100 ? 'text-amber-600 dark:text-amber-400'
                       :                'text-teal-600 dark:text-teal-400';
        const kpiItem = (label, val, sub, valCls='') => `
        <div class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl p-3">
            <p class="text-xs text-slate-400 uppercase tracking-wide mb-1">${label}</p>
            <p class="text-base font-bold tabular-nums ${valCls}">${val}</p>
            ${sub ? `<p class="text-xs text-slate-400 mt-0.5">${sub}</p>` : ''}
        </div>`;
        if (kpiEl) {
            kpiEl.innerHTML =
                kpiItem('Contratos', lista.length, search ? filtered.length+' filtrados' : 'todos') +
                kpiItem('CAPEX Total', '$'+tcFmt(totCapex,0)+' USD', '') +
                kpiItem('EAT Total', '$'+tcFmt(totEAT,0)+' USD', '') +
                kpiItem('EAT / CAPEX', tcFmt(pctEAT,1)+'%',
                    pctEAT>115?'Sobre presupuesto':pctEAT>100?'En alerta':'Dentro de presupuesto', ragCls);
            kpiEl.classList.remove('hidden');
        }
        if (toolbar) toolbar.classList.remove('hidden');
        if (countEl) countEl.textContent = search ? `${filtered.length} de ${lista.length}` : '';
    } else {
        if (kpiEl) kpiEl.classList.add('hidden');
        if (toolbar) toolbar.classList.add('hidden');
    }

    if (!lista.length) {
        contenedor.innerHTML = `<div class="text-center py-14 text-slate-400">
            <span class="material-icons text-5xl mb-3 block">receipt_long</span>
            <p class="text-sm font-semibold mb-1">Sin contratos registrados</p>
            <p class="text-xs">Haz clic en <strong>Nuevo Contrato</strong> para agregar el primero.</p>
        </div>`;
        return;
    }

    const fU  = v => '$' + tcFmt(v??0, 0);
    const rag = (eat, capex) => {
        if (!capex) return '';
        const p = eat/capex*100;
        const dot = p>115?'<span class="text-red-500">●</span>'
                  : p>100?'<span class="text-amber-500">●</span>'
                  :       '<span class="text-teal-500">●</span>';
        return `${dot} <span class="tabular-nums">${tcFmt(p,1)}%</span>`;
    };

    // Dropdown de familia para asignar
    const famOpts = [
        `<option value="">— Sin Familia —</option>`,
        ...fams.map(f => `<option value="${f.id}">${esc(f.nombre)}</option>`)
    ].join('');
    // ── helpers de fila y grupo ──────────────────────────────────────────────
    const famSelect = (c) => `
        <select onchange="tcAsignarFamiliaContrato('${c.id}', this.value || null)"
                onclick="event.stopPropagation()"
                title="Cambiar familia"
                class="text-xs rounded border border-slate-200 dark:border-slate-700 bg-transparent
                       text-slate-500 dark:text-slate-400 px-1 py-0.5 focus:outline-none focus:ring-1
                       focus:ring-teal-400 max-w-[140px]">
            ${fams.map(f => `<option value="${f.id}" ${c.familiaId===f.id?'selected':''}>${esc(f.nombre)}</option>`).join('')}
            <option value="" ${!c.familiaId?'selected':''}>— Sin Familia —</option>
        </select>`;

    const contratoRow = (c, accentColor = '#14b8a6', extraAttrs = '') => {
        const eat   = c.estimadoTerminoUsd ?? 0;
        const capex = c.capexUsd ?? 0;
        const pct   = capex > 0 ? eat/capex*100 : 0;
        const rowBg = pct > 115 ? 'bg-red-50/30 dark:bg-red-900/10'
                    : pct > 100 ? 'bg-amber-50/30 dark:bg-amber-900/10' : '';
        return `<tr ${extraAttrs} class="border-b border-slate-100 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800/50 cursor-pointer transition-colors ${rowBg}"
                    onclick="tcAbrirContrato('${c.id}')">
            <td class="py-2.5 pl-8 pr-3" style="border-left: 3px solid ${accentColor}20">
                <span class="inline-block text-xs font-bold px-2 py-0.5 rounded-md bg-teal-50 dark:bg-teal-900/30 text-teal-700 dark:text-teal-400 font-mono">${esc(c.codigo||'—')}</span>
            </td>
            <td class="py-2.5 px-3 text-sm font-medium text-slate-700 dark:text-slate-200 max-w-[220px]">
                <span class="truncate block" title="${esc(c.nombrePaquete||'')}">${esc(c.nombrePaquete||'—')}</span>
                <span class="text-xs text-slate-400">${c.tasaCambio?tcFmt(c.tasaCambio,0)+' CLP/USD':''} ${c.factor&&c.factor!=1?'· Factor '+c.factor:''}</span>
            </td>
            <td class="py-2.5 px-3 text-xs text-slate-500 max-w-[130px]"><span class="truncate block">${esc(c.empresaNombre||'—')}</span></td>
            <td class="py-2.5 px-3 text-xs text-right tabular-nums text-slate-500 dark:text-slate-400">${fU(capex)}</td>
            <td class="py-2.5 px-3 text-xs text-right tabular-nums font-semibold text-slate-700 dark:text-slate-200">${fU(eat)}</td>
            <td class="py-2.5 px-3 text-xs text-center whitespace-nowrap">${rag(eat, capex)}</td>
            <td class="py-2.5 px-3 text-xs text-center tabular-nums text-slate-500">
                <span class="font-semibold text-slate-700 dark:text-slate-300">${c.totalItems??0}</span>
                ${c.itemsIncertidumbre ? `<span class="text-amber-500 ml-1">(${c.itemsIncertidumbre}↕)</span>` : ''}
            </td>
            <td class="py-2.5 px-3 text-right whitespace-nowrap" onclick="event.stopPropagation()">
                ${famSelect(c)}
                <button class="inline-flex items-center justify-center w-6 h-6 rounded text-slate-300 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-900/30 transition-colors ml-1"
                        onclick="tcEliminarContrato('${c.id}')" title="Eliminar">
                    <span class="material-icons" style="font-size:14px">delete</span>
                </button>
            </td>
        </tr>`;
    };

    // Colores por familia (ciclo)
    const PALETTE = ['#14b8a6','#6366f1','#f59e0b','#10b981','#3b82f6','#ec4899','#8b5cf6','#f97316'];

    const renderGrupo = (nombre, contratos, icono, accentColor, esVacia = false, famKey = 'grp') => {
        if (esVacia) return `
            <tr>
                <td colspan="8" style="border-left: 4px solid ${accentColor}40"
                    class="py-3 pl-4 pr-3 bg-slate-50/50 dark:bg-slate-800/20 border-b border-slate-100 dark:border-slate-800">
                    <div class="flex items-center gap-2">
                        <span class="material-icons" style="font-size:15px; color:${accentColor}80">${icono}</span>
                        <span class="text-sm font-semibold" style="color:${accentColor}99">${esc(nombre)}</span>
                        <span class="text-xs italic text-slate-300 dark:text-slate-600">Sin contratos asignados</span>
                    </div>
                </td>
            </tr>`;

        const totC  = contratos.reduce((s,c) => s+(c.capexUsd??0), 0);
        const totE  = contratos.reduce((s,c) => s+(c.estimadoTerminoUsd??0), 0);
        const totI  = contratos.reduce((s,c) => s+(c.totalItems??0), 0);
        const totIn = contratos.reduce((s,c) => s+(c.itemsIncertidumbre??0), 0);
        const pct   = totC > 0 ? totE/totC*100 : 0;
        const ragCl = pct > 115 ? '#ef4444' : pct > 100 ? '#f59e0b' : '#14b8a6';
        const isCol = !!_tcFamCollapsed[famKey];

        const header = `
            <tr style="cursor:pointer;user-select:none" onclick="tcToggleFam('${famKey}')">
                <td colspan="8" style="border-left: 4px solid ${accentColor}"
                    class="py-3 pl-3 pr-4 bg-slate-50 dark:bg-slate-800/60 border-y border-slate-200 dark:border-slate-700">
                    <div class="flex items-center justify-between flex-wrap gap-2">
                        <div class="flex items-center gap-1.5">
                            <span id="fam-chev-${famKey}" class="material-icons text-slate-400 transition-transform duration-200"
                                  style="font-size:18px;${isCol ? 'transform:rotate(-90deg)' : ''}">expand_more</span>
                            <span class="material-icons" style="font-size:17px; color:${accentColor}">${icono}</span>
                            <span class="text-sm font-bold text-slate-700 dark:text-slate-100">${esc(nombre)}</span>
                            <span class="text-xs font-medium px-1.5 py-0.5 rounded-full" style="background:${accentColor}20; color:${accentColor}">
                                ${contratos.length} contrato${contratos.length!==1?'s':''}
                            </span>
                            ${isCol ? `<span class="text-xs text-slate-400 italic">(colapsado)</span>` : ''}
                        </div>
                        <div class="flex items-center gap-3 text-xs tabular-nums">
                            <span class="text-slate-400">CAPEX <strong class="text-slate-600 dark:text-slate-300">${fU(totC)}</strong></span>
                            <span class="text-slate-400">EAT <strong class="text-slate-600 dark:text-slate-300">${fU(totE)}</strong></span>
                            <span class="font-semibold px-2 py-0.5 rounded" style="background:${ragCl}15; color:${ragCl}">
                                ${tcFmt(pct,1)}%
                            </span>
                        </div>
                    </div>
                </td>
            </tr>`;

        const rows = contratos.map(c => contratoRow(
            c, accentColor,
            `data-fam="${famKey}"${isCol ? ' style="display:none"' : ''}`
        )).join('');

        const subtotal = `
            <tr class="border-b-2 border-slate-200 dark:border-slate-700">
                <td colspan="3" style="border-left: 4px solid ${accentColor}40"
                    class="py-2 pl-8 pr-3 text-xs font-semibold text-slate-400 bg-slate-50/70 dark:bg-slate-800/30 uppercase tracking-wide">
                    Subtotal ${esc(nombre)}
                </td>
                <td class="py-2 px-3 text-xs text-right font-semibold tabular-nums text-slate-600 dark:text-slate-300 bg-slate-50/70 dark:bg-slate-800/30">${fU(totC)}</td>
                <td class="py-2 px-3 text-xs text-right font-bold tabular-nums bg-slate-50/70 dark:bg-slate-800/30" style="color:${ragCl}">${fU(totE)}</td>
                <td class="py-2 px-3 text-xs text-center font-semibold bg-slate-50/70 dark:bg-slate-800/30" style="color:${ragCl}">${tcFmt(pct,1)}%</td>
                <td class="py-2 px-3 text-xs text-center tabular-nums text-slate-500 bg-slate-50/70 dark:bg-slate-800/30">
                    ${totI} <span class="text-amber-500">(${totIn}↕)</span>
                </td>
                <td class="bg-slate-50/70 dark:bg-slate-800/30"></td>
            </tr>`;

        return header + rows + subtotal;
    };

    // Construir secciones
    let sections = '';
    const sortedFams = [...fams].sort((a,b) => (a.orden??0)-(b.orden??0));
    sortedFams.forEach((f, i) => {
        const color  = PALETTE[i % PALETTE.length];
        const conts  = filtered.filter(c => c.familiaId === f.id);
        const famKey = `ctab-${f.id}`;
        sections    += renderGrupo(f.nombre, conts, 'folder', color, conts.length === 0, famKey);
    });

    const sinFam = filtered.filter(c => !c.familiaId);
    if (sinFam.length) {
        sections += renderGrupo('Sin Familia', sinFam, 'folder_off', '#94a3b8', false, 'ctab-sin-fam');
    }

    if (!sections && !fams.length && !sinFam.length) {
        contenedor.innerHTML = `<div class="text-center py-10 text-slate-400">
            <span class="material-icons text-3xl mb-2 block">search_off</span>
            <p class="text-sm">Sin resultados para "<em>${esc(search)}</em>"</p>
        </div>`;
        return;
    }

    // Gran total
    const totCapexAll = filtered.reduce((s,c) => s+(c.capexUsd??0), 0);
    const totEATAll   = filtered.reduce((s,c) => s+(c.estimadoTerminoUsd??0), 0);
    const totItmsAll  = filtered.reduce((s,c) => s+(c.totalItems??0), 0);
    const totIncAll   = filtered.reduce((s,c) => s+(c.itemsIncertidumbre??0), 0);
    const pctTot      = totCapexAll > 0 ? totEATAll/totCapexAll*100 : 0;
    const ragTot      = pctTot > 115 ? '#ef4444' : pctTot > 100 ? '#f59e0b' : '#14b8a6';

    contenedor.innerHTML = `
    <div class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl overflow-hidden shadow-sm">
        <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
            <thead class="bg-slate-50 dark:bg-slate-800/60 border-b border-slate-200 dark:border-slate-700">
                <tr>
                    <th class="py-2 pl-8 pr-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Código</th>
                    <th class="py-2 px-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Nombre del Paquete</th>
                    <th class="py-2 px-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Empresa</th>
                    <th class="py-2 px-3 text-xs font-semibold uppercase tracking-wide text-slate-400 text-right">CAPEX</th>
                    <th class="py-2 px-3 text-xs font-semibold uppercase tracking-wide text-slate-400 text-right">EAT</th>
                    <th class="py-2 px-3 text-xs font-semibold uppercase tracking-wide text-slate-400 text-center">EAT/CAPEX</th>
                    <th class="py-2 px-3 text-xs font-semibold uppercase tracking-wide text-slate-400 text-center">Ítems</th>
                    <th class="py-2 px-3 text-xs font-semibold uppercase tracking-wide text-slate-400 text-right">Acciones</th>
                </tr>
            </thead>
            <tbody>${sections}</tbody>
            <tfoot class="border-t-2 border-slate-300 dark:border-slate-600">
                <tr class="bg-slate-100 dark:bg-slate-800">
                    <td colspan="3" class="py-2.5 pl-4 pr-3 text-xs font-bold text-slate-700 dark:text-slate-200 uppercase tracking-wide">
                        TOTAL PROYECTO
                        <span class="font-normal text-slate-400 normal-case tracking-normal ml-1">(${lista.length} contratos)</span>
                    </td>
                    <td class="py-2.5 px-3 text-xs text-right font-bold tabular-nums text-slate-600 dark:text-slate-300">${fU(totCapexAll)}</td>
                    <td class="py-2.5 px-3 text-xs text-right font-bold tabular-nums" style="color:${ragTot}">${fU(totEATAll)}</td>
                    <td class="py-2.5 px-3 text-xs text-center font-bold" style="color:${ragTot}">${tcFmt(pctTot,1)}%</td>
                    <td class="py-2.5 px-3 text-xs text-center text-slate-500">${totItmsAll} <span class="text-amber-500">(${totIncAll}↕)</span></td>
                    <td></td>
                </tr>
            </tfoot>
        </table>
        </div>
        <div class="px-4 py-2 border-t border-slate-100 dark:border-slate-800 flex items-center gap-4 text-xs text-slate-400">
            <span class="flex items-center gap-1"><span style="color:#14b8a6" class="font-bold">●</span> EAT ≤ CAPEX</span>
            <span class="flex items-center gap-1"><span style="color:#f59e0b" class="font-bold">●</span> EAT 100–115%</span>
            <span class="flex items-center gap-1"><span style="color:#ef4444" class="font-bold">●</span> EAT &gt; 115%</span>
            <span class="ml-auto text-slate-300 dark:text-slate-600">Clic en la fila para editar · usa el selector para reasignar familia</span>
        </div>
    </div>`;
}

function tcAbrirModalContrato() {
    if (!TC.proyectoId) { alert('Primero selecciona o crea un proyecto.'); return; }
    document.getElementById('modal-cont-id').value = '';
    ['modal-cont-codigo','modal-cont-nombre'].forEach(id => document.getElementById(id).value = '');
    ['modal-cont-capex','modal-cont-comp','modal-cont-porcomp','modal-cont-eat'].forEach(id => document.getElementById(id).value = '0');
    const defTasa = TC.contratos?.length > 0 ? (TC.contratos[TC.contratos.length-1].tasaCambio || 900) : 900;
    const defFactor = TC.contratos?.length > 0 ? (TC.contratos[TC.contratos.length-1].factor || 1) : 1;
    tcSetVal('modal-cont-tasa',   defTasa);
    tcSetVal('modal-cont-factor', defFactor);
    tcSetVal('modal-cont-fecha-tasa', '');
    const selE = document.getElementById('modal-cont-empresa');
    if (selE) { tcPoblarSelectsEmpresa(); selE.value = (TC.empresas && TC.empresas.length === 1) ? TC.empresas[0].id : ''; }
    document.getElementById('modal-cont-titulo').textContent = 'Nuevo Contrato';
    // Listeners auto-calc EAT
    ['modal-cont-comp','modal-cont-porcomp'].forEach(id => {
        document.getElementById(id).oninput = tcCalcEATModal;
    });
    tcAbrirModal('modal-contrato');
}

function tcCalcEATModal() {
    const comp    = parseFloat(document.getElementById('modal-cont-comp').value) || 0;
    const porcomp = parseFloat(document.getElementById('modal-cont-porcomp').value) || 0;
    document.getElementById('modal-cont-eat').value = (comp + porcomp).toFixed(2);
}

async function tcGuardarContratoModal() {
    const nombre = document.getElementById('modal-cont-nombre').value.trim();
    if (!nombre) { alert('El nombre del paquete es requerido.'); return; }
    const empSel = document.getElementById('modal-cont-empresa')?.value;
    try {
        await tcPost('Contrato', {
            id: document.getElementById('modal-cont-id').value || '00000000-0000-0000-0000-000000000000',
            proyectoId: TC.proyectoId,
            codigo: document.getElementById('modal-cont-codigo').value.trim(),
            nombrePaquete: nombre,
            capexUsd: parseFloat(document.getElementById('modal-cont-capex').value) || 0,
            compometidoUsd: parseFloat(document.getElementById('modal-cont-comp').value) || 0,
            porComprometidoUsd: parseFloat(document.getElementById('modal-cont-porcomp').value) || 0,
            estimadoTerminoUsd: parseFloat(document.getElementById('modal-cont-eat').value) || 0,
            tasaCambio: parseFloat(document.getElementById('modal-cont-tasa')?.value) || 900,
            factor: parseFloat(document.getElementById('modal-cont-factor')?.value) || 1,
            fechaTasaCambio: document.getElementById('modal-cont-fecha-tasa')?.value || null,
            empresaId: empSel || null
        });
        tcCerrarModal('modal-contrato');
        tcCargarContratos();
    } catch(e) { alert('Error: ' + e.message); }
}

async function tcAbrirContrato(contratoId) {
    TC.contratoId = contratoId;
    let c = tcContratoById(contratoId);
    if (!c && TC.proyectoId) {
        try {
            TC.contratos = await tcGet('ContratosJson', { proyectoId: TC.proyectoId });
            c = tcContratoById(contratoId);
        } catch (e) {
            console.error('Error refrescando contratos:', e);
        }
    }
    if (c) {
        // Actualizar headers de bloques
        ['A','B','D','E','F'].forEach(l => {
            const el  = document.getElementById(`tc-contrato-codigo-${l}`);
            const el2 = document.getElementById(`tc-contrato-nombre-${l}`);
            if (el)  el.textContent  = c.codigo;
            if (el2) el2.textContent = c.nombrePaquete;
        });
        // Pre-cargar Bloque A (null-safe: los IDs pueden no existir en Montecarlo)
        tcSetVal('bloqA-nombre',      c.nombrePaquete || '');
        tcSetMoneyInput('bloqA-capex',   c.capexUsd || 0, 0);
        tcSetMoneyInput('bloqA-comp',    c.compometidoUsd || 0, 0);
        tcSetMoneyInput('bloqA-porcomp', c.porComprometidoUsd || 0, 0);
        tcSetMoneyInput('bloqA-eat',     c.estimadoTerminoUsd || 0, 0);
        tcSetVal('bloqA-tasa',        c.tasaCambio ?? 900);
        tcSetVal('bloqA-factor',      c.factor != null ? c.factor : 1);
        tcSetVal('bloqA-fecha-tasa',  c.fechaTasaCambio ?? '');
        // Poblar selector de familia
        const selFam = document.getElementById('bloqA-familia');
        if (selFam) {
            selFam.innerHTML = '<option value="">— Sin Familia —</option>' +
                (TC.familias || []).map(f =>
                    `<option value="${f.id}" ${c.familiaId === f.id ? 'selected' : ''}>${esc(f.nombre)}</option>`
                ).join('');
        }
        tcPoblarSelectsEmpresa(c.empresaId || null);
        // Actualizar tasa activa para cálculos
        TC.tasaCambio = parseFloat(c.tasaCambio) || 900;
    }
    // Cargar ítems del contrato
    try {
        await tcCargarItems();
    } catch(e) {
        console.error('Error cargando ítems:', e);
    }
    tcNav('bloqueA');
}

async function tcEliminarContrato(id) {
    if (!confirm('¿Eliminar este contrato y todos sus ítems?')) return;
    try {
        await tcDelete('Contrato', { id });
        tcCargarContratos();
    } catch(e) { alert('Error: ' + e.message); }
}

// ════════════════════════════════════════════════════════════════════════════
// BLOQUE A — guardar
// ════════════════════════════════════════════════════════════════════════════
function tcCalcEAT() {
    const compEl = document.getElementById('bloqA-comp');
    const porCompEl = document.getElementById('bloqA-porcomp');
    const eatEl = document.getElementById('bloqA-eat');
    const comp = tcParseInput(compEl);
    const porcomp = tcParseInput(porCompEl);
    const eat = comp + porcomp;
    if (eatEl) {
        eatEl.dataset.raw = String(eat);
        eatEl.dataset.decimals = eatEl.dataset.decimals || '0';
        eatEl.value = tcFmt(eat, tcMoneyDecimals(eatEl, 0));
    }
    // validar suma ítems vs PorComprometer
    tcValidarBloqueB();
}

async function tcGuardarBloqueA() {
    if (!TC.contratoId) return;
    const empBloq = document.getElementById('bloqA-empresa')?.value || null;
    try {
        const tasa      = parseFloat(document.getElementById('bloqA-tasa')?.value) || 900;
        const factor    = parseFloat(document.getElementById('bloqA-factor')?.value) || 1;
        const fecha     = document.getElementById('bloqA-fecha-tasa')?.value || null;
        const familiaId = document.getElementById('bloqA-familia')?.value || null;

        await tcPost('Contrato', {
            id: TC.contratoId,
            proyectoId: TC.proyectoId,
            codigo: '',
            nombrePaquete: document.getElementById('bloqA-nombre').value.trim(),
            capexUsd: tcParseInput(document.getElementById('bloqA-capex')),
            compometidoUsd: tcParseInput(document.getElementById('bloqA-comp')),
            porComprometidoUsd: tcParseInput(document.getElementById('bloqA-porcomp')),
            estimadoTerminoUsd: tcParseInput(document.getElementById('bloqA-eat')),
            tasaCambio: tasa, factor, fechaTasaCambio: fecha,
            empresaId: empBloq || null
        });

        // Guardar asignación de familia (endpoint separado)
        const c = tcContratoById(TC.contratoId);
        const familiaAnterior = c?.familiaId ?? null;
        if (familiaId !== familiaAnterior) {
            await tcPost('AsignarFamilia', { contratoId: TC.contratoId, familiaId: familiaId || null });
        }

        // Actualizar en lista local
        if (c) {
            c.capexUsd           = tcParseInput(document.getElementById('bloqA-capex'));
            c.compometidoUsd     = tcParseInput(document.getElementById('bloqA-comp'));
            c.porComprometidoUsd = tcParseInput(document.getElementById('bloqA-porcomp'));
            c.estimadoTerminoUsd = tcParseInput(document.getElementById('bloqA-eat'));
            c.nombrePaquete      = document.getElementById('bloqA-nombre').value.trim();
            c.tasaCambio         = tasa;
            c.factor             = factor;
            c.fechaTasaCambio    = fecha;
            c.familiaId          = familiaId || null;
            c.familiaNombre      = TC.familias?.find(f => f.id === familiaId)?.nombre ?? null;
            c.empresaId          = empBloq || null;
            c.empresaNombre      = empBloq ? (TC.empresas?.find(e => e.id === empBloq)?.nombre ?? null) : null;
        }
        TC.tasaCambio = tasa;
        // Refrescar conteo de familias en background
        tcGet('FamiliasJson', { proyectoId: TC.proyectoId }).then(fams => { TC.familias = fams; });
        document.getElementById('bloqA-validacion').classList.add('hidden');
        alert('Bloque A guardado.');
    } catch(e) { alert('Error: ' + e.message); }
}

// ════════════════════════════════════════════════════════════════════════════
// BLOQUE B+C — ítems
// ════════════════════════════════════════════════════════════════════════════
async function tcCargarItems() {
    if (!TC.contratoId) return;
    const lista = await tcGet('ItemsJson', { contratoId: TC.contratoId });
    TC.items = lista;
    TC.mcColumnValues = null; // resetear análisis al cambiar de contrato
    tcRenderBloqueB();
}

function tcGetItemsFromDOM() {
    const rows = document.querySelectorAll('#bloqB-tbody tr[data-idx]');
    return Array.from(rows).map((row, i) => {
        const g = id => row.querySelector(`[data-field="${id}"]`)?.value ?? '';
        const gb = id => row.querySelector(`[data-field="${id}"]`)?.checked ?? false;
        return {
            codigoItem:     g('codigo'),
            descripcion:    g('desc'),
            unidad:         g('unidad'),
            subPartida:     g('subpartida'),
            esCerteza:      gb('certeza'),
            esPorComprometer: gb('porComprometer'),
            costoUsd:       tcParseInput(row.querySelector('[data-field="costo"]')),
            costoClpOverride: (function() {
                const el = row.querySelector('[data-field="costoClpOverride"]');
                if (!el || el.dataset.isAuto === 'true') return null;
                const v = tcParseInput(el);
                return isNaN(v) ? null : v;
            })(),
            claseEstimacion: g('clase'),
            consideraciones: g('consider'),
            // D
            minPct:         parseFloat(g('minPct'))        || null,
            minKusd:        parseFloat(g('minKusd'))       || null,
            probablePct:    parseFloat(g('probablePct'))   || 100,
            probableKusd:   parseFloat(g('probableKusd'))  || null,
            maxPct:         parseFloat(g('maxPct'))        || null,
            maxKusd:        parseFloat(g('maxKusd'))       || null,
            viaRiesgo:      gb('viaRiesgo'),
            // E
            oportunidades:  g('opor'),
            amenazas:       g('amen'),
            clase:          parseFloat(g('claseE')) || null,
            peso:           parseFloat(g('pesoE')) || null
        };
    });
}

function tcRenderBloqueB() {
    const tbody = document.getElementById('bloqB-tbody');
    if (!tbody) return;
    tbody.innerHTML = TC.items.length === 0
        ? '<tr><td colspan="12" class="text-center py-6 text-slate-400 text-sm">Sin ítems. Agrega el primer ítem.</td></tr>'
        : TC.items.map((item, i) => tcItemRow(item, i, false)).join('');
    tcValidarBloqueB();
}

// Fórmula: (Costo Total USD / Factor) × Dólar
function tcCalcCosto(costoUsd) {
    const c = TC.contratos?.find(x => x.id === TC.contratoId);
    const factor = parseFloat(c?.factor) || 1;
    const tasa   = parseFloat(c?.tasaCambio) || TC.tasaCambio || 900;
    return factor > 0 ? ((costoUsd || 0) / factor) * tasa : 0;
}

// Llamado cuando el usuario edita manualmente el campo CLP
function tcOnClpOverrideChange(input) {
    tcNumInput(input);
    const row = input.closest('tr');
    const costoUsd = tcParseInput(row.querySelector('[data-field="costo"]'));
    const autoVal  = parseFloat(tcCalcCosto(costoUsd).toFixed(2));
    const userVal  = tcParseNum(input.value);
    const isManual = !isNaN(userVal) && Math.abs(userVal - autoVal) > 0.01;

    input.dataset.isAuto = isManual ? 'false' : 'true';

    if (isManual) {
        input.className = 'tc-input w-full text-right font-semibold bg-amber-50 dark:bg-amber-900/30 border-amber-400 dark:border-amber-600';
        let btn = input.nextElementSibling;
        if (!btn?.classList.contains('clp-reset-btn')) {
            btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'clp-reset-btn flex-shrink-0 text-amber-500 hover:text-red-500';
            btn.title = 'Restablecer cálculo automático';
            btn.innerHTML = '<span class="material-icons text-sm">restart_alt</span>';
            btn.onclick = function() { tcResetClpOverride(this); };
            input.after(btn);
        }
    } else {
        // Si el usuario escribió el mismo valor que auto-calc, vuelve a auto
        input.value = tcFmt(autoVal, 2);
        input.dataset.raw = autoVal;
        input.className = 'tc-input w-full text-right text-slate-500 dark:text-slate-400';
        input.nextElementSibling?.classList.contains('clp-reset-btn') && input.nextElementSibling.remove();
    }
    tcValidarBloqueB();
}

// Llamado cuando cambia "Costo USD$" — actualiza CLP solo si está en modo auto
function tcUpdateClpCalc(costoInput) {
    const row = costoInput.closest('tr');
    const clpInput = row.querySelector('[data-field="costoClpOverride"]');
    if (!clpInput || clpInput.dataset.isAuto !== 'true') return;
    const costoUsd = tcParseInput(costoInput) || 0;
    const calc = tcCalcCosto(costoUsd);
    clpInput.value = tcFmt(calc, 2);
    clpInput.dataset.raw = calc;
}

function tcResetClpOverride(btn) {
    const input = btn.previousElementSibling;
    const row = btn.closest('tr');
    const costoUsd = tcParseInput(row.querySelector('[data-field="costo"]'));
    const calc = tcCalcCosto(costoUsd);
    input.value = tcFmt(calc, 2);
    input.dataset.raw = calc;
    input.dataset.isAuto = 'true';
    input.className = 'tc-input w-full text-right text-slate-500 dark:text-slate-400';
    btn.remove();
    tcValidarBloqueB();
}

/** Campos ocultos D/E compartidos por fila (deben vivir dentro de un &lt;td&gt; válido). */
function tcItemRowHiddenDE(item) {
    return `
        <input type="hidden" data-field="minPct"       value="${item.minPct ?? ''}" />
        <input type="hidden" data-field="minKusd"      value="${item.minKusd ?? ''}" />
        <input type="hidden" data-field="probablePct"  value="${item.probablePct ?? 100}" />
        <input type="hidden" data-field="probableKusd" value="${item.probableKusd ?? item.costoUsd}" />
        <input type="hidden" data-field="maxPct"       value="${item.maxPct ?? ''}" />
        <input type="hidden" data-field="maxKusd"      value="${item.maxKusd ?? ''}" />
        <input type="checkbox" data-field="viaRiesgo" ${item.viaRiesgo ? 'checked' : ''} class="hidden" tabindex="-1" aria-hidden="true" />
        <input type="hidden" data-field="opor"   value="${esc(item.oportunidades || '')}" />
        <input type="hidden" data-field="amen"   value="${esc(item.amenazas || '')}" />
        <input type="hidden" data-field="claseE" value="${item.clase ?? 2}" />`;
}

/** Fila compacta (solo lectura): textos largos sin cajas de input. */
function tcItemRowView(item, i) {
    const costoN    = parseFloat(item.costoUsd) || 0;
    const costoCalc = tcCalcCosto(item.costoUsd);
    const hasOverride = item.costoClpOverride != null;
    const clpRaw    = hasOverride ? Number(item.costoClpOverride) : costoCalc;
    const codigoTxt = (item.codigoItem || '').trim() ? esc(item.codigoItem) : '—';
    const descTxt   = (item.descripcion || '').trim() ? esc(item.descripcion) : '—';
    const subTxt    = (item.subPartida || '').trim() ? esc(item.subPartida) : '—';
    const consTxt   = (item.consideraciones || '').trim() ? esc(item.consideraciones) : '—';
    const claseLbl  = esc(item.claseEstimacion || 'Clase 3');
    return `<tr data-idx="${i}" data-row-edit="0" class="align-middle tc-b-row-view">
        <td class="text-center text-xs font-semibold w-8 px-1 text-teal-600">${i + 1}</td>
        <td class="w-24 text-xs text-slate-600 dark:text-slate-300">
            <input type="hidden" data-field="codigo" value="${esc(item.codigoItem || '')}" />${codigoTxt}
        </td>
        <td class="min-w-[220px] max-w-xl">
            <input type="hidden" data-field="desc" value="${esc(item.descripcion || '')}" />
            <div class="text-xs text-slate-800 dark:text-slate-100 whitespace-normal break-words line-clamp-3" title="${esc(item.descripcion || '')}">${descTxt}</div>
        </td>
        <td class="w-24 text-xs text-slate-600 dark:text-slate-400">
            <input type="hidden" data-field="subpartida" value="${esc(item.subPartida || '')}" />${subTxt}
        </td>
        <td class="w-16 text-xs text-center text-slate-600 dark:text-slate-400">
            <input type="hidden" data-field="unidad" value="${esc(item.unidad || 'USD')}" />${esc(item.unidad || 'USD')}
        </td>
        <td class="w-12 text-center">
            <input type="checkbox" data-field="certeza" ${item.esCerteza ? 'checked' : ''} disabled class="w-4 h-4 accent-teal-600 opacity-70 cursor-not-allowed" />
        </td>
        <td class="w-24 text-center">
            <input type="checkbox" data-field="porComprometer" ${(item.esPorComprometer ?? true) ? 'checked' : ''} disabled class="w-4 h-4 accent-indigo-600 opacity-70 cursor-not-allowed" />
        </td>
        <td class="w-32 text-right">
            <input type="hidden" data-field="costo" data-raw="${costoN}" value="${costoN}" />
            <span class="text-xs font-medium tabular-nums text-slate-800 dark:text-slate-100">$${tcFmt(costoN, 2)}</span>
        </td>
        <td class="w-40 text-right">
            <input type="hidden" data-field="costoClpOverride" data-is-auto="${!hasOverride}" data-raw="${clpRaw}" value="${clpRaw}" />
            <span class="text-xs tabular-nums text-slate-600 dark:text-slate-300">$${tcFmt(clpRaw, 2)}</span>
        </td>
        <td class="w-24 text-xs text-slate-700 dark:text-slate-300">
            <input type="hidden" data-field="clase" value="${esc(item.claseEstimacion || 'Clase 3')}" />${claseLbl}
        </td>
        <td class="min-w-[160px] max-w-xs align-top">
            <input type="hidden" data-field="consider" value="${esc(item.consideraciones || '')}" />
            <div class="text-xs text-slate-600 dark:text-slate-400 whitespace-normal break-words line-clamp-2 mb-0.5" title="${esc(item.consideraciones || '')}">${consTxt}</div>
            ${tcItemRowHiddenDE(item)}
        </td>
        <td class="w-24 text-center align-middle">
            <div class="flex items-center justify-center gap-0.5 flex-wrap">
                <button type="button" class="btn-secondary py-1 px-1.5" onclick="tcBItemSetEdit(this,true)" title="Editar fila">
                    <span class="material-icons text-base">edit</span>
                </button>
                <button type="button" class="btn-danger py-1 px-1.5" onclick="tcRemoveItemRow(this)" title="Eliminar">
                    <span class="material-icons text-sm">delete</span>
                </button>
            </div>
        </td>
    </tr>`;
}

function tcItemRowEdit(item, i) {
    const clases      = ['Clase 1','Clase 2','Clase 3'];
    const costoCalc   = tcCalcCosto(item.costoUsd);
    const hasOverride = item.costoClpOverride != null;
    const clpRaw      = hasOverride ? Number(item.costoClpOverride) : costoCalc;
    const clpVal      = tcFmt(clpRaw, 2);
    const clpCls      = hasOverride
        ? 'tc-input w-full text-right font-semibold bg-amber-50 dark:bg-amber-900/30 border-amber-400 dark:border-amber-600'
        : 'tc-input w-full text-right text-slate-500 dark:text-slate-400';
    const clpTitle    = hasOverride
        ? 'Valor manual (NDC). Haz clic en ↺ para volver al cálculo automático.'
        : 'Calculado: (Costo USD ÷ Factor) × Dólar. Edita para personalizar.';
    return `<tr data-idx="${i}" data-row-edit="1" class="align-middle tc-b-row-edit">
        <td class="text-center text-xs text-slate-400 w-8 px-1">${i + 1}</td>
        <td class="w-24"><input class="tc-input w-full" data-field="codigo" value="${esc(item.codigoItem||'')}" /></td>
        <td class="min-w-[220px]"><input class="tc-input w-full" data-field="desc" value="${esc(item.descripcion||'')}" /></td>
        <td class="w-24"><input class="tc-input w-full" data-field="subpartida" value="${esc(item.subPartida||'')}" /></td>
        <td class="w-16"><input class="tc-input w-full text-center" data-field="unidad" value="${esc(item.unidad||'USD')}" /></td>
        <td class="w-12 text-center">
            <input type="checkbox" data-field="certeza" ${item.esCerteza?'checked':''} class="w-4 h-4 accent-teal-600" onchange="tcValidarBloqueB()" />
        </td>
        <td class="w-24 text-center">
            <input type="checkbox" data-field="porComprometer" ${(item.esPorComprometer ?? true) ? 'checked' : ''} class="w-4 h-4 accent-indigo-600" onchange="tcValidarBloqueB()" />
        </td>
        <td class="w-32"><input class="tc-input w-full text-right" data-field="costo" type="text" inputmode="decimal" value="${tcFmt(item.costoUsd||0, 2)}" data-raw="${item.costoUsd||0}" onfocus="tcNumFocus(this)" onblur="tcNumBlur(this)" oninput="tcNumInput(this);tcValidarBloqueB();tcUpdateClpCalc(this)" /></td>
        <td class="w-40">
            <div class="flex items-center gap-1">
                <input class="${clpCls}" data-field="costoClpOverride" type="text" inputmode="decimal"
                    value="${clpVal}"
                    data-raw="${clpRaw}"
                    data-is-auto="${!hasOverride}"
                    title="${clpTitle}"
                    onfocus="tcNumFocus(this)"
                    onblur="tcNumBlur(this)"
                    oninput="tcNumInput(this);tcOnClpOverrideChange(this)" />
                ${hasOverride ? `<button type="button" class="clp-reset-btn flex-shrink-0 text-amber-500 hover:text-red-500" title="Restablecer cálculo automático" onclick="tcResetClpOverride(this)"><span class="material-icons text-sm">restart_alt</span></button>` : ''}
            </div>
        </td>
        <td class="w-24">
            <select class="tc-select w-full text-xs" data-field="clase">
                ${clases.map(c => `<option ${item.claseEstimacion===c?'selected':''}>${c}</option>`).join('')}
            </select>
        </td>
        <td class="min-w-[160px]">
            <input class="tc-input w-full" data-field="consider" value="${esc(item.consideraciones||'')}" placeholder="Criterio..." />
            ${tcItemRowHiddenDE(item)}
        </td>
        <td class="w-24 text-center align-middle">
            <div class="flex items-center justify-center gap-0.5 flex-wrap">
                <button type="button" class="btn-secondary py-1 px-1.5" onclick="tcBItemSetEdit(this,false)" title="Vista compacta">
                    <span class="material-icons text-base">check</span>
                </button>
                <button type="button" class="btn-danger py-1 px-1.5" onclick="tcRemoveItemRow(this)" title="Eliminar">
                    <span class="material-icons text-sm">delete</span>
                </button>
            </div>
        </td>
    </tr>`;
}

function tcItemRow(item, i, editMode = false) {
    return editMode ? tcItemRowEdit(item, i) : tcItemRowView(item, i);
}

function tcBItemSetEdit(btn, asEdit) {
    const oldTr = btn.closest('tr');
    if (!oldTr || oldTr.dataset.idx === undefined) return;
    const idx = parseInt(oldTr.dataset.idx, 10);
    const items = tcGetItemsFromDOM();
    const item = items[idx];
    if (!item) return;
    const wrap = document.createElement('tbody');
    wrap.innerHTML = tcItemRow(item, idx, asEdit).trim();
    const newTr = wrap.firstElementChild;
    if (newTr) oldTr.replaceWith(newTr);
    tcValidarBloqueB();
}

function tcAddItemRow() {
    const tbody = document.getElementById('bloqB-tbody');
    const emptyRow = tbody.querySelector('td[colspan]');
    if (emptyRow) tbody.innerHTML = '';
    const i = tbody.rows.length;
    const fila = document.createElement('tr');
    fila.dataset.idx = i;
    fila.innerHTML = tcItemRow({ costoUsd: 0, claseEstimacion: 'Clase 3', unidad: 'USD', esPorComprometer: true }, i, true)
        .replace(/<tr[^>]*>/,'').replace(/<\/tr>/,'');
    tbody.appendChild(fila);
    tcValidarBloqueB();
}

function tcRemoveItemRow(btn) {
    btn.closest('tr').remove();
    // Renumerar
    document.querySelectorAll('#bloqB-tbody tr[data-idx]').forEach((r, i) => {
        r.dataset.idx = i;
        r.querySelector('td:first-child').textContent = i + 1;
    });
    tcValidarBloqueB();
}

function tcValidarBloqueB() {
    const rows = document.querySelectorAll('#bloqB-tbody tr[data-idx]');
    let suma = 0;
    let sumaPorComprometer = 0;
    let sumaClpFilas = 0;
    rows.forEach(row => {
        const costoUsd = tcParseInput(row.querySelector('[data-field="costo"]'));
        suma += costoUsd;
        const porComp = row.querySelector('[data-field="porComprometer"]')?.checked ?? false;
        if (porComp) sumaPorComprometer += costoUsd;
        const clpEl = row.querySelector('[data-field="costoClpOverride"]');
        if (clpEl) sumaClpFilas += tcParseInput(clpEl);
    });
    if (TC.activeSection === 'bloqueB') {
        tcSyncBloqueADesdeSumaPorComprometer(sumaPorComprometer);
    }
    const porcomp = tcParseInput(document.getElementById('bloqA-porcomp'));
    document.getElementById('bloqB-suma').textContent = '$' + tcFmt(sumaPorComprometer, 2);
    const sumaTotalEl = document.getElementById('bloqB-suma-total');
    if (sumaTotalEl) sumaTotalEl.textContent = '$' + tcFmt(suma, 2);
    const sumaClpEl = document.getElementById('bloqB-suma-clp');
    if (sumaClpEl) sumaClpEl.textContent = '$' + tcFmt(sumaClpFilas, 0);
    const comp = tcParseInput(document.getElementById('bloqA-comp'));
    const eatEl = document.getElementById('bloqB-eat');
    if (eatEl) eatEl.textContent = '$' + tcFmt(sumaPorComprometer + comp, 2);
    document.getElementById('bloqB-porcomp').textContent = '$' + tcFmt(porcomp, 2);
    const totFoot = document.getElementById('bloqB-total');
    if (totFoot) totFoot.textContent = '$' + tcFmt(suma, 2);

    const badge = document.getElementById('bloqB-diff-badge');
    if (badge && porcomp > 0) {
        const diff = Math.abs(sumaPorComprometer - porcomp);
        if (diff < 0.01) {
            badge.innerHTML = '<span class="badge-verde">✓ Cuadra</span>';
        } else {
            badge.innerHTML = `<span class="badge-rojo">Diferencia: $${tcFmt(diff,2)}</span>`;
        }
    }
}

function tcSumaPorComprometerDesdeItems(items) {
    return (items || []).reduce((acc, item) => {
        if (item?.esPorComprometer ?? true) return acc + (parseFloat(item.costoUsd) || 0);
        return acc;
    }, 0);
}

async function tcGuardarItems() {
    if (!TC.contratoId) { alert('No hay contrato activo.'); return; }
    tcSyncDToItems(); // asegurar que ediciones de D estén en el DOM de B
    tcSyncEToItems(); // asegurar que ediciones de E (opor/amen/clase) estén en el DOM de B
    const items = tcGetItemsFromDOM();
    // Calcular pesos automáticamente para Bloque E
    const totalIncert = items.filter(i => !i.esCerteza && !i.viaRiesgo).reduce((s,i) => s + i.costoUsd, 0);
    items.forEach(i => {
        if (!i.esCerteza && !i.viaRiesgo && totalIncert > 0) {
            i.peso = i.costoUsd / totalIncert;
        }
    });
    try {
        await tcPost('Items', { contratoId: TC.contratoId, items });
        TC.items = items.map((item, idx) => ({ ...item, id: null, orden: idx + 1, probableKusd: item.costoUsd }));

        // Persistir también los agregados de Bloque A para mantener coherencia A ↔ B
        const sumaPorComprometer = tcSumaPorComprometerDesdeItems(items);
        tcSyncBloqueADesdeSumaPorComprometer(sumaPorComprometer);
        if (TC.contratoId) {
            await tcPost('Contrato', {
                id: TC.contratoId,
                proyectoId: TC.proyectoId,
                codigo: '',
                nombrePaquete: document.getElementById('bloqA-nombre')?.value?.trim() || '',
                capexUsd: tcParseInput(document.getElementById('bloqA-capex')),
                compometidoUsd: tcParseInput(document.getElementById('bloqA-comp')),
                porComprometidoUsd: tcParseInput(document.getElementById('bloqA-porcomp')),
                estimadoTerminoUsd: tcParseInput(document.getElementById('bloqA-eat')),
                tasaCambio: parseFloat(document.getElementById('bloqA-tasa')?.value) || 900,
                factor: parseFloat(document.getElementById('bloqA-factor')?.value) || 1,
                fechaTasaCambio: document.getElementById('bloqA-fecha-tasa')?.value || null,
                empresaId: document.getElementById('bloqA-empresa')?.value || null
            });
        }
        alert('Ítems guardados correctamente.');
    } catch(e) { alert('Error: ' + e.message); }
}

// ════════════════════════════════════════════════════════════════════════════
// BLOQUE D — V. de Costo
// ════════════════════════════════════════════════════════════════════════════
function tcRenderBloqueD() {
    const tbody = document.getElementById('bloqD-tbody');
    if (!tbody) return;
    // Usa TC.items (cargado al seleccionar contrato) como fuente canónica.
    // Si el usuario tiene ediciones no guardadas en B, se fusionan desde el DOM.
    const domItems = tcGetItemsFromDOM();
    const allItems = domItems.length ? domItems : (Array.isArray(TC.items) ? TC.items : []);
    if (!allItems.length) {
        tbody.innerHTML = '<tr><td colspan="10" class="text-center py-6 text-slate-400 text-sm">Sin ítems. Agrega ítems en B+C primero.</td></tr>';
        return;
    }
    if (tcDEditingRowIndex !== null && tcDEditingRowIndex >= allItems.length) {
        tcDEditingRowIndex = null;
    }
    tbody.innerHTML = allItems.map((item, i) => {
        const costoUsdNum = parseFloat(item.costoUsd) || 0;
        const costoCell = `<td class="w-32 text-right text-xs tabular-nums font-medium ${item.esCerteza ? 'text-slate-500 dark:text-slate-400' : 'text-slate-800 dark:text-slate-200'}" title="Mismo valor que Costo Total USD$ en Bloque B (fila ${i + 1})">$${tcFmt(costoUsdNum, 2)}</td>`;
        const num = `<td class="text-center text-xs font-semibold w-8 ${item.esCerteza ? 'text-slate-400' : 'text-teal-600'}">${i+1}</td>`;
        const cod = `<td class="w-24 text-xs font-medium ${item.esCerteza ? 'text-slate-400 dark:text-slate-500' : 'text-slate-700 dark:text-slate-300'}">${esc(item.codigoItem||'—')}</td>`;
        const desc = `<td class="min-w-[220px] text-xs ${item.esCerteza ? 'text-slate-400 dark:text-slate-500 italic' : 'text-slate-600 dark:text-slate-400'}">${esc(item.descripcion||'—')}</td>`;
        if (item.esCerteza) {
            return `<tr data-d-idx="${i}" class="bg-slate-50 dark:bg-slate-800/40">
                ${num}${cod}${desc}${costoCell}
                <td colspan="6" class="text-center py-1">
                    <span class="inline-flex items-center gap-1 text-xs text-slate-400 bg-slate-100 dark:bg-slate-700 px-2 py-0.5 rounded-full">
                        <span class="material-icons" style="font-size:12px">lock</span> Ítem de Certeza — sin incertidumbre
                    </span>
                </td>
            </tr>`;
        }
        const probKusd = item.probableKusd ?? item.costoUsd;
        const probPct  = item.probablePct  ?? 100;
        const editing = tcDEditingRowIndex === i;
        if (!editing) {
            const minPctTxt = item.minPct != null && item.minPct !== '' ? `${tcFmt(Number(item.minPct), 2)}%` : '—';
            const minUsdTxt = item.minKusd != null && item.minKusd !== '' ? `$${tcFmt(Number(item.minKusd), 2)}` : '—';
            const probPctTxt = probPct != null && probPct !== '' ? `${tcFmt(Number(probPct), 2)}%` : '—';
            const probUsdTxt = probKusd != null && probKusd !== '' ? `$${tcFmt(Number(probKusd), 2)}` : '—';
            const maxPctTxt = item.maxPct != null && item.maxPct !== '' ? `${tcFmt(Number(item.maxPct), 2)}%` : '—';
            const maxUsdTxt = item.maxKusd != null && item.maxKusd !== '' ? `$${tcFmt(Number(item.maxKusd), 2)}` : '—';
            return `<tr data-d-idx="${i}" data-costo-usd="${costoUsdNum}" class="hover:bg-teal-50/30 dark:hover:bg-teal-900/10">
                ${num}${cod}${desc}${costoCell}
                <td class="w-24 border-l border-slate-200 dark:border-slate-700 text-center text-xs tabular-nums">${minPctTxt}</td>
                <td class="w-36 text-right text-xs tabular-nums">${minUsdTxt}</td>
                <td class="w-24 border-l border-slate-200 dark:border-slate-700 text-center text-xs tabular-nums font-semibold text-teal-700 dark:text-teal-300">${probPctTxt}</td>
                <td class="w-36 text-right text-xs tabular-nums font-semibold">${probUsdTxt}</td>
                <td class="w-24 border-l border-slate-200 dark:border-slate-700 text-center text-xs tabular-nums">${maxPctTxt}</td>
                <td class="w-36">
                    <div class="flex items-center justify-between gap-2">
                        <span class="text-right text-xs tabular-nums flex-1">${maxUsdTxt}</span>
                        <button type="button" class="btn-secondary py-1 px-1.5" onclick="tcDBeginEdit(this)" title="Editar fila">
                            <span class="material-icons text-base align-middle">edit</span>
                        </button>
                    </div>
                </td>
            </tr>`;
        }
        return `<tr data-d-idx="${i}" data-costo-usd="${costoUsdNum}" class="hover:bg-teal-50/30 dark:hover:bg-teal-900/10">
            ${num}${cod}${desc}${costoCell}
            <td class="w-24 border-l border-slate-200 dark:border-slate-700">
                <input class="tc-input w-full text-center" data-field="minPct" type="number" step="0.01"
                    value="${item.minPct??''}" oninput="tcCalcDRow(this,'min')" placeholder="%" />
            </td>
            <td class="w-36">
                <input class="tc-input w-full text-right bg-slate-100 dark:bg-slate-700 text-slate-500 cursor-not-allowed" data-field="minKusd" type="text"
                    value="${item.minKusd != null ? tcFmt(item.minKusd, 2) : ''}" placeholder="—" readonly tabindex="-1" />
            </td>
            <td class="w-24 border-l border-slate-200 dark:border-slate-700">
                <input class="tc-input w-full text-center bg-teal-50 dark:bg-teal-900/20" data-field="probablePct" type="number" step="0.01"
                    value="${probPct}" oninput="tcCalcDRow(this,'probable')" placeholder="%" />
            </td>
            <td class="w-36">
                <input class="tc-input w-full text-right bg-slate-100 dark:bg-slate-700 text-slate-500 cursor-not-allowed font-semibold" data-field="probableKusd" type="text"
                    value="${probKusd != null ? tcFmt(probKusd, 2) : ''}" placeholder="—" readonly tabindex="-1" />
            </td>
            <td class="w-24 border-l border-slate-200 dark:border-slate-700">
                <input class="tc-input w-full text-center" data-field="maxPct" type="number" step="0.01"
                    value="${item.maxPct??''}" oninput="tcCalcDRow(this,'max')" placeholder="%" />
            </td>
            <td class="w-36">
                <div class="flex items-center justify-between gap-2">
                    <input class="tc-input w-full text-right bg-slate-100 dark:bg-slate-700 text-slate-500 cursor-not-allowed" data-field="maxKusd" type="text"
                        value="${item.maxKusd != null ? tcFmt(item.maxKusd, 2) : ''}" placeholder="—" readonly tabindex="-1" />
                    <button type="button" class="btn-secondary py-1 px-1.5" onclick="tcDFinishEdit(this)" title="Bloquear fila">
                        <span class="material-icons text-base align-middle">check</span>
                    </button>
                </div>
            </td>
        </tr>`;
    }).join('');
}

function tcDBeginEdit(btn) {
    const row = btn.closest('tr[data-d-idx]');
    if (!row) return;
    const idx = parseInt(row.dataset.dIdx, 10);
    if (!Number.isFinite(idx)) return;
    if (tcDEditingRowIndex !== null && tcDEditingRowIndex !== idx) {
        tcSyncDToItems();
    }
    tcDEditingRowIndex = idx;
    tcRenderBloqueD();
}

function tcDFinishEdit() {
    tcSyncDToItems();
    tcDEditingRowIndex = null;
    tcRenderBloqueD();
}

function tcCalcDRow(input, tipo) {
    const row      = input.closest('tr');
    const pct      = parseFloat(input.value) || 0;
    const costoUsd = parseFloat(row.dataset.costoUsd) || 0;
    // USD$ = Costo Total USD * % / 100  — para los tres tipos
    const usd   = costoUsd * pct / 100;
    const field = tipo === 'min' ? 'minKusd' : tipo === 'max' ? 'maxKusd' : 'probableKusd';
    const el    = row.querySelector(`[data-field="${field}"]`);
    if (el) el.value = tcFmt(usd, 2);
}

function tcSyncDToItems() {
    const items = tcGetItemsFromDOM();
    document.querySelectorAll('#bloqD-tbody tr[data-d-idx]').forEach(dr => {
        const idx = parseInt(dr.dataset.dIdx);
        if (idx >= items.length || items[idx].esCerteza) return;
        // En vista lectura no hay inputs de edición: no sobreescribir valores.
        if (!dr.querySelector('[data-field="minPct"]')) return;
        const g  = f => dr.querySelector(`[data-field="${f}"]`)?.value;
        const gb = f => dr.querySelector(`[data-field="${f}"]`)?.checked;
        items[idx].minPct      = parseFloat(g('minPct'))       || null;
        items[idx].minKusd     = tcParseNum(g('minKusd'))      || null;
        items[idx].probablePct = parseFloat(g('probablePct'))  || 100;
        items[idx].probableKusd= tcParseNum(g('probableKusd')) || null;
        items[idx].maxPct      = parseFloat(g('maxPct'))       || null;
        items[idx].maxKusd     = tcParseNum(g('maxKusd'))      || null;
        items[idx].viaRiesgo   = gb('viaRiesgo') || false;
        // Escribir en inputs ocultos de la fila B
        const bRow = document.querySelectorAll('#bloqB-tbody tr[data-idx]')[idx];
        if (bRow) {
            ['minPct','minKusd','probablePct','probableKusd','maxPct','maxKusd'].forEach(f => {
                const el = bRow.querySelector(`[data-field="${f}"]`);
                if (el) el.value = items[idx][f] ?? '';
            });
            const vr = bRow.querySelector('[data-field="viaRiesgo"]');
            if (vr) vr.checked = items[idx].viaRiesgo;
        }
    });
}

// ════════════════════════════════════════════════════════════════════════════
// BLOQUE E — factores
// ════════════════════════════════════════════════════════════════════════════
function tcRenderBloqueE() {
    const tbody = document.getElementById('bloqE-tbody');
    if (!tbody) return;
    const domRows = tcGetItemsFromDOM();
    const rows = domRows.length ? domRows : (Array.isArray(TC.items) ? TC.items : []);
    const incertMapped = rows
        .map((item, bIdx) => ({ ...item, bIdx }))
        .filter(item => !item.esCerteza && !item.viaRiesgo);

    if (!incertMapped.length) {
        tbody.innerHTML = '<tr><td colspan="5" class="text-center py-6 text-slate-400 text-sm">Sin ítems de incertidumbre (no vía riesgo).</td></tr>';
        return;
    }
    if (tcEEditingRowBIdx !== null && !incertMapped.some(item => item.bIdx === tcEEditingRowBIdx)) {
        tcEEditingRowBIdx = null;
    }

    tbody.innerHTML = incertMapped.map((item, i) => {
        const editing = tcEEditingRowBIdx === item.bIdx;
        if (!editing) {
            const oporTxt = (item.oportunidades || '').trim();
            const amenTxt = (item.amenazas || '').trim();
            return `<tr data-e-idx="${i}" data-b-idx="${item.bIdx}">
            <td class="text-xs font-medium text-slate-700 dark:text-slate-300 whitespace-nowrap">${esc(item.codigoItem||'—')}</td>
            <td class="text-xs text-slate-600 dark:text-slate-400 whitespace-nowrap">${esc(item.descripcion||'—')}</td>
            <td class="text-xs text-slate-600 dark:text-slate-400">${oporTxt ? esc(oporTxt) : '<span class="text-slate-400">—</span>'}</td>
            <td class="text-xs text-slate-600 dark:text-slate-400">${amenTxt ? esc(amenTxt) : '<span class="text-slate-400">—</span>'}</td>
            <td class="text-center">
                <button type="button" class="btn-secondary py-1 px-1.5" onclick="tcEBeginEdit(this)" title="Editar fila">
                    <span class="material-icons text-base align-middle">edit</span>
                </button>
            </td>
        </tr>`;
        }
        return `<tr data-e-idx="${i}" data-b-idx="${item.bIdx}">
            <td class="text-xs font-medium text-slate-700 dark:text-slate-300 whitespace-nowrap">${esc(item.codigoItem||'—')}</td>
            <td class="text-xs text-slate-600 dark:text-slate-400 whitespace-nowrap">${esc(item.descripcion||'—')}</td>
            <td class="w-full"><input class="tc-input w-full" data-field="opor" value="${esc(item.oportunidades||'')}" placeholder="Factores que reducen costo..." /></td>
            <td class="w-full"><input class="tc-input w-full" data-field="amen" value="${esc(item.amenazas||'')}" placeholder="Factores que aumentan costo..." /></td>
            <td class="text-center">
                <button type="button" class="btn-secondary py-1 px-1.5" onclick="tcEFinishEdit()" title="Bloquear fila">
                    <span class="material-icons text-base align-middle">check</span>
                </button>
            </td>
        </tr>`;
    }).join('');
}

function tcEBeginEdit(btn) {
    const row = btn.closest('tr[data-b-idx]');
    if (!row) return;
    const bIdx = parseInt(row.dataset.bIdx, 10);
    if (!Number.isFinite(bIdx)) return;
    if (tcEEditingRowBIdx !== null && tcEEditingRowBIdx !== bIdx) {
        tcSyncEToItems();
    }
    tcEEditingRowBIdx = bIdx;
    tcRenderBloqueE();
}

function tcEFinishEdit() {
    tcSyncEToItems();
    tcEEditingRowBIdx = null;
    tcRenderBloqueE();
}


function tcSyncEToItems() {
    const bRows = document.querySelectorAll('#bloqB-tbody tr[data-idx]');
    document.querySelectorAll('#bloqE-tbody tr[data-e-idx]').forEach(er => {
        const bIdx = parseInt(er.dataset.bIdx);
        const bRow = bRows[bIdx];
        if (!bRow) return;
        if (!er.querySelector('[data-field="opor"]')) return;
        const fields = ['opor', 'amen'];
        fields.forEach(f => {
            const src = er.querySelector(`[data-field="${f}"]`);
            const dst = bRow.querySelector(`[data-field="${f}"]`);
            if (src && dst) dst.value = src.value;
        });
    });
}

// ════════════════════════════════════════════════════════════════════════════
// BLOQUE F — clasificación (readonly)
// ════════════════════════════════════════════════════════════════════════════
function tcRenderBloqueF() {
    const domRows = tcGetItemsFromDOM();
    const rows = domRows.length ? domRows : (Array.isArray(TC.items) ? TC.items : []);
    const incert = rows.filter(i => !i.esCerteza);
    const cert   = rows.filter(i => i.esCerteza);

    const fmtPct  = v => (v != null && v !== '') ? tcFmt(parseFloat(v), 2) + '%' : '—';
    const fmtKusd = v => (v != null && v !== '') ? tcFmt(parseFloat(v), 2) : '—';

    const mcArr = Array.isArray(TC.mcColumnValues) ? TC.mcColumnValues : [];
    const lenOk = mcArr.length === incert.length;
    const mcSumNum = mcArr.reduce((s, v) => s + (typeof v === 'number' && !Number.isNaN(v) ? v : 0), 0);
    const hasMcNumeric = lenOk && mcArr.some(v => typeof v === 'number' && !Number.isNaN(v));

    // Columna Incertidumbre siempre presente — valor MC o "—" si aún no calculado
    const incertRows = incert.map((i, idx) => {
        const mcVal  = lenOk ? mcArr[idx] : null;
        const mcDisp = mcVal != null ? fmtKusd(mcVal) : '—';
        return `<tr>
        <td class="text-xs font-medium text-slate-700 dark:text-slate-300">${esc(i.codigoItem||'—')}</td>
        <td class="text-xs text-slate-600 dark:text-slate-400">${esc(i.descripcion||'—')}</td>
        <td class="text-right text-xs font-semibold">${tcFmtUsd(i.costoUsd)}</td>
        <td class="text-center text-xs border-l border-slate-200 dark:border-slate-700 bg-blue-50 dark:bg-blue-900/10">${fmtPct(i.minPct)}</td>
        <td class="text-right text-xs bg-blue-50 dark:bg-blue-900/10">${fmtKusd(i.minKusd)}</td>
        <td class="text-center text-xs border-l border-slate-200 dark:border-slate-700 bg-teal-50 dark:bg-teal-900/10 font-semibold">${fmtPct(i.probablePct)}</td>
        <td class="text-right text-xs bg-teal-50 dark:bg-teal-900/10 font-semibold">${fmtKusd(i.probableKusd)}</td>
        <td class="text-center text-xs border-l border-slate-200 dark:border-slate-700 bg-amber-50 dark:bg-amber-900/10">${fmtPct(i.maxPct)}</td>
        <td class="text-right text-xs bg-amber-50 dark:bg-amber-900/10">${fmtKusd(i.maxKusd)}</td>
        <td class="text-right text-xs font-bold px-2 py-1 bg-purple-50 dark:bg-purple-900/10 text-purple-700 dark:text-purple-300 border-l-2 border-purple-400">${mcDisp}</td>
    </tr>`;
    }).join('');

    const certRows = cert.map(i => `<tr>
        <td class="text-xs font-medium text-slate-700 dark:text-slate-300">${esc(i.codigoItem||'—')}</td>
        <td class="text-xs text-slate-600 dark:text-slate-400">${esc(i.descripcion||'—')}</td>
        <td class="text-right text-xs font-semibold">${tcFmtUsd(i.costoUsd)}</td>
    </tr>`).join('');

    const incertBody = document.getElementById('bloqF-incert');
    const certBody   = document.getElementById('bloqF-cert');
    if (incertBody) incertBody.innerHTML = incertRows || '<tr><td colspan="10" class="text-center py-4 text-slate-400 text-xs">Sin ítems de incertidumbre.</td></tr>';
    if (certBody)   certBody.innerHTML   = certRows   || '<tr><td colspan="3"  class="text-center py-4 text-slate-400 text-xs">Sin ítems de certeza.</td></tr>';

    const tIncert = incert.reduce((s,i) => s + i.costoUsd, 0);
    const tCert   = cert.reduce((s,i)   => s + i.costoUsd, 0);
    const ti  = document.getElementById('bloqF-total-incert');
    const tc2 = document.getElementById('bloqF-total-cert');
    if (ti)  ti.textContent  = tcFmtUsd(tIncert);
    if (tc2) tc2.textContent = tcFmtUsd(tCert);

    // Total columna Incertidumbre en tfoot (siempre visible)
    const mcTotalEl = document.getElementById('bloqF-total-mc');
    if (mcTotalEl) {
        mcTotalEl.textContent = hasMcNumeric ? fmtKusd(mcSumNum) : '—';
    }
}

// ════════════════════════════════════════════════════════════════════════════
// ANÁLISIS MC LOCAL — BLOQUE F  (@RiskTriang  columna por ítem + KPI)
// ════════════════════════════════════════════════════════════════════════════

/**
 * Simulación Monte Carlo con distribución triangular.
 * 10.000 iteraciones × N ítems.
 *
 * En cada iteración:
 *   1. Para cada ítem genera U ~ Uniform(0,1)
 *   2. Aplica la CDF inversa triangular:
 *        Fc = (Probable - Mínimo) / (Máximo - Mínimo)
 *        Si U < Fc  → X = Mínimo + √(U × (Máx-Mín) × (Prob-Mín))
 *        Si U ≥ Fc  → X = Máximo - √((1-U) × (Máx-Mín) × (Máx-Prob))
 *   3. Suma los valores de todos los ítems → total de la iteración
 * Al final analiza la distribución de los 10.000 totales.
 *
 * Retorna:
 *   itemMeans  — media MC por ítem (KUS$), se muestra en columna RiskTriang
 *   p10/p50/p80/p90 — percentiles del total acumulado (KUS$)
 *   mean / stdDev   — media y desviación del total
 */
function tcRunMCSimulation(incertItems, iteraciones) {
    const N    = incertItems.length;
    const ITER = iteraciones || 10000;

    // Acumuladores por ítem (para calcular media por ítem)
    const itemSums = new Float64Array(N);
    // Total por iteración (para distribución)
    const totals   = new Float64Array(ITER);

    // Pre-calcular parámetros en USD$ para el loop
    const params = incertItems.map(i => ({
        a: i.minKusd    != null ? i.minKusd    : i.costoUsd * 0.8,   // Mínimo  USD$
        c: i.probableKusd != null ? i.probableKusd : i.costoUsd,      // Probable USD$
        b: i.maxKusd    != null ? i.maxKusd    : i.costoUsd * 1.2    // Máximo  USD$
    }));

    for (let iter = 0; iter < ITER; iter++) {
        let total = 0;
        for (let i = 0; i < N; i++) {
            let { a, c, b } = params[i];

            let x;
            if (b <= a) {
                // Rango degenerado: usar valor probable directamente
                x = c;
            } else {
                // Clamp moda al rango válido
                c = c < a ? a : c > b ? b : c;
                const U  = Math.random();
                const Fc = (c - a) / (b - a);
                x = U < Fc
                    ? a + Math.sqrt(U  * (b - a) * (c - a))
                    : b - Math.sqrt((1 - U) * (b - a) * (b - c));
            }

            itemSums[i] += x;
            total       += x;
        }
        totals[iter] = total;
    }

    // Media y desviación antes de ordenar
    let sum = 0, sumSq = 0;
    for (let i = 0; i < ITER; i++) { sum += totals[i]; sumSq += totals[i] * totals[i]; }
    const mean   = sum / ITER;
    const stdDev = Math.sqrt(sumSq / ITER - mean * mean);

    // Ordenar para percentiles
    totals.sort();

    const pct = p => {
        const idx = (p / 100) * (ITER - 1);
        const lo  = idx | 0;          // Math.floor más rápido
        const hi  = lo + 1;
        if (hi >= ITER) return totals[lo];
        return totals[lo] + (idx - lo) * (totals[hi] - totals[lo]);
    };

    const p10v = pct(10), p50v = pct(50), p80v = pct(80), p90v = pct(90);

    // Histograma: 40 bins entre mín y máx
    const N_BINS  = 40;
    const minVal  = totals[0];
    const maxVal  = totals[ITER - 1];
    const binW    = (maxVal - minVal) / N_BINS || 1;
    const histCounts = new Array(N_BINS).fill(0);
    for (let i = 0; i < ITER; i++) {
        const b = Math.min(Math.floor((totals[i] - minVal) / binW), N_BINS - 1);
        histCounts[b]++;
    }
    const histLabels = Array.from({length: N_BINS}, (_, i) => minVal + (i + 0.5) * binW);

    // CDF: 101 puntos (0% … 100%)
    const cdfPoints = Array.from({length: 101}, (_, i) => ({ x: pct(i), y: i }));

    return {
        itemMeans: Array.from(itemSums).map(s => s / ITER),
        p10: p10v, p50: p50v, p80: p80v, p90: p90v,
        mean, stdDev, iterations: ITER,
        histLabels, histCounts, cdfPoints
    };
}

/** Misma regla que Resumen Global / CRA: incertidumbre triangular en contrato, sin vía riesgo, solo «Por comprometer». */
function tcItemInMcLocalSim(i) {
    return !!(i && !i.esCerteza && !i.viaRiesgo && (i.esPorComprometer ?? true));
}

/**
 * Restaura medias por fila alineadas con todas las filas incertas de Bloque F.
 * Compat: guardados antiguos = un número por cada fila !esCerteza (sin nulls).
 */
function tcMapSavedMcMeansToIncertAll(incertAll, savedMeans) {
    if (!Array.isArray(savedMeans) || !savedMeans.length || !incertAll.length) return null;
    const mcRows = incertAll.filter(tcItemInMcLocalSim);
    const nums = savedMeans.filter(v => typeof v === 'number' && !Number.isNaN(v));
    if (nums.length === mcRows.length) {
        let k = 0;
        return incertAll.map(row => (tcItemInMcLocalSim(row) ? nums[k++] : null));
    }
    if (savedMeans.length === incertAll.length) {
        return savedMeans.map(v => (typeof v === 'number' && !Number.isNaN(v) ? v : null));
    }
    return null;
}

// Carga resultado MC guardado en DB; si no existe, corre la simulación automáticamente.
async function tcCargarMcResultado() {
    if (!TC.contratoId) { tcGenerarAnalisisMC(); return; }
    try {
        const data = await tcGet('McResultadoJson', { contratoId: TC.contratoId });
        if (data && data.itemMediasJson) {
            const rows = tcGetItemsFromDOM();
            const incertAll = rows.filter(i => !i.esCerteza);
            const rawMeans = JSON.parse(data.itemMediasJson);
            const mapped = tcMapSavedMcMeansToIncertAll(incertAll, rawMeans);
            TC.mcColumnValues = mapped || rawMeans;
            const histData = JSON.parse(data.histogramaJson || '{}');
            const cdfData  = JSON.parse(data.cdfJson  || '[]');
            const result = {
                itemMeans:  TC.mcColumnValues,
                p10: data.p10, p50: data.p50, p80: data.p80, p90: data.p90,
                mean: data.media, stdDev: data.desvStd, iterations: data.iteraciones,
                histLabels: histData.labels || [],
                histCounts: histData.counts || [],
                cdfPoints:  cdfData
            };
            tcRenderBloqueF();
            const cert = rows.filter(i => i.esCerteza && (i.esPorComprometer ?? true));
            const certTotalUsd = cert.reduce((s, i) => s + i.costoUsd, 0);
            const compUsd = tcParseInput(document.getElementById('bloqA-comp'));
            const mcUsd = (TC.mcColumnValues || []).reduce((s, v) => s + (typeof v === 'number' ? v : 0), 0);
            const nMc = incertAll.filter(tcItemInMcLocalSim).length;
            tcMostrarKpiF(result, mcUsd, certTotalUsd, compUsd, nMc);
            // Marcar botón guardar como ya guardado (datos vienen de DB)
            tcSetSaveBtnSaved(data.fechaCalculo);
            const btn = document.getElementById('bloqF-gen-btn');
            if (btn && data.fechaCalculo) btn.title = `Último cálculo: ${data.fechaCalculo}`;
        } else {
            tcGenerarAnalisisMC();
        }
    } catch {
        tcGenerarAnalisisMC();
    }
}

function tcGenerarAnalisisMC() {
    const rows      = tcGetItemsFromDOM();
    const incertAll = rows.filter(i => !i.esCerteza);
    const incertMc  = incertAll.filter(tcItemInMcLocalSim);
    const cert      = rows.filter(i => i.esCerteza && (i.esPorComprometer ?? true));

    if (!incertMc.length) return;   // nada que simular — la columna ya muestra "—"

    const btn     = document.getElementById('bloqF-gen-btn');
    const loading = document.getElementById('bloqF-gen-loading');
    if (btn)     btn.disabled = true;
    if (loading) loading.classList.remove('hidden');
    // Resetear botón guardar al estado "pendiente" mientras se calcula
    const saveBtn = document.getElementById('bloqF-save-btn');
    if (saveBtn) { saveBtn.classList.add('hidden'); saveBtn.disabled = false; }

    // Diferir la ejecución un frame para que el spinner se pinte primero
    requestAnimationFrame(() => setTimeout(() => {
        try {
            const result = tcRunMCSimulation(incertMc, 10000);

            let k = 0;
            TC.mcColumnValues = incertAll.map(i => (tcItemInMcLocalSim(i) ? result.itemMeans[k++] : null));

            // Re-renderizar tabla con la nueva columna
            tcRenderBloqueF();

            // KPI
            const mcTotalUsd   = result.itemMeans.reduce((s, v) => s + v, 0);
            const certTotalUsd = cert.reduce((s, i) => s + i.costoUsd, 0);
            const compUsd      = tcParseInput(document.getElementById('bloqA-comp'));
            tcMostrarKpiF(result, mcTotalUsd, certTotalUsd, compUsd, incertMc.length);

            // Guardar referencia al resultado actual para el botón Guardar
            TC.mcLastResult = { ...result, itemMeans: TC.mcColumnValues };
        } finally {
            if (btn)     btn.disabled = false;
            if (loading) loading.classList.add('hidden');
        }
    }, 20));
}

function tcMostrarKpiF(result, mcKusd, certKusd, compUsd, nItems) {
    const fK = v => tcFmt(v, 2) + ' USD$';
    const set = (id, val) => { const el = document.getElementById(id); if (el) el.textContent = val; };
    const base = (certKusd || 0) + (compUsd || 0);

    set('bloqF-kpi-eat',   fK(result.p50 + base));
    set('bloqF-kpi-mc',    fK(mcKusd));
    set('bloqF-kpi-cert',  fK(certKusd));
    set('bloqF-kpi-items', `${nItems} ítem${nItems !== 1 ? 's' : ''} · ${tcFmt(result.iterations)} iteraciones`);
    set('bloqF-kpi-p10',   fK(result.p10 + base));
    set('bloqF-kpi-p50',   fK(result.p50 + base));
    set('bloqF-kpi-p80',   fK(result.p80 + base));
    set('bloqF-kpi-p90',   fK(result.p90 + base));

    document.getElementById('bloqF-kpi').classList.remove('hidden');
    document.getElementById('bloqF-pdf-btn')?.classList.remove('hidden');

    // Mostrar botón guardar en estado "pendiente de guardar"
    const saveBtn = document.getElementById('bloqF-save-btn');
    if (saveBtn) {
        saveBtn.classList.remove('hidden');
        saveBtn.disabled = false;
        saveBtn.innerHTML = '<span class="material-icons text-base">save</span> Guardar Análisis';
    }

    tcRenderChartsF(result, base);
}

function tcSetSaveBtnSaved(fechaCalculo) {
    const saveBtn = document.getElementById('bloqF-save-btn');
    if (!saveBtn) return;
    saveBtn.classList.remove('hidden');
    saveBtn.disabled = true;
    const fecha = fechaCalculo ? ` (${fechaCalculo})` : '';
    saveBtn.innerHTML = `<span class="material-icons text-base">check_circle</span> Guardado${fecha}`;
}

async function tcGuardarMcManual() {
    if (!TC.contratoId) return;
    const saveBtn = document.getElementById('bloqF-save-btn');
    if (!TC.mcLastResult && !TC.mcColumnValues?.length) {
        alert('Primero genera el análisis Monte Carlo.'); return;
    }
    const result = TC.mcLastResult;
    if (saveBtn) { saveBtn.disabled = true; saveBtn.innerHTML = '<span class="material-icons text-base">hourglass_empty</span> Guardando…'; }
    try {
        const r = await tcPost('GuardarMcResultado', {
            contratoId:     TC.contratoId,
            p10:            result.p10,
            p50:            result.p50,
            p80:            result.p80,
            p90:            result.p90,
            media:          result.mean,
            desvStd:        result.stdDev,
            iteraciones:    result.iterations,
            itemMediasJson: JSON.stringify(TC.mcColumnValues || result.itemMeans),
            histogramaJson: JSON.stringify({ labels: result.histLabels, counts: result.histCounts }),
            cdfJson:        JSON.stringify(result.cdfPoints)
        });
        tcSetSaveBtnSaved(r?.fechaCalculo || '');
        const genBtn = document.getElementById('bloqF-gen-btn');
        if (genBtn && r?.fechaCalculo) genBtn.title = `Guardado: ${r.fechaCalculo}`;
    } catch(e) {
        if (saveBtn) { saveBtn.disabled = false; saveBtn.innerHTML = '<span class="material-icons text-base">save</span> Guardar Análisis'; }
        alert('Error al guardar: ' + e.message);
    }
}

function tcAbrirPdf() {
    if (!TC.contratoId) return;
    window.open(`/TallerCostosPdf/${TC.contratoId}`, '_blank');
}

function tcAbrirPdfRiesgos() {
    const pid = TC.proyectoId || window._risksTcId || null;
    if (!pid) { alert('Selecciona un proyecto primero.'); return; }
    window.open(`/TallerCostosPdfRiesgos/${pid}`, '_blank');
}

function tcAbrirPdfResumen() {
    const sel = document.getElementById('tc-resumen-sel-proy');
    const pid = sel?.value || TC.proyectoId;
    if (!pid) { alert('Selecciona un proyecto en el Resumen Global primero.'); return; }
    window.open(`/TallerCostosPdfResumen/${pid}`, '_blank');
}

/** Informe ejecutivo compacto (marca CODELCO): estado + recomendaciones dinámicas; imprimir a PDF desde el navegador. */
function tcAbrirPdfEjecutivoCod() {
    const sel = document.getElementById('tc-resumen-sel-proy');
    const pid = sel?.value || TC.proyectoId;
    if (!pid) { alert('Selecciona un proyecto en el Resumen Global primero.'); return; }
    window.open(`/TallerCostosPdfEjecutivo/${pid}`, '_blank');
}

// Instancias de gráficos (para destruir antes de recrear)
let _tcChartHist = null;
let _tcChartCdf  = null;

function tcRenderChartsF(result, baseKusd) {
    if (!window.Chart) return;

    if (_tcChartHist) { _tcChartHist.destroy(); _tcChartHist = null; }
    if (_tcChartCdf)  { _tcChartCdf.destroy();  _tcChartCdf  = null; }

    const { p10, p50, p80, p90, histLabels, histCounts, cdfPoints } = result;
    const base = baseKusd || 0;
    const fmt2 = v => tcFmt(v + base, 0);

    // ── Plugin inline: líneas verticales de percentiles ──────────────────────
    const vLines = {
        id: 'tcVLines',
        afterDraw(chart) {
            const { ctx, chartArea: { top, bottom }, scales: { x } } = chart;
            const lines = [
                { val: p10, color: '#3b82f6', label: 'P10' },
                { val: p50, color: '#10b981', label: 'P50' },
                { val: p80, color: '#f59e0b', label: 'P80' },
                { val: p90, color: '#ef4444', label: 'P90' }
            ];
            lines.forEach(({ val, color, label }) => {
                const px = x.getPixelForValue(val);
                ctx.save();
                ctx.beginPath();
                ctx.moveTo(px, top);
                ctx.lineTo(px, bottom);
                ctx.strokeStyle = color;
                ctx.lineWidth = 2;
                ctx.setLineDash([6, 3]);
                ctx.stroke();
                ctx.fillStyle = color;
                ctx.font = 'bold 10px sans-serif';
                ctx.fillText(label, px + 3, top + 12);
                ctx.restore();
            });
        }
    };

    // ── Colores de barras del histograma por zona ─────────────────────────────
    const barColors = histLabels.map(v =>
        v < p10 ? 'rgba(59,130,246,0.7)'  :   // azul  < P10
        v < p50 ? 'rgba(16,185,129,0.7)'  :   // verde P10-P50
        v < p80 ? 'rgba(245,158,11,0.7)'  :   // ámbar P50-P80
        v < p90 ? 'rgba(249,115,22,0.7)'  :   // naranja P80-P90
                  'rgba(239,68,68,0.7)'        // rojo  > P90
    );

    // ── HISTOGRAMA ────────────────────────────────────────────────────────────
    const histCanvas = document.getElementById('bloqF-chart-hist');
    if (histCanvas) {
        _tcChartHist = new Chart(histCanvas, {
            type: 'bar',
            plugins: [vLines],
            data: {
                labels: histLabels,
                datasets: [{
                    label: 'Frecuencia',
                    data: histCounts,
                    backgroundColor: barColors,
                    borderWidth: 0,
                    barPercentage: 1.0,
                    categoryPercentage: 1.0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            title: items => fmt2(items[0].parsed.x) + ' USD$',
                            label: item  => `${item.parsed.y} iteraciones`
                        }
                    }
                },
                scales: {
                    x: {
                        type: 'linear',
                        ticks: {
                            maxTicksLimit: 6,
                            callback: v => tcFmt(v + base, 0)
                        },
                        title: { display: true, text: 'EAT Total (USD$)', font: { size: 10 } }
                    },
                    y: {
                        title: { display: true, text: 'Iteraciones', font: { size: 10 } },
                        ticks: { maxTicksLimit: 5 }
                    }
                }
            }
        });
    }

    // ── CURVA S (CDF) ─────────────────────────────────────────────────────────
    const cdfCanvas = document.getElementById('bloqF-chart-cdf');
    if (cdfCanvas) {
        // Puntos especiales P10/P50/P80/P90 marcados como scatter overlay
        const specialPts = [
            { x: p10, y: 10,  r: 5, color: '#3b82f6', label: 'P10' },
            { x: p50, y: 50,  r: 5, color: '#10b981', label: 'P50' },
            { x: p80, y: 80,  r: 5, color: '#f59e0b', label: 'P80' },
            { x: p90, y: 90,  r: 5, color: '#ef4444', label: 'P90' }
        ];

        // Plugin líneas horizontales para CDF
        const hLines = {
            id: 'tcHLines',
            afterDraw(chart) {
                const { ctx, chartArea: { left, right }, scales: { y } } = chart;
                specialPts.forEach(({ y: yv, color, label }) => {
                    const py = y.getPixelForValue(yv);
                    ctx.save();
                    ctx.beginPath();
                    ctx.moveTo(left, py);
                    ctx.lineTo(right, py);
                    ctx.strokeStyle = color;
                    ctx.lineWidth = 1;
                    ctx.setLineDash([4, 3]);
                    ctx.stroke();
                    ctx.fillStyle = color;
                    ctx.font = 'bold 9px sans-serif';
                    ctx.fillText(label, left + 2, py - 3);
                    ctx.restore();
                });
            }
        };

        _tcChartCdf = new Chart(cdfCanvas, {
            type: 'line',
            plugins: [hLines],
            data: {
                datasets: [
                    {
                        label: 'Probabilidad acumulada',
                        data: cdfPoints,
                        borderColor: '#7c3aed',
                        backgroundColor: 'rgba(124,58,237,0.08)',
                        borderWidth: 2,
                        pointRadius: 0,
                        fill: true,
                        tension: 0.3
                    },
                    {
                        label: 'Percentiles',
                        data: specialPts.map(p => ({ x: p.x, y: p.y })),
                        type: 'scatter',
                        pointRadius: 5,
                        pointBackgroundColor: specialPts.map(p => p.color),
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2,
                        showLine: false
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            title: items => tcFmt(items[0].parsed.x + base, 0) + ' USD$',
                            label: item  => `Prob. acum.: ${tcFmt(item.parsed.y, 1)}%`
                        }
                    }
                },
                scales: {
                    x: {
                        type: 'linear',
                        ticks: {
                            maxTicksLimit: 6,
                            callback: v => tcFmt(v + base, 0)
                        },
                        title: { display: true, text: 'EAT Total (USD$)', font: { size: 10 } }
                    },
                    y: {
                        min: 0, max: 100,
                        ticks: { callback: v => v + '%', maxTicksLimit: 6 },
                        title: { display: true, text: 'Probabilidad (%)', font: { size: 10 } }
                    }
                }
            }
        });
    }
}

// ════════════════════════════════════════════════════════════════════════════
// SIMULACIÓN MC LOCAL — BLOQUE F  (endpoint backend, se mantiene para uso interno)
// ════════════════════════════════════════════════════════════════════════════
async function tcSimularClasificacion() {
    const rows   = tcGetItemsFromDOM();
    const incert = rows.filter(i => !i.esCerteza);

    if (!incert.length) {
        alert('No hay ítems de incertidumbre para simular.');
        return;
    }

    // Construir lista con valores en USD$
    const items = incert.map(i => {
        const prob = i.costoUsd || 0;
        return {
            minimo:   i.minKusd   != null ? i.minKusd   : prob * 0.8,
            probable: prob,
            maximo:   i.maxKusd   != null ? i.maxKusd   : prob * 1.2,
            codigo:   i.codigoItem || ''
        };
    });

    const btn     = document.getElementById('bloqF-sim-btn');
    const loading = document.getElementById('bloqF-sim-loading');
    const resPanel = document.getElementById('bloqF-sim-resultados');

    if (btn)     btn.disabled = true;
    if (loading) loading.classList.remove('hidden');
    if (resPanel) resPanel.classList.add('hidden');

    try {
        const data = await tcPost('SimularClasificacion', { items, iteraciones: 10000 });
        if (data.error) { alert(data.error); return; }
        tcMostrarResultadosF(data);
    } catch(e) {
        alert('Error al simular: ' + e.message);
    } finally {
        if (btn)     btn.disabled = false;
        if (loading) loading.classList.add('hidden');
    }
}

function tcMostrarResultadosF(data) {
    const fUsd = v => '$' + tcFmt(v, 0) + ' USD';
    document.getElementById('bloqF-p10').textContent  = fUsd(data.percentil10);
    document.getElementById('bloqF-p50').textContent  = fUsd(data.percentil50);
    document.getElementById('bloqF-p90').textContent  = fUsd(data.percentil90);
    document.getElementById('bloqF-iter').textContent  = tcFmt(data.iteraciones);
    document.getElementById('bloqF-media').textContent = fUsd(data.media);
    document.getElementById('bloqF-desv').textContent  = fUsd(data.desviacion);
    document.getElementById('bloqF-sim-resultados').classList.remove('hidden');
}

// ════════════════════════════════════════════════════════════════════════════
// RIESGOS
// ════════════════════════════════════════════════════════════════════════════
async function tcRiesgosCargarProyectos() {
    try {
        const lista = await tcGet('ProyectosJson');
        const sel = document.getElementById('tc-riesgos-proy-sel');
        if (!sel) return;
        sel.innerHTML = '<option value="">— Selecciona un proyecto para continuar —</option>'
            + lista.map(p => `<option value="${p.id}">${esc(p.codigo)} — ${esc(p.nombre)}</option>`).join('');
        // Pre-seleccionar el proyecto activo si ya hay uno
        if (TC.proyectoId) {
            sel.value = TC.proyectoId;
            await tcRiesgosSeleccionarProyecto(TC.proyectoId);
        } else {
            document.getElementById('tc-riesgos-sin-proyecto')?.classList.remove('hidden');
            document.getElementById('tc-riesgos-body')?.classList.add('hidden');
        }
    } catch(e) { console.error(e); }
}

async function tcRiesgosSeleccionarProyecto(id) {
    const sinProy = document.getElementById('tc-riesgos-sin-proyecto');
    const body    = document.getElementById('tc-riesgos-body');
    if (!id) {
        sinProy?.classList.remove('hidden');
        body?.classList.add('hidden');
        return;
    }
    sinProy?.classList.add('hidden');
    body?.classList.remove('hidden');
    // Si cambió el proyecto, resetear estado de revisión/riesgos y cargar en TC
    if (TC.proyectoId !== id) {
        TC.revisionId = null;
        TC.riesgos    = [];
        tcRiesgoEditingRowIndex = null;
        await tcSeleccionarProyecto(id, true /* silent — no navega a contratos */);
    }
    await tcCargarRevisionesSelect();
}

async function tcCargarRevisionesSelect() {
    if (!TC.proyectoId) return;
    try {
        const lista = await tcGet('RevisionesJson', { proyectoId: TC.proyectoId });
        TC.revisiones = lista;

        // Mostrar proyecto activo en cabecera de la pestaña Riesgos
        const lblProy = document.getElementById('riesgos-proyecto-label');
        if (lblProy) lblProy.textContent = TC.proyecto ? `${TC.proyecto.codigo} — ${TC.proyecto.nombre}` : '—';

        const sel = document.getElementById('tc-revision-sel');
        if (!sel) return;

        sel.innerHTML = '<option value="">— Selecciona revisión —</option>'
            + lista.map(r => `<option value="${r.id}" ${TC.revisionId===r.id?'selected':''}>Rev${r.numeroRevision} — ${esc(r.descripcion)} (${r.totalRiesgos} riesgos)</option>`).join('');

        // Auto-seleccionar la revisión actual; si no hay, tomar la más reciente (primera de la lista)
        if (TC.revisionId && lista.find(r => r.id === TC.revisionId)) {
            sel.value = TC.revisionId;
            tcCargarRevision();
        } else if (lista.length > 0 && !TC.revisionId) {
            sel.value = lista[0].id;
            TC.revisionId = lista[0].id;
            tcCargarRevision();
        }
    } catch(e) { console.error(e); }
}

async function tcCargarRevisionesSelectMC() {
    if (!TC.proyectoId) return;
    try {
        const lista = await tcGet('RevisionesJson', { proyectoId: TC.proyectoId });
        TC.revisiones = lista;
        const sel = document.getElementById('mc-revision-sel');
        if (!sel) return;
        sel.innerHTML = '<option value="">Sin riesgos</option>'
            + lista.map(r => `<option value="${r.id}" ${TC.revisionId===r.id?'selected':''}>Rev${r.numeroRevision} — ${esc(r.descripcion)}</option>`).join('');
    } catch(e) { console.error(e); }
}

async function tcCargarRevision() {
    const sel = document.getElementById('tc-revision-sel');
    const id  = sel?.value;
    if (!id) {
        TC.revisionId = null;
        TC.riesgos    = [];
        tcRiesgoEditingRowIndex = null;
        document.getElementById('tc-riesgos-panel').classList.add('hidden');
        document.getElementById('tc-riesgos-empty').classList.remove('hidden');
        return;
    }
    TC.revisionId = id;
    try {
        const data = await tcGet('RevisionJson', { id });
        TC.riesgos = data.riesgos || [];
        tcRiesgoEditingRowIndex = null;
        document.getElementById('tc-riesgos-panel').classList.remove('hidden');
        document.getElementById('tc-riesgos-empty').classList.add('hidden');
        document.getElementById('tc-rev-titulo').textContent =
            `Rev${data.numeroRevision} — ${data.descripcion} (${data.fechaCreacion})`;
        tcRenderRiesgos();
        // Remover panel MiroFish anterior (si cambió el proyecto/revisión) y re-inyectar
        document.getElementById('tc-mf-section')?.remove();
        setTimeout(tcMiroFishInjectSection, 300);
    } catch(e) { alert('Error: ' + e.message); }
}

function tcAutoResize(el) {
    el.style.height = 'auto';
    el.style.height = (el.scrollHeight) + 'px';
}

// Dispatcher: renderiza según el modo activo
function tcRenderRiesgos() {
    const compactTbody = document.getElementById('tc-riesgos-compact-tbody');
    if (tcRiesgoViewMode === 'compact' && compactTbody) tcRenderRiesgosCompact();
    else tcRenderRiesgosEdit();
    tcRenderRiesgosKpiCards();
}

function tcRenderRiesgosKpiCards() {
    const el = document.getElementById('tc-riesgos-kpi-cards');
    if (!el) return;

    const U  = v => '$' + tcFmt(v ?? 0, 0) + ' USD';
    const TT = (txt, pos) => `<span class="gpr-tt ${pos||''}"><span class="material-icons gpr-tt-icon" style="color:#94a3b8;font-size:13px">help_outline</span><span class="gpr-tt-box">${txt}</span></span>`;

    const amenazas = TC.riesgos.filter(r => !r.esOportunidad);
    const opors    = TC.riesgos.filter(r =>  r.esOportunidad);
    const veAme    = amenazas.reduce((s, r) => s + (r.probabilidad / 100) * (r.impactoProableUsd || 0), 0);
    const veOpo    = opors.reduce((s, r)    => s + (r.probabilidad / 100) * (r.impactoProableUsd || 0), 0);

    const K     = _tcMfKpis;
    const hasMC = K != null && K.nAmenazas != null;

    const amenContent = hasMC
        ? `<div class="space-y-1.5 text-sm mt-2">
               <div class="flex justify-between items-center">
                   <span class="text-slate-500 text-xs">P50 Monte Carlo</span>
                   <span class="font-bold text-red-700 dark:text-red-300 tabular-nums">${K.amenP50 > 0 ? U(K.amenP50) : '— (p&lt;50%)'}</span>
               </div>
               <div class="flex justify-between items-center">
                   <span class="text-slate-500 text-xs">P80 Monte Carlo</span>
                   <span class="font-semibold text-red-600 dark:text-red-400 tabular-nums">${U(K.amenP80)}</span>
               </div>
               <div class="flex justify-between items-center border-t border-red-100 dark:border-red-800 pt-1 mt-1">
                   <span class="text-slate-400 text-xs">Valor Esperado</span>
                   <span class="text-slate-500 text-xs tabular-nums">${U(veAme)}</span>
               </div>
           </div>`
        : `<div class="space-y-1.5 text-sm mt-2">
               <div class="flex justify-between items-center">
                   <span class="text-slate-500 text-xs">Valor Esperado</span>
                   <span class="font-bold text-red-700 dark:text-red-300 tabular-nums">${U(veAme)}</span>
               </div>
               <p class="text-xs text-slate-400 italic mt-1">Ejecute el Resumen Global para ver P50 MC</p>
           </div>`;

    const opoContent = hasMC
        ? `<div class="space-y-1.5 text-sm mt-2">
               <div class="flex justify-between items-center">
                   <span class="text-slate-500 text-xs">Ahorro P50 Monte Carlo</span>
                   <span class="font-bold text-emerald-700 dark:text-emerald-300 tabular-nums">${K.opoP50 > 0 ? U(K.opoP50) : '— (p&lt;50%)'}</span>
               </div>
               <div class="flex justify-between items-center border-t border-emerald-100 dark:border-emerald-800 pt-1 mt-1">
                   <span class="text-slate-400 text-xs">Valor Esperado</span>
                   <span class="text-slate-500 text-xs tabular-nums">${U(Math.abs(veOpo))}</span>
               </div>
           </div>`
        : `<div class="space-y-1.5 text-sm mt-2">
               <div class="flex justify-between items-center">
                   <span class="text-slate-500 text-xs">Valor Esperado</span>
                   <span class="font-bold text-emerald-700 dark:text-emerald-300 tabular-nums">${U(Math.abs(veOpo))}</span>
               </div>
               <p class="text-xs text-slate-400 italic mt-1">Ejecute el Resumen Global para ver P50 MC</p>
           </div>`;

    el.innerHTML = `
        <div class="bg-red-50 dark:bg-red-900/20 rounded-xl p-4 border-2 border-red-200 dark:border-red-800">
            <div class="flex items-center gap-2">
                <span class="material-icons text-red-500" style="font-size:18px">trending_up</span>
                <span class="text-sm font-bold text-red-700 dark:text-red-300">Amenazas</span>
                ${TT('Eventos negativos que aumentan el costo. El P50 MC viene del Resumen Global.', 'gpr-tt-right')}
                <span class="ml-auto text-xs font-semibold text-red-400 bg-red-100 dark:bg-red-900/40 px-2 py-0.5 rounded-full">${amenazas.length}</span>
            </div>
            ${amenContent}
        </div>
        <div class="bg-emerald-50 dark:bg-emerald-900/20 rounded-xl p-4 border-2 border-emerald-200 dark:border-emerald-800">
            <div class="flex items-center gap-2">
                <span class="material-icons text-emerald-500" style="font-size:18px">trending_down</span>
                <span class="text-sm font-bold text-emerald-700 dark:text-emerald-300">Oportunidades</span>
                ${TT('Eventos positivos que reducen el costo. El P50 MC viene del Resumen Global.', 'gpr-tt-right')}
                <span class="ml-auto text-xs font-semibold text-emerald-400 bg-emerald-100 dark:bg-emerald-900/40 px-2 py-0.5 rounded-full">${opors.length}</span>
            </div>
            ${opoContent}
        </div>`;
}

// ── Vista Ejecutiva (compacta, solo lectura) ──────────────────────────────
function tcRenderRiesgosCompact() {
    const tbody = document.getElementById('tc-riesgos-compact-tbody');
    if (!tbody) return;

    if (!TC.riesgos.length) {
        tbody.innerHTML = '<tr><td colspan="9" style="text-align:center;padding:2rem 1rem;color:#94a3b8;font-size:0.875rem">Sin riesgos. Cambia a vista Edición para agregar registros.</td></tr>';
        tcActualizarTotalRiesgos();
        return;
    }

    // Ordenar por VE absoluto
    const sorted = TC.riesgos.map((r, origIdx) => ({
        ...r, origIdx,
        ve: (r.probabilidad / 100) * (r.impactoProableUsd || 0)
    })).sort((a, b) => {
        const diff = Math.abs(b.ve) - Math.abs(a.ve);
        return tcRiesgoSortAsc ? -diff : diff;
    });

    const totalVeAme = sorted.filter(r => !r.esOportunidad).reduce((s, r) => s + r.ve, 0) || 0.01;

    tbody.innerHTML = sorted.map((r, di) => {
        const esOp   = r.esOportunidad;
        const prob   = r.probabilidad ?? 100;
        const imp    = r.impactoProableUsd || 0;
        const impMin = r.impactoMinUsd  != null ? r.impactoMinUsd  : imp * 0.6;
        const impMax = r.impactoMaxUsd  != null ? r.impactoMaxUsd  : imp * 1.5;
        const ve     = r.ve;
        const veAbs  = Math.abs(ve);
        const pctC   = !esOp && totalVeAme > 0.01 ? (ve / totalVeAme * 100).toFixed(1) : null;

        const veColor = esOp ? '#059669' : '#ea580c';
        const rowBg   = esOp ? 'background:rgba(209,250,229,0.18)' : 'background:rgba(254,226,226,0.14)';

        // Tipo badge con icono
        const typeBg  = esOp ? 'background:#d1fae5;color:#065f46' : 'background:#fee2e2;color:#991b1b';
        const typeIcon = esOp ? '↑' : '⚠';
        const typeBadge = `<span style="display:inline-flex;align-items:center;gap:3px;font-size:10px;font-weight:700;padding:2px 7px;border-radius:4px;${typeBg};white-space:nowrap">${typeIcon} ${esOp?'OPO':'AME'}</span>`;

        // Probabilidad badge + barra
        const pn = tcProbNivel(prob);
        const probCell = `<div style="text-align:center;min-width:80px">
            <span style="font-size:10px;font-weight:700;padding:1px 5px;border-radius:3px;color:${pn.color};background:${pn.bg}">${pn.label}</span>
            <div style="font-size:12px;font-weight:700;color:${pn.color};margin-top:2px">${prob}%</div>
            <div style="margin-top:3px;height:4px;border-radius:2px;background:#e2e8f0;overflow:hidden">
                <div style="height:100%;border-radius:2px;background:${pn.color};width:${Math.min(prob,100)}%"></div>
            </div>
        </div>`;

        // VE con contribución %
        const veCell = `<div style="text-align:right">
            <div style="font-size:13px;font-weight:700;color:${veColor};white-space:nowrap">${esOp?'+':''}\$${tcFmtM(ve)}</div>
            ${pctC ? `<div style="font-size:10px;color:#94a3b8;margin-top:1px">${pctC}% del total AME</div>` : ''}
        </div>`;

        // Spread Mín–Máx con barra tricolor
        const maxRef  = Math.max(impMax, imp, 1);
        const wMin    = Math.min((impMin / maxRef * 100), 100).toFixed(1);
        const wMid    = Math.min(((imp - impMin) / maxRef * 100), 100).toFixed(1);
        const wMax    = Math.min(((impMax - imp) / maxRef * 100), 100).toFixed(1);
        const barC    = esOp ? '#10b981' : '#f97316';
        const barCMax = esOp ? '#059669' : '#ef4444';
        const spreadCell = `<div style="min-width:110px">
            <div style="display:flex;justify-content:space-between;font-size:9px;color:#94a3b8;margin-bottom:2px">
                <span>\$${tcFmtM(impMin)}</span><span>\$${tcFmtM(impMax)}</span>
            </div>
            <div style="height:6px;border-radius:3px;background:#e2e8f0;overflow:hidden;display:flex">
                <div style="width:${wMin}%;background:transparent"></div>
                <div style="width:${wMid}%;background:${barC};opacity:0.45"></div>
                <div style="width:${wMax}%;background:${barCMax};opacity:0.7"></div>
            </div>
            <div style="font-size:9px;color:#94a3b8;text-align:center;margin-top:1px">\$${tcFmtM(imp)} probable</div>
        </div>`;

        // Nivel de riesgo
        const nv = tcNivelRiesgo(prob, veAbs);
        const nivelBadge = `<span style="font-size:10px;font-weight:700;padding:2px 6px;border-radius:4px;color:${nv.color};background:${nv.bg};white-space:nowrap">${nv.label}</span>`;

        // Título truncado con toggle de detalle
        const titulo = esc(r.titulo || '—');
        const origen = esc(r.origen || '');
        const desc   = esc(r.descripcion || '');
        const notas  = esc(r.notasCambio || '');
        const hasDetail = !!(r.descripcion || r.notasCambio);
        const detailId  = `rdet-${di}`;

        const tituloCell = `<div>
            <div style="font-size:0.8rem;font-weight:600;color:#1e293b;line-height:1.3;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;max-width:240px" title="${titulo}">${titulo}</div>
            ${origen ? `<div style="font-size:10px;color:#94a3b8;margin-top:1px">${origen}</div>` : ''}
            ${hasDetail ? `<button onclick="document.getElementById('${detailId}').classList.toggle('hidden')" style="font-size:10px;color:#6366f1;background:none;border:none;padding:0;cursor:pointer;margin-top:2px">▾ ver detalle</button>` : ''}
        </div>`;

        const detailRow = hasDetail ? `
        <tr id="${detailId}" class="hidden">
            <td colspan="9" style="padding:8px 12px 10px 52px;background:rgba(241,245,249,0.85)">
                <div style="display:grid;grid-template-columns:${desc&&notas?'1fr 1fr':'1fr'};gap:14px">
                    ${desc ? `<div>
                        <div style="font-size:9px;font-weight:700;color:#64748b;text-transform:uppercase;letter-spacing:.05em;margin-bottom:3px">Descripción</div>
                        <div style="font-size:12px;color:#374151;line-height:1.5">${desc}</div>
                    </div>` : ''}
                    ${notas ? `<div>
                        <div style="font-size:9px;font-weight:700;color:#64748b;text-transform:uppercase;letter-spacing:.05em;margin-bottom:3px">Notas / Plan de Respuesta</div>
                        <div style="font-size:12px;color:#374151;line-height:1.5">${notas}</div>
                    </div>` : ''}
                </div>
            </td>
        </tr>` : '';

        return `
        <tr style="${rowBg}" class="tc-riesgo-compact-row">
            <td style="padding:8px 4px;color:#94a3b8;font-size:11px;text-align:center;vertical-align:middle">${di+1}</td>
            <td style="padding:8px 4px;vertical-align:middle">${typeBadge}</td>
            <td style="padding:8px 4px;font-family:monospace;font-size:11px;font-weight:600;color:#64748b;vertical-align:middle;white-space:nowrap">${esc(r.codigoRiesgo||'—')}</td>
            <td style="padding:8px 4px;vertical-align:middle">${tituloCell}</td>
            <td style="padding:8px 4px;vertical-align:middle">${probCell}</td>
            <td style="padding:8px 4px;vertical-align:middle">${veCell}</td>
            <td style="padding:8px 4px;vertical-align:middle">${spreadCell}</td>
            <td style="padding:8px 4px;text-align:center;vertical-align:middle">${nivelBadge}</td>
            <td style="padding:8px 4px;text-align:center;vertical-align:middle">
                <button onclick="tcToggleRiesgoView('edit')" title="Ir a vista Edición"
                    style="display:inline-flex;align-items:center;justify-content:center;width:26px;height:26px;border-radius:6px;border:none;background:transparent;cursor:pointer;color:#94a3b8"
                    onmouseover="this.style.color='#6366f1';this.style.background='#eef2ff'"
                    onmouseout="this.style.color='#94a3b8';this.style.background='transparent'">
                    <span class="material-icons" style="font-size:15px">edit_note</span>
                </button>
            </td>
        </tr>${detailRow}`;
    }).join('');

    // Hover en filas compactas
    tbody.querySelectorAll('tr.tc-riesgo-compact-row').forEach(tr => {
        const base = tr.style.background;
        const isOp = base.includes('209,250');
        const hov  = isOp ? 'rgba(167,243,208,0.32)' : 'rgba(254,202,202,0.32)';
        tr.addEventListener('mouseenter', () => tr.style.background = hov);
        tr.addEventListener('mouseleave', () => tr.style.background = base);
    });

    tcActualizarTotalRiesgos();
}

function tcRiesgosSyncRowFromDom(i) {
    const row = document.querySelector(`#tc-riesgos-tbody tr[data-r-idx="${i}"]`);
    if (!row || tcRiesgoEditingRowIndex !== i) return;
    const g  = f => row.querySelector(`[data-rf="${f}"]`)?.value ?? '';
    const gb = f => row.querySelector(`[data-rf="${f}"]`)?.checked ?? false;
    const impMinRaw = g('impMin').trim();
    const impMaxRaw = g('impMax').trim();
    const impMin = parseFloat(impMinRaw);
    const impMax = parseFloat(impMaxRaw);
    const base = TC.riesgos[i] || {};
    TC.riesgos[i] = {
        ...base,
        origen: g('origen') || 'Proyecto',
        codigoRiesgo: g('codigo'),
        titulo: g('titulo'),
        descripcion: g('desc'),
        probabilidad: parseFloat(g('prob')) || 100,
        impactoProableUsd: parseFloat(g('impacto')) || 0,
        impactoMinUsd: impMinRaw !== '' && Number.isFinite(impMin) ? impMin : null,
        impactoMaxUsd: impMaxRaw !== '' && Number.isFinite(impMax) ? impMax : null,
        esOportunidad: gb('esOp'),
        notasCambio: g('notas')
    };
}

function tcRiesgosBeginEdit(i) {
    if (tcRiesgoEditingRowIndex !== null && tcRiesgoEditingRowIndex !== i)
        tcRiesgosSyncRowFromDom(tcRiesgoEditingRowIndex);
    tcRiesgoEditingRowIndex = i;
    tcRenderRiesgos();
}

function tcRiesgosFinishEdit() {
    if (tcRiesgoEditingRowIndex !== null) tcRiesgosSyncRowFromDom(tcRiesgoEditingRowIndex);
    tcRiesgoEditingRowIndex = null;
    tcRenderRiesgos();
}

// ── Tabla principal: lectura por defecto, una fila editable a la vez ───────
function tcRenderRiesgosEdit() {
    const tbody = document.getElementById('tc-riesgos-tbody');
    if (!tbody) return;
    if (!TC.riesgos.length) {
        tbody.innerHTML = '<tr><td colspan="13" style="text-align:center;padding:2rem 1rem;color:#94a3b8;font-size:0.875rem">Sin riesgos. Usa <strong>Agregar</strong> para el primer registro.</td></tr>';
        tcActualizarTotalRiesgos();
        return;
    }

    if (tcRiesgoEditingRowIndex !== null && tcRiesgoEditingRowIndex >= TC.riesgos.length)
        tcRiesgoEditingRowIndex = null;

    const ta = (rf, val, placeholder='') =>
        `<textarea class="tc-input" data-rf="${rf}" rows="1" placeholder="${placeholder}"
            style="width:100%;resize:none;overflow:hidden;line-height:1.4;min-height:28px"
            oninput="tcAutoResize(this)">${esc(val||'')}</textarea>`;

    const trunc = (s, n) => {
        const t = (s || '').trim();
        if (!t) return '<span class="text-slate-400">—</span>';
        if (t.length <= n) return esc(t);
        return `<span title="${esc(t)}">${esc(t.slice(0, n))}…</span>`;
    };

    tbody.innerHTML = TC.riesgos.map((r, i) => {
        const editing = tcRiesgoEditingRowIndex === i;
        const esOp    = r.esOportunidad;
        const prob    = r.probabilidad ?? 100;
        const imp     = Number(r.impactoProableUsd) || 0;
        const ve      = prob / 100 * imp;
        const rowBg   = esOp ? 'background:rgba(209,250,229,0.35)' : 'background:rgba(254,226,226,0.25)';
        const veColor = esOp ? '#059669' : '#ea580c';
        const badgeBg = esOp ? 'background:#d1fae5;color:#065f46' : 'background:#fee2e2;color:#991b1b';
        const barColor= esOp ? '#10b981' : '#f97316';
        const badge   = `<span style="display:inline-block;font-size:10px;font-weight:700;padding:1px 5px;border-radius:4px;${badgeBg}">${esOp?'OPO':'AME'}</span>`;
        const pn      = tcProbNivel(prob);

        if (!editing) {
            const minS = r.impactoMinUsd != null && r.impactoMinUsd !== '' ? '$' + tcFmt(Number(r.impactoMinUsd), 0) : '<span class="text-slate-400 text-xs">Auto</span>';
            const maxS = r.impactoMaxUsd != null && r.impactoMaxUsd !== '' ? '$' + tcFmt(Number(r.impactoMaxUsd), 0) : '<span class="text-slate-400 text-xs">Auto</span>';
            return `
        <tr data-r-idx="${i}" style="${rowBg}" class="tc-riesgo-row tc-riesgo-row--read border-b border-slate-100 dark:border-slate-700/80 transition-colors">
            <td class="px-2 py-2.5 align-top text-xs text-slate-400 tabular-nums">${i + 1}</td>
            <td class="px-2 py-2.5 align-top text-sm text-slate-700 dark:text-slate-200">${esc(r.origen || 'Proyecto')}</td>
            <td class="px-2 py-2.5 align-top font-mono text-xs font-semibold text-slate-600 dark:text-slate-300">${esc(r.codigoRiesgo || '—')}</td>
            <td class="px-2 py-2.5 align-top text-sm text-slate-800 dark:text-slate-100 max-w-[11rem]">${trunc(r.titulo, 48)}</td>
            <td class="px-2 py-2.5 align-top text-xs text-slate-600 dark:text-slate-400 max-w-[14rem]">${trunc(r.descripcion, 80)}</td>
            <td class="px-2 py-2.5 align-top text-center">
                <div class="text-xs font-bold text-slate-800 dark:text-slate-100 tabular-nums">${prob}%</div>
                <div class="mt-1 text-[10px] font-semibold px-1.5 py-0.5 rounded inline-block" style="color:${pn.color};background:${pn.bg}">${pn.label}</div>
                <div class="mt-1 h-1 rounded bg-slate-200 dark:bg-slate-600 overflow-hidden max-w-[56px] mx-auto">
                    <div class="h-full rounded" style="background:${barColor};width:${Math.min(prob,100)}%"></div>
                </div>
            </td>
            <td class="px-2 py-2.5 align-top text-sm text-right tabular-nums font-medium text-slate-800 dark:text-slate-100">$${tcFmt(imp, 0)}</td>
            <td class="px-2 py-2.5 align-top text-xs text-right tabular-nums text-slate-600 dark:text-slate-300">${minS}</td>
            <td class="px-2 py-2.5 align-top text-xs text-right tabular-nums text-slate-600 dark:text-slate-300">${maxS}</td>
            <td class="px-2 py-2.5 align-top text-center">${badge}</td>
            <td class="px-2 py-2.5 align-top text-right text-sm font-semibold tabular-nums whitespace-nowrap" style="color:${veColor}">${ve === 0 ? '—' : '$' + tcFmt(ve, 0)}</td>
            <td class="px-2 py-2.5 align-top text-xs text-slate-600 dark:text-slate-400 max-w-[10rem]">${trunc(r.notasCambio, 60)}</td>
            <td class="px-2 py-2.5 align-top text-right whitespace-nowrap">
                <div class="inline-flex items-center gap-0.5 justify-end">
                    <button type="button" onclick="tcRiesgosBeginEdit(${i})" title="Editar fila"
                        class="inline-flex items-center justify-center w-8 h-8 rounded-lg border-0 bg-transparent cursor-pointer text-teal-600 hover:bg-teal-50 dark:hover:bg-teal-900/30">
                        <span class="material-icons text-lg">edit</span>
                    </button>
                    <button type="button" onclick="tcRemoveRiesgoRow(this)" title="Eliminar"
                        class="inline-flex items-center justify-center w-8 h-8 rounded-lg border-0 bg-transparent cursor-pointer text-slate-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20">
                        <span class="material-icons text-base">delete</span>
                    </button>
                </div>
            </td>
        </tr>`;
        }

        return `
        <tr data-r-idx="${i}" style="${rowBg}" class="tc-riesgo-row tc-riesgo-row--editing ring-1 ring-inset ring-teal-400/60 dark:ring-teal-500/50">
            <td style="padding:6px 4px;white-space:nowrap;color:#94a3b8;font-size:0.75rem;vertical-align:top">${i+1}</td>
            <td style="padding:4px 3px;vertical-align:top;min-width:90px">
                <select class="tc-input" data-rf="origen" style="width:100%">
                    <option ${(r.origen||'Proyecto')==='Proyecto'?'selected':''}>Proyecto</option>
                    <option ${r.origen==='Interno'?'selected':''}>Interno</option>
                    <option ${r.origen==='Externo'?'selected':''}>Externo</option>
                    <option ${r.origen==='Contrato'?'selected':''}>Contrato</option>
                </select>
            </td>
            <td style="padding:4px 3px;vertical-align:top;min-width:80px">
                <input class="tc-input" data-rf="codigo" value="${esc(r.codigoRiesgo||'')}" placeholder="R-001"
                    style="width:100%;font-family:monospace" />
            </td>
            <td style="padding:4px 3px;vertical-align:top;min-width:150px;max-width:200px">${ta('titulo', r.titulo, 'Título del riesgo')}</td>
            <td style="padding:4px 3px;vertical-align:top;min-width:200px;max-width:300px">${ta('desc', r.descripcion, 'Descripción detallada...')}</td>
            <td style="padding:4px 3px;vertical-align:top;text-align:center;min-width:72px">
                <input class="tc-input" data-rf="prob" type="number" step="1" min="0" max="100"
                    value="${prob}" oninput="tcActualizarTotalRiesgos()" title="Probabilidad %"
                    style="width:56px;text-align:center;font-weight:600" />
                <div style="margin-top:3px;height:4px;border-radius:2px;background:#e2e8f0;overflow:hidden">
                    <div style="height:100%;border-radius:2px;background:${barColor};width:${Math.min(prob,100)}%"></div>
                </div>
            </td>
            <td style="padding:4px 3px;vertical-align:top;min-width:190px">
                <input class="tc-input" data-rf="impacto" type="number" step="0.01"
                    value="${imp}" oninput="tcActualizarTotalRiesgos()"
                    style="width:100%;text-align:right" />
            </td>
            <td style="padding:4px 3px;vertical-align:top;min-width:175px">
                <input class="tc-input" data-rf="impMin" type="number" step="0.01"
                    value="${r.impactoMinUsd != null && r.impactoMinUsd !== '' ? esc(String(r.impactoMinUsd)) : ''}" placeholder="Auto"
                    style="width:100%;text-align:right" />
            </td>
            <td style="padding:4px 3px;vertical-align:top;min-width:175px">
                <input class="tc-input" data-rf="impMax" type="number" step="0.01"
                    value="${r.impactoMaxUsd != null && r.impactoMaxUsd !== '' ? esc(String(r.impactoMaxUsd)) : ''}" placeholder="Auto"
                    style="width:100%;text-align:right" />
            </td>
            <td style="padding:4px 3px;vertical-align:top;text-align:center;white-space:nowrap">
                <label style="display:flex;flex-direction:column;align-items:center;gap:4px;cursor:pointer">
                    ${badge}
                    <input type="checkbox" data-rf="esOp" ${esOp?'checked':''} style="width:16px;height:16px;accent-color:#0d9488"
                        onchange="tcRiesgosSyncRowFromDom(${i}); tcRenderRiesgos();" title="Marcar como Oportunidad" />
                </label>
            </td>
            <td style="padding:4px 3px;vertical-align:top;text-align:right;white-space:nowrap;font-weight:600;font-size:0.8125rem;color:${veColor}">
                ${ve===0?'—':'$'+tcFmt(ve,0)}
            </td>
            <td style="padding:4px 3px;vertical-align:top;min-width:140px;max-width:200px">${ta('notas', r.notasCambio, 'Notas de cambio...')}</td>
            <td style="padding:4px 3px;vertical-align:top;text-align:right">
                <div class="inline-flex flex-col items-end gap-1">
                    <button type="button" onclick="tcRiesgosFinishEdit()" title="Listo (guardar cambios en memoria)"
                        class="inline-flex items-center justify-center w-8 h-8 rounded-lg border-0 cursor-pointer text-white bg-teal-600 hover:bg-teal-700">
                        <span class="material-icons text-base">check</span>
                    </button>
                    <button type="button" onclick="tcRemoveRiesgoRow(this)" title="Eliminar fila"
                        class="inline-flex items-center justify-center w-8 h-8 rounded-lg border-0 bg-transparent cursor-pointer text-slate-400 hover:text-red-600 hover:bg-red-50">
                        <span class="material-icons text-base">delete</span>
                    </button>
                </div>
            </td>
        </tr>`;
    }).join('');

    tbody.querySelectorAll('textarea').forEach(tcAutoResize);

    tbody.querySelectorAll('tr.tc-riesgo-row--read').forEach(tr => {
        const base = tr.style.background;
        const esOp = base.includes('209,250');
        const hov  = esOp ? 'rgba(167,243,208,0.45)' : 'rgba(254,202,202,0.38)';
        tr.addEventListener('mouseenter', () => { tr.style.background = hov; });
        tr.addEventListener('mouseleave', () => { tr.style.background = base; });
    });

    tbody.querySelectorAll('tr.tc-riesgo-row--editing').forEach(tr => {
        const base = tr.style.background;
        const esOp = base.includes('209,250');
        const hov  = esOp ? 'rgba(167,243,208,0.5)' : 'rgba(254,202,202,0.45)';
        tr.addEventListener('mouseenter', () => { tr.style.background = hov; });
        tr.addEventListener('mouseleave', () => { tr.style.background = base; });
    });

    tcActualizarTotalRiesgos();
}

// Alterna entre vista ejecutiva y edición
function tcToggleRiesgoView(mode) {
    if (mode === 'compact' && tcRiesgoViewMode === 'edit') {
        // Sincronizar DOM → TC.riesgos antes de cambiar
        TC.riesgos = tcGetRiesgosFromDOM();
    }
    tcRiesgoViewMode = mode;

    const cv      = document.getElementById('tc-riesgos-compact-view');
    const ev      = document.getElementById('tc-riesgos-edit-view');
    const btnC    = document.getElementById('btn-vista-compacta');
    const btnE    = document.getElementById('btn-vista-edicion');
    const btnAg   = document.getElementById('tc-riesgos-btn-agregar');
    const btnGu   = document.getElementById('tc-riesgos-btn-guardar');

    const activeStyle   = 'bg-white dark:bg-slate-700 shadow-sm text-slate-700 dark:text-slate-200';
    const inactiveStyle = 'text-slate-500 dark:text-slate-400';

    if (mode === 'compact') {
        cv?.classList.remove('hidden');
        ev?.classList.add('hidden');
        btnC?.setAttribute('class', `flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors ${activeStyle}`);
        btnE?.setAttribute('class', `flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors hover:bg-white/60 dark:hover:bg-slate-600/40 ${inactiveStyle}`);
        btnAg?.classList.add('hidden');
        btnGu?.classList.add('hidden');
        tcRenderRiesgosCompact();
    } else {
        cv?.classList.add('hidden');
        ev?.classList.remove('hidden');
        btnE?.setAttribute('class', `flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors ${activeStyle}`);
        btnC?.setAttribute('class', `flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors hover:bg-white/60 dark:hover:bg-slate-600/40 ${inactiveStyle}`);
        btnAg?.classList.remove('hidden');
        btnGu?.classList.remove('hidden');
        tcRiesgoEditingRowIndex = null;
        tcRenderRiesgosEdit();
    }
}

// Alterna orden de VE en vista compacta
function tcSortRiesgos() {
    tcRiesgoSortAsc = !tcRiesgoSortAsc;
    tcRenderRiesgosCompact();
}

function tcExportarRiesgosCSV() {
    const riesgos = tcGetRiesgosFromDOM();
    if (!riesgos.length) { alert('No hay riesgos para exportar.'); return; }
    const proyNombre = TC.proyecto?.nombre || 'Proyecto';
    const revTitulo  = document.getElementById('tc-rev-titulo')?.textContent || '';
    const headers = ['#','Origen','Código','Título','Descripción','Prob %','Impacto Probable USD',
                     'Impacto Mín USD','Impacto Máx USD','Tipo','VE USD','Notas Cambio'];
    const rows = riesgos.map((r, i) => {
        const ve = (r.probabilidad/100) * r.impactoProableUsd;
        return [
            i+1,
            r.origen, r.codigoRiesgo, r.titulo, r.descripcion,
            r.probabilidad,
            r.impactoProableUsd,
            r.impactoMinUsd ?? '',
            r.impactoMaxUsd ?? '',
            r.esOportunidad ? 'Oportunidad' : 'Amenaza',
            ve.toFixed(2),
            r.notasCambio
        ].map(v => `"${String(v??'').replace(/"/g,'""')}"`).join(',');
    });
    const bom  = '\uFEFF'; // BOM para Excel en español (UTF-8)
    const csv  = bom + `Proyecto:,"${proyNombre}"\nRevisión:,"${revTitulo}"\n\n` + headers.join(',') + '\n' + rows.join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = `Riesgos_${(proyNombre).replace(/\s+/g,'_')}.csv`;
    a.click();
    URL.revokeObjectURL(url);
}

function tcAddRiesgoRow() {
    if (!TC.revisionId) { alert('Selecciona una revisión primero.'); return; }
    if (tcRiesgoEditingRowIndex !== null) tcRiesgosSyncRowFromDom(tcRiesgoEditingRowIndex);
    TC.riesgos.push({ origen:'Proyecto', codigoRiesgo:'', titulo:'', descripcion:'',
        probabilidad:100, impactoProableUsd:0, esOportunidad:false, notasCambio:'' });
    tcRiesgoEditingRowIndex = TC.riesgos.length - 1;
    tcRenderRiesgos();
}

function tcRemoveRiesgoRow(btn) {
    const idx = parseInt(btn.closest('tr').dataset.rIdx, 10);
    if (Number.isNaN(idx)) return;
    if (tcRiesgoEditingRowIndex === idx) tcRiesgoEditingRowIndex = null;
    else if (tcRiesgoEditingRowIndex !== null && tcRiesgoEditingRowIndex > idx) tcRiesgoEditingRowIndex--;
    TC.riesgos.splice(idx, 1);
    tcRenderRiesgos();
}

function tcGetRiesgosFromDOM() {
    if (tcRiesgoEditingRowIndex !== null) tcRiesgosSyncRowFromDom(tcRiesgoEditingRowIndex);
    return TC.riesgos.map(r => ({
        ...r,
        origen: r.origen ?? 'Proyecto',
        codigoRiesgo: r.codigoRiesgo ?? '',
        titulo: r.titulo ?? '',
        descripcion: r.descripcion ?? '',
        probabilidad: Number(r.probabilidad) || 100,
        impactoProableUsd: Number(r.impactoProableUsd) || 0,
        impactoMinUsd: r.impactoMinUsd != null && r.impactoMinUsd !== '' ? Number(r.impactoMinUsd) : null,
        impactoMaxUsd: r.impactoMaxUsd != null && r.impactoMaxUsd !== '' ? Number(r.impactoMaxUsd) : null,
        esOportunidad: !!r.esOportunidad,
        notasCambio: r.notasCambio ?? '',
        itemViaRiesgoId: r.itemViaRiesgoId ?? null
    }));
}

function tcActualizarTotalRiesgos() {
    let neto = 0;
    const compactTbody = document.getElementById('tc-riesgos-compact-tbody');
    const mainTbody    = document.getElementById('tc-riesgos-tbody');
    const useMainGrid  = mainTbody && (!compactTbody || tcRiesgoViewMode === 'edit');

    if (useMainGrid) {
        TC.riesgos.forEach((r, i) => {
            let val, prob, esOp;
            if (i === tcRiesgoEditingRowIndex) {
                const row = mainTbody.querySelector(`tr[data-r-idx="${i}"]`);
                val  = parseFloat(row?.querySelector('[data-rf="impacto"]')?.value) || 0;
                prob = parseFloat(row?.querySelector('[data-rf="prob"]')?.value) || 0;
                esOp = row?.querySelector('[data-rf="esOp"]')?.checked ?? false;
            } else {
                val  = Number(r.impactoProableUsd) || 0;
                prob = Number(r.probabilidad) || 100;
                esOp = !!r.esOportunidad;
            }
            neto += esOp ? -(prob / 100) * val : (prob / 100) * val;
        });
        const el = document.getElementById('tc-riesgos-total');
        if (el) {
            el.textContent = (neto >= 0 ? '+' : '') + tcFmtUsd(neto);
            el.className = neto >= 0
                ? 'px-2 py-2 text-orange-600 dark:text-orange-400 font-bold tabular-nums'
                : 'px-2 py-2 text-green-600 dark:text-green-400 font-bold tabular-nums';
        }
    } else if (tcRiesgoViewMode === 'compact') {
        TC.riesgos.forEach(r => {
            const ve = (r.probabilidad / 100) * (r.impactoProableUsd || 0);
            neto += r.esOportunidad ? -ve : ve;
        });
        const el = document.getElementById('tc-riesgos-compact-total');
        if (el) {
            el.textContent = (neto >= 0 ? '+' : '') + tcFmtUsd(neto);
            el.style.color = neto >= 0 ? '#ea580c' : '#059669';
        }
    }
}

async function tcGuardarRiesgos() {
    if (!TC.revisionId) { alert('Selecciona una revisión.'); return; }
    if (document.getElementById('tc-riesgos-tbody')) TC.riesgos = tcGetRiesgosFromDOM();
    try {
        await tcPost('RiesgosBulk', { revisionId: TC.revisionId, riesgos: TC.riesgos });
        alert('Riesgos guardados correctamente.');
    } catch(e) { alert('Error: ' + e.message); }
}

async function tcNuevaRevision() {
    if (!TC.proyectoId) { alert('Primero selecciona un proyecto.'); return; }
    const desc = prompt('Descripción de la nueva revisión (ej: Rev3):');
    if (desc === null) return;
    const copiar = TC.revisionId && confirm('¿Copiar riesgos de la revisión actual como base?');
    try {
        const r = await tcPost('Revision', {
            proyectoId: TC.proyectoId,
            descripcion: desc.trim() || null,
            copiarDeRevisionId: copiar ? TC.revisionId : null
        });
        await tcCargarRevisionesSelect();
        document.getElementById('tc-revision-sel').value = r.id;
        TC.revisionId = r.id;
        await tcCargarRevision();
    } catch(e) { alert('Error: ' + e.message); }
}

// ════════════════════════════════════════════════════════════════════════════
// MONTE CARLO
// ════════════════════════════════════════════════════════════════════════════
function tcActualizarResumenMC() {
    const el = document.getElementById('mc-resumen-body');
    const btn = document.getElementById('mc-ejecutar-btn');
    if (!TC.proyectoId) {
        if (el) el.innerHTML = '<p class="text-xs text-slate-400">Selecciona un proyecto primero.</p>';
        if (btn) btn.disabled = true;
        return;
    }
    const totalContratos = TC.contratos.length;
    const totalItems = TC.contratos.reduce((s,c) => s + (c.totalItems||0), 0);
    const incertItems = TC.contratos.reduce((s,c) => s + (c.itemsIncertidumbre||0), 0);
    const revSel = document.getElementById('mc-revision-sel')?.value;
    const revInfo = TC.revisiones.find(r => r.id === revSel);

    if (el) el.innerHTML = `
        <div class="flex justify-between text-xs py-0.5"><span class="text-slate-500">Proyecto:</span><span class="font-medium truncate max-w-32">${esc(TC.proyecto?.nombre||'—')}</span></div>
        <div class="flex justify-between text-xs py-0.5"><span class="text-slate-500">Código:</span><span class="font-medium">${esc(TC.proyecto?.codigo||'—')}</span></div>
        <div class="flex justify-between text-xs py-0.5"><span class="text-slate-500">Contratos:</span><span class="font-medium">${totalContratos}</span></div>
        <div class="flex justify-between text-xs py-0.5"><span class="text-slate-500">Ítems totales:</span><span class="font-medium">${totalItems}</span></div>
        <div class="flex justify-between text-xs py-0.5"><span class="text-slate-500">Ítems MC:</span><span class="font-medium text-teal-600">${incertItems}</span></div>
        <div class="flex justify-between text-xs py-0.5"><span class="text-slate-500">Revisión riesgos:</span><span class="font-medium">${revInfo ? `Rev${revInfo.numeroRevision}` : 'Sin riesgos'}</span></div>
    `;

    const habilitar = totalContratos > 0 && incertItems > 0;
    if (btn) btn.disabled = !habilitar;
}

async function tcEjecutarMonteCarlo() {
    if (!TC.proyectoId) { alert('Selecciona un proyecto.'); return; }
    const iter = parseInt(document.getElementById('mc-iter').value) || 10000;
    const revId = document.getElementById('mc-revision-sel').value || null;

    document.getElementById('mc-loading').classList.remove('hidden');
    document.getElementById('mc-resultados').classList.add('hidden');
    document.getElementById('mc-sin-resultados').classList.add('hidden');
    document.getElementById('mc-loading-iter').textContent = `${tcFmt(iter)} iteraciones...`;
    document.getElementById('mc-ejecutar-btn').disabled = true;

    try {
        const data = await tcPost('EjecutarMontecarlo', {
            proyectoId: TC.proyectoId,
            revisionId: revId,
            iteraciones: iter
        });
        tcMostrarResultados(data);
        tcCargarHistorial();
    } catch(e) {
        alert('Error al ejecutar Monte Carlo: ' + e.message);
        document.getElementById('mc-sin-resultados').classList.remove('hidden');
    } finally {
        document.getElementById('mc-loading').classList.add('hidden');
        document.getElementById('mc-ejecutar-btn').disabled = false;
    }
}

function tcMostrarResultados(data) {
    document.getElementById('mc-resultados').classList.remove('hidden');
    document.getElementById('mc-sin-resultados').classList.add('hidden');

    const fUsd = v => '$' + tcFmt(v, 0) + ' USD';
    const fPct = v => (v >= 0 ? '+' : '') + tcFmt(v, 1) + '%';

    // Cards
    document.getElementById('mc-p50-eat').textContent  = fUsd(data.eatP50);
    document.getElementById('mc-p50-cont').textContent = fUsd(data.contingenciaP50);
    document.getElementById('mc-p50-crec').textContent = fPct(data.crecimientoP50Pct);

    document.getElementById('mc-p80-eat').textContent  = fUsd(data.eatP80);
    document.getElementById('mc-p80-cont').textContent = fUsd(data.contingenciaP80);
    document.getElementById('mc-p80-crec').textContent = fPct(data.crecimientoP80Pct);

    document.getElementById('mc-p90-eat').textContent  = fUsd(data.eatP90);
    document.getElementById('mc-p90-cont').textContent = fUsd(data.contingenciaP90);
    document.getElementById('mc-p90-crec').textContent = fPct(data.crecimientoP90Pct);

    // Tabla
    const tbody = document.getElementById('mc-tabla-resultados');
    if (tbody) {
        const filas = [
            { label: 'CAPEX Aprobado', eat: data.capexApi, cont: 0, crec: 0, cls: '' },
            { label: 'EAT Determinístico', eat: data.eatDeterministico, cont: data.eatDeterministico - data.capexApi, crec: (data.eatDeterministico - data.capexApi) / (data.capexApi || 1) * 100, cls: '' },
            { label: 'P50 — Escenario Base', eat: data.eatP50, cont: data.contingenciaP50, crec: data.crecimientoP50Pct, cls: 'bg-blue-50 dark:bg-blue-900/10' },
            { label: 'P80 — Conservador',  eat: data.eatP80, cont: data.contingenciaP80, crec: data.crecimientoP80Pct, cls: 'bg-green-50 dark:bg-green-900/10' },
            { label: 'P90 — Gestión',       eat: data.eatP90, cont: data.contingenciaP90, crec: data.crecimientoP90Pct, cls: 'bg-orange-50 dark:bg-orange-900/10' }
        ];
        tbody.innerHTML = filas.map(f => `
            <tr class="${f.cls}">
                <td class="px-2 py-1.5 text-sm font-medium text-slate-700 dark:text-slate-300">${f.label}</td>
                <td class="px-2 py-1.5 text-right text-sm font-bold text-slate-800 dark:text-slate-100">${fUsd(f.eat)}</td>
                <td class="px-2 py-1.5 text-right text-sm ${f.cont >= 0 ? 'text-orange-600' : 'text-green-600'}">${fUsd(f.cont)}</td>
                <td class="px-2 py-1.5 text-right text-sm font-semibold ${f.crec >= 0 ? 'text-orange-600' : 'text-green-600'}">${fPct(f.crec)}</td>
            </tr>`).join('');
    }

    // Estadísticas
    const statsGrid = document.getElementById('mc-stats-grid');
    if (statsGrid) {
        const cvRisk = data.cv > 0.5 ? 'badge-rojo' : data.cv > 0.2 ? 'badge-amarillo' : 'badge-verde';
        const cvLabel = data.cv > 0.5 ? 'ALTO' : data.cv > 0.2 ? 'MEDIO' : 'BAJO';
        statsGrid.innerHTML = `
            <div class="tc-card py-2 px-3">
                <p class="text-xs text-slate-500 mb-1">Iteraciones</p>
                <p class="font-bold text-slate-800 dark:text-slate-100">${tcFmt(data.iteraciones)}</p>
            </div>
            <div class="tc-card py-2 px-3">
                <p class="text-xs text-slate-500 mb-1">Media Incertidumbre</p>
                <p class="font-bold text-slate-800 dark:text-slate-100">${fUsd(data.mediaIncertidumbre)}</p>
            </div>
            <div class="tc-card py-2 px-3">
                <p class="text-xs text-slate-500 mb-1">Desviación Estándar</p>
                <p class="font-bold text-slate-800 dark:text-slate-100">${fUsd(data.desviacion)}</p>
            </div>
            <div class="tc-card py-2 px-3">
                <p class="text-xs text-slate-500 mb-1">Nivel de Riesgo (CV)</p>
                <span class="${cvRisk}">${cvLabel} (CV: ${tcFmt(data.cv*100,1)}%)</span>
            </div>`;
    }
}

async function tcCargarHistorial() {
    if (!TC.proyectoId) return;
    const el = document.getElementById('mc-historial');
    if (!el) return;
    try {
        const lista = await tcGet('ResultadosJson', { proyectoId: TC.proyectoId });
        if (!lista.length) {
            el.innerHTML = '<p class="text-xs text-slate-400">Sin análisis previos para este proyecto.</p>';
            return;
        }
        el.innerHTML = lista.map(a => {
            let r = {};
            try { r = JSON.parse(a.resultadosJson); } catch {}
            return `<div class="flex items-center justify-between py-1.5 border-b border-slate-100 dark:border-slate-800 last:border-0">
                <div>
                    <p class="text-xs font-medium text-slate-700 dark:text-slate-300">${esc(a.fechaEjecucion)}</p>
                    <p class="text-xs text-slate-400">${tcFmt(a.iteraciones)} iter. · P50: $${tcFmt(r.eatP50,0)} · P80: $${tcFmt(r.eatP80,0)}</p>
                </div>
                <button class="btn-secondary py-1 px-2 text-xs" onclick='tcMostrarResultados(${JSON.stringify(r)})'>
                    <span class="material-icons text-sm">visibility</span>
                </button>
            </div>`;
        }).join('');
    } catch(e) { el.innerHTML = '<p class="text-xs text-red-400">' + e.message + '</p>'; }
}

// ════════════════════════════════════════════════════════════════════════════
// MODALES
// ════════════════════════════════════════════════════════════════════════════
function tcAbrirModal(id) {
    const el = document.getElementById(id);
    if (el) { el.classList.remove('hidden'); el.classList.add('flex'); }
}

function tcCerrarModal(id) {
    const el = document.getElementById(id);
    if (el) { el.classList.add('hidden'); el.classList.remove('flex'); }
}

// Cerrar modal al hacer click fuera
document.addEventListener('click', e => {
    ['modal-proyecto','modal-contrato'].forEach(id => {
        const modal = document.getElementById(id);
        if (modal && e.target === modal) tcCerrarModal(id);
    });
});

// ════════════════════════════════════════════════════════════════════════════
// UTIL
// ════════════════════════════════════════════════════════════════════════════
function esc(str) {
    if (str === null || str === undefined) return '';
    return String(str)
        .replace(/&/g,'&amp;').replace(/</g,'&lt;')
        .replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

// ════════════════════════════════════════════════════════════════════════════
// RESUMEN GLOBAL DEL PROYECTO
// ════════════════════════════════════════════════════════════════════════════

async function tcPopularSelectResumen() {
    const sel = document.getElementById('tc-resumen-sel-proy');
    if (!sel) return;
    try {
        const lista = await tcGet('ProyectosJson');
        const current = sel.value;
        sel.innerHTML = '<option value="">— Selecciona un proyecto —</option>' +
            lista.map(p => `<option value="${esc(p.id)}">${esc(p.codigo)} — ${esc(p.nombre)}</option>`).join('');
        // Mantener selección actual si sigue existiendo
        if (current && lista.some(p => p.id === current)) sel.value = current;
        else if (TC.proyectoId && lista.some(p => p.id === TC.proyectoId)) sel.value = TC.proyectoId;
    } catch { /* silencioso */ }
}

async function tcCambiarProyectoResumen(id) {
    if (!id) return;
    await tcSeleccionarProyecto(id, true);
    tcCargarResumen();
}

function tcResumenIrContrato(contratoId) {
    if (!contratoId) return;
    // Asegurar que el contrato está cargado en TC.contratos
    if (!TC.contratos || !TC.contratos.find(x => x.id === contratoId)) return;
    // tcAbrirContrato carga los datos del contrato y navega a Bloque A
    tcAbrirContrato(contratoId);
}

async function tcCargarResumen() {
    const el = document.getElementById('tc-resumen-content');
    if (!el) return;

    if (!TC.proyectoId) {
        el.innerHTML = `
            <div class="tc-card border-dashed border-slate-200 dark:border-slate-700 text-center py-16">
                <span class="material-icons text-5xl text-slate-200 dark:text-slate-700 mb-3 block">insights</span>
                <p class="text-sm font-semibold text-slate-400 dark:text-slate-500 mb-1">Sin datos todavía</p>
                <p class="text-xs text-slate-400 dark:text-slate-600">Selecciona un proyecto en el selector de arriba.</p>
            </div>`;
        return;
    }

    el.innerHTML = `<div class="flex items-center justify-center py-16 text-slate-400"><div class="tc-spinner mr-3"></div> Cargando resumen…</div>`;
    try {
        const ctrl  = new AbortController();
        const timer = setTimeout(() => ctrl.abort(), 15000);
        const qs    = new URLSearchParams({ proyectoId: TC.proyectoId }).toString();
        const resp  = await fetch(`/TallerCostos?handler=ResumenGlobalJson&${qs}`, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            signal: ctrl.signal
        });
        clearTimeout(timer);
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        const data = await resp.json();
        window._tcLastResumenData = data;

        // Subtítulo del header
        const sub = document.getElementById('tc-resumen-subtitle');
        if (sub) sub.textContent = `${data.proyecto.codigo} — ${data.proyecto.nombre}  ·  Ejercicio ${data.proyecto.fechaEjercicio}`;

        // Sincronizar select
        const sel = document.getElementById('tc-resumen-sel-proy');
        if (sel) sel.value = TC.proyectoId;

        el.innerHTML = tcBuildResumen(data);
        requestAnimationFrame(() => {
            if (data.eatCombinado?.histograma?.length) {
                renderHistogramaEAT(data.eatCombinado, data.totales);
            }
            if (data.eatCombinado?.tieneRiesgos && data.eatCombinado?.histogramaRiesgoNeto?.length) {
                renderHistogramaRiesgoNeto(data.eatCombinado);
            }
            tcBindRiskDistPercentilesChart(el, data.eatCombinado);
            tcBindExposicionCapexBar();
        });

    } catch(e) {
        el.innerHTML = `<div class="text-center py-12 text-red-500"><span class="material-icons text-3xl block mb-2">error</span>Error: ${esc(e.message)}</div>`;
    }
}

/** Tooltips al pasar el mouse sobre la barra «Exposición vs CAPEX» (usa _tcLastResumenData). */
function tcBindExposicionCapexBar() {
    const hit = document.getElementById('tc-expo-bar-hit');
    let tip = document.getElementById('tc-expo-tooltip');
    if (!hit) return;
    if (!tip) {
        tip = document.createElement('div');
        tip.id = 'tc-expo-tooltip';
        tip.setAttribute('role', 'tooltip');
        tip.className = 'fixed z-[10050] hidden max-w-[min(320px,calc(100vw-24px))] rounded-lg text-[11px] p-3 pointer-events-none leading-snug';
        tip.style.background = 'rgba(15, 23, 42, 0.94)';
        tip.style.backdropFilter = 'blur(10px)';
        tip.style.webkitBackdropFilter = 'blur(10px)';
        tip.style.color = '#f8fafc';
        tip.style.border = '1px solid rgba(148, 163, 184, 0.4)';
        tip.style.boxShadow = '0 12px 42px rgba(0, 0, 0, 0.45), 0 0 0 1px rgba(0, 0, 0, 0.2)';
        document.body.appendChild(tip);
    } else {
        tip.style.background = 'rgba(15, 23, 42, 0.94)';
        tip.style.backdropFilter = 'blur(10px)';
        tip.style.webkitBackdropFilter = 'blur(10px)';
        tip.style.color = '#f8fafc';
        tip.style.border = '1px solid rgba(148, 163, 184, 0.4)';
        tip.style.boxShadow = '0 12px 42px rgba(0, 0, 0, 0.45), 0 0 0 1px rgba(0, 0, 0, 0.2)';
    }
    if (hit._tcExpoMove) {
        hit.removeEventListener('mousemove', hit._tcExpoMove);
        hit.removeEventListener('mouseleave', hit._tcExpoLeave);
        hit.removeEventListener('blur', hit._tcExpoLeave);
    }
    const T = window._tcLastResumenData?.totales;
    if (!T) return;
    const capex = T.capex || 1;
    const pctComp = (T.comprometido / capex) * 100;
    const pctPorComp = (T.porComprometer / capex) * 100;
    const pctEATv = pctComp + pctPorComp;
    const maxScale = Math.max(Math.ceil(pctEATv / 20) * 20 + 15, 115);
    const wComp = (pctComp / maxScale) * 100;
    const wPor = (pctPorComp / maxScale) * 100;
    const wCapex = (100 / maxScale) * 100;
    const stackEnd = wComp + wPor;
    const U = v => '$' + tcFmt(v ?? 0, 0) + ' USD';
    const Pct = v => tcFmt(v, 1) + '%';
    const excUsd = Math.max(0, (T.eat ?? 0) - capex);

    const move = ev => {
        const r = hit.getBoundingClientRect();
        if (!r.width) return;
        const x = ev.clientX - r.left;
        const pxPct = (x / r.width) * 100;
        const linePx = (wCapex / 100) * r.width;
        const nearLine = Math.abs(x - linePx) <= Math.max(5, r.width * 0.012);

        let html = '';
        if (nearLine) {
            html = `<div class="font-semibold text-indigo-200 mb-1">Tope 100% — CAPEX</div>
                <div class="tabular-nums text-white font-semibold text-sm">${U(T.capex)}</div>
                <div class="mt-1.5 text-slate-300">Línea índigo: presupuesto base aprobado. La suma Comprometido + Por comprometer la cruza cuando el EAT determinístico supera el techo.</div>`;
        } else if (pxPct < wComp) {
            html = `<div class="font-semibold text-emerald-200 mb-1">Comprometido</div>
                <div class="tabular-nums text-white font-semibold text-sm">${U(T.comprometido)}</div>
                <div class="text-slate-200 mt-0.5">${Pct(pctComp)} del CAPEX · ${Pct((pctComp / maxScale) * 100)} de la escala del gráfico</div>
                <div class="mt-1.5 text-slate-300">Monto ya contratado u obligado; no entra al Monte Carlo como variación.</div>`;
        } else if (pxPct < stackEnd) {
            html = `<div class="font-semibold text-blue-200 mb-1">Por comprometer</div>
                <div class="tabular-nums text-white font-semibold text-sm">${U(T.porComprometer)}</div>
                <div class="text-slate-200 mt-0.5">${Pct(pctPorComp)} del CAPEX · ${Pct((pctPorComp / maxScale) * 100)} de la escala del gráfico</div>
                <div class="mt-1.5 text-slate-300">Costo pendiente de contratar; con rangos Min/Prob/Máx alimenta la incertidumbre del modelo.</div>`;
        } else if (pctEATv > 100 && pxPct >= wCapex) {
            html = `<div class="font-semibold text-red-200 mb-1">Por encima del techo CAPEX</div>
                <div class="text-slate-200">EAT determinístico <span class="tabular-nums text-white font-semibold">${U(T.eat)}</span> (${Pct(pctEATv)} del CAPEX)</div>
                <div class="mt-1 text-amber-200">Exceso sobre techo: <span class="tabular-nums font-semibold text-white">${U(excUsd)}</span> (${Pct(pctEATv - 100)})</div>
                <div class="mt-1.5 text-slate-300">Zona sombreada: escala de referencia más allá del 100% aprobado.</div>`;
        } else if (pxPct >= stackEnd && pxPct < wCapex) {
            html = `<div class="font-semibold text-slate-100 mb-1">Margen en escala</div>
                <div class="text-slate-300">Entre el fin del EAT mostrado y el 100% CAPEX hay <span class="text-emerald-200 font-medium">${Pct(wCapex - stackEnd)}</span> de la escala sin usar.</div>
                <div class="mt-1.5 text-slate-400">Equivale a holgura visual antes del techo índigo.</div>`;
        } else {
            html = `<div class="font-semibold text-slate-100 mb-1">Escala extendida</div>
                <div class="text-slate-300">Esta franja amplía el eje hasta <strong class="text-white">${Pct(maxScale)}</strong> del CAPEX para ver barras que superan el 100%.</div>`;
        }

        tip.innerHTML = html;
        tip.classList.remove('hidden');
        void tip.offsetWidth;
        const pad = 14;
        const tw = tip.getBoundingClientRect().width || 260;
        const th = tip.getBoundingClientRect().height || 80;
        let lx = ev.clientX + pad;
        let ly = ev.clientY + pad;
        if (lx + tw > window.innerWidth - 8) lx = ev.clientX - tw - pad;
        if (ly + th > window.innerHeight - 8) ly = ev.clientY - th - pad;
        tip.style.left = `${Math.max(8, lx)}px`;
        tip.style.top = `${Math.max(8, ly)}px`;
    };
    const leave = () => { tip.classList.add('hidden'); };
    hit._tcExpoMove = move;
    hit._tcExpoLeave = leave;
    hit.addEventListener('mousemove', move);
    hit.addEventListener('mouseleave', leave);
}

/**
 * Bloque colapsable: estadísticos de la distribución del EAT simulado y tabla de percentiles (lectura tipo informes @RISK).
 * Usar dentro de un contenedor ancho completo (p. ej. debajo del grid 9+3).
 * @param {object} EC eatCombinado
 * @param {(v:number)=>string} U formateador USD
 * @param {(html:string,pos?:string)=>string} TT tooltip
 */
function tcHtmlDistribucionRiskStyle(EC, U, TT) {
    if (!EC?.percentilesTabla?.length) return '';
    const S = EC.soloEstadisticasMc;
    const hasCmp = S != null;
    const fmtN = (x, d) => (x != null && typeof x === 'number' && isFinite(x)) ? tcFmt(x, d) : '—';
    const cellUsd = x => (x != null && typeof x === 'number') ? U(x) : '—';
    const hi = 'bg-violet-50/95 dark:bg-violet-950/40 border-l-[3px] border-violet-500 dark:border-violet-400 font-semibold';
    const row3 = (label, tip, aSolo, aFull, isDim, important) => {
        const cR = isDim ? fmtN(aFull, 4) : cellUsd(aFull);
        const cB = hasCmp ? (isDim ? fmtN(aSolo, 4) : cellUsd(aSolo)) : '';
        const trc = important ? hi : '';
        return `<tr class="border-b border-slate-200/80 dark:border-slate-700/80 ${trc}">
            <td class="py-2 px-3 text-xs text-slate-700 dark:text-slate-200 ${important ? 'bg-transparent' : 'bg-amber-50/80 dark:bg-amber-950/25'}">${label}${tip || ''}</td>
            ${hasCmp ? `<td class="py-2 px-3 text-xs text-right tabular-nums text-blue-900 dark:text-blue-200 ${important ? 'bg-transparent' : ''}">${cB}</td>` : ''}
            <td class="py-2 px-3 text-xs text-right tabular-nums text-red-900 dark:text-red-200 ${important ? 'bg-transparent' : ''}">${cR}</td>
        </tr>`;
    };
    const mediaFull = EC.eatMedia ?? EC.media;
    const p50Full = EC.eatP50 ?? EC.p50;
    const icTip = TT('Semiancho (P95−P5)/2 sobre la distribución simulada del EAT total. Resume el ancho del 90% central del resultado; no es un intervalo de confianza de la media muestral.', 'gpr-tt-left');
    const icRow = `<tr class="border-b border-slate-200/80 dark:border-slate-700/80 ${hi}">
        <td class="py-2 px-3 text-xs text-slate-800 dark:text-slate-100 bg-transparent">IC 90% ${icTip}</td>
        ${hasCmp ? `<td class="py-2 px-3 text-xs text-right tabular-nums text-blue-900 dark:text-blue-200 bg-transparent">${S.ic90Semirango != null ? '±' + U(S.ic90Semirango) : '—'}</td>` : ''}
        <td class="py-2 px-3 text-xs text-right tabular-nums text-red-900 dark:text-red-200 bg-transparent">${EC.ic90Semirango != null ? '±' + U(EC.ic90Semirango) : '—'}</td>
    </tr>`;

    const statBody = [
        row3('Mínimo', '', S?.min, EC.min, false, false),
        row3('Máximo', '', S?.max, EC.max, false, false),
        row3('Media', TT('Promedio aritmético de todas las iteraciones; con asimetría, puede diferir de la mediana.', 'gpr-tt-left'), S?.media, mediaFull, false, true),
        icRow,
        row3('Moda (estim.)', TT('Centro del bin de mayor frecuencia en el histograma.', 'gpr-tt-left'), S?.modaEstimada, EC.modaEstimada, false, false),
        row3('Mediana (P50)', TT('50% de la distribución acumulada queda por debajo; referencia central para decisión.', 'gpr-tt-left'), S?.mediana, p50Full, false, true),
        row3('Desv. est.', '', S?.desvEst, EC.desvEst, false, false),
        row3('Asimetría', TT('>0: cola derecha (más probabilidad de sobrecosto que de ahorro).', 'gpr-tt-left'), S?.skewness, EC.skewness, true, false),
        row3('Curtosis (exceso)', TT('Fisher: 0 ≈ normal; >0 colas más pesadas.', 'gpr-tt-left'), S?.kurtosisExceso, EC.kurtosisExceso, true, false),
        `<tr class="border-b border-slate-200/80 dark:border-slate-700/80">
            <td class="py-2 px-3 text-xs text-slate-600 dark:text-slate-400 bg-slate-50/80 dark:bg-slate-800/50">Iteraciones</td>
            ${hasCmp ? `<td class="py-2 px-3 text-xs text-right tabular-nums text-blue-800 dark:text-blue-300">${fmtN(EC.nSimulaciones, 0)}</td>` : ''}
            <td class="py-2 px-3 text-xs text-right tabular-nums text-red-800 dark:text-red-300">${fmtN(EC.nSimulaciones, 0)}</td>
        </tr>`
    ].join('');

    const pctKey = p => [10, 50, 80, 90].includes(p);
    const pctHead = hasCmp
        ? `<tr class="text-[10px] uppercase tracking-wide text-slate-600 dark:text-slate-400 border-b-2 border-slate-300 dark:border-slate-600">
            <th class="py-2 px-3 text-left bg-amber-100/90 dark:bg-amber-950/40 font-bold">Percentil</th>
            <th class="py-2 px-3 text-right text-blue-800 dark:text-blue-300 font-bold">Solo incertidumbre</th>
            <th class="py-2 px-3 text-right text-red-800 dark:text-red-300 font-bold">+ Riesgos MC</th>
           </tr>`
        : `<tr class="text-[10px] uppercase tracking-wide text-slate-600 dark:text-slate-400 border-b-2 border-slate-300 dark:border-slate-600">
            <th class="py-2 px-3 text-left bg-amber-100/90 dark:bg-amber-950/40 font-bold">Percentil</th>
            <th class="py-2 px-3 text-right text-red-800 dark:text-red-300 font-bold">EAT simulado</th>
           </tr>`;

    const pctBody = EC.percentilesTabla.map(r => {
        const sv = S?.percentilesTabla?.find(x => x.p === r.p);
        const pk = pctKey(r.p) ? hi : '';
        return hasCmp
            ? `<tr class="border-b border-slate-100 dark:border-slate-800 ${pk}">
                <td class="py-1.5 px-3 text-xs font-medium bg-amber-50/90 dark:bg-amber-950/30">${r.p}%</td>
                <td class="py-1.5 px-3 text-xs text-right tabular-nums text-blue-900 dark:text-blue-200">${sv ? U(sv.v) : '—'}</td>
                <td class="py-1.5 px-3 text-xs text-right tabular-nums text-red-900 dark:text-red-200">${U(r.v)}</td>
               </tr>`
            : `<tr class="border-b border-slate-100 dark:border-slate-800 ${pk}">
                <td class="py-1.5 px-3 text-xs font-medium bg-amber-50/90 dark:bg-amber-950/30">${r.p}%</td>
                <td class="py-1.5 px-3 text-xs text-right tabular-nums text-red-900 dark:text-red-200">${U(r.v)}</td>
               </tr>`;
    }).join('');

    return `
    <details class="tc-risk-dist-details mt-5 w-full max-w-none rounded-xl border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-900/50 shadow-sm overflow-hidden">
        <summary class="cursor-pointer select-none px-4 py-3 text-sm font-semibold text-slate-800 dark:text-slate-100 flex items-center gap-2 bg-gradient-to-r from-slate-50 to-slate-100/80 dark:from-slate-800 dark:to-slate-800/70 border-b border-slate-200 dark:border-slate-600">
            <span class="material-icons text-lg text-violet-600 dark:text-violet-400">analytics</span>
            Distribución del EAT simulado — estadísticos y percentiles (referencia tipo @RISK)
            ${TT('Resumen numérico de la misma simulación Monte Carlo que alimenta el histograma: momentos, moda por histograma y cuantiles cada 5% (y P99). Compare la columna azul (solo variación triangular de ítems, mismos sorteos por iteración) con la roja (más Bernoulli×Triangular de riesgos) para ver cómo los riesgos ensanchan la distribución.', 'gpr-tt-left')}
        </summary>
        <div class="p-4 sm:p-5 space-y-6 border-t border-slate-200 dark:border-slate-600 w-full">
            <p class="text-[11px] text-slate-600 dark:text-slate-400 leading-relaxed max-w-5xl">
                Los valores son el <strong>EAT total</strong> por iteración (comprometido + certeza + ítems inciertos [+ riesgos en la columna roja]).
                La <strong>moda</strong> es estimación por histograma; <strong>IC 90%</strong> aquí es el semiancho (P95−P5)/2 del resultado simulado, no el error estándar de la media.
                Las filas <span class="text-violet-700 dark:text-violet-300 font-medium">resaltadas</span> son las de mayor uso en decisiones de presupuesto (media, dispersión central IC 90%, mediana, y en la tabla inferior P10, P50, P80, P90).
            </p>
            <div class="w-full">
                <h4 class="text-xs font-bold uppercase tracking-wide text-slate-600 dark:text-slate-400 mb-2 flex items-center gap-2">
                    <span class="material-icons text-base text-slate-500">show_chart</span>
                    Curva EAT vs percentil (millones USD)
                </h4>
                <p class="text-[10px] text-slate-500 mb-2">Misma rejilla de percentiles que la tabla inferior: lectura tipo curva-S / cuantiles @RISK.</p>
                <div class="relative w-full rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50/50 dark:bg-slate-950/30" style="height:min(320px,45vh);min-height:220px">
                    <canvas id="tc-cdf-percentiles-chart" class="block w-full h-full"></canvas>
                </div>
            </div>
            <div class="w-full overflow-x-auto rounded-lg border border-slate-200 dark:border-slate-700">
                <table class="w-full min-w-[640px] text-left border-collapse text-xs">
                    <thead>
                        <tr class="text-[10px] uppercase tracking-wide text-slate-600 dark:text-slate-300 border-b-2 border-slate-300 dark:border-slate-600 bg-slate-100/90 dark:bg-slate-800/80">
                            <th class="py-2.5 px-3 text-left w-[28%]">Métrica</th>
                            ${hasCmp ? '<th class="py-2.5 px-3 text-right text-blue-800 dark:text-blue-300 font-bold">Solo incertidumbre</th>' : ''}
                            <th class="py-2.5 px-3 text-right text-red-800 dark:text-red-300 font-bold">+ Riesgos MC</th>
                        </tr>
                    </thead>
                    <tbody>${statBody}</tbody>
                </table>
            </div>
            <div class="w-full">
                <h4 class="text-xs font-bold uppercase tracking-wide text-slate-600 dark:text-slate-400 mb-2">Percentiles de la distribución acumulada</h4>
                <div class="overflow-x-auto max-h-[min(480px,55vh)] overflow-y-auto rounded-lg border border-slate-200 dark:border-slate-700 w-full">
                    <table class="w-full min-w-[640px] text-left border-collapse text-xs">
                        <thead class="sticky top-0 z-10 shadow-sm">${pctHead}</thead>
                        <tbody>${pctBody}</tbody>
                    </table>
                </div>
            </div>
        </div>
    </details>`;
}

/** Acordeón @RISK: minimizado por defecto; el gráfico se crea al abrir (canvas con tamaño válido). */
function tcBindRiskDistPercentilesChart(container, EC) {
    const det = container?.querySelector?.('details.tc-risk-dist-details');
    if (!det || !EC?.percentilesTabla?.length) return;
    det.addEventListener('toggle', () => {
        if (!det.open) return;
        requestAnimationFrame(() => {
            renderTcCdfPercentilesChart(EC);
            requestAnimationFrame(() => {
                try { _tcPctChart?.resize?.(); } catch (_) { /* noop */ }
            });
        });
    });
}

/** Curva de cuantiles: EAT (M USD) vs percentil; destruye instancia previa si existe. */
let _tcPctChart = null;
function renderTcCdfPercentilesChart(EC) {
    const canvas = document.getElementById('tc-cdf-percentiles-chart');
    if (!canvas || typeof Chart === 'undefined') return;
    if (_tcPctChart) { _tcPctChart.destroy(); _tcPctChart = null; }
    if (!EC?.percentilesTabla?.length) return;

    const pts = EC.percentilesTabla;
    const labels = pts.map(r => r.p + '%');
    const yFull = pts.map(r => r.v / 1e6);
    const S = EC.soloEstadisticasMc;
    const datasets = [];

    const prDot = pts.map(r => ([10, 50, 80, 90].includes(r.p) ? 5 : 0));

    if (S?.percentilesTabla?.length) {
        const ySolo = pts.map(r => {
            const row = S.percentilesTabla.find(x => x.p === r.p);
            return row ? row.v / 1e6 : null;
        });
        datasets.push({
            label: 'Solo incertidumbre',
            data: ySolo,
            borderColor: '#1d4ed8',
            backgroundColor: 'rgba(29,78,216,0.08)',
            borderWidth: 2,
            pointRadius: prDot,
            pointHoverRadius: 7,
            tension: 0.2,
            fill: false
        });
    }

    datasets.push({
        label: '+ Riesgos MC',
        data: yFull,
        borderColor: '#b91c1c',
        backgroundColor: 'rgba(185,28,28,0.06)',
        borderWidth: 2,
        pointRadius: prDot,
        pointHoverRadius: 7,
        tension: 0.2,
        fill: false
    });

    _tcPctChart = new Chart(canvas, {
        type: 'line',
        data: { labels, datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: {
                    position: 'top',
                    labels: { boxWidth: 12, font: { size: 11 }, color: '#64748b' }
                },
                tooltip: {
                    callbacks: {
                        title: items => 'Percentil ' + (pts[items[0].dataIndex]?.p ?? '') + '%',
                        label: ctx => {
                            const v = ctx.parsed.y;
                            if (v == null || !isFinite(v)) return ctx.dataset.label + ': —';
                            return ctx.dataset.label + ': $' + v.toFixed(2) + ' M USD';
                        }
                    }
                }
            },
            scales: {
                x: {
                    title: { display: true, text: 'Percentil acumulado', color: '#64748b', font: { size: 11, weight: '600' } },
                    ticks: { maxRotation: 45, font: { size: 9 }, color: '#94a3b8', autoSkip: true, maxTicksLimit: 14 }
                },
                y: {
                    title: { display: true, text: 'EAT total (millones USD)', color: '#64748b', font: { size: 11, weight: '600' } },
                    ticks: { font: { size: 10 }, color: '#94a3b8' }
                }
            }
        }
    });
}

function tcBuildResumen(data) {
    if (!data?.totales) return '<div class="text-center py-12 text-slate-400">Sin datos de proyecto</div>';

    const T     = data.totales;
    const EC    = data.eatCombinado;
    const RD    = data.riesgosDisplay;
    // Resultado guardado de la última depuración en pestaña Riesgos (fuente de verdad para el card de riesgos)
    const SR    = data.savedRisksSimulation;
    const U     = v => '$' + tcFmt(v ?? 0, 0) + ' USD';
    const Pct   = v => v != null ? tcFmt(v, 1) + '%' : '—';
    const Pct2  = v => v != null ? tcFmt(v, 2) + '%' : '—';
    const fmtCV = v => v != null ? tcFmt(v, 1) + '%' : '—';
    const capex = T.capex || 1;

    // ══════ SECCIÓN 1 — KPIs del Proyecto ══════════════════════════════════

    const kpiComp = T.pctComprometido > 115 ? ['border-red-200 dark:border-red-900',    'bg-red-50 dark:bg-red-900/20',       'text-red-700 dark:text-red-300']
                  : T.pctComprometido > 100  ? ['border-orange-200 dark:border-orange-900','bg-orange-50 dark:bg-orange-900/20','text-orange-700 dark:text-orange-300']
                  :                            ['border-emerald-200 dark:border-emerald-900','bg-emerald-50 dark:bg-emerald-900/20','text-emerald-700 dark:text-emerald-300'];
    const kpiEAT  = T.pctEAT > 115 ? ['border-red-200 dark:border-red-900',    'bg-red-50 dark:bg-red-900/20',    'text-red-700 dark:text-red-300']
                  : T.pctEAT > 100  ? ['border-amber-200 dark:border-amber-900','bg-amber-50 dark:bg-amber-900/20','text-amber-700 dark:text-amber-300']
                  :                   ['border-teal-200 dark:border-teal-900',  'bg-teal-50 dark:bg-teal-900/20',  'text-teal-700 dark:text-teal-300'];
    const slackPos = (T.slack ?? 0) >= 0;
    const pctSlackVsCapex = (Math.abs(T.slack ?? 0) / capex) * 100;
    const kpiSlack = slackPos ? ['border-emerald-200 dark:border-emerald-900','bg-emerald-50 dark:bg-emerald-900/20','text-emerald-700 dark:text-emerald-300']
                              : ['border-red-200 dark:border-red-900',    'bg-red-50 dark:bg-red-900/20',       'text-red-700 dark:text-red-300'];

    const TT = (tip, pos='') => `<span class="gpr-tt ${pos} ml-auto flex-shrink-0"><span class="material-icons gpr-tt-icon" style="font-size:12px;opacity:0.55">help_outline</span><span class="gpr-tt-box">${tip}</span></span>`;

    const kpiCard = (label, value, sub, border, bg, valCls, icon, tip='') => `
    <div class="rounded-2xl ${border} ${bg} p-4 border">
        <div class="flex items-center gap-2 mb-3">
            <div class="w-8 h-8 rounded-lg bg-white/40 dark:bg-black/20 flex items-center justify-center flex-shrink-0">
                <span class="material-icons text-sm ${valCls} opacity-80">${icon}</span>
            </div>
            <span class="text-xs font-medium opacity-60 uppercase tracking-wide leading-tight">${label}</span>
            ${tip ? TT(tip, 'gpr-tt-left') : ''}
        </div>
        <div class="text-xl font-bold tabular-nums ${valCls}">${value}</div>
        ${sub ? `<div class="text-xs mt-1 opacity-60">${sub}</div>` : ''}
    </div>`;

    const sec1KpiCards = `
    <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3 mb-4">
        ${kpiCard('CAPEX Total', U(T.capex), 'Presupuesto base',
            'border-slate-200 dark:border-slate-700','bg-white dark:bg-slate-900',
            'text-slate-800 dark:text-slate-100','account_balance',
            'Presupuesto total aprobado por el directorio. Es el techo financiero del proyecto. El semáforo EAT/CAPEX mide si el proyecto está dentro del margen autorizado (Verde ≤100%, Ámbar 100–115%, Rojo &gt;115%).')}
        ${kpiCard('Comprometido', U(T.comprometido), Pct(T.pctComprometido)+' del CAPEX',
            kpiComp[0], kpiComp[1], kpiComp[2], 'payments',
            'Monto ya contratado mediante órdenes de compra o contratos firmados. Es un valor fijo sin variabilidad probabilística. Su % sobre el CAPEX indica el avance de adjudicación del proyecto.')}
        ${kpiCard('Por Comprometer', U(T.porComprometer), Pct(T.porComprometer/capex*100)+' del CAPEX',
            'border-blue-200 dark:border-blue-900','bg-blue-50 dark:bg-blue-900/20',
            'text-blue-700 dark:text-blue-300','pending_actions',
            'Costos pendientes de contratar. Tiene variabilidad (rangos Mín/Probable/Máx) y es el insumo principal del Monte Carlo. Concentra el riesgo de sobrecosto no contratado aún.')}
        ${kpiCard('EAT Determinístico', U(T.eat), Pct(T.pctEAT)+' del CAPEX',
            kpiEAT[0], kpiEAT[1], kpiEAT[2], 'flag',
            'EAT = Comprometido + Por Comprometer. Proyección base del costo final sin contingencias probabilísticas. El Monte Carlo genera el EAT probabilístico (P50/P80/P90) sobre esta base.')}
        ${kpiCard(slackPos ? 'Slack' : 'Déficit', U(Math.abs(T.slack??0)),
            slackPos ? `Margen disponible · ${Pct(pctSlackVsCapex)} del CAPEX` : `Déficit proyectado · ${Pct(pctSlackVsCapex)} del CAPEX`,
            kpiSlack[0], kpiSlack[1], kpiSlack[2],
            slackPos ? 'check_circle' : 'warning',
            slackPos
                ? 'Slack = CAPEX − EAT Det. Margen presupuestario disponible antes de superar el techo autorizado. El % es |Slack| sobre el CAPEX total. Un Slack amplio absorbe mejor los riesgos no modelados.'
                : 'Déficit = EAT Det. − CAPEX. El % es el déficit sobre el CAPEX total (cuánto supera el EAT base al techo aprobado). Requiere revisión de alcance o solicitud de ajuste presupuestario urgente.')}
    </div>`;

    // Exposure bar — overflow-aware
    const pctComp    = (T.comprometido / capex) * 100;
    const pctPorComp = (T.porComprometer / capex) * 100;
    const pctEATv    = pctComp + pctPorComp;
    const maxScale   = Math.max(Math.ceil(pctEATv / 20) * 20 + 15, 115);
    const compBarW   = (pctComp / maxScale * 100).toFixed(1);
    const porCBarW   = (pctPorComp / maxScale * 100).toFixed(1);
    const capexLeft  = (100 / maxScale * 100).toFixed(1);
    const ragLabel   = T.pctEAT > 115 ? '● SOBRE PRESUPUESTO' : T.pctEAT > 105 ? '● EN ALERTA' :
                       T.pctEAT > 95  ? '● DENTRO DE PRESUPUESTO' : '● BAJO PRESUPUESTO';
    const ragColor   = T.pctEAT > 115 ? 'text-red-600' : T.pctEAT > 105 ? 'text-amber-600' :
                       T.pctEAT > 95  ? 'text-teal-600' : 'text-emerald-600';

    const sec1Bar = `
    <div class="tc-card mb-6 !py-4 border-slate-200/80 dark:border-slate-700/80" data-tc-sec="exposicion-capex">
        <div class="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between mb-3">
            <div>
                <span class="text-xs font-semibold text-slate-600 dark:text-slate-300 flex items-center gap-1">
                    Exposición financiera vs CAPEX
                    ${TT('Misma información que las tarjetas de arriba: Comprometido + Por comprometer frente al CAPEX total. Los colores de la barra coinciden con las tarjetas (verde = comprometido, azul = por comprometer). La línea vertical índigo = 100% del CAPEX. La zona sombreada a la derecha de esa línea es escala por encima del techo aprobado.', 'gpr-tt-right')}
                </span>
                <p class="text-[11px] text-slate-400 dark:text-slate-500 mt-1 leading-snug">
                    Proporción respecto al CAPEX · <span class="text-emerald-600 dark:text-emerald-400 font-medium">${U(T.comprometido)}</span>
                    <span class="text-slate-300 dark:text-slate-600 mx-0.5">+</span>
                    <span class="text-blue-600 dark:text-blue-400 font-medium">${U(T.porComprometer)}</span>
                    <span class="text-slate-300 dark:text-slate-600 mx-0.5">=</span>
                    <span class="${T.pctEAT > 100 ? 'text-amber-600 dark:text-amber-400' : 'text-slate-600 dark:text-slate-300'} font-medium">${U(T.eat)}</span>
                    <span class="text-slate-400"> (${Pct(pctEATv)} CAPEX)</span>
                </p>
            </div>
            <span class="text-xs font-bold ${ragColor} shrink-0 sm:text-right">${ragLabel}</span>
        </div>
        <div class="flex justify-between text-[10px] uppercase tracking-wide text-slate-400 mb-0.5 tabular-nums">
            <span>0</span>
            <span>${Pct(maxScale)} del CAPEX (escala)</span>
        </div>
        <p class="text-[10px] text-slate-400 dark:text-slate-500 mb-1">Pasa el cursor sobre la barra para ver cada tramo, el techo CAPEX o la zona de exceso.</p>
        <div id="tc-expo-bar-track" class="w-full bg-slate-100 dark:bg-slate-700 rounded-full h-9 relative overflow-hidden shadow-inner ring-1 ring-inset ring-slate-200/60 dark:ring-slate-600/50">
            ${pctEATv > 100 ? `<div class="absolute inset-y-0 right-0 z-0 rounded-r-full bg-red-500/15 dark:bg-red-500/20 pointer-events-none"
                 style="left:${capexLeft}%"></div>` : ''}
            <div class="absolute top-0 left-0 h-9 rounded-l-full z-[1] pointer-events-none"
                 style="width:${compBarW}%;background:linear-gradient(90deg,#059669,#34d399)"></div>
            <div class="absolute top-0 h-9 z-[1] pointer-events-none"
                 style="left:${compBarW}%;width:${porCBarW}%;background:linear-gradient(90deg,#2563eb,#60a5fa);${parseFloat(porCBarW) > 0 ? 'border-left:2px solid rgba(255,255,255,0.35)' : ''}"></div>
            <div class="absolute top-0 bottom-0 w-1.5 z-[2] rounded-full bg-indigo-500 dark:bg-indigo-400 shadow-[0_0_0_1px_rgba(255,255,255,0.35)] pointer-events-none"
                 style="left:${capexLeft}%;transform:translateX(-50%)" title="100% CAPEX"></div>
            <div id="tc-expo-bar-hit" class="absolute inset-0 z-[3] rounded-full cursor-crosshair" style="background:transparent" title=""></div>
        </div>
        <div class="flex flex-wrap items-center gap-x-4 gap-y-1 mt-2.5 text-xs text-slate-500 dark:text-slate-400">
            <span class="flex items-center gap-1"><span class="inline-block w-3 h-3 rounded-sm bg-emerald-500 shrink-0"></span>Comprometido ${Pct(pctComp)}</span>
            <span class="flex items-center gap-1"><span class="inline-block w-3 h-3 rounded-sm bg-blue-500 shrink-0"></span>Por comprometer ${Pct(pctPorComp)}</span>
            ${pctEATv > 100 ? `<span class="text-red-600 dark:text-red-400 font-medium">Exceso sobre CAPEX ${Pct(pctEATv-100)}</span>` : `<span class="text-emerald-600 dark:text-emerald-500 font-medium">Margen bajo techo: ${Pct(100-pctEATv)} del CAPEX</span>`}
            <span class="sm:ml-auto flex items-center gap-1 text-slate-500"><span class="inline-block w-4 border-t-2 border-dashed border-indigo-500 align-middle"></span>100% = CAPEX ${U(T.capex)}</span>
        </div>
    </div>`;

    const sec1 = sec1KpiCards + sec1Bar;

    // ══════ SECCIÓN 2 — Análisis de Incertidumbre ══════════════════════════

    const sec2Header = `
    <div class="flex items-center gap-3 mb-4">
        <div class="w-1 h-6 bg-blue-500 rounded-full flex-shrink-0"></div>
        <h2 class="text-base font-semibold text-slate-800 dark:text-slate-200 flex items-center gap-1">
            Análisis de Incertidumbre
            ${TT('Cuantifica la variabilidad del costo total por ítems pendientes de contratar (Por Comprometer). Incluye distribución Certeza/Incertidumbre, Clase de Estimación AACE y el EAT probabilístico (P10–P90) por contrato. Permite identificar cuánto del presupuesto está expuesto a variabilidad antes de contratar.', 'gpr-tt-right')}
        </h2>
        <span class="text-xs text-slate-400 bg-slate-100 dark:bg-slate-800 px-2 py-0.5 rounded-full ml-auto">
            ${T.totalItems} ítems · ${T.contratosMcOk}/${data.contratos.length} contratos con MC
        </span>
    </div>`;

    // ── Helpers fila y grupo para tabla de Estado por Contrato ───────────────
    const RPAL = ['#14b8a6','#6366f1','#f59e0b','#10b981','#3b82f6','#ec4899','#8b5cf6','#f97316'];

    const buildContratoRow = (c, accentColor = '#14b8a6', extraAttrs = '') => {
        const pct = c.pctEAT ?? 0;
        const rowBg = pct > 115 ? 'bg-red-50/40 dark:bg-red-900/10'
                    : pct > 105 ? 'bg-amber-50/40 dark:bg-amber-900/10' : '';
        const dot = pct <= 95  ? '<span class="text-emerald-500">●</span>'
                  : pct <= 105 ? '<span class="text-teal-500">●</span>'
                  : pct <= 115 ? '<span class="text-amber-500">●</span>'
                  :              '<span class="text-red-500">●</span>';
        const mcBadge = c.hasMc
            ? `<span class="text-xs px-1.5 py-0.5 rounded bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-300">MC P50: ${U((c.mcP50??0)+(c.certTotal??0)+(c.comprometido??0))}</span>`
            : `<span class="text-xs px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-700 text-slate-400">Sin MC</span>`;
        const cvVal   = c.mcCV ?? null;
        const cvColor = cvVal == null ? '' : cvVal > 50 ? 'text-red-500 font-semibold' : cvVal > 20 ? 'text-amber-500 font-semibold' : 'text-emerald-600 font-semibold';
        const cvTip   = cvVal == null ? '' : cvVal > 50
            ? 'CV &gt;50% — Incertidumbre ALTA: usar P90 como base presupuestaria.'
            : cvVal > 20 ? 'CV 20–50% — Incertidumbre MEDIA: planificar con P75.'
            : 'CV &lt;20% — Incertidumbre BAJA: P50 es un estimado sólido.';
        const cv = cvVal != null
            ? `<span class="gpr-tt"><span class="${cvColor} text-xs ml-1">CV: ${fmtCV(cvVal)}</span><span class="gpr-tt-box" style="color:#f8fafc">${cvTip}</span></span>`
            : '';
        // Δ Incertidumbre = P50 MC − Suma probable ítems inciertos (sólo si hay MC guardado)
        const delta    = c.hasMc ? (c.mcP50 ?? 0) - (c.incertTotal ?? 0) : null;
        const deltaCls = delta == null ? 'text-slate-300 dark:text-slate-600'
                       : delta > 0    ? 'text-red-600 dark:text-red-400 font-semibold'
                       : delta < 0    ? 'text-emerald-600 dark:text-emerald-400 font-semibold'
                       :                'text-slate-400';
        const deltaHtml = delta == null
            ? '<span class="text-slate-300 dark:text-slate-600">—</span>'
            : `<span class="${deltaCls}">${delta >= 0 ? '+' : ''}${U(delta)}</span>`;
        return `<tr ${extraAttrs} class="border-b border-slate-100 dark:border-slate-800 ${rowBg} hover:bg-teal-50/50 dark:hover:bg-teal-900/10 cursor-pointer transition-colors"
                    onclick="tcResumenIrContrato('${c.id}')"
                    title="Ir al contrato ${esc(c.codigo)}">
            <td class="py-1.5 pl-8 pr-3 text-xs font-mono font-semibold" style="border-left:3px solid ${accentColor}30">
                <span class="inline-flex items-center gap-1">${esc(c.codigo)}<span class="material-icons text-teal-400 opacity-0 group-hover:opacity-100" style="font-size:11px">open_in_new</span></span>
            </td>
            <td class="py-1.5 px-3 text-xs">${esc(c.nombre)}</td>
            <td class="py-1.5 px-3 text-xs text-right tabular-nums">${U(c.capex)}</td>
            <td class="py-1.5 px-3 text-xs text-right tabular-nums">${U(c.comprometido)} <span class="text-slate-400">${Pct(c.pctComprometido)}</span></td>
            <td class="py-1.5 px-3 text-xs text-right tabular-nums">${U(c.porComprometer)}</td>
            <td class="py-1.5 px-3 text-xs text-right font-semibold tabular-nums">${U(c.eat)}</td>
            <td class="py-1.5 px-3 text-xs text-center">${dot} ${Pct2(pct)}</td>
            <td class="py-1.5 px-3 text-xs text-right tabular-nums">${deltaHtml}</td>
            <td class="py-1.5 px-3 text-xs">${mcBadge}${cv}</td>
        </tr>`;
    };

    const buildGrupoResumen = (nombre, conts, accentColor, icono = 'folder', esVacia = false, famKey = 'grp') => {
        if (esVacia) return `
            <tr><td colspan="9" style="border-left:4px solid ${accentColor}40"
                class="py-2.5 pl-4 pr-3 bg-slate-50/50 dark:bg-slate-800/20 border-b border-slate-100 dark:border-slate-800">
                <div class="flex items-center gap-2">
                    <span class="material-icons" style="font-size:14px;color:${accentColor}70">${icono}</span>
                    <span class="text-xs font-semibold" style="color:${accentColor}80">${esc(nombre)}</span>
                    <span class="text-xs italic text-slate-300 dark:text-slate-600">Sin contratos asignados</span>
                </div>
            </td></tr>`;

        const tC = conts.reduce((s,c) => s+(c.capex??0), 0);
        const tK = conts.reduce((s,c) => s+(c.comprometido??0), 0);
        const tP = conts.reduce((s,c) => s+(c.porComprometer??0), 0);
        const tE = conts.reduce((s,c) => s+(c.eat??0), 0);
        const pp = tC > 0 ? tE/tC*100 : 0;
        const rc = pp > 115 ? '#ef4444' : pp > 105 ? '#f59e0b' : '#14b8a6';
        const mcOk = conts.filter(c => c.hasMc).length;
        const isCol = !!_tcFamCollapsed[famKey];

        const header = `
            <tr style="cursor:pointer;user-select:none" onclick="tcToggleFam('${famKey}')">
                <td colspan="9" style="border-left:4px solid ${accentColor}"
                    class="py-2.5 pl-3 pr-4 bg-slate-50 dark:bg-slate-800/60 border-y border-slate-200 dark:border-slate-700">
                <div class="flex items-center justify-between flex-wrap gap-2">
                    <div class="flex items-center gap-1.5">
                        <span id="fam-chev-${famKey}" class="material-icons text-slate-400 transition-transform duration-200"
                              style="font-size:18px;${isCol ? 'transform:rotate(-90deg)' : ''}">expand_more</span>
                        <span class="material-icons" style="font-size:16px;color:${accentColor}">${icono}</span>
                        <span class="text-sm font-bold text-slate-700 dark:text-slate-100">${esc(nombre)}</span>
                        <span class="text-xs font-medium px-1.5 py-0.5 rounded-full" style="background:${accentColor}20;color:${accentColor}">
                            ${conts.length} contrato${conts.length!==1?'s':''}
                        </span>
                        ${isCol ? `<span class="text-xs text-slate-400 italic">(colapsado)</span>` : ''}
                    </div>
                    <div class="flex items-center gap-3 text-xs tabular-nums">
                        <span class="text-slate-400">CAPEX <strong class="text-slate-600 dark:text-slate-300">${U(tC)}</strong></span>
                        <span class="text-slate-400">EAT <strong class="text-slate-600 dark:text-slate-300">${U(tE)}</strong></span>
                        <span class="font-semibold px-2 py-0.5 rounded" style="background:${rc}15;color:${rc}">${Pct2(pp)}</span>
                        ${mcOk > 0 ? `<span class="text-purple-500 text-xs">${mcOk} con MC</span>` : ''}
                    </div>
                </div>
            </td></tr>`;

        const rows = conts.map(c => buildContratoRow(
            c, accentColor,
            `data-fam="${famKey}"${isCol ? ' style="display:none"' : ''}`
        )).join('');

        const tDelta  = conts.reduce((s,c) => c.hasMc ? s + (c.mcP50??0) - (c.incertTotal??0) : s, 0);
        const tDeltaHasMc = conts.some(c => c.hasMc);
        const tDeltaCls = tDelta > 0 ? 'text-red-600 font-semibold' : tDelta < 0 ? 'text-emerald-600 font-semibold' : 'text-slate-400';
        const subtotal = `
            <tr class="border-b-2 border-slate-200 dark:border-slate-700">
                <td colspan="2" style="border-left:4px solid ${accentColor}40"
                    class="py-1.5 pl-8 pr-3 text-xs font-semibold text-slate-400 bg-slate-50/70 dark:bg-slate-800/30 uppercase tracking-wide">
                    Subtotal ${esc(nombre)}
                </td>
                <td class="py-1.5 px-3 text-xs text-right font-semibold tabular-nums text-slate-600 dark:text-slate-300 bg-slate-50/70 dark:bg-slate-800/30">${U(tC)}</td>
                <td class="py-1.5 px-3 text-xs text-right tabular-nums text-slate-500 bg-slate-50/70 dark:bg-slate-800/30">${U(tK)}</td>
                <td class="py-1.5 px-3 text-xs text-right tabular-nums text-slate-500 bg-slate-50/70 dark:bg-slate-800/30">${U(tP)}</td>
                <td class="py-1.5 px-3 text-xs text-right font-bold tabular-nums bg-slate-50/70 dark:bg-slate-800/30" style="color:${rc}">${U(tE)}</td>
                <td class="py-1.5 px-3 text-xs text-center font-semibold bg-slate-50/70 dark:bg-slate-800/30" style="color:${rc}">${Pct2(pp)}</td>
                <td class="py-1.5 px-3 text-xs text-right tabular-nums bg-slate-50/70 dark:bg-slate-800/30">${tDeltaHasMc ? `<span class="${tDeltaCls}">${tDelta>=0?'+':''}${U(tDelta)}</span>` : '<span class="text-slate-300">—</span>'}</td>
                <td class="py-1.5 px-3 text-xs bg-slate-50/70 dark:bg-slate-800/30 text-slate-400">${mcOk} con MC</td>
            </tr>`;

        return header + rows + subtotal;
    };

    // Construir secciones agrupadas
    const famList = data.familias || [];
    let gruposHtml = '';
    famList.forEach((f, i) => {
        const color  = RPAL[i % RPAL.length];
        const conts  = data.contratos.filter(c => c.familiaId === f.id);
        const famKey = `fam-${f.id}`;
        gruposHtml  += buildGrupoResumen(f.nombre, conts, color, 'folder', conts.length === 0, famKey);
    });
    const sinFamConts = data.contratos.filter(c => !c.familiaId);
    if (sinFamConts.length) {
        gruposHtml += buildGrupoResumen('Sin Familia', sinFamConts, '#94a3b8', 'folder_off', false, 'sin-fam');
    }
    if (!gruposHtml) {
        gruposHtml = '<tr><td colspan="8" class="text-center py-6 text-slate-400">Sin contratos</td></tr>';
    }

    const contratoTable = `
    <div class="tc-card mb-4">
        <h3 class="font-semibold text-slate-700 dark:text-slate-200 flex items-center gap-2 mb-3 text-sm">
            <span class="material-icons text-indigo-500 text-base">receipt_long</span> Estado por Contrato
            ${TT('Tabla comparativa agrupada por familia.&#10;&#10;Semáforo EAT/CAPEX: ● Verde ≤95% · ● Teal 95–105% · ● Ámbar 105–115% · ● Rojo &gt;115%.&#10;&#10;Δ Incert.: diferencia entre el P50 MC y la suma probable de ítems inciertos. Rojo = distribución sesgada al alza; Verde = sesgada a la baja.&#10;&#10;Columna MC: EAT P50 probabilístico del contrato. CV = Desv.Estándar / Media (Bajo &lt;20% · Medio 20–50% · Alto &gt;50%).', 'gpr-tt-left')}
        </h3>
        <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
            <thead><tr class="border-b border-slate-200 dark:border-slate-700 text-xs text-slate-500 uppercase tracking-wide">
                <th class="py-2 pl-8 pr-3">Código</th><th class="py-2 px-3">Contrato</th>
                <th class="py-2 px-3 text-right">CAPEX</th><th class="py-2 px-3 text-right">Comprometido</th>
                <th class="py-2 px-3 text-right">Por Comp.</th><th class="py-2 px-3 text-right">EAT</th>
                <th class="py-2 px-3 text-center">EAT/CAPEX</th>
                <th class="py-2 px-3 text-right">${TT('Diferencia entre el P50 simulado (MC Bloque F) y la suma probable de ítems inciertos incluidos en ese MC (excluye vía riesgo). Positivo = sesgo al alza; negativo = a la baja.', 'gpr-tt-left')}Δ Incert.</th>
                <th class="py-2 px-3">${TT('MC P50: EAT total estimado (comprometido + certeza por comp. + P50 del tramo simulado). CV = Desv.Estándar/Media.', 'gpr-tt-left')}MC</th>
            </tr></thead>
            <tbody>${gruposHtml}</tbody>
            <tfoot class="border-t-2 border-slate-300 dark:border-slate-600 font-semibold bg-slate-100 dark:bg-slate-800">
                <tr>
                    <td class="py-2 pl-4 pr-3 text-xs" colspan="2">TOTAL PROYECTO <span class="font-normal text-slate-400">(${data.contratos.length} contratos)</span></td>
                    <td class="py-2 px-3 text-xs text-right tabular-nums">${U(T.capex)}</td>
                    <td class="py-2 px-3 text-xs text-right tabular-nums">${U(T.comprometido)}</td>
                    <td class="py-2 px-3 text-xs text-right tabular-nums">${U(T.porComprometer)}</td>
                    <td class="py-2 px-3 text-xs text-right tabular-nums ${ragColor}">${U(T.eat)}</td>
                    <td class="py-2 px-3 text-xs text-center ${ragColor}">${Pct2(T.pctEAT)}</td>
                    <td class="py-2 px-3 text-xs text-right tabular-nums">${(() => {
                        const tot = data.contratos.reduce((s,c) => c.hasMc ? s + (c.mcP50??0) - (c.incertTotal??0) : s, 0);
                        const any = data.contratos.some(c => c.hasMc);
                        if (!any) return '<span class="text-slate-300">—</span>';
                        const cls = tot > 0 ? 'text-red-600' : tot < 0 ? 'text-emerald-600' : 'text-slate-400';
                        return `<span class="${cls}">${tot>=0?'+':''}${U(tot)}</span>`;
                    })()}</td>
                    <td class="py-2 px-3 text-xs text-slate-400">${T.contratosMcOk} con MC</td>
                </tr>
            </tfoot>
        </table>
        </div>
    </div>`;

    // Item composition + clases + top-3
    const pctCert = T.pctCerteza ?? 0;
    const pctInc  = 100 - pctCert;
    const clasesAgg = {};
    data.contratos.forEach(c => (c.clases||[]).forEach(cl => {
        if (!clasesAgg[cl.clase]) clasesAgg[cl.clase] = { count: 0, costoUsd: 0 };
        clasesAgg[cl.clase].count    += cl.count;
        clasesAgg[cl.clase].costoUsd += cl.costoUsd;
    }));
    const totalItemsClase = Object.values(clasesAgg).reduce((s, v) => s + v.count, 0);
    const clasesHtml = Object.entries(clasesAgg)
        .sort(([a],[b]) => a.localeCompare(b))
        .map(([clase, v]) => {
            const pctC = totalItemsClase > 0 ? v.count / totalItemsClase * 100 : 0;
            return `<div class="mb-2">
                <div class="flex justify-between text-xs mb-1">
                    <span class="text-slate-600 dark:text-slate-400">${esc(clase)} <span class="text-slate-400">${v.count} ítems</span></span>
                    <span class="font-semibold tabular-nums">${Pct(pctC)}</span>
                </div>
                <div class="h-2 bg-slate-100 dark:bg-slate-700 rounded-full overflow-hidden">
                    <div class="h-2 rounded-full bg-teal-400" style="width:${Math.min(pctC,100).toFixed(1)}%"></div>
                </div>
            </div>`;
        }).join('');

    const allTopRisk = [];
    data.contratos.forEach(c => (c.topRisk||[]).forEach(r => allTopRisk.push({...r, contrato: c.codigo})));
    allTopRisk.sort((a,b) => b.spread - a.spread);
    const top3RiskHtml = allTopRisk.slice(0,3).map(r => `
        <div class="mb-2 pb-2 border-b border-amber-100 dark:border-amber-900/30 last:border-0 last:mb-0 last:pb-0">
            <div class="text-xs font-medium text-slate-600 dark:text-slate-400">${esc(r.contrato)} · ${esc(r.codigoItem)}</div>
            <div class="text-xs text-slate-500 truncate mb-1">${esc(r.descripcion)}</div>
            <div class="flex justify-between text-xs">
                <span class="text-blue-500 tabular-nums">Mín ${U(r.minUsd)}</span>
                <span class="font-semibold text-amber-600 tabular-nums">Spread ${U(r.spread)}</span>
            </div>
        </div>`).join('') || '<p class="text-xs text-slate-400">Sin ítems de riesgo</p>';

    const sec2Analytics = `
    <details class="tc-minimized-disclosure mb-4">
        <summary class="flex items-center gap-3 cursor-pointer select-none flex-wrap">
            <span class="material-icons tc-minimized-section-chevron" aria-hidden="true">expand_more</span>
            <div class="w-1 h-6 bg-cyan-500 rounded-full flex-shrink-0"></div>
            <h3 class="text-base font-semibold text-slate-800 dark:text-slate-200 flex items-center gap-1">
                Composición, clases AACE y Top 3 exposición
                ${TT('Tres paneles: mezcla Certeza/Incertidumbre del costo, distribución de ítems por clase de estimación AACE, y los tres ítems con mayor spread (Máx − Mín) entre todos los contratos. Útil para ver madurez de la estimación y dónde concentrar definición de alcance.', 'gpr-tt-right')}
            </h3>
            <span class="text-xs text-slate-500 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 px-2 py-0.5 rounded-full ml-auto tabular-nums">
                ${Pct(pctInc)} incert. · ${totalItemsClase} ítems en clases${allTopRisk.length ? ` · spread máx. ${U(allTopRisk[0].spread)}` : ''}
            </span>
        </summary>
        <div class="mt-3">
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div class="tc-card">
            <div class="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-3 flex items-center gap-1">
                Composición de ítems
                ${TT('Certeza: ítems con costo fijo, sin variabilidad (no alimentan el MC). Incertidumbre: ítems con rangos Mín/Probable/Máx que el Monte Carlo simula. Un % alto de incertidumbre amplifica el spread P10–P90 del análisis probabilístico.', 'gpr-tt-right')}
            </div>
            <div class="mb-3">
                <div class="flex justify-between text-xs mb-1">
                    <span class="text-slate-600 dark:text-slate-400">Certeza</span>
                    <span class="font-semibold">${Pct(pctCert)}</span>
                </div>
                <div class="h-2.5 bg-slate-100 dark:bg-slate-700 rounded-full overflow-hidden">
                    <div class="h-2.5 rounded-full bg-slate-500" style="width:${pctCert.toFixed(1)}%"></div>
                </div>
            </div>
            <div class="mb-4">
                <div class="flex justify-between text-xs mb-1">
                    <span class="text-slate-600 dark:text-slate-400">Incertidumbre</span>
                    <span class="font-semibold text-amber-600">${Pct(pctInc)}</span>
                </div>
                <div class="h-2.5 bg-slate-100 dark:bg-slate-700 rounded-full overflow-hidden">
                    <div class="h-2.5 rounded-full" style="width:${pctInc.toFixed(1)}%;background:linear-gradient(90deg,#f59e0b,#fcd34d)"></div>
                </div>
            </div>
            <div class="grid grid-cols-2 gap-2 pt-3 border-t border-slate-100 dark:border-slate-700">
                ${tcResMetric(T.totalItems, 'ítems totales', 'text-slate-700 dark:text-slate-300')}
                ${tcResMetric(Pct(pctInc), 'exposición incert.', 'text-amber-600')}
                ${tcResMetric(U(T.certeza), 'costo certeza', 'text-slate-600')}
                ${tcResMetric(U(T.incertidumbre), 'costo incertidumbre', 'text-amber-600')}
            </div>
        </div>
        <div class="tc-card">
            <div class="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-3 flex items-center gap-1">
                Distribución por Clase de Estimación
                ${TT('Clase AACE 1 (±3–15%): ingeniería de detalle completada. Clase 5 (±50%): orden de magnitud, muy poca definición. El mix de clases revela la madurez global de la estimación y cuánta variabilidad esperar en el Monte Carlo.', 'gpr-tt-right')}
            </div>
            ${clasesHtml || '<p class="text-xs text-slate-400">Sin datos de clase</p>'}
            <p class="text-xs text-slate-400 mt-2 pt-2 border-t border-slate-100 dark:border-slate-700">Clase 1 = alta precisión · Clase 5 = orden de magnitud</p>
        </div>
        <div class="tc-card border border-amber-200 dark:border-amber-900/50 bg-amber-50/30 dark:bg-amber-900/10">
            <div class="text-xs font-semibold text-amber-700 dark:text-amber-400 uppercase tracking-wide mb-3 flex items-center gap-1">
                ⚠ Top 3 — Mayor exposición
                ${TT('Los tres ítems de incertidumbre con mayor spread (Máx − Mín). Concentrar el esfuerzo de definición de alcance en estos ítems reduce significativamente la variabilidad total del proyecto y mejora la clase de estimación.', 'gpr-tt-left')}
            </div>
            ${top3RiskHtml}
        </div>
    </div>
        </div>
    </details>`;

    // MC probabilístico solo incertidumbre (sin riesgos)
    // Preferir percentiles de la MISMA simulación conjunta que «Incertidumbre + Riesgos» (eatSoloItemsP*).
    // La suma de percentiles por contrato (T.eatP*) suele SOBRESTIMAR P80/P90 vs una cartera conjunta (@RISK / simulación única).
    let mcOnlySection = '';
    const useJointSoloMc = EC != null && typeof EC.eatSoloItemsP50 === 'number';
    if (T.mcP50 != null || useJointSoloMc) {
        const cobert = data.contratos.length > 0 ? (T.contratosMcOk / data.contratos.length * 100) : 0;
        const p10Nr = useJointSoloMc ? (EC.eatSoloItemsP10 ?? T.eatP10 ?? 0) : (T.eatP10 ?? 0);
        const p50Nr = useJointSoloMc ? (EC.eatSoloItemsP50 ?? T.eatP50 ?? 0) : (T.eatP50 ?? 0);
        const p80Nr = useJointSoloMc ? (EC.eatSoloItemsP80 ?? T.eatP80 ?? 0) : (T.eatP80 ?? 0);
        const p90Nr = useJointSoloMc ? (EC.eatSoloItemsP90 ?? T.eatP90 ?? 0) : (T.eatP90 ?? 0);
        const eatDetNr = T.eat ?? 0;
        const barScaleNr = Math.max(capex * 1.06, eatDetNr, p10Nr, p50Nr, p80Nr, p90Nr, 1);
        const tickCapexNr = barScaleNr > 0 ? Math.min(100, (capex / barScaleNr) * 100) : 0;
        const rowMcNoRisk = (label, val, grad, labelCls, delta, pctCap) => {
            const v = val ?? 0;
            const w = barScaleNr > 0 ? Math.min(100, (v / barScaleNr) * 100) : 0;
            return `<tr class="border-b border-slate-100 dark:border-slate-800">
                <td class="py-1 px-1.5 text-[11px] font-medium ${labelCls}">${label}</td>
                <td class="py-1 px-1">
                    <div class="relative h-3 w-full rounded-full bg-slate-100 dark:bg-slate-700 overflow-hidden">
                        <div class="h-full rounded-full" style="width:${w.toFixed(1)}%;background:${grad};opacity:.92"></div>
                        <div class="absolute top-0 bottom-0 w-0.5 bg-indigo-500 z-[1] shadow-sm" style="left:calc(${tickCapexNr.toFixed(2)}% - 1px)" title="Referencia CAPEX"></div>
                    </div>
                </td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums font-semibold">${U(v)}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums ${delta >= 0 ? 'text-red-600 dark:text-red-400' : 'text-emerald-600 dark:text-emerald-400'}">${delta >= 0 ? '+' : ''}${U(Math.abs(delta))}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${Pct2(pctCap)}</td>
            </tr>`;
        };
        const mcOnlyMeta = useJointSoloMc
            ? `<span class="text-xs font-normal text-teal-600 dark:text-teal-400 ml-auto">Simulación conjunta · ${tcFmt(EC.nSimulaciones)} iter</span>`
            : `<span class="text-xs font-normal text-slate-400 ml-auto">${T.contratosMcOk}/${data.contratos.length} contratos · ${Pct(cobert)}</span>`;
        const mcOnlyFoot = useJointSoloMc
            ? 'Misma base y mismos triángulos que «EAT Probabilístico — Incertidumbre + Riesgos», sin Bernoulli de riesgos. Comparable fila a fila con la siguiente sección.'
            : 'Percentiles = suma de los P10–P90 guardados por contrato (Bloque F). Suele dar colas más altas que una simulación conjunta de todos los ítems; al ejecutar análisis combinado se usa la vista conjunta arriba.';
        mcOnlySection = `
        <div class="tc-card mb-4">
            <h3 class="font-semibold text-slate-700 dark:text-slate-200 flex items-center gap-2 mb-3 text-sm">
                <span class="material-icons text-purple-500 text-base">casino</span> Monte Carlo Agregado — Sólo Incertidumbre
                ${mcOnlyMeta}
                ${TT(useJointSoloMc
                    ? 'Una sola simulación Monte Carlo con todos los ítems inciertos «Por comprometer» (triángulos), misma corrida que alimenta «Incertidumbre + Riesgos» pero sin activar riesgos. Es la referencia correcta para comparar con P10–P90 de esa sección (mismo modelo que Excel @RISK en un solo libro).'
                    : 'Suma de los percentiles del MC guardado por contrato más base fija. No es estadísticamente equivalente a simular toda la cartera junta: sumar P90 contrato a contrato suele sobrestimar la cola frente a un modelo conjunto.', 'gpr-tt-left')}
            </h3>
            <div class="grid grid-cols-1 lg:grid-cols-12 gap-4 lg:gap-5 lg:items-start">
                <div class="lg:col-span-9 min-w-0">
                    <p class="text-xs text-slate-500 mb-2">EAT probabilístico sin riesgos (comprometido + certeza + incertidumbre MC)${useJointSoloMc ? ' — <span class="text-teal-600 dark:text-teal-400 font-medium">cartera conjunta</span>' : ''}
                        ${TT('Cada fila P10–P90 es un <strong>percentil</strong> de la distribución simulada, no «EAT det. + un fijo». El EAT determinístico usa el <strong>probable</strong> de cada ítem; el <strong>P10</strong> es un escenario optimista (muchos sorteos cerca del mínimo del triángulo), por eso <strong>puede ser menor</strong> que el EAT det. — es esperable. La <strong>media</strong> de la simulación suele acercarse al det.; para contingencia vs. det. use P50–P90 o mire la media en el panel derecho.', 'gpr-tt-left')}
                    </p>
                    <div class="rounded-lg border border-slate-200 dark:border-slate-700 min-w-0 overflow-hidden">
                        <table class="w-full text-left border-collapse table-fixed">
                            <colgroup>
                                <col style="width:14%" /><col style="width:42%" /><col style="width:19%" /><col style="width:13%" /><col style="width:12%" />
                            </colgroup>
                            <thead>
                                <tr class="border-b border-slate-200 dark:border-slate-700 text-[9px] leading-tight text-slate-500 uppercase tracking-wide bg-slate-50/80 dark:bg-slate-800/50">
                                    <th class="py-1.5 px-1 text-left">Concepto</th>
                                    <th class="py-1.5 px-1">Barra</th>
                                    <th class="py-1.5 px-1 text-right">Monto</th>
                                    <th class="py-1.5 px-1 text-right">${TT('Diferencia vs EAT determinístico (suma de probables «Por comprometer»). Negativo en P10 = cola optimista; no indica error.', 'gpr-tt-right')}Δ vs EAT det.</th>
                                    <th class="py-1.5 px-1 text-right">% / CAPEX</th>
                                </tr>
                            </thead>
                            <tbody>
                                ${rowMcNoRisk('P10', p10Nr, 'linear-gradient(90deg,#93c5fd,#3b82f6)', 'text-blue-600', p10Nr - eatDetNr, capex > 0 ? (p10Nr / capex) * 100 : 0)}
                                ${rowMcNoRisk('P50', p50Nr, 'linear-gradient(90deg,#6ee7b7,#10b981)', 'text-emerald-600', p50Nr - eatDetNr, capex > 0 ? (p50Nr / capex) * 100 : 0)}
                                ${rowMcNoRisk('P80', p80Nr, 'linear-gradient(90deg,#fcd34d,#f59e0b)', 'text-amber-600', p80Nr - eatDetNr, capex > 0 ? (p80Nr / capex) * 100 : 0)}
                                ${rowMcNoRisk('P90', p90Nr, 'linear-gradient(90deg,#fca5a5,#ef4444)', 'text-red-600', p90Nr - eatDetNr, capex > 0 ? (p90Nr / capex) * 100 : 0)}
                            </tbody>
                        </table>
                    </div>
                    <div class="flex gap-4 text-xs mt-2 flex-wrap">
                        <span><span class="inline-block w-5 border-t-2 border-dashed border-indigo-400 mr-1 align-middle"></span>Referencia CAPEX</span>
                        <span class="text-slate-400">${esc(mcOnlyFoot)}</span>
                    </div>
                </div>
                <div class="lg:col-span-3 min-w-0 space-y-1 text-sm">
                    ${useJointSoloMc && typeof EC.eatSoloItemsMedia === 'number' ? tcResRow('Media MC (solo ítems)', U(EC.eatSoloItemsMedia), 'text-slate-700 dark:text-slate-200') : ''}
                    ${useJointSoloMc && typeof EC.eatSoloItemsMedia === 'number' ? tcResRow('Δ Media − EAT det.', U(EC.eatSoloItemsMedia - eatDetNr), EC.eatSoloItemsMedia >= eatDetNr ? 'text-amber-600' : 'text-emerald-600') : ''}
                    ${tcResRow('Contingencia P50→P80', U(p80Nr - p50Nr), 'text-amber-600')}
                    ${tcResRow('Spread P10→P90', U(p90Nr - p10Nr), 'text-purple-600')}
                    ${tcResRow('P50 vs CAPEX', (p50Nr > T.capex ? '+' : '') + Pct((p50Nr - T.capex) / capex * 100), p50Nr <= T.capex ? 'text-emerald-600' : 'text-red-600')}
                    ${!useJointSoloMc && T.contratosSinMc > 0 ? `<div class="mt-2 p-2 rounded bg-amber-50 dark:bg-amber-900/20 text-xs text-amber-700"><span class="material-icons text-xs align-middle">warning_amber</span> ${T.contratosSinMc} contrato${T.contratosSinMc>1?'s':''} sin MC — EAT probabilístico subestimado</div>` : ''}
                </div>
            </div>
        </div>`;
    }

    const sec2 = sec2Header + contratoTable + sec2Analytics + mcOnlySection;

    // ══════ SECCIÓN 3 — Análisis de Riesgos ════════════════════════════════

    let sec3 = '';
    {
        const nivelColors = { red:'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300 border-red-200 dark:border-red-800',
            orange:'bg-orange-100 dark:bg-orange-900/30 text-orange-700 dark:text-orange-300 border-orange-200 dark:border-orange-800',
            amber:'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-300 border-amber-200 dark:border-amber-800',
            blue:'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 border-blue-200 dark:border-blue-800',
            slate:'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 border-slate-200 dark:border-slate-700' };
        const nivelTextColors = { red:'text-red-600', orange:'text-orange-500', amber:'text-amber-500', blue:'text-blue-500', slate:'text-slate-400' };
        const nRiesgos = RD?.nRiesgos ?? 0;
        const nAmenazas = RD?.nAmenazas ?? 0;
        const nOportunidades = RD?.nOportunidades ?? 0;

        const hayRevision = RD?.hayRevision ?? false;
        const revision    = RD?.revision ?? null;
        const fuente      = RD?.fuente ?? 'sin_datos';

        if (nRiesgos === 0) {
            const sinRevMsg = fuente === 'montecarlo'
                ? `<p class="text-xs mt-2 text-slate-500">La pestaña <strong>Riesgos</strong> de Montecarlo no tiene riesgos con impacto registrado para este proyecto. Agrega amenazas/oportunidades y ejecuta el análisis.</p>`
                : !hayRevision
                    ? `<p class="text-xs mt-2 text-slate-500">Ve a la pestaña <strong>Riesgos</strong> de Montecarlo, selecciona este proyecto y registra las amenazas y oportunidades asociadas.</p>`
                    : `<p class="text-xs mt-2 text-slate-500">La revisión <strong>Rev${revision?.numero} — ${esc(revision?.descripcion||'')}</strong> (${revision?.fecha}) no tiene riesgos registrados. Agrega riesgos en la pestaña Riesgos y guarda.</p>`;
            sec3 = `<div class="tc-card !py-8 text-center text-slate-400 mb-4">
                <span class="material-icons align-middle text-slate-300 text-3xl block mb-2">shield</span>
                <p class="text-sm font-medium mb-1">Sin riesgos registrados para este proyecto.</p>
                ${sinRevMsg}
            </div>`;
        } else {
            const nivelBadges = (RD?.distribucionNiveles || []).map(n => {
                const tw = nivelColors[n.color] || nivelColors.slate;
                return `<span class="flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium border ${tw}">
                    <span class="font-bold">${n.nivel}</span> ${esc(n.label)}: ${n.count}
                </span>`;
            }).join('');

            const topRiskRows = (RD?.topRiesgos || []).map(r => {
                const ntc = nivelTextColors[r.nivelColor] || 'text-slate-400';
                return `<tr class="border-b border-slate-100 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800/50">
                    <td class="py-1.5 px-3 text-xs font-mono">${esc(r.codigo) || '—'}</td>
                    <td class="py-1.5 px-3 text-xs text-slate-600 dark:text-slate-400 max-w-[200px] truncate">${esc(r.descripcion) || '—'}</td>
                    <td class="py-1.5 px-3 text-xs text-right tabular-nums">${Pct(r.probabilidad)}</td>
                    <td class="py-1.5 px-3 text-xs text-right tabular-nums ${r.esAmenaza ? 'text-red-500' : 'text-emerald-500'}">${U(r.moda)}</td>
                    <td class="py-1.5 px-3 text-xs text-right tabular-nums font-semibold ${r.esAmenaza ? 'text-red-600' : 'text-emerald-600'}">${r.esAmenaza&&r.ve>0?'+':''}${U(r.ve??0)}</td>
                    <td class="py-1.5 px-3 text-xs text-center"><span class="${ntc} font-bold">${r.nivel}</span> <span class="text-slate-400 text-xs">${esc(r.nivelLabel)}</span></td>
                    <td class="py-1.5 px-3 text-xs text-center">${r.tieneRespuesta ? '<span class="material-icons text-xs text-emerald-500">check_circle</span>' : '<span class="material-icons text-xs text-slate-300">radio_button_unchecked</span>'}</td>
                </tr>`;
            }).join('');

            // ── Tarjeta puente: Distribución de riesgos → EAT Combinado ──────
            const riskBridgeCard = (() => {
                if (!EC) return '';
                // SR = savedRisksSimulation: resultado exacto de la última depuración guardada en pestaña Riesgos.
                // Prioridad: SR (fuente de verdad) → EC (simulación combinada, valores aproximados)
                const amenP50   = SR?.threatP50  ?? EC.contingenciaAmenazasP50  ?? 0;
                const amenP80   = SR?.threatP80  ?? EC.contingenciaAmenazasP80  ?? 0;
                const amenP90   = SR?.threatP90  ?? EC.contingenciaAmenazasP90  ?? 0;
                const amenMedia = EC.contingenciaAmenazasMedia ?? 0;
                const oportunP50 = SR?.oppP50 != null ? Math.abs(SR.oppP50) : Math.abs(EC.impactoOportunidadesP50 ?? 0);
                const oportunP80 = SR?.oppP80 != null ? Math.abs(SR.oppP80) : Math.abs(EC.impactoOportunidadesP80 ?? 0);
                const hasOport = (RD?.nOportunidades ?? 0) > 0;
                const hasAmen  = (RD?.nAmenazas     ?? 0) > 0;

                // Desglose de componentes EAT
                const eatDet  = EC.eatDeterministico ?? 0;
                const deltaInc   = T.eatP50 != null ? T.eatP50 - eatDet    : null;
                const deltaInc80 = T.eatP80 != null ? T.eatP80 - eatDet    : null;
                // Δ Riesgos MC: neto del portafolio en la MISMA simulación (amenazas − oportunidades por iteración).
                // NO es igual a (P80 amenazas − P80 oportunidades): arriba son percentiles *marginales* por tipo.
                // Fallback: diferencia EAT combinado − solo incertidumbre (mismo cierre contable que el pie de tabla).
                const deltaRsk   = SR?.netP50 != null ? SR.netP50
                                 : (T.eatP50  != null ? EC.eatP50 - T.eatP50 : EC.eatP50 - eatDet);
                const deltaRsk80 = SR?.netP80 != null ? SR.netP80
                                 : (T.eatP80  != null ? EC.eatP80 - T.eatP80 : EC.eatP80 - eatDet);

                const signCls = v => v >= 0 ? 'text-red-600' : 'text-emerald-600';
                const signPfx = v => v >= 0 ? '+' : '−';
                const Uabs   = v => U(Math.abs(v));

                // Barras relativas al máximo de cada columna (P50/P80/P90 y media en amenazas), no al CAPEX:
                // así los millones de riesgo se ven proporcionales entre sí; el monto en USD sigue siendo la referencia absoluta.
                const amenBarMax = Math.max(1, amenP50, amenP80, amenP90, amenMedia);
                const oportunBarMax = Math.max(1, oportunP50, oportunP80);

                const amenBar = (label, val, cls) => {
                    const pct = val > 0 ? Math.min((val / amenBarMax) * 100, 100) : 0;
                    return `<div class="flex items-center gap-2 mb-1.5">
                        <span class="text-xs font-bold w-7 ${cls}">${label}</span>
                        <div class="flex-1 bg-red-100 dark:bg-red-900/20 rounded h-3 relative overflow-hidden">
                            <div class="h-3 rounded" style="width:${pct.toFixed(1)}%;background:linear-gradient(90deg,#fca5a5,#ef4444)"></div>
                        </div>
                        <span class="text-xs tabular-nums w-32 text-right font-semibold ${cls}">${val > 0 ? U(val) : '<span class="text-slate-400 font-normal">— (p&lt;50%)</span>'}</span>
                    </div>`;
                };
                const oportunBar = (label, val, cls) => {
                    const pct = val > 0 ? Math.min((val / oportunBarMax) * 100, 100) : 0;
                    return `<div class="flex items-center gap-2 mb-1.5">
                        <span class="text-xs font-bold w-7 ${cls}">${label}</span>
                        <div class="flex-1 bg-emerald-100 dark:bg-emerald-900/20 rounded h-3 relative overflow-hidden">
                            <div class="h-3 rounded" style="width:${pct.toFixed(1)}%;background:linear-gradient(90deg,#6ee7b7,#10b981)"></div>
                        </div>
                        <span class="text-xs tabular-nums w-32 text-right font-semibold ${cls}">${val > 0 ? U(val) : '<span class="text-slate-400 font-normal">— (p&lt;50%)</span>'}</span>
                    </div>`;
                };

                const desgloseRow = (label, p50, p80, cls50, cls80) => `
                <tr class="border-b border-slate-100 dark:border-slate-800">
                    <td class="py-1.5 px-3 text-xs text-slate-600 dark:text-slate-400">${label}</td>
                    <td class="py-1.5 px-3 text-xs text-right tabular-nums font-semibold ${cls50}">${p50}</td>
                    <td class="py-1.5 px-3 text-xs text-right tabular-nums font-semibold ${cls80}">${p80}</td>
                </tr>`;

                return `
                <div class="tc-card mb-4 border border-red-200 dark:border-red-900/40" style="border-left:4px solid #ef4444">
                    <h3 class="font-semibold text-slate-700 dark:text-slate-200 flex items-center gap-2 mb-1 text-sm">
                        <span class="material-icons text-red-500 text-base">functions</span>
                        Distribución Probabilística de Riesgos
                        ${TT('Muestra los percentiles P50/P80/P90 del impacto probabilístico de amenazas y ahorros de oportunidades, calculados mediante simulación Bernoulli \u00d7 Triangular. Cada riesgo ocurre con su probabilidad y su impacto sigue una distribución Triangular. Los percentiles del portafolio completo se suman a la incertidumbre de costos para obtener el EAT Combinado.', 'gpr-tt-left')}
                        ${SR ? '<span class="ml-auto text-xs px-2 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-300 flex items-center gap-1"><span class="material-icons" style="font-size:11px">save</span>Resultado guardado</span>' : '<span class="ml-auto text-xs text-slate-400 italic">Estimado (sin depuración guardada)</span>'}
                    </h3>
                    <p class="text-xs text-slate-400 dark:text-slate-500 mb-2">Cada riesgo se activa con su probabilidad (Bernoulli) y su impacto sigue una distribución Triangular (Mín, Probable, Máx). Los bloques de arriba son percentiles <strong>por tipo</strong> sobre las <strong>mismas</strong> iteraciones que el neto (mismos sorteos por escenario). Aun así, el <strong>Δ Riesgos MC</strong> es el percentil del <strong>neto</strong> (amenazas − oportunidades en cada iteración), y en general <strong>no es igual</strong> a restar los P50/P80 de las dos columnas: en estadística, P80(neto) ≠ P80(amenazas) − P80(oportunidades).</p>
                    <p class="text-[10px] text-slate-500 dark:text-slate-400 mb-4 flex items-start gap-1"><span class="material-icons flex-shrink-0" style="font-size:12px">straighten</span><span><strong>Escala de barras:</strong> cada columna se normaliza al mayor valor mostrado en esa columna (amenazas incluye P50/P80/P90 y la media; oportunidades P50/P80). Así se comparan visualmente los percentiles entre sí; el monto en USD es la magnitud económica real (no se compara con el CAPEX del proyecto).</span></p>

                    <div class="grid grid-cols-1 ${hasOport ? 'lg:grid-cols-2' : ''} gap-4 mb-4">
                        ${hasAmen ? `
                        <div>
                            <div class="flex items-center gap-2 mb-2">
                                <span class="material-icons text-red-500 text-sm">trending_up</span>
                                <span class="text-xs font-bold text-red-700 dark:text-red-300 uppercase tracking-wide">Amenazas — Impacto Acumulado</span>
                                <span class="text-xs text-red-400 ml-auto">${RD?.nAmenazas ?? 0} riesgo${(RD?.nAmenazas??0)!==1?'s':''}</span>
                            </div>
                            ${amenBar('P50', amenP50, 'text-red-500')}
                            ${amenBar('P80', amenP80, 'text-red-600')}
                            ${amenBar('P90', amenP90, 'text-red-700')}
                            <p class="text-xs text-slate-400 mt-1.5 flex items-center gap-1">
                                <span class="material-icons" style="font-size:11px">info</span>
                                Media: ${U(amenMedia)} · P50=0 cuando prob &lt;50% en todos los riesgos
                            </p>
                        </div>` : ''}
                        ${hasOport ? `
                        <div>
                            <div class="flex items-center gap-2 mb-2">
                                <span class="material-icons text-emerald-500 text-sm">trending_down</span>
                                <span class="text-xs font-bold text-emerald-700 dark:text-emerald-300 uppercase tracking-wide">Oportunidades — Ahorro Potencial</span>
                                <span class="text-xs text-emerald-400 ml-auto">${RD?.nOportunidades ?? 0}</span>
                            </div>
                            ${oportunBar('P50', oportunP50, 'text-emerald-600')}
                            ${oportunBar('P80', oportunP80, 'text-emerald-700')}
                            <p class="text-xs text-slate-400 mt-1.5 flex items-center gap-1">
                                <span class="material-icons" style="font-size:11px">info</span>
                                Ahorro neto en escenarios donde se materializan
                            </p>
                        </div>` : (!hasAmen ? '' : `
                        <div class="flex items-center justify-center rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 p-4">
                            <span class="text-xs text-slate-400 text-center"><span class="material-icons text-slate-300 block mb-1">auto_awesome</span>Sin oportunidades registradas</span>
                        </div>`)}
                    </div>

                    ${/* Tabla "Contribución al EAT Combinado" oculta en Resumen Global; poner true para volver a mostrar */ false ? `
                    <div class="border-t border-slate-200 dark:border-slate-700 pt-3">
                        <p class="text-xs font-semibold text-slate-500 dark:text-slate-400 mb-2 flex items-center gap-1">
                            <span class="material-icons text-purple-500" style="font-size:14px">merge_type</span>
                            Contribución al EAT Combinado (Incertidumbre + Riesgos)
                            ${TT('El EAT Combinado es el resultado de simular conjuntamente la variabilidad de los ítems incertidumbre y la ocurrencia aleatoria de los riesgos. No es la simple suma de percentiles individuales. El desglose siguiente es una aproximación para visualizar cada componente.', 'gpr-tt-right')}
                        </p>
                        <div class="overflow-x-auto">
                        <table class="w-full text-left text-xs">
                            <thead><tr class="text-slate-400 uppercase tracking-wide border-b border-slate-200 dark:border-slate-700">
                                <th class="pb-1.5 px-3">Componente</th>
                                <th class="pb-1.5 px-3 text-right">P50</th>
                                <th class="pb-1.5 px-3 text-right">P80</th>
                            </tr></thead>
                            <tbody>
                                ${desgloseRow('EAT Determinístico (base)', U(eatDet), U(eatDet), 'text-slate-600 dark:text-slate-300', 'text-slate-600 dark:text-slate-300')}
                                ${deltaInc != null ? desgloseRow(
                                    `<span class="flex items-center gap-1">${TT('Δ por variabilidad de los ítems de Bloque B+C con rangos Mín/Probable/Máx (sin riesgos). Proviene del MC Agregado por contrato guardado.','gpr-tt-right')} Δ Incertidumbre de ítems (sin riesgos)</span>`,
                                    `<span class="${signCls(deltaInc)}">${signPfx(deltaInc)}${Uabs(deltaInc)}</span>`,
                                    `<span class="${signCls(deltaInc80)}">${signPfx(deltaInc80)}${Uabs(deltaInc80)}</span>`,
                                    '', '') : ''}
                                ${desgloseRow(
                                    `<span class="flex items-center gap-1">${TT('Impacto neto de riesgos al percentil indicado, calculado en la simulación conjunta (en cada iteración: suma amenazas − suma oportunidades), Bernoulli × Triangular. Es la cifra que cierra con el EAT combinado. Los P50/P80 de los recuadros superiores son percentiles marginales por tipo; por ello no deben restarse mentalmente para obtener esta fila (P80 del neto ≠ P80 amenazas − P80 oportunidades).','gpr-tt-right')} Δ Riesgos MC (neto conjunto, misma simulación)</span>`,
                                    `<span class="${signCls(deltaRsk)}">${signPfx(deltaRsk)}${Uabs(deltaRsk)}</span>`,
                                    `<span class="${signCls(deltaRsk80)}">${signPfx(deltaRsk80)}${Uabs(deltaRsk80)}</span>`,
                                    '', '')}
                            </tbody>
                            <tfoot class="border-t-2 border-purple-300 dark:border-purple-700 bg-purple-50 dark:bg-purple-900/20 font-bold">
                                <tr>
                                    <td class="py-1.5 px-3 text-purple-700 dark:text-purple-300 flex items-center gap-1">
                                        <span class="material-icons text-sm">arrow_downward</span> EAT Combinado (ver Sección siguiente)
                                    </td>
                                    <td class="py-1.5 px-3 text-right tabular-nums text-purple-700 dark:text-purple-300">${U(EC.eatP50)}</td>
                                    <td class="py-1.5 px-3 text-right tabular-nums text-purple-700 dark:text-purple-300">${U(EC.eatP80)}</td>
                                </tr>
                            </tfoot>
                        </table>
                        </div>
                    </div>
                    ` : ''}
                </div>`;
            })();

            sec3 = `
            <details class="tc-minimized-disclosure mb-4">
                <summary class="flex items-center gap-2 cursor-pointer select-none flex-wrap">
                    <span class="material-icons tc-minimized-section-chevron" aria-hidden="true">expand_more</span>
                    <span class="text-xs font-semibold text-slate-500 uppercase tracking-wide">Distribución por nivel de probabilidad</span>
                    <span class="text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wide">· Riesgos MC</span>
                    ${TT('Incluye la distribución por escala CODELCO (N1–N5) y la tabla detallada de riesgos MC. Niveles: N1 Remoto (&lt;25%), N2 Posible (25–50%), N3 Probable (50–65%), N4 Muy Probable (65–75%), N5 Casi Seguro (≥75%). La tabla lista cada riesgo por VE (Prob × impacto probable), orden descendente.', 'gpr-tt-left')}
                </summary>
                ${nivelBadges ? `<div class="flex gap-2 flex-wrap mt-3">${nivelBadges}</div>` : ''}
                <div class="tc-card ${nivelBadges ? 'mt-4' : 'mt-3'} mb-0 overflow-x-auto">
                    <h3 class="font-semibold text-slate-700 dark:text-slate-200 flex items-center gap-2 mb-3 text-sm">
                        <span class="material-icons text-red-500 text-base">warning_amber</span>
                        Riesgos MC <span class="text-xs font-normal text-slate-400">(VE = probabilidad × impacto probable)</span>
                        ${TT('Tabla de riesgos registrados en el análisis Monte Carlo. Ordenados por Valor Esperado (VE) descendente. Las amenazas aumentan el EAT; las oportunidades lo reducen. Riesgos sin plan de respuesta son los de mayor exposición no gestionada.', 'gpr-tt-left')}
                    </h3>
                    <table class="w-full text-left">
                        <thead><tr class="text-xs text-slate-400 uppercase border-b border-slate-200 dark:border-slate-700">
                            <th class="py-2 px-3">Código</th><th class="py-2 px-3">Descripción</th>
                            <th class="py-2 px-3 text-right">Prob %</th>
                            <th class="py-2 px-3 text-right">
                                Moda ${TT('Impacto probable (valor modal) si el riesgo se materializa. Centro de la distribución triangular (Mín, Probable, Máx). Es el valor más frecuente en la simulación cuando el evento ocurre.', 'gpr-tt-left')}
                            </th>
                            <th class="py-2 px-3 text-right">
                                VE ${TT('Valor Esperado = Probabilidad × Impacto Probable. Cuantifica la contribución individual de este riesgo al costo esperado. Amenaza = positivo (sobrecosto); Oportunidad = negativo (ahorro).', 'gpr-tt-left')}
                            </th>
                            <th class="py-2 px-3 text-center">
                                Nivel CODELCO ${TT('Clasificación según escala CODELCO: N1 Remoto &lt;25%, N2 ≥25%, N3 ≥50%, N4 ≥65%, N5 ≥75% Casi Seguro. A mayor nivel, mayor prioridad de mitigación y seguimiento en el comité de riesgos.', 'gpr-tt-left')}
                            </th>
                            <th class="py-2 px-3 text-center">
                                Plan ${TT('Indica si existe un plan de respuesta documentado (mitigación, transferencia, aceptación o contingencia). Sin plan = exposición no gestionada. Los riesgos N4–N5 sin plan representan el mayor riesgo residual del proyecto.', 'gpr-tt-left')}
                            </th>
                        </tr></thead>
                        <tbody>${topRiskRows}</tbody>
                    </table>
                </div>
            </details>
            ${riskBridgeCard}`;
        }
    }

    // ══════ SECCIÓN 4 — Análisis Combinado ═════════════════════════════════

    let sec4 = '';
    if (EC) {
        const eatDet = EC.eatDeterministico ?? T.eat ?? 0;
        const capApi = T.capex ?? 0;
        const porComprometerTot = T.porComprometer ?? 0;
        const p10 = EC.eatP10, p50 = EC.eatP50, p80 = EC.eatP80, p90 = EC.eatP90;
        const s10 = EC.eatSoloItemsP10, s50 = EC.eatSoloItemsP50, s80 = EC.eatSoloItemsP80, s90 = EC.eatSoloItemsP90;
        const barScale = Math.max(capApi * 1.06, eatDet, p10 ?? 0, p50 ?? 0, p80 ?? 0, p90 ?? 0, 1);
        const tickCapexPct = barScale > 0 ? Math.min(100, (capApi / barScale) * 100) : 0;
        const eatProbBarRow = (label, val, grad, labelCls) => {
            const v = val ?? 0;
            const w = barScale > 0 ? Math.min(100, (v / barScale) * 100) : 0;
            return `<tr class="border-b border-slate-100 dark:border-slate-800">
                <td class="py-1 px-1.5 text-[11px] font-medium leading-tight ${labelCls}">${label}</td>
                <td class="py-1 px-1">
                    <div class="relative h-3 w-full rounded-full bg-slate-100 dark:bg-slate-700 overflow-hidden">
                        <div class="h-full rounded-full" style="width:${w.toFixed(1)}%;background:${grad};opacity:0.92"></div>
                        <div class="absolute top-0 bottom-0 w-0.5 bg-indigo-500 z-[1] shadow-sm" style="left:calc(${tickCapexPct.toFixed(2)}% - 1px)" title="Referencia CAPEX"></div>
                    </div>
                </td>`;
        };
        const Usgn = (v) => {
            if (v == null || (typeof v === 'number' && Math.abs(v) < 0.5)) return '<span class="text-slate-400">$0 USD</span>';
            const s = v >= 0 ? '+' : '−';
            return `<span class="${v >= 0 ? 'text-red-600 dark:text-red-400' : 'text-emerald-600 dark:text-emerald-400'}">${s}${U(Math.abs(v))}</span>`;
        };
        const Pct2 = v => v != null ? tcFmt(v, 2) + '%' : '—';
        const PctOver2 = (num, den) => {
            if (den == null || Math.abs(den) < 1e-6) return '—';
            return Pct2((num / den) * 100);
        };
        const dEat = eatDet - capApi;
        const dP10 = (p10 != null) ? p10 - eatDet : null;
        const dP50 = (p50 != null && p10 != null) ? p50 - p10 : null;
        const dP80 = (p80 != null && p50 != null) ? p80 - p50 : null;
        const dP90 = (p90 != null && p80 != null) ? p90 - p80 : null;
        const contP10 = (p10 != null) ? p10 - eatDet : null;
        const contP50 = (p50 != null) ? p50 - eatDet : null;
        const contP80 = (p80 != null) ? p80 - eatDet : null;
        const contP90 = (p90 != null) ? p90 - eatDet : null;
        // Desglose: misma simulación acoplada — variabilidad triangular ítems vs aporte riesgos (Bernoulli) al mismo rango nominal
        const contMcP10 = (s10 != null) ? s10 - eatDet : null;
        const contMcP50 = (s50 != null) ? s50 - eatDet : null;
        const contMcP80 = (s80 != null) ? s80 - eatDet : null;
        const contMcP90 = (s90 != null) ? s90 - eatDet : null;
        const contRskP10 = (p10 != null && s10 != null) ? p10 - s10 : null;
        const contRskP50 = (p50 != null && s50 != null) ? p50 - s50 : null;
        const contRskP80 = (p80 != null && s80 != null) ? p80 - s80 : null;
        const contRskP90 = (p90 != null && s90 != null) ? p90 - s90 : null;
        const pctCont = (absCont) => {
            if (absCont == null) return '—';
            if (porComprometerTot == null || Math.abs(porComprometerTot) < 1e-6) return '—';
            return Pct2((absCont / porComprometerTot) * 100);
        };
        const eatProbTableRows = `
            ${eatProbBarRow('CAPEX API', capApi, 'linear-gradient(90deg,#94a3b8,#64748b)', 'text-slate-700 dark:text-slate-200')}
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums font-semibold text-slate-700 dark:text-slate-200">${U(capApi)}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums text-slate-400">$0 USD</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums text-slate-400">${Pct2(0)}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums text-slate-400">—</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums text-slate-400">—</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums text-slate-400">$0 USD</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums text-slate-400">${Pct2(0)}</td>
            </tr>
            ${eatProbBarRow('EAT determinístico', eatDet, 'linear-gradient(90deg,#cbd5e1,#475569)', 'text-slate-600 dark:text-slate-300')}
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums font-semibold text-slate-700 dark:text-slate-200">${U(eatDet)}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${Usgn(dEat)}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${PctOver2(dEat, capApi)}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums text-slate-400">—</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums text-slate-400">—</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums text-slate-400">—</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums text-slate-400">—</td>
            </tr>
            ${eatProbBarRow('P10', p10, 'linear-gradient(90deg,#93c5fd,#3b82f6)', 'text-blue-600 font-bold')}
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums font-semibold text-blue-700 dark:text-blue-300">${p10 != null ? U(p10) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${dP10 != null ? Usgn(dP10) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${p10 != null ? PctOver2(p10, capApi) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contMcP10 != null ? Usgn(contMcP10) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contRskP10 != null ? Usgn(contRskP10) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contP10 != null ? Usgn(contP10) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${pctCont(contP10)}</td>
            </tr>
            ${eatProbBarRow('P50', p50, 'linear-gradient(90deg,#6ee7b7,#10b981)', 'text-emerald-600 font-bold')}
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums font-semibold text-emerald-700 dark:text-emerald-300">${p50 != null ? U(p50) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${dP50 != null ? Usgn(dP50) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${p50 != null ? PctOver2(p50, capApi) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contMcP50 != null ? Usgn(contMcP50) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contRskP50 != null ? Usgn(contRskP50) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contP50 != null ? Usgn(contP50) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${pctCont(contP50)}</td>
            </tr>
            ${eatProbBarRow('P80', p80, 'linear-gradient(90deg,#fcd34d,#f59e0b)', 'text-amber-600 font-bold')}
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums font-semibold text-amber-700 dark:text-amber-300">${p80 != null ? U(p80) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${dP80 != null ? Usgn(dP80) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${p80 != null ? PctOver2(p80, capApi) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contMcP80 != null ? Usgn(contMcP80) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contRskP80 != null ? Usgn(contRskP80) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contP80 != null ? Usgn(contP80) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${pctCont(contP80)}</td>
            </tr>
            ${eatProbBarRow('P90', p90, 'linear-gradient(90deg,#fca5a5,#ef4444)', 'text-red-600 font-bold')}
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums font-semibold text-red-700 dark:text-red-300">${p90 != null ? U(p90) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${dP90 != null ? Usgn(dP90) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${p90 != null ? PctOver2(p90, capApi) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contMcP90 != null ? Usgn(contMcP90) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contRskP90 != null ? Usgn(contRskP90) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${contP90 != null ? Usgn(contP90) : '—'}</td>
                <td class="py-1 px-1.5 text-[11px] text-right tabular-nums">${pctCont(contP90)}</td>
            </tr>`;

        const contPureP80 = T.eatP80 != null ? EC.eatP80 - T.eatP80 : null;
        let pctZ1 = 50, pctZ2 = 30, pctZ3 = 20;
        if (EC.histograma?.length) {
            const p50r = EC.eatP50 ?? EC.p50, p80r = EC.eatP80 ?? EC.p80;
            pctZ1 = +(EC.histograma.reduce((s,b) => s+(b.hasta<=p50r?b.frecuencia:0),0)*100).toFixed(0);
            pctZ3 = +(EC.histograma.reduce((s,b) => s+(b.desde>=p80r?b.frecuencia:0),0)*100).toFixed(0);
            pctZ2 = Math.max(0, 100-pctZ1-pctZ3);
        }

        let drPctZ1 = 50, drPctZ2 = 30, drPctZ3 = 20;
        const hRiesgo = EC.histogramaRiesgoNeto;
        if (EC.tieneRiesgos && hRiesgo?.length && EC.deltaRiesgosP50 != null && EC.deltaRiesgosP80 != null) {
            const dp50 = EC.deltaRiesgosP50, dp80 = EC.deltaRiesgosP80;
            drPctZ1 = +(hRiesgo.reduce((s,b) => s+(b.hasta<=dp50?b.frecuencia:0),0)*100).toFixed(0);
            drPctZ3 = +(hRiesgo.reduce((s,b) => s+(b.desde>=dp80?b.frecuencia:0),0)*100).toFixed(0);
            drPctZ2 = Math.max(0, 100-drPctZ1-drPctZ3);
        }

        const riesgosBadge = EC.nRiesgos > 0
            ? `<span class="text-xs px-2 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700">${EC.nRiesgos} riesgo${EC.nRiesgos!==1?'s':''} MC</span>`
            : `<span class="text-xs px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-700 text-slate-400">Sin riesgos MC</span>`;

        // Usar RD como fuente de verdad para existencia de amenazas/oportunidades.
        // EC.contingenciaAmenazasP50 puede ser 0 legítimamente cuando la probabilidad
        // de cada riesgo es < 50% (el P50 de Bernoulli×Triangular es 0 en ese caso).
        const hasAmen  = (RD?.nAmenazas  ?? 0) > 0;
        const hasOport = (RD?.nOportunidades ?? 0) > 0;
        // P80 es el valor de referencia más representativo para distribuciones de baja probabilidad
        // SR = saved risks simulation (fuente de verdad: resultado exacto de la última depuración guardada)
        const amenP80  = SR?.threatP80  ?? EC.contingenciaAmenazasP80 ?? 0;
        const amenP50  = SR?.threatP50  ?? EC.contingenciaAmenazasP50 ?? 0;
        const oportunP50 = SR?.oppP50 != null ? Math.abs(SR.oppP50) : Math.abs(EC.impactoOportunidadesP50 ?? 0);
        const amenCard = hasAmen
            ? `<div class="bg-red-50 dark:bg-red-900/20 rounded-xl p-3 border border-red-100 dark:border-red-800">
                <div class="flex items-center gap-1 mb-2">
                    <span class="material-icons text-base text-red-500">trending_up</span>
                    <span class="text-xs font-bold text-red-700 dark:text-red-300">Amenazas</span>
                    ${TT('Eventos negativos que aumentan el EAT. Se simulan como Bernoulli (ocurre/no ocurre) con impacto triangular. El P80 de amenazas es la referencia conservadora para provisionar contingencia por riesgos. Si P50=0, la probabilidad de cada amenaza es &lt;50%.', 'gpr-tt-left')}
                    <span class="text-xs text-red-400 ml-auto">${RD?.nAmenazas ?? 0}</span>
                </div>
                <div class="space-y-1 text-xs">
                    <div class="flex justify-between"><span class="text-slate-500">P80 (ref.)</span><span class="font-semibold text-red-600">${U(amenP80)}</span></div>
                    <div class="flex justify-between"><span class="text-slate-500">P50</span><span class="${amenP50>0?'text-red-700 font-semibold':'text-slate-400'}">${amenP50>0?U(amenP50):'— (p&lt;50%)'}</span></div>
                </div>
               </div>`
            : `<div class="bg-slate-50 dark:bg-slate-800 rounded-xl p-3 border border-slate-200 dark:border-slate-700 flex items-center justify-center">
                <span class="text-xs text-slate-400 text-center"><span class="material-icons text-base align-middle block mb-1 text-slate-300">shield</span>Sin amenazas</span>
               </div>`;
        const oportunCard = hasOport
            ? `<div class="bg-emerald-50 dark:bg-emerald-900/20 rounded-xl p-3 border border-emerald-100 dark:border-emerald-800">
                <div class="flex items-center gap-1 mb-2">
                    <span class="material-icons text-base text-emerald-500">trending_down</span>
                    <span class="text-xs font-bold text-emerald-700 dark:text-emerald-300">Oportunidades</span>
                    ${TT('Eventos positivos que reducen el EAT. El Ahorro P50 es el valor mediano del beneficio simulado. Si aparece "— (p&lt;50%)", la probabilidad de cada oportunidad es menor al 50%, por lo que en la mayoría de escenarios no se materializa y el P50 de Bernoulli es 0.', 'gpr-tt-left')}
                    <span class="text-xs text-emerald-400 ml-auto">${RD?.nOportunidades ?? 0}</span>
                </div>
                <div class="text-xs">
                    <div class="flex justify-between"><span class="text-slate-500">Ahorro P50</span><span class="${oportunP50>0?'text-emerald-700 font-semibold':'text-slate-400'}">${oportunP50>0?U(oportunP50):'— (p&lt;50%)'}</span></div>
                </div>
               </div>`
            : `<div class="bg-slate-50 dark:bg-slate-800 rounded-xl p-3 border border-slate-200 dark:border-slate-700 flex items-center justify-center">
                <span class="text-xs text-slate-400 text-center"><span class="material-icons text-base align-middle block mb-1 text-slate-300">auto_awesome</span>Sin oportunidades</span>
               </div>`;

        const p50VsDet   = eatDet > 0 ? (EC.eatP50 - eatDet) / eatDet * 100 : 0;
        const p50DetCls  = p50VsDet >= 0 ? 'text-amber-600' : 'text-emerald-600';
        const p80OverCap = T.capex > 0 && EC.eatP80 > T.capex;

        sec4 = `
        <style>
        @keyframes tcBarIn{from{opacity:0;transform:translateX(-8px)}to{opacity:1;transform:translateX(0)}}
        .tc-bar-anim{animation:tcBarIn 0.4s ease-out both}
        .tc-eatprob-metrics .rounded-xl{padding:0.5rem !important}
        .tc-eatprob-metrics .material-icons.text-base{font-size:1rem !important}
        /* Tooltips: sin recorte; en cabecera de tabla se abren hacia abajo (evita clip con scroll/main) */
        .tc-eatprob-table-wrap{overflow:visible!important}
        .tc-eatprob-table-wrap table,.tc-eatprob-table-wrap thead,.tc-eatprob-table-wrap thead tr,.tc-eatprob-table-wrap thead th{overflow:visible}
        .tc-eatprob-table-wrap .gpr-tt .gpr-tt-box{
            z-index:10050;
            bottom:auto!important;
            top:calc(100% + 6px)!important;
            transform:translateX(-50%);
        }
        .tc-eatprob-table-wrap .gpr-tt.gpr-tt-right .gpr-tt-box{transform:none}
        .tc-eatprob-table-wrap .gpr-tt .gpr-tt-box::after{
            top:auto!important;
            bottom:100%!important;
            border-top-color:transparent!important;
            border-bottom-color:#1e293b!important;
        }
        </style>
        <div class="flex items-center gap-3 mb-2 mt-6">
            <div class="w-1 h-6 bg-purple-500 rounded-full flex-shrink-0"></div>
            <h2 class="text-base font-semibold text-slate-800 dark:text-slate-200 flex items-center gap-1">
                EAT Probabilístico — Incertidumbre + Riesgos
                ${TT('EAT combinado = Comprometido + Certeza + Incertidumbre MC + Impacto Riesgos MC (Bernoulli). Integra en una sola distribución la variabilidad de todos los ítems inciertos y la ocurrencia aleatoria de riesgos. Los percentiles P50/P80/P90 indican el costo bajo distintos niveles de confianza.', 'gpr-tt-right')}
            </h2>
            <div class="flex items-center gap-2 ml-auto flex-wrap">
                ${riesgosBadge}
                <span class="text-xs text-slate-400">${tcFmt(EC.nSimulaciones)} iter · ${EC.nItems} ítem${EC.nItems!==1?'s':''}</span>
            </div>
        </div>
        <p class="text-xs text-slate-500 dark:text-slate-400 mb-4 flex items-center gap-1.5 flex-wrap">
            <span class="material-icons text-blue-400" style="font-size:14px">merge_type</span>
            Combina la variabilidad de <strong>ítems de incertidumbre</strong> (Sección 2) con el impacto de <strong>riesgos MC</strong> (Sección 3) en una sola simulación conjunta de ${tcFmt(EC.nSimulaciones)} iteraciones.
            ${TT('El <strong>P10</strong> puede quedar <strong>por debajo</strong> del EAT determinístico: es el percentil 10 de costos totales simulados (escenario favorable en los triángulos), no el det. más un margen. Compare la <strong>media</strong> (eatMedia en datos) o use P50+ para lectura tipo «sobre costo base».', 'gpr-tt-left')}
        </p>
        <div class="tc-card mb-4">
            <div class="grid grid-cols-1 lg:grid-cols-12 gap-4 lg:gap-5 lg:items-start">
                <div class="lg:col-span-9 min-w-0">
                    <p class="text-xs text-slate-500 mb-2 flex flex-wrap items-center gap-x-1 gap-y-0.5">
                        <span class="font-medium text-slate-600 dark:text-slate-400">EAT combinado vs CAPEX</span>
                        <span class="text-indigo-500">│</span>
                        <span class="text-slate-400">Línea índigo en barras = CAPEX (${U(capApi)})</span>
                        ${TT('Monto P10–P90 = percentiles del EAT conjunto (comprometido + certeza por comp. + sorteos triángulos + riesgos). <strong>P10</strong> puede ser &lt; EAT det. (cola optimista). <strong>Contingencias</strong> vs det.: C. ítems / C. riesg. / C. total como se define en cabeceras.', 'gpr-tt-right')}
                    </p>
                    <div class="tc-eatprob-table-wrap rounded-lg border border-slate-200 dark:border-slate-700 mb-3 min-w-0 overflow-x-auto">
                        <table class="w-full text-left border-collapse table-fixed min-w-[720px]">
                            <colgroup>
                                <col style="width:11%" /><col style="width:10%" /><col style="width:15%" /><col style="width:10%" />
                                <col style="width:9%" /><col style="width:11%" /><col style="width:11%" /><col style="width:11%" /><col style="width:12%" />
                            </colgroup>
                            <thead>
                                <tr class="border-b border-slate-200 dark:border-slate-700 text-[9px] leading-tight text-slate-500 uppercase tracking-wide bg-slate-50/80 dark:bg-slate-800/50">
                                    <th class="py-1.5 px-1 text-left align-bottom">Concepto</th>
                                    <th class="py-1.5 px-1 align-bottom">${TT('Longitud proporcional al monto respecto al máximo de la escala (CAPEX y percentiles). La marca vertical índigo indica el CAPEX aprobado.', 'gpr-tt-right')}Barra</th>
                                    <th class="py-1.5 px-1 text-right align-bottom">Monto</th>
                                    <th class="py-1.5 px-1 text-right align-bottom">${TT('CAPEX: 0. EAT det.: EAT det. − CAPEX. P10: P10 − EAT det. P50: P50 − P10. P80: P80 − P50. P90: P90 − P80.', 'gpr-tt-right')}Δ&nbsp;Monto</th>
                                    <th class="py-1.5 px-1 text-right align-bottom">${TT('CAPEX: 0%. EAT det.: (EAT det. − CAPEX) / CAPEX. P10–P90: valor del percentil / CAPEX.', 'gpr-tt-right')}%&nbsp;/&nbsp;CAPEX</th>
                                    <th class="py-1.5 px-1 text-right align-bottom">${TT('Solo variabilidad triangular de ítems «Por comprometer» (sin activar riesgos). Pxx ítems − EAT determinístico.', 'gpr-tt-right')}C.&nbsp;ítems</th>
                                    <th class="py-1.5 px-1 text-right align-bottom">${TT('Aporte neto de riesgos MC al mismo rango nominal: Pxx conjunto − Pxx solo ítems (misma simulación acoplada por iteración).', 'gpr-tt-right')}C.&nbsp;riesg.</th>
                                    <th class="py-1.5 px-1 text-right align-bottom">${TT('Contingencia total vs EAT determinístico: Pxx conjunto − EAT det. (= C. ítems + C. riesg. salvo redondeo).', 'gpr-tt-right')}C.&nbsp;total</th>
                                    <th class="py-1.5 px-1 text-right align-bottom">${TT('C. total dividida por la suma de Por Comprometer de todos los contratos.', 'gpr-tt-right')}%&nbsp;c./&nbsp;P.comp.</th>
                                </tr>
                            </thead>
                            <tbody>${eatProbTableRows}</tbody>
                        </table>
                    </div>
                    <div class="relative mt-3 w-full" style="height:min(240px,42vh);min-height:180px">
                        <canvas id="histograma-eat-v2" class="block w-full h-full"></canvas>
                    </div>
                    <div class="flex gap-4 text-xs mt-2 flex-wrap">
                        <span><span class="inline-block w-3 h-2 rounded bg-blue-400 mr-1 align-middle"></span>≤ P50 (${pctZ1}%)</span>
                        <span><span class="inline-block w-3 h-2 rounded bg-amber-400 mr-1 align-middle"></span>P50–P80 (${pctZ2}%)</span>
                        <span><span class="inline-block w-3 h-2 rounded bg-red-400 mr-1 align-middle"></span>≥ P80 (${pctZ3}%)
                            ${TT('Histograma de frecuencias del EAT simulado. Azul = escenarios optimistas (≤P50); Ámbar = zona de contingencia (P50–P80); Rojo = escenarios adversos extremos (≥P80). Una cola roja extensa indica distribución asimétrica con alto riesgo de sobrecosto.', 'gpr-tt-left')}
                        </span>
                        <span class="ml-auto"><span class="inline-block w-5 border-t-2 border-dashed border-indigo-400 mr-1 align-middle"></span>Ref. CAPEX (tabla)</span>
                    </div>
                    ${EC.tieneRiesgos && hRiesgo?.length ? `
                    <p class="text-xs text-slate-500 dark:text-slate-400 mt-5 mb-2 flex flex-wrap items-center gap-1.5">
                        <span class="material-icons text-violet-400" style="font-size:14px">difference</span>
                        <strong class="text-slate-600 dark:text-slate-300">Impacto neto riesgos MC (misma simulación)</strong>
                        <span class="text-slate-400">— Δ = EAT conjunto − EAT solo ítems por iteración; escala propia en M&nbsp;USD (no el EAT total).</span>
                    </p>
                    <div class="relative w-full" style="height:min(200px,36vh);min-height:160px">
                        <canvas id="histograma-riesgo-neto-v2" class="block w-full h-full"></canvas>
                    </div>
                    <div class="flex gap-4 text-xs mt-2 flex-wrap">
                        <span><span class="inline-block w-3 h-2 rounded bg-blue-400 mr-1 align-middle"></span>≤ P50 Δ (${drPctZ1}%)</span>
                        <span><span class="inline-block w-3 h-2 rounded bg-amber-400 mr-1 align-middle"></span>P50–P80 Δ (${drPctZ2}%)</span>
                        <span><span class="inline-block w-3 h-2 rounded bg-red-400 mr-1 align-middle"></span>≥ P80 Δ (${drPctZ3}%)</span>
                        ${hRiesgo?.length && hRiesgo[0].desde < 0 && hRiesgo[hRiesgo.length - 1].hasta > 0
        ? `<span class="ml-auto"><span class="inline-block w-5 border-t-2 border-dashed border-slate-400 mr-1 align-middle"></span>Cero (sin efecto neto)</span>`
        : ''}
                    </div>` : ''}
                </div>
                <div class="lg:col-span-3 min-w-0 tc-eatprob-metrics">
                    <p class="text-[11px] text-slate-500 mb-2 font-medium">Métricas del análisis combinado</p>
                    <div class="grid grid-cols-2 gap-2">
                        ${amenCard}
                        ${oportunCard}
                        <div class="bg-indigo-50 dark:bg-indigo-900/20 rounded-xl p-3 border border-indigo-100 dark:border-indigo-800">
                            <div class="flex items-center gap-1 mb-2">
                                <span class="material-icons text-base text-indigo-500">assessment</span>
                                <span class="text-xs font-bold text-indigo-700 dark:text-indigo-300">EAT P50</span>
                                ${TT('Mediana del EAT combinado (incertidumbre + riesgos MC). El 50% de las simulaciones están por debajo. Es el escenario base del proyecto. Compárelo con el EAT Det. para cuantificar la contingencia esperada.', 'gpr-tt-left')}
                            </div>
                            <div class="space-y-1 text-xs">
                                <div class="flex justify-between"><span class="text-slate-500">Total</span><span class="font-semibold text-indigo-700">${U(EC.eatP50)}</span></div>
                                <div class="flex justify-between"><span class="text-slate-500">vs Det.</span><span class="font-semibold ${p50DetCls}">${p50VsDet>=0?'+':''}${tcFmt(p50VsDet,1)}%</span></div>
                            </div>
                        </div>
                        <div class="${p80OverCap?'bg-red-50 dark:bg-red-900/20 border-red-100 dark:border-red-800':'bg-amber-50 dark:bg-amber-900/20 border-amber-100 dark:border-amber-800'} rounded-xl p-3 border">
                            <div class="flex items-center gap-1 mb-2">
                                <span class="material-icons text-base ${p80OverCap?'text-red-500':'text-amber-500'}">warning_amber</span>
                                <span class="text-xs font-bold ${p80OverCap?'text-red-700 dark:text-red-300':'text-amber-700 dark:text-amber-300'}">EAT P80</span>
                                ${TT('El 80% de las simulaciones están por debajo. Es la contingencia conservadora recomendada para proyectos mineros CODELCO. Si supera el CAPEX, se requiere revisión de alcance o solicitud de ajuste presupuestario formal.', 'gpr-tt-left')}
                            </div>
                            <div class="space-y-1 text-xs">
                                <div class="flex justify-between"><span class="text-slate-500">Total</span><span class="font-semibold ${p80OverCap?'text-red-700':'text-amber-700'}">${U(EC.eatP80)}</span></div>
                                ${contPureP80!=null?`<div class="flex justify-between"><span class="text-slate-500 flex items-center gap-0.5">Cont. neta ${TT('Contingencia neta P80 = EAT P80 combinado − EAT P80 solo incertidumbre. Aísla el aporte de los riesgos MC al percentil P80. Positivo = los riesgos encarecen el proyecto; Negativo = las oportunidades compensan las amenazas.', 'gpr-tt-left')}</span><span class="font-semibold ${contPureP80>=0?'text-amber-600':'text-emerald-600'}">${U(Math.abs(contPureP80))}</span></div>`:''}
                            </div>
                        </div>
                    </div>
                    <div class="mt-3 space-y-1 text-sm">
                        ${tcResRow('EAT Determinístico', U(eatDet), 'text-slate-600 dark:text-slate-300')}
                        ${tcResRow(`Spread P10→P90 ${TT('Amplitud total de la distribución (EAT P90 − EAT P10). Spread amplio = alta incertidumbre; spread estrecho = estimación madura. Depende del % de ítems con variabilidad y del número e impacto de los riesgos MC.', 'gpr-tt-left')}`, U(EC.eatP90-EC.eatP10), 'text-purple-600')}
                        ${tcResRow(`P50 vs CAPEX ${TT('Diferencia porcentual entre el EAT P50 probabilístico y el CAPEX. Negativo = proyecto dentro del presupuesto en el escenario base; Positivo = el escenario más probable ya supera el techo autorizado.', 'gpr-tt-left')}`, (EC.eatP50>T.capex?'+':'')+Pct((EC.eatP50-T.capex)/capex*100), EC.eatP50<=T.capex?'text-emerald-600':'text-red-600')}
                        ${EC.cvar90?tcResRow(`CVaR90 — Peor 10% ${TT('Conditional Value at Risk al 90%: media del peor 10% de simulaciones. Es el Expected Shortfall, más conservador que el P90. Indica cuánto costaría el proyecto si se materializan los escenarios más adversos.', 'gpr-tt-left')}`, U(EC.cvar90), 'text-rose-600 font-semibold'):''}
                        ${EC.cvar80?tcResRow(`CVaR80 — Peor 20% ${TT('Media del peor 20% de simulaciones. Complementa el CVaR90 para entender la cola de la distribución. Útil para dimensionar la reserva de contingencia ante escenarios adversos moderados.', 'gpr-tt-left')}`, U(EC.cvar80), 'text-orange-600'):''}
                        ${EC.excessP90!=null?tcResRow(`Exceso cola P90 ${TT('Diferencia entre el CVaR90 (Expected Shortfall) y el P90. Un exceso positivo indica una cola "gorda" — los peores escenarios son significativamente más caros que el P90. Requiere mayor reserva de gestión.', 'gpr-tt-left')}`, U(Math.abs(EC.excessP90)), EC.excessP90>0?'text-red-500':'text-emerald-500'):''}
                    </div>
                    ${!EC.tieneRiesgos?`<div class="mt-3 p-2 rounded bg-blue-50 dark:bg-blue-900/20 text-xs text-blue-700 dark:text-blue-300">
                        <span class="material-icons text-sm align-middle">info</span>
                        Sin riesgos MC. Vincula riesgos en la pestaña <strong>Riesgos</strong> de Monte Carlo.
                    </div>`:''}
                </div>
            </div>
            ${tcHtmlDistribucionRiskStyle(EC, U, TT)}
            <div id="eat-corr-banner" class="hidden mt-3 pt-3 border-t border-dashed border-rose-200 dark:border-rose-800"></div>
        </div>`;
    } else {
        // EC es null: no hay ítems de incertidumbre ni riesgos → sección informativa
        sec4 = `
        <div class="flex items-center gap-3 mb-4 mt-6">
            <div class="w-1 h-6 bg-purple-500 rounded-full flex-shrink-0"></div>
            <h2 class="text-base font-semibold text-slate-800 dark:text-slate-200">EAT Probabilístico — Incertidumbre + Riesgos</h2>
        </div>
        <div class="tc-card !py-8 text-center text-slate-400 mb-4">
            <span class="material-icons text-slate-300 text-3xl block mb-2">merge_type</span>
            <p class="text-sm">Sin datos para análisis combinado.</p>
            <p class="text-xs mt-1">Define ítems con rango Min/Probable/Máx en las pestañas de contrato y riesgos en Monte Carlo.</p>
        </div>`;
    }

    // ══════ SECCIÓN 5 — Alertas y Recomendaciones ══════════════════════════
    const hintAlerta = {
        eat_det_115: 'EAT determinístico = comprometido + suma de costos de ítems (valor base del modelo). Superar el 115% del CAPEX es el umbral GPR de revisión urgente ante el techo aprobado.',
        eat_det_105: 'El EAT base ya supera el 100% del CAPEX (pero no el 115%). El proyecto parte con sobrecosto en la estimación contable antes de contingencias MC.',
        eat_comb_p80: 'EAT combinado P80: simulación conjunta incertidumbre + riesgos MC; ~80% de iteraciones quedan por debajo. Si supera CAPEX, la contingencia conservadora habitual quedaría por encima del presupuesto.',
        eat_comb_p50: 'P50 combinado es la mediana con riesgos. Si ya supera CAPEX, el escenario “central” del modelo probabilístico tensiona el techo.',
        eat_mc_p80: 'P80 del MC agregado solo ítems (sin Bernoulli de riesgos) usa la suma de percentiles por contrato guardados. Indica presión de cola solo por variabilidad de contratación.',
        sin_mc: 'Sin MC por contrato no hay P10–P90 agregado completo; el resumen probabilístico queda parcial hasta ejecutar y guardar simulaciones en Bloque F.',
        pct_incert: 'Mucho costo en ítems con rango Min/Probable/Máx amplía el spread del MC y la incertidumbre total del proyecto.',
        riesgo_sin_plan: 'Nivel CODELCO 4–5 implica probabilidad ≥65%. Sin plan de respuesta documentado, la exposición no gestionada es máxima en el comité de riesgos.',
        contrato_eat: 'Semáforo por contrato: EAT del paquete (comprometido + ítems) vs CAPEX del contrato. Rojo GPR cuando supera 115% del CAPEX del contrato.',
        contrato_cv: 'CV = desviación estándar / media del MC del contrato. Valores altos indican mucha dispersión relativa en la distribución simulada de ese paquete.',
        todo_ok: 'Las reglas automáticas del resumen no detectaron incumplimientos. Sigue siendo necesaria la validación humana de supuestos, datos y riesgos no modelados.'
    };

    const alertas = [];
    if (T.pctEAT > 115)
        alertas.push({ codigo:'eat_det_115', level:'red',   icon:'error',         msg:`EAT total (${Pct(T.pctEAT)}) supera el 115% del CAPEX. Revisión urgente.`, rec:'Solicitar sobregasto o reducir alcance del proyecto.' });
    else if (T.pctEAT > 105)
        alertas.push({ codigo:'eat_det_105', level:'amber', icon:'warning_amber', msg:`EAT total (${Pct(T.pctEAT)}) supera el CAPEX.`, rec:'Evalúa contingencias o ajuste de alcance.' });

    if (EC && EC.eatP80 > T.capex)
        alertas.push({ codigo:'eat_comb_p80', level:'red',   icon:'merge_type', msg:`EAT combinado P80 (${U(EC.eatP80)}) supera CAPEX. Riesgo confirmado de sobrecosto.`, rec:'El análisis conjunto indica necesidad de contingencia presupuestaria.' });
    else if (EC && EC.tieneRiesgos && EC.eatP50 > T.capex)
        alertas.push({ codigo:'eat_comb_p50', level:'amber', icon:'merge_type', msg:`EAT combinado P50 (${U(EC.eatP50)}) supera CAPEX al incluir riesgos MC.`, rec:'Revisar plan de respuesta a riesgos de mayor impacto.' });

    if (T.eatP80 != null && T.eatP80 > T.capex)
        alertas.push({ codigo:'eat_mc_p80', level:'amber', icon:'trending_up', msg:`EAT P80 agregado (${U(T.eatP80)}) excede CAPEX (sin riesgos MC).`, rec:null });

    if (T.contratosSinMc > 0)
        alertas.push({ codigo:'sin_mc', level:'amber', icon:'casino', msg:`${T.contratosSinMc} contrato${T.contratosSinMc>1?'s':''} sin análisis Monte Carlo. EAT probabilístico incompleto.`, rec:'Ejecuta MC en la pestaña F — Clasificación de cada contrato pendiente.' });

    const pctIncCosto = T.incertidumbre / ((T.certeza + T.incertidumbre) || 1) * 100;
    if (pctIncCosto > 60)
        alertas.push({ codigo:'pct_incert', level:'amber', icon:'help_outline', msg:`El ${Pct(pctIncCosto)} del costo total está en ítems de incertidumbre.`, rec:'Considere aumentar la precisión de los estimados clave.' });

    if (RD?.topRiesgos?.some(r => !r.tieneRespuesta && r.nivel >= 4))
        alertas.push({ codigo:'riesgo_sin_plan', level:'amber', icon:'warning_amber', msg:'Hay riesgos Nivel 4-5 (Muy Probable / Casi Seguro) sin plan de respuesta.', rec:'Definir acciones de mitigación para riesgos de mayor probabilidad.' });

    data.contratos.forEach(c => {
        if (c.pctEAT > 115)    alertas.push({ codigo:'contrato_eat', level:'red',   icon:'receipt_long', msg:`Contrato ${c.codigo}: EAT supera CAPEX en ${Pct(c.pctEAT-100)}.`, rec:null });
        if (c.mcCV != null && c.mcCV > 20) alertas.push({ codigo:'contrato_cv', level:'amber', icon:'casino', msg:`Contrato ${c.codigo}: CV MC ${fmtCV(c.mcCV)} — alta dispersión probabilística.`, rec:null });
    });

    if (!alertas.length)
        alertas.push({ codigo:'todo_ok', level:'green', icon:'check_circle', msg:'Sin alertas activas. El proyecto se encuentra en parámetros normales.', rec:null });

    const priAlerta = { red: 0, amber: 1, green: 2 };
    alertas.sort((a, b) => (priAlerta[a.level] ?? 9) - (priAlerta[b.level] ?? 9));

    // ══════ SECCIÓN A — Tornado Chart (Análisis de Sensibilidad) ═══════════
    let secTornado = '';
    const TORN = data.tornado;
    if (TORN?.barras?.length) {
        const maxSwing = TORN.barras[0].swing;
        const tBase    = TORN.eatBase;
        const rangeMin = Math.min(...TORN.barras.map(x => Math.min(x.eatMin, x.eatMax)));
        const rangeMax = Math.max(...TORN.barras.map(x => Math.max(x.eatMin, x.eatMax)));
        const range    = rangeMax - rangeMin || 1;
        const baseLeft = ((tBase - rangeMin) / range * 100).toFixed(1);
        const maxSwingVsCapex = T.capex > 0 ? (maxSwing / T.capex) * 100 : null;
        const sumSwingTop3 = TORN.barras.slice(0, 3).reduce((s, b) => s + b.swing, 0);

        const tornadoBarras = TORN.barras.map(b => {
            const leftPx  = Math.min(b.eatMin, b.eatMax);
            const rightPx = Math.max(b.eatMin, b.eatMax);
            const barLeft  = ((leftPx  - rangeMin) / range * 100).toFixed(1);
            const barW     = ((rightPx - leftPx)   / range * 100).toFixed(1);
            const color    = b.esRiesgo ? '#f59e0b' : '#6366f1';
            const colorBg  = b.esRiesgo ? 'rgba(245,158,11,0.20)' : 'rgba(99,102,241,0.20)';
            const swingPct = (b.swing / maxSwing * 100).toFixed(0);
            const nombre   = b.nombre.length > 38 ? b.nombre.slice(0,36)+'…' : b.nombre;
            const barTitle = `${b.esRiesgo ? 'Riesgo' : 'Ítem'}: ${b.nombre} · EAT mín (solo esta var. al mín.): ${U(b.eatMin)} · EAT máx (solo esta var. al máx.): ${U(b.eatMax)} · Swing: ${U(b.swing)}`;
            return `<div class="mb-1.5">
                <div class="flex items-center gap-2 text-xs mb-0.5">
                    <span class="w-3 h-3 rounded-sm flex-shrink-0" style="background:${colorBg};border:1px solid ${color}" title="${b.esRiesgo ? 'Riesgo MC (sensibilidad de impacto)' : 'Ítem de incertidumbre'}"></span>
                    <span class="flex-1 text-slate-600 dark:text-slate-400 truncate min-w-0" title="${esc(b.nombre)}">${esc(nombre)}</span>
                    <span class="text-right flex-shrink-0 w-20 sm:w-24">
                        <span class="block tabular-nums text-slate-500 dark:text-slate-400 font-medium">${swingPct}%<span class="text-[10px] font-normal text-slate-400"> rel.</span></span>
                        <span class="block tabular-nums text-[10px] text-slate-400">${U(b.swing)}</span>
                    </span>
                </div>
                <div class="relative h-4 bg-slate-100 dark:bg-slate-700 rounded overflow-hidden" title="${esc(barTitle)}">
                    <div class="absolute top-0 h-4 rounded" style="left:${barLeft}%;width:${barW}%;background:${color};opacity:0.7"></div>
                    <div class="absolute top-0 bottom-0 w-0.5 bg-indigo-500 z-10" style="left:${baseLeft}%" title="EAT Base de referencia: ${U(tBase)}"></div>
                </div>
            </div>`;
        }).join('');

        secTornado = `
        <details class="tc-minimized-disclosure mb-4 mt-6">
            <summary class="flex items-center gap-3 cursor-pointer select-none flex-wrap">
                <span class="material-icons tc-minimized-section-chevron" aria-hidden="true">expand_more</span>
                <div class="w-1 h-6 bg-indigo-400 rounded-full flex-shrink-0"></div>
                <h2 class="text-base font-semibold text-slate-800 dark:text-slate-200 flex items-center gap-1">
                    Tornado — Análisis de Sensibilidad
                    ${TT('Análisis determinístico tipo “un factor a la vez”: para cada ítem se recorre Min→Máx triangular dejando el resto en su probable; para cada riesgo se recorre el impacto triangular escalado por su probabilidad (p fija). El swing = |EAT máx − EAT mín| mide cuánto mueve el total ese elemento. No reemplaza el Monte Carlo (no modela interacciones ni correlación); sirve para priorizar dónde reducir rango o mejorar definición. La línea índigo es el EAT base de esa misma referencia.', 'gpr-tt-right')}
                </h2>
                <span class="text-xs text-slate-400 bg-slate-100 dark:bg-slate-800 px-2 py-0.5 rounded-full ml-auto">
                    Top ${TORN.barras.length} · ${TORN.nItems} ítems · ${TORN.nRiesgos} riesgos
                </span>
            </summary>
            <div class="tc-card mb-0 mt-3">
                <p class="text-xs text-slate-500 dark:text-slate-400 mb-3">
                    <strong class="text-slate-600 dark:text-slate-300">Referencia EAT Base:</strong> ${U(tBase)}
                    <span class="text-slate-400">(comprometido + certeza + Σ probable ítems inciertos + Σ VE riesgos).</span>
                    <span class="inline-block w-3 h-1 bg-indigo-500 align-middle mx-1"></span>línea vertical en cada barra.
                    <span class="block mt-1.5">Columna derecha: <strong>% rel.</strong> = swing del ítem respecto al mayor del ranking; debajo, <strong>swing en USD</strong> (impacto bruto en el EAT total).</span>
                </p>
                <div class="flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500 mb-3">
                    <span><span class="inline-block w-3 h-3 rounded-sm mr-1 align-middle" style="background:rgba(99,102,241,0.2);border:1px solid #6366f1"></span>Ítems incertidumbre</span>
                    <span><span class="inline-block w-3 h-3 rounded-sm mr-1 align-middle" style="background:rgba(245,158,11,0.2);border:1px solid #f59e0b"></span>Riesgos (sensib. impacto, p fija)</span>
                    <span class="sm:ml-auto font-medium text-slate-600 dark:text-slate-300">Swing máx: ${U(maxSwing)}${maxSwingVsCapex != null ? ` · ${tcFmt(maxSwingVsCapex, 1)}% del CAPEX` : ''}</span>
                </div>
                <div class="rounded-lg bg-indigo-50/50 dark:bg-indigo-900/15 border border-indigo-100 dark:border-indigo-800/50 px-3 py-2 text-xs text-indigo-900 dark:text-indigo-100 mb-3">
                    <span class="material-icons text-sm align-middle mr-1" style="vertical-align:-2px">tips_and_updates</span>
                    <strong>Lectura:</strong> el ítem con mayor swing es el que más amplía el rango del EAT al variar solo esa variable; focalizar definición contractual, ingeniería o cotizaciones ahí suele ser la palanca más eficiente antes de mitigar riesgos de cola más abajo en el ranking.
                    ${sumSwingTop3 > 0 ? ` Los <strong>3 primeros</strong> suman <strong>${U(sumSwingTop3)}</strong> de swing agregado (no aditivo en escenarios reales, pero indica concentración de sensibilidad).` : ''}
                </div>
                ${tornadoBarras}
            </div>
        </details>`;
    }

    // ══════ SECCIÓN B — What-If Escenarios ═════════════════════════════════
    let secEscenarios = '';
    const ESC = data.escenarios;
    if (ESC) {
        const wfTip = text =>
            `<span class="gpr-tt gpr-tt-left inline-flex align-middle flex-shrink-0"><span class="material-icons gpr-tt-icon" style="font-size:11px;opacity:0.55">help_outline</span><span class="gpr-tt-box">${esc(text)}</span></span>`;
        const eatBaseWf = ESC.base?.eat ?? 0;
        const spreadWf  = (ESC.pesimista?.eat ?? 0) - (ESC.optimista?.eat ?? 0);
        const spreadPctBase = eatBaseWf > 0 ? (spreadWf / eatBaseWf) * 100 : 0;
        const capexWf = T.capex ?? 0;
        const gapCx = eat => (capexWf > 0 ? eat - capexWf : null);
        const fmtGap = g => g == null ? '—' : `${g > 0 ? '+' : ''}${U(g)}`;

        const scenTips = {
            optimista: 'Todos los ítems inciertos en su mínimo triangular. Amenazas: probabilidad × 0,5 y el impacto valorado en la moda (menor VE). Oportunidades: probabilidad hasta ×1,5 (tope 100%) y moda. Escenario de “mejor caso” operativo; no equivale al P10 del Monte Carlo.',
            base:      'Ítems en valor probable y riesgos como Σ (probabilidad × moda), misma lógica de referencia determinística que el análisis Tornado (EAT base del bloque). Punto de anclaje para comparar Optimista y Pesimista.',
            pesimista: 'Ítems inciertos en su máximo. Amenazas: probabilidad ×1,5 (tope 100%) aplicada al impacto máximo triangular. Oportunidades: probabilidad ×0,5 sobre el mínimo triangular (menos ahorro). Aproxima un “peor caso” estructurado sin simulación.'
        };

        const scenCards = [ESC.optimista, ESC.base, ESC.pesimista].map(s => {
            const clrMap = { emerald:'border-emerald-200 dark:border-emerald-800 bg-emerald-50 dark:bg-emerald-900/20',
                             blue:   'border-blue-200 dark:border-blue-800 bg-blue-50 dark:bg-blue-900/20',
                             red:    'border-red-200 dark:border-red-800 bg-red-50 dark:bg-red-900/20' };
            const txtMap = { emerald:'text-emerald-700 dark:text-emerald-300', blue:'text-blue-700 dark:text-blue-300', red:'text-red-700 dark:text-red-300' };
            const cls    = clrMap[s.color] || clrMap.blue;
            const txt    = txtMap[s.color] || txtMap.blue;
            const sign   = s.vsBase >= 0 ? '+' : '';
            const tipKey = s.nombre === 'Optimista' ? 'optimista' : s.nombre === 'Pesimista' ? 'pesimista' : 'base';
            const gCx    = gapCx(s.eat);
            const gapCls = gCx == null ? 'text-slate-500' : gCx > 0 ? 'text-red-600 dark:text-red-400 font-semibold' : 'text-emerald-600 dark:text-emerald-400 font-semibold';
            return `<div class="rounded-xl border p-4 ${cls}">
                <div class="flex items-start justify-between gap-1 mb-1">
                    <div class="font-bold text-sm ${txt}">${esc(s.nombre)}</div>
                    ${wfTip(scenTips[tipKey])}
                </div>
                <div class="text-xs text-slate-600 dark:text-slate-400 mb-3 leading-snug">${esc(s.descripcion)}</div>
                <div class="text-lg font-bold tabular-nums ${txt} mb-2">${U(s.eat)}</div>
                <div class="space-y-1 text-xs">
                    <div class="flex justify-between gap-2"><span class="text-slate-500 flex items-center gap-0.5 min-w-0">% CAPEX ${wfTip('EAT del escenario dividido por el CAPEX total del proyecto (Resumen Global). >100% indica sobrecosto vs techo en ese supuesto.')}</span><span class="font-semibold flex-shrink-0 ${s.pctCapex>115?'text-red-600':s.pctCapex>105?'text-amber-600':'text-emerald-600'}">${tcFmt(s.pctCapex,1)}%</span></div>
                    ${capexWf > 0 ? `<div class="flex justify-between gap-2"><span class="text-slate-500 flex items-center gap-0.5 min-w-0">Brecha vs CAPEX ${wfTip('EAT del escenario menos CAPEX. Positivo = sobrecosto respecto al presupuesto aprobado en ese supuesto; negativo = margen bajo el techo.')}</span><span class="${gapCls} flex-shrink-0 tabular-nums">${fmtGap(gCx)}</span></div>` : ''}
                    ${s.vsBase!==0?`<div class="flex justify-between"><span class="text-slate-500">vs esc. Base</span><span class="font-semibold ${s.vsBase>0?'text-red-600':'text-emerald-600'}">${sign}${tcFmt(s.vsBase,1)}%</span></div>`:''}
                    ${s.eatItemsDelta!==0?`<div class="flex justify-between"><span class="text-slate-500">Δ Ítems vs Base</span><span class="${s.eatItemsDelta>0?'text-red-500':'text-emerald-500'} tabular-nums">${s.eatItemsDelta>0?'+':''}${U(s.eatItemsDelta)}</span></div>`:''}
                    ${s.eatRiesgosDelta!==0?`<div class="flex justify-between"><span class="text-slate-500">Δ Riesgos vs Base</span><span class="${s.eatRiesgosDelta>0?'text-red-500':'text-emerald-500'} tabular-nums">${s.eatRiesgosDelta>0?'+':''}${U(s.eatRiesgosDelta)}</span></div>`:''}
                </div>
            </div>`;
        }).join('');

        const pesSupCx = capexWf > 0 && ESC.pesimista.eat > capexWf;
        const optBajCx = capexWf > 0 && ESC.optimista.eat <= capexWf;
        const lecturaWf = pesSupCx && optBajCx
            ? `En este proyecto el <strong>pesimista</strong> supera el CAPEX mientras el <strong>optimista</strong> queda en o bajo el techo: el rango What-If atraviesa la viabilidad presupuestaria — conviene cruzar con P80/P90 del MC.`
            : pesSupCx
            ? `El escenario <strong>pesimista</strong> queda por encima del CAPEX: el “peor caso” estructurado ya tensiona el techo; revise contingencia y alcance.`
            : `Ninguno de los tres escenarios supera el CAPEX en este corte; la banda What-If sigue siendo una aproximación — el MC captura colas y correlación que aquí no aparecen.`;

        secEscenarios = `
        <details class="tc-minimized-disclosure mb-4 mt-6">
            <summary class="flex items-center gap-3 cursor-pointer select-none flex-wrap">
                <span class="material-icons tc-minimized-section-chevron" aria-hidden="true">expand_more</span>
                <div class="w-1 h-6 bg-teal-500 rounded-full flex-shrink-0"></div>
                <h2 class="text-base font-semibold text-slate-800 dark:text-slate-200 flex items-center gap-1">
                    What-If — Escenarios Estructurados
                    ${TT('Tres escenarios 100% determinísticos (sin MC): Optimista pone ítems en mínimo y ajusta probabilidades de amenazas/oportunidades; Base usa probables y p×moda; Pesimista ítems en máximo y el peor perfil de riesgo según las reglas del motor. Útil para ver el ancho del EAT y el cruce vs CAPEX; no sustituye percentiles simulados ni correlación.', 'gpr-tt-right')}
                </h2>
                <span class="text-xs text-slate-500 bg-slate-100 dark:bg-slate-800 px-2 py-0.5 rounded-full ml-auto tabular-nums">Banda ${U(spreadWf)}</span>
            </summary>
            <div class="tc-card mb-0 mt-3">
                <p class="text-xs text-slate-500 dark:text-slate-400 mb-3">
                    <strong class="text-slate-600 dark:text-slate-300">Referencia:</strong> el escenario <strong>Base</strong> usa EAT = comprometido + certeza + Σ probable (ítems inciertos) + Σ (prob × moda) de riesgos — coherente con la referencia determinística del Tornado.
                    <span class="block mt-1.5"><strong>EAT Base (What-If):</strong> ${U(eatBaseWf)} · <strong>Spread Optimista→Pesimista:</strong> ${U(spreadWf)} (${tcFmt(spreadPctBase, 1)}% sobre Base)</span>
                </p>
                <div class="rounded-lg bg-teal-50/50 dark:bg-teal-900/15 border border-teal-100 dark:border-teal-800/50 px-3 py-2 text-xs text-teal-900 dark:text-teal-100 mb-4">
                    <span class="material-icons text-sm align-middle mr-1" style="vertical-align:-2px">tips_and_updates</span>
                    <strong>Lectura:</strong> ${lecturaWf}
                </div>
                <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
                    ${scenCards}
                </div>
            </div>
        </details>`;
    }

    // ══════ SECCIÓN C — Correlación (Cópula Gaussiana) ═════════════════════
    const nRisksCorr  = (RD?.nRiesgos ?? 0);
    const nItemsCorr  = data.eatCombinado?.nItems ?? 0;
    const corrProyId  = data.proyecto?.id;
    const hasCorrData = nRisksCorr > 0 || nItemsCorr > 0;

    // Source-info helper: returns [N, description] for each source
    const corrSrcInfo = {
        contratos: [nItemsCorr,  `${nItemsCorr} ítems de incertidumbre (contratos)`],
        riesgos:   [nRisksCorr,  `${nRisksCorr} riesgos del proyecto`],
        ambos:     [nItemsCorr + nRisksCorr, `${nItemsCorr} ítems + ${nRisksCorr} riesgos`]
    };

    const srcBtnBase = 'py-1.5 px-3 text-xs font-medium transition-colors border-r border-rose-200 dark:border-rose-700 last:border-r-0';
    const srcBtnActive = 'bg-rose-600 text-white';
    const srcBtnInactive = 'text-rose-700 dark:text-rose-300 hover:bg-rose-50 dark:hover:bg-rose-900/20';

    const corrPresetHtml = `
        <div class="flex gap-1 mb-1 flex-wrap" id="corr-preset-btns">
            <button onclick="tcCorrPreset(0,this)" data-corr-preset="0" class="btn-secondary py-1 px-2 text-xs">Sin correlación</button>
            <button onclick="tcCorrPreset(0.2,this)" data-corr-preset="0.2" class="btn-secondary py-1 px-2 text-xs">Baja (0.2)</button>
            <button onclick="tcCorrPreset(0.5,this)" data-corr-preset="0.5" class="btn-secondary py-1 px-2 text-xs">Media (0.5)</button>
            <button onclick="tcCorrPreset(0.8,this)" data-corr-preset="0.8" class="btn-secondary py-1 px-2 text-xs">Alta (0.8)</button>
        </div>
        <div id="corr-preset-active-label" class="hidden text-xs text-indigo-600 dark:text-indigo-400 font-medium mb-3"></div>`;

    const corrSrcSelector = `
        <div class="flex flex-wrap items-center gap-2 mb-3">
            <span class="text-xs font-semibold text-slate-600 dark:text-slate-300">Variables desde:</span>
            <div class="flex rounded-lg border border-rose-200 dark:border-rose-700 overflow-hidden text-xs font-medium">
                <button type="button" id="tc-corr-src-contratos" onclick="tcCorrSetSource('contratos',this)"
                        class="${srcBtnBase} ${nItemsCorr === 0 ? 'opacity-40 cursor-not-allowed' : srcBtnInactive}"
                        ${nItemsCorr === 0 ? 'disabled' : ''}>
                    <span class="material-icons" style="font-size:11px;vertical-align:-2px">receipt_long</span> Contratos
                    <span class="ml-1 text-slate-400 dark:text-slate-500 font-normal">(${nItemsCorr})</span>
                </button>
                <button type="button" id="tc-corr-src-riesgos" onclick="tcCorrSetSource('riesgos',this)"
                        class="${srcBtnBase} ${nRisksCorr === 0 ? 'opacity-40 cursor-not-allowed' : srcBtnActive}"
                        ${nRisksCorr === 0 ? 'disabled' : ''}>
                    <span class="material-icons" style="font-size:11px;vertical-align:-2px">shield</span> Riesgos
                    <span class="ml-1 ${nRisksCorr === 0 ? 'opacity-0' : 'opacity-70'} font-normal">(${nRisksCorr})</span>
                </button>
                <button type="button" id="tc-corr-src-ambos" onclick="tcCorrSetSource('ambos',this)"
                        class="${srcBtnBase} ${(nItemsCorr === 0 || nRisksCorr === 0) ? 'opacity-40 cursor-not-allowed' : srcBtnInactive}"
                        ${(nItemsCorr === 0 || nRisksCorr === 0) ? 'disabled' : ''}>
                    <span class="material-icons" style="font-size:11px;vertical-align:-2px">merge</span> Ambos
                    <span class="ml-1 text-slate-400 dark:text-slate-500 font-normal">(${nItemsCorr + nRisksCorr})</span>
                </button>
            </div>
            <span id="corr-src-info-badge" class="text-xs text-slate-400 dark:text-slate-500"></span>
        </div>`;

    const secCorrelacion = `
    <div class="flex items-center gap-3 mb-4 mt-6">
        <div class="w-1 h-6 bg-rose-500 rounded-full flex-shrink-0"></div>
        <h2 class="text-base font-semibold text-slate-800 dark:text-slate-200 flex items-center gap-1">
            Correlación — Cópula Gaussiana
            ${TT('Modela la correlación entre variables del proyecto (contratos, riesgos, o ambos) usando la Cópula Gaussiana con Descomposición de Cholesky (L·Lᵀ = Ρ). Variables con correlación positiva (ρ>0) tienden a materializarse juntas, aumentando la cola del EAT — lo que eleva el P90 y el CVaR90 respecto al modelo independiente.', 'gpr-tt-right')}
        </h2>
        <span class="text-xs text-slate-400 bg-slate-100 dark:bg-slate-800 px-2 py-0.5 rounded-full ml-auto">${nItemsCorr} ítems · ${nRisksCorr} riesgos</span>
    </div>
    <div class="tc-card mb-4">
        <p class="text-xs text-slate-500 mb-4">
            Selecciona qué variables correlacionar y el nivel de correlación uniforme entre ellas.
            La Cópula Gaussiana (Cholesky) modela la dependencia estructural — si dos variables están correlacionadas,
            sus valores extremos tienden a coincidir, engrosando la cola adversa del EAT.
            <span class="block mt-2 text-slate-600 dark:text-slate-400 border-l-2 border-rose-300 dark:border-rose-700 pl-2">
                <strong class="font-semibold text-slate-700 dark:text-slate-300">Lectura:</strong> los montos del resultado son <strong>EAT total simulado</strong>
                (comprometido + certeza + ítems inciertos + riesgos MC), alineado con el EAT probabilístico combinado del resumen.
                «Sin correlación» aquí es simulación <em>independiente</em> entre las variables elegidas (ρ homogéneo fuera de la diagonal), no el bloque «Monte Carlo Agregado — Sólo Incertidumbre».
            </span>
        </p>
        <div id="corr-config-panel"
             data-proy="${corrProyId}"
             data-n-items="${nItemsCorr}"
             data-n-risks="${nRisksCorr}">
            ${hasCorrData ? corrSrcSelector : ''}
            ${hasCorrData ? corrPresetHtml : `<p class="text-xs text-slate-400 mb-3">Sin ítems ni riesgos cargados. Define contratos con incertidumbre o riesgos para activar el análisis.</p>`}
            ${hasCorrData ? `<button onclick="tcEjecutarCorrelacion()"
                id="tc-corr-calc-btn"
                class="btn-primary py-1.5 px-4 text-sm flex items-center gap-2 opacity-50 cursor-not-allowed" disabled>
                <span class="material-icons text-base">hub</span> Calcular con Correlación
            </button>
            <p class="text-xs text-slate-400 mt-1.5" id="tc-corr-hint">Selecciona un nivel de correlación para habilitar el cálculo.</p>` : ''}
        </div>
        <div id="corr-result-panel" class="hidden mt-4"></div>
    </div>`;

    const critCount = alertas.filter(a => a.level === 'red').length;
    const warnCount = alertas.filter(a => a.level === 'amber').length;
    const aColors = {
        red:   'bg-red-50 dark:bg-red-900/20 border-red-200 dark:border-red-800 text-red-800 dark:text-red-200',
        amber: 'bg-amber-50 dark:bg-amber-900/20 border-amber-200 dark:border-amber-800 text-amber-800 dark:text-amber-200',
        green: 'bg-emerald-50 dark:bg-emerald-900/20 border-emerald-200 dark:border-emerald-800 text-emerald-800 dark:text-emerald-200'
    };
    const lblNivelAlerta = { red: 'Crítica', amber: 'Advertencia', green: 'Resumen OK' };
    const alTip = text =>
        `<span class="gpr-tt gpr-tt-left inline-flex align-middle flex-shrink-0"><span class="material-icons gpr-tt-icon" style="font-size:11px;opacity:0.55">help_outline</span><span class="gpr-tt-box">${esc(text)}</span></span>`;
    const badgeClsAlerta = {
        red:   'bg-red-200/60 dark:bg-red-900/50 text-red-900 dark:text-red-100',
        amber: 'bg-amber-200/60 dark:bg-amber-900/50 text-amber-900 dark:text-amber-100',
        green: 'bg-emerald-200/60 dark:bg-emerald-900/50 text-emerald-900 dark:text-emerald-100'
    };

    const alertasCardsHtml = alertas.map(a => {
        const hint = a.codigo && hintAlerta[a.codigo] ? hintAlerta[a.codigo] : '';
        const badge = lblNivelAlerta[a.level] || a.level;
        return `<div class="flex items-start gap-3 p-3 rounded-xl border ${aColors[a.level]} shadow-sm">
                <span class="material-icons text-xl flex-shrink-0 mt-0.5 opacity-90">${a.icon}</span>
                <div class="flex-1 min-w-0">
                    <div class="flex flex-wrap items-center gap-2 mb-1">
                        <span class="text-[10px] font-bold uppercase tracking-wide px-1.5 py-0.5 rounded ${badgeClsAlerta[a.level]}">${badge}</span>
                        ${hint ? alTip(hint) : ''}
                    </div>
                    <div class="text-sm leading-snug">${a.msg}</div>
                    ${a.rec ? `<div class="text-xs opacity-85 dark:opacity-80 mt-2 pl-2 border-l-2 border-current/25">
                        <span class="font-semibold">Acción sugerida:</span> ${a.rec}
                    </div>` : ''}
                </div>
            </div>`;
    }).join('');

    const sec5 = `
    <details id="tc-resumen-alertas" class="tc-minimized-disclosure mb-6 mt-6">
        <summary class="flex items-center gap-3 cursor-pointer select-none flex-wrap">
            <span class="material-icons tc-minimized-section-chevron" aria-hidden="true">expand_more</span>
            <div class="w-1 h-6 bg-yellow-500 rounded-full flex-shrink-0"></div>
            <h2 class="text-base font-semibold text-slate-800 dark:text-slate-200 flex items-center gap-1">
                Alertas y Recomendaciones
                ${TT('Reglas automáticas evaluadas sobre los datos de este proyecto: EAT determinístico y probabilístico vs CAPEX, P50/P80 combinado, cobertura MC por contrato, peso de la incertidumbre en el costo y riesgos Nivel CODELCO 4–5 sin plan. Son una ayuda para priorizar; no reemplazan revisión humana ni cubren todos los riesgos del negocio.', 'gpr-tt-right')}
            </h2>
            <div class="flex gap-1.5 ml-auto flex-wrap justify-end items-center">
                ${critCount > 0 ? `<span class="text-xs bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300 px-2 py-0.5 rounded-full border border-red-200 dark:border-red-800 font-medium">● ${critCount} crítica${critCount > 1 ? 's' : ''}</span>` : ''}
                ${warnCount > 0 ? `<span class="text-xs bg-amber-100 dark:bg-amber-900/30 text-amber-800 dark:text-amber-200 px-2 py-0.5 rounded-full border border-amber-200 dark:border-amber-800 font-medium">⚠ ${warnCount} advertencia${warnCount > 1 ? 's' : ''}</span>` : ''}
                ${critCount === 0 && warnCount === 0 ? `<span class="text-xs bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-300 px-2 py-0.5 rounded-full border border-emerald-200 dark:border-emerald-800 font-medium">✓ Sin incumplimientos</span>` : ''}
            </div>
        </summary>
        <div class="mt-4 space-y-3">
            <div class="rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50/90 dark:bg-slate-800/50 px-3 py-2.5 text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
                <span class="material-icons text-slate-400 align-middle mr-1" style="font-size:14px;vertical-align:-2px">lightbulb_outline</span>
                <strong class="text-slate-700 dark:text-slate-200">Lectura:</strong> las tarjetas están <strong>ordenadas por severidad</strong> (críticas → advertencias → estado OK).
                El ícono <span class="material-icons align-middle text-slate-400" style="font-size:13px;vertical-align:-2px">help_outline</span> junto al rótulo de cada alerta explica el criterio o umbral GPR. Las acciones sugeridas son orientativas.
            </div>
            <div class="space-y-2">
                ${alertasCardsHtml}
            </div>
        </div>
    </details>`;


    return sec1 + sec2 + sec3 + sec4 + secCorrelacion + secTornado + secEscenarios + sec5;
}

// Renderiza el histograma Chart.js tras insertar el HTML del resumen
let _tcHistEatChart = null;
function renderHistogramaEAT(EC, T) {
    const canvas = document.getElementById('histograma-eat-v2');
    if (!canvas || !EC?.histograma?.length) return;

    if (_tcHistEatChart) { _tcHistEatChart.destroy(); _tcHistEatChart = null; }

    const hist = EC.histograma;
    // Los bins del backend son EAT TOTAL por iteración (comprometido + certeza + MC + riesgos), en USD absolutos.
    const p50Abs = EC.eatP50 ?? EC.p50;
    const p80Abs = EC.eatP80 ?? EC.p80;
    const eatDet = EC.eatDeterministico;
    const capex  = T?.capex ?? 0;

    const spanM = (hist[hist.length - 1].hasta - hist[0].desde) / 1e6;
    const decM  = spanM > 0 && spanM < 50 ? 2 : 1;
    /** Eje / tooltip estilo informe de riesgo: siempre en millones USD cuando el proyecto es grande. */
    const fmtM = v => {
        if (v == null || !isFinite(v)) return '—';
        const m = v / 1e6;
        return '$' + m.toFixed(decM) + 'M';
    };
    const fmtRangeTitle = (d0, d1) => `${fmtM(d0)} – ${fmtM(d1)}`;

    // Eje X: centro del bin en M USD (lectura tipo «Valores en millones»)
    const labels = hist.map(b => fmtM((b.desde + b.hasta) / 2));
    const freqs  = hist.map(b => +(b.frecuencia * 100).toFixed(2));
    const maxFreqPct = Math.max(...freqs, 0.01);
    const yAxisCap = Math.min(100, Math.max(5, Math.ceil(maxFreqPct * 1.2 * 10) / 10));

    const colors = hist.map(b => {
        const mid = (b.desde + b.hasta) / 2;
        return mid <= p50Abs ? 'rgba(59,130,246,0.82)' :
               mid >= p80Abs ? 'rgba(239,68,68,0.82)'  : 'rgba(245,158,11,0.82)';
    });

    const minVal = hist[0].desde;
    const maxVal = hist[hist.length - 1].hasta;
    const xFrac  = val => (maxVal > minVal) ? (val - minVal) / (maxVal - minVal) : 0.5;

    const refLinePlugin = {
        id: 'tcHistRefLines',
        afterDraw(chart) {
            const { ctx, chartArea } = chart;
            const drawV = (val, stroke, dash, label, labelColor) => {
                if (val == null || !isFinite(val) || val < minVal || val > maxVal) return;
                const x = chartArea.left + xFrac(val) * (chartArea.right - chartArea.left);
                ctx.save();
                ctx.strokeStyle = stroke;
                ctx.lineWidth   = val === p50Abs || val === p80Abs ? 1.5 : 2;
                ctx.setLineDash(dash);
                ctx.beginPath();
                ctx.moveTo(x, chartArea.top);
                ctx.lineTo(x, chartArea.bottom);
                ctx.stroke();
                ctx.setLineDash([]);
                ctx.fillStyle = labelColor || stroke;
                ctx.font      = 'bold 9px system-ui,sans-serif';
                ctx.textAlign = 'center';
                ctx.fillText(label, x, chartArea.top - 2);
                ctx.restore();
            };
            drawV(p50Abs, '#10b981', [4, 3], 'P50', '#059669');
            drawV(p80Abs, '#d97706', [4, 3], 'P80', '#b45309');
            drawV(eatDet, '#6366f1', [6, 4], 'Det.', '#4f46e5');
            if (capex > 0) drawV(capex, '#4338ca', [2, 2], 'CAPEX', '#3730a3');
        }
    };

    _tcHistEatChart = new Chart(canvas, {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                label: 'Frecuencia',
                data: freqs,
                backgroundColor: colors,
                borderColor: colors.map(c => c.replace('0.82', '1')),
                borderWidth: 1,
                borderRadius: 1,
                barPercentage: 1.0,
                categoryPercentage: 1.0
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            layout: { padding: { top: 20, right: 6, bottom: 4, left: 4 } },
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: 'rgba(15,23,42,0.92)',
                    titleFont: { size: 12, weight: '600' },
                    bodyFont: { size: 11 },
                    padding: 10,
                    callbacks: {
                        title: items => {
                            const i = items[0].dataIndex;
                            return 'Rango: ' + fmtRangeTitle(hist[i].desde, hist[i].hasta);
                        },
                        label: item => 'Frecuencia: ' + item.raw.toFixed(2) + '% de iteraciones'
                    }
                }
            },
            scales: {
                x: {
                    title: {
                        display: true,
                        text: 'EAT total simulado (millones USD)',
                        color: '#64748b',
                        font: { size: 10, weight: '600' }
                    },
                    ticks: {
                        maxRotation: 0,
                        minRotation: 0,
                        autoSkip: true,
                        maxTicksLimit: 14,
                        font: { size: 9 },
                        color: '#64748b'
                    },
                    grid: { display: false }
                },
                y: {
                    title: {
                        display: true,
                        text: '% iteraciones',
                        color: '#64748b',
                        font: { size: 10, weight: '600' }
                    },
                    beginAtZero: true,
                    suggestedMax: yAxisCap,
                    ticks: {
                        font: { size: 9 },
                        color: '#94a3b8',
                        callback: v => (Number.isInteger(v) ? v : v.toFixed(1)) + '%'
                    },
                    grid: { color: 'rgba(148,163,184,0.18)' }
                }
            },
            animation: { duration: 500, easing: 'easeOutQuart' }
        },
        plugins: [refLinePlugin]
    });
}

/** Histograma de Δ EAT por riesgos (conjunto − solo ítems), misma corrida MC; escala en M USD propia al incremento. */
let _tcHistRiesgoNetoChart = null;
function renderHistogramaRiesgoNeto(EC) {
    const canvas = document.getElementById('histograma-riesgo-neto-v2');
    const hist = EC?.histogramaRiesgoNeto;
    if (!canvas || !hist?.length) return;

    if (_tcHistRiesgoNetoChart) { _tcHistRiesgoNetoChart.destroy(); _tcHistRiesgoNetoChart = null; }

    const p50d = EC.deltaRiesgosP50;
    const p80d = EC.deltaRiesgosP80;
    if (p50d == null || p80d == null || !isFinite(p50d) || !isFinite(p80d)) return;

    const spanM = (hist[hist.length - 1].hasta - hist[0].desde) / 1e6;
    const decM  = spanM > 0 && Math.abs(spanM) < 50 ? 2 : 1;
    const fmtM = v => {
        if (v == null || !isFinite(v)) return '—';
        const m = v / 1e6;
        const s = (m >= 0 ? '' : '−') + '$' + Math.abs(m).toFixed(decM) + 'M';
        return s;
    };
    const fmtRangeTitle = (d0, d1) => `${fmtM(d0)} – ${fmtM(d1)}`;

    const labels = hist.map(b => fmtM((b.desde + b.hasta) / 2));
    const freqs  = hist.map(b => +(b.frecuencia * 100).toFixed(2));
    const maxFreqPct = Math.max(...freqs, 0.01);
    const yAxisCap = Math.min(100, Math.max(5, Math.ceil(maxFreqPct * 1.2 * 10) / 10));

    const colors = hist.map(b => {
        const mid = (b.desde + b.hasta) / 2;
        return mid <= p50d ? 'rgba(59,130,246,0.82)' :
               mid >= p80d ? 'rgba(239,68,68,0.82)'  : 'rgba(245,158,11,0.82)';
    });

    const minVal = hist[0].desde;
    const maxVal = hist[hist.length - 1].hasta;
    const xFrac  = val => (maxVal > minVal) ? (val - minVal) / (maxVal - minVal) : 0.5;
    const zeroInRange = minVal < 0 && maxVal > 0;

    const refLinePlugin = {
        id: 'tcHistRiesgoNetoRef',
        afterDraw(chart) {
            const { ctx, chartArea } = chart;
            const drawV = (val, stroke, dash, label, labelColor, bold) => {
                if (val == null || !isFinite(val) || val < minVal || val > maxVal) return;
                const x = chartArea.left + xFrac(val) * (chartArea.right - chartArea.left);
                ctx.save();
                ctx.strokeStyle = stroke;
                ctx.lineWidth   = bold ? 1.5 : 1.25;
                ctx.setLineDash(dash);
                ctx.beginPath();
                ctx.moveTo(x, chartArea.top);
                ctx.lineTo(x, chartArea.bottom);
                ctx.stroke();
                ctx.setLineDash([]);
                ctx.fillStyle = labelColor || stroke;
                ctx.font      = (bold ? 'bold ' : '') + '9px system-ui,sans-serif';
                ctx.textAlign = 'center';
                ctx.fillText(label, x, chartArea.top - 2);
                ctx.restore();
            };
            if (zeroInRange) drawV(0, '#64748b', [5, 3], '0', '#475569', false);
            drawV(p50d, '#10b981', [4, 3], 'P50 Δ', '#059669', true);
            drawV(p80d, '#d97706', [4, 3], 'P80 Δ', '#b45309', true);
        }
    };

    _tcHistRiesgoNetoChart = new Chart(canvas, {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                label: 'Frecuencia',
                data: freqs,
                backgroundColor: colors,
                borderColor: colors.map(c => c.replace('0.82', '1')),
                borderWidth: 1,
                borderRadius: 1,
                barPercentage: 1.0,
                categoryPercentage: 1.0
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            layout: { padding: { top: 20, right: 6, bottom: 4, left: 4 } },
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: 'rgba(15,23,42,0.92)',
                    titleFont: { size: 12, weight: '600' },
                    bodyFont: { size: 11 },
                    padding: 10,
                    callbacks: {
                        title: items => {
                            const i = items[0].dataIndex;
                            return 'Rango: ' + fmtRangeTitle(hist[i].desde, hist[i].hasta);
                        },
                        label: item => 'Frecuencia: ' + item.raw.toFixed(2) + '% de iteraciones'
                    }
                }
            },
            scales: {
                x: {
                    title: {
                        display: true,
                        text: 'Δ EAT por riesgos MC (millones USD; conjunto − solo ítems)',
                        color: '#64748b',
                        font: { size: 10, weight: '600' }
                    },
                    ticks: {
                        maxRotation: 0,
                        minRotation: 0,
                        autoSkip: true,
                        maxTicksLimit: 14,
                        font: { size: 9 },
                        color: '#64748b'
                    },
                    grid: { display: false }
                },
                y: {
                    title: {
                        display: true,
                        text: '% iteraciones',
                        color: '#64748b',
                        font: { size: 10, weight: '600' }
                    },
                    beginAtZero: true,
                    suggestedMax: yAxisCap,
                    ticks: {
                        font: { size: 9 },
                        color: '#94a3b8',
                        callback: v => (Number.isInteger(v) ? v : v.toFixed(1)) + '%'
                    },
                    grid: { color: 'rgba(148,163,184,0.18)' }
                }
            },
            animation: { duration: 500, easing: 'easeOutQuart' }
        },
        plugins: [refLinePlugin]
    });
}

// Helpers de construcción HTML para el resumen
function tcResKpi(label, value, icon, bgClass, valClass, sub = '') {
    return `<div class="tc-card !py-3 !px-4 ${bgClass}">
        <div class="flex items-center gap-2 mb-1">
            <span class="material-icons text-base opacity-60">${icon}</span>
            <span class="text-xs text-slate-500 dark:text-slate-400">${label}</span>
        </div>
        <p class="text-base font-bold ${valClass}">${value}</p>
        ${sub ? `<p class="text-xs text-slate-400 mt-0.5">${sub}</p>` : ''}
    </div>`;
}
function tcResRow(label, value, valClass) {
    return `<div class="flex justify-between items-center py-1 border-b border-slate-100 dark:border-slate-800">
        <span class="text-xs text-slate-500">${label}</span>
        <span class="text-xs font-bold ${valClass}">${value}</span>
    </div>`;
}
function tcResMetric(value, label, cls) {
    return `<div class="bg-slate-50 dark:bg-slate-800 rounded p-2">
        <div class="font-bold text-sm ${cls}">${value}</div>
        <div class="text-slate-400" style="font-size:10px">${label}</div>
    </div>`;
}

// ════════════════════════════════════════════════════════════════════════════
// CORRELACIÓN — helpers para la matriz y cálculo
// ════════════════════════════════════════════════════════════════════════════

/** Mantiene simetría de la matriz de correlación al editar una celda. */
function tcCorrSimetria(input) {
    const i = +input.dataset.i, j = +input.dataset.j;
    const table = document.getElementById('corr-matrix-table');
    if (!table) return;
    // Buscar la celda simétrica (j,i)
    const mirror = table.querySelector(`input[data-i="${j}"][data-j="${i}"]`);
    if (mirror) mirror.value = input.value;
    // Clamp a [-1,1]
    let v = parseFloat(input.value);
    if (isNaN(v)) v = 0;
    v = Math.max(-1, Math.min(1, v));
    input.value = v;
    if (mirror) mirror.value = v;
}

/** Cambia la fuente de variables para la correlación (contratos / riesgos / ambos). */
function tcCorrSetSource(src, btn) {
    window._tcCorrVarSource = src;

    // Actualizar estado visual de los botones de fuente
    ['contratos', 'riesgos', 'ambos'].forEach(s => {
        const b = document.getElementById(`tc-corr-src-${s}`);
        if (!b) return;
        if (s === src) {
            b.classList.remove('text-rose-700', 'dark:text-rose-300', 'hover:bg-rose-50', 'dark:hover:bg-rose-900/20');
            b.classList.add('bg-rose-600', 'text-white');
        } else {
            b.classList.remove('bg-rose-600', 'text-white');
            b.classList.add('text-rose-700', 'dark:text-rose-300', 'hover:bg-rose-50', 'dark:hover:bg-rose-900/20');
        }
    });

    // Actualizar badge de info
    const configPanel = document.getElementById('corr-config-panel');
    const nItems = parseInt(configPanel?.dataset.nItems || '0', 10);
    const nRisks = parseInt(configPanel?.dataset.nRisks  || '0', 10);
    const nMap = { contratos: nItems, riesgos: nRisks, ambos: nItems + nRisks };
    const descMap = {
        contratos: `${nItems} ítems de incertidumbre de contratos`,
        riesgos:   `${nRisks} riesgos del proyecto`,
        ambos:     `${nItems} ítems + ${nRisks} riesgos = ${nItems + nRisks} variables`
    };
    const badge = document.getElementById('corr-src-info-badge');
    if (badge) badge.textContent = descMap[src] ?? '';

    // Limpiar resultado anterior al cambiar fuente
    const resultPanel = document.getElementById('corr-result-panel');
    if (resultPanel) { resultPanel.innerHTML = ''; resultPanel.classList.add('hidden'); }

    // Si ya había un preset seleccionado, re-aplicar la etiqueta activa
    if (window._tcCorrPresetVal != null) {
        const lbl = document.getElementById('corr-preset-active-label');
        if (lbl) {
            lbl.textContent = `Usando: ${window._tcCorrPresetLabel ?? `ρ = ${window._tcCorrPresetVal}`}`;
            lbl.classList.remove('hidden');
        }
    }
}

/** Rellena la matriz con un valor uniforme off-diagonal.
 *  @param {number} val  - valor de correlación (0, 0.2, 0.5, 0.8)
 *  @param {HTMLElement} btn - el botón que fue clickeado (para marcar activo)
 */
function tcCorrPreset(val, btn) {
    const table = document.getElementById('corr-matrix-table');
    if (table) {
        table.querySelectorAll('input.corr-cell').forEach(inp => { inp.value = val; });
    }
    window._tcCorrPresetVal = val;

    // Marcar visualmente el botón activo
    if (btn) {
        const container = btn.closest('.flex');
        if (container) {
            container.querySelectorAll('button[data-corr-preset]').forEach(b => {
                b.classList.remove('btn-primary');
                b.classList.add('btn-secondary');
            });
        }
        btn.classList.remove('btn-secondary');
        btn.classList.add('btn-primary');
    }

    // Actualizar etiqueta de preset activo
    const labels = { 0: 'Sin correlación (ρ = 0)', 0.2: 'Baja (ρ = 0.2)', 0.5: 'Media (ρ = 0.5)', 0.8: 'Alta (ρ = 0.8)' };
    window._tcCorrPresetLabel = labels[val] ?? `ρ = ${val}`;
    const lbl = document.getElementById('corr-preset-active-label');
    if (lbl) {
        lbl.textContent = `Usando: ${window._tcCorrPresetLabel}`;
        lbl.classList.remove('hidden');
    }

    // Habilitar el botón Calcular
    const calcBtn = document.getElementById('tc-corr-calc-btn');
    if (calcBtn) {
        calcBtn.disabled = false;
        calcBtn.classList.remove('opacity-50', 'cursor-not-allowed');
    }
    const hint = document.getElementById('tc-corr-hint');
    if (hint) hint.classList.add('hidden');
}

/** Lee la matriz del DOM y ejecuta el análisis con el backend. */
async function tcEjecutarCorrelacion() {
    const configPanel = document.getElementById('corr-config-panel');
    const panel = document.getElementById('corr-result-panel');
    if (!panel || !configPanel) return;

    const proyId  = configPanel.dataset.proy;
    const nItems  = parseInt(configPanel.dataset.nItems || '0', 10);
    const nRisks  = parseInt(configPanel.dataset.nRisks || '0', 10);
    const varSource = window._tcCorrVarSource || 'riesgos';

    // Calcular N según fuente seleccionada
    const n = varSource === 'contratos' ? nItems
            : varSource === 'ambos'     ? nItems + nRisks
            : nRisks;

    if (!proyId || n === 0) return;

    panel.classList.remove('hidden');
    panel.innerHTML = `<div class="flex items-center gap-2 text-xs text-slate-500"><div class="tc-spinner"></div> Calculando simulación con correlación (${varSource})…</div>`;

    // Construir matriz de correlación uniforme N×N con el preset seleccionado
    const pv = window._tcCorrPresetVal ?? 0;
    const matriz = Array.from({length: n}, (_, i) =>
        Array.from({length: n}, (_, j) => (i === j ? 1.0 : pv))
    );

    try {
        const result = await tcPost('Correlacion', { proyectoId: proyId, matriz, varSource, iteraciones: 10000 });
        if (result.error) throw new Error(result.error);
        // Si el backend usó un n distinto al esperado, la matriz fue descartada — avisar
        const nReal = varSource === 'contratos' ? (result.nContratos ?? 0)
                    : varSource === 'ambos'     ? (result.nContratos ?? 0) + (result.nRiesgos ?? 0)
                    : (result.nRiesgos ?? 0);
        if (nReal > 0 && nReal !== n) {
            panel.insertAdjacentHTML('afterbegin',
                `<div class="mb-3 text-xs text-amber-700 dark:text-amber-300 bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-700 rounded-lg px-3 py-2 flex items-start gap-2">
                    <span class="material-icons text-sm flex-shrink-0 mt-0.5">warning</span>
                    <span>El proyecto tiene ahora <strong>${nReal}</strong> variables (se esperaban <strong>${n}</strong>). Recarga el Resumen Global para actualizar y aplicar la correlación correctamente.</span>
                </div>`);
        }

        const U = v => '$' + tcFmt(v ?? 0, 0) + ' USD';
        const dSign = v => (v > 0 ? '+' : '') + tcFmt(v, 1) + '%';
        const deltaColor = v => v > 5 ? 'text-red-600' : v < -5 ? 'text-emerald-600' : 'text-slate-600';

        const totCtx = window._tcLastResumenData?.totales;
        const capexRef = totCtx?.capex ?? 0;
        const eatDetRef = totCtx?.eat ?? 0;
        const pctEatDet = capexRef > 0 && eatDetRef != null ? (eatDetRef / capexRef) * 100 : null;
        const resumenProyMatch = window._tcLastResumenData?.proyecto?.id &&
            String(window._tcLastResumenData.proyecto.id) === String(proyId);
        const budgetCtxOk = !!(totCtx && capexRef > 0 && resumenProyMatch);
        const tcMiniTip = (text, cls = 'gpr-tt-left') =>
            `<span class="gpr-tt ${cls} ml-auto flex-shrink-0"><span class="material-icons gpr-tt-icon" style="font-size:12px;opacity:0.55">help_outline</span><span class="gpr-tt-box">${esc(text)}</span></span>`;

        const impactLevel = result.deltaP90 > 5 ? 'high' : result.deltaP90 < -5 ? 'low' : 'neutral';
        const impactBg    = impactLevel==='high'    ? 'bg-red-50 dark:bg-red-900/20 border-red-200 dark:border-red-800'
                          : impactLevel==='low'     ? 'bg-emerald-50 dark:bg-emerald-900/20 border-emerald-200 dark:border-emerald-800'
                          : 'bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-800';
        const impactTxt   = impactLevel==='high' ? 'text-red-700 dark:text-red-300' : impactLevel==='low' ? 'text-emerald-700 dark:text-emerald-300' : 'text-blue-700 dark:text-blue-300';
        const impactIcon  = impactLevel==='high' ? 'warning' : impactLevel==='low' ? 'trending_down' : 'hub';
        const impactMsg   = impactLevel==='high'
            ? `La correlación aumenta el P90 en <strong>${dSign(result.deltaP90)}</strong> y el P50 en <strong>${dSign(result.deltaP50)}</strong>. Los riesgos tienden a materializarse juntos — la cola adversa es mayor que en el modelo independiente.`
            : impactLevel==='low'
            ? `La correlación negativa reduce el P90 en <strong>${dSign(Math.abs(result.deltaP90))}</strong>. Los riesgos se compensan entre sí, aliviando la cola adversa del EAT.`
            : `Efecto de correlación bajo: Δ P90 = <strong>${dSign(result.deltaP90)}</strong>. El modelo independiente es suficientemente conservador para este proyecto.`;

        const mkDeltaBadge = (delta) => {
            const cls = delta > 2 ? 'text-red-600 bg-red-50 dark:bg-red-900/30 border-red-200' :
                        delta < -2 ? 'text-emerald-600 bg-emerald-50 dark:bg-emerald-900/30 border-emerald-200' :
                        'text-slate-500 bg-slate-50 dark:bg-slate-700 border-slate-200';
            const arrow = delta > 0 ? '↑' : delta < 0 ? '↓' : '→';
            return `<span class="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded border text-xs font-semibold tabular-nums ${cls}">${arrow} ${dSign(delta)}</span>`;
        };

        const presetLabel = window._tcCorrPresetLabel ?? (window._tcCorrPresetVal != null ? `ρ = ${window._tcCorrPresetVal}` : 'personalizada');

        // Análisis textual profundo
        const absD50 = Math.abs(result.deltaP50);
        const absD90 = Math.abs(result.deltaP90);
        const pv = window._tcCorrPresetVal ?? null;
        const corrDir = result.deltaP90 > 0 ? 'positiva' : result.deltaP90 < 0 ? 'negativa' : 'nula';

        // Etiqueta de fuente de variables para el resultado
        const srcLabels = { contratos: 'Contratos', riesgos: 'Riesgos', ambos: 'Ambos (Contratos + Riesgos)' };
        const srcUsed   = result.varSource ?? varSource;
        const srcLabel  = srcLabels[srcUsed] ?? srcUsed;
        const nVarsUsed = srcUsed === 'contratos' ? result.nContratos
                        : srcUsed === 'ambos'     ? (result.nContratos ?? 0) + (result.nRiesgos ?? 0)
                        : result.nRiesgos;

        // Interpretación según nivel de correlación y fuente de variables
        let corrInterpretacion = '';
        const srcCtx = srcUsed === 'contratos' ? 'los ítems de incertidumbre de los contratos'
                     : srcUsed === 'ambos'      ? 'los ítems de contratos y los riesgos del proyecto conjuntamente'
                     : 'los riesgos del proyecto';
        if (pv === null) {
            corrInterpretacion = `Correlación definida mediante matriz personalizada aplicada a ${srcCtx}. Los valores off-diagonal reflejan la hipótesis de dependencia específica entre pares de variables.`;
        } else if (pv === 0) {
            corrInterpretacion = `Con correlación nula (variables independientes), la diversificación es máxima entre ${srcCtx}. Este escenario entrega el EAT más optimista del análisis probabilístico — úselo como línea base.`;
        } else if (pv <= 0.2) {
            corrInterpretacion = `Una correlación baja (ρ ≈ 0.2) aplicada a ${srcCtx} es típica de proyectos con variables de distintas disciplinas sin causa raíz compartida. El efecto sobre la cola P90 es moderado.`;
        } else if (pv <= 0.5) {
            corrInterpretacion = `Una correlación media (ρ ≈ 0.5) aplicada a ${srcCtx} refleja proyectos donde las variables comparten factores comunes: condiciones de mercado, disponibilidad de proveedores o incertidumbre regulatoria. Es el supuesto más conservador-razonable para presupuestación GPR.`;
        } else {
            corrInterpretacion = `Una correlación alta (ρ ≈ 0.8) aplicada a ${srcCtx} es típica de entornos de alto riesgo sistémico: crisis geopolítica, colapso de un proveedor único, cambio regulatorio masivo. Si las variables comparten la misma causa raíz, este escenario es el más realista para la cola adversa.`;
        }

        // Impacto financiero en lenguaje natural
        const eatBase = result.p50; // con correlación
        let impactoFinanciero = '';
        if (absD90 > 10) {
            impactoFinanciero = `El impacto es <strong>significativo</strong>: la correlación ${corrDir} desplaza el P90 en <strong>${dSign(result.deltaP90)}</strong> respecto al modelo independiente. Esto representa una variación material del presupuesto de contingencia.`;
        } else if (absD90 > 3) {
            impactoFinanciero = `El impacto es <strong>moderado</strong>: Δ P90 = <strong>${dSign(result.deltaP90)}</strong>. Considere ajustar la reserva de contingencia si la correlación asumida es conservadora para este proyecto.`;
        } else {
            impactoFinanciero = `El impacto es <strong>bajo</strong>: Δ P90 = <strong>${dSign(result.deltaP90)}</strong>. El modelo de riesgos independientes es suficientemente representativo — la correlación no cambia materialmente el perfil de riesgo.`;
        }

        // Recomendación de acción
        let recomendacion = '';
        if (result.deltaP90 > 8) {
            recomendacion = 'Recomendación: Elevar la reserva de contingencia al nivel P80-P90 con correlación. Revisar si existe una causa raíz común entre los principales riesgos y considerar medidas de mitigación coordinadas.';
        } else if (result.deltaP90 > 3) {
            recomendacion = 'Recomendación: Usar el P80 con correlación como referencia para la contingencia mínima. Documentar la hipótesis de correlación asumida en el informe GPR.';
        } else if (result.deltaP90 < -3) {
            recomendacion = 'Recomendación: La correlación negativa sugiere que algunos riesgos se compensan. Verificar que esta hipótesis sea válida — correlaciones negativas son poco comunes en proyectos CAPEX salvo diseño deliberado de coberturas.';
        } else {
            recomendacion = 'Recomendación: Mantener la contingencia basada en el modelo independiente. El análisis de correlación confirma la robustez del presupuesto GPR actual.';
        }

        // ── Clasificación de impacto y tráfico semafórico ──────────────────
        // absD90usd calculado DESPUÉS de que sinP90 esté definido (ver más abajo en el flujo)
        // Se declara aquí como placeholder; el valor real se asigna tras calcular sinP90
        let absD90usd = 0;
        const impLvl    = absD90 > 5  ? 'alto'
                        : absD90 > 3  ? 'moderado'
                        : absD90 > 1  ? 'bajo-mod'
                        :               'bajo';
        const impEmoji  = absD90 > 5  ? '🔴' : absD90 > 3  ? '🟠' : absD90 > 1 ? '🟡' : '🟢';
        const impLabel  = absD90 > 5  ? 'ALTO' : absD90 > 3 ? 'MODERADO' : absD90 > 1 ? 'BAJO-MODERADO' : 'BAJO';
        const impBorder = absD90 > 5  ? 'border-red-200 dark:border-red-800'
                        : absD90 > 3  ? 'border-orange-200 dark:border-orange-800'
                        : absD90 > 1  ? 'border-amber-200 dark:border-amber-800'
                        :               'border-emerald-200 dark:border-emerald-800';
        const impBgCard = absD90 > 5  ? 'bg-red-50 dark:bg-red-900/20'
                        : absD90 > 3  ? 'bg-orange-50 dark:bg-orange-900/20'
                        : absD90 > 1  ? 'bg-amber-50 dark:bg-amber-900/20'
                        :               'bg-emerald-50 dark:bg-emerald-900/20';
        const impTxtCard= absD90 > 5  ? 'text-red-700 dark:text-red-300'
                        : absD90 > 3  ? 'text-orange-700 dark:text-orange-300'
                        : absD90 > 1  ? 'text-amber-700 dark:text-amber-300'
                        :               'text-emerald-700 dark:text-emerald-300';
        const impBarClr = absD90 > 5  ? 'bg-red-500'
                        : absD90 > 3  ? 'bg-orange-500'
                        : absD90 > 1  ? 'bg-amber-500'
                        :               'bg-emerald-500';

        const UMBRAL_PCT = 5;
        const progW = Math.min(absD90 / UMBRAL_PCT * 100, 100).toFixed(1);
        const pctOfUmbral = (absD90 / UMBRAL_PCT * 100).toFixed(0);

        // ── Etiqueta de correlación ─────────────────────────────────────────
        const corrLabel = pv === null ? 'Matriz personalizada'
                        : pv === 0   ? 'Sin correlación (independiente)'
                        : pv <= 0.2  ? 'Correlación BAJA'
                        : pv <= 0.5  ? 'Correlación MEDIA'
                        :              'Correlación ALTA';

        // ── Delta con signo y color para celdas de la tabla ─────────────────
        const deltaCellCls = d => {
            const a = Math.abs(d);
            if (a < 1)  return 'text-emerald-600 dark:text-emerald-400 font-semibold';
            if (a < 3)  return 'text-amber-600 dark:text-amber-400 font-semibold';
            if (a < 5)  return 'text-orange-600 dark:text-orange-400 font-semibold';
            return 'text-red-600 dark:text-red-400 font-bold';
        };
        const deltaCellBg = d => {
            const a = Math.abs(d);
            if (a < 1)  return 'bg-emerald-50 dark:bg-emerald-900/20';
            if (a < 3)  return 'bg-amber-50 dark:bg-amber-900/20';
            if (a < 5)  return 'bg-orange-50 dark:bg-orange-900/20';
            return 'bg-red-50 dark:bg-red-900/20';
        };
        const arrowIcon = d => d > 0.05 ? '↑' : d < -0.05 ? '↓' : '≈';

        const pctCell = v => {
            if (capexRef <= 0 || v == null) return '<td class="px-3 py-2 text-xs text-right text-slate-400">—</td>';
            return `<td class="px-3 py-2 text-xs text-right tabular-nums text-slate-500 dark:text-slate-400">${tcFmt((v / capexRef) * 100, 1)}%</td>`;
        };

        const mkTableRow = (label, sinVal, conVal, deltaPct) => {
            const dCls = deltaCellCls(deltaPct);
            const dBg  = deltaCellBg(deltaPct);
            const arr  = arrowIcon(deltaPct);
            return `<tr class="border-b border-slate-100 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800/40">
                <td class="px-4 py-2 text-xs font-bold text-slate-600 dark:text-slate-300">${label}</td>
                <td class="px-4 py-2 text-xs text-right tabular-nums text-slate-500 dark:text-slate-400">${sinVal != null ? U(sinVal) : '—'}</td>
                ${pctCell(sinVal)}
                <td class="px-4 py-2 text-xs text-right tabular-nums font-semibold text-slate-700 dark:text-slate-200">${U(conVal)}</td>
                ${pctCell(conVal)}
                <td class="px-4 py-2 text-xs text-right tabular-nums ${dCls} ${dBg} rounded-sm">${sinVal != null ? `${arr} ${dSign(deltaPct)}` : '—'}</td>
            </tr>`;
        };

        // Deltas para todos los percentiles — usar los enviados por el backend si existen,
        // sino los calculamos aquí desde los valores de referencia (refXxx).
        // refXxx son los valores exactos de la simulación independiente; si el backend
        // los envía úsalos directamente; si no (servidor antiguo), calcula via delta.
        const _refFromDelta = (conVal, deltaPct) =>
            (deltaPct != null && deltaPct !== 0) ? conVal / (1 + deltaPct / 100) : conVal;

        const deltaP50pct   = result.deltaP50 ?? 0;
        const deltaP90pct_v = result.deltaP90 ?? 0;
        const deltaP80pct   = result.deltaP80 ?? (result.refP80  > 0 ? (result.p80  - result.refP80)  / result.refP80  * 100 : 0);
        const deltaP10pct   = result.deltaP10 ?? (result.refP10  > 0 ? (result.p10  - result.refP10)  / result.refP10  * 100 : 0);
        const deltaCvar90pct= result.deltaCvar90 ?? (result.refCvar90 > 0 ? (result.cvar90 - result.refCvar90) / result.refCvar90 * 100 : 0);

        // Valores "sin correlación" — preferir refXxx exactos, fallback a derivar del delta
        const sinP10    = result.refP10   ?? _refFromDelta(result.p10,   deltaP10pct);
        const sinP50    = result.refP50   ?? _refFromDelta(result.p50,   deltaP50pct);
        const sinP80    = result.refP80   ?? _refFromDelta(result.p80,   deltaP80pct);
        const sinP90    = result.refP90   ?? _refFromDelta(result.p90,   deltaP90pct_v);
        const sinCvar90 = result.refCvar90 ?? _refFromDelta(result.cvar90, deltaCvar90pct);

        absD90usd = Math.abs(result.p90 - sinP90);

        // ── Recomendación semafórica ────────────────────────────────────────
        const recEmoji = result.deltaP90 > 8  ? '🔴' : result.deltaP90 > 3  ? '🟡' : result.deltaP90 < -3 ? '🟢' : '🟢';
        const recShort = result.deltaP90 > 8
            ? 'Elevar contingencia al nivel P80–P90 con correlación.'
            : result.deltaP90 > 3
            ? 'Usar P80 con correlación como referencia de contingencia mínima.'
            : result.deltaP90 < -3
            ? 'Correlación negativa — verificar validez de la hipótesis.'
            : 'Mantener modelo independiente — el presupuesto GPR es robusto.';

        panel.innerHTML = `
            <div class="border-t border-slate-200 dark:border-slate-700 pt-4 space-y-4">

                <!-- Header -->
                <div class="flex flex-wrap items-center gap-2">
                    <span class="material-icons text-base text-rose-500">hub</span>
                    <h4 class="text-sm font-semibold text-slate-700 dark:text-slate-300">Resultado — Cópula Gaussiana</h4>
                    <span class="text-xs bg-rose-50 dark:bg-rose-900/30 text-rose-700 dark:text-rose-300 border border-rose-200 dark:border-rose-700 px-2 py-0.5 rounded-full font-medium">${srcLabel} · ${nVarsUsed ?? n} vars</span>
                    <span class="text-xs bg-indigo-50 dark:bg-indigo-900/30 text-indigo-600 dark:text-indigo-400 border border-indigo-200 dark:border-indigo-700 px-2 py-0.5 rounded-full font-medium">${presetLabel}</span>
                    <span class="text-xs text-slate-400 ml-auto">${tcFmt(result.nSimulaciones)} iter · Cópula Gaussiana</span>
                </div>

                ${budgetCtxOk ? `
                <div class="rounded-xl border border-teal-200 dark:border-teal-800 bg-teal-50/60 dark:bg-teal-900/15 p-4">
                    <div class="text-xs font-bold text-teal-800 dark:text-teal-200 uppercase tracking-wide mb-2 flex items-center gap-1">
                        <span class="material-icons text-sm">account_balance</span> Puente con el presupuesto (Resumen Global)
                    </div>
                    <p class="text-xs text-teal-900/80 dark:text-teal-100/80 mb-3">Misma fuente que las tarjetas KPI: CAPEX y EAT determinístico se actualizan al cambiar contratos o montos.</p>
                    <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 text-xs">
                        <div class="rounded-lg bg-white/70 dark:bg-slate-900/40 p-2 border border-teal-100 dark:border-teal-900/50">
                            <div class="text-slate-500 dark:text-slate-400 mb-0.5">CAPEX total</div>
                            <div class="font-bold tabular-nums text-slate-800 dark:text-slate-100">${U(capexRef)}</div>
                        </div>
                        <div class="rounded-lg bg-white/70 dark:bg-slate-900/40 p-2 border border-teal-100 dark:border-teal-900/50">
                            <div class="text-slate-500 dark:text-slate-400 mb-0.5">EAT determinístico</div>
                            <div class="font-bold tabular-nums text-slate-800 dark:text-slate-100">${U(eatDetRef)}</div>
                            <div class="text-slate-400">${pctEatDet != null ? tcFmt(pctEatDet, 1) + '% del CAPEX' : ''}</div>
                        </div>
                        <div class="rounded-lg bg-white/70 dark:bg-slate-900/40 p-2 border border-teal-100 dark:border-teal-900/50">
                            <div class="text-slate-500 dark:text-slate-400 mb-0.5">P90 con correlación</div>
                            <div class="font-bold tabular-nums text-rose-700 dark:text-rose-300">${U(result.p90)}</div>
                            <div class="text-slate-400">${tcFmt((result.p90 / capexRef) * 100, 1)}% del CAPEX</div>
                        </div>
                        <div class="rounded-lg bg-white/70 dark:bg-slate-900/40 p-2 border border-teal-100 dark:border-teal-900/50">
                            <div class="text-slate-500 dark:text-slate-400 mb-0.5">Brecha P90 − CAPEX</div>
                            <div class="font-bold tabular-nums ${result.p90 > capexRef ? 'text-red-600 dark:text-red-400' : 'text-emerald-600 dark:text-emerald-400'}">${result.p90 > capexRef ? '+' : ''}$${tcFmt(result.p90 - capexRef, 0)} USD</div>
                            <div class="text-slate-400">Cola adversa vs techo</div>
                        </div>
                    </div>
                </div>` : `
                <div class="rounded-lg border border-amber-200 dark:border-amber-800 bg-amber-50/50 dark:bg-amber-900/15 px-3 py-2 text-xs text-amber-900 dark:text-amber-100 flex items-start gap-2">
                    <span class="material-icons text-sm flex-shrink-0">info</span>
                    <span>${!window._tcLastResumenData?.totales ? 'Ejecuta <strong>Resumen Global</strong> para este proyecto y así mostrar CAPEX, EAT determinístico y brechas vs techo junto a la cópula.'
                        : !resumenProyMatch ? 'El <strong>Resumen Global</strong> en memoria no corresponde a este proyecto. Vuelve a cargar el resumen con el mismo proyecto activo antes de interpretar montos vs CAPEX.'
                        : 'No hay CAPEX válido en el resumen para calcular % sobre presupuesto.'}</span>
                </div>`}

                <!-- Panel 1 + 2 -->
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">

                    <!-- Panel 1: Hipótesis -->
                    <div class="rounded-xl border border-indigo-200 dark:border-indigo-800 bg-indigo-50 dark:bg-indigo-900/20 p-4">
                        <div class="text-xs font-bold text-indigo-600 dark:text-indigo-400 uppercase tracking-wide mb-2 flex items-center gap-1">
                            <span class="material-icons text-sm">hub</span> Hipótesis de Correlación
                        </div>
                        <div class="text-2xl font-bold text-indigo-700 dark:text-indigo-200 tabular-nums mb-0.5">ρ = ${pv ?? '—'}</div>
                        <div class="text-xs font-semibold text-indigo-500 dark:text-indigo-400 mb-3">${corrLabel}</div>
                        <p class="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">${corrInterpretacion}</p>
                    </div>

                    <!-- Panel 2: Impacto en P90 -->
                    <div class="rounded-xl border ${impBorder} ${impBgCard} p-4">
                        <div class="text-xs font-bold ${impTxtCard} uppercase tracking-wide mb-2 flex items-center gap-1">
                            <span class="material-icons text-sm">insights</span> Impacto en el Percentil P90
                        </div>
                        <div class="flex items-baseline gap-2 mb-3">
                            <span class="text-2xl font-bold tabular-nums ${impTxtCard}">${result.deltaP90 >= 0 ? '↑' : '↓'} ${dSign(result.deltaP90)}</span>
                            <span class="text-xs text-slate-500 dark:text-slate-400">= ${U(absD90usd)}</span>
                        </div>
                        <div class="mb-3">
                            <div class="flex justify-between text-xs mb-1">
                                <span class="text-slate-500 dark:text-slate-400">Umbral de relevancia (5%)</span>
                                <span class="font-semibold">${tcFmt(absD90, 1)}% / 5,0%</span>
                            </div>
                            <div class="h-2.5 bg-white/60 dark:bg-black/20 rounded-full overflow-hidden">
                                <div class="h-2.5 rounded-full transition-all duration-500 ${impBarClr}" style="width:${progW}%"></div>
                            </div>
                        </div>
                        <div class="flex items-center gap-2">
                            <span class="text-base leading-none">${impEmoji}</span>
                            <span class="text-xs font-bold ${impTxtCard}">Impacto: ${impLabel}</span>
                            <span class="text-xs text-slate-400 ml-auto">${pctOfUmbral}% del umbral</span>
                        </div>
                    </div>
                </div>

                <!-- Panel 3: Tabla comparativa -->
                <div class="rounded-xl border border-slate-200 dark:border-slate-700 overflow-hidden">
                    <div class="px-4 py-2.5 bg-slate-50 dark:bg-slate-800 border-b border-slate-200 dark:border-slate-700 flex items-center gap-2">
                        <span class="material-icons text-slate-500 text-sm">compare_arrows</span>
                        <span class="text-xs font-semibold text-slate-600 dark:text-slate-300">Comparativa — Sin Correlación vs Con Correlación ${presetLabel}</span>
                    </div>
                    <div class="overflow-x-auto">
                    <table class="w-full text-left">
                        <thead>
                            <tr class="text-xs text-slate-400 uppercase tracking-wide border-b border-slate-200 dark:border-slate-700 bg-slate-50/50 dark:bg-slate-800/50">
                                <th class="px-4 py-2">Percentil</th>
                                <th class="px-4 py-2 text-right">Sin Correlación</th>
                                <th class="px-3 py-2 text-right" title="Percentil sin correlación ÷ CAPEX del Resumen Global">% CAPEX</th>
                                <th class="px-4 py-2 text-right">Con Correlación ${presetLabel}</th>
                                <th class="px-3 py-2 text-right" title="Percentil con correlación ÷ CAPEX del Resumen Global">% CAPEX</th>
                                <th class="px-4 py-2 text-right">Δ %</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${mkTableRow('P10',    sinP10,   result.p10,   deltaP10pct)}
                            ${mkTableRow('P50',    sinP50,   result.p50,   deltaP50pct)}
                            ${mkTableRow('P80',    sinP80,   result.p80,   deltaP80pct)}
                            ${mkTableRow('P90',    sinP90,   result.p90,   deltaP90pct_v)}
                            ${mkTableRow('CVaR90', sinCvar90,result.cvar90,deltaCvar90pct)}
                        </tbody>
                    </table>
                    </div>
                    <div class="px-4 py-2 bg-slate-50 dark:bg-slate-800/50 border-t border-slate-100 dark:border-slate-700 flex flex-wrap gap-3 text-xs text-slate-400">
                        <span><span class="inline-block w-3 h-2.5 rounded-sm bg-emerald-500 opacity-70 mr-1 align-middle"></span>&lt;1% bajo</span>
                        <span><span class="inline-block w-3 h-2.5 rounded-sm bg-amber-500 opacity-70 mr-1 align-middle"></span>1–3% moderado</span>
                        <span><span class="inline-block w-3 h-2.5 rounded-sm bg-orange-500 opacity-70 mr-1 align-middle"></span>3–5% notable</span>
                        <span><span class="inline-block w-3 h-2.5 rounded-sm bg-red-500 opacity-70 mr-1 align-middle"></span>&gt;5% alto</span>
                        ${capexRef > 0 ? `<span class="text-slate-500 dark:text-slate-400 ml-auto flex items-center gap-1">${tcMiniTip('% CAPEX = percentil simulado ÷ CAPEX total del Resumen Global. Valores mayores que 100% indican sobrecosto respecto al techo en ese cuantil.')}</span>` : ''}
                    </div>
                </div>

                <!-- Panel 4: Recomendación -->
                <div class="rounded-xl border ${impBorder} ${impBgCard} p-4">
                    <div class="flex items-center gap-2 mb-2">
                        <span class="text-lg leading-none">${recEmoji}</span>
                        <span class="text-xs font-bold ${impTxtCard} uppercase tracking-wide">Recomendación</span>
                    </div>
                    <p class="text-xs text-slate-700 dark:text-slate-200 leading-relaxed">${recomendacion}</p>
                </div>

                <!-- Línea de resumen -->
                <div class="rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-700 px-4 py-3 text-xs space-y-1.5">
                    <div class="flex items-start gap-2">
                        <span class="text-slate-400 flex-shrink-0 w-28">Hipótesis:</span>
                        <span class="font-semibold text-slate-700 dark:text-slate-200">${presetLabel} — ${corrLabel}</span>
                    </div>
                    <div class="flex items-start gap-2">
                        <span class="text-slate-400 flex-shrink-0 w-28">Impacto:</span>
                        <span>${impEmoji} <span class="font-semibold ${impTxtCard}">${impLabel}</span> — ${result.deltaP90 >= 0 ? '↑' : '↓'} ${dSign(result.deltaP90)} = ${U(absD90usd)} <span class="text-slate-400">(${pctOfUmbral}% del umbral de relevancia)</span></span>
                    </div>
                    <div class="flex items-start gap-2">
                        <span class="text-slate-400 flex-shrink-0 w-28">Recomendación:</span>
                        <span class="text-slate-600 dark:text-slate-300">${recEmoji} ${recShort}</span>
                    </div>
                </div>
            </div>`;

        // ── Inyectar resumen de correlación en la sección EAT Probabilístico ──
        const banner = document.getElementById('eat-corr-banner');
        if (banner) {
            banner.classList.remove('hidden');
            const bannerKpiTip = text =>
                `<span class="gpr-tt gpr-tt-left inline-flex align-middle flex-shrink-0"><span class="material-icons gpr-tt-icon" style="font-size:11px;opacity:0.55">help_outline</span><span class="gpr-tt-box">${esc(text)}</span></span>`;
            const d50Cls  = result.deltaP50 > 3 ? 'text-red-600 font-bold' : result.deltaP50 < -3 ? 'text-emerald-600 font-bold' : 'text-slate-500';
            const d90Cls  = result.deltaP90 > 3 ? 'text-red-600 font-bold' : result.deltaP90 < -3 ? 'text-emerald-600 font-bold' : 'text-slate-500';
            const alertBg = result.deltaP90 > 5 ? 'bg-red-50 dark:bg-red-900/20 border-red-200' :
                            result.deltaP90 < -5 ? 'bg-emerald-50 dark:bg-emerald-900/20 border-emerald-200' :
                            'bg-blue-50 dark:bg-blue-900/20 border-blue-200';
            const alertTxt= result.deltaP90 > 5 ? 'text-red-700' : result.deltaP90 < -5 ? 'text-emerald-700' : 'text-blue-700';
            const alertIcon= result.deltaP90 > 5 ? 'warning' : result.deltaP90 < -5 ? 'trending_down' : 'hub';
            const alertMsg = result.deltaP90 > 5
                ? `La correlación entre riesgos aumenta el P90 en <strong>${dSign(result.deltaP90)}</strong> y el P50 en <strong>${dSign(result.deltaP50)}</strong>. Los riesgos tienden a materializarse juntos — la cola adversa es mayor que en el modelo independiente.`
                : result.deltaP90 < -5
                ? `La correlación negativa reduce el P90 en <strong>${dSign(Math.abs(result.deltaP90))}</strong> — los riesgos se compensan entre sí, aliviando la cola adversa.`
                : `El efecto de la correlación es bajo: Δ P90 = <strong>${dSign(result.deltaP90)}</strong>. El modelo independiente es suficientemente conservador.`;
            banner.innerHTML = `
                <div class="flex items-center gap-2 mb-2">
                    <span class="material-icons text-sm text-rose-500">hub</span>
                    <span class="text-xs font-semibold text-slate-600 dark:text-slate-300">Impacto de Correlación (Cópula Gaussiana)</span>
                </div>
                <div class="grid grid-cols-2 sm:grid-cols-4 gap-2 mb-2">
                    <div class="bg-slate-50 dark:bg-slate-800 rounded p-2 text-center">
                        <div class="text-xs text-slate-400 mb-0.5 flex items-center justify-center gap-0.5 flex-wrap">
                            <span>P50 con corr.</span>
                            ${bannerKpiTip('Mediana del EAT total simulado con la correlación aplicada (comprometido + certeza + ítems inciertos + riesgos MC). El porcentaje debajo es el cambio vs. el mismo percentil en el modelo independiente (sin ρ entre las variables elegidas en la cópula). Compare con el CAPEX en el Resumen Global.')}
                        </div>
                        <div class="text-sm font-bold text-emerald-600">${U(result.p50)}</div>
                        <div class="text-xs ${d50Cls}">${dSign(result.deltaP50)}</div>
                    </div>
                    <div class="bg-slate-50 dark:bg-slate-800 rounded p-2 text-center">
                        <div class="text-xs text-slate-400 mb-0.5 flex items-center justify-center gap-0.5 flex-wrap">
                            <span>P80 con corr.</span>
                            ${bannerKpiTip('El 80% de las simulaciones quedan por debajo de este EAT. Referencia conservadora frecuente para contingencia. Con correlación positiva entre variables suele ser mayor que en el modelo independiente.')}
                        </div>
                        <div class="text-sm font-bold text-amber-600">${U(result.p80)}</div>
                    </div>
                    <div class="bg-slate-50 dark:bg-slate-800 rounded p-2 text-center">
                        <div class="text-xs text-slate-400 mb-0.5 flex items-center justify-center gap-0.5 flex-wrap">
                            <span>P90 con corr.</span>
                            ${bannerKpiTip('Solo el 10% de los escenarios simulados superan este EAT (cola adversa). Muy sensible a dependencia entre riesgos e ítems: la correlación suele engrosar esta cola. El % debajo refleja el efecto vs. simulación independiente.')}
                        </div>
                        <div class="text-sm font-bold text-red-600">${U(result.p90)}</div>
                        <div class="text-xs ${d90Cls}">${dSign(result.deltaP90)}</div>
                    </div>
                    <div class="bg-slate-50 dark:bg-slate-800 rounded p-2 text-center">
                        <div class="text-xs text-slate-400 mb-0.5 flex items-center justify-center gap-0.5 flex-wrap">
                            <span>CVaR90 con corr.</span>
                            ${bannerKpiTip('Conditional Value at Risk al 90%: promedio del peor 10% de simulaciones (Expected Shortfall). Más conservador que el P90 cuando la cola es gruesa. Incluye el mismo supuesto de correlación que el resto de la cópula.')}
                        </div>
                        <div class="text-sm font-bold text-rose-700">${U(result.cvar90)}</div>
                    </div>
                </div>
                <div class="p-2 rounded border text-xs ${alertBg} ${alertTxt} flex items-start gap-1.5">
                    <span class="material-icons text-sm flex-shrink-0 mt-0.5">${alertIcon}</span>
                    <span>${alertMsg}</span>
                </div>`;
        }
    } catch(e) {
        panel.innerHTML = `<div class="text-xs text-red-500 mt-2">Error: ${esc(e.message)}</div>`;
    }
}

// ════════════════════════════════════════════════════════════════════════════
// MIROFISH — Predicción IA de Enjambre
// ════════════════════════════════════════════════════════════════════════════

var _mfPollTimer   = null;
var _tcMfKpis      = null; // KPIs de contexto para mostrar en el resultado MiroFish

/**
 * Inyecta la sección "Predicción IA de Enjambre" en el panel de riesgos TC.
 * Se llama tras tcCargarRevision() cuando hay riesgos cargados.
 */
function tcMiroFishInjectSection() {
    if (document.getElementById('tc-mf-section')) return;

    // Encontrar punto de anclaje: después del panel de riesgos activo
    var anchor = document.getElementById('tc-riesgos-panel');
    if (!anchor) {
        anchor = document.getElementById('risks-form');
    }
    if (!anchor) return;

    var html = `
<div id="tc-mf-section" style="margin-top:20px;border:1px solid #c4b5fd;border-radius:12px;overflow:hidden;font-family:'Segoe UI',sans-serif">
  <!-- Cabecera -->
  <div style="background:linear-gradient(135deg,#7c3aed,#5b21b6);padding:14px 20px;display:flex;align-items:center;gap:12px">
    <span class="material-icons" style="color:#fff;font-size:22px">hub</span>
    <div style="flex:1">
      <div style="color:#fff;font-size:14px;font-weight:700">Predicción IA de Enjambre</div>
      <div style="color:#c4b5fd;font-size:11px">MiroFish — Motor multiagente de simulación de escenarios</div>
    </div>
    <span id="tc-mf-status-dot" style="display:inline-flex;align-items:center;gap:5px;font-size:10px;color:#a78bfa;background:rgba(255,255,255,0.1);padding:3px 10px;border-radius:99px">
      <span id="tc-mf-dot-circle" style="width:7px;height:7px;border-radius:50%;background:#a78bfa"></span>
      <span id="tc-mf-dot-label">Verificando...</span>
    </span>
  </div>

  <!-- Formulario de inicio -->
  <div id="tc-mf-form" style="padding:16px 20px;background:#faf5ff">
    <div style="display:flex;align-items:flex-start;gap:10px;margin-bottom:12px">
      <span class="material-icons" style="color:#7c3aed;font-size:18px;margin-top:2px">info</span>
      <p style="font-size:11px;color:#6d28d9;line-height:1.5">
        MiroFish crea miles de agentes con personalidad independiente que simulan trayectorias futuras del proyecto
        usando el contexto del mundo real como entrada. El análisis tarda <strong>5-15 minutos</strong> y complementa
        (no reemplaza) el análisis Monte Carlo existente.
      </p>
    </div>
    <div style="margin-bottom:12px">
      <label style="font-size:11px;font-weight:600;color:#374151;display:block;margin-bottom:5px">
        Contexto del mundo real <span style="color:#9ca3af;font-weight:400">(opcional pero recomendado)</span>
      </label>
      <textarea id="tc-mf-contexto" rows="4" placeholder="Ejemplo: El proyecto enfrenta retrasos por huelga portuaria en el norte. El precio del cobre ha aumentado 12% en las últimas semanas. El equipo de ingeniería reporta avance menor al 60% en la etapa de diseño..."
        style="width:100%;padding:10px;border:1px solid #d8b4fe;border-radius:8px;font-size:11px;font-family:inherit;resize:vertical;background:#fff;color:#1f2937;outline:none"></textarea>
    </div>
    <div style="display:flex;gap:10px;align-items:center">
      <button id="tc-mf-btn-iniciar" onclick="tcMiroFishIniciar()"
        style="display:inline-flex;align-items:center;gap:7px;padding:9px 20px;border-radius:8px;font-size:13px;font-weight:600;cursor:pointer;border:none;background:#7c3aed;color:#fff">
        <span class="material-icons" style="font-size:16px">rocket_launch</span>
        Analizar con IA de Enjambre
      </button>
      <span id="tc-mf-aviso-nodisp" style="display:none;font-size:11px;color:#b91c1c;background:#fef2f2;padding:6px 12px;border-radius:6px;border:1px solid #fecaca">
        ⚠ Servidor MiroFish no disponible. Asegúrate de que el servidor Python esté corriendo en localhost:5001.
      </span>
    </div>
  </div>

  <!-- Progreso -->
  <div id="tc-mf-progress" style="display:none;padding:16px 20px;background:#f5f3ff;border-top:1px solid #e9d5ff">
    <div style="display:flex;align-items:center;gap:12px;margin-bottom:10px">
      <div id="tc-mf-spinner" style="width:20px;height:20px;border:3px solid #e9d5ff;border-top-color:#7c3aed;border-radius:50%;animation:tcMfSpin 0.8s linear infinite;flex-shrink:0"></div>
      <div style="flex:1">
        <div id="tc-mf-step-label" style="font-size:12px;font-weight:600;color:#5b21b6">Iniciando análisis...</div>
        <div style="height:6px;background:#e9d5ff;border-radius:99px;margin-top:5px;overflow:hidden">
          <div id="tc-mf-bar" style="height:100%;background:linear-gradient(90deg,#7c3aed,#a855f7);border-radius:99px;transition:width 0.8s ease;width:0%"></div>
        </div>
      </div>
      <span id="tc-mf-pct" style="font-size:11px;color:#7c3aed;font-weight:700;width:35px;text-align:right">0%</span>
    </div>
    <div id="tc-mf-steps-list" style="display:grid;grid-template-columns:repeat(6,1fr);gap:4px">
      ${[
        'Semilla','Grafo','Esperar grafo','Agentes','Simular','Reporte'
      ].map((s,i) => `<div class="tc-mf-step" data-step="${i+1}" style="font-size:9px;text-align:center;padding:4px 2px;border-radius:4px;color:#9ca3af;background:#f3f4f6">${s}</div>`).join('')}
    </div>
    <p style="font-size:10px;color:#9ca3af;margin-top:8px;text-align:center">
      El análisis usa LLM + OASIS. Puedes continuar trabajando mientras esperas.
    </p>
  </div>

  <!-- Resultados -->
  <div id="tc-mf-results" style="display:none;border-top:1px solid #e9d5ff"></div>
</div>

<style>
@keyframes tcMfSpin { to { transform: rotate(360deg); } }
</style>`;

    anchor.insertAdjacentHTML('afterend', html);
    tcMiroFishPing();
}

// Verifica disponibilidad del servidor
async function tcMiroFishPing() {
    try {
        const data = await tcGet('MiroFishPing');
        const dot   = document.getElementById('tc-mf-dot-circle');
        const label = document.getElementById('tc-mf-dot-label');
        const aviso = document.getElementById('tc-mf-aviso-nodisp');
        const btn   = document.getElementById('tc-mf-btn-iniciar');
        if (data?.disponible) {
            if (dot)   { dot.style.background = '#22c55e'; }
            if (label) label.textContent = 'Servidor disponible';
            if (aviso) aviso.style.display = 'none';
            if (btn)   btn.disabled = false;
        } else {
            if (dot)   { dot.style.background = '#ef4444'; }
            if (label) label.textContent = 'No disponible';
            if (aviso) aviso.style.display = '';
            if (btn)   btn.disabled = true;
        }
    } catch {
        const label = document.getElementById('tc-mf-dot-label');
        if (label) label.textContent = 'Sin conexión';
    }
}

// Inicia el análisis MiroFish
async function tcMiroFishIniciar() {
    if (!TC.proyectoId) { alert('Selecciona un proyecto primero.'); return; }
    if (!TC.riesgos?.length) { alert('No hay riesgos cargados en esta revisión.'); return; }

    // Recopilar datos del proyecto y riesgos
    const ctx = document.getElementById('tc-mf-contexto')?.value || '';
    const proy = TC.proyecto || {};

    // Recopilar totales del resumen si está disponible.
    // Se priorizan los valores del EAT Combinado (ítems + riesgos) para que coincidan
    // con los percentiles que muestra Resumen Global, evitando inconsistencias de P50/P80.
    let eatDet = 0, pctIncert = 0, eatP50 = null, eatP80 = null, eatP90 = null, spread = null;
    try {
        const resData = await tcGet('ResumenGlobalJson', { proyectoId: TC.proyectoId });
        const T  = resData?.totales || {};
        const ec = resData?.eatCombinado;
        eatDet    = T.eat || 0;
        pctIncert = T.incertidumbre && T.eat ? T.incertidumbre / T.eat * 100 : 0;
        // Preferir EAT Combinado (incluye riesgos) para concordar con Resumen Global
        eatP50 = ec?.eatP50 ?? T.eatP50 ?? null;
        eatP80 = ec?.eatP80 ?? T.eatP80 ?? null;
        eatP90 = ec?.eatP90 ?? T.eatP90 ?? null;
        spread = ec ? (ec.eatP90 - ec.eatP10) : (T.eatP90 != null && T.eatP10 != null ? T.eatP90 - T.eatP10 : null);
        // Guardar desglose amenazas/oportunidades para mostrar en el resultado
        _tcMfKpis = ec ? {
            amenP50:  ec.contingenciaAmenazasP50  ?? 0,
            amenP80:  ec.contingenciaAmenazasP80  ?? 0,
            amenP90:  ec.contingenciaAmenazasP90  ?? 0,
            amenMedia:ec.contingenciaAmenazasMedia ?? 0,
            opoP50:   Math.abs(ec.impactoOportunidadesP50 ?? 0),
            opoP80:   Math.abs(ec.impactoOportunidadesP80 ?? 0),
            netoP50:  (ec.contingenciaAmenazasP50 ?? 0) - Math.abs(ec.impactoOportunidadesP50 ?? 0),
            netoP80:  (ec.contingenciaAmenazasP80 ?? 0) - Math.abs(ec.impactoOportunidadesP80 ?? 0),
            eatCombP50: ec.eatP50 ?? null,
            eatCombP80: ec.eatP80 ?? null,
            eatCombP90: ec.eatP90 ?? null,
            nAmenazas:   resData.riesgosDisplay?.nAmenazas   ?? 0,
            nOportunidades: resData.riesgosDisplay?.nOportunidades ?? 0,
        } : null;
        // Refrescar tarjetas en pestaña Riesgos si hay datos MC disponibles
        if (_tcMfKpis) tcRenderRiesgosKpiCards();
    } catch {}

    const payload = {
        proyectoId:       TC.proyectoId,
        proyectoNombre:   proy.nombre || '',
        proyectoCodigo:   proy.codigo || '',
        organizacion:     proy.organizacion || '',
        eatDeterministico: eatDet,
        pctIncertidumbre:  pctIncert,
        eatP50:            eatP50,
        eatP80:            eatP80,
        eatP90:            eatP90,
        spreadP10P90:      spread,
        contextoUsuario:   ctx,
        riesgos: TC.riesgos.map(r => ({
            codigo:        r.codigoRiesgo || r.codigo || '',
            descripcion:   r.titulo || r.descripcion || '',
            probabilidad:  r.probabilidad || 50,
            min:           r.impactoMinUsd || r.minImpact || 0,
            moda:          r.impactoProableUsd || r.moda || 0,
            max:           r.impactoMaxUsd || r.maxImpact || 0,
            valorEsperado: (r.probabilidad || 50) / 100 * (r.impactoProableUsd || r.moda || 0),
            esAmenaza:     !r.esOportunidad,
            nivelLabel:    r.nivelLabel || '',
            nivelInt:      r.nivel || 1,
            planRespuesta: r.notasCambio || r.planRespuesta || ''
        }))
    };

    // Limpiar resultado anterior
    document.getElementById('tc-mf-results').style.display = 'none';

    // Mostrar progreso
    document.getElementById('tc-mf-form').style.display = 'none';
    const progEl = document.getElementById('tc-mf-progress');
    if (progEl) progEl.style.display = '';
    document.getElementById('tc-mf-results').style.display = 'none';

    try {
        const resp = await tcPost('MiroFishIniciar', payload);
        if (resp?.jobId) {
            tcMiroFishPollStatus(resp.jobId);
        } else {
            tcMiroFishMostrarError('No se pudo iniciar el análisis.');
        }
    } catch(e) {
        tcMiroFishMostrarError('Error al iniciar: ' + e.message);
    }
}

// Polling de estado del job
function tcMiroFishPollStatus(jobId) {
    if (_mfPollTimer) clearInterval(_mfPollTimer);
    _mfPollTimer = setInterval(async function() {
        try {
            const s = await tcGet('MiroFishStatus', { jobId });
            tcMiroFishActualizarProgreso(s);
            if (s.status === 'completado' || s.status === 'error' || s.status === 'timeout') {
                clearInterval(_mfPollTimer);
                _mfPollTimer = null;
                if (s.status === 'completado' && s.result) {
                    tcMiroFishMostrarResultado(s.result, s.completedAt);
                } else {
                    tcMiroFishMostrarError(s.error || 'El análisis terminó con error.');
                }
            }
        } catch(e) { console.error('MiroFish poll error:', e); }
    }, 5000);
}

// Actualiza indicadores de progreso
function tcMiroFishActualizarProgreso(s) {
    const label = document.getElementById('tc-mf-step-label');
    const bar   = document.getElementById('tc-mf-bar');
    const pct   = document.getElementById('tc-mf-pct');
    if (label) label.textContent = s.stepLabel || '...';
    if (bar)   bar.style.width   = (s.progress || 0) + '%';
    if (pct)   pct.textContent   = Math.round(s.progress || 0) + '%';
    // Colorear pasos completados
    document.querySelectorAll('.tc-mf-step').forEach(el => {
        const n = parseInt(el.dataset.step);
        if (n <= (s.stepNum || 0)) {
            el.style.background = '#ede9fe';
            el.style.color = '#6d28d9';
            el.style.fontWeight = '600';
        }
    });
}

function tcMiroFishMostrarError(msg) {
    document.getElementById('tc-mf-progress').style.display = 'none';
    document.getElementById('tc-mf-form').style.display = '';
    document.getElementById('tc-mf-btn-iniciar').textContent = 'Reintentar análisis';
    const res = document.getElementById('tc-mf-results');
    res.style.display = '';
    res.innerHTML = `<div style="padding:16px 20px;background:#fef2f2;color:#b91c1c;font-size:12px;display:flex;gap:10px;align-items:flex-start">
        <span class="material-icons" style="font-size:18px">error</span>
        <div><strong>Error en el análisis MiroFish</strong><br>${esc(msg)}</div>
    </div>`;
}

// Renderiza los resultados del análisis
function tcMiroFishMostrarResultado(result, completedAt) {
    document.getElementById('tc-mf-progress').style.display = 'none';
    document.getElementById('tc-mf-form').style.display = '';
    const btnIniciar = document.getElementById('tc-mf-btn-iniciar');
    if (btnIniciar) {
        btnIniciar.innerHTML = '<span class="material-icons" style="font-size:16px">refresh</span> Nuevo análisis';
    }

    const res = document.getElementById('tc-mf-results');
    res.style.display = '';

    const riesgos = result.riesgosAjustados || [];
    const divergentes = riesgos.filter(r => r.esDivergente);

    const escPill = (label, color, bg) =>
        `<span style="font-size:9px;font-weight:700;color:${color};background:${bg};padding:2px 8px;border-radius:99px">${label}</span>`;

    const riesgosHtml = riesgos.map(r => {
        const hasProbAdj = r.probAjustada != null;
        const vari = hasProbAdj ? r.variacion : 0;
        const variColor = vari > 10 ? '#b91c1c' : vari < -10 ? '#15803d' : '#475569';
        const variSign  = vari >= 0 ? '+' : '';
        return `<tr style="${r.esDivergente ? 'background:#fff7ed' : ''}">
            <td style="padding:5px 8px;font-size:10px;font-family:monospace;font-weight:600">${esc(r.codigo)}</td>
            <td style="padding:5px 8px;font-size:10px;max-width:200px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${esc(r.descripcion)}</td>
            <td style="padding:5px 8px;font-size:10px;text-align:center;color:#475569">${r.probOriginal.toFixed(0)}%</td>
            <td style="padding:5px 8px;font-size:10px;text-align:center;font-weight:600;color:${hasProbAdj?variColor:'#94a3b8'}">
                ${hasProbAdj ? r.probAjustada.toFixed(0)+'%' : '—'}
            </td>
            <td style="padding:5px 8px;font-size:10px;text-align:center;color:${variColor};font-weight:600">
                ${hasProbAdj ? variSign+vari.toFixed(0)+'pp' : '—'}
                ${r.esDivergente ? ' ⚠' : ''}
            </td>
            <td style="padding:5px 8px;font-size:9px;color:#6b7280;max-width:250px">${esc(r.narrativaIA || '—')}</td>
        </tr>`;
    }).join('');

    res.innerHTML = `
    <div style="padding:16px 20px;background:#fff">
      <!-- Cabecera resultado -->
      <div style="display:flex;align-items:center;gap:10px;margin-bottom:14px">
        <span class="material-icons" style="color:#7c3aed;font-size:22px">psychology</span>
        <div>
          <div style="font-size:13px;font-weight:700;color:#1f2937">Resultados del Análisis de Enjambre</div>
          <div style="font-size:10px;color:#9ca3af">Generado ${completedAt || ''} · ${riesgos.length} riesgo(s) analizados</div>
        </div>
        ${result.hayDivergencia ? `<span style="margin-left:auto;font-size:10px;font-weight:700;color:#d97706;background:#fef3c7;padding:4px 10px;border-radius:6px;border:1px solid #fde68a">
          ⚠ ${divergentes.length} riesgo(s) con divergencia significativa vs. Monte Carlo
        </span>` : `<span style="margin-left:auto;font-size:10px;color:#15803d;background:#f0fdf4;padding:4px 10px;border-radius:6px;border:1px solid #bbf7d0">
          ✓ Análisis consistente con Monte Carlo
        </span>`}
      </div>

      <!-- Panel de reconciliación con Resumen Global -->
      ${_tcMfKpis ? (() => {
        const K  = _tcMfKpis;
        const U  = v => '$' + tcFmt(v ?? 0, 0);
        const neto = K.netoP50;
        const netoColor = neto >= 0 ? '#dc2626' : '#059669';
        return `
      <div style="margin-bottom:14px;border:1px solid #e0e7ff;border-radius:10px;overflow:hidden;font-size:11px">
        <div style="background:#eef2ff;padding:8px 14px;display:flex;align-items:center;gap:8px;border-bottom:1px solid #e0e7ff">
          <span class="material-icons" style="font-size:15px;color:#6366f1">link</span>
          <span style="font-weight:700;color:#4338ca;font-size:11px">Reconciliación con Resumen Global — origen de los percentiles</span>
          <span style="margin-left:auto;font-size:9px;color:#818cf8">${K.nAmenazas} amenaza${K.nAmenazas!==1?'s':''} · ${K.nOportunidades} oportunidad${K.nOportunidades!==1?'es':''}</span>
        </div>
        <div style="padding:10px 14px;background:#fafafa">
          <table style="width:100%;border-collapse:collapse;font-size:10px">
            <thead>
              <tr style="border-bottom:1px solid #e5e7eb">
                <th style="padding:4px 8px;text-align:left;color:#6b7280;font-weight:600">Componente</th>
                <th style="padding:4px 8px;text-align:right;color:#6b7280;font-weight:600">P50</th>
                <th style="padding:4px 8px;text-align:right;color:#6b7280;font-weight:600">P80</th>
                <th style="padding:4px 8px;text-align:left;color:#6b7280;font-weight:600;padding-left:16px">Referencia en Resumen Global</th>
              </tr>
            </thead>
            <tbody>
              <tr style="border-bottom:1px solid #f3f4f6">
                <td style="padding:5px 8px;font-weight:600;color:#dc2626">⚠ Amenazas</td>
                <td style="padding:5px 8px;text-align:right;font-weight:700;color:#dc2626;font-family:monospace">${U(K.amenP50)}</td>
                <td style="padding:5px 8px;text-align:right;color:#b91c1c;font-family:monospace">${U(K.amenP80)}</td>
                <td style="padding:5px 8px;padding-left:16px;color:#6b7280;font-size:9px">"Amenazas — Impacto Acumulado P50/P80" en Resumen Global</td>
              </tr>
              <tr style="border-bottom:1px solid #f3f4f6">
                <td style="padding:5px 8px;font-weight:600;color:#059669">↑ Oportunidades (ahorro)</td>
                <td style="padding:5px 8px;text-align:right;font-weight:700;color:#059669;font-family:monospace">−${U(K.opoP50)}</td>
                <td style="padding:5px 8px;text-align:right;color:#047857;font-family:monospace">−${U(K.opoP80)}</td>
                <td style="padding:5px 8px;padding-left:16px;color:#6b7280;font-size:9px">"Oportunidades — Impacto Acumulado P50/P80" en Resumen Global</td>
              </tr>
              <tr style="background:#f0f9ff;font-weight:700;border-top:2px solid #bae6fd">
                <td style="padding:5px 8px;color:#0369a1">= Impacto Neto de Riesgos</td>
                <td style="padding:5px 8px;text-align:right;color:${netoColor};font-family:monospace">${neto>=0?'+':''}${U(neto)}</td>
                <td style="padding:5px 8px;text-align:right;color:${K.netoP80>=0?'#dc2626':'#059669'};font-family:monospace">${K.netoP80>=0?'+':''}${U(K.netoP80)}</td>
                <td style="padding:5px 8px;padding-left:16px;font-size:9px;color:#0284c7">
                  ← Este es el "BASE P50" que utiliza MiroFish = Amenazas P50 − Oportunidades P50
                </td>
              </tr>
              ${K.eatCombP50 != null ? `
              <tr style="border-top:1px dashed #e5e7eb">
                <td style="padding:5px 8px;color:#475569;font-weight:600">EAT Total Combinado</td>
                <td style="padding:5px 8px;text-align:right;font-weight:700;color:#475569;font-family:monospace">${U(K.eatCombP50)}</td>
                <td style="padding:5px 8px;text-align:right;color:#475569;font-family:monospace">${U(K.eatCombP80)}</td>
                <td style="padding:5px 8px;padding-left:16px;font-size:9px;color:#6b7280">Costos base + incertidumbre + riesgos — ver sección "EAT Probabilístico" en Resumen Global</td>
              </tr>` : ''}
            </tbody>
          </table>
          <p style="margin-top:6px;font-size:9px;color:#94a3b8;line-height:1.4">
            Los percentiles provienen de simulación Bernoulli × Triangular en Resumen Global.
            P50=0 en amenazas indica que la probabilidad individual de cada amenaza es &lt;50%.
            El EAT Total Combinado es el número definitivo para decisiones presupuestarias.
          </p>
        </div>
      </div>`;
      })() : ''}

      <!-- Escenarios -->
      <div style="display:grid;grid-template-columns:1fr 1fr 1fr;gap:12px;margin-bottom:16px">
        <div style="border:1px solid #bbf7d0;border-radius:8px;padding:12px;background:#f0fdf4">
          <div style="display:flex;align-items:center;gap:6px;margin-bottom:6px">
            ${escPill('Optimista','#15803d','#dcfce7')}
          </div>
          <p style="font-size:10px;color:#374151;line-height:1.5">${esc(result.escenarioOptimista || 'No disponible')}</p>
        </div>
        <div style="border:1px solid #bfdbfe;border-radius:8px;padding:12px;background:#eff6ff">
          <div style="display:flex;align-items:center;gap:6px;margin-bottom:6px">
            ${escPill('Base','#1d4ed8','#dbeafe')}
          </div>
          <p style="font-size:10px;color:#374151;line-height:1.5">${esc(result.escenarioBase || 'No disponible')}</p>
        </div>
        <div style="border:1px solid #fde68a;border-radius:8px;padding:12px;background:#fffbeb">
          <div style="display:flex;align-items:center;gap:6px;margin-bottom:6px">
            ${escPill('Pesimista','#92400e','#fef3c7')}
          </div>
          <p style="font-size:10px;color:#374151;line-height:1.5">${esc(result.escenarioPesimista || 'No disponible')}</p>
        </div>
      </div>

      <!-- Tabla de ajuste de probabilidades -->
      ${riesgos.length ? `
      <div style="margin-bottom:14px">
        <div style="font-size:11px;font-weight:600;color:#374151;margin-bottom:6px">Probabilidades Ajustadas por IA</div>
        <div style="overflow-x:auto">
          <table style="width:100%;border-collapse:collapse;font-size:10px">
            <thead>
              <tr style="border-bottom:2px solid #e5e7eb">
                <th style="padding:5px 8px;text-align:left;color:#6b7280;font-weight:600">Código</th>
                <th style="padding:5px 8px;text-align:left;color:#6b7280;font-weight:600">Riesgo</th>
                <th style="padding:5px 8px;text-align:center;color:#6b7280;font-weight:600">Prob. MC</th>
                <th style="padding:5px 8px;text-align:center;color:#6b7280;font-weight:600">Prob. IA</th>
                <th style="padding:5px 8px;text-align:center;color:#6b7280;font-weight:600">Δ pp</th>
                <th style="padding:5px 8px;text-align:left;color:#6b7280;font-weight:600">Razonamiento IA</th>
              </tr>
            </thead>
            <tbody>${riesgosHtml}</tbody>
          </table>
        </div>
        <p style="font-size:9px;color:#94a3b8;margin-top:4px">
          pp = puntos porcentuales de diferencia. ⚠ = divergencia >15pp con el análisis MC (revisar manualmente).
        </p>
      </div>` : ''}

      <!-- Nota metodológica -->
      <div style="padding:8px 12px;background:#f5f3ff;border-radius:6px;border:1px solid #e9d5ff">
        <p style="font-size:9px;color:#6d28d9;line-height:1.5">
          <strong>Nota metodológica:</strong> Este análisis es de naturaleza <strong>cualitativa y predictiva</strong>.
          MiroFish simula interacciones de agentes para identificar trayectorias emergentes,
          complementando —no reemplazando— el análisis cuantitativo Monte Carlo.
          Las probabilidades ajustadas son estimaciones del modelo y deben ser validadas por el equipo del proyecto.
        </p>
      </div>
    </div>`;
}

// ── Hook activateTab para inyectar botones PDF en tabs Riesgos y Resumen ──────
document.addEventListener('DOMContentLoaded', function () {
    var _prev = window.activateTab;
    window.activateTab = function (tabId) {
        if (_prev) _prev.call(this, tabId);
        if (tabId === 'risks')      tcInjectPdfBtn('risks');
        if (tabId === 'tc-resumen') tcInjectPdfBtn('resumen');
    };
    // Inyectar también en la sección activa al cargar (por si el tab por defecto es resumen)
    setTimeout(function () {
        if (document.getElementById('tc-resumen-content')) tcInjectPdfBtn('resumen');
        if (document.getElementById('risks-form'))         tcInjectPdfBtn('risks');
    }, 500);
});
