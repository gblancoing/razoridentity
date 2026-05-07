(function () {
  "use strict";

  function byId(id) { return document.getElementById(id); }
  function asNum(v) { var n = Number(v); return Number.isFinite(n) ? n : 0; }
  function ymNow() { var d = new Date(); return d.toISOString().slice(0, 7); }
  function ymPrevious() { var d = new Date(); d.setDate(1); d.setMonth(d.getMonth() - 1); return d.toISOString().slice(0, 7); }
  function fmtMoney(v) { return new Intl.NumberFormat("es-CL", { style: "currency", currency: "USD", maximumFractionDigits: 2 }).format(asNum(v)); }
  function fmtPct(v) { return asNum(v).toFixed(2) + "%"; }

  function formatMesLargo(iso) {
    if (!iso || typeof iso !== "string") return "";
    var p = iso.split("-");
    if (p.length < 2) return iso;
    var meses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
    var m = parseInt(p[1], 10);
    if (!m || m < 1 || m > 12) return iso;
    return meses[m - 1] + " " + p[0];
  }

  var chartFin = null;
  var chartFis = null;
  var activeMain = "ef";
  var lastEficienciaData = null;

  function removeLegacyHeaderCards() {
    ["pmo-fac-title", "pmo-pred-title", "pmo-fac-subtitle", "pmo-pred-subtitle"].forEach(function (id) {
      var el = byId(id);
      if (el && el.closest && el.closest("div")) {
        var card = el.closest("div.rounded-xl");
        if (card && card.parentElement && /pmo-fac-panel-(eficiencia|predictividad)/.test(card.parentElement.id || "")) {
          card.remove();
          return;
        }
      }
      if (el) el.remove();
    });
  }

  function setHeaderContext(title, subtitle) {
    var t = byId("pmo-fac-active-title");
    var s = byId("pmo-fac-active-subtitle");
    if (t) t.textContent = title || (activeMain === "pr" ? "Predictividad" : "Eficiencia del Gasto Físico - Financiero");
    if (s) s.textContent = subtitle || "";
  }

  function setMainTab(which) {
    activeMain = which === "pr" ? "pr" : "ef";
    var pEf = byId("pmo-fac-panel-eficiencia");
    var pPr = byId("pmo-fac-panel-predictividad");
    var bEf = byId("pmo-fac-main-eficiencia");
    var bPr = byId("pmo-fac-main-predictividad");
    var isEf = activeMain === "ef";

    if (pEf) pEf.classList.toggle("hidden", !isEf);
    if (pPr) pPr.classList.toggle("hidden", isEf);

    if (bEf) bEf.className = "px-3 py-1.5 text-xs font-semibold rounded-md " + (isEf ? "bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 shadow-sm" : "text-slate-600 dark:text-slate-300");
    if (bPr) bPr.className = "px-3 py-1.5 text-xs font-semibold rounded-md " + (!isEf ? "bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 shadow-sm" : "text-slate-600 dark:text-slate-300");
    if (isEf) setHeaderContext("Eficiencia del Gasto Físico - Financiero", "");
    else setHeaderContext("Predictividad", "");
  }

  function notaClass(nota) {
    if (nota >= 4) return "text-emerald-700 dark:text-emerald-300";
    if (nota === 3) return "text-sky-700 dark:text-sky-300";
    if (nota === 2) return "text-amber-700 dark:text-amber-300";
    return "text-rose-700 dark:text-rose-300";
  }

  function eficienciaClass(v) {
    var n = asNum(v);
    if (n >= 100) return "text-emerald-700 dark:text-emerald-300";
    if (n >= 90) return "text-amber-700 dark:text-amber-300";
    return "text-rose-700 dark:text-rose-300";
  }

  function renderExecutiveRows(rows) {
    var tbody = byId("pmo-fac-ef-tbody");
    if (!tbody) return;
    tbody.innerHTML = "";
    if (!Array.isArray(rows) || rows.length === 0) {
      tbody.innerHTML = '<tr><td colspan="9" class="px-3 py-6 text-center text-sm text-slate-500">Sin datos para el período seleccionado.</td></tr>';
      return;
    }
    rows.forEach(function (r, idx) {
      var tr = document.createElement("tr");
      tr.className = idx % 2 === 0 ? "bg-white dark:bg-slate-800/80" : "bg-slate-50/50 dark:bg-slate-900/40";
      tr.innerHTML =
        '<td class="px-3 py-2 font-semibold whitespace-nowrap">' + (r.periodo || "-") + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums">' + fmtMoney(r.planV0Usd) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums text-amber-700 dark:text-amber-300 font-semibold">' + fmtMoney(r.gastoRealUsd) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums font-semibold">' + fmtPct(r.cumplimientoA) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums">' + fmtPct(r.progV0Pct) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums text-amber-700 dark:text-amber-300 font-semibold">' + fmtPct(r.avanceFisicoPct) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums font-semibold">' + fmtPct(r.cumplimientoB) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums font-semibold ' + eficienciaClass(r.eficienciaGasto) + '">' + fmtPct(r.eficienciaGasto) + "</td>" +
        '<td class="px-3 py-2 text-center font-semibold ' + notaClass(asNum(r.nota)) + '">' + asNum(r.nota).toFixed(0) + "</td>";
      tbody.appendChild(tr);
    });
  }

  function renderHistorico(rows, promedio) {
    var tbody = byId("pmo-fac-hist-tbody");
    if (!tbody) return;
    tbody.innerHTML = "";
    if (!Array.isArray(rows) || rows.length === 0) {
      tbody.innerHTML = '<tr><td colspan="9" class="px-3 py-6 text-center text-sm text-slate-500">Sin histórico mensual para el período seleccionado.</td></tr>';
      return;
    }
    rows.forEach(function (r, idx) {
      var tr = document.createElement("tr");
      tr.className = idx % 2 === 0 ? "bg-white dark:bg-slate-800/80" : "bg-slate-50/50 dark:bg-slate-900/40";
      tr.innerHTML =
        '<td class="px-3 py-2 font-semibold whitespace-nowrap">' + (r.periodo || "-") + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums text-teal-700 dark:text-teal-300">' + fmtMoney(r.planV0Usd) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums text-amber-700 dark:text-amber-300 font-semibold">' + fmtMoney(r.gastoRealUsd) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums">' + fmtPct(r.cumplimientoA) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums text-teal-700 dark:text-teal-300">' + fmtPct(r.progV0Pct) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums text-amber-700 dark:text-amber-300 font-semibold">' + fmtPct(r.avanceFisicoPct) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums">' + fmtPct(r.cumplimientoB) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums font-semibold ' + eficienciaClass(r.eficienciaGasto) + '">' + fmtPct(r.eficienciaGasto) + "</td>" +
        '<td class="px-3 py-2 text-center font-semibold ' + notaClass(asNum(r.nota)) + '">' + asNum(r.nota).toFixed(0) + "</td>";
      tbody.appendChild(tr);
    });

    if (promedio) {
      var p = document.createElement("tr");
      p.className = "bg-[#123B6D] text-white";
      p.innerHTML =
        '<td class="px-3 py-2 font-bold whitespace-nowrap"><span class="material-icons text-[18px] align-middle mr-1 opacity-90">analytics</span>PROMEDIO</td>' +
        '<td class="px-3 py-2 text-right tabular-nums font-semibold">' + fmtMoney(promedio.planV0Usd) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums font-semibold">' + fmtMoney(promedio.gastoRealUsd) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums font-semibold">' + fmtPct(promedio.cumplimientoA) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums font-semibold">' + fmtPct(promedio.progV0Pct) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums font-semibold">' + fmtPct(promedio.avanceFisicoPct) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums font-semibold">' + fmtPct(promedio.cumplimientoB) + "</td>" +
        '<td class="px-3 py-2 text-right tabular-nums font-semibold ' + eficienciaClass(promedio.eficienciaGasto) + '">' + fmtPct(promedio.eficienciaGasto) + "</td>" +
        '<td class="px-3 py-2 text-center font-bold ' + notaClass(asNum(promedio.nota)) + '">' + asNum(promedio.nota).toFixed(0) + "</td>";
      tbody.appendChild(p);
    }
  }

  function exportEficienciaHistorico() {
    if (!window.XLSX || !lastEficienciaData) return;
    var hist = Array.isArray(lastEficienciaData.historicoMensual) ? lastEficienciaData.historicoMensual : [];
    var prom = lastEficienciaData.promedioHistorico || null;
    var aoa = [];
    aoa.push(["TABLA DE DATOS HISTORICOS MENSUALES - EFICIENCIA DEL GASTO"]);
    aoa.push(["Resumen mensual de avances fisicos y financieros"]);
    aoa.push([]);
    aoa.push(["Periodo","Plan V0 USD","Gasto Real USD","Cumpl. Fin (%)","Prog. V0 (%)","Avance Fisico (%)","Cumpl. Fisico (%)","Eficien. Gasto (%)","Nota"]);
    hist.forEach(function (r) {
      aoa.push([r.periodo, asNum(r.planV0Usd), asNum(r.gastoRealUsd), asNum(r.cumplimientoA), asNum(r.progV0Pct), asNum(r.avanceFisicoPct), asNum(r.cumplimientoB), asNum(r.eficienciaGasto), asNum(r.nota)]);
    });
    if (prom) aoa.push(["PROMEDIO", asNum(prom.planV0Usd), asNum(prom.gastoRealUsd), asNum(prom.cumplimientoA), asNum(prom.progV0Pct), asNum(prom.avanceFisicoPct), asNum(prom.cumplimientoB), asNum(prom.eficienciaGasto), asNum(prom.nota)]);
    var wb = XLSX.utils.book_new();
    var ws = XLSX.utils.aoa_to_sheet(aoa);
    XLSX.utils.book_append_sheet(wb, ws, "Eficiencia");
    XLSX.writeFile(wb, "eficiencia_gasto_historico_" + (lastEficienciaData.mesCorte || "export") + ".xlsx");
  }

  async function loadEficiencia(getProjectId) {
    var pid = Number(getProjectId && getProjectId());
    if (!pid) return;
    var month = byId("pmo-fac-mes-corte");
    if (month && !month.value) month.value = ymPrevious();

    var cfg = window.__pmoFactorialCfg || {};
    var base = (cfg.pageBase || "/Pmo").replace(/\/+$/, "");
    var url = base + "?handler=FactorialEficiencia&proyectoId=" + encodeURIComponent(pid) + "&mes=" + encodeURIComponent(month ? month.value : ymPrevious());
    var st = byId("pmo-fac-status");
    if (st) st.textContent = "Cargando eficiencia...";
    try {
      var res = await fetch(url, { headers: { "Accept": "application/json" } });
      var data = await res.json();
      if (!res.ok || data.error) throw new Error(data.error || ("HTTP " + res.status));
      if (activeMain === "ef")
        setHeaderContext(data.titulo || "Eficiencia del Gasto Físico - Financiero", data.subtitulo || "");
      var ban = byId("pmo-fac-exec-banner-sub");
      if (ban) {
        var corte = data.mesCorte || "";
        ban.textContent = "Análisis de Eficiencia Presupuestaria y Ejecución Física" + (corte ? " · " + formatMesLargo(corte) : "");
      }
      lastEficienciaData = data;
      renderExecutiveRows(data.filas || []);
      renderHistorico(data.historicoMensual || [], data.promedioHistorico || null);
      if (st) st.textContent = "Mes corte: " + (data.mesCorte || (month && month.value) || "");
    } catch (err) {
      if (st) st.textContent = "Error: " + (err && err.message ? err.message : String(err));
      var banE = byId("pmo-fac-exec-banner-sub");
      if (banE) banE.textContent = "Análisis de Eficiencia Presupuestaria y Ejecución Física";
      lastEficienciaData = null;
      renderExecutiveRows([]);
      renderHistorico([], null);
    }
  }

  function renderPredictCards(resumen) {
    var el = byId("pmo-pred-cards");
    if (!el) return;
    function estadoPrecision(v) {
      var n = asNum(v);
      if (n >= 95) return "Excelente predictividad";
      if (n >= 85) return "Buena predictividad";
      if (n >= 75) return "Media predictividad";
      return "Baja predictividad";
    }
    function claseNota(nota) {
      var n = asNum(nota);
      if (n >= 5) return "text-emerald-600 dark:text-emerald-300";
      if (n >= 4) return "text-sky-600 dark:text-sky-300";
      if (n >= 3) return "text-amber-600 dark:text-amber-300";
      return "text-rose-600 dark:text-rose-300";
    }
    function claseDesv(v) {
      return asNum(v) <= 0 ? "text-emerald-600 dark:text-emerald-300" : "text-rose-600 dark:text-rose-300";
    }

    var notaFin = asNum(resumen.notaFinanciera).toFixed(0);
    var notaFis = asNum(resumen.notaFisica).toFixed(0);
    var precisionProm = asNum(resumen.precisionPromedio);
    var estadoProm = estadoPrecision(precisionProm);

    el.innerHTML =
      '<div class="rounded-xl overflow-hidden border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 shadow-sm w-full">' +
      '<div class="bg-[#123B6D] text-white px-5 py-4">' +
      '<p class="text-2xl font-bold tracking-wide text-center">PREDICTIVIDAD</p>' +
      '<p class="text-xs text-slate-200 text-center mt-1">Análisis de Proyecciones y Desviaciones</p>' +
      '</div>' +
      '<div class="overflow-x-auto">' +
      '<table class="min-w-full text-sm">' +
      '<thead class="bg-slate-100 dark:bg-slate-900 text-slate-700 dark:text-slate-200">' +
      '<tr class="text-xs uppercase tracking-wide">' +
      '<th class="px-4 py-3 text-left font-semibold">Categoría</th>' +
      '<th class="px-4 py-3 text-center font-semibold">Proyección</th>' +
      '<th class="px-4 py-3 text-center font-semibold">Real</th>' +
      '<th class="px-4 py-3 text-center font-semibold">Desviación</th>' +
      '<th class="px-4 py-3 text-center font-semibold">Precisión</th>' +
      '<th class="px-4 py-3 text-center font-semibold">Nota</th>' +
      '</tr>' +
      '</thead>' +
      '<tbody class="divide-y divide-slate-200 dark:divide-slate-700 text-slate-800 dark:text-slate-100">' +
      '<tr class="bg-white dark:bg-slate-800">' +
      '<td class="px-4 py-4 font-semibold">Financiera</td>' +
      '<td class="px-4 py-4 text-center">' +
      '<div class="font-semibold tabular-nums text-[15px]">' + fmtMoney(resumen.proyeccionFinanciera) + '</div>' +
      '<div class="text-[11px] text-slate-500 dark:text-slate-400">Interno SAP</div>' +
      '</td>' +
      '<td class="px-4 py-4 text-center">' +
      '<div class="font-semibold tabular-nums text-[15px]">' + fmtMoney(resumen.realFinanciera) + '</div>' +
      '<div class="text-[11px] text-slate-500 dark:text-slate-400">Datos Reales</div>' +
      '</td>' +
      '<td class="px-4 py-4 text-center">' +
      '<div class="font-semibold tabular-nums text-[15px] ' + claseDesv(resumen.desviacionFinancieraPct) + '">' + fmtPct(resumen.desviacionFinancieraPct) + '</div>' +
      '</td>' +
      '<td class="px-4 py-4 text-center">' +
      '<div class="font-semibold tabular-nums text-[15px]">' + fmtPct(resumen.precisionFinanciera) + '</div>' +
      '<div class="text-[11px] text-slate-500 dark:text-slate-400">Precisión</div>' +
      '</td>' +
      '<td class="px-4 py-4 text-center">' +
      '<div class="font-bold text-lg leading-none ' + claseNota(notaFin) + '">' + notaFin + '</div>' +
      '<div class="text-[11px] text-slate-500 dark:text-slate-400">' + estadoPrecision(resumen.precisionFinanciera) + '</div>' +
      '</td>' +
      '</tr>' +
      '<tr class="bg-slate-50/70 dark:bg-slate-900/40">' +
      '<td class="px-4 py-4 font-semibold">Física</td>' +
      '<td class="px-4 py-4 text-center">' +
      '<div class="font-semibold tabular-nums text-[15px]">' + fmtPct(resumen.proyeccionFisica) + '</div>' +
      '<div class="text-[11px] text-slate-500 dark:text-slate-400">Predictividad</div>' +
      '</td>' +
      '<td class="px-4 py-4 text-center">' +
      '<div class="font-semibold tabular-nums text-[15px]">' + fmtPct(resumen.realFisica) + '</div>' +
      '<div class="text-[11px] text-slate-500 dark:text-slate-400">Parcial REAL</div>' +
      '</td>' +
      '<td class="px-4 py-4 text-center">' +
      '<div class="font-semibold tabular-nums text-[15px] ' + claseDesv(resumen.desviacionFisicaPct) + '">' + fmtPct(resumen.desviacionFisicaPct) + '</div>' +
      '</td>' +
      '<td class="px-4 py-4 text-center">' +
      '<div class="font-semibold tabular-nums text-[15px]">' + fmtPct(resumen.precisionFisica) + '</div>' +
      '<div class="text-[11px] text-slate-500 dark:text-slate-400">Precisión</div>' +
      '</td>' +
      '<td class="px-4 py-4 text-center">' +
      '<div class="font-bold text-lg leading-none ' + claseNota(notaFis) + '">' + notaFis + '</div>' +
      '<div class="text-[11px] text-slate-500 dark:text-slate-400">' + estadoPrecision(resumen.precisionFisica) + '</div>' +
      '</td>' +
      '</tr>' +
      '</tbody>' +
      '</table>' +
      '</div>' +
      '<div class="px-4 py-2 border-t border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/60 flex items-center justify-between">' +
      '<span class="text-xs text-slate-500 dark:text-slate-400">Precisión promedio</span>' +
      '<span class="text-sm font-semibold text-slate-700 dark:text-slate-100">' + fmtPct(precisionProm) + ' · ' + estadoProm + '</span>' +
      '</div>' +
      '</div>';
  }

  function desviacionClassRef(v) {
    var n = asNum(v);
    if (n > 0) return "text-emerald-600 dark:text-emerald-300 font-semibold";
    if (n < 0) return "text-rose-600 dark:text-rose-300 font-semibold";
    return "text-slate-600 dark:text-slate-300 font-semibold";
  }

  function notaHistoricoClass(n) {
    var x = Math.round(asNum(n));
    if (x >= 5) return "text-emerald-600 dark:text-emerald-300";
    if (x >= 4) return "text-sky-600 dark:text-sky-300";
    if (x >= 3) return "text-amber-600 dark:text-amber-300";
    if (x >= 2) return "text-orange-600 dark:text-orange-300";
    return "text-rose-600 dark:text-rose-300";
  }

  function precisionBadgeClass(v) {
    var n = asNum(v);
    if (n >= 95) return "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300";
    if (n >= 85) return "bg-sky-100 text-sky-700 dark:bg-sky-900/40 dark:text-sky-300";
    if (n >= 75) return "bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300";
    return "bg-rose-100 text-rose-700 dark:bg-rose-900/40 dark:text-rose-300";
  }

  function exportPredictividadHistorico(data) {
    if (!window.XLSX || !data) return;
    var rows = data.historialMensual || [];
    var p = data.historialPromedios || {};
    var aoa = [];
    aoa.push(["TABLA DE DATOS HISTÓRICOS - PREDICTIVIDAD"]);
    aoa.push(["Resumen mensual de proyecciones vs valores reales"]);
    aoa.push([]);
    aoa.push([
      "Período",
      "Fís. Proyección %", "Fís. Real %", "Fís. Desv. %", "Fís. Precisión %", "Fís. Nota",
      "Fin. Proyección USD", "Fin. Real USD", "Fin. Desv. %", "Fin. Precisión %", "Fin. Nota"
    ]);
    rows.forEach(function (r) {
      aoa.push([
        r.periodoEtiqueta || r.periodoIso,
        r.fisProyeccion, r.fisReal, r.fisDesviacionPct, r.fisPrecision, r.fisNota,
        r.finProyeccion, r.finReal, r.finDesviacionPct, r.finPrecision, r.finNota
      ]);
    });
    aoa.push([
      "PROMEDIO / SUMA",
      p.fisPromedioProyeccion, p.fisPromedioReal, p.fisPromedioDesviacionPct, p.fisPromedioPrecision, p.fisPromedioNota,
      p.finSumaProyeccion, p.finSumaReal, p.finDesviacionPct, p.finPromedioPrecision, p.finPromedioNota
    ]);
    var wb = XLSX.utils.book_new();
    var ws = XLSX.utils.aoa_to_sheet(aoa);
    XLSX.utils.book_append_sheet(wb, ws, "Predictividad");
    var fn = "predictividad_historico_" + (data.hasta20 || "export") + ".xlsx";
    XLSX.writeFile(wb, fn);
  }

  function renderHistoricoPredictividad(data) {
    var host = byId("pmo-pred-historico-host");
    if (!host) return;
    window.__pmoPredHistoricoPayload = data;
    var rows = data.historialMensual || [];
    var pr = data.historialPromedios || {};
    var hasta = data.hasta20 || "";

    if (!rows.length) {
      host.innerHTML = '<div class="rounded-xl border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-800 p-6 text-center text-sm text-slate-500">Sin filas de histórico mensual para el año de corte seleccionado.</div>';
      return;
    }

    function cellNota(n, texto) {
      return '<div class="text-center px-2 py-2">' +
        '<div class="text-2xl font-bold leading-tight ' + notaHistoricoClass(n) + '">' + Math.round(asNum(n)) + '</div>' +
        '<div class="text-[10px] text-slate-500 dark:text-slate-400 mt-0.5 leading-snug">' + esc(texto || "") + '</div></div>';
    }
    function cellDesv(v) {
      var n = asNum(v);
      var icon = n > 0 ? "▲" : (n < 0 ? "▼" : "■");
      return '<span class="inline-flex items-center gap-1 ' + desviacionClassRef(n) + '">' + icon + ' ' + fmtPct(n) + '</span>';
    }
    function cellPrecision(v) {
      return '<span class="inline-flex items-center px-2 py-1 rounded-full text-xs font-semibold ' + precisionBadgeClass(v) + '">' + fmtPct(v) + '</span>';
    }
    function esc(s) {
      return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/"/g, "&quot;");
    }

    var body = rows.map(function (r, idx) {
      return '<tr class="border-b border-slate-200 dark:border-slate-700 ' + (idx % 2 === 0 ? "bg-white dark:bg-slate-800/80" : "bg-slate-50/40 dark:bg-slate-900/30") + ' hover:bg-slate-100/80 dark:hover:bg-slate-700/50 transition-colors">' +
        '<td class="px-3 py-3 font-bold text-slate-800 dark:text-slate-100 whitespace-nowrap"><span class="inline-flex items-center px-2 py-1 rounded-md bg-slate-100 dark:bg-slate-700/70">' + esc(r.periodoEtiqueta || r.periodoIso) + '</span></td>' +
        '<td class="px-2 py-3 text-center tabular-nums text-sm bg-sky-50/80 dark:bg-sky-950/30">' + fmtPct(r.fisProyeccion) + '</td>' +
        '<td class="px-2 py-3 text-center tabular-nums text-sm bg-sky-50/80 dark:bg-sky-950/30">' + fmtPct(r.fisReal) + '</td>' +
        '<td class="px-2 py-3 text-center tabular-nums text-sm bg-sky-50/80 dark:bg-sky-950/30">' + cellDesv(r.fisDesviacionPct) + '</td>' +
        '<td class="px-2 py-3 text-center tabular-nums text-sm bg-sky-50/80 dark:bg-sky-950/30">' + cellPrecision(r.fisPrecision) + '</td>' +
        '<td class="px-1 py-2 align-middle bg-sky-50/80 dark:bg-sky-950/30">' + cellNota(r.fisNota, r.fisNotaTexto) + '</td>' +
        '<td class="px-2 py-3 text-center tabular-nums text-sm bg-amber-50/70 dark:bg-amber-950/25">' + fmtMoney(r.finProyeccion) + '</td>' +
        '<td class="px-2 py-3 text-center tabular-nums text-sm bg-amber-50/70 dark:bg-amber-950/25">' + fmtMoney(r.finReal) + '</td>' +
        '<td class="px-2 py-3 text-center tabular-nums text-sm bg-amber-50/70 dark:bg-amber-950/25">' + cellDesv(r.finDesviacionPct) + '</td>' +
        '<td class="px-2 py-3 text-center tabular-nums text-sm bg-amber-50/70 dark:bg-amber-950/25">' + cellPrecision(r.finPrecision) + '</td>' +
        '<td class="px-1 py-2 align-middle bg-amber-50/70 dark:bg-amber-950/25">' + cellNota(r.finNota, r.finNotaTexto) + '</td>' +
        '</tr>';
    }).join("");

    var foot = '<tr class="bg-[#123B6D] text-white">' +
      '<td class="px-3 py-3 font-bold whitespace-nowrap"><span class="material-icons text-[18px] align-middle mr-1 opacity-90">analytics</span>PROMEDIO</td>' +
      '<td class="px-2 py-3 text-center tabular-nums font-semibold">' + fmtPct(pr.fisPromedioProyeccion) + '<div class="text-[10px] font-normal opacity-80">Promedio</div></td>' +
      '<td class="px-2 py-3 text-center tabular-nums font-semibold">' + fmtPct(pr.fisPromedioReal) + '<div class="text-[10px] font-normal opacity-80">Promedio</div></td>' +
      '<td class="px-2 py-3 text-center tabular-nums font-semibold">' + fmtPct(pr.fisPromedioDesviacionPct) + '</td>' +
      '<td class="px-2 py-3 text-center tabular-nums font-semibold">' + fmtPct(pr.fisPromedioPrecision) + '<div class="text-[10px] font-normal opacity-80">Promedio</div></td>' +
      '<td class="px-2 py-3 text-center tabular-nums font-semibold">' + asNum(pr.fisPromedioNota).toFixed(1) + '<div class="text-[10px] font-normal opacity-80">Promedio</div></td>' +
      '<td class="px-2 py-3 text-center tabular-nums font-semibold">' + fmtMoney(pr.finSumaProyeccion) + '<div class="text-[10px] font-normal opacity-80">Suma</div></td>' +
      '<td class="px-2 py-3 text-center tabular-nums font-semibold">' + fmtMoney(pr.finSumaReal) + '<div class="text-[10px] font-normal opacity-80">Suma</div></td>' +
      '<td class="px-2 py-3 text-center tabular-nums font-semibold">' + fmtPct(pr.finDesviacionPct) + '</td>' +
      '<td class="px-2 py-3 text-center tabular-nums font-semibold">' + fmtPct(pr.finPromedioPrecision) + '<div class="text-[10px] font-normal opacity-80">Promedio</div></td>' +
      '<td class="px-2 py-3 text-center tabular-nums font-semibold">' + asNum(pr.finPromedioNota).toFixed(1) + '<div class="text-[10px] font-normal opacity-80">Promedio</div></td>' +
      '</tr>';

    host.innerHTML =
      '<div class="rounded-xl overflow-hidden border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 shadow-sm w-full">' +
      '<div class="bg-gradient-to-r from-[#123B6D] to-[#0f4d8a] text-white px-4 py-4 flex flex-wrap items-center justify-between gap-3">' +
      '<div class="flex items-start gap-2 min-w-0">' +
      '<span class="material-icons text-[22px] shrink-0 mt-0.5 opacity-90">table_chart</span>' +
      '<div><p class="text-lg sm:text-xl font-bold tracking-wide">TABLA DE DATOS HISTÓRICOS - PREDICTIVIDAD</p>' +
      '<p class="text-xs text-slate-200 mt-1">Resumen mensual de proyecciones vs valores reales' + (hasta ? " · Corte " + esc(hasta) : "") + '</p></div></div>' +
      '<button type="button" id="pmo-pred-export-historico" class="inline-flex items-center gap-1.5 shrink-0 px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold shadow-sm">' +
      '<span class="material-icons text-[18px]">grid_on</span>Exportar a Excel</button></div>' +
      '<div class="overflow-x-auto max-h-[560px]">' +
      '<table class="min-w-[920px] w-full text-sm border-collapse">' +
      '<thead>' +
      '<tr class="text-slate-800 dark:text-slate-100">' +
      '<th rowspan="2" class="sticky top-0 z-20 border border-slate-200 dark:border-slate-600 bg-slate-100 dark:bg-slate-900 px-3 py-3 text-left text-xs font-bold uppercase align-middle">' +
      '<div>Período</div><div class="text-[10px] font-normal normal-case text-slate-600 dark:text-slate-400 mt-0.5">Mes/Año</div></th>' +
      '<th colspan="5" class="sticky top-0 z-20 border border-slate-200 dark:border-slate-600 bg-sky-100 dark:bg-sky-900/50 px-2 py-2 text-center text-xs font-bold uppercase tracking-wide">Avance físico (%)</th>' +
      '<th colspan="5" class="sticky top-0 z-20 border border-slate-200 dark:border-slate-600 bg-amber-100 dark:bg-amber-900/40 px-2 py-2 text-center text-xs font-bold uppercase tracking-wide">Avance financiero (USD)</th>' +
      '</tr>' +
      '<tr class="text-[11px] uppercase text-slate-700 dark:text-slate-200">' +
      '<th class="sticky top-[40px] z-10 border border-slate-200 dark:border-slate-600 bg-sky-50 dark:bg-sky-950/40 px-1 py-2 font-semibold">Proyección</th>' +
      '<th class="sticky top-[40px] z-10 border border-slate-200 dark:border-slate-600 bg-sky-50 dark:bg-sky-950/40 px-1 py-2 font-semibold">Real</th>' +
      '<th class="sticky top-[40px] z-10 border border-slate-200 dark:border-slate-600 bg-sky-50 dark:bg-sky-950/40 px-1 py-2 font-semibold">Desviación</th>' +
      '<th class="sticky top-[40px] z-10 border border-slate-200 dark:border-slate-600 bg-sky-50 dark:bg-sky-950/40 px-1 py-2 font-semibold">Precisión</th>' +
      '<th class="sticky top-[40px] z-10 border border-slate-200 dark:border-slate-600 bg-sky-50 dark:bg-sky-950/40 px-1 py-2 font-semibold">Nota</th>' +
      '<th class="sticky top-[40px] z-10 border border-slate-200 dark:border-slate-600 bg-amber-50/90 dark:bg-amber-950/35 px-1 py-2 font-semibold">Proyección</th>' +
      '<th class="sticky top-[40px] z-10 border border-slate-200 dark:border-slate-600 bg-amber-50/90 dark:bg-amber-950/35 px-1 py-2 font-semibold">Real</th>' +
      '<th class="sticky top-[40px] z-10 border border-slate-200 dark:border-slate-600 bg-amber-50/90 dark:bg-amber-950/35 px-1 py-2 font-semibold">Desviación</th>' +
      '<th class="sticky top-[40px] z-10 border border-slate-200 dark:border-slate-600 bg-amber-50/90 dark:bg-amber-950/35 px-1 py-2 font-semibold">Precisión</th>' +
      '<th class="sticky top-[40px] z-10 border border-slate-200 dark:border-slate-600 bg-amber-50/90 dark:bg-amber-950/35 px-1 py-2 font-semibold">Nota</th>' +
      '</tr>' +
      '</thead>' +
      '<tbody>' + body + foot + '</tbody>' +
      '</table></div>' +
      '<div class="px-4 py-3 bg-slate-50 dark:bg-slate-900/70 border-t border-slate-200 dark:border-slate-700 text-[11px] text-slate-600 dark:text-slate-400 leading-relaxed">' +
      '<p class="font-semibold text-slate-700 dark:text-slate-300 mb-2">Leyenda</p>' +
      '<div class="flex flex-wrap gap-x-4 gap-y-2">' +
      '<span class="inline-flex items-center gap-1.5"><span class="w-3 h-3 rounded-sm bg-sky-400"></span>Proyección / bloque físico</span>' +
      '<span class="inline-flex items-center gap-1.5"><span class="w-3 h-3 rounded-sm bg-amber-400"></span>Proyección / bloque financiero</span>' +
      '<span class="inline-flex items-center gap-1.5"><span class="text-emerald-600 font-semibold">■</span>Desviación positiva: mayor avance o gasto que lo proyectado</span>' +
      '<span class="inline-flex items-center gap-1.5"><span class="text-rose-600 font-semibold">■</span>Desviación negativa: menor avance o gasto que lo proyectado</span>' +
      '</div></div></div>';

    var exp = byId("pmo-pred-export-historico");
    if (exp) exp.addEventListener("click", function () { exportPredictividadHistorico(window.__pmoPredHistoricoPayload); });
  }

  function renderPredictChart(canvasId, data, unit) {
    var cv = byId(canvasId);
    if (!cv || !window.Chart) return null;
    return new Chart(cv.getContext("2d"), {
      type: "line",
      data: {
        labels: data.map(function (x) { return x.periodo; }),
        datasets: [
          { label: "Proyección", data: data.map(function (x) { return asNum(x.proyeccion); }), borderColor: "#2563eb", tension: 0.2 },
          { label: "Real", data: data.map(function (x) { return asNum(x.real); }), borderColor: "#16a34a", tension: 0.2 },
          { label: "Desviación %", data: data.map(function (x) { return asNum(x.desviacionPct); }), borderColor: "#dc2626", tension: 0.2, yAxisID: "y1" }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
          y: { title: { display: true, text: unit } },
          y1: { position: "right", grid: { drawOnChartArea: false }, title: { display: true, text: "%" } }
        }
      }
    });
  }

  async function loadPredictividad(getProjectId) {
    var pid = Number(getProjectId && getProjectId());
    if (!pid) return;
    var hasta = byId("pmo-pred-hasta20");
    if (hasta && !hasta.value) hasta.value = ymNow();

    var cfg = window.__pmoFactorialCfg || {};
    var base = (cfg.pageBase || "/Pmo").replace(/\/+$/, "");
    var url = base + "?handler=FactorialPredictividad&proyectoId=" + encodeURIComponent(pid) + "&hasta20=" + encodeURIComponent(hasta ? hasta.value : ymNow());
    var st = byId("pmo-pred-status");
    if (st) st.textContent = "Cargando predictividad...";
    try {
      var res = await fetch(url, { headers: { "Accept": "application/json" } });
      var data = await res.json();
      if (!res.ok || data.error) throw new Error(data.error || ("HTTP " + res.status));
      if (activeMain === "pr")
        setHeaderContext(data.titulo || "Predictividad", (data.subtitulo || "") + " · " + (data.hasta20 || "") + " · " + (data.filtroDescripcion || ""));
      renderPredictCards(data.resumen || {});
      renderHistoricoPredictividad(data);
      if (chartFin) chartFin.destroy();
      if (chartFis) chartFis.destroy();
      chartFin = renderPredictChart("pmo-pred-chart-fin", data.tendenciaFinanciera || [], "MM USD");
      chartFis = renderPredictChart("pmo-pred-chart-fis", data.tendenciaFisica || [], "%");
      if (st) st.textContent = "OK";
    } catch (err) {
      if (st) st.textContent = "Error: " + (err && err.message ? err.message : String(err));
      var hh = byId("pmo-pred-historico-host");
      if (hh) hh.innerHTML = "";
    }
  }

  function normalizeKey(k) {
    return String(k || "").trim().toLowerCase().replace(/\s+/g, "_");
  }

  function parseExcelDate(v) {
    if (v == null || v === "") return "";
    if (typeof v === "number" && v > 20000 && v < 60000) {
      var epoch = new Date(Date.UTC(1899, 11, 30));
      var d = new Date(epoch.getTime() + Math.round(v) * 86400000);
      return d.toISOString().slice(0, 10);
    }
    var s = String(v).trim();
    var m = s.match(/^(\d{2})-(\d{2})-(\d{4})$/);
    if (m) return m[3] + "-" + m[2] + "-" + m[1];
    if (/^\d{4}-\d{2}-\d{2}$/.test(s)) return s;
    var d2 = new Date(s);
    return Number.isNaN(d2.getTime()) ? "" : d2.toISOString().slice(0, 10);
  }

  function parsePct(v) {
    if (v == null || v === "") return 0;
    if (typeof v === "number") return (v > 0 && v < 1) ? v * 100 : v;
    var s = String(v).trim().replace("%", "").replace(",", ".");
    var n = Number(s);
    if (!Number.isFinite(n)) return 0;
    return (n > 0 && n < 1) ? n * 100 : n;
  }

  function mapPredictRow(row) {
    var m = {};
    Object.keys(row || {}).forEach(function (k) { m[normalizeKey(k)] = row[k]; });
    return {
      periodo_prediccion: parseExcelDate(m.periodo_prediccion),
      porcentaje_predicido: parsePct(m.porcentaje_predicido),
      periodo_cierre_real: parseExcelDate(m.periodo_cierre_real),
      valor_real_porcentaje: parsePct(m.valor_real_porcentaje)
    };
  }

  async function importPredictividad(getProjectId, file) {
    var pid = Number(getProjectId && getProjectId());
    if (!pid || !file) return;
    var st = byId("pmo-pred-status");
    if (!window.XLSX) {
      if (st) st.textContent = "No se pudo cargar la librería Excel.";
      return;
    }
    var clave = window.prompt("Ingrese clave de importación:");
    if (!clave) return;
    if (st) st.textContent = "Leyendo Excel...";

    var reader = new FileReader();
    reader.onload = async function () {
      try {
        var wb = XLSX.read(reader.result, { type: "binary" });
        var sh = wb.Sheets[wb.SheetNames[0]];
        var raw = XLSX.utils.sheet_to_json(sh, { defval: "" });
        var rows = raw.map(mapPredictRow).filter(function (r) { return r.periodo_prediccion; });
        if (!rows.length) {
          if (st) st.textContent = "No hay filas válidas para importar.";
          return;
        }

        var cfg = window.__pmoFactorialCfg || {};
        var base = (cfg.pageBase || "/Pmo").replace(/\/+$/, "");

        var vr = await fetch(base + "?handler=VerificarClaveImportacion", {
          method: "POST",
          headers: { "Content-Type": "application/json", "Accept": "application/json" },
          body: JSON.stringify({ clave: clave })
        });
        var vj = await vr.json();
        if (!vr.ok || !vj.ok) throw new Error(vj.error || "Clave inválida.");

        if (st) st.textContent = "Importando predictividad...";
        var ir = await fetch(base + "?handler=FactorialImportPredictividad", {
          method: "POST",
          headers: { "Content-Type": "application/json", "Accept": "application/json" },
          body: JSON.stringify({ proyectoId: pid, clave: clave, rows: rows })
        });
        var ij = await ir.json();
        if (!ir.ok || !ij.ok) throw new Error(ij.error || ("HTTP " + ir.status));

        if (st) st.textContent = (ij.message || "Importación OK") + " · Insertadas: " + asNum(ij.inserted);
        loadPredictividad(getProjectId);
      } catch (e) {
        if (st) st.textContent = "Error importación: " + (e && e.message ? e.message : String(e));
      }
    };
    reader.readAsBinaryString(file);
  }

  window.pmoFactorialInit = function (getProjectId) {
    removeLegacyHeaderCards();
    var bEf = byId("pmo-fac-main-eficiencia");
    var bPr = byId("pmo-fac-main-predictividad");
    var rEf = byId("pmo-fac-refresh");
    var rPr = byId("pmo-pred-refresh");
    var mEf = byId("pmo-fac-mes-corte");
    var mPr = byId("pmo-pred-hasta20");
    var bImp = byId("pmo-pred-import-btn");
    var fImp = byId("pmo-pred-file");
    var expEf = byId("pmo-fac-export-hist");

    if (bEf) bEf.addEventListener("click", function () { setMainTab("ef"); loadEficiencia(getProjectId); });
    if (bPr) bPr.addEventListener("click", function () { setMainTab("pr"); loadPredictividad(getProjectId); });
    if (rEf) rEf.addEventListener("click", function () { loadEficiencia(getProjectId); });
    if (rPr) rPr.addEventListener("click", function () { loadPredictividad(getProjectId); });
    if (mEf) mEf.addEventListener("change", function () { loadEficiencia(getProjectId); });
    if (mPr) mPr.addEventListener("change", function () { loadPredictividad(getProjectId); });
    if (bImp && fImp) bImp.addEventListener("click", function () { fImp.click(); });
    if (fImp) fImp.addEventListener("change", function (ev) {
      var file = ev.target.files && ev.target.files[0];
      if (file) importPredictividad(getProjectId, file);
      ev.target.value = "";
    });
    if (expEf) expEf.addEventListener("click", exportEficienciaHistorico);

    setMainTab("ef");
    loadEficiencia(getProjectId);
    loadPredictividad(getProjectId);
  };
})();
