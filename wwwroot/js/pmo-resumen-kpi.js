/**
 * PMO — Resumen General: paneles N1/N2/N3 + mini curva desde Reporte1CurvaSEvm.
 */
(function () {
    function $(id) { return document.getElementById(id); }

    function escapeHtml(s) {
        if (s == null) return '';
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function formatUsdM(n) {
        if (n == null || isNaN(Number(n))) return '—';
        var m = Number(n) / 1e6;
        return '$' + m.toLocaleString('es-CL', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + 'M';
    }

    function fmtPct(x, dec) {
        if (x == null || isNaN(Number(x))) return '—';
        var d = dec != null ? dec : 1;
        return Number(x).toFixed(d) + '%';
    }

    function fmtIdx(x) {
        if (x == null || isNaN(Number(x))) return '—';
        return Number(x).toFixed(3);
    }

    function previousMonthIso() {
        var now = new Date();
        var y = now.getFullYear();
        var m = now.getMonth();
        if (m === 0) {
            y -= 1;
            m = 12;
        }
        return String(y) + '-' + String(m).padStart(2, '0');
    }

    function estadoPillClass(t) {
        var u = String(t || '').toUpperCase();
        if (u.indexOf('VERDE') >= 0 && u.indexOf('AMARILLO') < 0) return 'bg-emerald-100 text-emerald-900 border-emerald-300 dark:bg-emerald-950/60 dark:text-emerald-100 dark:border-emerald-700';
        if (u.indexOf('AMARILLO') >= 0 || u.indexOf('ALERTA') >= 0) return 'bg-amber-100 text-amber-950 border-amber-300 dark:bg-amber-950/50 dark:text-amber-100 dark:border-amber-700';
        if (u.indexOf('ROJO') >= 0) return 'bg-red-100 text-red-900 border-red-300 dark:bg-red-950/50 dark:text-red-100 dark:border-red-700';
        return 'bg-slate-100 text-slate-800 border-slate-300 dark:bg-slate-700 dark:text-slate-100 dark:border-slate-600';
    }

    function barDual(label, aPct, bPct, colorA, colorB, legendA, legendB) {
        var pa = Math.min(100, Math.max(0, Number(aPct) || 0));
        var pb = Math.min(100, Math.max(0, Number(bPct) || 0));
        return '<div class="mb-0">' +
            '<div class="text-[10px] font-bold uppercase tracking-[0.08em] text-slate-500 dark:text-slate-400 mb-3">' + escapeHtml(label) + '</div>' +
            '<div class="space-y-3.5">' +
            '<div class="flex items-center gap-3 text-xs"><span class="w-[5.25rem] shrink-0 font-medium text-slate-600 dark:text-slate-300">' + escapeHtml(legendA) + '</span>' +
            '<div class="flex-1 h-3.5 rounded-full bg-slate-100 dark:bg-slate-700/90 overflow-hidden ring-1 ring-slate-200/60 dark:ring-slate-600/50"><div class="h-full rounded-full ' + colorA + ' shadow-sm" style="width:' + pa + '%"></div></div>' +
            '<span class="tabular-nums shrink-0 w-[3.25rem] text-right font-bold text-slate-800 dark:text-slate-100">' + fmtPct(pa, 1) + '</span></div>' +
            '<div class="flex items-center gap-3 text-xs"><span class="w-[5.25rem] shrink-0 font-medium text-slate-600 dark:text-slate-300">' + escapeHtml(legendB) + '</span>' +
            '<div class="flex-1 h-3.5 rounded-full bg-slate-100 dark:bg-slate-700/90 overflow-hidden ring-1 ring-slate-200/60 dark:ring-slate-600/50"><div class="h-full rounded-full ' + colorB + ' shadow-sm" style="width:' + pb + '%"></div></div>' +
            '<span class="tabular-nums shrink-0 w-[3.25rem] text-right font-bold text-slate-800 dark:text-slate-100">' + fmtPct(pb, 1) + '</span></div>' +
            '</div></div>';
    }

    /** Bloque EV/PV legible: números grandes + referencia 100% BAC. */
    function n1ValorizadoTiles(pctEv, pctPv) {
        var ev = Number(pctEv);
        var pv = Number(pctPv);
        var evOk = !isNaN(ev);
        var pvOk = !isNaN(pv);
        var gap = (evOk && pvOk) ? (ev - pv) : null;
        var gapTxt = '';
        if (gap != null) {
            if (Math.abs(gap) < 0.05) {
                gapTxt = '<p class="mt-3 text-[11px] text-slate-600 dark:text-slate-400 font-medium">EV y PV muestran el mismo avance % respecto al BAC (trabajo valorizado alineado con lo planificado a esta fecha).</p>';
            } else if (gap >= 0) {
                gapTxt = '<p class="mt-3 text-[11px] text-emerald-700 dark:text-emerald-300 font-medium">EV supera a PV en <span class="tabular-nums">' + fmtPct(gap, 1) + '</span> — avance valorizado por delante del plan financiero.</p>';
            } else {
                gapTxt = '<p class="mt-3 text-[11px] text-amber-800 dark:text-amber-200 font-medium">EV por debajo de PV en <span class="tabular-nums">' + fmtPct(Math.abs(gap), 1) + '</span> — revisar desempeño frente al plan.</p>';
            }
        }
        function tile(title, pct, barCls, foot) {
            var p = Math.min(100, Math.max(0, Number(pct) || 0));
            return '<div class="rounded-xl border border-slate-200/80 dark:border-slate-600/80 bg-slate-50/80 dark:bg-slate-900/40 p-3 sm:p-4">' +
                '<p class="text-[10px] font-bold uppercase tracking-wide text-slate-500 dark:text-slate-400">' + escapeHtml(title) + '</p>' +
                '<p class="mt-1 text-2xl sm:text-[1.75rem] font-bold tabular-nums text-slate-900 dark:text-slate-50 tracking-tight">' + (isNaN(Number(pct)) ? '—' : fmtPct(p, 1)) + '</p>' +
                '<p class="text-[10px] text-slate-500 dark:text-slate-400 mt-0.5">' + escapeHtml(foot) + '</p>' +
                '<div class="mt-3 h-2 rounded-full bg-slate-200/90 dark:bg-slate-700 overflow-hidden">' +
                '<div class="h-full rounded-full ' + barCls + '" style="width:' + p + '%"></div></div>' +
                '<p class="text-[9px] text-slate-400 dark:text-slate-500 mt-1.5 tabular-nums">Referencia: 100% = BAC</p></div>';
        }
        return '<div class="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-4">' +
            tile('% EV / BAC', pctEv, 'bg-emerald-500', 'Trabajo valorizado ejecutado') +
            tile('% PV / BAC', pctPv, 'bg-sky-500', 'Trabajo planificado') +
            '</div>' + gapTxt;
    }

    function metricRow(k, v, vClass) {
        vClass = vClass || 'text-slate-900 dark:text-slate-100';
        return '<div class="flex justify-between items-baseline gap-3 py-2.5 border-b border-slate-100 dark:border-slate-700/80 last:border-0 text-xs leading-snug">' +
            '<span class="text-slate-600 dark:text-slate-400 pr-2 max-w-[58%]">' + escapeHtml(k) + '</span>' +
            '<span class="font-semibold tabular-nums shrink-0 text-right text-[13px] ' + vClass + '">' + v + '</span></div>';
    }

    function idxVerdict(idx) {
        var n = Number(idx);
        if (isNaN(n)) return { cls: 'bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-200', t: 'Sin dato' };
        if (n >= 1) return { cls: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-900/50 dark:text-emerald-100', t: 'Favorable' };
        return { cls: 'bg-amber-100 text-amber-950 dark:bg-amber-950/40 dark:text-amber-100', t: 'Requiere atención' };
    }

    /** Texto de estado GPR que implica brecha (alerta / no esperado). */
    function estadoIndicaBrecha(t) {
        var u = String(t || '').toUpperCase();
        if (!u.trim()) return false;
        if (u.indexOf('ROJO') >= 0) return true;
        if (u.indexOf('AMARILLO') >= 0 || u.indexOf('ALERTA') >= 0) return true;
        if (u.indexOf('CRÍTICO') >= 0 || u.indexOf('CRITICO') >= 0) return true;
        if (u.indexOf('SOBRE') >= 0 && u.indexOf('PRESUPUESTO') >= 0) return true;
        if (u.indexOf('ATRAS') >= 0) return true;
        if (u.indexOf('DEFAVORABLE') >= 0 || u.indexOf('DESFAVORABLE') >= 0) return true;
        return false;
    }

    /**
     * Escala fija 0–1,5: zona roja [0, 1), verde [1, 1,5]; marca umbral 1,00 y posición del índice.
     */
    function evmThresholdTrack(val, markerBg, markerRing) {
        var minV = 0;
        var maxV = 1.5;
        var thresh = 1;
        var n = Number(val);
        if (isNaN(n)) {
            return '<p class="text-[11px] text-slate-500 dark:text-slate-400 mt-3">Sin dato para graficar la escala.</p>';
        }
        var tPct = ((thresh - minV) / (maxV - minV)) * 100;
        var vPct = Math.min(100, Math.max(0, ((Math.min(n, maxV) - minV) / (maxV - minV)) * 100));
        var over = n > maxV;
        return '<div class="mt-4 w-full space-y-1.5">' +
            '<div class="relative h-10 rounded-lg overflow-hidden ring-1 ring-slate-300/90 dark:ring-slate-600 shadow-inner">' +
            '<div class="absolute inset-0 flex">' +
            '<div class="h-full bg-gradient-to-b from-red-200/95 to-red-300/80 dark:from-red-950/70 dark:to-red-900/50" style="width:' + tPct + '%"></div>' +
            '<div class="h-full flex-1 bg-gradient-to-b from-emerald-200/95 to-emerald-300/75 dark:from-emerald-950/55 dark:to-emerald-900/45"></div></div>' +
            '<div class="absolute top-0 bottom-0 w-0.5 bg-slate-900 dark:bg-white z-[1] shadow" style="left:' + tPct + '%;margin-left:-1px"></div>' +
            '<div class="absolute top-1/2 z-[2] -translate-y-1/2 -translate-x-1/2 w-3.5 h-3.5 rounded-full ' + markerBg + ' ' + markerRing + ' ring-2 ring-white dark:ring-slate-900 shadow-md" style="left:' + vPct + '%" title="' + escapeHtml(fmtIdx(n)) + '"></div></div>' +
            '<div class="flex justify-between text-[9px] text-slate-500 dark:text-slate-400 tabular-nums px-0.5">' +
            '<span>0</span><span class="font-bold text-slate-700 dark:text-slate-200">Umbral 1,00</span><span>1,5</span></div>' +
            (over ? '<p class="text-[9px] text-center text-sky-700 dark:text-sky-300 font-medium">Índice &gt; 1,5 (marca al tope de escala)</p>' : '') +
            '</div>';
    }

    function buildMotivosMitigacion(ind, pack) {
        var motivos = [];
        var cpi = Number(ind.cpi);
        var spi = Number(ind.spi);
        var vac = Number(ind.vac);
        var bac = Number(ind.bac) || 0;
        var eac = Number(ind.eac) || 0;
        var ev = Number(ind.pctEv);
        var pv = Number(ind.pctPv);

        if (!isNaN(cpi) && cpi < 1) {
            motivos.push('CPI &lt; 1,00 — desempeño de costo bajo la meta (sobrecosto relativo al valor ganado).');
        }
        if (!isNaN(spi) && spi < 1) {
            motivos.push('SPI &lt; 1,00 — avance valorizado por debajo del plan (retraso valorizado).');
        }
        if (!isNaN(vac) && vac < 0) {
            motivos.push('VAC negativo — variación desfavorable al término respecto al presupuesto a la conclusión (BAC).');
        }
        if (bac > 0 && !isNaN(eac) && eac > bac) {
            motivos.push('EAC mayor que BAC — costo estimado al completar supera el presupuesto total aprobado.');
        }
        if (!isNaN(ev) && !isNaN(pv) && (pv - ev) > 0.5) {
            motivos.push('Brecha EV vs PV — el % de avance valorizado (EV) va más de 0,5 pp por debajo del % planificado (PV) respecto al BAC.');
        }
        if (estadoIndicaBrecha(ind.estadoGeneral) && ind.estadoGeneral) {
            motivos.push('Estado general GPR en zona de alerta: ' + escapeHtml(String(ind.estadoGeneral)) + '.');
        }
        if (estadoIndicaBrecha(ind.estadoCosto) && ind.estadoCosto) {
            motivos.push('Estado de costo GPR en zona de alerta: ' + escapeHtml(String(ind.estadoCosto)) + '.');
        }
        if (estadoIndicaBrecha(ind.estadoCronograma) && ind.estadoCronograma) {
            motivos.push('Estado de cronograma GPR en zona de alerta: ' + escapeHtml(String(ind.estadoCronograma)) + '.');
        }
        var af = pack && pack.avanceFisico;
        if (af && af.desviacionPct != null && Number(af.desviacionPct) < -2) {
            motivos.push('Avance físico real por debajo de la línea API en más de 2 pp (brecha operativa N3).');
        }
        return motivos;
    }

    /** Brechas vistas desde el nivel táctico (PMO): índices y variaciones EVM. */
    function buildMotivosMitigacionN2(ind) {
        var motivos = [];
        if (!ind) return motivos;
        var cpi = Number(ind.cpi);
        var spi = Number(ind.spi);
        var cv = Number(ind.cv);
        var sv = Number(ind.sv);
        if (!isNaN(cpi) && cpi < 1) {
            motivos.push('CPI &lt; 1,00 — eficiencia de costo bajo la meta táctica (sobrecosto relativo al valor ganado).');
        }
        if (!isNaN(spi) && spi < 1) {
            motivos.push('SPI &lt; 1,00 — avance valorizado por debajo del plan (retraso valorizado frente a PV).');
        }
        if (!isNaN(cv) && cv < 0) {
            motivos.push('CV negativo — variación de costo desfavorable (EV − AC menor que cero).');
        }
        if (!isNaN(sv) && sv < 0) {
            motivos.push('SV negativo — variación de cronograma desfavorable (EV − PV menor que cero).');
        }
        return motivos;
    }

    /** Brechas operativas: físico declarado vs API y costo acumulado vs BAC. */
    function buildMotivosMitigacionN3(ind, pack) {
        var motivos = [];
        var af = pack && pack.avanceFisico;
        var d = af && af.desviacionPct != null ? Number(af.desviacionPct) : NaN;
        if (!isNaN(d) && d < -2) {
            motivos.push('Desvío fuerte del avance físico real frente a la línea API (brecha &gt; 2 puntos porcentuales).');
        } else if (!isNaN(d) && d < 0) {
            motivos.push('Avance físico real por debajo de la línea API — brecha operativa en terreno.');
        }
        var curva = pack && pack.curva ? pack.curva : [];
        var last = curva.length ? curva[curva.length - 1] : null;
        var bac = Number((ind && ind.bac) != null ? ind.bac : (pack && pack.bacTotalProyecto)) || 0;
        if (last && bac > 0) {
            var r = Number(last.realAcum) || 0;
            if (r / bac > 1.02) {
                motivos.push('Costo acumulado real supera al BAC en más de 2% en el último corte — riesgo de sobrecosto acumulado.');
            }
        }
        return motivos;
    }

    /**
     * Misma tarjeta verde/ámbar que N1; pieCriterios explica qué reglas se usaron en este nivel.
     * outerClass opcional (p. ej. lg:col-span-12 solo en la rejilla N1).
     */
    function renderMitigacionGPR(motivos, pieCriterios, outerClass) {
        var requiere = motivos.length > 0;
        var wrap = outerClass ? ('<div class="' + outerClass + '">') : '';
        var wrapEnd = outerClass ? '</div>' : '';
        var inner;
        if (!requiere) {
            inner = '<div class="w-full rounded-xl border border-emerald-200/90 dark:border-emerald-800/80 bg-emerald-50/50 dark:bg-emerald-950/25 px-4 py-4 sm:px-5 sm:py-4">' +
                '<div class="flex flex-wrap items-start gap-3">' +
                '<span class="material-icons text-emerald-700 dark:text-emerald-400 text-[22px] shrink-0" aria-hidden="true">verified</span>' +
                '<div class="min-w-0 flex-1">' +
                '<p class="text-[10px] font-bold uppercase tracking-[0.1em] text-emerald-800 dark:text-emerald-300">Estrategia de mitigación (GPR)</p>' +
                '<p class="text-sm font-semibold text-slate-800 dark:text-slate-100 mt-1">No se detectan brechas que exijan un plan formal de mitigación con los parámetros actuales.</p>' +
                '<p class="text-[11px] text-slate-600 dark:text-slate-400 mt-2 leading-relaxed">Mantenga el ciclo de control (tablero, KPI y comités). Si algún indicador cae bajo el umbral «Esperado» o la tendencia deja de ser aceptable, declare la brecha y active SMD con responsables y plazos.</p>' +
                (pieCriterios ? '<p class="text-[10px] text-slate-500 dark:text-slate-500 mt-2 leading-relaxed">' + escapeHtml(pieCriterios) + '</p>' : '') +
                '</div></div></div>';
        } else {
            var list = motivos.map(function (m) {
                return '<li class="text-[12px] text-slate-700 dark:text-slate-200 leading-snug pl-1">' + m + '</li>';
            }).join('');
            inner = '<div class="w-full rounded-xl border-2 border-amber-300/90 dark:border-amber-700/70 bg-amber-50/80 dark:bg-amber-950/30 px-4 py-4 sm:px-5 sm:py-4">' +
                '<div class="flex flex-wrap items-start gap-3">' +
                '<span class="material-icons text-amber-700 dark:text-amber-400 text-[24px] shrink-0" aria-hidden="true">healing</span>' +
                '<div class="min-w-0 flex-1">' +
                '<p class="text-[10px] font-bold uppercase tracking-[0.1em] text-amber-900 dark:text-amber-200">Requiere estrategia de mitigación</p>' +
                '<p class="text-sm font-semibold text-slate-900 dark:text-slate-50 mt-1">Se identificó al menos una brecha frente a parámetros de desempeño o GPR. Se recomienda activar el paso <strong class="font-bold">Mitigación (SMD)</strong>: plan de acción, responsables y plazos hasta recuperar estado aceptable.</p>' +
                '<ul class="mt-3 space-y-2 list-disc list-inside marker:text-amber-600 dark:marker:text-amber-400">' + list + '</ul>' +
                (pieCriterios ? '<p class="text-[10px] text-slate-500 dark:text-slate-400 mt-3 leading-relaxed">' + escapeHtml(pieCriterios) + '</p>' : '') +
                '</div></div></div>';
        }
        return wrap + inner + wrapEnd;
    }

    var mitigacionLevels = {};

    /**
     * Pinta una insignia (verde "verificado" / rojo "alerta") en el slot del header de la tarjeta.
     * Al hacer click abre el modal con el detalle.
     */
    function setMitigacionBadge(level, titleNivel, motivos, criterios) {
        mitigacionLevels[level] = { titleNivel: titleNivel, motivos: motivos || [], criterios: criterios || '' };
        var slot = $('pmo-res-' + level + '-mit-slot');
        if (!slot) return;
        var requiere = (motivos || []).length > 0;
        var icon = requiere ? 'gpp_maybe' : 'verified';
        var labelTxt = requiere ? 'Requiere mitigación' : 'Sin brechas';
        var btnCls = requiere
            ? 'bg-red-50 hover:bg-red-100 text-red-800 border border-red-300 dark:bg-red-950/40 dark:text-red-100 dark:border-red-700/80 dark:hover:bg-red-900/50'
            : 'bg-emerald-50 hover:bg-emerald-100 text-emerald-800 border border-emerald-300 dark:bg-emerald-950/40 dark:text-emerald-100 dark:border-emerald-700/80 dark:hover:bg-emerald-900/40';
        var dot = requiere
            ? '<span class="ml-2 inline-block h-2 w-2 rounded-full bg-red-500 animate-pulse" aria-hidden="true"></span>'
            : '<span class="ml-2 inline-block h-2 w-2 rounded-full bg-emerald-500" aria-hidden="true"></span>';
        slot.innerHTML = '<button type="button" data-pmo-mit-open="' + level + '"' +
            ' title="' + escapeHtml(labelTxt + ' — Click para ver detalle (Estrategia de mitigación GPR)') + '"' +
            ' aria-label="' + escapeHtml(labelTxt + ' — abrir detalle de mitigación GPR') + '"' +
            ' class="inline-flex items-center gap-1.5 px-2.5 py-1.5 rounded-full text-[11px] font-semibold shadow-sm transition focus:outline-none focus:ring-2 focus:ring-offset-1 focus:ring-slate-400 ' + btnCls + '">' +
            '<span class="material-icons text-[18px]" aria-hidden="true">' + icon + '</span>' +
            '<span class="hidden sm:inline">' + escapeHtml(labelTxt) + '</span>' +
            dot +
            '</button>';
    }

    function buildModalBody(state) {
        if (!state) return '';
        var motivos = state.motivos || [];
        var requiere = motivos.length > 0;
        var head;
        if (!requiere) {
            head = '<div class="rounded-xl border border-emerald-200/90 dark:border-emerald-800/80 bg-emerald-50/50 dark:bg-emerald-950/25 px-4 py-4">' +
                '<p class="text-sm font-semibold text-slate-800 dark:text-slate-100">No se detectan brechas que exijan un plan formal de mitigación con los parámetros actuales.</p>' +
                '<p class="text-[12px] text-slate-600 dark:text-slate-400 mt-2 leading-relaxed">Mantenga el ciclo de control (tablero, KPI y comités). Si algún indicador cae bajo el umbral «Esperado» o la tendencia deja de ser aceptable, declare la brecha y active SMD con responsables y plazos.</p>' +
                '</div>';
        } else {
            var list = motivos.map(function (m) {
                return '<li class="text-[13px] text-slate-700 dark:text-slate-200 leading-snug pl-1">' + m + '</li>';
            }).join('');
            head = '<div class="rounded-xl border-2 border-amber-300/90 dark:border-amber-700/70 bg-amber-50/80 dark:bg-amber-950/30 px-4 py-4">' +
                '<p class="text-sm font-semibold text-slate-900 dark:text-slate-50">Se identificó al menos una brecha frente a parámetros de desempeño o GPR. Se recomienda activar el paso <strong class="font-bold">Mitigación (SMD)</strong>: plan de acción, responsables y plazos hasta recuperar estado aceptable.</p>' +
                '<p class="text-[10px] font-bold uppercase tracking-[0.1em] text-amber-900 dark:text-amber-200 mt-3 mb-1">Brechas detectadas</p>' +
                '<ul class="space-y-2 list-disc list-inside marker:text-amber-600 dark:marker:text-amber-400">' + list + '</ul>' +
                '</div>';
        }
        var pie = state.criterios
            ? '<p class="text-[11px] text-slate-500 dark:text-slate-400 leading-relaxed border-t border-slate-200 dark:border-slate-700 pt-3"><span class="font-semibold text-slate-600 dark:text-slate-300">Criterios considerados:</span> ' + escapeHtml(state.criterios) + '</p>'
            : '';
        return head + pie;
    }

    function openMitModal(level) {
        var state = mitigacionLevels[level];
        if (!state) return;
        var modal = $('pmo-res-mit-modal');
        var titleEl = $('pmo-res-mit-modal-title');
        var iconEl = $('pmo-res-mit-modal-icon');
        var eyebrowEl = $('pmo-res-mit-modal-eyebrow');
        var bodyEl = $('pmo-res-mit-modal-body');
        var headEl = $('pmo-res-mit-modal-head');
        if (!modal || !titleEl || !iconEl || !bodyEl) return;
        var requiere = (state.motivos || []).length > 0;
        if (titleEl) titleEl.textContent = state.titleNivel || 'Detalle de mitigación';
        if (eyebrowEl) eyebrowEl.textContent = 'Estrategia de mitigación (GPR)';
        if (iconEl) {
            iconEl.textContent = requiere ? 'gpp_maybe' : 'verified';
            iconEl.className = 'material-icons text-[26px] shrink-0 ' + (requiere ? 'text-red-600 dark:text-red-400' : 'text-emerald-600 dark:text-emerald-400');
        }
        if (headEl) {
            headEl.classList.remove('bg-emerald-50/60', 'bg-red-50/60', 'dark:bg-emerald-950/30', 'dark:bg-red-950/30');
            headEl.classList.add(requiere ? 'bg-red-50/60' : 'bg-emerald-50/60', requiere ? 'dark:bg-red-950/30' : 'dark:bg-emerald-950/30');
        }
        bodyEl.innerHTML = buildModalBody(state);
        modal.classList.remove('hidden');
        modal.setAttribute('aria-hidden', 'false');
    }

    function closeMitModal() {
        var modal = $('pmo-res-mit-modal');
        if (!modal) return;
        modal.classList.add('hidden');
        modal.setAttribute('aria-hidden', 'true');
    }

    function wireMitModalOnce() {
        if (window.__pmoResMitWired) return;
        window.__pmoResMitWired = true;
        document.addEventListener('click', function (ev) {
            var t = ev.target;
            if (!t || !t.closest) return;
            var open = t.closest('[data-pmo-mit-open]');
            if (open) {
                ev.preventDefault();
                openMitModal(open.getAttribute('data-pmo-mit-open'));
                return;
            }
            if (t.closest('#pmo-res-mit-modal-close') || t.closest('#pmo-res-mit-modal-close-2')) {
                ev.preventDefault();
                closeMitModal();
                return;
            }
            var modal = $('pmo-res-mit-modal');
            if (modal && t === modal) {
                closeMitModal();
            }
        });
        document.addEventListener('keydown', function (ev) {
            if (ev.key === 'Escape') closeMitModal();
        });
    }

    function renderN1(ind, pack) {
        var el = $('pmo-res-n1-body');
        if (!el) return;
        if (!ind) {
            el.innerHTML = '<p class="text-center text-slate-500 py-6">Sin indicadores EVM en esta fecha.</p>';
            return;
        }
        var pills = '<div class="flex flex-wrap gap-2">';
        if (ind.estadoGeneral) pills += '<span class="inline-flex px-3 py-1.5 rounded-full text-[11px] font-semibold border ' + estadoPillClass(ind.estadoGeneral) + '">General · ' + escapeHtml(ind.estadoGeneral) + '</span>';
        if (ind.estadoCosto) pills += '<span class="inline-flex px-3 py-1.5 rounded-full text-[11px] font-semibold border ' + estadoPillClass(ind.estadoCosto) + '">Costo · ' + escapeHtml(ind.estadoCosto) + '</span>';
        if (ind.estadoCronograma) pills += '<span class="inline-flex px-3 py-1.5 rounded-full text-[11px] font-semibold border ' + estadoPillClass(ind.estadoCronograma) + '">Cronograma · ' + escapeHtml(ind.estadoCronograma) + '</span>';
        pills += '</div>';

        var html = '<div class="w-full grid grid-cols-1 lg:grid-cols-12 gap-5 lg:gap-6 lg:items-stretch">';
        html += '<div class="lg:col-span-3 flex flex-col gap-3 min-w-0">';
        html += '<div class="rounded-xl border border-slate-200/90 dark:border-slate-600 bg-white dark:bg-slate-800/80 p-4 shadow-sm">';
        html += '<p class="text-[10px] font-bold uppercase tracking-[0.1em] text-slate-500 dark:text-slate-400">Fecha de corte</p>';
        html += '<p class="mt-1.5 text-lg font-bold text-slate-900 dark:text-slate-50 tabular-nums tracking-tight">' + escapeHtml(ind.fechaSeguimiento || '—') + '</p>';
        html += '<p class="text-[10px] text-slate-500 dark:text-slate-400 mt-2 leading-snug">Estado semáforo GPR según cierre valorizado y desempeño.</p></div>';
        html += '<div class="rounded-xl border border-slate-200/90 dark:border-slate-600 bg-white dark:bg-slate-800/80 p-4 shadow-sm flex-1">' +
            '<p class="text-[10px] font-bold uppercase tracking-[0.1em] text-slate-500 dark:text-slate-400 mb-2.5">Lectura integrada</p>' + pills + '</div></div>';

        html += '<div class="lg:col-span-5 min-w-0 rounded-xl border border-slate-200/90 dark:border-slate-600 bg-white dark:bg-slate-800/70 p-4 sm:p-5 shadow-sm ring-1 ring-slate-100/70 dark:ring-slate-700/40">';
        html += '<div class="flex items-start justify-between gap-2 mb-1">';
        html += '<h3 class="text-[11px] font-bold uppercase tracking-[0.1em] text-[#123B6D] dark:text-sky-300">Avance valorizado vs plan</h3>';
        html += '<span class="text-[9px] font-medium text-slate-400 dark:text-slate-500 uppercase tracking-wide hidden sm:inline">EVM</span></div>';
        html += '<p class="text-[11px] text-slate-500 dark:text-slate-400 mb-4 leading-relaxed">Comparación del trabajo valorizado (EV) y planificado (PV) respecto al presupuesto aprobado (BAC).</p>';
        html += n1ValorizadoTiles(ind.pctEv, ind.pctPv);
        html += barDual('Comparativo visual (% del BAC)', ind.pctEv, ind.pctPv, 'bg-emerald-500', 'bg-sky-500', 'EV', 'PV');
        html += '</div>';

        html += '<div class="lg:col-span-4 min-w-0 rounded-xl border border-slate-200/90 dark:border-slate-600 bg-white dark:bg-slate-800/90 p-4 sm:p-5 shadow-md border-l-4 border-l-[#123B6D] dark:border-l-sky-500">';
        html += '<p class="text-[10px] font-bold uppercase tracking-[0.1em] text-slate-500 dark:text-slate-400 mb-0.5">Cierre proyectado</p>';
        html += '<p class="text-[11px] text-slate-500 dark:text-slate-400 mb-3">Proyección al término según desempeño a la fecha de corte.</p>';
        html += '<div class="space-y-0">';
        html += metricRow('VAC (variación al término)', formatUsdM(ind.vac), Number(ind.vac) >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400');
        html += metricRow('EAC (costo estimado al completar)', formatUsdM(ind.eac), 'text-orange-700 dark:text-orange-300');
        html += metricRow('BAC (presupuesto total)', formatUsdM(ind.bac), 'text-slate-800 dark:text-slate-100');
        var bac = Number(ind.bac) || 0;
        var eac = Number(ind.eac) || 0;
        if (bac > 0) html += metricRow('EAC / BAC', (eac / bac).toFixed(3), eac <= bac ? 'text-emerald-600 dark:text-emerald-400' : 'text-amber-700 dark:text-amber-300');
        html += '</div></div>';
        html += '</div>';
        el.innerHTML = html;
        setMitigacionBadge('n1', 'Nivel 1 — Estratégico / dirección',
            buildMotivosMitigacion(ind, pack || {}),
            'Criterios N1 (estratégico): CPI/SPI vs 1,00, VAC, EAC vs BAC, brecha EV–PV, semáforos integrados y desvío físico Real−API.');
    }

    function renderN2(ind) {
        var el = $('pmo-res-n2-body');
        if (!el) return;
        if (!ind) {
            el.innerHTML = '<p class="text-center text-slate-500 py-6">Sin índices de desempeño.</p>';
            return;
        }
        var cpi = Number(ind.cpi);
        var spi = Number(ind.spi);
        var vCpi = idxVerdict(ind.cpi);
        var vSpi = idxVerdict(ind.spi);
        var ringCpi = !isNaN(cpi) && cpi >= 1 ? 'ring-2 ring-sky-400/50 dark:ring-sky-500/40' : '';
        var ringSpi = !isNaN(spi) && spi >= 1 ? 'ring-2 ring-violet-400/50 dark:ring-violet-500/40' : '';
        var html = '<div class="w-full space-y-5">';
        html += '<div class="grid grid-cols-1 sm:grid-cols-2 gap-4">';
        html += '<div class="rounded-2xl border border-slate-200/90 dark:border-slate-600 bg-white dark:bg-slate-800 p-5 sm:p-6 text-center shadow-md ' + ringCpi + '">';
        html += '<div class="flex items-center justify-center gap-2 mb-1 flex-wrap">';
        html += '<span class="text-[10px] font-bold uppercase tracking-wider text-sky-800 dark:text-sky-200">CPI</span>';
        html += '<span class="inline-flex px-2 py-0.5 rounded-full text-[10px] font-semibold ' + vCpi.cls + '">' + escapeHtml(vCpi.t) + '</span></div>';
        html += '<p class="text-[10px] text-slate-500 dark:text-slate-400 mb-2">Costo · EV ÷ AC · meta ≥ 1,00</p>';
        html += '<div class="text-3xl sm:text-[2rem] font-bold tabular-nums text-sky-900 dark:text-sky-100 leading-none">' + fmtIdx(ind.cpi) + '</div>';
        html += '<p class="text-[10px] text-slate-500 dark:text-slate-500 mt-2">Rojo: índice &lt; 1 · Verde: índice ≥ 1</p>';
        html += evmThresholdTrack(ind.cpi, 'bg-sky-600', 'ring-sky-300 dark:ring-sky-700');
        html += '<p class="text-[11px] text-slate-500 dark:text-slate-400 mt-3 font-medium">CPI &lt; 1: sobrecosto · CPI ≥ 1: menor costo relativo al valor ganado</p></div>';
        html += '<div class="rounded-2xl border border-slate-200/90 dark:border-slate-600 bg-white dark:bg-slate-800 p-5 sm:p-6 text-center shadow-md ' + ringSpi + '">';
        html += '<div class="flex items-center justify-center gap-2 mb-1 flex-wrap">';
        html += '<span class="text-[10px] font-bold uppercase tracking-wider text-violet-800 dark:text-violet-200">SPI</span>';
        html += '<span class="inline-flex px-2 py-0.5 rounded-full text-[10px] font-semibold ' + vSpi.cls + '">' + escapeHtml(vSpi.t) + '</span></div>';
        html += '<p class="text-[10px] text-slate-500 dark:text-slate-400 mb-2">Plazo · EV ÷ PV · meta ≥ 1,00</p>';
        html += '<div class="text-3xl sm:text-[2rem] font-bold tabular-nums text-violet-900 dark:text-violet-100 leading-none">' + fmtIdx(ind.spi) + '</div>';
        html += '<p class="text-[10px] text-slate-500 dark:text-slate-500 mt-2">Rojo: índice &lt; 1 · Verde: índice ≥ 1</p>';
        html += evmThresholdTrack(ind.spi, 'bg-violet-600', 'ring-violet-300 dark:ring-violet-700');
        html += '<p class="text-[11px] text-slate-500 dark:text-slate-400 mt-3 font-medium">SPI &lt; 1: retraso valorizado · SPI ≥ 1: adelanto frente al plan</p></div></div>';

        html += '<div class="rounded-xl border border-slate-200/90 dark:border-slate-600 bg-white/90 dark:bg-slate-800/60 p-4 shadow-sm">';
        html += '<p class="text-[10px] font-bold uppercase tracking-[0.1em] text-slate-500 dark:text-slate-400 mb-1">Variaciones y magnitudes</p>';
        html += '<p class="text-[11px] text-slate-500 dark:text-slate-400 mb-3">CV / SV en moneda del proyecto; porcentajes respecto a referencia EVM.</p>';
        var cvCls = Number(ind.cv) >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400';
        var svCls = Number(ind.sv) >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400';
        html += metricRow('CV (costo)', formatUsdM(ind.cv) + ' · ' + fmtPct(ind.cvPct, 1), cvCls);
        html += metricRow('SV (cronograma)', formatUsdM(ind.sv) + ' · ' + fmtPct(ind.svPct, 1), svCls);
        html += metricRow('AC / PV / EV', formatUsdM(ind.ac) + ' / ' + formatUsdM(ind.pv) + ' / ' + formatUsdM(ind.ev), 'text-slate-800 dark:text-slate-100');
        html += '</div>';
        html += '</div>';
        el.innerHTML = html;
        setMitigacionBadge('n2', 'Nivel 2 — Táctico / PMO — proyecto',
            buildMotivosMitigacionN2(ind),
            'Criterios N2 (táctico / PMO): CPI y SPI frente a 1,00; variaciones CV (costo) y SV (cronograma).');
    }

    function renderN3(ind, pack) {
        var el = $('pmo-res-n3-body');
        if (!el) return;
        var af = pack.avanceFisico || {};
        var curva = pack.curva || [];
        var last = curva.length ? curva[curva.length - 1] : null;
        var bac = Number((ind && ind.bac) != null ? ind.bac : pack.bacTotalProyecto) || 0;

        var colFis = '';
        if (af && (af.realPct != null || af.apiPct != null)) {
            colFis += '<div class="rounded-xl border border-slate-200/90 dark:border-slate-600 bg-white/90 dark:bg-slate-800/60 p-4 sm:p-5 shadow-sm ring-1 ring-slate-100/70 dark:ring-slate-700/40">';
            colFis += '<div class="flex items-center gap-2 mb-1"><span class="h-0.5 w-8 rounded-full bg-emerald-500 shrink-0"></span>';
            colFis += '<p class="text-[10px] font-bold uppercase tracking-[0.1em] text-emerald-800 dark:text-emerald-300">Avance físico declarado</p></div>';
            colFis += '<p class="text-[11px] text-slate-500 dark:text-slate-400 mb-4">Contraste entre avance real reportado y línea API (referencia reproyectada).</p>';
            colFis += barDual('Real vs línea API (%)', af.realPct, af.apiPct, 'bg-emerald-500', 'bg-rose-500', 'Real %', 'API %');
            if (af.desviacionPct != null) {
                var dc = Number(af.desviacionPct) >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400';
                var dN = Number(af.desviacionPct);
                var interp = isNaN(dN) ? '' : (dN >= 0
                    ? ' El avance físico real supera la curva API en el período.'
                    : ' El avance real está por debajo de la curva API.');
                colFis += '<div class="mt-4 rounded-lg border border-slate-200/80 dark:border-slate-600 bg-slate-50/90 dark:bg-slate-900/50 px-3 py-2.5">';
                colFis += '<p class="text-xs font-semibold ' + dc + '">Brecha Real − API: <span class="tabular-nums">' + fmtPct(af.desviacionPct, 2) + '</span></p>';
                colFis += '<p class="text-[10px] text-slate-500 dark:text-slate-400 mt-1 leading-snug">' + escapeHtml(interp) + '</p></div>';
            }
            if (af.fuente) colFis += '<p class="text-[10px] text-slate-500 dark:text-slate-400 mt-3 leading-snug border-t border-slate-100 dark:border-slate-700 pt-3"><span class="font-semibold text-slate-600 dark:text-slate-300">Fuente datos:</span> ' + escapeHtml(af.fuente) + '</p>';
            colFis += '</div>';
        }

        var colCost = '';
        if (last) {
            colCost += '<div class="rounded-xl border border-slate-200/90 dark:border-slate-600 bg-white dark:bg-slate-800/90 p-4 sm:p-5 shadow-md border-l-4 border-l-slate-400 dark:border-l-slate-500">';
            colCost += '<div class="flex items-center gap-2 mb-1"><span class="h-0.5 w-8 rounded-full bg-[#123B6D] dark:bg-sky-500 shrink-0"></span>';
            colCost += '<p class="text-[10px] font-bold uppercase tracking-[0.1em] text-slate-600 dark:text-slate-300">Costo acumulado</p></div>';
            colCost += '<p class="text-[11px] text-slate-500 dark:text-slate-400 mb-3">Corte <span class="font-semibold tabular-nums text-slate-700 dark:text-slate-200">' + escapeHtml(String(last.periodo || '').substring(0, 7)) + '</span> · curvas financieras consolidadas (MUSD).</p>';
            var r = Number(last.realAcum) || 0;
            var npc = Number(last.npcAcum) || 0;
            var api = Number(last.apiAcum) || 0;
            colCost += '<div class="space-y-0 text-xs">';
            colCost += metricRow('Real acumulado', formatUsdM(r), 'text-slate-900 dark:text-slate-50');
            colCost += metricRow('NPC acum. (reproyectado)', formatUsdM(npc), 'text-blue-700 dark:text-blue-300');
            colCost += metricRow('API acumulado', formatUsdM(api), 'text-rose-700 dark:text-rose-300');
            if (bac > 0) {
                colCost += metricRow('Real / BAC', fmtPct(100 * r / bac, 1), 'text-slate-800 dark:text-slate-100');
                colCost += metricRow('NPC / BAC', fmtPct(100 * npc / bac, 1), 'text-slate-800 dark:text-slate-100');
                colCost += metricRow('API / BAC', fmtPct(100 * api / bac, 1), 'text-slate-700 dark:text-slate-200');
            }
            colCost += '</div></div>';
        }

        var p = pack || {};
        var n3Motivos = buildMotivosMitigacionN3(ind, p);
        var n3Criterios = 'Criterios N3 (operativo / terreno): brecha física Real vs línea API; costo acumulado real frente al BAC en el último período.';
        if (!colFis && !colCost) {
            el.innerHTML = '<p class="text-center text-slate-500 py-6">Sin datos operativos en el paquete.</p>';
            setMitigacionBadge('n3', 'Nivel 3 — Operativo / terreno', n3Motivos, n3Criterios);
            return;
        }
        el.innerHTML = '<div class="w-full space-y-5">' +
            '<div class="w-full flex flex-col lg:flex-row gap-5 lg:gap-6 lg:items-stretch">' +
            (colFis ? '<div class="lg:flex-1 min-w-0">' + colFis + '</div>' : '') +
            (colCost ? '<div class="lg:flex-1 min-w-0">' + colCost + '</div>' : '') +
            '</div>' +
            '</div>';
        setMitigacionBadge('n3', 'Nivel 3 — Operativo / terreno', n3Motivos, n3Criterios);
    }

    var chartInst = null;

    function destroyChart() {
        if (chartInst) {
            chartInst.destroy();
            chartInst = null;
        }
    }

    function renderCurva(pack) {
        var canvas = $('pmo-res-curva-chart');
        if (!canvas || typeof Chart === 'undefined') return;
        var curva = pack.curva || [];
        destroyChart();
        if (!curva.length) {
            var ctx = canvas.getContext('2d');
            if (ctx) ctx.clearRect(0, 0, canvas.width, canvas.height);
            return;
        }
        var take = Math.min(curva.length, 18);
        var slice = curva.slice(-take);
        var labels = slice.map(function (p) { return String(p.periodo || '').substring(0, 7); });
        var toM = function (v) { return (Number(v) || 0) / 1e6; };
        chartInst = new Chart(canvas, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    { label: 'Real acum. (MUSD)', data: slice.map(function (p) { return toM(p.realAcum); }), borderColor: 'rgb(22 101 52)', backgroundColor: 'rgba(22,101,52,0.1)', tension: 0.25, fill: false },
                    { label: 'V0 acum.', data: slice.map(function (p) { return toM(p.v0Acum); }), borderColor: 'rgb(8 145 178)', tension: 0.25, fill: false },
                    { label: 'NPC acum.', data: slice.map(function (p) { return toM(p.npcAcum); }), borderColor: 'rgb(37 99 235)', tension: 0.25, fill: false },
                    { label: 'API acum.', data: slice.map(function (p) { return toM(p.apiAcum); }), borderColor: 'rgb(190 24 93)', tension: 0.25, fill: false }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 10 } } }
                },
                scales: {
                    x: { ticks: { maxRotation: 45, minRotation: 0, font: { size: 9 } } },
                    y: { ticks: { font: { size: 9 } }, title: { display: true, text: 'MUSD' } }
                }
            }
        });
    }

    function showMsg(text, isErr) {
        var m = $('pmo-res-msg');
        if (!m) return;
        if (!text) {
            m.classList.add('hidden');
            m.textContent = '';
            return;
        }
        m.textContent = text;
        m.classList.remove('hidden');
        m.classList.toggle('border-red-300', !!isErr);
        m.classList.toggle('bg-red-50', !!isErr);
        m.classList.toggle('text-red-900', !!isErr);
    }

    function load(getPid) {
        var pid = getPid && getPid();
        var pageBase = (window.__pmoResumenCfg && window.__pmoResumenCfg.pageBase) || '/PMO';
        var fsEl = $('pmo-res-fecha-seg');
        if (fsEl && !fsEl.value) fsEl.value = previousMonthIso();
        var fs = fsEl && fsEl.value ? fsEl.value : '';
        if (!pid) return;
        var n1 = $('pmo-res-n1-body');
        var n2 = $('pmo-res-n2-body');
        var n3 = $('pmo-res-n3-body');
        var spin = '<span class="inline-flex items-center gap-2"><span class="material-icons text-[20px] animate-spin">progress_activity</span> Cargando…</span>';
        if (n1) n1.innerHTML = spin;
        if (n2) n2.innerHTML = spin;
        if (n3) n3.innerHTML = spin;
        showMsg('');
        if (!fs) {
            showMsg('Seleccione mes de seguimiento EVM.', true);
            return;
        }
        var fechaSeg = fs.length === 7 ? fs + '-01' : fs;
        var url = pageBase + '?handler=Reporte1CurvaSEvm&proyectoId=' + encodeURIComponent(pid) + '&fechaSeguimiento=' + encodeURIComponent(fechaSeg);
        fetch(url, { headers: { Accept: 'application/json' } })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
            .then(function (x) {
                if (!x.ok) {
                    var err = (x.j && x.j.error) ? x.j.error : 'No fue posible cargar el resumen.';
                    if (n1) n1.innerHTML = '<p class="text-red-600 dark:text-red-400 text-sm text-center">' + escapeHtml(err) + '</p>';
                    if (n2) n2.innerHTML = '';
                    if (n3) n3.innerHTML = '';
                    destroyChart();
                    showMsg(err, true);
                    return;
                }
                var pack = x.j;
                if (pack.evmFechaMensaje && !pack.evmFechaOk) showMsg(pack.evmFechaMensaje, false);
                var ind = pack.indicadores;
                renderN1(ind, pack);
                renderN2(ind);
                renderN3(ind, pack);
                renderCurva(pack);
            })
            .catch(function (e) {
                var err = e.message || String(e);
                if (n1) n1.innerHTML = '<p class="text-red-600 text-sm">' + escapeHtml(err) + '</p>';
                if (n2) n2.innerHTML = '';
                if (n3) n3.innerHTML = '';
                destroyChart();
                showMsg(err, true);
            });
    }

    window.pmoResumenKpiInit = function (getPid) {
        var fsEl = $('pmo-res-fecha-seg');
        var btn = $('pmo-res-refresh');
        if (fsEl && !fsEl.value) fsEl.value = previousMonthIso();
        wireMitModalOnce();
        function go() { load(getPid); }
        if (btn) btn.addEventListener('click', go);
        if (fsEl) fsEl.addEventListener('change', go);
        go();
    };
})();
