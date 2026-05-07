/**
 * Vectores Financieros — UI cliente (KPI, filtros, grid, import Excel, Curva S básica).
 * Config: window.__pycFinCfg desde Pmo.cshtml
 */
(function () {
    var cfg = window.__pycFinCfg || {};
    var modosVector = cfg.modosVector || [];
    var modosAnalisis = cfg.modosAnalisis || [];
    var categoriasKpi = cfg.categoriasKpi || [];
    var codigoSap = cfg.codigoSap || {};
    var pageBase = cfg.pageBase || '/PMO';

    var state = {
        vista: 'vector',
        modoVectorId: 'real_parcial',
        modoAnalisisId: 'reporte1',
        tablaActual: 'real_parcial',
        rows: [],
        page: 1,
        pageSize: 20,
        proyectoId: null,
        sapDescripcionFilter: '',
        pendingSecureAction: null,
        reporte1Pack: null,
        r1MainChart: null,
        r1CascadeV0: null,
        r1CascadeApi: null
    };

    var order9c = { IE: 1, AD: 2, EM: 3, MO: 4, IC: 5, CT: 6, CL: 7, SC: 8 };

    function $(id) { return document.getElementById(id); }

    function normalizeText(s) {
        if (s == null) return '';
        return String(s).normalize('NFD').replace(/\p{M}/gu, '').toUpperCase().replace(/\s+/g, ' ').trim();
    }

    function mapDetalleToCategoria(detalle) {
        var n = normalizeText(detalle);
        if (!n) return null;
        for (var i = 0; i < categoriasKpi.length; i++) {
            var c = categoriasKpi[i];
            var cn = normalizeText(c).replace(/\./g, '');
            if (n.indexOf(cn.replace(/\s/g, '')) >= 0 || cn.replace(/\s/g, '').indexOf(n.replace(/\s/g, '')) >= 0) return c;
        }
        if (n.indexOf('CONSTR') >= 0) return 'CONSTRUCCION';
        if (n.indexOf('INDIRECT') >= 0) return 'INDIRECTOS DE CONTRATISTAS';
        if (n.indexOf('EQUIP') >= 0 || n.indexOf('MATERIAL') >= 0) return 'EQUIPOS Y MATERIALES';
        if (n.indexOf('INGEN') >= 0) return 'INGENIERIA';
        if (n.indexOf('SERVIC') >= 0 || n.indexOf('APOYO') >= 0) return 'SERVICIOS DE APOYO A LA CONSTRUCCION';
        if (n.indexOf('ADM') >= 0 || n.indexOf('ADMIN') >= 0) return 'ADM. DEL PROYECTO';
        if (n.indexOf('ESPECIAL') >= 0) return 'COSTOS ESPECIALES';
        if (n.indexOf('CONTING') >= 0) return 'CONTINGENCIA';
        return null;
    }

    /** Código SAP (MO, IC, …) dentro de texto; coincide palabra / límites no alfanuméricos. */
    function categoriaPorCodigoSap(texto) {
        if (texto == null || texto === '') return null;
        var upper = String(texto).toUpperCase().replace(/\s+/g, ' ').trim();
        if (!upper) return null;
        var keys = Object.keys(codigoSap);
        keys.sort(function (a, b) { return String(b).length - String(a).length; });
        for (var i = 0; i < keys.length; i++) {
            var code = String(keys[i]).toUpperCase();
            if (!code) continue;
            if (upper === code) return codigoSap[keys[i]];
            var re = new RegExp('(^|[^A-Z0-9])' + code.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '([^A-Z0-9]|$)');
            if (re.test(upper)) return codigoSap[keys[i]];
        }
        return null;
    }

    /**
     * Categoría KPI para una fila vector: TIPO (p. ej. MO) y cat_vp primero (alineado a PostgreSQL / Excel),
     * luego detalle_factorial.
     */
    function rowToKpiCategoria(r) {
        var tipo = r.tipo != null ? String(r.tipo) : (r.Tipo != null ? String(r.Tipo) : '');
        var cat = categoriaPorCodigoSap(tipo);
        if (cat) return cat;
        var catVp = r.cat_vp != null ? String(r.cat_vp) : (r.catVp != null ? String(r.catVp) : '');
        cat = categoriaPorCodigoSap(catVp);
        if (cat) return cat;
        return mapDetalleToCategoria(r.detalle_factorial || r.DetalleFactorial);
    }

    function rowPeriodo(row) {
        var p = row.periodo || row.Periodo || '';
        if (!p) return null;
        var d = String(p).substring(0, 10);
        return d;
    }

    function sapDesc(row) {
        return String(row.descripcion || row.Descripcion || '').trim();
    }

    function sapVersion(row) {
        return String(row.versionSap || row.version_sap || row.VersionSap || '').trim();
    }

    function monthStart(s) {
        if (!s) return null;
        return s.length >= 7 ? s.substring(0, 7) + '-01' : s;
    }

    function filterByPeriod(rows, desde, hasta) {
        if (!desde && !hasta) return rows;
        var d0 = desde ? monthStart(desde) : null;
        var d1 = hasta ? monthStart(hasta) : null;
        return rows.filter(function (r) {
            var pr = rowPeriodo(r);
            if (!pr) return true;
            if (d0 && pr < d0) return false;
            if (d1 && pr > d1) return false;
            return true;
        });
    }

    function isParcialMode(id) {
        return id && id.indexOf('parcial') >= 0;
    }

    function computeKpi(rows) {
        var sums = {};
        categoriasKpi.forEach(function (c) { sums[c] = 0; });
        var total = 0;
        rows.forEach(function (r) {
            var m = parseFloat(r.monto != null ? r.monto : r.Monto) || 0;
            total += m;
            var kcat = rowToKpiCategoria(r);
            if (kcat && sums[kcat] != null) sums[kcat] += m;
        });
        return { sums: sums, total: total };
    }

    function renderKpi(rows, modoId) {
        var el = $('vf-kpi');
        if (!el) return;
        var filtered = filterByPeriod(rows, $('vf-desde').value, $('vf-hasta').value);
        var parcial = isParcialMode(modoId);
        var k = computeKpi(filtered);
        var html = '<div class="flex flex-col sm:flex-row gap-3 sm:items-stretch">';
        html += '<div class="grid grid-cols-2 sm:grid-cols-4 gap-2 flex-1 min-w-0">';
        categoriasKpi.forEach(function (c) {
            var amt = k.sums[c] || 0;
            var pct = k.total > 0 ? (100 * amt / k.total).toFixed(1) : '0.0';
            html += '<div class="rounded-lg border border-slate-200 dark:border-slate-600 bg-slate-50 dark:bg-slate-900/50 p-3">';
            html += '<div class="text-[10px] font-semibold text-slate-500 dark:text-slate-400 uppercase leading-tight">' + escapeHtml(c) + '</div>';
            html += '<div class="text-sm font-bold text-slate-800 dark:text-slate-100 mt-1">' + formatUsd(amt) + '</div>';
            html += '<div class="text-xs text-slate-500">' + pct + '%</div></div>';
        });
        html += '</div>';
        if (parcial) {
            html += '<div class="rounded-lg border-2 border-teal-300 dark:border-teal-700 bg-teal-50/80 dark:bg-teal-950/30 px-4 py-3 w-full sm:w-44 lg:w-48 shrink-0 flex flex-col justify-center gap-2 sm:gap-3 sm:min-h-[10.25rem] sm:self-center">';
            html += '<div class="text-[10px] font-semibold text-teal-800 dark:text-teal-200 uppercase leading-tight">Total filtro</div>';
            html += '<div class="text-base sm:text-lg font-bold text-teal-900 dark:text-teal-100 leading-tight break-words">' + formatUsd(k.total) + '</div>';
            html += '</div>';
        }
        html += '</div>';
        el.innerHTML = html;
    }

    function renderSapKpiAndManager(rows) {
        var el = $('vf-kpi');
        if (!el) return;
        var sapTop = $('vf-sap-desc-top');
        // Filtro primario: descripción (lote)
        var descripciones = Array.from(new Set(rows.map(function (r) { return sapDesc(r); }).filter(Boolean))).sort();
        if (sapTop) {
            sapTop.innerHTML = '<option value="">Todas</option>' +
                descripciones.map(function (d) { return '<option value="' + escapeAttr(d) + '"' + (state.sapDescripcionFilter === d ? ' selected' : '') + '>' + escapeHtml(d) + '</option>'; }).join('');
        }
        var byPrimary = rows.filter(function (r) {
            var d = sapDesc(r);
            if (state.sapDescripcionFilter && d !== state.sapDescripcionFilter) return false;
            return true;
        });
        // Filtro secundario: rango de meses
        var filtered = filterByPeriod(byPrimary, $('vf-desde').value, $('vf-hasta').value);
        var cods = ['MO', 'IC', 'EM', 'IE', 'SC', 'AD', 'CL', 'CT'];
        var totals = {};
        cods.forEach(function (c) { totals[c] = 0; });
        filtered.forEach(function (r) {
            cods.forEach(function (c) { totals[c] += parseFloat(r[c] != null ? r[c] : r[c.toLowerCase()]) || 0; });
        });
        var totalSap = cods.reduce(function (a, c) { return a + totals[c]; }, 0);
        var html = '<div class="space-y-3">' +
            '<div class="rounded-lg border border-sky-200 dark:border-sky-700/60 bg-sky-50/60 dark:bg-slate-800/60 p-3">' +
            '<div class="flex flex-wrap items-end gap-2">' +
            '<span class="text-xs text-sky-800 dark:text-sky-300">Filtro primario activo: <strong>' + escapeHtml(state.sapDescripcionFilter || 'Todas') + '</strong></span>' +
            '<button type="button" id="vf-sap-clear" class="px-3 py-1.5 rounded-md border border-slate-300 dark:border-slate-600 text-xs">Limpiar descripción</button>' +
            '<button type="button" id="vf-sap-delete" class="px-3 py-1.5 rounded-md bg-red-600 hover:bg-red-700 text-white text-xs">Eliminar selección</button>' +
            '<div class="sm:ml-auto text-xs font-semibold text-teal-800 dark:text-teal-300">TOTAL SAP: ' + formatUsd(totalSap) + '</div>' +
            '</div></div></div>';
        el.innerHTML = html;
        var sc = $('vf-sap-clear');
        var del = $('vf-sap-delete');
        if (sc) sc.addEventListener('click', function () { state.sapDescripcionFilter = ''; state.page = 1; renderSapKpiAndManager(rows); renderTable(rows); });
        if (del) del.addEventListener('click', function () {
            if (!state.sapDescripcionFilter) {
                showVfToast('Seleccione una descripción para eliminar.', 'warning');
                return;
            }
            if (!confirm('¿Eliminar registros SAP de la selección actual?')) return;
            state.pendingSecureAction = function (clave) {
                fetch(pageBase + '?handler=EliminarFinancieroSap', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
                    body: JSON.stringify({
                        proyectoId: parseInt(state.proyectoId, 10),
                        clave: clave,
                        versionSap: null,
                        descripcion: state.sapDescripcionFilter || null
                    })
                }).then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
                    .then(function (x) {
                        if (!x.ok || !x.j.ok) throw new Error((x.j && x.j.error) || 'No se pudo eliminar');
                        showVfToast(x.j.message || 'Eliminación SAP completada.', 'success');
                        fetchDatos();
                    })
                    .catch(function (e) {
                        showVfToast('Error eliminando selección SAP: ' + (e.message || String(e)), 'error');
                    });
            };
            $('vf-modal-clave').classList.remove('hidden');
            $('vf-clave-input').value = '';
        });
    }

    function formatUsd(n) {
        return new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(n);
    }

    /** Millones USD estilo tablero EVM ($298,06 M). */
    function formatUsdM(n) {
        if (n == null || isNaN(Number(n))) return '—';
        var m = Number(n) / 1e6;
        return '$' + m.toLocaleString('es-CL', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + 'M';
    }

    function fmtEvmIdx(x) {
        if (x == null || isNaN(Number(x))) return '—';
        return Number(x).toFixed(3);
    }

    function evmValueClassSigned(n) {
        if (n == null || isNaN(Number(n))) return 'text-slate-700 dark:text-slate-200';
        return Number(n) >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400';
    }

    function escapeHtml(s) {
        if (s == null) return '';
        var d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    /** Para atributos HTML (p. ej. title). */
    function escapeAttr(s) {
        if (s == null) return '';
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;')
            .replace(/</g, '&lt;');
    }

    /** Burbuja bajo la etiqueta (evita recorte por overflow-hidden y solapa menos con filas de arriba). */
    function evmTipBubbleHtml(tt) {
        return '<span class="vf-evm-tip-bubble invisible opacity-0 translate-y-px group-hover:visible group-hover:opacity-100 group-hover:translate-y-0 group-focus:visible group-focus:opacity-100 group-focus:translate-y-0 transition-all duration-150 ease-out absolute z-[100] top-full left-0 mt-1.5 w-[min(15.5rem,calc(100vw-2rem))] rounded-lg bg-slate-900 text-white text-[10px] px-2.5 py-2 shadow-lg shadow-black/30 ring-1 ring-white/10 dark:bg-slate-950 dark:ring-slate-600/40 leading-snug text-left whitespace-normal pointer-events-none" role="tooltip">' + escapeHtml(tt) + '</span>';
    }

    /**
     * Etiqueta con tooltip (solo burbuja custom, sin title). Una línea + truncate en la etiqueta.
     */
    function evmTipLabel(displayText, tip) {
        var tt = tip || displayText || '';
        return '<span class="group relative isolate block min-w-0 w-full outline-none rounded-sm focus-visible:ring-2 focus-visible:ring-sky-500/80 focus-visible:ring-offset-1 focus-visible:ring-offset-white dark:focus-visible:ring-offset-slate-800" tabindex="0" role="button" aria-label="' + escapeAttr(tt) + '">' +
            '<span class="block w-full min-w-0 truncate text-[10px] font-semibold text-slate-600 dark:text-slate-300 cursor-help border-b border-dotted border-slate-400/70 dark:border-slate-500">' + escapeHtml(displayText) + '</span>' +
            evmTipBubbleHtml(tt) + '</span>';
    }

    function evmMetricLine(labelDisplay, tip, valueText, valueClass) {
        valueClass = valueClass || 'text-slate-800 dark:text-slate-100';
        var v = valueText == null ? '' : String(valueText);
        return '<div class="flex items-baseline gap-2 w-full min-w-0 py-0.5">' +
            '<span class="min-w-0 flex-1">' + evmTipLabel(labelDisplay, tip) + '</span>' +
            '<span class="tabular-nums text-right font-semibold text-[11px] shrink-0 pl-1 ' + valueClass + '">' + escapeHtml(v) + '</span></div>';
    }

    function evmIndexLine(labelDisplay, tip, val) {
        var c = Number(val);
        var cls = !isNaN(c) && c >= 1 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400';
        return '<div class="flex items-baseline gap-2 w-full min-w-0 py-0.5">' +
            '<span class="min-w-0 flex-1">' + evmTipLabel(labelDisplay, tip) + '</span>' +
            '<span class="text-base font-bold tabular-nums shrink-0 pl-1 leading-none ' + cls + '">' + escapeHtml(fmtEvmIdx(val)) + '</span></div>';
    }

    function evmEstadoLine(labelDisplay, tip, estadoText) {
        var t = String(estadoText || '—');
        var good = /adelant|bajo|excelente|favorable|en\s*plazo|según\s*plan|ok|bueno/i.test(t) && !/sobre|retraso|crítico|alerta|defavorable|malo|riesgo\s*alto/i.test(t);
        var ic = good ? 'check_circle' : 'warning';
        var cls = good ? 'text-emerald-600 dark:text-emerald-400' : 'text-amber-600 dark:text-amber-400';
        return '<div class="flex items-center gap-2 w-full min-w-0 py-0.5">' +
            '<span class="min-w-0 flex-1">' + evmTipLabel(labelDisplay, tip) + '</span>' +
            '<span class="inline-flex items-center gap-0.5 font-semibold text-[11px] shrink-0 pl-1 text-right max-w-[58%] min-w-0 justify-end ' + cls + '">' +
            '<span class="material-icons text-[15px] shrink-0" aria-hidden="true">' + ic + '</span><span class="leading-tight break-words">' + escapeHtml(t) + '</span></span></div>';
    }

    /** Fila larga (IEAC/ECD): tooltip bajo el bloque, mismo estilo. */
    function evmMethodLabel(text, tip) {
        var tt = tip || text || '';
        return '<span class="group relative isolate inline-flex items-start gap-0.5 min-w-0 max-w-full outline-none rounded-sm focus-visible:ring-2 focus-visible:ring-sky-500/80 focus-visible:ring-offset-1 focus-visible:ring-offset-white dark:focus-visible:ring-offset-slate-800" tabindex="0" role="button" aria-label="' + escapeAttr(tt) + '">' +
            '<span class="cursor-help border-b border-dotted border-slate-400/80 dark:border-slate-500 min-w-0 break-words text-[11px] leading-snug">' + escapeHtml(text) + '</span>' +
            '<span class="material-icons text-[13px] text-slate-400 shrink-0 mt-px group-hover:text-sky-600 dark:group-hover:text-sky-400 transition-colors" aria-hidden="true">info</span>' +
            evmTipBubbleHtml(tt) + '</span>';
    }

    function cardEvmPro(o) {
        o = o || {};
        var sub = o.subtitle
            ? '<p class="text-[9px] text-slate-500 dark:text-slate-400 mt-0.5 leading-snug">' + escapeHtml(o.subtitle) + '</p>'
            : '';
        return '<div class="rounded-lg border border-slate-200/90 dark:border-slate-600 bg-white dark:bg-slate-800 shadow-sm overflow-visible flex flex-col h-full relative z-0 hover:z-[70] focus-within:z-[70]">' +
            '<div class="h-0.5 ' + (o.accentBar || 'bg-slate-400') + '"></div>' +
            '<div class="p-2.5 flex flex-col flex-1 overflow-visible">' +
            '<div class="flex items-start gap-2 mb-2">' +
            '<span class="flex h-7 w-7 shrink-0 items-center justify-center rounded-full ' + (o.iconCircle || 'bg-slate-600') + ' text-white material-icons text-[16px] leading-none" aria-hidden="true">' + escapeHtml(o.icon || 'insights') + '</span>' +
            '<div class="min-w-0 flex-1 pt-0.5">' +
            '<div class="text-[10px] font-bold uppercase tracking-wide text-slate-700 dark:text-slate-200 leading-tight truncate">' + escapeHtml(o.title || '') + '</div>' +
            sub +
            '</div></div>' +
            '<div class="space-y-0 flex-1 text-xs">' + (o.bodyHtml || '') + '</div></div></div>';
    }

    /** Acordeón nativo para listas largas IEAC / ECD (cerrado por defecto). */
    function evmAccordionDetails(summaryText, innerRowsHtml) {
        return '<details class="vf-evm-details mt-2 border-t border-slate-200 dark:border-slate-600 pt-2">' +
            '<summary class="flex cursor-pointer list-none items-center gap-1 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-sky-800 dark:text-sky-300 hover:text-sky-950 dark:hover:text-sky-100 [&::-webkit-details-marker]:hidden">' +
            '<span class="material-icons vf-evm-acc-icon text-[17px] text-sky-600 shrink-0 transition-transform duration-200" aria-hidden="true">chevron_right</span>' +
            '<span>' + escapeHtml(summaryText) + '</span></summary>' +
            '<div class="mt-2 divide-y divide-slate-200 dark:divide-slate-600">' + innerRowsHtml + '</div></details>';
    }

    function addCanvasToPdfPages(pdf, canvas, marginMm) {
        marginMm = marginMm == null ? 10 : marginMm;
        var pageW = pdf.internal.pageSize.getWidth();
        var pageH = pdf.internal.pageSize.getHeight();
        var maxW = pageW - 2 * marginMm;
        var maxH = pageH - 2 * marginMm;
        var fullImgH = (canvas.height * maxW) / canvas.width;
        if (fullImgH <= maxH) {
            pdf.addImage(canvas.toDataURL('image/png'), 'PNG', marginMm, marginMm, maxW, fullImgH);
            return;
        }
        var yPx = 0;
        while (yPx < canvas.height) {
            var slicePx = Math.max(1, Math.floor((maxH * canvas.width) / maxW));
            var hThis = Math.min(slicePx, canvas.height - yPx);
            var slice = document.createElement('canvas');
            slice.width = canvas.width;
            slice.height = hThis;
            var ctx = slice.getContext('2d');
            ctx.drawImage(canvas, 0, yPx, canvas.width, hThis, 0, 0, canvas.width, hThis);
            var sliceHmm = (hThis * maxW) / canvas.width;
            pdf.addImage(slice.toDataURL('image/png'), 'PNG', marginMm, marginMm, maxW, sliceHmm);
            yPx += hThis;
            if (yPx < canvas.height) pdf.addPage();
        }
    }

    function getJsPdfCtor() {
        return window.jspdf && window.jspdf.jsPDF ? window.jspdf.jsPDF : window.jsPDF;
    }

    function evmMusd(n) {
        return (Number(n) || 0) / 1e6;
    }

    /** Marca y pie de pagina alineados a informes PMO JEJ (PDF de referencia). */
    var PDF_JEJ_ORG = 'JEJ INGENIERÍA S.A.';
    var PDF_JEJ_PMO = 'PMO - JEJ INGENIERÍA';

    /** Millones USD estilo informe adjunto: $409.20M */
    function formatPdfMUsd(n) {
        if (n == null || isNaN(Number(n))) return '$0.00M';
        var x = Number(n) / 1e6;
        return '$' + x.toFixed(2) + 'M';
    }

    /**
     * Montos para jsPDF: solo ASCII (evita glifos rotos con Helvetica / WinAnsi).
     * Chile: miles con punto. Ej.: US$ 65.101.091
     */
    function formatUsdForPdf(n) {
        if (n == null || isNaN(Number(n))) return 'US$ 0';
        var v = Math.round(Number(n));
        var neg = v < 0;
        v = Math.abs(v);
        var s = String(v).replace(/\B(?=(\d{3})+(?!\d))/g, '.');
        return (neg ? '- ' : '') + 'US$ ' + s;
    }

    /** Normaliza texto para fuentes estandar de PDF (sin >= unicode, guiones raros, etc.). */
    function pdfSafeText(s) {
        if (s == null) return '';
        return String(s)
            .replace(/\u00A0|\u202F|\u2007|\u2009/g, ' ')
            .replace(/\u2212|\u2013|\u2014|\u2015/g, '-')
            .replace(/\u2265/g, '>=')
            .replace(/\u2264/g, '<=')
            .replace(/\u00F7/g, '/')
            .replace(/\u00D7/g, 'x')
            .replace(/[\u2018\u2019]/g, "'")
            .replace(/[\u201C\u201D]/g, '"');
    }

    function pdfTimestampForPdf() {
        var d = new Date();
        function z(n) { return n < 10 ? '0' + n : '' + n; }
        return z(d.getDate()) + '/' + z(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + z(d.getHours()) + ':' + z(d.getMinutes());
    }

    function pdfPara(doc, text, x, y, maxW, lh) {
        lh = lh || 5;
        doc.splitTextToSize(pdfSafeText(text), maxW).forEach(function (ln) {
            doc.text(ln, x, y);
            y += lh;
        });
        return y;
    }

    /** Pie estilo PDF JEJ: PMO | titulo | Pagina X de Y + Generado: ... */
    function pdfFooterJej(doc, reportTitle, pageNum, totalPages) {
        var H = doc.internal.pageSize.getHeight();
        var W = doc.internal.pageSize.getWidth();
        var m = 14;
        doc.setFillColor(45, 52, 64);
        doc.rect(0, H - 14, W, 14, 'F');
        doc.setFontSize(7.5);
        doc.setFont('helvetica', 'normal');
        doc.setTextColor(255, 255, 255);
        var line1 = PDF_JEJ_PMO + ' | ' + reportTitle + ' | Página ' + pageNum + ' de ' + totalPages;
        doc.text(pdfSafeText(line1), m, H - 9);
        doc.text(pdfSafeText('Generado: ' + pdfTimestampForPdf()), m, H - 4);
        doc.setTextColor(0, 0, 0);
    }

    function tcpiFromInd(ind) {
        var bac = Number(ind.bac);
        var ev = Number(ind.ev);
        var ac = Number(ind.ac);
        var eac = Number(ind.eac);
        var d1 = bac - ac;
        if (Math.abs(d1) > 1e-9) return (bac - ev) / d1;
        var d2 = eac - ac;
        if (Math.abs(d2) > 1e-9) return (bac - ev) / d2;
        return null;
    }

    function qualColor(doc, labelText) {
        var t = String(labelText).toLowerCase();
        var ok = /eficiente|adelantado|favorable|bajo|excelente/.test(t) && !/ineficiente|atrasado|desfavorable|sobre/.test(t);
        if (/ineficiente|atrasado|desfavorable|sobre presupuesto/.test(t)) ok = false;
        doc.setTextColor(ok ? 16 : 180, ok ? 120 : 40, ok ? 70 : 40);
    }

    /** Titulo de seccion + linea (estructura tipo informe formal). */
    function pdfSectionTitle(doc, title, m, y, maxW) {
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(10);
        doc.setTextColor(30, 58, 95);
        doc.text(pdfSafeText(title), m, y);
        doc.setDrawColor(180, 190, 205);
        doc.setLineWidth(0.35);
        doc.line(m, y + 1.8, m + maxW, y + 1.8);
        doc.setTextColor(25, 35, 50);
        return y + 7;
    }

    /** Fila etiqueta (negrita) + valor (posible multilinea). contentMaxW = ancho util desde m. */
    function pdfLabeledValue(doc, label, value, m, y, labelW, contentMaxW) {
        labelW = labelW || 58;
        var valW = Math.max(40, contentMaxW - labelW - 2);
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(9);
        doc.setTextColor(55, 62, 75);
        doc.text(pdfSafeText(label) + ':', m, y);
        doc.setFont('helvetica', 'normal');
        doc.setTextColor(20, 28, 42);
        var lines = doc.splitTextToSize(pdfSafeText(String(value)), valW);
        var yy = y;
        lines.forEach(function (ln) {
            doc.text(ln, m + labelW, yy);
            yy += 4.2;
        });
        return yy + 1;
    }

    /** Tabla 3 columnas con encabezado y filas (bordes visibles). */
    function pdfTable3ColHeader(doc, m, y, totalW, x2, x3) {
        var rh = 6;
        doc.setFillColor(225, 232, 242);
        doc.setDrawColor(150, 162, 182);
        doc.setLineWidth(0.2);
        doc.rect(m, y - 4, totalW, rh, 'FD');
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(8);
        doc.setTextColor(35, 48, 72);
        doc.text('Indicador', m + 2, y);
        doc.text('Valor', m + x2, y);
        doc.text('Estado', m + x3, y);
        doc.setFont('helvetica', 'normal');
        doc.setTextColor(25, 35, 50);
        return y + rh - 0.5;
    }

    function pdfTable3ColRow(doc, m, y, totalW, x2, x3, c1, c2, c3, stripe) {
        var rh = 6.5;
        if (stripe) {
            doc.setFillColor(248, 250, 253);
            doc.rect(m, y - 4, totalW, rh, 'F');
        }
        doc.setDrawColor(190, 198, 212);
        doc.setLineWidth(0.15);
        doc.rect(m, y - 4, totalW, rh, 'S');
        doc.setFontSize(8);
        doc.text(pdfSafeText(c1), m + 2, y);
        doc.text(pdfSafeText(c2), m + x2, y);
        qualColor(doc, c3);
        doc.text(pdfSafeText(c3), m + x3, y);
        doc.setTextColor(25, 35, 50);
        return y + rh;
    }

    /** Encabezado tabla 3 columnas con titulos personalizados. */
    function pdfTable3ColHeaderCustom(doc, m, y, totalW, x2, x3, t1, t2, t3) {
        var rh = 6;
        doc.setFillColor(225, 232, 242);
        doc.setDrawColor(150, 162, 182);
        doc.setLineWidth(0.2);
        doc.rect(m, y - 4, totalW, rh, 'FD');
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(8);
        doc.setTextColor(35, 48, 72);
        doc.text(pdfSafeText(t1), m + 2, y);
        doc.text(pdfSafeText(t2), m + x2, y);
        doc.text(pdfSafeText(t3), m + x3, y);
        doc.setFont('helvetica', 'normal');
        doc.setTextColor(25, 35, 50);
        return y + rh - 0.5;
    }

    /** Bloque estimacion: caja con borde; primero calcula altura, dibuja fondo y luego texto. */
    function pdfEstimacionBox(doc, titulo, valor, narrativa, m, yTop, maxW) {
        var pad = 3;
        var innerW = maxW - 2 * pad;
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(8.5);
        var lineA = titulo + ' ' + valor;
        var lines1 = doc.splitTextToSize(pdfSafeText(lineA), innerW);
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(8.5);
        var lines2 = doc.splitTextToSize(pdfSafeText(narrativa), innerW);
        var boxH = pad + lines1.length * 4 + 2 + lines2.length * 3.8 + pad;
        doc.setDrawColor(175, 185, 200);
        doc.setFillColor(252, 253, 255);
        doc.setLineWidth(0.2);
        doc.roundedRect(m, yTop, maxW, boxH, 0.8, 0.8, 'FD');
        var ty = yTop + 5;
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(8.5);
        doc.setTextColor(40, 52, 72);
        lines1.forEach(function (ln) {
            doc.text(ln, m + pad, ty);
            ty += 4;
        });
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(8.5);
        doc.setTextColor(45, 50, 60);
        lines2.forEach(function (ln) {
            doc.text(ln, m + pad, ty);
            ty += 3.8;
        });
        doc.setTextColor(25, 35, 50);
        return yTop + boxH + 3;
    }

    /** PDF técnico: 1 página, mismo esquema de contenido que informe JEJ de referencia. */
    function generarPdfTecnicoEvm(JsPDF) {
        var pack = state.reporte1Pack;
        var ind = pack.indicadores;
        if (!ind || !ind.fechaSeguimiento) {
            showVfToast('No hay fecha de seguimiento. Seleccione el mes y actualice el análisis EVM.', 'warning');
            return;
        }
        var fs = String(ind.fechaSeguimiento).substring(0, 10);
        var doc = new JsPDF({ unit: 'mm', format: 'a4', orientation: 'portrait' });
        var W = doc.internal.pageSize.getWidth();
        var H = doc.internal.pageSize.getHeight();
        var m = 14;
        var maxW = W - 2 * m;
        var bac = Number(ind.bac);
        var eac = Number(ind.eac);
        var etc = Number(ind.etc);
        var vac = Number(ind.vac);
        var spiOk = Number(ind.spi) >= 1;
        var cpiOk = Number(ind.cpi) >= 1;
        var lblCpi = Number(ind.cpi) >= 1 ? 'Eficiente' : 'Ineficiente';
        var lblSpi = Number(ind.spi) >= 1 ? 'Adelantado' : 'Atrasado';
        var lblCv = Number(ind.cv) >= 0 ? 'Favorable' : 'Desfavorable';
        var lblSv = Number(ind.sv) >= 0 ? 'Favorable' : 'Desfavorable';
        var pctBajoBac = bac > 0 ? ((bac - eac) / bac) * 100 : 0;
        var pctVacBac = bac > 0 ? (vac / bac) * 100 : 0;
        var etcMod = bac > 0 && etc < bac * 0.08 ? 'bajo' : 'moderado';

        doc.setFillColor(255, 255, 255);
        doc.rect(0, 0, W, H, 'F');
        doc.setFillColor(26, 54, 93);
        doc.rect(0, 0, W, 12, 'F');
        doc.setTextColor(255, 255, 255);
        doc.setFontSize(12);
        doc.setFont('helvetica', 'bold');
        doc.text('REPORTE TÉCNICO EVM', m, 8);
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(8);
        doc.text(PDF_JEJ_ORG + ' | ' + fs, W - m, 8, { align: 'right' });

        var y = 18;
        var colVal = 82;
        var colEst = 118;
        y = pdfSectionTitle(doc, 'RESUMEN GENERAL', m, y, maxW);
        y = pdfLabeledValue(doc, 'Fecha de seguimiento', fs, m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Presupuesto total (BAC)', formatPdfMUsd(ind.bac), m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Costo real (AC)', formatPdfMUsd(ind.ac), m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Valor ganado (EV)', formatPdfMUsd(ind.ev), m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Costo planificado (PV)', formatPdfMUsd(ind.pv), m, y, 56, maxW);
        y += 2;
        y = pdfSectionTitle(doc, 'ESTADO DEL PROYECTO', m, y, maxW);
        y = pdfLabeledValue(doc, 'Avance completado (EV)', Number(ind.pctEv).toFixed(1) + '%', m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Avance planificado (PV)', Number(ind.pctPv).toFixed(1) + '%', m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Avance real (AC)', Number(ind.pctAc).toFixed(1) + '%', m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Estado del cronograma', spiOk ? 'Adelantado' : 'Atrasado', m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Estado de costo', cpiOk ? 'Bajo Presupuesto' : 'Sobre Presupuesto', m, y, 56, maxW);
        y += 2;
        y = pdfSectionTitle(doc, 'INDICADORES CLAVE DE RENDIMIENTO', m, y, maxW);
        doc.setFont('helvetica', 'italic');
        doc.setFontSize(8);
        doc.setTextColor(90, 96, 108);
        doc.text('Indicador / Valor / Estado', m, y);
        doc.setFont('helvetica', 'normal');
        doc.setTextColor(25, 35, 50);
        y += 4;
        y = pdfTable3ColHeader(doc, m, y, maxW, colVal, colEst);
        y = pdfTable3ColRow(doc, m, y, maxW, colVal, colEst, 'CPI (Desempeño de Costos)', fmtEvmIdx(ind.cpi), '(' + lblCpi + ')', false);
        y = pdfTable3ColRow(doc, m, y, maxW, colVal, colEst, 'SPI (Desempeño del Cronograma)', fmtEvmIdx(ind.spi), '(' + lblSpi + ')', true);
        y = pdfTable3ColRow(doc, m, y, maxW, colVal, colEst, 'CV (Variación de Costo)', formatPdfMUsd(ind.cv), '(' + lblCv + ')', false);
        y = pdfTable3ColRow(doc, m, y, maxW, colVal, colEst, 'SV (Variación de Cronograma)', formatPdfMUsd(ind.sv), '(' + lblSv + ')', true);
        y += 2;
        y = pdfSectionTitle(doc, 'ESTIMACIONES FINANCIERAS', m, y, maxW);
        doc.setFont('helvetica', 'italic');
        doc.setFontSize(8);
        doc.setTextColor(90, 96, 108);
        doc.text('Estimación / Valor / Análisis', m, y);
        doc.setFont('helvetica', 'normal');
        doc.setTextColor(25, 35, 50);
        y += 5;
        var narrEac = eac <= bac && pctBajoBac >= 0
            ? ('Excelente: ' + pctBajoBac.toFixed(1) + '% bajo presupuesto. Gestión financiera sobresaliente.')
            : 'Atención: proyección por encima del BAC; reforzar control de costos.';
        y = pdfEstimacionBox(doc, 'EAC (Costo Estimado Total)', formatPdfMUsd(ind.eac) + ' -', narrEac, m, y, maxW);
        y = pdfEstimacionBox(doc, 'ETC (Costo para Completar)', formatPdfMUsd(ind.etc) + ' -', 'Costo restante ' + etcMod + ': ' + formatPdfMUsd(ind.etc) + ' para finalizar el proyecto.', m, y, maxW);
        var narrVac = vac >= 0
            ? ('Ahorro significativo: ' + pctVacBac.toFixed(1) + '% del presupuesto. Oportunidad estratégica.')
            : 'Sobrecosto proyectado al cierre; revisar plan de mitigación.';
        y = pdfEstimacionBox(doc, 'VAC (Variación Final)', formatPdfMUsd(ind.vac) + ' -', narrVac, m, y, maxW);

        pdfFooterJej(doc, 'Reporte Técnico EVM', 1, 1);
        var safeFs = fs.replace(/[^\d-]/g, '') || fs;
        doc.save('Reporte_Tecnico_EVM_' + safeFs + '.pdf');
        showVfToast('PDF técnico descargado.', 'success');
    }

    function analisisCurvaSText(ind) {
        var ev = formatPdfMUsd(ind.ev);
        var pv = formatPdfMUsd(ind.pv);
        var ac = formatPdfMUsd(ind.ac);
        var spi = Number(ind.spi);
        var cpi = Number(ind.cpi);
        var adelCron = (spi - 1) * 100;
        var effCost = (cpi - 1) * 100;
        if (Number(ind.ev) >= Number(ind.pv) && spi >= 1) {
            return 'La curva de Valor Ganado (EV: ' + ev + ') supera significativamente tanto el Costo Planificado (PV: ' + pv + ') como el Costo Real (AC: ' + ac + '). El adelanto del ' + adelCron.toFixed(1) + '% sugiere posibilidad de completar antes del cronograma. La eficiencia del ' + effCost.toFixed(1) + '% en costos confirma excelente gestión financiera.';
        }
        return 'A la fecha de seguimiento, EV (' + ev + '), PV (' + pv + ') y AC (' + ac + ') describen la posición de la curva. CPI ' + fmtEvmIdx(cpi) + ' y SPI ' + fmtEvmIdx(spi) + ' sintetizan eficiencia de costo y avance valorizado vs. plan; conviene revisar el detalle mensual y el avance físico.';
    }

    function analisisTendenciasText(ind) {
        var vac = formatPdfMUsd(ind.vac);
        var bac = Number(ind.bac);
        var eac = Number(ind.eac);
        var pctAhorro = bac > 0 ? ((bac - eac) / bac) * 100 : 0;
        var cpiOk = Number(ind.cpi) >= 1;
        var spiOk = Number(ind.spi) >= 1;
        if (cpiOk && spiOk && pctAhorro > 0) {
            return 'Tendencia POSITIVA: Los índices muestran desempeño favorable y estable. Proyección MUY OPTIMISTA: Ahorro significativo del ' + pctAhorro.toFixed(1) + '% proyectado. OPORTUNIDAD: Ahorro de ' + vac + ' permite reasignar recursos estratégicos.';
        }
        return 'Tendencia a monitorear: CPI ' + fmtEvmIdx(ind.cpi) + ' y SPI ' + fmtEvmIdx(ind.spi) + '. Mantener seguimiento mensual y acciones correctivas según desviaciones de costo o cronograma.';
    }

    function pdfEvaluacionEjecutiva(ind) {
        var spiOk = Number(ind.spi) >= 1;
        var cpiOk = Number(ind.cpi) >= 1;
        if (spiOk && cpiOk) {
            return 'El proyecto está adelantado en términos de valor ganado y dentro de presupuesto, con un desempeño financiero y de cronograma favorable.';
        }
        if (!spiOk && !cpiOk) {
            return 'El proyecto requiere atención en costo y cronograma según los índices CPI y SPI a la fecha de seguimiento.';
        }
        if (!cpiOk) {
            return 'El proyecto presenta presión de costo a la fecha de seguimiento; el cronograma valorizado puede ser favorable o no según SPI.';
        }
        return 'El proyecto muestra desafíos de cronograma valorizado a la fecha de seguimiento; el desempeño de costo debe revisarse según CPI.';
    }

    function pdfTcpiNarrativa(tcpi) {
        if (tcpi == null || !isFinite(tcpi)) return '';
        if (tcpi < 1) return 'TCPI favorable: ' + tcpi.toFixed(3) + '. El proyecto puede relajarse en costos futuros.';
        if (tcpi > 1.05) return 'TCPI ' + tcpi.toFixed(3) + ': se requiere mayor eficiencia en el trabajo restante para cumplir el BAC.';
        return 'TCPI: ' + tcpi.toFixed(3) + '. Mantener el ritmo de costo observado en el trabajo restante.';
    }

    function pdfConclusionEjecutiva(ind) {
        var spiOk = Number(ind.spi) >= 1;
        var cpiOk = Number(ind.cpi) >= 1;
        if (spiOk && cpiOk) {
            return 'El proyecto está en una posición sólida: adelantado en el cronograma, bajo presupuesto y con un desempeño eficiente tanto en costos como en tiempo.';
        }
        return 'El proyecto requiere seguimiento integrado de costo y cronograma; los indicadores EVM a la fecha de seguimiento orientan las prioridades de gestión.';
    }

    /** PDF ejecutivo: 4 páginas, mismo esquema que informe JEJ de referencia. */
    function generarPdfEjecutivoEvm(JsPDF) {
        var pack = state.reporte1Pack;
        var ind = pack.indicadores;
        if (!ind || !ind.fechaSeguimiento) {
            showVfToast('No hay fecha de seguimiento seleccionada. Seleccione una fecha en el análisis EVM.', 'warning');
            return;
        }
        if (ind.ac == null || ind.ev == null || ind.pv == null || ind.bac == null) {
            showVfToast('Faltan datos críticos (AC, EV, PV, BAC) para el PDF ejecutivo. Actualice el análisis.', 'warning');
            return;
        }
        var fs = String(ind.fechaSeguimiento).substring(0, 10);
        var doc = new JsPDF({ unit: 'mm', format: 'a4', orientation: 'portrait' });
        var W = doc.internal.pageSize.getWidth();
        var H = doc.internal.pageSize.getHeight();
        var m = 14;
        var maxW = W - 2 * m;
        var af = pack.avanceFisico || {};
        var ie = pack.ieac || {};
        var ieacRec = ie.promedio != null ? Number(ie.promedio) : null;
        var ieMin = ie.minimo != null ? Number(ie.minimo) : null;
        var ieMax = ie.maximo != null ? Number(ie.maximo) : null;
        var bac = Number(ind.bac);
        var eac = Number(ind.eac);
        var etc = Number(ind.etc);
        var spiOk = Number(ind.spi) >= 1;
        var cpiOk = Number(ind.cpi) >= 1;

        doc.setFillColor(255, 255, 255);
        doc.rect(0, 0, W, H, 'F');
        doc.setFillColor(26, 54, 93);
        doc.rect(0, 0, W, 26, 'F');
        doc.setTextColor(255, 255, 255);
        doc.setFontSize(13);
        doc.setFont('helvetica', 'bold');
        doc.text('REPORTE EJECUTIVO EVM', m, 10);
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(8.5);
        doc.text(PDF_JEJ_ORG + ' - Análisis de Gestión de Proyectos', m, 16);
        doc.text('Fecha de Análisis: ' + fs, m, 21);
        doc.setTextColor(0, 0, 0);

        var cxV = 82;
        var cxE = 118;
        var y = 32;
        y = pdfSectionTitle(doc, 'RESUMEN GENERAL DEL PROYECTO', m, y, maxW);
        y = pdfLabeledValue(doc, 'Fecha de seguimiento', fs, m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Presupuesto total (BAC)', formatPdfMUsd(ind.bac), m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Costo real (AC)', formatPdfMUsd(ind.ac) + ' (gastos acumulados hasta la fecha)', m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Valor ganado (EV)', formatPdfMUsd(ind.ev) + ' (valor del trabajo completado)', m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Costo planificado (PV)', formatPdfMUsd(ind.pv) + ' (valor planificado para esta fecha)', m, y, 56, maxW);
        y += 2;
        y = pdfSectionTitle(doc, 'ESTADO DEL PROYECTO', m, y, maxW);
        y = pdfLabeledValue(doc, 'Completado (EV)', Number(ind.pctEv).toFixed(1) + '%', m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Planificado (PV)', Number(ind.pctPv).toFixed(1) + '%', m, y, 56, maxW);
        y = pdfLabeledValue(doc, 'Real (AC)', Number(ind.pctAc).toFixed(1) + '%', m, y, 56, maxW);
        y += 1;
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(9);
        doc.setTextColor(55, 62, 75);
        doc.text('Evaluación:', m, y);
        y += 4;
        doc.setFont('helvetica', 'normal');
        doc.setTextColor(25, 35, 50);
        y = pdfPara(doc, pdfEvaluacionEjecutiva(ind), m, y, maxW, 4.3);
        y += 2;
        y = pdfSectionTitle(doc, 'RESUMEN GESTIÓN DE VALOR GANADO (GVG)', m, y, maxW);
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(8.5);
        doc.text('1. GESTION DEL COSTE', m + 2, y);
        doc.setFont('helvetica', 'normal');
        y += 4.5;
        y = pdfLabeledValue(doc, 'CPI', fmtEvmIdx(ind.cpi), m + 4, y, 24, maxW - 6);
        y = pdfLabeledValue(doc, 'CV', formatPdfMUsd(ind.cv), m + 4, y, 24, maxW - 6);
        y += 2;
        doc.setFont('helvetica', 'bold');
        doc.text('2. GESTION DEL CRONOGRAMA', m + 2, y);
        doc.setFont('helvetica', 'normal');
        y += 4.5;
        y = pdfLabeledValue(doc, 'SPI', fmtEvmIdx(ind.spi), m + 4, y, 24, maxW - 6);
        y = pdfLabeledValue(doc, 'SV', formatPdfMUsd(ind.sv), m + 4, y, 24, maxW - 6);
        y += 2;
        doc.setFont('helvetica', 'bold');
        doc.text('3. GESTION DEL DESEMPEÑO TECNICO', m + 2, y);
        doc.setFont('helvetica', 'normal');
        y += 4.5;
        y = pdfLabeledValue(doc, 'Avance Fisico Real', af.realPct != null ? Number(af.realPct).toFixed(2) + '%' : 'N/A', m + 4, y, 44, maxW - 6);
        y = pdfLabeledValue(doc, 'Avance Fisico Planif.', af.apiPct != null ? Number(af.apiPct).toFixed(2) + '%' : 'N/A', m + 4, y, 44, maxW - 6);
        y = pdfLabeledValue(doc, 'Desviacion Fisica', af.desviacionPct != null ? Number(af.desviacionPct).toFixed(2) + '%' : 'N/A', m + 4, y, 44, maxW - 6);
        y = pdfLabeledValue(doc, 'Estado cronograma', spiOk ? 'Adelantado' : 'Atrasado', m + 4, y, 44, maxW - 6);
        y = pdfLabeledValue(doc, 'Estado costo', cpiOk ? 'Bajo Presupuesto' : 'Sobre Presupuesto', m + 4, y, 44, maxW - 6);
        y += 2;
        y = pdfSectionTitle(doc, 'INDICADORES CLAVE DE RENDIMIENTO', m, y, maxW);
        y += 2;
        y = pdfTable3ColHeaderCustom(doc, m, y, maxW, cxV, cxE, 'Indicador', 'Valor', 'Análisis');
        y = pdfTable3ColRow(doc, m, y, maxW, cxV, cxE, 'CPI (Desempeño Costos)', fmtEvmIdx(ind.cpi), cpiOk ? 'Eficiencia excelente por dólar gastado' : 'Revisar eficiencia', false);
        y = pdfTable3ColRow(doc, m, y, maxW, cxV, cxE, 'SPI (Desempeño Cronograma)', fmtEvmIdx(ind.spi), spiOk ? 'Adelanto vs. plan' : 'Atraso vs. plan', true);
        pdfFooterJej(doc, 'Reporte Ejecutivo EVM', 1, 4);

        doc.addPage();
        doc.setFillColor(255, 255, 255);
        doc.rect(0, 0, W, H, 'F');
        doc.setFillColor(26, 54, 93);
        doc.rect(0, 0, W, 12, 'F');
        doc.setTextColor(255, 255, 255);
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(11);
        doc.text('ANÁLISIS DE VARIACIONES Y ESTIMACIONES', m, 8);
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(8);
        doc.text(PDF_JEJ_ORG + ' | ' + fs, W - m, 8, { align: 'right' });
        doc.setTextColor(0, 0, 0);

        var pctBajoBac = bac > 0 ? ((bac - eac) / bac) * 100 : 0;
        var vacPct = bac > 0 ? (Number(ind.vac) / bac) * 100 : 0;
        var etcModP2 = bac > 0 && etc < bac * 0.08 ? 'bajo' : 'moderado';
        var pctEtcEac = eac > 1e-6 ? (etc / eac) * 100 : 0;
        var tcpiP2 = tcpiFromInd(ind);
        var colV2 = 72;
        var colI2 = 118;

        y = 18;
        y = pdfSectionTitle(doc, 'ANÁLISIS DE VARIACIONES', m, y, maxW);
        doc.setFont('helvetica', 'italic');
        doc.setFontSize(8);
        doc.setTextColor(90, 96, 108);
        doc.text('Concepto / Valor / Interpretación', m, y);
        doc.setFont('helvetica', 'normal');
        doc.setTextColor(25, 35, 50);
        y += 4;
        y = pdfTable3ColHeaderCustom(doc, m, y, maxW, colV2, colI2, 'Variación', 'Valor', 'Interpretación');
        y = pdfTable3ColRow(doc, m, y, maxW, colV2, colI2, 'CV (Costo)', formatPdfMUsd(ind.cv), Number(ind.cv) >= 0 ? 'Bajo presupuesto' : 'Sobre presupuesto', false);
        y = pdfTable3ColRow(doc, m, y, maxW, colV2, colI2, 'SV (Cronograma)', formatPdfMUsd(ind.sv), Number(ind.sv) >= 0 ? 'Adelantado' : 'Atrasado', true);
        y = pdfTable3ColRow(doc, m, y, maxW, colV2, colI2, 'VAC (Final prevista)', formatPdfMUsd(ind.vac), Number(ind.vac) >= 0 ? 'Ahorro proyectado' : 'Sobrecosto proyectado', false);
        y += 2;
        y = pdfSectionTitle(doc, 'ESTIMACIONES FINANCIERAS', m, y, maxW);
        doc.setFont('helvetica', 'italic');
        doc.setFontSize(8);
        doc.setTextColor(90, 96, 108);
        doc.text('Estimación / Valor / Análisis', m, y);
        doc.setFont('helvetica', 'normal');
        doc.setTextColor(25, 35, 50);
        y += 5;
        var narrEacEj = eac <= bac
            ? ('Excelente: ' + pctBajoBac.toFixed(1) + '% bajo presupuesto. Gestión financiera sobresaliente.')
            : 'Atención: EAC supera el BAC; requiere plan de recuperación de costos.';
        y = pdfEstimacionBox(doc, 'EAC (Costo Estimado Total)', formatPdfMUsd(ind.eac) + ' -', narrEacEj, m, y, maxW);
        if (ieacRec != null) {
            var rangoPart = ieMin != null && ieMax != null ? 'IEAC Promedio: ' + formatPdfMUsd(ieacRec) + '. Rango: ' + formatPdfMUsd(ieMin) + ' - ' + formatPdfMUsd(ieMax) + '. ' : '';
            var pctIe = bac > 0 ? ((bac - ieacRec) / bac) * 100 : 0;
            var diffPct = eac > 0 ? (Math.abs(eac - ieacRec) / eac) * 100 : 0;
            var ieacBody = rangoPart + (ieacRec <= bac
                ? ('Excelente: ' + pctIe.toFixed(1) + '% bajo presupuesto original. Coincide con EAC tradicional (±' + diffPct.toFixed(1) + '%).')
                : 'Revisar dispersión de metodologías IEAC frente al escenario base.');
            y = pdfEstimacionBox(doc, 'IEAC (Estimación Independiente)', formatPdfMUsd(ieacRec) + ' -', ieacBody, m, y, maxW);
        }
        var narrEtcEj = etc < bac * 0.1
            ? ('Costo restante bajo: ' + formatPdfMUsd(ind.etc) + ' para completar ' + pctEtcEac.toFixed(1) + '% del proyecto.')
            : ('Costo restante moderado: ' + formatPdfMUsd(ind.etc) + ' para completar ' + pctEtcEac.toFixed(1) + '% del proyecto.');
        y = pdfEstimacionBox(doc, 'ETC (Costo para Completar)', formatPdfMUsd(ind.etc) + ' -', narrEtcEj, m, y, maxW);
        var narrVacEj = Number(ind.vac) >= 0
            ? ('Ahorro significativo: ' + vacPct.toFixed(1) + '% del presupuesto. Oportunidad estratégica.')
            : 'Variación final negativa: revisar cierre económico del proyecto.';
        y = pdfEstimacionBox(doc, 'VAC (Variación Final)', formatPdfMUsd(ind.vac) + ' -', narrVacEj, m, y, maxW);
        if (tcpiP2 != null && isFinite(tcpiP2)) {
            y = pdfEstimacionBox(doc, 'TCPI (Índice para completar)', tcpiP2.toFixed(3) + ' -', pdfTcpiNarrativa(tcpiP2), m, y, maxW);
        }
        y += 2;
        y = pdfSectionTitle(doc, 'ANÁLISIS DE LA CURVA S', m, y, maxW);
        y = pdfPara(doc, analisisCurvaSText(ind), m, y, maxW, 4.5);
        y += 2;
        y = pdfSectionTitle(doc, 'ANÁLISIS DE TENDENCIAS', m, y, maxW);
        y = pdfPara(doc, analisisTendenciasText(ind), m, y, maxW, 4.5);
        pdfFooterJej(doc, 'Reporte Ejecutivo EVM', 2, 4);

        doc.addPage('a4', 'l');
        var WL = doc.internal.pageSize.getWidth();
        var HL = doc.internal.pageSize.getHeight();
        doc.setFillColor(255, 255, 255);
        doc.rect(0, 0, WL, HL, 'F');
        doc.setFillColor(30, 58, 95);
        doc.rect(0, 0, WL, 14, 'F');
        doc.setTextColor(255, 255, 255);
        doc.setFontSize(11);
        doc.setFont('helvetica', 'bold');
        doc.text('ANÁLISIS DE LA CURVA S - GRÁFICO DE EVOLUCIÓN', m, 9);
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(8);
        doc.text(PDF_JEJ_ORG + ' | ' + fs, WL - m, 9, { align: 'right' });
        doc.setTextColor(0, 0, 0);

        function finishPage4() {
            doc.addPage('a4', 'p');
            doc.setFillColor(255, 255, 255);
            doc.rect(0, 0, W, H, 'F');
            doc.setFillColor(30, 58, 95);
            doc.rect(0, 0, W, 20, 'F');
            doc.setTextColor(255, 255, 255);
            doc.setFontSize(10);
            doc.setFont('helvetica', 'bold');
            doc.text('CONCLUSIONES Y RECOMENDACIONES ESTRATÉGICAS', m, 9);
            doc.setFont('helvetica', 'normal');
            doc.setFontSize(8);
            doc.text(PDF_JEJ_ORG + ' | ' + fs, m, 15);
            doc.setTextColor(0, 0, 0);
            var y4 = 26;
            doc.setFont('helvetica', 'bold');
            doc.setFontSize(10);
            doc.text('CONCLUSIÓN GENERAL', m, y4);
            y4 += 5;
            doc.setFont('helvetica', 'normal');
            doc.setFontSize(9);
            y4 = pdfPara(doc, pdfConclusionEjecutiva(ind), m, y4, maxW, 4.5);
            y4 += 4;
            doc.setFont('helvetica', 'bold');
            doc.text('RECOMENDACIONES ESTRATÉGICAS', m, y4);
            y4 += 5;
            doc.setFont('helvetica', 'normal');
            var recsRef = [
                '1. Monitorear si el adelanto y el ahorro se mantienen a medida que el proyecto avance.',
                '2. Revisar si el valor ganado refleja avances reales o si hay riesgos de retrasos futuros.',
                '3. Ajustar planes si surgen imprevistos para maximizar los beneficios del desempeño actual.',
                '4. Mantener las prácticas actuales que están generando resultados sobresalientes.',
                '5. Considerar la reasignación de recursos ahorrados a otras iniciativas estratégicas.',
                '6. Implementar un sistema de alertas tempranas para detectar cambios en las tendencias.'
            ];
            recsRef.forEach(function (r) { y4 = pdfPara(doc, r, m, y4, maxW, 4.4); });
            y4 += 4;
            var colMVal = 72;
            var colMEst = 118;
            y4 = pdfSectionTitle(doc, 'MÉTRICAS DETALLADAS COMPLETAS', m, y4, maxW);
            doc.setFont('helvetica', 'italic');
            doc.setFontSize(8);
            doc.setTextColor(90, 96, 108);
            doc.text('Métrica / Valor / Estado', m, y4);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(25, 35, 50);
            y4 += 4;
            y4 = pdfTable3ColHeaderCustom(doc, m, y4, maxW, colMVal, colMEst, 'Métrica', 'Valor', 'Estado');
            var rowsM = [
                ['Costo Real (AC)', formatPdfMUsd(ind.ac), 'Actual'],
                ['Valor Ganado (EV)', formatPdfMUsd(ind.ev), 'Completado'],
                ['Costo Planeado (PV)', formatPdfMUsd(ind.pv), 'Planificado'],
                ['Presupuesto Total (BAC)', formatPdfMUsd(ind.bac), 'Objetivo'],
                ['Variación Costo (CV)', formatPdfMUsd(ind.cv), Number(ind.cv) >= 0 ? 'Favorable' : 'Desfavorable'],
                ['Variación Cronograma (SV)', formatPdfMUsd(ind.sv), Number(ind.sv) >= 0 ? 'Favorable' : 'Desfavorable'],
                ['Variación Final (VAC)', formatPdfMUsd(ind.vac), Number(ind.vac) >= 0 ? 'Favorable' : 'Desfavorable'],
                ['EAC Proyectado', formatPdfMUsd(ind.eac), 'Estimación'],
                ['IEAC Independiente', ieacRec != null ? formatPdfMUsd(ieacRec) : '-', 'Estimación alternativa'],
                ['ETC', formatPdfMUsd(ind.etc), 'Para completar'],
                ['CPI', fmtEvmIdx(ind.cpi), Number(ind.cpi) >= 1 ? 'Excelente' : 'Revisar'],
                ['SPI', fmtEvmIdx(ind.spi), Number(ind.spi) >= 1 ? 'Adelantado' : 'Atrasado']
            ];
            var stripeM = false;
            rowsM.forEach(function (row) {
                y4 = pdfTable3ColRow(doc, m, y4, maxW, colMVal, colMEst, row[0], row[1], row[2], stripeM);
                stripeM = !stripeM;
            });
            pdfFooterJej(doc, 'Reporte Ejecutivo EVM', 4, 4);
            doc.save('reporte_ejecutivo_evm_directorio.pdf');
            showVfToast('PDF ejecutivo descargado.', 'success');
        }

        setTimeout(function () {
            var graphX = 18;
            var graphY = 22;
            var graphW = WL - 36;
            var graphH = HL - 42;
            var chartEl = $('vf-r1-chart');
            var placed = false;
            if (chartEl && chartEl.width > 0 && chartEl.height > 0) {
                try {
                    var img = chartEl.toDataURL('image/png', 1.0);
                    doc.addImage(img, 'PNG', graphX, graphY, graphW, graphH, undefined, 'FAST');
                    placed = true;
                } catch (e1) { placed = false; }
            }
            var ycap = graphY + graphH + 5;
            doc.setFontSize(9);
            doc.setFont('helvetica', 'normal');
            pdfPara(doc, 'El gráfico muestra la evolución del proyecto con las líneas de Valor Ganado (EV), Costo Real (AC) y Costo Planificado (PV).', graphX, ycap, graphW, 4.5);
            if (!placed && typeof html2canvas !== 'undefined') {
                var wrap = chartEl && chartEl.parentElement ? chartEl.parentElement : null;
                if (wrap) {
                    html2canvas(wrap, {
                        backgroundColor: '#ffffff',
                        scale: 2,
                        useCORS: true,
                        logging: false
                    }).then(function (cvs) {
                        try {
                            doc.addImage(cvs.toDataURL('image/png', 1.0), 'PNG', graphX, graphY, graphW, graphH, undefined, 'FAST');
                        } catch (e2) { /* noop */ }
                        var yH2c = graphY + graphH + 5;
                        doc.setFontSize(9);
                        doc.setFont('helvetica', 'normal');
                        pdfPara(doc, 'El gráfico muestra la evolución del proyecto con las líneas de Valor Ganado (EV), Costo Real (AC) y Costo Planificado (PV).', graphX, yH2c, graphW, 4.5);
                        pdfFooterJej(doc, 'Reporte Ejecutivo EVM', 3, 4);
                        finishPage4();
                    }).catch(function () {
                        pdfFooterJej(doc, 'Reporte Ejecutivo EVM', 3, 4);
                        finishPage4();
                    });
                    return;
                }
            }
            if (!placed) {
                doc.setFontSize(9);
                doc.text(pdfSafeText('Gráfico no disponible. Visualice la Curva S y vuelva a exportar.'), graphX, graphY + 30);
            }
            pdfFooterJej(doc, 'Reporte Ejecutivo EVM', 3, 4);
            finishPage4();
        }, 500);
    }

    function exportEvmPdf(isEjecutivo) {
        var panel = $('vf-r1-evm-panel');
        if (!panel || panel.classList.contains('hidden') || !state.reporte1Pack || !state.reporte1Pack.indicadores) {
            showVfToast('Genere primero el análisis EVM (fecha de seguimiento y Actualizar).', 'warning');
            return;
        }
        var JsPDF = getJsPdfCtor();
        if (typeof JsPDF !== 'function') {
            showVfToast('No se pudo cargar jsPDF. Recargue la página.', 'error');
            return;
        }
        if (isEjecutivo) generarPdfEjecutivoEvm(JsPDF);
        else generarPdfTecnicoEvm(JsPDF);
    }

    var EVM_IEAC_ROW_TIPS = {
        a: 'IEAC = AC + trabajo por ganar (BAC − EV). Asume completar el resto al costo unitario implícito del plan base.',
        b: 'IEAC = BAC ÷ CPI. Asume que el índice de desempeño de costo (CPI) se mantiene hasta el fin del proyecto.',
        c: 'IEAC = AC + (BAC − EV) ÷ CPI. Fórmula clásica PMI: el trabajo restante al ritmo de costo observado (CPI).',
        d: 'IEAC = AC + (BAC − EV) ÷ CPI de los últimos 3 meses. CPI reciente como tendencia de costo.',
        e: 'IEAC = AC + (BAC − EV) ÷ CPI de los últimos 6 meses. CPI semestral como tendencia.',
        f: 'IEAC = AC + (BAC − EV) ÷ (CPI × SPI). Considera simultáneamente presión de costo y de cronograma.',
        g: 'IEAC = AC + (BAC − EV) ÷ (CPI 3m × SPI). CPI corto plazo con SPI.',
        h: 'IEAC = AC + (BAC − EV) ÷ (CPI 6m × SPI). CPI medio plazo con SPI.',
        i: 'IEAC = AC + (BAC − EV) ÷ (0,7×CPI + 0,3×SPI). Combinación ponderada de desempeño de costo y plazo.'
    };

    var EVM_ECD_ROW_TIPS = {
        a: 'ECD: duración planeada total (meses) ÷ SPI, convertida a fecha desde el primer periodo API. Refleja el retraso o adelanto global del cronograma.',
        b: 'ECD: meses transcurridos con control a la fecha + (BAC − EV) ÷ PV del último mes. Meses adicionales al ritmo del último devengo planificado.',
        c: 'ECD: plazo con control + (BAC − EV) ÷ PV promedio últimos 3 meses.',
        d: 'ECD: plazo con control + (BAC − EV) ÷ PV promedio últimos 6 meses.',
        e: 'ECD: plazo con control + (BAC − EV) ÷ PV promedio últimos 12 meses.',
        f: 'ECD: plazo con control + (BAC − EV) ÷ PV (3m) variante alternativa de escalación de plazo.',
        g: 'ECD: plazo con control + trabajo por ganar ÷ (BAC × tasa EV física 3m). Usa avance físico reciente.',
        h: 'ECD: plazo con control + trabajo por ganar ÷ (BAC × tasa EV física 6m).',
        i: 'ECD: plazo con control + trabajo por ganar ÷ (BAC × tasa EV física 12m).',
        j: 'ECD: duración planeada total + (BAC − EV) ÷ PV (6m). Combina horizonte total y ritmo reciente.',
        k: 'ECD: plazo con control + (BAC − EV) ÷ PV último mes (variante alternativa).'
    };

    /** Notificación en página (sustituye alert del navegador). variant: success | error | warning | info */
    function showVfToast(text, variant) {
        if (!text) return;
        variant = variant || 'info';
        var host = document.getElementById('vf-toast-host');
        if (!host) {
            host = document.createElement('div');
            host.id = 'vf-toast-host';
            host.className = 'fixed bottom-6 right-6 z-[250] flex flex-col gap-2 pointer-events-none max-w-md w-[min(24rem,calc(100vw-2rem))]';
            host.setAttribute('aria-live', 'polite');
            document.body.appendChild(host);
        }
        var palettes = {
            success: { box: 'border-emerald-300/90 bg-emerald-50 dark:border-emerald-700 dark:bg-emerald-950/95', icon: 'text-emerald-600 dark:text-emerald-400', name: 'check_circle' },
            error: { box: 'border-red-300/90 bg-red-50 dark:border-red-900/70 dark:bg-red-950/95', icon: 'text-red-600 dark:text-red-400', name: 'error_outline' },
            warning: { box: 'border-amber-300/90 bg-amber-50 dark:border-amber-900/60 dark:bg-amber-950/95', icon: 'text-amber-600 dark:text-amber-400', name: 'warning_amber' },
            info: { box: 'border-slate-300/90 bg-white dark:border-slate-600 dark:bg-slate-800', icon: 'text-slate-600 dark:text-slate-400', name: 'info' }
        };
        var p = palettes[variant] || palettes.info;
        var wrap = document.createElement('div');
        wrap.className = 'pointer-events-auto rounded-xl border shadow-lg px-4 py-3 flex gap-3 items-start ' + p.box;
        wrap.setAttribute('role', 'status');
        var ic = document.createElement('span');
        ic.className = 'material-icons shrink-0 text-xl leading-none mt-0.5 ' + p.icon;
        ic.setAttribute('aria-hidden', 'true');
        ic.textContent = p.name;
        var msg = document.createElement('p');
        msg.className = 'text-sm leading-snug flex-1 text-slate-800 dark:text-slate-100';
        msg.textContent = text;
        var closeBtn = document.createElement('button');
        closeBtn.type = 'button';
        closeBtn.className = 'shrink-0 -mr-1 -mt-0.5 p-1 rounded-md text-slate-500 hover:bg-black/5 dark:hover:bg-white/10';
        closeBtn.setAttribute('aria-label', 'Cerrar aviso');
        var closeIc = document.createElement('span');
        closeIc.className = 'material-icons text-lg';
        closeIc.textContent = 'close';
        closeBtn.appendChild(closeIc);
        function removeToast() {
            if (wrap.parentNode) wrap.parentNode.removeChild(wrap);
        }
        closeBtn.addEventListener('click', removeToast);
        wrap.appendChild(ic);
        wrap.appendChild(msg);
        wrap.appendChild(closeBtn);
        host.appendChild(wrap);
        setTimeout(removeToast, variant === 'error' ? 9000 : 7000);
    }

    function mensajeImportacionExitosa(inserted) {
        var n = typeof inserted === 'number' ? inserted : 0;
        if (n === 0) {
            return 'El archivo se procesó, pero no se incorporaron registros nuevos. Revise el formato y el contenido.';
        }
        var fmt = n.toLocaleString('es-CL');
        return n === 1
            ? 'Importación finalizada correctamente. Se incorporó 1 registro al proyecto.'
            : 'Importación finalizada correctamente. Se incorporaron ' + fmt + ' registros al proyecto.';
    }

    function renderTable(rows) {
        var tbody = $('vf-tbody');
        var thead = $('vf-thead');
        if (!tbody || !thead) return;
        var vista = state.vista;
        var filtered = rows;
        if (vista === 'analisis' && state.modoAnalisisId === 'reporte9') {
            filtered = filtered.filter(function (r) {
                var d = sapDesc(r);
                if (state.sapDescripcionFilter && d !== state.sapDescripcionFilter) return false;
                return true;
            });
            filtered = filterByPeriod(filtered, $('vf-desde').value, $('vf-hasta').value);
        } else {
            filtered = filterByPeriod(filtered, $('vf-desde').value, $('vf-hasta').value);
        }
        state.rows = filtered;
        var start = (state.page - 1) * state.pageSize;
        var rowCount = filtered.length;
        var pageRows = filtered.slice(start, start + state.pageSize);

        if (vista === 'vector') {
            thead.innerHTML = '<tr class="text-left text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400 border-b border-slate-200 dark:border-slate-600 bg-slate-50 dark:bg-slate-800/70">' +
                '<th class="py-2 pr-2">Período</th><th class="py-2 pr-2">Tipo</th>' +
                '<th class="py-2 pr-2">Cat VP</th><th class="py-2 pr-2">Detalle factorial</th><th class="py-2 pr-2 text-right">Monto</th></tr>';
            tbody.innerHTML = pageRows.map(function (r) {
                return '<tr class="border-b border-slate-100 dark:border-slate-700 text-sm odd:bg-white even:bg-slate-50/60 dark:odd:bg-slate-900 dark:even:bg-slate-800/50 hover:bg-amber-50/70 dark:hover:bg-amber-900/20 transition-colors">' +
                    '<td class="py-2 pr-2">' + escapeHtml(r.periodo || r.Periodo || '') + '</td>' +
                    '<td class="py-2 pr-2">' + escapeHtml(r.tipo || r.Tipo || '') + '</td>' +
                    '<td class="py-2 pr-2 font-medium text-slate-700 dark:text-slate-200">' + escapeHtml(r.cat_vp || r.catVp || r.CatVp || '') + '</td>' +
                    '<td class="py-2 pr-2 max-w-[200px] truncate" title="' + escapeHtml(r.detalle_factorial || r.detalleFactorial || r.DetalleFactorial || '') + '">' +
                    escapeHtml(r.detalle_factorial || r.detalleFactorial || r.DetalleFactorial || '') + '</td>' +
                    '<td class="py-2 pr-2 text-right font-semibold tabular-nums">' + formatUsd(parseFloat(r.monto != null ? r.monto : r.Monto) || 0) + '</td></tr>';
            }).join('');
        } else if (vista === 'analisis' && state.modoAnalisisId === 'reporte9') {
            thead.innerHTML = '<tr class="text-left text-xs uppercase text-slate-500 dark:text-slate-400 border-b border-slate-200 dark:border-slate-600">' +
                '<th class="py-2 pr-2">ID SAP</th><th class="py-2 pr-2">Versión</th><th class="py-2 pr-2">Descripción</th><th class="py-2 pr-2">Período</th>' +
                '<th class="py-2 pr-2 text-right">MO</th><th class="py-2 pr-2 text-right">IC</th><th class="py-2 pr-2 text-right">EM</th><th class="py-2 pr-2 text-right">IE</th>' +
                '<th class="py-2 pr-2 text-right">SC</th><th class="py-2 pr-2 text-right">AD</th><th class="py-2 pr-2 text-right">CL</th><th class="py-2 pr-2 text-right">CT</th></tr>';
            tbody.innerHTML = pageRows.map(function (r) {
                return '<tr class="border-b border-slate-100 dark:border-slate-700 text-sm">' +
                    '<td class="py-2 pr-2">' + escapeHtml(r.id_sap || r.idSap || r.IdSap || '') + '</td>' +
                    '<td class="py-2 pr-2">' + escapeHtml(r.version_sap || r.versionSap || '') + '</td>' +
                    '<td class="py-2 pr-2 max-w-[180px] truncate">' + escapeHtml(r.descripcion || r.Descripcion || '') + '</td>' +
                    '<td class="py-2 pr-2">' + escapeHtml(r.periodo || r.Periodo || '') + '</td>' +
                    ['MO', 'IC', 'EM', 'IE', 'SC', 'AD', 'CL', 'CT'].map(function (k) {
                        var v = parseFloat(r[k] != null ? r[k] : r[k.toLowerCase()]) || 0;
                        return '<td class="py-2 pr-2 text-right">' + formatUsd(v) + '</td>';
                    }).join('') + '</tr>';
            }).join('');
        } else if (vista === 'analisis' && state.modoAnalisisId === 'av_fisico_c9') {
            thead.innerHTML = '<tr class="text-left text-xs uppercase text-slate-500 dark:text-slate-400 border-b border-slate-200 dark:border-slate-600">' +
                '<th class="py-2 pr-2">ID C9</th><th class="py-2 pr-2">Período</th><th class="py-2 pr-2">Cat VP</th><th class="py-2 pr-2 text-right">Moneda Base</th>' +
                '<th class="py-2 pr-2 text-right">Base</th><th class="py-2 pr-2 text-right">Cambio</th><th class="py-2 pr-2 text-right">Control</th><th class="py-2 pr-2 text-right">Tendencia</th>' +
                '<th class="py-2 pr-2 text-right">EAT</th><th class="py-2 pr-2 text-right">Compromiso</th><th class="py-2 pr-2 text-right">Incurrido</th><th class="py-2 pr-2 text-right">Financiero</th><th class="py-2 pr-2 text-right">Por comprometer</th></tr>';
            var sorted = filtered.slice().sort(function (a, b) {
                var pa = rowPeriodo(a) || '';
                var pb = rowPeriodo(b) || '';
                if (pa !== pb) return pa.localeCompare(pb);
                var ca = normalizeText(a.cat_vp || a.catVp || '');
                var cb = normalizeText(b.cat_vp || b.catVp || '');
                return (order9c[ca] || 99) - (order9c[cb] || 99);
            });
            rowCount = sorted.length;
            pageRows = sorted.slice(start, start + state.pageSize);
            tbody.innerHTML = pageRows.map(function (r) {
                function num(k) { return formatUsd(parseFloat(r[k] != null ? r[k] : r[k.charAt(0).toUpperCase() + k.slice(1)]) || 0); }
                return '<tr class="border-b border-slate-100 dark:border-slate-700 text-sm">' +
                    '<td class="py-2 pr-2">' + escapeHtml(r.id_c9 || r.idC9 || '') + '</td>' +
                    '<td class="py-2 pr-2">' + escapeHtml(r.periodo || r.Periodo || '') + '</td>' +
                    '<td class="py-2 pr-2">' + escapeHtml(r.cat_vp || r.catVp || '') + '</td>' +
                    '<td class="py-2 pr-2 text-right">' + escapeHtml(String(r.moneda_base != null ? r.moneda_base : (r.monedaBase != null ? r.monedaBase : ''))) + '</td>' +
                    '<td class="py-2 pr-2 text-right">' + num('base') + '</td>' +
                    '<td class="py-2 pr-2 text-right">' + num('cambio') + '</td>' +
                    '<td class="py-2 pr-2 text-right">' + num('control') + '</td>' +
                    '<td class="py-2 pr-2 text-right">' + num('tendencia') + '</td>' +
                    '<td class="py-2 pr-2 text-right">' + num('eat') + '</td>' +
                    '<td class="py-2 pr-2 text-right">' + num('compromiso') + '</td>' +
                    '<td class="py-2 pr-2 text-right">' + num('incurrido') + '</td>' +
                    '<td class="py-2 pr-2 text-right">' + num('financiero') + '</td>' +
                    '<td class="py-2 pr-2 text-right">' + num('por_comprometer') + '</td></tr>';
            }).join('');
        } else {
            tbody.innerHTML = '';
        }

        var totalP = Math.max(1, Math.ceil(rowCount / state.pageSize));
        var pg = $('vf-pag');
        if (pg) pg.textContent = 'Página ' + state.page + ' / ' + totalP + ' · ' + rowCount + ' filas';
    }

    function previousMonthIso() {
        var now = new Date();
        var y = now.getFullYear();
        var m = now.getMonth(); // 0..11, mes actual
        if (m === 0) {
            y -= 1;
            m = 12;
        }
        return String(y) + '-' + String(m).padStart(2, '0');
    }

    function ensureDefaultReporte1Month(force) {
        var fsEl = $('vf-r1-fecha-seg');
        if (fsEl && (force || !fsEl.value)) fsEl.value = previousMonthIso();
    }

    function setKpiVisible(visible) {
        var el = $('vf-kpi');
        if (!el) return;
        el.classList.toggle('hidden', !visible);
    }

    /** Rejilla vf-thead/tbody: visible en modos vector y análisis distintos de reporte1; oculta solo en «Curva S - Parcial / Acum». */
    function updateImportedGridVisibility() {
        var wrap = $('vf-imported-grid-wrap');
        if (!wrap) return;
        var hide = state.vista === 'analisis' && state.modoAnalisisId === 'reporte1';
        wrap.classList.toggle('hidden', hide);
    }

    function fetchDatos() {
        var pid = state.proyectoId;
        if (!pid) return;
        if (state.vista === 'analisis' && state.modoAnalisisId === 'reporte1') {
            setKpiVisible(false);
            $('vf-kpi').innerHTML = '<p class="text-sm text-slate-500 dark:text-slate-400">Curva S / EVM: datos desde el paquete consolidado del servidor (no hay tabla <code class="text-xs bg-slate-100 dark:bg-slate-800 px-1 rounded">reporte1</code> en base de datos).</p>';
            ensureDefaultReporte1Month();
            var fsR1 = $('vf-r1-fecha-seg');
            if (fsR1 && fsR1.value) loadCurvaS();
            return;
        }
        setKpiVisible(true);
        var tabla = state.tablaActual;
        var u = pageBase + '?handler=DatosFinancieros&proyectoId=' + encodeURIComponent(pid) +
            '&tabla=' + encodeURIComponent(tabla);
        var d0 = $('vf-desde').value;
        var d1 = $('vf-hasta').value;
        if (d0) u += '&desde=' + encodeURIComponent(d0 + '-01');
        if (d1) u += '&hasta=' + encodeURIComponent(d1 + '-01');
        $('vf-load').classList.remove('hidden');
        fetch(u, { headers: { Accept: 'application/json' } })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
            .then(function (x) {
                $('vf-load').classList.add('hidden');
                if (!x.ok) throw new Error(x.j && x.j.error ? x.j.error : 'Error al cargar datos');
                var rows = Array.isArray(x.j) ? x.j : [];
                state.allRows = rows;
                state.page = 1;
                if (state.vista === 'vector') {
                    renderKpi(rows, state.modoVectorId);
                } else {
                    if (state.modoAnalisisId === 'reporte9') renderSapKpiAndManager(rows);
                    else {
                        $('vf-kpi').innerHTML = '<p class="text-sm text-slate-500">KPI categorías SAP: use códigos MO–CT. Vista 9C: sumas en panel resumen.</p>';
                        if (state.modoAnalisisId === 'av_fisico_c9') renderKpi9c(rows);
                    }
                }
                renderTable(rows);
                if (state.vista === 'analisis' && state.modoAnalisisId === 'reporte1') loadCurvaS();
            })
            .catch(function (e) {
                $('vf-load').classList.add('hidden');
                showVfToast('No fue posible actualizar la información financiera. ' + (e.message || String(e)), 'error');
            });
    }

    function renderKpi9c(rows) {
        var keys = ['base', 'cambio', 'control', 'tendencia', 'eat', 'compromiso', 'incurrido', 'financiero', 'por_comprometer'];
        var sums = {};
        keys.forEach(function (k) { sums[k] = 0; });
        rows.forEach(function (r) {
            keys.forEach(function (k) {
                var v = parseFloat(r[k] != null ? r[k] : r[k.charAt(0).toUpperCase() + k.slice(1)]) || 0;
                sums[k] += v;
            });
        });
        var el = $('vf-kpi');
        if (!el) return;
        var html = '<div class="grid grid-cols-3 sm:grid-cols-5 gap-2 text-xs">';
        keys.forEach(function (k) {
            html += '<div class="rounded border border-slate-200 dark:border-slate-600 p-2"><div class="text-slate-500 uppercase">' + k.replace(/_/g, ' ') + '</div><div class="font-semibold">' + formatUsd(sums[k]) + '</div></div>';
        });
        html += '</div>';
        el.innerHTML = html;
    }

    function tryRegisterChartAnnotation() {
        try {
            if (typeof Chart === 'undefined') return;
            var plug = window['chartjs-plugin-annotation'];
            if (!plug) return;
            var p = plug.default !== undefined ? plug.default : plug;
            if (typeof Chart.register === 'function' && p) Chart.register(p);
        } catch (ignore) { }
    }

    function destroyReporte1Charts() {
        if (state.r1MainChart) { state.r1MainChart.destroy(); state.r1MainChart = null; }
        if (state.r1CascadeV0) { state.r1CascadeV0.destroy(); state.r1CascadeV0 = null; }
        if (state.r1CascadeApi) { state.r1CascadeApi.destroy(); state.r1CascadeApi = null; }
    }

    function msUsd(n) {
        return (Number(n) || 0) / 1e6;
    }

    function renderReporte1MainChart() {
        var pack = state.reporte1Pack;
        if (!pack || !pack.curva || !pack.curva.length || typeof Chart === 'undefined') return;
        if (state.r1MainChart) { state.r1MainChart.destroy(); state.r1MainChart = null; }
        var curva = pack.curva;
        var labels = curva.map(function (c) { return c.periodo; });
        var modo = (document.querySelector('input[name="vf-r1-modo-graf"]:checked') || {}).value || 'normal';
        var ind = pack.indicadores || {};
        var fechaSeg = (ind.fechaSeguimiento || '').substring(0, 10);
        var idxSeg = labels.indexOf(fechaSeg);
        var bac = Number(pack.bacTotalProyecto != null ? pack.bacTotalProyecto : ind.bac) || 0;
        var cpi = Number(ind.cpi) || 1;
        var eac = cpi > 0 ? bac / cpi : bac;
        var datasets = [
            { label: modo === 'evm' ? 'Costo Real (AC)' : 'Real (acum.)', data: curva.map(function (c) { return msUsd(c.realAcum); }), borderColor: '#1ecb4f', backgroundColor: 'rgba(30,203,79,0.08)', tension: 0.15, fill: false, pointRadius: 0 },
            { label: modo === 'evm' ? 'Costo Planeado (PV) — API' : 'API (acum.)', data: curva.map(function (c) { return msUsd(c.apiAcum); }), borderColor: '#0177FF', backgroundColor: 'rgba(1,119,255,0.06)', tension: 0.15, fill: false, pointRadius: 0 },
            { label: modo === 'evm' ? 'Escenario NPC' : 'NPC (acum.)', data: curva.map(function (c) { return msUsd(c.npcAcum); }), borderColor: '#FFD000', backgroundColor: 'rgba(255,208,0,0.08)', tension: 0.15, fill: false, pointRadius: 0 },
            { label: modo === 'evm' ? 'Escenario V0' : 'V0 (acum.)', data: curva.map(function (c) { return msUsd(c.v0Acum); }), borderColor: '#16355D', backgroundColor: 'rgba(22,53,93,0.06)', tension: 0.15, fill: false, pointRadius: 0 }
        ];
        if (modo === 'evm') {
            var eacData = labels.map(function (p, i) {
                if (idxSeg < 0) return null;
                return i >= idxSeg ? msUsd(eac) : null;
            });
            datasets.push({ label: 'EAC proyectado', data: eacData, borderColor: '#e53935', borderDash: [5, 4], tension: 0, fill: false, pointRadius: 0 });
            var bacLine = labels.map(function () { return msUsd(bac); });
            datasets.push({ label: 'BAC', data: bacLine, borderColor: '#424242', borderDash: [2, 3], tension: 0, fill: false, pointRadius: 0 });
        }
        var ann = {};
        if (modo === 'evm' && fechaSeg && idxSeg >= 0) {
            ann.vline = { type: 'line', scaleID: 'x', value: fechaSeg, borderColor: 'rgba(229,57,53,0.9)', borderWidth: 2, borderDash: [6, 4], label: { display: true, content: 'Seguimiento', position: 'start' } };
            if (ind.ac != null) ann.hAc = { type: 'line', scaleID: 'y', yMin: msUsd(ind.ac), yMax: msUsd(ind.ac), borderColor: 'rgba(251,140,0,0.85)', borderWidth: 1, borderDash: [4, 3] };
            if (ind.pv != null) ann.hPv = { type: 'line', scaleID: 'y', yMin: msUsd(ind.pv), yMax: msUsd(ind.pv), borderColor: 'rgba(30,203,79,0.75)', borderWidth: 1, borderDash: [4, 3] };
            if (ind.ev != null) ann.hEv = { type: 'line', scaleID: 'y', yMin: msUsd(ind.ev), yMax: msUsd(ind.ev), borderColor: 'rgba(1,119,255,0.85)', borderWidth: 1, borderDash: [4, 3] };
        }
        var ctx = $('vf-r1-chart');
        if (!ctx) return;
        var dark = document.documentElement.classList.contains('dark');
        var opts = {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: { labels: { color: dark ? '#e2e8f0' : '#334155' } },
                tooltip: {
                    callbacks: {
                        label: function (x) {
                            var v = x.parsed.y;
                            if (v == null || isNaN(v)) return x.dataset.label + ': —';
                            return x.dataset.label + ': USD ' + (v * 1e6).toLocaleString('es-CL', { maximumFractionDigits: 0 });
                        },
                        afterBody: function (items) {
                            if (modo !== 'evm' || !pack.avanceFisico || !items.length) return '';
                            var fs = items[0].label;
                            if (fechaSeg && fs !== fechaSeg) return '';
                            var af = pack.avanceFisico;
                            return ['', 'Avance físico (%)', '  REAL: ' + af.realPct + '%', '  API: ' + af.apiPct + '%', '  DESV.: ' + af.desviacionPct + '%'];
                        }
                    }
                }
            },
            scales: {
                x: {
                    ticks: {
                        color: dark ? '#94a3b8' : '#64748b',
                        maxRotation: 50,
                        autoSkip: true,
                        maxTicksLimit: 22
                    }
                },
                y: { ticks: { color: dark ? '#94a3b8' : '#64748b' }, title: { display: true, text: 'Millones USD', color: dark ? '#94a3b8' : '#64748b' } }
            }
        };
        if (Object.keys(ann).length) opts.plugins.annotation = { annotations: ann };
        try {
            state.r1MainChart = new Chart(ctx, { type: 'line', data: { labels: labels, datasets: datasets }, options: opts });
        } catch (err) {
            showVfToast('No se pudo dibujar el gráfico (Chart.js / plugin de anotaciones). ' + (err.message || ''), 'error');
        }
    }

    function renderReporte1Cascada(canvasId, cascada) {
        var el = $(canvasId);
        if (!el || typeof Chart === 'undefined' || !cascada || !cascada.barras) return;
        if (canvasId === 'vf-r1-cascade-v0' && state.r1CascadeV0) { state.r1CascadeV0.destroy(); state.r1CascadeV0 = null; }
        if (canvasId === 'vf-r1-cascade-api' && state.r1CascadeApi) { state.r1CascadeApi.destroy(); state.r1CascadeApi = null; }
        var labels = cascada.barras.map(function (b) { return b.etiqueta; });
        var lows = cascada.barras.map(function (b) {
            var base = Number(b.baseMs) || 0;
            var d = Number(b.deltaMs) || 0;
            return Math.min(base, base + d);
        });
        var highs = cascada.barras.map(function (b) {
            var base = Number(b.baseMs) || 0;
            var d = Number(b.deltaMs) || 0;
            return Math.max(base, base + d);
        });
        var colors = cascada.barras.map(function (b) {
            if (b.tipo === 'total') return '#43a047';
            if (b.tipo === 'inicio') return '#4a90e2';
            return b.colorDelta || '#4a90e2';
        });
        var valuesForLabel = cascada.barras.map(function (b) { return Number(b.deltaMs) || 0; });
        var chart = new Chart(el, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'M$USD',
                    data: highs.map(function (h, i) { return [lows[i], h]; }),
                    backgroundColor: colors,
                    borderWidth: 1,
                    borderColor: colors.map(function (c) { return c; })
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                return 'M$USD: ' + (valuesForLabel[ctx.dataIndex] || 0).toLocaleString('es-CL');
                            }
                        }
                    }
                },
                scales: {
                    x: { ticks: { maxRotation: 45, minRotation: 30, font: { size: 9 } } },
                    y: { beginAtZero: true, title: { display: true, text: 'M$USD' } }
                }
            },
            plugins: [{
                id: 'vfCascadeLabels',
                afterDatasetsDraw: function (chartObj) {
                    var ctx = chartObj.ctx;
                    ctx.save();
                    ctx.font = '11px sans-serif';
                    ctx.textAlign = 'center';
                    ctx.fillStyle = '#334155';
                    var yScale = chartObj.scales.y;
                    var meta = chartObj.getDatasetMeta(0);
                    meta.data.forEach(function (barEl, i) {
                        var v = valuesForLabel[i] || 0;
                        if (!v) return;
                        var y = v >= 0 ? yScale.getPixelForValue(highs[i]) - 6 : yScale.getPixelForValue(lows[i]) + 14;
                        ctx.fillText(String(v), barEl.x, y);
                    });
                    ctx.restore();
                }
            }]
        });
        if (canvasId === 'vf-r1-cascade-v0') state.r1CascadeV0 = chart;
        else state.r1CascadeApi = chart;
    }

    function renderReporte1Tabla(pack) {
        var thead = $('vf-r1-tabla-head');
        var tbody = $('vf-r1-tabla-body');
        if (!thead || !tbody) return;
        thead.innerHTML = '<tr class="bg-slate-800 text-white text-left">' +
            '<th class="p-2">Categoría VP</th><th class="p-2 text-right">Real USD (A)</th><th class="p-2 text-right">V0 USD (B)</th><th class="p-2 text-right">NPC USD (C)</th><th class="p-2 text-right">API USD (D)</th>' +
            '<th class="p-2 text-right border-l-2 border-red-500">Cascada V0 (A-B)</th><th class="p-2 text-right">Cascada API (A-D)</th></tr>';
        var rows = pack.tablaCategorias || [];
        function cellCas(v, tipo, categoria) {
            var n = Number(v) || 0;
            var cls = n > 0 ? 'text-emerald-700 bg-emerald-50 dark:bg-emerald-950/40' : (n < 0 ? 'text-red-600' : 'text-slate-500');
            if (n > 0) {
                return '<td class="p-2 text-right border-l-2 border-red-500 ' + cls + '">' +
                    '<button type="button" class="vf-cascada-open inline-flex items-center gap-1 hover:underline" data-categoria="' + escapeAttr(categoria) + '" data-tipo="' + escapeAttr(tipo) + '" data-monto="' + n + '">' +
                    formatUsd(n) + ' <span class="material-icons text-[14px] text-sky-600">search</span></button></td>';
            }
            return '<td class="p-2 text-right border-l-2 border-red-500 ' + cls + '">' + formatUsd(n) + '</td>';
        }
        tbody.innerHTML = rows.map(function (r) {
            return '<tr class="border-t border-slate-200 dark:border-slate-600">' +
                '<td class="p-2 font-medium">' + escapeHtml(r.categoria) + '</td>' +
                '<td class="p-2 text-right">' + formatUsd(r.realUsd) + '</td>' +
                '<td class="p-2 text-right">' + formatUsd(r.v0Usd) + '</td>' +
                '<td class="p-2 text-right">' + formatUsd(r.npcUsd) + '</td>' +
                '<td class="p-2 text-right">' + formatUsd(r.apiUsd) + '</td>' +
                cellCas(r.cascadaV0, 'V0', r.categoria) +
                cellCas(r.cascadaApi, 'API', r.categoria) + '</tr>';
        }).join('');
        var t = pack.tablaTotales || {};
        tbody.innerHTML += '<tr class="border-t-2 border-slate-400 font-semibold bg-slate-100 dark:bg-slate-800">' +
            '<td class="p-2">TOTAL PARCIAL</td>' +
            '<td class="p-2 text-right">' + formatUsd(t.totalReal) + '</td>' +
            '<td class="p-2 text-right">' + formatUsd(t.totalV0) + '</td>' +
            '<td class="p-2 text-right">' + formatUsd(t.totalNpc) + '</td>' +
            '<td class="p-2 text-right">' + formatUsd(t.totalApi) + '</td>' +
            '<td class="p-2 text-right border-l-2 border-red-500">—</td><td class="p-2 text-right">—</td></tr>';
        tbody.innerHTML += '<tr class="border-t border-slate-200 dark:bg-slate-50 dark:bg-slate-900/50 text-slate-600">' +
            '<td class="p-2">% avance parcial</td>' +
            '<td class="p-2 text-right">' + (t.pctReal != null ? Number(t.pctReal).toFixed(2) + '%' : '—') + '</td>' +
            '<td class="p-2 text-right">' + (t.pctV0 != null ? Number(t.pctV0).toFixed(2) + '%' : '—') + '</td>' +
            '<td class="p-2 text-right">' + (t.pctNpc != null ? Number(t.pctNpc).toFixed(2) + '%' : '—') + '</td>' +
            '<td class="p-2 text-right">' + (t.pctApi != null ? Number(t.pctApi).toFixed(2) + '%' : '—') + '</td>' +
            '<td class="p-2 text-right border-l-2 border-red-500">—</td><td class="p-2 text-right">—</td></tr>';
    }

    function openCascadaModalBase(subtitle) {
        $('vf-cascada-sub').textContent = subtitle || '';
        $('vf-cascada-kpi-base').textContent = '—';
        $('vf-cascada-kpi-real').textContent = '—';
        $('vf-cascada-kpi-dif').textContent = '—';
        $('vf-cascada-kpi-pct').textContent = '—';
        $('vf-cascada-conclusiones').textContent = 'Cargando datos...';
        $('vf-cascada-tbody').innerHTML = '<tr><td colspan="5" class="p-3 text-slate-500">Cargando datos...</td></tr>';
        $('vf-modal-cascada').classList.remove('hidden');
    }

    function closeCascadaModal() {
        var m = $('vf-modal-cascada');
        if (m) m.classList.add('hidden');
    }

    function openCascadaDetalle(categoria, tipo, monto) {
        var pid = state.proyectoId;
        if (!pid) return;
        openCascadaModalBase((categoria || '') + ' - ' + (tipo || '') + ' (' + formatUsd(monto || 0) + ')');
        var d0 = $('vf-desde') && $('vf-desde').value ? $('vf-desde').value + '-01' : '';
        var d1 = $('vf-hasta') && $('vf-hasta').value ? $('vf-hasta').value + '-01' : '';
        var u = pageBase + '?handler=Reporte1CascadaDetalle&proyectoId=' + encodeURIComponent(pid) +
            '&categoria=' + encodeURIComponent(categoria || '') +
            '&tipo=' + encodeURIComponent(tipo || '') +
            '&monto=' + encodeURIComponent(String(monto || 0));
        if (d0) u += '&desde=' + encodeURIComponent(d0);
        if (d1) u += '&hasta=' + encodeURIComponent(d1);
        fetch(u, { headers: { Accept: 'application/json' } })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
            .then(function (x) {
                if (!x.ok) throw new Error(x.j && x.j.error ? x.j.error : 'No fue posible cargar análisis detallado.');
                var d = x.j || {};
                $('vf-cascada-kpi-base').textContent = formatUsd(d.totalBaseUsd || 0);
                $('vf-cascada-kpi-real').textContent = formatUsd(d.totalRealUsd || 0);
                var difTotal = Number(d.diferenciaTotalUsd || 0);
                $('vf-cascada-kpi-dif').textContent = formatUsd(difTotal);
                var pctText = d.diferenciaTotalPct != null ? Number(d.diferenciaTotalPct).toFixed(2) + '%' : '—';
                $('vf-cascada-kpi-pct').textContent = pctText;
                var difKpi = $('vf-cascada-kpi-dif');
                var pctKpi = $('vf-cascada-kpi-pct');
                if (difKpi) {
                    difKpi.classList.remove('text-emerald-700', 'text-red-600', 'text-slate-800', 'dark:text-slate-100');
                    difKpi.classList.add(difTotal > 0 ? 'text-emerald-700' : (difTotal < 0 ? 'text-red-600' : 'text-slate-800'));
                }
                if (pctKpi) {
                    pctKpi.classList.remove('text-emerald-700', 'text-red-600', 'text-slate-800', 'dark:text-slate-100');
                    pctKpi.classList.add(difTotal > 0 ? 'text-emerald-700' : (difTotal < 0 ? 'text-red-600' : 'text-slate-800'));
                }
                $('vf-cascada-conclusiones').textContent = d.conclusiones || 'Sin conclusiones automáticas.';
                var filas = d.filas || [];
                if (!filas.length) {
                    $('vf-cascada-tbody').innerHTML = '<tr><td colspan="5" class="p-3 text-slate-500">Sin datos para el filtro seleccionado.</td></tr>';
                    return;
                }
                $('vf-cascada-tbody').innerHTML = filas.map(function (f) {
                    var dif = Number(f.diferenciaUsd || 0);
                    var cls = dif > 0 ? 'text-emerald-700' : (dif < 0 ? 'text-red-600' : 'text-slate-500');
                    return '<tr class="border-t border-slate-200 dark:border-slate-600 odd:bg-white even:bg-slate-50/70 dark:odd:bg-slate-900 dark:even:bg-slate-800/60">' +
                        '<td class="p-2">' + escapeHtml(f.periodo || '') + '</td>' +
                        '<td class="p-2 text-right">' + formatUsd(f.baseUsd || 0) + '</td>' +
                        '<td class="p-2 text-right">' + formatUsd(f.realUsd || 0) + '</td>' +
                        '<td class="p-2 text-right ' + cls + '">' + formatUsd(dif) + '</td>' +
                        '<td class="p-2 text-right ' + cls + '">' + (f.diferenciaPct != null ? Number(f.diferenciaPct).toFixed(2) + '%' : '—') + '</td>' +
                        '</tr>';
                }).join('');
            })
            .catch(function (e) {
                $('vf-cascada-conclusiones').textContent = e.message || String(e);
                $('vf-cascada-tbody').innerHTML = '<tr><td colspan="5" class="p-3 text-red-600">No fue posible cargar el análisis detallado.</td></tr>';
            });
    }

    function renderReporte1EvmPanel(pack) {
        var panel = $('vf-r1-evm-panel');
        var sub = $('vf-r1-evm-sub');
        var ieacBox = $('vf-r1-ieac');
        var ecdEl = $('vf-r1-ecd');
        var kpiGrid = $('vf-r1-evm-kpi');
        if (!panel || !kpiGrid) return;
        var ind = pack.indicadores;
        if (!ind) {
            panel.classList.add('hidden');
            return;
        }
        var sinFiltro = !($('vf-desde') && $('vf-desde').value) && !($('vf-hasta') && $('vf-hasta').value);
        var subParts = ['Fecha de seguimiento: ' + (ind.fechaSeguimiento || '')];
        if (sinFiltro) subParts.push('Sin filtros Desde/Hasta');
        if (!pack.evmFechaOk) subParts.push('Revise advertencia arriba');
        if (sub) sub.textContent = subParts.join(' · ');
        var pct = function (x) { return x == null ? '—' : Number(x).toFixed(1) + '%'; };
        var cvLine = formatUsdM(ind.cv) + ' (' + pct(ind.cvPct) + ')';
        var svLine = formatUsdM(ind.sv) + ' (' + pct(ind.svPct) + ')';
        kpiGrid.innerHTML =
            cardEvmPro({
                title: 'Valores básicos',
                icon: 'attach_money',
                accentBar: 'bg-violet-600',
                iconCircle: 'bg-violet-600',
                bodyHtml:
                    evmMetricLine('AC (Costo real)', 'Actual Cost (AC). Costo real acumulado hasta la fecha de seguimiento (devengos del escenario Real).', formatUsdM(ind.ac), 'text-red-600 dark:text-red-400') +
                    evmMetricLine('PV (Costo planeado)', 'Planned Value (PV). Valor del trabajo planeado a la fecha según la curva plan / API.', formatUsdM(ind.pv), 'text-sky-600 dark:text-sky-400') +
                    evmMetricLine('EV (Valor ganado)', 'Earned Value (EV). Valor del trabajo completado a la fecha según avance físico.', formatUsdM(ind.ev), 'text-emerald-600 dark:text-emerald-400') +
                    evmMetricLine('BAC (Presupuesto total)', 'Budget at Completion (BAC). Presupuesto total del proyecto al término.', formatUsdM(ind.bac), 'text-slate-900 dark:text-slate-50')
            }) +
            cardEvmPro({
                title: 'Variaciones',
                subtitle: 'Cursor sobre la sigla, descripción',
                icon: 'arrow_forward',
                accentBar: 'bg-fuchsia-500',
                iconCircle: 'bg-fuchsia-600',
                bodyHtml:
                    evmMetricLine('CV (Variación costo)', 'Cost Variance (CV) = EV − AC. Positivo: favorable en costo (bajo el valor ganado a esta fecha).', cvLine, evmValueClassSigned(ind.cv)) +
                    evmMetricLine('SV (Variación cronograma)', 'Schedule Variance (SV) = EV − PV. Positivo: mayor avance valorizado que lo planeado.', svLine, evmValueClassSigned(ind.sv)) +
                    evmMetricLine('VAC (Variación final)', 'Variance at Completion (VAC) = BAC − EAC. Positivo: se proyecta terminar por debajo del BAC.', formatUsdM(ind.vac), evmValueClassSigned(ind.vac))
            }) +
            cardEvmPro({
                title: 'Índices de rendimiento',
                icon: 'percent',
                accentBar: 'bg-sky-500',
                iconCircle: 'bg-sky-600',
                bodyHtml:
                    evmIndexLine('CPI (Índice costo)', 'Cost Performance Index = EV ÷ AC. Valores ≥ 1 indican eficiencia de costo favorable.', ind.cpi) +
                    evmIndexLine('SPI (Índice cronograma)', 'Schedule Performance Index = EV ÷ PV. Valores ≥ 1 indican avance favorable vs. plan.', ind.spi)
            }) +
            cardEvmPro({
                title: 'Estimaciones',
                icon: 'lightbulb',
                accentBar: 'bg-teal-500',
                iconCircle: 'bg-teal-600',
                bodyHtml:
                    evmMetricLine('EAC (Costo estimado total)', 'Estimate at Completion. Proyección del costo total al cierre con el desempeño observado.', formatUsdM(ind.eac), 'text-orange-600 dark:text-orange-400') +
                    evmMetricLine('ETC (Costo para completar)', 'Estimate to Complete. Costo restante estimado hasta terminar (EAC − AC).', formatUsdM(ind.etc), 'text-orange-600 dark:text-orange-400')
            }) +
            cardEvmPro({
                title: 'Estados del proyecto',
                icon: 'bar_chart',
                accentBar: 'bg-amber-500',
                iconCircle: 'bg-amber-600',
                bodyHtml:
                    evmEstadoLine('Estado costo', 'Juicio cualitativo según CV y CPI respecto a umbrales del tablero.', ind.estadoCosto) +
                    evmEstadoLine('Estado cronograma', 'Juicio cualitativo según SV y SPI.', ind.estadoCronograma) +
                    evmEstadoLine('Estado general', 'Síntesis integrada de costo y plazo.', ind.estadoGeneral)
            }) +
            cardEvmPro({
                title: 'Porcentajes de avance',
                icon: 'show_chart',
                accentBar: 'bg-slate-400 dark:bg-slate-500',
                iconCircle: 'bg-slate-600',
                bodyHtml:
                    evmMetricLine('% Completado (EV)', 'Avance físico valorizado: EV ÷ BAC × 100.', ind.pctEv != null ? Number(ind.pctEv).toFixed(1) + '%' : '—', 'text-emerald-600 dark:text-emerald-400') +
                    evmMetricLine('% Planeado (PV)', 'Avance planeado a la fecha: PV ÷ BAC × 100.', ind.pctPv != null ? Number(ind.pctPv).toFixed(1) + '%' : '—', 'text-sky-600 dark:text-sky-400') +
                    evmMetricLine('% Real (AC)', 'Consumo presupuestario acumulado: AC ÷ BAC × 100.', ind.pctAc != null ? Number(ind.pctAc).toFixed(1) + '%' : '—', 'text-red-600 dark:text-red-400')
            });
        var ie = pack.ieac || {};
        var ieMets = ie.metodologias || [];
        var nIe = ieMets.length;
        var tipIeProm = 'Promedio aritmético de las metodologías IEAC (Estimate at Completion independientes). Compara escenarios de cierre de costo.';
        var ieacHtml =
            evmMetricLine('IEAC promedio (' + nIe + ' metodologías)', tipIeProm, formatUsdM(ie.promedio), 'text-orange-600 dark:text-orange-400') +
            evmMetricLine('IEAC máximo', 'Mayor estimación de costo al completar entre las metodologías listadas.', formatUsdM(ie.maximo), 'text-red-600 dark:text-red-400') +
            evmMetricLine('IEAC mínimo', 'Menor estimación de costo al completar entre las metodologías listadas.', formatUsdM(ie.minimo), 'text-emerald-600 dark:text-emerald-400') +
            evmMetricLine('Trabajo por ganar', 'BAC − EV: valor del trabajo pendiente de ejecutar a precio planeado.', formatUsdM(ie.trabajoPorGanar), 'text-sky-600 dark:text-sky-400');
        if (ieMets.length) {
            var ieInner = '';
            ieMets.forEach(function (m) {
                var tipIe = EVM_IEAC_ROW_TIPS[m.id] || ('Metodología IEAC: ' + (m.etiqueta || ''));
                ieInner += '<div class="flex justify-between gap-2 items-baseline py-1.5 first:pt-0"><span class="text-slate-700 dark:text-slate-200 pr-1 min-w-0">' + evmMethodLabel(m.etiqueta || '', tipIe) + '</span><span class="text-[11px] font-semibold shrink-0 tabular-nums text-slate-800 dark:text-slate-100">' + formatUsdM(m.valor) + '</span></div>';
            });
            ieacHtml += evmAccordionDetails('IEAC (' + nIe + ' metodologías) — ver detalle', ieInner);
        }
        if (ieacBox) ieacBox.innerHTML = ieacHtml;
        var ecd = pack.ecd || {};
        var ecdMets = ecd.metodologias || [];
        var nEc = ecdMets.length;
        var ecdHtml = '';
        var tipEcProm = 'Fecha promedio derivada de las ' + nEc + ' metodologías ECD (fecha estimada de culminación).';
        ecdHtml +=
            evmMetricLine('Fecha promedio (' + nEc + ' metodologías)', tipEcProm, ecd.fechaPromedio || '—', 'text-violet-700 dark:text-violet-300') +
            evmMetricLine('Fecha máxima', 'Fecha más tardía entre las estimaciones ECD.', ecd.fechaMaxima || '—', 'text-red-600 dark:text-red-400') +
            evmMetricLine('Fecha mínima', 'Fecha más temprana entre las estimaciones ECD.', ecd.fechaMinima || '—', 'text-emerald-600 dark:text-emerald-400');
        if (ecd.rangoMeses != null) {
            ecdHtml += evmMetricLine('Rango de estimación', 'Dispersión entre fecha mínima y máxima, en meses.', String(ecd.rangoMeses) + ' meses', 'text-orange-600 dark:text-orange-400');
        }
        if (ecdMets.length) {
            var ecInner = '';
            ecdMets.forEach(function (m) {
                var tipEc = EVM_ECD_ROW_TIPS[m.id] || ('Metodología ECD: ' + (m.etiqueta || ''));
                ecInner += '<div class="flex justify-between gap-2 items-baseline py-1.5 first:pt-0"><span class="text-slate-700 dark:text-slate-200 pr-1 min-w-0">' + evmMethodLabel(m.etiqueta || '', tipEc) + '</span><span class="text-[11px] font-semibold shrink-0 tabular-nums text-slate-800 dark:text-slate-100">' + escapeHtml(m.fechaIso || '—') + '</span></div>';
            });
            ecdHtml += evmAccordionDetails('ECD (' + nEc + ' metodologías) — ver detalle', ecInner);
        }
        if (ecdEl) ecdEl.innerHTML = ecdHtml || '<p class="text-slate-500 py-2">Sin fechas ECD.</p>';
    }

    function loadCurvaS() {
        var pid = state.proyectoId;
        if (!pid) return;
        var fsEl = $('vf-r1-fecha-seg');
        var d0 = $('vf-desde').value;
        var d1 = $('vf-hasta').value;
        if (fsEl && !fsEl.value && d1) fsEl.value = d1;
        var fs = fsEl && fsEl.value ? fsEl.value : '';
        if (!fs) {
            showVfToast('Seleccione la fecha de seguimiento EVM (mes) para cargar el análisis.', 'warning');
            return;
        }
        var fechaSeg = fs.length === 7 ? fs + '-01' : fs;
        var url = pageBase + '?handler=Reporte1CurvaSEvm&proyectoId=' + encodeURIComponent(pid) + '&fechaSeguimiento=' + encodeURIComponent(fechaSeg);
        if (d0) url += '&desde=' + encodeURIComponent(d0 + '-01');
        if (d1) url += '&hasta=' + encodeURIComponent(d1 + '-01');
        var msg = $('vf-r1-msg');
        var loadEl = $('vf-load');
        if (msg) { msg.classList.add('hidden'); msg.textContent = ''; }
        if (loadEl) loadEl.classList.remove('hidden');
        fetch(url, { headers: { Accept: 'application/json' } })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
            .then(function (x) {
                if (loadEl) loadEl.classList.add('hidden');
                if (!x.ok) throw new Error(x.j && x.j.error ? x.j.error : 'Error al cargar reporte');
                state.reporte1Pack = x.j;
                if (msg && x.j.evmFechaMensaje && !x.j.evmFechaOk) {
                    msg.textContent = x.j.evmFechaMensaje;
                    msg.classList.remove('hidden');
                }
                var title = $('vf-r1-chart-title');
                var modo = (document.querySelector('input[name="vf-r1-modo-graf"]:checked') || {}).value || 'normal';
                if (title) title.textContent = modo === 'evm' ? 'Gráfico EVM — Curva S' : 'Curva S — evolución (parciales acumulados)';
                destroyReporte1Charts();
                renderReporte1MainChart();
                renderReporte1EvmPanel(x.j);
                renderReporte1Tabla(x.j);
                renderReporte1Cascada('vf-r1-cascade-v0', x.j.cascadaV0);
                renderReporte1Cascada('vf-r1-cascade-api', x.j.cascadaApi);
                var evmPanel = $('vf-r1-evm-panel');
                if (evmPanel) evmPanel.classList.remove('hidden');
            })
            .catch(function (e) {
                if (loadEl) loadEl.classList.add('hidden');
                destroyReporte1Charts();
                state.reporte1Pack = null;
                showVfToast('Curva S / EVM: ' + (e.message || String(e)), 'error');
            });
    }

    function cleanMonto(v) {
        if (v == null || v === '') return 0;
        if (typeof v === 'number' && !isNaN(v)) return v;
        var s = String(v).trim();
        if (s === '-' || s === '') return 0;
        s = s.replace(/\s/g, '');
        if (s.indexOf(',') >= 0 && s.indexOf('.') >= 0) s = s.replace(/\./g, '').replace(',', '.');
        else s = s.replace(',', '.');
        var n = parseFloat(s);
        return isNaN(n) ? 0 : n;
    }

    function excelSerialToIso(n) {
        if (typeof n !== 'number') return null;
        var utc = Math.round((n - 25569) * 86400 * 1000);
        var d = new Date(utc);
        if (isNaN(d.getTime())) return null;
        return d.toISOString().substring(0, 10);
    }

    function normalizeHeader(h) {
        return String(h || '').toUpperCase().trim().replace(/\s+/g, '_');
    }

    function sheetRowsToVectorJson(rows, proyectoId) {
        return rows.map(function (row) {
            var o = {};
            Object.keys(row).forEach(function (k) {
                o[normalizeHeader(k)] = row[k];
            });
            var periodo = o.PERIODO;
            if (typeof periodo === 'number') periodo = excelSerialToIso(periodo) || String(periodo);
            if (periodo && String(periodo).indexOf('/') >= 0) {
                var p = String(periodo).split(/[\/\-]/);
                if (p.length === 3) periodo = p[2] + '-' + p[1].padStart(2, '0') + '-' + p[0].padStart(2, '0');
            }
            return {
                proyecto_id: proyectoId,
                centro_costo: o.CENTRO_COSTO != null ? String(o.CENTRO_COSTO) : '',
                periodo: periodo ? String(periodo).substring(0, 10) : '',
                tipo: o.TIPO != null ? String(o.TIPO) : '',
                cat_vp: o.CAT_VP != null ? String(o.CAT_VP) : '',
                detalle_factorial: o.DETALLE_FACTORIAL != null ? String(o.DETALLE_FACTORIAL) : '',
                monto: cleanMonto(o.MONTO)
            };
        });
    }

    function pickFirst(o, keys) {
        for (var i = 0; i < keys.length; i++) {
            var k = keys[i];
            if (o[k] != null && String(o[k]).trim() !== '') return o[k];
        }
        return '';
    }

    function normalizePeriodoSap(raw) {
        var periodo = raw;
        if (typeof periodo === 'number') periodo = excelSerialToIso(periodo) || String(periodo);
        if (periodo && String(periodo).indexOf('/') >= 0) {
            var p = String(periodo).split(/[\/\-]/);
            if (p.length === 3) periodo = p[2] + '-' + p[1].padStart(2, '0') + '-' + p[0].padStart(2, '0');
        }
        return periodo ? String(periodo).substring(0, 10) : '';
    }

    function sheetRowsToSapJson(rows, proyectoId) {
        return rows.map(function (row) {
            var o = {};
            Object.keys(row).forEach(function (k) { o[normalizeHeader(k)] = row[k]; });
            var idSap = pickFirst(o, ['ID_SAP', 'ID_SAP_', 'ID SAP', 'ID']);
            var version = pickFirst(o, ['VERSION_SAP', 'VERSION', 'VERSION SAP']);
            var descr = pickFirst(o, ['DESCRIPCION', 'DESCRIPCIÓN', 'DESCRIP']);
            var grupo = pickFirst(o, ['GRUPO_VERSION', 'GRUPO', 'GRUPO VERSION']);
            var periodo = normalizePeriodoSap(pickFirst(o, ['PERIODO', 'PERÍODO', 'FECHA']));
            return {
                proyecto_id: proyectoId,
                id_sap: idSap != null ? String(idSap).trim() : '',
                version_sap: version != null ? String(version).trim() : '',
                descripcion: descr != null ? String(descr).trim() : '',
                grupo_version: grupo != null ? String(grupo).trim() : '',
                periodo: periodo,
                MO: cleanMonto(pickFirst(o, ['MO'])),
                IC: cleanMonto(pickFirst(o, ['IC'])),
                EM: cleanMonto(pickFirst(o, ['EM'])),
                IE: cleanMonto(pickFirst(o, ['IE'])),
                SC: cleanMonto(pickFirst(o, ['SC'])),
                AD: cleanMonto(pickFirst(o, ['AD'])),
                CL: cleanMonto(pickFirst(o, ['CL'])),
                CT: cleanMonto(pickFirst(o, ['CT']))
            };
        }).filter(function (r) { return !!r.id_sap; });
    }

    function sheetRowsTo9cJson(rows, proyectoId) {
        return rows.map(function (row) {
            var o = {};
            Object.keys(row).forEach(function (k) { o[normalizeHeader(k)] = row[k]; });
            var idC9 = pickFirst(o, ['ID_VCP', 'ID C9', 'ID_C9', 'ID_C_9', 'ID']);
            var periodo = normalizePeriodoSap(pickFirst(o, ['PERIODO', 'PERÍODO', 'FECHA']));
            var catVp = pickFirst(o, ['CAT_VP', 'CAT VP', 'CATVP']);
            return {
                proyecto_id: proyectoId,
                id_c9: idC9 != null ? String(idC9).trim() : '',
                periodo: periodo,
                cat_vp: catVp != null ? String(catVp).trim() : '',
                moneda_base: parseInt(String(pickFirst(o, ['MONEDA_BASE', 'MONEDA BASE', 'MONEDA']) || '2025'), 10) || 2025,
                base: cleanMonto(pickFirst(o, ['BASE'])),
                cambio: cleanMonto(pickFirst(o, ['CAMBIO'])),
                control: cleanMonto(pickFirst(o, ['CONTROL'])),
                tendencia: cleanMonto(pickFirst(o, ['TENDENCIA'])),
                eat: cleanMonto(pickFirst(o, ['EAT'])),
                compromiso: cleanMonto(pickFirst(o, ['COMPROMISO'])),
                incurrido: cleanMonto(pickFirst(o, ['INCURRIDO'])),
                financiero: cleanMonto(pickFirst(o, ['FINANCIERO'])),
                por_comprometer: cleanMonto(pickFirst(o, ['POR_COMPROMETER', 'POR COMPROMETER']))
            };
        }).filter(function (r) { return !!r.id_c9 && !!r.periodo && !!r.cat_vp; });
    }

    function openImportModal() {
        if (!state.proyectoId) {
            showVfToast('Seleccione un proyecto en el menú superior antes de importar un archivo.', 'warning');
            return;
        }
        $('vf-modal-clave').classList.remove('hidden');
        $('vf-clave-input').value = '';
    }

    function confirmClaveAndPickFile() {
        var clave = $('vf-clave-input').value;
        fetch(pageBase + '?handler=VerificarClaveImportacion', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
            body: JSON.stringify({ clave: clave })
        }).then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
            .then(function (x) {
                if (!x.ok || !x.j.ok) throw new Error(x.j.error || 'Clave rechazada');
                $('vf-modal-clave').classList.add('hidden');
                if (typeof state.pendingSecureAction === 'function') {
                    var act = state.pendingSecureAction;
                    state.pendingSecureAction = null;
                    act(clave);
                    return;
                }
                $('vf-import-clave').value = clave;
                $('vf-file').click();
            })
            .catch(function (e) {
                var m = e.message || String(e);
                if (/clave|autoriz|rechazad|403|401/i.test(m)) {
                    showVfToast('La clave de importación no es válida o ha expirado. Si el problema continúa, contacte al administrador.', 'error');
                } else {
                    showVfToast(m, 'error');
                }
            });
    }

    function postImport(rows) {
        var kind = 'vector';
        var modoId = state.modoVectorId;
        if (state.vista === 'analisis' && state.modoAnalisisId === 'reporte9') kind = 'sap';
        else if (state.vista === 'analisis' && state.modoAnalisisId === 'av_fisico_c9') kind = 'c9';
        else if (state.vista === 'analisis') {
            showVfToast('La importación desde Excel está disponible únicamente en las vistas Vector financiero, SAP o Avance físico (9C).', 'warning');
            return;
        }
        var body = {
            clave: $('vf-import-clave').value,
            kind: kind,
            modoId: kind === 'vector' ? modoId : null,
            proyectoId: parseInt(state.proyectoId, 10),
            rows: rows
        };
        fetch(pageBase + '?handler=ImportarFinanciero', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
            body: JSON.stringify(body)
        }).then(function (r) { return r.text().then(function (t) { return { ok: r.ok, t: t }; }); })
            .then(function (x) {
                if (!x.ok) {
                    var errMsg = x.t;
                    try {
                        var ej = JSON.parse(x.t);
                        if (ej && ej.error) errMsg = ej.error;
                    } catch (ignore) { }
                    throw new Error(errMsg);
                }
                var msg = 'La importación se ha registrado correctamente.';
                try {
                    var j = JSON.parse(x.t);
                    if (j && j.ok && typeof j.inserted === 'number') {
                        msg = mensajeImportacionExitosa(j.inserted);
                    } else if (j && j.message) {
                        msg = String(j.message);
                    }
                } catch (ignore) {
                    msg = 'La solicitud de importación fue enviada. Verifique los datos en pantalla.';
                }
                showVfToast(msg, 'success');
                fetchDatos();
            })
            .catch(function (e) {
                showVfToast('No se pudo completar la importación. ' + (e.message || String(e)), 'error');
            });
    }

    function onFile(e) {
        var f = e.target.files && e.target.files[0];
        e.target.value = '';
        if (!f || typeof XLSX === 'undefined') {
            showVfToast('No se pudo leer el archivo o falta cargar la utilidad de hojas de cálculo. Actualice la página e inténtelo de nuevo.', 'error');
            return;
        }
        var reader = new FileReader();
        reader.onload = function (ev) {
            try {
                var wb = XLSX.read(new Uint8Array(ev.target.result), { type: 'array' });
                var sheet = wb.Sheets[wb.SheetNames[0]];
                var rows = XLSX.utils.sheet_to_json(sheet, { defval: '' });
                if (state.vista === 'vector') {
                    postImport(sheetRowsToVectorJson(rows, parseInt(state.proyectoId, 10)));
                } else if (state.modoAnalisisId === 'reporte9') {
                    var sapRows = sheetRowsToSapJson(rows, parseInt(state.proyectoId, 10));
                    if (!sapRows.length) {
                        showVfToast('No se encontraron filas SAP válidas (falta ID_SAP). Revise encabezados del Excel.', 'warning');
                        return;
                    }
                    postImport(sapRows);
                } else if (state.modoAnalisisId === 'av_fisico_c9') {
                    var c9Rows = sheetRowsTo9cJson(rows, parseInt(state.proyectoId, 10));
                    if (!c9Rows.length) {
                        showVfToast('No se encontraron filas 9C válidas (ID_C9/ID_VCP, período y cat_vp). Revise encabezados del Excel.', 'warning');
                        return;
                    }
                    postImport(c9Rows);
                } else {
                    showVfToast('En la vista Curva S no está disponible la importación de archivos. Cambie a vector financiero, SAP o 9C.', 'info');
                }
            } catch (err) {
                showVfToast('El archivo no pudo interpretarse. Compruebe que sea un Excel válido (.xlsx). Detalle: ' + (err.message || 'error desconocido'), 'error');
            }
        };
        reader.readAsArrayBuffer(f);
    }

    function wireUi() {
        document.addEventListener('toggle', function (ev) {
            var el = ev.target;
            if (!el || el.tagName !== 'DETAILS' || !el.classList.contains('vf-evm-details')) return;
            var ic = el.querySelector('.vf-evm-acc-icon');
            if (ic) ic.style.transform = el.open ? 'rotate(90deg)' : 'rotate(0deg)';
        }, true);
        function stripModeRings(el) {
            if (!el) return;
            var c = el.className;
            c = c.replace(/\bring-2 ring-(amber|sky|teal)-[0-9]+\b/g, '');
            c = c.replace(/\bring-offset-1\b/g, '');
            c = c.replace(/\bring-offset-white\b/g, '');
            c = c.replace(/\bdark:ring-offset-slate-900\b/g, '');
            el.className = c.replace(/ +/g, ' ').trim();
        }
        modosVector.forEach(function (m, i) {
            var b = $('vf-mv-' + m.id);
            if (b) b.addEventListener('click', function () {
                state.vista = 'vector';
                state.modoVectorId = m.id;
                state.tablaActual = m.tabla;
                modosVector.forEach(function (x) {
                    stripModeRings($('vf-mv-' + x.id));
                });
                modosAnalisis.forEach(function (x) {
                    stripModeRings($('vf-ma-' + x.id));
                });
                b.className += ' ring-2 ring-amber-500 ring-offset-1 ring-offset-white dark:ring-offset-slate-900';
                $('vf-panel-analisis').classList.add('hidden');
                $('vf-curvas-wrap').classList.add('hidden');
                setKpiVisible(true);
                var sapTopVec = $('vf-sap-desc-top');
                if (sapTopVec) sapTopVec.classList.add('hidden');
                $('vf-import-wrap').classList.remove('hidden');
                updateImportedGridVisibility();
                fetchDatos();
            });
        });
        modosAnalisis.forEach(function (m) {
            var b = $('vf-ma-' + m.id);
            if (b) b.addEventListener('click', function () {
                state.vista = 'analisis';
                state.modoAnalisisId = m.id;
                state.tablaActual = m.tabla;
                modosVector.forEach(function (x) {
                    stripModeRings($('vf-mv-' + x.id));
                });
                modosAnalisis.forEach(function (x) {
                    stripModeRings($('vf-ma-' + x.id));
                });
                b.className += ' ring-2 ring-sky-500 ring-offset-1 ring-offset-white dark:ring-offset-slate-900';
                // Curva S usa su propio bloque dedicado; no mostrar franja "Vista análisis...".
                $('vf-panel-analisis').classList.toggle('hidden', m.id === 'reporte1');
                $('vf-curvas-wrap').classList.toggle('hidden', m.id !== 'reporte1');
                setKpiVisible(m.id !== 'reporte1');
                var filtros = $('vf-filtros-wrap');
                if (filtros) filtros.classList.toggle('hidden', m.id === 'reporte1');
                var sapTop = $('vf-sap-desc-top');
                if (sapTop) sapTop.classList.toggle('hidden', m.id !== 'reporte9');
                $('vf-import-wrap').classList.remove('hidden');
                updateImportedGridVisibility();
                if (m.id === 'reporte1') {
                    ensureDefaultReporte1Month(true);
                    var thEl = $('vf-thead');
                    var tbEl = $('vf-tbody');
                    if (thEl) thEl.innerHTML = '';
                    if (tbEl) tbEl.innerHTML = '';
                    loadCurvaS();
                } else fetchDatos();
            });
        });
        updateImportedGridVisibility();
        $('vf-desde').addEventListener('change', function () {
            var r = state.allRows || [];
            if (state.vista === 'vector') renderKpi(r, state.modoVectorId);
            else if (state.modoAnalisisId === 'av_fisico_c9') renderKpi9c(r);
            else if (state.vista === 'analisis' && state.modoAnalisisId === 'reporte1') loadCurvaS();
            renderTable(r);
        });
        $('vf-hasta').addEventListener('change', function () {
            var r = state.allRows || [];
            if (state.vista === 'vector') renderKpi(r, state.modoVectorId);
            else if (state.modoAnalisisId === 'av_fisico_c9') renderKpi9c(r);
            else if (state.vista === 'analisis' && state.modoAnalisisId === 'reporte1') loadCurvaS();
            renderTable(r);
        });
        $('vf-clear-fechas').addEventListener('click', function () {
            $('vf-desde').value = ''; $('vf-hasta').value = '';
            var r = state.allRows || [];
            if (state.vista === 'vector') renderKpi(r, state.modoVectorId);
            else if (state.modoAnalisisId === 'av_fisico_c9') renderKpi9c(r);
            else if (state.vista === 'analisis' && state.modoAnalisisId === 'reporte1') loadCurvaS();
            renderTable(r);
        });
        var r1ref = $('vf-r1-refresh');
        if (r1ref) r1ref.addEventListener('click', function () {
            if (state.vista === 'analisis' && state.modoAnalisisId === 'reporte1') loadCurvaS();
        });
        var pdfTec = $('vf-evm-pdf-tecnico');
        var pdfEje = $('vf-evm-pdf-ejecutivo');
        if (pdfTec) pdfTec.addEventListener('click', function () { exportEvmPdf(false); });
        if (pdfEje) pdfEje.addEventListener('click', function () { exportEvmPdf(true); });
        document.querySelectorAll('input[name="vf-r1-modo-graf"]').forEach(function (el) {
            el.addEventListener('change', function () {
                if (!(state.vista === 'analisis' && state.modoAnalisisId === 'reporte1') || !state.reporte1Pack) return;
                var title = $('vf-r1-chart-title');
                var modo = (document.querySelector('input[name="vf-r1-modo-graf"]:checked') || {}).value || 'normal';
                if (title) title.textContent = modo === 'evm' ? 'Gráfico EVM — Curva S' : 'Curva S — evolución (parciales acumulados)';
                renderReporte1MainChart();
                var evmPanel = $('vf-r1-evm-panel');
                if (evmPanel) evmPanel.classList.remove('hidden');
            });
        });
        var vPrev = $('vf-prev');
        var vNext = $('vf-next');
        if (vPrev) vPrev.addEventListener('click', function () { if (state.page > 1) { state.page--; renderTable(state.allRows || []); } });
        if (vNext) vNext.addEventListener('click', function () { state.page++; renderTable(state.allRows || []); });
        $('vf-import-btn').addEventListener('click', openImportModal);
        $('vf-clave-ok').addEventListener('click', confirmClaveAndPickFile);
        $('vf-clave-cancel').addEventListener('click', function () { $('vf-modal-clave').classList.add('hidden'); });
        $('vf-file').addEventListener('change', onFile);
        var sapTopSel = $('vf-sap-desc-top');
        if (sapTopSel) sapTopSel.addEventListener('change', function () {
            state.sapDescripcionFilter = sapTopSel.value || '';
            state.page = 1;
            var r = state.allRows || [];
            if (state.vista === 'analisis' && state.modoAnalisisId === 'reporte9') {
                renderSapKpiAndManager(r);
                renderTable(r);
            }
        });
        var r1tbody = $('vf-r1-tabla-body');
        if (r1tbody) {
            r1tbody.addEventListener('click', function (ev) {
                var btn = ev.target && ev.target.closest ? ev.target.closest('.vf-cascada-open') : null;
                if (!btn) return;
                openCascadaDetalle(btn.getAttribute('data-categoria') || '', btn.getAttribute('data-tipo') || '', Number(btn.getAttribute('data-monto') || 0));
            });
        }
        var closeX = $('vf-cascada-close-x');
        if (closeX) closeX.addEventListener('click', closeCascadaModal);
        var modal = $('vf-modal-cascada');
        if (modal) {
            modal.addEventListener('click', function (ev) {
                if (ev.target === modal) closeCascadaModal();
            });
        }
    }

    window.pycVectoresFinInit = function (proyectoIdGetter) {
        cfg = window.__pycFinCfg || cfg;
        modosVector = cfg.modosVector || [];
        modosAnalisis = cfg.modosAnalisis || [];
        categoriasKpi = cfg.categoriasKpi || [];
        codigoSap = cfg.codigoSap || {};
        pageBase = cfg.pageBase || pageBase;
        tryRegisterChartAnnotation();
        ensureDefaultReporte1Month();
        wireUi();
        if (cfg.iniciarEnCurvaS) {
            var pidInit = typeof proyectoIdGetter === 'function' ? proyectoIdGetter() : null;
            if (pidInit) state.proyectoId = pidInit;
            var maR1 = $('vf-ma-reporte1');
            if (maR1) maR1.click();
        }
        setInterval(function () {
            var id = proyectoIdGetter();
            if (id && id !== state.proyectoId) {
                state.proyectoId = id;
                var tab = $('pmo-tab-panel-vf');
                if (tab && !tab.classList.contains('hidden')) fetchDatos();
            }
        }, 400);
    };

    window.pycVectoresOnTabShown = function () {
        updateImportedGridVisibility();
        if (state.proyectoId) fetchDatos();
    };
})();
