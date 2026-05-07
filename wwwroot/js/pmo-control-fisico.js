/**
 * PMO — Control físico / líneas base (Curva S api_acum, KPI, import Excel).
 * Requiere: SheetJS (XLSX), Chart.js 4, elemento #pmo-cf-proyecto-id
 */
(function () {
    var TABLAS = [
        { key: 'real', tabla: 'av_fisico_real', label: 'REAL', color: '#22c55e' },
        { key: 'npc', tabla: 'av_fisico_npc', label: 'NPC', color: '#3b82f6' },
        { key: 'poa', tabla: 'av_fisico_poa', label: 'POA', color: '#eab308' },
        { key: 'v0', tabla: 'av_fisico_v0', label: 'V0', color: '#06b6d4' },
        { key: 'api', tabla: 'av_fisico_api', label: 'API', color: '#ef4444' }
    ];

    var pageBase = '/PMO';
    var chartInst = null;
    var rawByKey = { real: [], npc: [], poa: [], v0: [], api: [] };
    var vistaSeries = 'todas';
    /** Las tablas bajo el gráfico solo aparecen tras pulsar un botón de Curva S (Todas, REAL, …). */
    var cfeShowDetailTables = false;

    function $(id) { return document.getElementById(id); }

    function rowPeriodo(r) {
        var p = r.periodo || r.Periodo || '';
        return String(p).substring(0, 10);
    }

    function toNum(v) {
        if (v == null || v === '') return 0;
        if (typeof v === 'number' && !isNaN(v)) return v;
        var s = String(v).trim().replace(',', '.');
        if (s.indexOf('%') >= 0) {
            var n = parseFloat(s.replace('%', ''));
            return isNaN(n) ? 0 : n / 100;
        }
        var x = parseFloat(s);
        return isNaN(x) ? 0 : x;
    }

    /** Eje Y en %: si el valor guardado es fracción (≤1) se multiplica por 100; si ya viene como 0–100 se deja. */
    function toPctDisplay(v) {
        var x = toNum(v);
        if (x > 1.0001) return x;
        return x * 100;
    }

    /**
     * Fechas + opcional filtro por la columna "vector" dentro de una misma tabla.
     * Cada línea base (REAL, NPC, …) es una tabla distinta: si el vector elegido no existe
     * en esa tabla, se muestran todas las filas de esa tabla (no se “apaga” la curva en cero).
     */
    function filterRows(rows, desde, hasta, vector) {
        var base = rows.filter(function (r) {
            var pr = rowPeriodo(r);
            if (!pr) return false;
            if (desde && pr < desde) return false;
            if (hasta && pr > hasta) return false;
            return true;
        });
        var vNeed = vector != null ? String(vector).trim() : '';
        if (!vNeed) return base;
        var withVec = base.filter(function (r) {
            var vec = String(r.vector != null ? r.vector : r.Vector || '').trim();
            return vec.toLowerCase() === vNeed.toLowerCase();
        });
        return withVec.length ? withVec : base;
    }

    /** Un punto por período: máximo api_acum % entre filas coincidentes (varios vectores). */
    function periodToPctMap(rows) {
        var m = {};
        rows.forEach(function (r) {
            var p = rowPeriodo(r);
            if (!p) return;
            var pct = toPctDisplay(r.api_acum != null ? r.api_acum : r.apiAcum);
            if (m[p] == null || pct > m[p]) m[p] = pct;
        });
        return m;
    }

    /** Un punto por período: api_parcial % del período (no acumulado, sin dividir por total). */
    function periodToPartialPctMap(rows) {
        var m = {};
        rows.forEach(function (r) {
            var p = rowPeriodo(r);
            if (!p) return;
            var pct = toPctDisplay(r.api_parcial != null ? r.api_parcial : r.apiParcial);
            if (m[p] == null || pct > m[p]) m[p] = pct;
        });
        return m;
    }

    function mergePeriods(maps) {
        var set = {};
        maps.forEach(function (mp) {
            Object.keys(mp).forEach(function (k) { set[k] = true; });
        });
        return Object.keys(set).sort();
    }

    function hoyIso() {
        var d = new Date();
        var m = (d.getMonth() + 1).toString().padStart(2, '0');
        var day = d.getDate().toString().padStart(2, '0');
        return d.getFullYear() + '-' + m + '-' + day;
    }

    function nearestPeriod(periods, target) {
        if (!periods || !periods.length) return null;
        var tt = new Date(target + 'T12:00:00').getTime();
        var best = periods[0];
        var bestD = Math.abs(new Date(best + 'T12:00:00').getTime() - tt);
        for (var i = 1; i < periods.length; i++) {
            var p = periods[i];
            var d = Math.abs(new Date(p + 'T12:00:00').getTime() - tt);
            if (d < bestD) {
                best = p;
                bestD = d;
            }
        }
        return best;
    }

    function pctAtPeriod(map, period) {
        if (period == null || map[period] == null) return null;
        return map[period];
    }

    function sumApiParcial(rows) {
        var s = 0;
        rows.forEach(function (r) {
            s += toNum(r.api_parcial != null ? r.api_parcial : r.apiParcial);
        });
        return s;
    }

    var vlinePlugin = {
        id: 'pmoCfVline',
        afterDatasetsDraw: function (chart) {
            var xLab = chart.options.plugins && chart.options.plugins.pmoCfVline && chart.options.plugins.pmoCfVline.xLabel;
            if (!xLab || !chart.scales.x) return;
            var labels = chart.data.labels || [];
            var idx = -1;
            for (var i = 0; i < labels.length; i++) {
                if (String(labels[i]) === String(xLab)) {
                    idx = i;
                    break;
                }
            }
            if (idx < 0) return;
            var x = chart.scales.x.getPixelForTick(idx);
            var top = chart.chartArea.top;
            var bot = chart.chartArea.bottom;
            var ctx = chart.ctx;
            ctx.save();
            ctx.beginPath();
            ctx.strokeStyle = '#f97316';
            ctx.lineWidth = 2;
            ctx.setLineDash([6, 4]);
            ctx.moveTo(x, top);
            ctx.lineTo(x, bot);
            ctx.stroke();
            ctx.setLineDash([]);
            ctx.fillStyle = '#ea580c';
            ctx.font = '10px sans-serif';
            ctx.fillText('HOY', x + 4, top + 12);
            ctx.restore();
        }
    };

    function buildChartConfig(labels, seriesMaps, hoyLine) {
        var datasets = TABLAS.map(function (t) {
            var data = labels.map(function (lab) {
                var v = seriesMaps[t.key][lab];
                if (vistaSeries !== 'todas' && vistaSeries !== t.key) return 0;
                return v != null ? v : 0;
            });
            return {
                label: t.label,
                data: data,
                borderColor: t.color,
                backgroundColor: t.color + '33',
                tension: 0.15,
                fill: false,
                spanGaps: true
            };
        });

        return {
            type: 'line',
            data: { labels: labels, datasets: datasets },
            plugins: [vlinePlugin],
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    pmoCfVline: { xLabel: hoyLine && labels.indexOf(hoyLine) >= 0 ? hoyLine : null },
                    title: { display: true, text: 'Curva S — API acumulado (% por línea base)', font: { size: 14 } },
                    legend: { position: 'bottom' }
                },
                scales: {
                    y: {
                        title: { display: true, text: '%' },
                        min: 0,
                        suggestedMax: 100
                    },
                    x: { title: { display: true, text: 'Período' } }
                }
            }
        };
    }

    function destroyChart() {
        if (chartInst) {
            chartInst.destroy();
            chartInst = null;
        }
    }

    function renderChart(labels, seriesMaps, hoyLine) {
        var canvas = $('pmo-cf-chart');
        if (!canvas || !window.Chart) return;
        destroyChart();
        if (!labels || !labels.length) return;
        var cfg = buildChartConfig(labels, seriesMaps, hoyLine);
        chartInst = new Chart(canvas.getContext('2d'), cfg);
    }

    function renderKpi(desde, hasta, vector) {
        var el = $('pmo-cf-kpi');
        if (!el) return;

        var hoy = hoyIso();

        var parts = TABLAS.map(function (t) {
            var rowsF = filterRows(rawByKey[t.key], desde, hasta, vector);
            var mapAcum = periodToPctMap(rowsF);
            var mapParcial = periodToPartialPctMap(rowsF);
            var periods = Object.keys(mapAcum).sort();
            var near = nearestPeriod(periods, hoy);
            var acum = near != null ? pctAtPeriod(mapAcum, near) : null;
            var parcialPct = near != null ? pctAtPeriod(mapParcial, near) : null;
            return {
                label: t.label,
                color: t.color,
                acum: acum != null ? acum.toFixed(2) + ' %' : '—',
                parcial: parcialPct != null ? parcialPct.toFixed(2) + ' %' : '—'
            };
        });

        el.innerHTML = '<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-3">' +
            parts.map(function (p) {
                return '<div class="rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-800 p-3 shadow-sm">' +
                    '<p class="text-xs font-bold uppercase tracking-wide text-slate-500 dark:text-slate-400">' + p.label + '</p>' +
                    '<p class="text-[11px] text-slate-500 mt-2">API acum. (ref. HOY)</p>' +
                    '<p class="text-lg font-semibold" style="color:' + p.color + '">' + p.acum + '</p>' +
                    '<p class="text-[11px] text-slate-500 mt-2">API parcial (ref. HOY)</p>' +
                    '<p class="text-sm font-medium text-slate-800 dark:text-slate-200">' + p.parcial + '</p>' +
                    '</div>';
            }).join('') + '</div>';
    }

    function escapeHtml(s) {
        if (s == null) return '';
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function formatPctCol(v) {
        var x = toPctDisplay(v);
        if (!isFinite(x)) return '—';
        return (Math.round(x * 100) / 100).toFixed(2) + '%';
    }

    /** Tablas bajo el gráfico: solo si el usuario pulsó Curva S; respeta Desde/Hasta/Vector y la serie elegida. */
    function renderDetailTables(desde, hasta, vector) {
        var hint = $('pmo-cf-table-hint');
        var wrap = $('pmo-cf-table-wrap');
        var inner = $('pmo-cf-table-inner');
        var cap = $('pmo-cf-table-filter-caption');
        if (!wrap || !inner) return;

        if (!cfeShowDetailTables) {
            if (hint) hint.classList.remove('hidden');
            wrap.classList.add('hidden');
            inner.innerHTML = '';
            if (cap) cap.textContent = '';
            return;
        }

        if (hint) hint.classList.add('hidden');
        wrap.classList.remove('hidden');

        var bits = [];
        if (desde) bits.push('Desde ' + desde);
        if (hasta) bits.push('Hasta ' + hasta);
        if (vector) bits.push('Vector ' + vector);
        var serieTabla = 'Todas las líneas base';
        if (vistaSeries !== 'todas') {
            var tf = TABLAS.filter(function (x) { return x.key === vistaSeries; })[0];
            serieTabla = tf ? 'Solo ' + tf.label + ' (' + tf.tabla + ')' : vistaSeries;
        }
        bits.push('Vista: ' + serieTabla);
        if (cap) cap.textContent = bits.join(' · ');

        var showTablas = vistaSeries === 'todas' ? TABLAS : TABLAS.filter(function (t) { return t.key === vistaSeries; });

        var th = '<thead><tr class="bg-slate-50 dark:bg-slate-800/80 text-left text-[10px] uppercase tracking-wide text-slate-500">' +
            '<th class="p-2">ID</th><th class="p-2">Período</th><th class="p-2">Vector</th>' +
            '<th class="p-2 text-right">IE p.</th><th class="p-2 text-right">IE ac.</th>' +
            '<th class="p-2 text-right">EM p.</th><th class="p-2 text-right">EM ac.</th>' +
            '<th class="p-2 text-right">MO p.</th><th class="p-2 text-right">MO ac.</th>' +
            '<th class="p-2 text-right">API p.</th><th class="p-2 text-right">API ac.</th></tr></thead>';

        var blocks = showTablas.map(function (t) {
            var rows = filterRows(rawByKey[t.key], desde, hasta, vector).slice().sort(function (a, b) {
                var pa = rowPeriodo(a);
                var pb = rowPeriodo(b);
                if (pa !== pb) return pa.localeCompare(pb);
                return String(a.id != null ? a.id : '').localeCompare(String(b.id != null ? b.id : ''));
            });
            var body = rows.map(function (r) {
                return '<tr class="border-b border-slate-100 dark:border-slate-700">' +
                    '<td class="py-1.5 pr-2 font-mono text-[11px] whitespace-nowrap">' + escapeHtml(r.id) + '</td>' +
                    '<td class="py-1.5 pr-2 whitespace-nowrap">' + escapeHtml(rowPeriodo(r)) + '</td>' +
                    '<td class="py-1.5 pr-2">' + escapeHtml(String(r.vector != null ? r.vector : r.Vector || '')) + '</td>' +
                    '<td class="py-1.5 pr-2 text-right tabular-nums">' + formatPctCol(r.ie_parcial != null ? r.ie_parcial : r.ieParcial) + '</td>' +
                    '<td class="py-1.5 pr-2 text-right tabular-nums">' + formatPctCol(r.ie_acumulado != null ? r.ie_acumulado : r.ieAcumulado) + '</td>' +
                    '<td class="py-1.5 pr-2 text-right tabular-nums">' + formatPctCol(r.em_parcial != null ? r.em_parcial : r.emParcial) + '</td>' +
                    '<td class="py-1.5 pr-2 text-right tabular-nums">' + formatPctCol(r.em_acumulado != null ? r.em_acumulado : r.emAcumulado) + '</td>' +
                    '<td class="py-1.5 pr-2 text-right tabular-nums">' + formatPctCol(r.mo_parcial != null ? r.mo_parcial : r.moParcial) + '</td>' +
                    '<td class="py-1.5 pr-2 text-right tabular-nums">' + formatPctCol(r.mo_acumulado != null ? r.mo_acumulado : r.moAcumulado) + '</td>' +
                    '<td class="py-1.5 pr-2 text-right tabular-nums">' + formatPctCol(r.api_parcial != null ? r.api_parcial : r.apiParcial) + '</td>' +
                    '<td class="py-1.5 text-right tabular-nums">' + formatPctCol(r.api_acum != null ? r.api_acum : r.apiAcum) + '</td></tr>';
            }).join('');
            if (!body) {
                body = '<tr><td colspan="11" class="p-3 text-slate-500 text-center">Sin filas con el filtro actual.</td></tr>';
            }
            return '<div class="mb-6 last:mb-0">' +
                '<h4 class="text-xs font-bold uppercase tracking-wide mb-2 flex flex-wrap items-center gap-2">' +
                '<span style="color:' + t.color + '">' + escapeHtml(t.label) + '</span>' +
                '<span class="font-mono font-normal text-slate-500 dark:text-slate-400 text-[10px]">' + escapeHtml(t.tabla) + '</span>' +
                '<span class="text-slate-400 font-normal normal-case">(' + rows.length + ' filas)</span></h4>' +
                '<div class="overflow-x-auto rounded-lg border border-slate-200 dark:border-slate-600">' +
                '<table class="min-w-full text-xs">' + th + '<tbody>' + body + '</tbody></table></div></div>';
        });

        inner.innerHTML = blocks.join('');
    }

    function refreshUi() {
        var desde = ($('pmo-cf-desde') && $('pmo-cf-desde').value) || '';
        var hasta = ($('pmo-cf-hasta') && $('pmo-cf-hasta').value) || '';
        var vector = ($('pmo-cf-vector') && $('pmo-cf-vector').value) || '';

        var maps = {};
        TABLAS.forEach(function (t) {
            maps[t.key] = periodToPctMap(filterRows(rawByKey[t.key], desde, hasta, vector));
        });
        var labels = mergePeriods(TABLAS.map(function (t) { return maps[t.key]; }));
        var hoy = hoyIso();
        var hoyLine = labels.length ? nearestPeriod(labels, hoy) : null;

        renderChart(labels, maps, hoyLine);
        renderKpi(desde, hasta, vector);
        renderDetailTables(desde, hasta, vector);

        var stat = $('pmo-cf-status');
        if (stat) {
            var n = 0;
            TABLAS.forEach(function (t) { n += rawByKey[t.key].length; });
            stat.textContent = n ? (n + ' filas cargadas · HOY ≈ ' + (hoyLine || hoy)) : 'Sin datos importados para este proyecto.';
        }
    }

    function fillVectorSelect() {
        var sel = $('pmo-cf-vector');
        if (!sel) return;
        var set = {};
        TABLAS.forEach(function (t) {
            rawByKey[t.key].forEach(function (r) {
                var v = String(r.vector != null ? r.vector : r.Vector || '').trim();
                if (v) set[v] = true;
            });
        });
        var cur = sel.value;
        sel.innerHTML = '<option value="">Todos los vectores</option>';
        Object.keys(set).sort().forEach(function (v) {
            var o = document.createElement('option');
            o.value = v;
            o.textContent = v;
            sel.appendChild(o);
        });
        if (cur && set[cur]) sel.value = cur;
    }

    /**
     * Unifica cabeceras de Excel/exports PHP: espacios → snake_case y camelCase/PascalCase → snake_case.
     * Sin esto, "IeParcial" pasaba a "ieparcial" y no matcheaba r.ie_parcial (import con ceros).
     */
    function normalizeKey(k) {
        var s = String(k || '').trim();
        if (!s) return '';
        s = s.replace(/([a-z0-9])([A-Z])/g, '$1_$2').replace(/([A-Z])([A-Z][a-z])/g, '$1_$2');
        s = s.toLowerCase().replace(/\s+/g, '_').replace(/_+/g, '_');
        return s;
    }

    function normalizeRow(obj) {
        var o = {};
        Object.keys(obj).forEach(function (k) {
            o[normalizeKey(k)] = obj[k];
        });
        return o;
    }

    function cleanPercentageCell(val) {
        if (val == null || val === '') return 0;
        if (typeof val === 'number') return val > 9.9999 ? val / 100 : val;
        var s = String(val).trim();
        if (s.indexOf('%') >= 0) {
            var n = parseFloat(s.replace('%', '').replace(',', '.'));
            if (isNaN(n)) return 0;
            return Math.min(n, 999.99) / 100;
        }
        var x = parseFloat(s.replace(',', '.'));
        if (isNaN(x)) return 0;
        return x;
    }

    function excelSerialToIso(n) {
        var epoch = new Date(Date.UTC(1899, 11, 30));
        var d = new Date(epoch.getTime() + Math.round(n) * 86400000);
        var m = (d.getUTCMonth() + 1).toString().padStart(2, '0');
        var day = d.getUTCDate().toString().padStart(2, '0');
        return d.getUTCFullYear() + '-' + m + '-' + day;
    }

    function parsePeriodoCell(v) {
        if (v == null || v === '') return null;
        if (typeof v === 'number' && v > 20000 && v < 60000) return excelSerialToIso(v);
        var s = String(v).trim().replace(/\s+(real|npc|poa|v0|vo|api)\s*$/i, '');
        if (/^\d{4}-\d{2}-\d{2}/.test(s)) return s.substring(0, 10);
        var m = s.match(/^(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})/);
        if (m) {
            var dd = m[1].padStart(2, '0');
            var mm = m[2].padStart(2, '0');
            return m[3] + '-' + mm + '-' + dd;
        }
        return null;
    }

    function mapExcelToPayload(tablaDestino, sheetRow) {
        var r = normalizeRow(sheetRow);
        var id = r.id || r.id_av_real || r.id_av_npc || r.id_av_poa || r.id_av_v0 || r.id_av_vo || r.id_av_api || '';
        id = String(id).replace(/\s+(real|npc|poa|v0|vo|api)\s*$/i, '').trim();
        var periodo = parsePeriodoCell(r.periodo || r.fecha);
        if (!id || !periodo) return null;

        return {
            id: id.substring(0, 20),
            periodo: periodo,
            vector: String(r.vector || 'GEN').trim().substring(0, 10) || 'GEN',
            ie_parcial: cleanPercentageCell(r.ie_parcial != null ? r.ie_parcial : r.ie),
            ie_acumulado: cleanPercentageCell(r.ie_acumulado != null ? r.ie_acumulado : r.ie_acum),
            em_parcial: cleanPercentageCell(r.em_parcial != null ? r.em_parcial : r.em),
            em_acumulado: cleanPercentageCell(r.em_acumulado != null ? r.em_acumulado : r.em_acum),
            mo_parcial: cleanPercentageCell(r.mo_parcial != null ? r.mo_parcial : r.mo),
            mo_acumulado: cleanPercentageCell(r.mo_acumulado != null ? r.mo_acumulado : r.mo_acum),
            api_parcial: cleanPercentageCell(r.api_parcial != null ? r.api_parcial : r.api),
            api_acum: cleanPercentageCell(r.api_acum != null ? r.api_acum : r.api_acumulado)
        };
    }

    function loadAll(getPid) {
        var pid = getPid();
        if (!pid) {
            var l0 = $('pmo-cf-load');
            if (l0) l0.classList.add('hidden');
            return Promise.resolve();
        }
        var load = $('pmo-cf-load');
        if (load) load.classList.remove('hidden');
        var qs = '';
        var fetches = TABLAS.map(function (t) {
            var u = pageBase + '?handler=DatosAvFisico&proyectoId=' + encodeURIComponent(pid) +
                '&tabla=' + encodeURIComponent(t.tabla) + qs;
            return fetch(u, { credentials: 'same-origin' }).then(function (res) {
                return res.ok ? res.json() : [];
            }).catch(function () { return []; });
        });
        return Promise.all(fetches).then(function (arrays) {
            for (var i = 0; i < TABLAS.length; i++) {
                var arr = arrays[i];
                rawByKey[TABLAS[i].key] = Array.isArray(arr) ? arr : [];
            }
            fillVectorSelect();
            refreshUi();
        }).finally(function () {
            if (load) load.classList.add('hidden');
        });
    }

    function postImport(clave, proyectoId, tabla, rows, onOk, onErr) {
        fetch(pageBase + '?handler=ImportarFinanciero', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                clave: clave,
                kind: 'av_fisico',
                modoId: tabla,
                proyectoId: proyectoId,
                rows: rows
            })
        }).then(function (res) { return res.json().then(function (j) { return { res: res, j: j }; }); })
            .then(function (x) {
                var j = x.j;
                var ok = x.res.ok && (j.ok === true || j.success === true);
                if (ok) onOk(j);
                else onErr((j && (j.error || j.message)) || x.res.statusText || 'Error');
            }).catch(function (e) { onErr(e.message || 'Error de red'); });
    }

    function openClaveModal(onContinue) {
        var m = $('pmo-cf-modal-clave');
        if (!m) return;
        m.classList.remove('hidden');
        var inp = $('pmo-cf-clave-input');
        if (inp) inp.value = '';
        var ok = $('pmo-cf-clave-ok');
        var cancel = $('pmo-cf-clave-cancel');
        function close() {
            m.classList.add('hidden');
            if (ok) ok.onclick = null;
            if (cancel) cancel.onclick = null;
        }
        if (cancel) cancel.onclick = close;
        if (ok) ok.onclick = function () {
            var c = inp ? inp.value.trim() : '';
            if (!c) return;
            close();
            onContinue(c);
        };
    }

    function openImportModal() {
        var m = $('pmo-cf-import-modal');
        if (m) {
            m.classList.remove('hidden');
            m.setAttribute('aria-hidden', 'false');
        }
    }

    function closeImportModal() {
        var m = $('pmo-cf-import-modal');
        if (m) {
            m.classList.add('hidden');
            m.setAttribute('aria-hidden', 'true');
        }
    }

    function verifyClave(clave, onOk, onErr) {
        fetch(pageBase + '?handler=VerificarClaveImportacion', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ clave: clave })
        }).then(function (r) { return r.json(); })
            .then(function (j) {
                if (j.ok) onOk();
                else onErr(j.error || 'Clave no válida');
            }).catch(function (e) { onErr(e.message); });
    }

    function handleFile(e, getPid) {
        var f = e.target.files && e.target.files[0];
        if (!f) return;
        var tabla = $('pmo-cf-tabla-import') && $('pmo-cf-tabla-import').value;
        if (!tabla) {
            alert('Seleccione la tabla destino (Real, NPC, …).');
            e.target.value = '';
            return;
        }
        closeImportModal();
        var reader = new FileReader();
        reader.onload = function () {
            try {
                var wb = XLSX.read(reader.result, { type: 'binary' });
                var sh = wb.Sheets[wb.SheetNames[0]];
                var json = XLSX.utils.sheet_to_json(sh, { defval: '' });
                var rows = [];
                for (var i = 0; i < json.length; i++) {
                    var p = mapExcelToPayload(tabla, json[i]);
                    if (p) rows.push(p);
                }
                if (!rows.length) {
                    alert('No se pudieron leer filas válidas (id y periodo/fecha obligatorios).');
                    e.target.value = '';
                    return;
                }
                openClaveModal(function (clave) {
                    verifyClave(clave, function () {
                        postImport(clave, getPid(), tabla, rows, function (j) {
                            var msg = j.message || j.Message || 'OK';
                            var ins = j.inserted != null ? j.inserted : j.inserted_count;
                            alert(msg + (ins != null ? ' · Insertadas: ' + ins : ''));
                            loadAll(getPid);
                        }, function (err) { alert('Importación: ' + err); });
                    }, function (err) { alert(err); });
                });
            } catch (ex) {
                alert('Error al leer Excel: ' + (ex.message || ex));
            }
            e.target.value = '';
        };
        reader.readAsBinaryString(f);
    }

    window.pmoControlFisicoInit = function (getProyectoId) {
        pageBase = (window.__pmoCfCfg && window.__pmoCfCfg.pageBase) || pageBase;

        TABLAS.forEach(function (t) {
            var b = $('pmo-cf-serie-' + t.key);
            if (b) b.addEventListener('click', function () {
                vistaSeries = t.key;
                cfeShowDetailTables = true;
                refreshUi();
            });
        });
        var bAll = $('pmo-cf-serie-todas');
        if (bAll) bAll.addEventListener('click', function () {
            vistaSeries = 'todas';
            cfeShowDetailTables = true;
            refreshUi();
        });

        ['pmo-cf-desde', 'pmo-cf-hasta', 'pmo-cf-vector'].forEach(function (id) {
            var el = $(id);
            if (el) el.addEventListener('change', refreshUi);
        });
        var clr = $('pmo-cf-clear');
        if (clr) clr.addEventListener('click', function () {
            var a = $('pmo-cf-desde'), b = $('pmo-cf-hasta'), c = $('pmo-cf-vector');
            if (a) a.value = '';
            if (b) b.value = '';
            if (c) c.value = '';
            cfeShowDetailTables = false;
            refreshUi();
        });

        var impModal = $('pmo-cf-import-modal');
        if (impModal) {
            impModal.addEventListener('click', function (e) {
                if (e.target === impModal) closeImportModal();
            });
        }
        var openImp = $('pmo-cf-open-import-modal');
        if (openImp) openImp.addEventListener('click', openImportModal);
        ['pmo-cf-import-modal-close-x', 'pmo-cf-import-modal-close-btn'].forEach(function (id) {
            var b = $(id);
            if (b) b.addEventListener('click', closeImportModal);
        });

        var impFileBtn = $('pmo-cf-import-modal-file-btn');
        var file = $('pmo-cf-file');
        if (impFileBtn && file) {
            impFileBtn.addEventListener('click', function () { file.click(); });
        }
        if (file) {
            file.addEventListener('change', function (ev) { handleFile(ev, getProyectoId); });
        }

        var reload = $('pmo-cf-reload');
        if (reload) reload.addEventListener('click', function () { loadAll(getProyectoId); });

        loadAll(getProyectoId);
    };
})();
