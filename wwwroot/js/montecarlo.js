(function () {

    // ── Diálogos GPR (sustituyen alert / confirm nativos) ───────────────────
    var _mcDialogKeyHandler = null;

    function mcDialogTeardown(overlay) {
        if (!overlay) return;
        overlay.classList.add('hidden');
        overlay.classList.remove('flex');
        overlay.setAttribute('aria-hidden', 'true');
        document.documentElement.classList.remove('overflow-hidden');
        var back = overlay.querySelector('[data-mc-dialog-backdrop]');
        if (back) back.onclick = null;
        if (_mcDialogKeyHandler) {
            document.removeEventListener('keydown', _mcDialogKeyHandler);
            _mcDialogKeyHandler = null;
        }
    }

    function mcDialogSetVariant(variant, iconWrap) {
        if (!iconWrap) return;
        var ic = iconWrap.querySelector('.material-icons');
        var base = 'flex h-12 w-12 shrink-0 items-center justify-center rounded-xl ';
        if (variant === 'danger') {
            iconWrap.className = base + 'bg-red-100 dark:bg-red-950/50';
            if (ic) { ic.textContent = 'error_outline'; ic.className = 'material-icons text-2xl text-red-600 dark:text-red-400'; }
        } else if (variant === 'warning') {
            iconWrap.className = base + 'bg-amber-100 dark:bg-amber-950/45';
            if (ic) { ic.textContent = 'warning'; ic.className = 'material-icons text-2xl text-amber-600 dark:text-amber-400'; }
        } else {
            iconWrap.className = base + 'bg-indigo-100 dark:bg-indigo-900/40';
            if (ic) { ic.textContent = 'info'; ic.className = 'material-icons text-2xl text-indigo-600 dark:text-indigo-300'; }
        }
    }

    window.mcDialogAlert = function(message, opts) {
        opts = opts || {};
        return new Promise(function(resolve) {
            var overlay = document.getElementById('mc-dialog-overlay');
            if (!overlay) {
                try { window.alert(message); } catch (e) {}
                resolve();
                return;
            }
            var titleEl = document.getElementById('mc-dialog-title');
            var bodyEl = document.getElementById('mc-dialog-body');
            var footer = document.getElementById('mc-dialog-footer');
            var iconWrap = document.getElementById('mc-dialog-icon');
            var variant = opts.variant || 'info';
            mcDialogSetVariant(variant, iconWrap);
            if (titleEl) titleEl.textContent = opts.title || 'Monte Carlo · GPR';
            if (bodyEl) bodyEl.textContent = message;
            var okLabel = opts.confirmLabel || 'Entendido';
            if (footer) {
                footer.innerHTML = '<button type="button" id="mc-dialog-btn-primary" class="inline-flex items-center justify-center gap-1.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400 px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition-colors focus:outline-none focus:ring-2 focus:ring-indigo-400 focus:ring-offset-2 dark:focus:ring-offset-slate-800">' +
                    '<span class="material-icons text-base">check</span>' + okLabel + '</button>';
            }
            function finish() {
                mcDialogTeardown(overlay);
                resolve();
            }
            var btn = document.getElementById('mc-dialog-btn-primary');
            if (btn) btn.onclick = finish;
            var back = overlay.querySelector('[data-mc-dialog-backdrop]');
            if (back) back.onclick = finish;
            _mcDialogKeyHandler = function(ev) {
                if (ev.key === 'Escape') finish();
            };
            document.addEventListener('keydown', _mcDialogKeyHandler);
            overlay.classList.remove('hidden');
            overlay.classList.add('flex');
            overlay.setAttribute('aria-hidden', 'false');
            document.documentElement.classList.add('overflow-hidden');
            if (btn) setTimeout(function() { try { btn.focus(); } catch (e2) {} }, 50);
        });
    };

    window.mcDialogConfirm = function(message, opts) {
        opts = opts || {};
        return new Promise(function(resolve) {
            var overlay = document.getElementById('mc-dialog-overlay');
            if (!overlay) {
                resolve(window.confirm(message));
                return;
            }
            var titleEl = document.getElementById('mc-dialog-title');
            var bodyEl = document.getElementById('mc-dialog-body');
            var footer = document.getElementById('mc-dialog-footer');
            var iconWrap = document.getElementById('mc-dialog-icon');
            var variant = opts.variant || 'warning';
            mcDialogSetVariant(variant, iconWrap);
            if (titleEl) titleEl.textContent = opts.title || 'Confirmar acción';
            if (bodyEl) bodyEl.textContent = message;
            var cancelLabel = opts.cancelLabel || 'Cancelar';
            var confirmLabel = opts.confirmLabel || 'Continuar';
            var isDanger = variant === 'danger';
            var primaryCls = isDanger
                ? 'inline-flex items-center justify-center gap-1.5 rounded-xl bg-red-600 hover:bg-red-700 dark:bg-red-600 dark:hover:bg-red-500 px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition-colors focus:outline-none focus:ring-2 focus:ring-red-400 focus:ring-offset-2 dark:focus:ring-offset-slate-800'
                : 'inline-flex items-center justify-center gap-1.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400 px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition-colors focus:outline-none focus:ring-2 focus:ring-indigo-400 focus:ring-offset-2 dark:focus:ring-offset-slate-800';
            if (footer) {
                footer.innerHTML =
                    '<button type="button" id="mc-dialog-btn-cancel" class="inline-flex items-center justify-center gap-1 rounded-xl border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 px-4 py-2.5 text-sm font-medium text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-700/80 transition-colors focus:outline-none focus:ring-2 focus:ring-slate-300 dark:focus:ring-slate-500">' +
                    cancelLabel + '</button>' +
                    '<button type="button" id="mc-dialog-btn-ok" class="' + primaryCls + '">' +
                    '<span class="material-icons text-base">' + (isDanger ? 'delete_forever' : 'check_circle') + '</span>' + confirmLabel + '</button>';
            }
            function onCancel() {
                mcDialogTeardown(overlay);
                resolve(false);
            }
            function onOk() {
                mcDialogTeardown(overlay);
                resolve(true);
            }
            var bCancel = document.getElementById('mc-dialog-btn-cancel');
            var bOk = document.getElementById('mc-dialog-btn-ok');
            if (bCancel) bCancel.onclick = onCancel;
            if (bOk) bOk.onclick = onOk;
            var back = overlay.querySelector('[data-mc-dialog-backdrop]');
            if (back) back.onclick = onCancel;
            _mcDialogKeyHandler = function(ev) {
                if (ev.key === 'Escape') onCancel();
            };
            document.addEventListener('keydown', _mcDialogKeyHandler);
            overlay.classList.remove('hidden');
            overlay.classList.add('flex');
            overlay.setAttribute('aria-hidden', 'false');
            document.documentElement.classList.add('overflow-hidden');
            if (bOk) setTimeout(function() { try { bOk.focus(); } catch (e2) {} }, 50);
        });
    };

    // ── Templates de filas dinámicas ────────────────────────────────────────
    var DIST_SEL = function(prefix, i, field) {
        return '<select name="' + prefix + '[' + i + '].' + field + '" class="mc-input" style="width:115px">' +
            '<option value="Triangular">Triangular</option>' +
            '<option value="Normal">Normal</option>' +
            '<option value="PERT">PERT</option>' +
            '<option value="Uniform">Uniforme</option></select>';
    };
    var EST_SEL = function(prefix, i) {
        return '<select name="' + prefix + '[' + i + '].EstimationClass" class="mc-input" style="width:100px">' +
            '<option value="">—</option><option value="Clase 1">Clase 1</option><option value="Clase 2">Clase 2</option>' +
            '<option value="Clase 3">Clase 3</option><option value="Clase 4">Clase 4</option><option value="Clase 5">Clase 5</option></select>';
    };
    var TA_STYLE = 'width:100%;max-width:100%;min-width:0;min-height:2.75rem;resize:vertical;overflow:auto;line-height:1.4;vertical-align:top;box-sizing:border-box;overflow-wrap:break-word;word-break:break-word';
    var T = {
        risk: function(i) {
            return buildRiskEditRowFromData(i, defaultRiskData());
        },
        cost: function(i) {
            return '<tr class="data-row hover:bg-slate-50 dark:hover:bg-slate-800/50">' +
                '<td class="px-2 py-1.5"><input name="CostComponents[' + i + '].Name" type="text" placeholder="Nombre..." class="mc-input" style="min-width:130px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="CostComponents[' + i + '].BaseCost" type="number" step="any" value="0" class="mc-input" style="width:100px" /></td>' +
                '<td class="px-2 py-1.5">' + EST_SEL('CostComponents', i) + '</td>' +
                '<td class="px-2 py-1.5"><input name="CostComponents[' + i + '].MinCost" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CostComponents[' + i + '].MostLikelyCost" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CostComponents[' + i + '].MaxCost" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CostComponents[' + i + '].OpportunityProbability" type="number" step="0.01" min="0" max="1" value="0" class="mc-input" style="width:70px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CostComponents[' + i + '].OpportunityAmount" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CostComponents[' + i + '].ThreatProbability" type="number" step="0.01" min="0" max="1" value="0" class="mc-input" style="width:70px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CostComponents[' + i + '].ThreatAmount" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5">' + DIST_SEL('CostComponents', i, 'Distribution') + '</td>' +
                '<td class="px-2 py-1.5"><input name="CostComponents[' + i + '].Observations" type="text" placeholder="Notas..." class="mc-input" style="min-width:100px;width:100%" /></td>' +
                '<td class="px-2 py-1.5 text-center"><button type="button" onclick="removeRow(this,\'costs-body\',\'CostComponents\')" class="text-slate-400 hover:text-red-500 transition-colors" title="Eliminar"><span class="material-icons text-base">delete</span></button></td>' +
                '</tr>';
        },
        schedule: function(i) {
            return buildScheduleEditRowFromData(i, defaultScheduleData());
        },
        'sched-risk': function(i) {
            return '<tr class="data-row hover:bg-slate-50 dark:hover:bg-slate-800/50">' +
                '<td class="px-2 py-1.5"><input name="ScheduleRisks[' + i + '].Cause" type="text" placeholder="Causa..." class="mc-input" style="min-width:120px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="ScheduleRisks[' + i + '].RiskEvent" type="text" placeholder="Evento..." class="mc-input" style="min-width:130px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="ScheduleRisks[' + i + '].Consequence" type="text" placeholder="Consecuencia..." class="mc-input" style="min-width:110px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="ScheduleRisks[' + i + '].Probability" type="number" step="0.01" min="0" max="1" value="0.3" class="mc-input" style="width:70px" /></td>' +
                '<td class="px-2 py-1.5"><input name="ScheduleRisks[' + i + '].MinImpact" type="number" step="any" value="0" class="mc-input" style="width:80px" /></td>' +
                '<td class="px-2 py-1.5"><input name="ScheduleRisks[' + i + '].MostLikelyImpact" type="number" step="any" value="0" class="mc-input" style="width:80px" /></td>' +
                '<td class="px-2 py-1.5"><input name="ScheduleRisks[' + i + '].MaxImpact" type="number" step="any" value="0" class="mc-input" style="width:80px" /></td>' +
                '<td class="px-2 py-1.5">' + DIST_SEL('ScheduleRisks', i, 'Distribution') + '</td>' +
                '<td class="px-2 py-1.5"><input name="ScheduleRisks[' + i + '].Observations" type="text" placeholder="Notas..." class="mc-input" style="min-width:100px;width:100%" /></td>' +
                '<td class="px-2 py-1.5 text-center"><button type="button" onclick="removeRow(this,\'sched-risks-body\',\'ScheduleRisks\')" class="text-slate-400 hover:text-red-500 transition-colors" title="Eliminar"><span class="material-icons text-base">delete</span></button></td>' +
                '</tr>';
        },
        'var': function(i) {
            return '<tr class="data-row">' +
                '<td class="px-3 py-2"><input name="Assets[' + i + '].Name" type="text" placeholder="Ej: Acciones CLP" class="mc-input" style="width:100%;min-width:130px" /></td>' +
                '<td class="px-3 py-2"><input name="Assets[' + i + '].InitialValue" type="number" step="any" value="1000000" class="mc-input" style="width:120px" /></td>' +
                '<td class="px-3 py-2"><input name="Assets[' + i + '].ExpectedAnnualReturn" type="number" step="0.001" value="0.08" class="mc-input" style="width:110px" /></td>' +
                '<td class="px-3 py-2"><input name="Assets[' + i + '].AnnualVolatility" type="number" step="0.001" value="0.20" class="mc-input" style="width:110px" /></td>' +
                '<td class="px-2 py-2"><button type="button" onclick="removeRow(this,\'assets-body\',\'Assets\')" class="text-slate-400 hover:text-red-500 transition-colors" title="Eliminar"><span class="material-icons text-base">delete</span></button></td>' +
                '</tr>';
        },
        // ── Tablas editables locales de SRA (Cronograma + Riesgos Prog.) ────────
        'sra-sched': function(i) {
            return '<tr class="data-row hover:bg-slate-50 dark:hover:bg-slate-800/50">' +
                '<td class="px-2 py-1.5"><input name="SraSched[' + i + '].Name" type="text" placeholder="Tarea..." class="mc-input" style="min-width:130px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraSched[' + i + '].DependenciesText" type="text" placeholder="Ej: Diseño, Compras" class="mc-input" style="min-width:120px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraSched[' + i + '].MinDays" type="number" step="any" value="0" class="mc-input" style="width:75px" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraSched[' + i + '].MostLikelyDays" type="number" step="any" value="0" class="mc-input" style="width:75px" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraSched[' + i + '].MaxDays" type="number" step="any" value="0" class="mc-input" style="width:75px" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraSched[' + i + '].PlannedDays" type="number" step="any" value="0" class="mc-input" style="width:75px" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraSched[' + i + '].Opportunities" type="number" step="any" value="0" class="mc-input" style="width:75px" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraSched[' + i + '].Threats" type="number" step="any" value="0" class="mc-input" style="width:75px" /></td>' +
                '<td class="px-2 py-1.5">' + DIST_SEL('SraSched', i, 'Distribution') + '</td>' +
                '<td class="px-2 py-1.5"><input name="SraSched[' + i + '].Observations" type="text" placeholder="Notas..." class="mc-input" style="min-width:100px;width:100%" /></td>' +
                '<td class="px-2 py-1.5 text-center"><button type="button" onclick="removeRow(this,\'sra-sched-body\',\'SraSched\')" class="text-slate-400 hover:text-red-500 transition-colors" title="Eliminar"><span class="material-icons text-base">delete</span></button></td>' +
                '</tr>';
        },
        'sra-risk': function(i) {
            return '<tr class="data-row hover:bg-slate-50 dark:hover:bg-slate-800/50">' +
                '<td class="px-2 py-1.5"><input name="SraRisk[' + i + '].Cause" type="text" placeholder="Causa..." class="mc-input" style="min-width:120px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraRisk[' + i + '].RiskEvent" type="text" placeholder="Evento..." class="mc-input" style="min-width:130px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraRisk[' + i + '].Consequence" type="text" placeholder="Consecuencia..." class="mc-input" style="min-width:110px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraRisk[' + i + '].Probability" type="number" step="0.01" min="0" max="1" value="0.3" class="mc-input" style="width:70px" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraRisk[' + i + '].MinImpact" type="number" step="any" value="0" class="mc-input" style="width:80px" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraRisk[' + i + '].MostLikelyImpact" type="number" step="any" value="0" class="mc-input" style="width:80px" /></td>' +
                '<td class="px-2 py-1.5"><input name="SraRisk[' + i + '].MaxImpact" type="number" step="any" value="0" class="mc-input" style="width:80px" /></td>' +
                '<td class="px-2 py-1.5">' + DIST_SEL('SraRisk', i, 'Distribution') + '</td>' +
                '<td class="px-2 py-1.5"><input name="SraRisk[' + i + '].Observations" type="text" placeholder="Notas..." class="mc-input" style="min-width:100px;width:100%" /></td>' +
                '<td class="px-2 py-1.5 text-center"><button type="button" onclick="removeRow(this,\'sra-risk-body\',\'SraRisk\')" class="text-slate-400 hover:text-red-500 transition-colors" title="Eliminar"><span class="material-icons text-base">delete</span></button></td>' +
                '</tr>';
        },
        // ── Tablas editables locales de CRA (Costos + Riesgos) ─────────────────
        'cra-cost': function(i) {
            return '<tr class="data-row hover:bg-slate-50 dark:hover:bg-slate-800/50">' +
                '<td class="px-2 py-1.5"><input name="CraCost[' + i + '].Name" type="text" placeholder="Nombre..." class="mc-input" style="min-width:130px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraCost[' + i + '].BaseCost" type="number" step="any" value="0" class="mc-input" style="width:100px" /></td>' +
                '<td class="px-2 py-1.5">' + EST_SEL('CraCost', i) + '</td>' +
                '<td class="px-2 py-1.5"><input name="CraCost[' + i + '].MinCost" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraCost[' + i + '].MostLikelyCost" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraCost[' + i + '].MaxCost" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraCost[' + i + '].OpportunityProbability" type="number" step="0.01" min="0" max="1" value="0" class="mc-input" style="width:70px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraCost[' + i + '].OpportunityAmount" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraCost[' + i + '].ThreatProbability" type="number" step="0.01" min="0" max="1" value="0" class="mc-input" style="width:70px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraCost[' + i + '].ThreatAmount" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5">' + DIST_SEL('CraCost', i, 'Distribution') + '</td>' +
                '<td class="px-2 py-1.5"><input name="CraCost[' + i + '].Observations" type="text" placeholder="Notas..." class="mc-input" style="min-width:100px;width:100%" /></td>' +
                '<td class="px-2 py-1.5 text-center"><button type="button" onclick="removeRow(this,\'cra-cost-body\',\'CraCost\')" class="text-slate-400 hover:text-red-500 transition-colors" title="Eliminar"><span class="material-icons text-base">delete</span></button></td>' +
                '</tr>';
        },
        'cra-risk': function(i) {
            return '<tr class="data-row hover:bg-slate-50 dark:hover:bg-slate-800/50">' +
                '<td class="px-2 py-1.5"><input name="CraRisk[' + i + '].Cause" type="text" placeholder="Causa..." class="mc-input" style="min-width:120px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraRisk[' + i + '].RiskEvent" type="text" placeholder="Evento..." class="mc-input" style="min-width:130px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraRisk[' + i + '].Consequence" type="text" placeholder="Consecuencia..." class="mc-input" style="min-width:110px;width:100%" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraRisk[' + i + '].Probability" type="number" step="0.01" min="0" max="1" value="0.5" class="mc-input" style="width:70px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraRisk[' + i + '].MinImpact" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraRisk[' + i + '].MostLikelyImpact" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5"><input name="CraRisk[' + i + '].MaxImpact" type="number" step="any" value="0" class="mc-input" style="width:90px" /></td>' +
                '<td class="px-2 py-1.5">' + DIST_SEL('CraRisk', i, 'Distribution') + '</td>' +
                '<td class="px-2 py-1.5"><input name="CraRisk[' + i + '].Observations" type="text" placeholder="Notas..." class="mc-input" style="min-width:100px;width:100%" /></td>' +
                '<td class="px-2 py-1.5 text-center"><button type="button" onclick="removeRow(this,\'cra-risk-body\',\'CraRisk\')" class="text-slate-400 hover:text-red-500 transition-colors" title="Eliminar"><span class="material-icons text-base">delete</span></button></td>' +
                '</tr>';
        }
    };

    window.addRow = function(bodyId, prefix, type) {
        var body = document.getElementById(bodyId);
        var count = body.querySelectorAll('.data-row').length;
        body.insertAdjacentHTML('beforeend', T[type](count));
    };

    window.removeRow = function(btn, bodyId, prefix) {
        var body = document.getElementById(bodyId);
        if (body.querySelectorAll('.data-row').length <= 1) return;
        var row = btn.closest('.data-row');
        var remIdx = -1;
        if (bodyId === 'risks-body' || bodyId === 'schedule-body') {
            var all = body.querySelectorAll('.data-row');
            for (var ri = 0; ri < all.length; ri++) {
                if (all[ri] === row) { remIdx = ri; break; }
            }
        }
        row.remove();
        body.querySelectorAll('.data-row').forEach(function(row, i) {
            row.querySelectorAll('input[name], select[name], textarea[name]').forEach(function(inp) {
                inp.name = inp.name.replace(/\[\d+\]/, '[' + i + ']');
            });
            var numEl = row.querySelector('.card-num');
            if (numEl) {
                var lbl = row.dataset.cardLabel || 'Item';
                numEl.textContent = lbl + ' #' + (i + 1);
            }
        });
        if (bodyId === 'risks-body') {
            var ed = window._mcRiskEditingRowIndex;
            if (ed !== null && ed !== undefined) {
                if (ed === remIdx) window._mcRiskEditingRowIndex = null;
                else if (ed > remIdx) window._mcRiskEditingRowIndex = ed - 1;
            }
            if (typeof window.mcRefreshRiskRowsDisplay === 'function') window.mcRefreshRiskRowsDisplay();
            else updateRisksVeTotal();
        } else if (bodyId === 'schedule-body') {
            var edS = window._mcScheduleEditingRowIndex;
            if (edS !== null && edS !== undefined) {
                if (edS === remIdx) window._mcScheduleEditingRowIndex = null;
                else if (edS > remIdx) window._mcScheduleEditingRowIndex = edS - 1;
            }
            if (typeof window.mcRefreshScheduleRowsDisplay === 'function') window.mcRefreshScheduleRowsDisplay();
        }
    };

    // ── Tabla de Probabilidad de Ocurrencia CODELCO ──────────────────────────
    var CODELCO_LEVELS = [
        { nivel: 5, cualitativo: 'Casi Seguro',   min: 75,  max: 100, rep: 90,   color: '#DC2626', textColor: '#fff' },
        { nivel: 4, cualitativo: 'Muy Probable',  min: 50,  max: 75,  rep: 62.5, color: '#F97316', textColor: '#fff' },
        { nivel: 3, cualitativo: 'Probable',      min: 30,  max: 50,  rep: 40,   color: '#FACC15', textColor: '#000' },
        { nivel: 2, cualitativo: 'Poco Probable', min: 10,  max: 30,  rep: 20,   color: '#60A5FA', textColor: '#fff' },
        { nivel: 1, cualitativo: 'Remoto',        min: 0,   max: 10,  rep: 5,    color: '#9CA3AF', textColor: '#fff' }
    ];

    function getCodelcoLevel(pct) {
        for (var i = 0; i < CODELCO_LEVELS.length; i++) {
            var l = CODELCO_LEVELS[i];
            if (pct > l.min && pct <= l.max) return l;
        }
        return CODELCO_LEVELS[CODELCO_LEVELS.length - 1]; // fallback → Remoto (0%)
    }

    function codelcoBadgeHtml(pct) {
        var l = getCodelcoLevel(pct);
        return '<span class="risk-prob-badge inline-flex items-center gap-0.5 px-1 py-px rounded text-xs font-semibold whitespace-nowrap leading-tight" ' +
               'style="background:' + l.color + ';color:' + l.textColor + ';font-size:10px">' +
               l.nivel + ' ' + l.cualitativo + '</span>';
    }

    // ── Formato de impactos con separador de miles (1.000.000) ──────────────────
    function parseImpact(s) {
        if (typeof s === 'number') return s;
        return parseFloat(String(s).replace(/\./g, '').replace(',', '.')) || 0;
    }
    function fmtImpact(v) {
        var n = Math.round(parseFloat(String(v).replace(/\./g, '').replace(',', '.')) || 0);
        if (isNaN(n)) return '0';
        var neg = n < 0;
        var result = Math.abs(n).toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.');
        return neg ? '-' + result : result;
    }
    // Formatea todos los inputs de impacto en la tabla Riesgos
    window.formatAllRisksImpacts = function() {
        document.querySelectorAll('#risks-body .risk-impact-input').forEach(function(el) {
            var n = parseImpact(el.value);
            el.value = fmtImpact(n);
        });
    };
    // Eventos de formato: al entrar al campo se muestran dígitos puros, al salir se formatea
    document.addEventListener('focus', function(e) {
        if (e.target && e.target.classList.contains('risk-impact-input')) {
            e.target.value = String(parseImpact(e.target.value) || '').replace(/^0$/, '0');
            e.target.select();
        }
    }, true);
    document.addEventListener('blur', function(e) {
        if (e.target && e.target.classList.contains('risk-impact-input')) {
            e.target.value = fmtImpact(parseImpact(e.target.value));
        }
    }, true);

    // ── Valor Esperado analítico por fila: p × (min+moda+max)/3 ────────────────
    function fmtVe(v) {
        if (v === null || v === undefined || isNaN(v)) return '$0';
        var s = Math.round(Math.abs(v)).toLocaleString('es-CL');
        return (v < 0 ? '-$' : '$') + s;
    }

    window.calcRiskVE = function(inputEl) {
        var row = inputEl ? inputEl.closest('.data-row') : null;
        if (!row) return;
        var get = function(suffix) {
            var el = row.querySelector('[name$=".' + suffix + '"]');
            if (!el) return 0;
            if (suffix === 'MinImpact' || suffix === 'MostLikelyImpact' || suffix === 'MaxImpact')
                return parseImpact(el.value);
            return el.classList && el.classList.contains('risk-impact-input') ? parseImpact(el.value) : (parseFloat(el.value) || 0);
        };
        var prob = get('Probability');
        var minV = get('MinImpact');
        var moda = get('MostLikelyImpact');
        var maxV = get('MaxImpact');
        var ve   = (prob / 100.0) * ((minV + moda + maxV) / 3.0);
        var tipoEl = row.querySelector('[name$=".Tipo"]');
        if (tipoEl && tipoEl.value === 'Oportunidad') ve = -Math.abs(ve);

        // Actualizar badge de nivel CODELCO
        var badge = row.querySelector('.risk-prob-badge');
        if (badge) {
            var lvl = getCodelcoLevel(prob);
            badge.textContent = lvl.nivel + ' ' + lvl.cualitativo;
            badge.style.background = lvl.color;
            badge.style.color      = lvl.textColor;
        }

        // Actualizar celda Valor Esperado
        var cell = row.querySelector('.risk-ve-val');
        if (cell) {
            cell.dataset.ve  = ve;
            cell.textContent = fmtVe(ve);
            cell.className   = 'risk-ve-val text-xs font-semibold break-all ' + (
                ve < -1  ? 'text-emerald-600 dark:text-emerald-400' :
                ve >  1  ? 'text-red-600 dark:text-red-400' :
                           'text-slate-400 dark:text-slate-500');
        }
        updateRisksVeTotal();
    };

    function updateRisksVeTotal() {
        var total = 0;
        document.querySelectorAll('#risks-body .risk-ve-val').forEach(function(cell) {
            total += parseFloat(cell.dataset.ve) || 0;
        });
        var el = document.getElementById('risks-ve-total');
        if (!el) return;
        el.textContent = fmtVe(total);
        el.className = 'px-2 py-2 text-right font-bold text-sm ' + (
            total < -1  ? 'text-emerald-600 dark:text-emerald-400' :
            total >  1  ? 'text-red-600 dark:text-red-400' :
                          'text-slate-500 dark:text-slate-400');
    }

    // Recalcula todas las filas (llamar tras restaurar desde storage)
    window.recalcAllRisksVe = function() {
        var body = document.getElementById('risks-body');
        if (!body) return;
        body.querySelectorAll('.data-row').forEach(function(row) {
            var p = row.querySelector('[name$=".Probability"]');
            if (p) window.calcRiskVE(p);
        });
    };

    function getRisksVeTotal() {
        var total = 0;
        document.querySelectorAll('#risks-body .risk-ve-val').forEach(function(cell) {
            total += parseFloat(cell.dataset.ve) || 0;
        });
        return total;
    }

    // ── Monte Carlo — Riesgos: vista lectura + una fila en edición ────────────
    window._mcRiskEditingRowIndex = null;

    function defaultRiskData() {
        return {
            Tipo: 'Amenaza', Origin: '', Code: '', Description: '', Cause: '', ResponsePlan: '',
            Probability: '50', MinImpact: '0', MostLikelyImpact: '0', MaxImpact: '0',
            Distribution: 'Triangular', EstimationBase: '', Opportunity: '', Threat: ''
        };
    }

    function mcEscAttr(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/\r?\n/g, ' ');
    }

    function mcEscHtml(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    function mcEscTextareaBody(s) {
        return String(s == null ? '' : s).replace(/<\/textarea/gi, '<\\/textarea');
    }

    function mcGetRiskDataFromRow(row) {
        function get(field) {
            var el = row.querySelector('[name$=".' + field + '"]');
            return el ? el.value : '';
        }
        return {
            Tipo: get('Tipo') || 'Amenaza',
            Origin: get('Origin'),
            Code: get('Code'),
            Description: get('Description'),
            Cause: get('Cause'),
            ResponsePlan: get('ResponsePlan'),
            Probability: String(get('Probability') !== '' ? get('Probability') : '50'),
            MinImpact: get('MinImpact'),
            MostLikelyImpact: get('MostLikelyImpact'),
            MaxImpact: get('MaxImpact'),
            Distribution: get('Distribution') || 'Triangular',
            EstimationBase: get('EstimationBase'),
            Opportunity: get('Opportunity'),
            Threat: get('Threat')
        };
    }

    function mcDistribLabel(dist) {
        var x = dist || 'Triangular';
        return x === 'Uniform' ? 'Uniforme' : x;
    }

    function mcCalcVeFromData(d) {
        var prob = parseFloat(d.Probability) || 0;
        var minV = parseImpact(d.MinImpact);
        var moda = parseImpact(d.MostLikelyImpact);
        var maxV = parseImpact(d.MaxImpact);
        var ve = (prob / 100.0) * ((minV + moda + maxV) / 3.0);
        if ((d.Tipo || '') === 'Oportunidad') ve = -Math.abs(ve);
        return ve;
    }

    function mcRiskExtraStyle(minW) {
        var hide = !window._tcRiskExtraVisible;
        return 'vertical-align:top;min-width:' + minW + 'px' + (hide ? ';display:none' : '');
    }

    function buildRiskReadCellText(txt, maxLen) {
        var raw = String(txt || '').trim();
        if (!raw) return '<span class="text-slate-400 dark:text-slate-600">—</span>';
        var show = maxLen && raw.length > maxLen ? raw.slice(0, maxLen) + '…' : raw;
        return '<span class="mc-risk-read-cell text-xs text-slate-800 dark:text-slate-200 whitespace-pre-wrap break-words" title="' + mcEscAttr(raw) + '">' + mcEscHtml(show) + '</span>';
    }

    function buildRiskReadRowFromData(i, d) {
        var ve = mcCalcVeFromData(d);
        var prob = parseFloat(d.Probability) || 0;
        var veCls = ve < -1 ? 'text-emerald-600 dark:text-emerald-400' :
            ve > 1 ? 'text-red-600 dark:text-red-400' :
                'text-slate-400 dark:text-slate-500';
        var tipoOpo = (d.Tipo || 'Amenaza') === 'Oportunidad';
        var tipoBadge = tipoOpo
            ? '<span class="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-xs font-bold bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300">✦ Oportunidad</span>'
            : '<span class="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-xs font-bold bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300">⚠ Amenaza</span>';
        var mi = fmtImpact(parseImpact(d.MinImpact));
        var mp = fmtImpact(parseImpact(d.MostLikelyImpact));
        var mx = fmtImpact(parseImpact(d.MaxImpact));
        var dist = d.Distribution || 'Triangular';
        return '<tr class="data-row align-top">' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="Risks[' + i + '].Tipo" value="' + mcEscAttr(d.Tipo || 'Amenaza') + '" />' +
                tipoBadge +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="Risks[' + i + '].Origin" value="' + mcEscAttr(d.Origin) + '" />' +
                buildRiskReadCellText(d.Origin, null) +
            '</td>' +
            '<td class="px-1 py-1.5 font-mono align-top min-w-0 text-xs">' +
                '<input type="hidden" name="Risks[' + i + '].Code" value="' + mcEscAttr(d.Code) + '" />' +
                buildRiskReadCellText(d.Code, null) +
            '</td>' +
            '<td class="px-1.5 py-1.5 align-top min-w-0">' +
                '<textarea name="Risks[' + i + '].Description" style="display:none" tabindex="-1" aria-hidden="true">' + mcEscTextareaBody(d.Description) + '</textarea>' +
                buildRiskReadCellText(d.Description, null) +
            '</td>' +
            '<td class="px-1.5 py-1.5 align-top min-w-0">' +
                '<textarea name="Risks[' + i + '].Cause" style="display:none" tabindex="-1" aria-hidden="true">' + mcEscTextareaBody(d.Cause) + '</textarea>' +
                buildRiskReadCellText(d.Cause, null) +
            '</td>' +
            '<td class="px-1.5 py-1.5 align-top min-w-0">' +
                '<textarea name="Risks[' + i + '].ResponsePlan" style="display:none" tabindex="-1" aria-hidden="true">' + mcEscTextareaBody(d.ResponsePlan) + '</textarea>' +
                buildRiskReadCellText(d.ResponsePlan, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="Risks[' + i + '].Probability" value="' + mcEscAttr(String(Math.round(prob))) + '" />' +
                '<div class="flex flex-col items-center gap-0.5">' +
                    '<span class="text-xs font-semibold text-slate-700 dark:text-slate-200">' + Math.round(prob) + '%</span>' +
                    codelcoBadgeHtml(prob) +
                '</div>' +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="Risks[' + i + '].MinImpact" value="' + mcEscAttr(mi) + '" />' +
                buildRiskReadCellText(mi, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="Risks[' + i + '].MostLikelyImpact" value="' + mcEscAttr(mp) + '" />' +
                buildRiskReadCellText(mp, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="Risks[' + i + '].MaxImpact" value="' + mcEscAttr(mx) + '" />' +
                buildRiskReadCellText(mx, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="Risks[' + i + '].Distribution" value="' + mcEscAttr(dist) + '" />' +
                '<span class="text-xs text-slate-700 dark:text-slate-300 break-words">' + mcEscHtml(mcDistribLabel(dist)) + '</span>' +
            '</td>' +
            '<td class="px-1 py-1.5 text-right risk-ve-cell align-top min-w-0">' +
                '<span class="risk-ve-val text-xs font-semibold break-all ' + veCls + '" data-ve="' + ve + '">' + fmtVe(ve) + '</span>' +
            '</td>' +
            '<td class="px-1.5 py-1.5 tc-risk-extra align-top min-w-0" style="' + mcRiskExtraStyle(120) + '">' +
                '<textarea name="Risks[' + i + '].EstimationBase" class="mc-input" style="display:none" tabindex="-1" aria-hidden="true">' + mcEscTextareaBody(d.EstimationBase) + '</textarea>' +
                buildRiskReadCellText(d.EstimationBase, null) +
            '</td>' +
            '<td class="px-0.5 py-1.5 text-center align-top w-9">' +
                '<div class="flex items-center justify-center gap-0.5">' +
                    '<button type="button" onclick="mcRiskBeginEdit(' + i + ')" class="p-1 rounded text-slate-500 hover:text-indigo-600 hover:bg-indigo-50 dark:hover:bg-indigo-900/30 transition-colors" title="Editar"><span class="material-icons text-base">edit</span></button>' +
                    '<button type="button" onclick="removeRow(this,\'risks-body\',\'Risks\')" class="p-1 text-slate-400 hover:text-red-500 transition-colors" title="Eliminar"><span class="material-icons text-base">delete</span></button>' +
                '</div>' +
            '</td>' +
            '</tr>';
    }

    function buildRiskEditRowFromData(i, d) {
        var dd = d || defaultRiskData();
        var p = parseFloat(dd.Probability);
        if (isNaN(p)) p = 50;
        var t = dd.Tipo || 'Amenaza';
        var selAm = t !== 'Oportunidad' ? ' selected' : '';
        var selOp = t === 'Oportunidad' ? ' selected' : '';
        var dist = dd.Distribution || 'Triangular';
        function distOpt(val, lab) {
            return '<option value="' + val + '"' + (dist === val ? ' selected' : '') + '>' + lab + '</option>';
        }
        var mi = fmtImpact(parseImpact(dd.MinImpact));
        var mp = fmtImpact(parseImpact(dd.MostLikelyImpact));
        var mx = fmtImpact(parseImpact(dd.MaxImpact));
        var xs = mcRiskExtraStyle;
        return '<tr class="data-row align-top">' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<select name="Risks[' + i + '].Tipo" class="mc-input risk-tipo-sel w-full min-w-0 max-w-full box-border" style="max-width:6.5rem">' +
                    '<option value="Amenaza"' + selAm + '>⚠ Amenaza</option>' +
                    '<option value="Oportunidad"' + selOp + '>✦ Oportunidad</option>' +
                '</select></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="Risks[' + i + '].Origin" type="text" placeholder="Origen..." class="mc-input w-full min-w-0 max-w-full box-border" value="' + mcEscAttr(dd.Origin) + '" /></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="Risks[' + i + '].Code" type="text" placeholder="Cód..." class="mc-input w-full min-w-0 max-w-full box-border font-mono text-xs" value="' + mcEscAttr(dd.Code) + '" /></td>' +
            '<td class="px-1.5 py-1.5 align-top min-w-0"><textarea name="Risks[' + i + '].Description" placeholder="Riesgo Proyecto..." class="mc-input mc-risk-textarea" style="' + TA_STYLE + '" rows="2" oninput="tcAutoResize(this)">' + mcEscTextareaBody(dd.Description) + '</textarea></td>' +
            '<td class="px-1.5 py-1.5 align-top min-w-0"><textarea name="Risks[' + i + '].Cause" placeholder="Titulo Riesgo..." class="mc-input mc-risk-textarea" style="' + TA_STYLE + '" rows="2" oninput="tcAutoResize(this)">' + mcEscTextareaBody(dd.Cause) + '</textarea></td>' +
            '<td class="px-1.5 py-1.5 align-top min-w-0"><textarea name="Risks[' + i + '].ResponsePlan" placeholder="Causas - (Debido a...)" class="mc-input mc-risk-textarea" style="' + TA_STYLE + '" rows="2" oninput="tcAutoResize(this)">' + mcEscTextareaBody(dd.ResponsePlan) + '</textarea></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<div class="flex flex-col items-center gap-0.5">' +
                    '<input name="Risks[' + i + '].Probability" type="number" step="1" min="0" max="100" value="' + Math.round(p) + '" class="mc-input text-center w-full max-w-[4.25rem] min-w-0 box-border" />' +
                    '<span class="risk-prob-badge inline-flex items-center gap-0.5 px-1 py-px rounded font-semibold whitespace-nowrap leading-tight" style="background:#FACC15;color:#000;font-size:10px">3 Probable</span>' +
                '</div></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="Risks[' + i + '].MinImpact" type="text" inputmode="numeric" value="' + mcEscAttr(mi) + '" class="mc-input risk-impact-input w-full min-w-0 max-w-full box-border text-xs" /></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="Risks[' + i + '].MostLikelyImpact" type="text" inputmode="numeric" value="' + mcEscAttr(mp) + '" class="mc-input risk-impact-input w-full min-w-0 max-w-full box-border text-xs" /></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="Risks[' + i + '].MaxImpact" type="text" inputmode="numeric" value="' + mcEscAttr(mx) + '" class="mc-input risk-impact-input w-full min-w-0 max-w-full box-border text-xs" /></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<select name="Risks[' + i + '].Distribution" class="mc-input w-full min-w-0 max-w-full box-border text-xs" style="max-width:5.5rem">' +
                    distOpt('Triangular', 'Triangular') + distOpt('Normal', 'Normal') + distOpt('PERT', 'PERT') + distOpt('Uniform', 'Uniforme') +
                '</select></td>' +
            '<td class="px-1 py-1.5 text-right risk-ve-cell align-top min-w-0"><span class="risk-ve-val text-xs font-semibold text-slate-400 break-all" data-ve="0">$0</span></td>' +
            '<td class="px-1.5 py-1.5 tc-risk-extra align-top min-w-0" style="' + xs(120) + '"><textarea name="Risks[' + i + '].EstimationBase" placeholder="Plan de Respuesta para resguardar costos..." class="mc-input mc-risk-textarea" style="' + TA_STYLE + '" rows="2" oninput="tcAutoResize(this)">' + mcEscTextareaBody(dd.EstimationBase) + '</textarea></td>' +
            '<td class="px-0.5 py-1.5 text-center align-top w-9">' +
                '<div class="flex items-center justify-center gap-0.5">' +
                    '<button type="button" onclick="mcRiskFinishEdit()" class="p-1 rounded text-emerald-600 hover:bg-emerald-50 dark:hover:bg-emerald-900/30 transition-colors" title="Guardar fila"><span class="material-icons text-base">check</span></button>' +
                    '<button type="button" onclick="removeRow(this,\'risks-body\',\'Risks\')" class="p-1 text-slate-400 hover:text-red-500 transition-colors" title="Eliminar"><span class="material-icons text-base">delete</span></button>' +
                '</div></td>' +
            '</tr>';
    }

    function applyMcRisksRowListeners(body, ed) {
        body.querySelectorAll('.data-row').forEach(function(row, idx) {
            if (ed !== idx) return;
            var tipoSel = row.querySelector('.risk-tipo-sel');
            if (tipoSel) {
                if (typeof window.tcUpdateRiskTipoColor === 'function') window.tcUpdateRiskTipoColor(tipoSel);
                tipoSel.addEventListener('change', function() {
                    if (typeof window.tcUpdateRiskTipoColor === 'function') window.tcUpdateRiskTipoColor(this);
                    window.calcRiskVE(this);
                });
            }
            row.querySelectorAll('[name$=".Probability"], .risk-impact-input').forEach(function(el) {
                el.addEventListener('input', function() { window.calcRiskVE(el); });
            });
            var pEl = row.querySelector('[name$=".Probability"]');
            if (pEl) window.calcRiskVE(pEl);
            row.querySelectorAll('textarea.mc-input').forEach(function(ta) {
                if (typeof window.tcAutoResize === 'function') window.tcAutoResize(ta);
            });
        });
        if (typeof window.formatAllRisksImpacts === 'function') window.formatAllRisksImpacts();
        if (typeof window.recalcAllRisksVe === 'function') window.recalcAllRisksVe();
    }

    window.mcRefreshRiskRowsDisplay = function() {
        var body = document.getElementById('risks-body');
        if (!body) return;
        var datas = [];
        body.querySelectorAll('.data-row').forEach(function(r) { datas.push(mcGetRiskDataFromRow(r)); });
        var ed = window._mcRiskEditingRowIndex;
        body.innerHTML = '';
        for (var i = 0; i < datas.length; i++) {
            body.insertAdjacentHTML('beforeend', (ed === i)
                ? buildRiskEditRowFromData(i, datas[i])
                : buildRiskReadRowFromData(i, datas[i]));
        }
        applyMcRisksRowListeners(body, ed);
    };

    window.mcRisksInitReadMode = function() {
        window._mcRiskEditingRowIndex = null;
        window.mcRefreshRiskRowsDisplay();
    };

    window.mcRiskBeginEdit = function(i) {
        window._mcRiskEditingRowIndex = i;
        window.mcRefreshRiskRowsDisplay();
    };

    window.mcRiskFinishEdit = function() {
        window._mcRiskEditingRowIndex = null;
        window.mcRefreshRiskRowsDisplay();
    };

    window.mcRisksAppendNewRow = function() {
        var body = document.getElementById('risks-body');
        if (!body) return;
        var datas = [];
        body.querySelectorAll('.data-row').forEach(function(r) { datas.push(mcGetRiskDataFromRow(r)); });
        datas.push(defaultRiskData());
        window._mcRiskEditingRowIndex = datas.length - 1;
        var ed = window._mcRiskEditingRowIndex;
        body.innerHTML = '';
        for (var j = 0; j < datas.length; j++) {
            body.insertAdjacentHTML('beforeend', (ed === j)
                ? buildRiskEditRowFromData(j, datas[j])
                : buildRiskReadRowFromData(j, datas[j]));
        }
        applyMcRisksRowListeners(body, ed);
    };

    // ── Monte Carlo — Cronograma: vista lectura + una fila en edición ─────────
    window._mcScheduleEditingRowIndex = null;

    function defaultScheduleData() {
        return {
            Name: '', DependenciesText: '',
            MinDays: '0', MostLikelyDays: '0', MaxDays: '0', PlannedDays: '0',
            Opportunities: '0', Threats: '0',
            Distribution: 'Triangular', Observations: ''
        };
    }

    function schedNumStr(v) {
        var s = String(v == null ? '' : v).trim();
        return s === '' ? '0' : s;
    }

    function mcGetScheduleDataFromRow(row) {
        function get(field) {
            var el = row.querySelector('[name$=".' + field + '"]');
            return el ? el.value : '';
        }
        return {
            Name: get('Name'),
            DependenciesText: get('DependenciesText'),
            MinDays: schedNumStr(get('MinDays')),
            MostLikelyDays: schedNumStr(get('MostLikelyDays')),
            MaxDays: schedNumStr(get('MaxDays')),
            PlannedDays: schedNumStr(get('PlannedDays')),
            Opportunities: schedNumStr(get('Opportunities')),
            Threats: schedNumStr(get('Threats')),
            Distribution: get('Distribution') || 'Triangular',
            Observations: get('Observations')
        };
    }

    function buildScheduleReadRowFromData(i, d) {
        var dist = d.Distribution || 'Triangular';
        return '<tr class="data-row align-top">' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="ScheduleTasks[' + i + '].Name" value="' + mcEscAttr(d.Name) + '" />' +
                buildRiskReadCellText(d.Name, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<textarea name="ScheduleTasks[' + i + '].DependenciesText" style="display:none" tabindex="-1" aria-hidden="true">' + mcEscTextareaBody(d.DependenciesText) + '</textarea>' +
                buildRiskReadCellText(d.DependenciesText, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="ScheduleTasks[' + i + '].MinDays" value="' + mcEscAttr(d.MinDays) + '" />' +
                buildRiskReadCellText(d.MinDays, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="ScheduleTasks[' + i + '].MostLikelyDays" value="' + mcEscAttr(d.MostLikelyDays) + '" />' +
                buildRiskReadCellText(d.MostLikelyDays, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="ScheduleTasks[' + i + '].MaxDays" value="' + mcEscAttr(d.MaxDays) + '" />' +
                buildRiskReadCellText(d.MaxDays, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="ScheduleTasks[' + i + '].PlannedDays" value="' + mcEscAttr(d.PlannedDays) + '" />' +
                buildRiskReadCellText(d.PlannedDays, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="ScheduleTasks[' + i + '].Opportunities" value="' + mcEscAttr(d.Opportunities) + '" />' +
                buildRiskReadCellText(d.Opportunities, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="ScheduleTasks[' + i + '].Threats" value="' + mcEscAttr(d.Threats) + '" />' +
                buildRiskReadCellText(d.Threats, null) +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<input type="hidden" name="ScheduleTasks[' + i + '].Distribution" value="' + mcEscAttr(dist) + '" />' +
                '<span class="text-xs text-slate-700 dark:text-slate-300 break-words">' + mcEscHtml(mcDistribLabel(dist)) + '</span>' +
            '</td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<textarea name="ScheduleTasks[' + i + '].Observations" style="display:none" tabindex="-1" aria-hidden="true">' + mcEscTextareaBody(d.Observations) + '</textarea>' +
                buildRiskReadCellText(d.Observations, null) +
            '</td>' +
            '<td class="px-0.5 py-1.5 text-center align-top w-9">' +
                '<div class="flex items-center justify-center gap-0.5">' +
                    '<button type="button" onclick="mcScheduleBeginEdit(' + i + ')" class="p-1 rounded text-slate-500 hover:text-indigo-600 hover:bg-indigo-50 dark:hover:bg-indigo-900/30 transition-colors" title="Editar"><span class="material-icons text-base">edit</span></button>' +
                    '<button type="button" onclick="removeRow(this,\'schedule-body\',\'ScheduleTasks\')" class="p-1 text-slate-400 hover:text-red-500 transition-colors" title="Eliminar"><span class="material-icons text-base">delete</span></button>' +
                '</div>' +
            '</td>' +
            '</tr>';
    }

    function buildScheduleEditRowFromData(i, d) {
        var dd = d || defaultScheduleData();
        var dist = dd.Distribution || 'Triangular';
        function distOpt(val, lab) {
            return '<option value="' + val + '"' + (dist === val ? ' selected' : '') + '>' + lab + '</option>';
        }
        return '<tr class="data-row align-top">' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="ScheduleTasks[' + i + '].Name" type="text" placeholder="Tarea..." class="mc-input w-full min-w-0 max-w-full box-border" value="' + mcEscAttr(dd.Name) + '" /></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><textarea name="ScheduleTasks[' + i + '].DependenciesText" placeholder="Dependencias..." class="mc-input mc-risk-textarea" style="' + TA_STYLE + '" rows="2" oninput="tcAutoResize(this)">' + mcEscTextareaBody(dd.DependenciesText) + '</textarea></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="ScheduleTasks[' + i + '].MinDays" type="number" step="any" value="' + mcEscAttr(dd.MinDays) + '" class="mc-input w-full min-w-0 max-w-full box-border text-xs" /></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="ScheduleTasks[' + i + '].MostLikelyDays" type="number" step="any" value="' + mcEscAttr(dd.MostLikelyDays) + '" class="mc-input w-full min-w-0 max-w-full box-border text-xs" /></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="ScheduleTasks[' + i + '].MaxDays" type="number" step="any" value="' + mcEscAttr(dd.MaxDays) + '" class="mc-input w-full min-w-0 max-w-full box-border text-xs" /></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="ScheduleTasks[' + i + '].PlannedDays" type="number" step="any" value="' + mcEscAttr(dd.PlannedDays) + '" class="mc-input w-full min-w-0 max-w-full box-border text-xs" /></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="ScheduleTasks[' + i + '].Opportunities" type="number" step="any" value="' + mcEscAttr(dd.Opportunities) + '" class="mc-input w-full min-w-0 max-w-full box-border text-xs" /></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><input name="ScheduleTasks[' + i + '].Threats" type="number" step="any" value="' + mcEscAttr(dd.Threats) + '" class="mc-input w-full min-w-0 max-w-full box-border text-xs" /></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0">' +
                '<select name="ScheduleTasks[' + i + '].Distribution" class="mc-input w-full min-w-0 max-w-full box-border text-xs" style="max-width:5.5rem">' +
                    distOpt('Triangular', 'Triangular') + distOpt('Normal', 'Normal') + distOpt('PERT', 'PERT') + distOpt('Uniform', 'Uniforme') +
                '</select></td>' +
            '<td class="px-1 py-1.5 align-top min-w-0"><textarea name="ScheduleTasks[' + i + '].Observations" placeholder="Notas..." class="mc-input mc-risk-textarea" style="' + TA_STYLE + '" rows="2" oninput="tcAutoResize(this)">' + mcEscTextareaBody(dd.Observations) + '</textarea></td>' +
            '<td class="px-0.5 py-1.5 text-center align-top w-9">' +
                '<div class="flex items-center justify-center gap-0.5">' +
                    '<button type="button" onclick="mcScheduleFinishEdit()" class="p-1 rounded text-emerald-600 hover:bg-emerald-50 dark:hover:bg-emerald-900/30 transition-colors" title="Guardar fila"><span class="material-icons text-base">check</span></button>' +
                    '<button type="button" onclick="removeRow(this,\'schedule-body\',\'ScheduleTasks\')" class="p-1 text-slate-400 hover:text-red-500 transition-colors" title="Eliminar"><span class="material-icons text-base">delete</span></button>' +
                '</div></td>' +
            '</tr>';
    }

    function applyMcScheduleRowListeners(body, ed) {
        body.querySelectorAll('.data-row').forEach(function(row, idx) {
            if (ed !== idx) return;
            row.querySelectorAll('textarea.mc-input').forEach(function(ta) {
                if (typeof window.tcAutoResize === 'function') window.tcAutoResize(ta);
            });
        });
    }

    window.mcRefreshScheduleRowsDisplay = function() {
        var body = document.getElementById('schedule-body');
        if (!body) return;
        var datas = [];
        body.querySelectorAll('.data-row').forEach(function(r) { datas.push(mcGetScheduleDataFromRow(r)); });
        var ed = window._mcScheduleEditingRowIndex;
        body.innerHTML = '';
        for (var i = 0; i < datas.length; i++) {
            body.insertAdjacentHTML('beforeend', (ed === i)
                ? buildScheduleEditRowFromData(i, datas[i])
                : buildScheduleReadRowFromData(i, datas[i]));
        }
        applyMcScheduleRowListeners(body, ed);
    };

    window.mcScheduleInitReadMode = function() {
        window._mcScheduleEditingRowIndex = null;
        window.mcRefreshScheduleRowsDisplay();
    };

    window.mcScheduleBeginEdit = function(i) {
        window._mcScheduleEditingRowIndex = i;
        window.mcRefreshScheduleRowsDisplay();
    };

    window.mcScheduleFinishEdit = function() {
        window._mcScheduleEditingRowIndex = null;
        window.mcRefreshScheduleRowsDisplay();
    };

    window.mcScheduleAppendNewRow = function() {
        var body = document.getElementById('schedule-body');
        if (!body) return;
        var datas = [];
        body.querySelectorAll('.data-row').forEach(function(r) { datas.push(mcGetScheduleDataFromRow(r)); });
        datas.push(defaultScheduleData());
        window._mcScheduleEditingRowIndex = datas.length - 1;
        var ed = window._mcScheduleEditingRowIndex;
        body.innerHTML = '';
        for (var j = 0; j < datas.length; j++) {
            body.insertAdjacentHTML('beforeend', (ed === j)
                ? buildScheduleEditRowFromData(j, datas[j])
                : buildScheduleReadRowFromData(j, datas[j]));
        }
        applyMcScheduleRowListeners(body, ed);
    };

    // ── Tarjeta de verificación analítica vs. Monte Carlo ─────────────────────
    function renderRisksVerification(s) {
        var el = document.getElementById('risks-verification-card');
        if (!el || !s) return;

        var ve   = getRisksVeTotal();
        var mean = s.mean || 0;
        var p50  = s.p50  || 0;
        var p80  = s.p80  || 0;
        var p90  = s.p90  || 0;

        // Diferencia relativa entre VE analítico y Media MC
        var diffPct = (mean !== 0) ? Math.abs((ve - mean) / mean) * 100 : 0;
        var convOk  = diffPct < 5;
        var convColor = diffPct < 5  ? 'emerald' :
                        diffPct < 10 ? 'amber'   : 'red';
        var convIcon  = diffPct < 5  ? 'check_circle' :
                        diffPct < 10 ? 'warning'      : 'error';
        var convLabel = diffPct < 5  ? 'Convergencia correcta (< 5%)' :
                        diffPct < 10 ? 'Diferencia moderada (5–10%)' :
                                       'Divergencia alta (> 10%) — revisar datos';

        // Contingencias sobre VE analítico
        var cont80 = p80 - ve;
        var cont90 = p90 - ve;
        var cont80Pct = ve !== 0 ? (cont80 / Math.abs(ve)) * 100 : 0;
        var cont90Pct = ve !== 0 ? (cont90 / Math.abs(ve)) * 100 : 0;

        el.innerHTML =
            '<div class="flex items-center gap-2 mb-3">' +
                '<span class="material-icons text-base text-indigo-500">science</span>' +
                '<span class="text-xs font-semibold text-slate-600 dark:text-slate-400 uppercase tracking-wide">Verificación Analítica vs. Simulación</span>' +
            '</div>' +
            // Convergencia
            '<div class="flex items-start gap-2 mb-3 p-2.5 rounded-lg bg-' + convColor + '-50 dark:bg-' + convColor + '-900/20 border border-' + convColor + '-200 dark:border-' + convColor + '-800">' +
                '<span class="material-icons text-base text-' + convColor + '-600 dark:text-' + convColor + '-400 flex-shrink-0 mt-0.5">' + convIcon + '</span>' +
                '<div class="min-w-0">' +
                    '<p class="text-xs font-semibold text-' + convColor + '-700 dark:text-' + convColor + '-300">' + convLabel + '</p>' +
                    '<p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">' +
                        'VE Analítico: <strong>' + fmtVe(ve) + '</strong> &nbsp;·&nbsp; ' +
                        'Media MC: <strong>' + fmtN(mean) + '</strong> &nbsp;·&nbsp; ' +
                        'Diferencia: <strong>' + diffPct.toFixed(1) + '%</strong>' +
                    '</p>' +
                    '<p class="text-xs text-slate-400 dark:text-slate-500 mt-1 italic">' +
                        'La Media MC debe converger al VE Analítico. Una diferencia < 5% confirma que la simulación es consistente con la teoría.' +
                    '</p>' +
                '</div>' +
            '</div>' +
            // Tabla de relaciones
            '<div class="grid grid-cols-2 gap-2 text-xs">' +
                // VE vs P50
                '<div class="rounded-lg bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-700 p-2">' +
                    '<p class="font-semibold text-slate-500 dark:text-slate-400 mb-1">VE Analítico vs. P50</p>' +
                    '<p class="font-bold text-indigo-600 dark:text-indigo-400">' + fmtVe(ve) + ' → ' + fmtN(p50) + '</p>' +
                    '<p class="text-slate-400 mt-0.5">' + (ve > p50 ? 'Media > P50: distribución sesgada a la derecha (cola de riesgos altos).' : 'Media ≤ P50: distribución simétrica o con sesgo izquierdo.') + '</p>' +
                '</div>' +
                // Contingencia P80
                '<div class="rounded-lg bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 p-2">' +
                    '<p class="font-semibold text-amber-600 dark:text-amber-400 mb-1">Contingencia P80 sobre VE</p>' +
                    '<p class="font-bold text-amber-700 dark:text-amber-300">+' + fmtVe(cont80) + ' (' + cont80Pct.toFixed(0) + '%)</p>' +
                    '<p class="text-slate-400 mt-0.5">Capital extra sobre VE para cubrir el 80% de escenarios.</p>' +
                '</div>' +
                // Contingencia P90
                '<div class="rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 p-2">' +
                    '<p class="font-semibold text-red-600 dark:text-red-400 mb-1">Contingencia P90 sobre VE</p>' +
                    '<p class="font-bold text-red-700 dark:text-red-300">+' + fmtVe(cont90) + ' (' + cont90Pct.toFixed(0) + '%)</p>' +
                    '<p class="text-slate-400 mt-0.5">Capital extra sobre VE para cubrir el 90% de escenarios.</p>' +
                '</div>' +
                // Fórmula explicativa
                '<div class="rounded-lg bg-indigo-50 dark:bg-indigo-900/20 border border-indigo-200 dark:border-indigo-800 p-2">' +
                    '<p class="font-semibold text-indigo-600 dark:text-indigo-400 mb-1">Fórmula (por riesgo)</p>' +
                    '<p class="font-mono text-indigo-700 dark:text-indigo-300">p × (min+moda+max) / 3</p>' +
                    '<p class="text-slate-400 mt-0.5">Bernoulli × E[Triangular] — equivalente analítico de @RiskBernoulli × @RiskTriang.</p>' +
                '</div>' +
            '</div>' +
            // ── Distribución por Nivel CODELCO ────────────────────────────
            '<div class="mt-3 pt-3 border-t border-slate-200 dark:border-slate-700">' +
                '<p class="text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wide mb-2 flex items-center gap-1">' +
                    '<span class="material-icons text-sm">bar_chart</span>Distribución por Nivel CODELCO' +
                '</p>' +
                (function() {
                    // Calcular conteo y VE por nivel
                    var byLevel = {};
                    var totalR  = 0;
                    document.querySelectorAll('#risks-body .data-row').forEach(function(row) {
                        var probEl = row.querySelector('[name$=".Probability"]');
                        var veCell = row.querySelector('.risk-ve-val');
                        if (!probEl) return;
                        var pct = parseFloat(probEl.value) || 0;
                        var ve  = veCell ? (parseFloat(veCell.dataset.ve) || 0) : 0;
                        var lvl = getCodelcoLevel(pct);
                        if (!byLevel[lvl.nivel]) byLevel[lvl.nivel] = { count: 0, ve: 0, lvl: lvl };
                        byLevel[lvl.nivel].count++;
                        byLevel[lvl.nivel].ve += ve;
                        totalR++;
                    });
                    var rows = CODELCO_LEVELS.map(function(l) {
                        var d    = byLevel[l.nivel] || { count: 0, ve: 0 };
                        var barW = totalR > 0 ? Math.round((d.count / totalR) * 100) : 0;
                        var veColor = d.ve < -1 ? '#10b981' : d.ve > 1 ? '#ef4444' : '#94a3b8';
                        return '<div class="flex items-center gap-2 py-0.5">' +
                            '<span class="w-5 h-5 rounded-full flex-shrink-0 flex items-center justify-center text-xs font-bold" style="background:' + l.color + ';color:' + l.textColor + '">' + l.nivel + '</span>' +
                            '<span class="text-xs text-slate-600 dark:text-slate-300 w-24 flex-shrink-0">' + l.cualitativo + '</span>' +
                            '<div class="flex-1 bg-slate-200 dark:bg-slate-700 rounded-full h-1.5">' +
                                '<div class="h-1.5 rounded-full transition-all" style="width:' + barW + '%;background:' + l.color + '"></div>' +
                            '</div>' +
                            '<span class="text-xs text-slate-500 w-14 text-right flex-shrink-0">' + d.count + ' riesgo' + (d.count !== 1 ? 's' : '') + '</span>' +
                            '<span class="text-xs font-semibold w-20 text-right flex-shrink-0" style="color:' + veColor + '">' + fmtVe(d.ve) + '</span>' +
                        '</div>';
                    }).join('');
                    return '<div class="space-y-0.5">' + rows + '</div>';
                }()) +
            '</div>';

        el.classList.remove('hidden');
    }

    window.renderRisksVerification = renderRisksVerification;

    // ── Ejecutar Riesgos vía AJAX (sin borrar la tabla) ─────────────────────────
    window.executeRisks = function() {
        var body = document.getElementById('risks-body');
        if (!body) return;
        var risks = [];
        body.querySelectorAll('.data-row').forEach(function(row) {
            var get = function(field) {
                var el = row.querySelector('[name$=".' + field + '"]');
                return el ? el.value : '';
            };
            risks.push({
                Tipo:              get('Tipo') || 'Amenaza',
                Origin:            get('Origin'),
                Code:              get('Code'),
                Description:       get('Description'),
                Cause:             get('Cause'),
                ResponsePlan:      get('ResponsePlan'),
                Probability:       parseFloat(get('Probability')) || 0,
                MinImpact:         parseImpact(get('MinImpact')),
                MostLikelyImpact:  parseImpact(get('MostLikelyImpact')),
                MaxImpact:         parseImpact(get('MaxImpact')),
                Distribution:      get('Distribution') || 'Triangular',
                EstimationBase:    get('EstimationBase'),
                Opportunity:       get('Opportunity'),
                Threat:            get('Threat')
            });
        });
        var simEl = document.querySelector('[name="RiskSimulations"]');
        var sims  = simEl ? (parseInt(simEl.value) || 10000) : 10000;
        var tok   = document.querySelector('input[name="__RequestVerificationToken"]');
        var btnExec = document.getElementById('btn-exec-risks');
        var btnLbl  = document.getElementById('btn-exec-risks-lbl');
        var errBox  = document.getElementById('risks-ajax-error');
        var errMsg  = document.getElementById('risks-ajax-error-msg');
        if (btnExec) btnExec.disabled = true;
        if (btnLbl)  btnLbl.textContent = 'Ejecutando…';
        if (errBox)  errBox.classList.add('hidden');
        fetch('?handler=RisksAjax', {
            method:  'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': tok ? tok.value : ''
            },
            body: JSON.stringify({ risks: risks, simulations: sims })
        })
        .then(function(r) { return r.text(); })
        .then(function(text) {
            if (btnExec) btnExec.disabled = false;
            if (btnLbl)  btnLbl.textContent = 'Ejecutar';
            var d;
            try { d = JSON.parse(text); }
            catch(parseErr) {
                if (errMsg) errMsg.textContent = 'Respuesta no válida del servidor: ' + text.substring(0, 300);
                if (errBox) errBox.classList.remove('hidden');
                return;
            }
            if (d && d.error) {
                if (errMsg) errMsg.textContent = d.error;
                if (errBox) errBox.classList.remove('hidden');
                return;
            }
            try {
                renderMcResult('risks', d);
            } catch(renderErr) {
                console.error('[Risks] renderMcResult:', renderErr);
                if (errMsg) errMsg.textContent = 'Error al mostrar resultados: ' + renderErr.message;
                if (errBox) errBox.classList.remove('hidden');
                return;
            }
            renderRisksVerification(d.statistics);
            saveResultToStorage('risks', d);
            saveTabToStorage('risks');
            saveRisksToProject(d);
            var sect = document.getElementById('risks-result-section');
            if (sect) sect.classList.remove('hidden');
        })
        .catch(function(e) {
            if (btnExec) btnExec.disabled = false;
            if (btnLbl)  btnLbl.textContent = 'Ejecutar';
            console.error('[Risks] fetch:', e);
            if (errMsg)  errMsg.textContent = 'Error de red: ' + e.message;
            if (errBox)  errBox.classList.remove('hidden');
        });
    };

    // ── Guardar filas de Riesgos (localStorage + BD del proyecto) ───────────────
    window.saveRisksData = function() {
        saveTabToStorage('risks');
        saveRisksToProject(null); // persiste inputJson en BD asociado al proyecto seleccionado
        var btn = document.getElementById('btn-save-risks');
        if (!btn) return;
        var orig = btn.innerHTML;
        btn.innerHTML = '<span class="material-icons text-base">check_circle</span> Guardado';
        btn.disabled = true;
        setTimeout(function() { btn.innerHTML = orig; btn.disabled = false; }, 2000);
    };

    // ── Riesgos: selector de proyecto (proyectos del Taller de Costos) ─────────
    // _risksTcId  → TallerProyecto.Id (GUID) — fuente de verdad del selector
    // _risksMcId  → MontecarloProject.Id (GUID) — fila de persistencia MC, null hasta primer guardado
    // _risksLabel → texto para mostrar en el banner
    var _risksTcId  = null;
    var _risksMcId  = null;
    var _risksLabel = null;
    try {
        _risksTcId  = localStorage.getItem('mc-risks-tc-id')   || null;
        _risksMcId  = localStorage.getItem('mc-risks-mc-id')   || null;
        _risksLabel = localStorage.getItem('mc-risks-tc-name') || null;
    } catch(e) {}

    function loadRisksProjectSelector(onDone) {
        var sel = document.getElementById('risks-project-select');
        if (!sel) return;
        // Reutiliza tcGet de taller-costos.js que apunta a /TallerCostos?handler=
        if (typeof tcGet !== 'function') return;
        tcGet('ProyectosJson')
            .then(function(lista) {
                sel.innerHTML = '<option value="">— Seleccione un proyecto —</option>' +
                    lista.map(function(p) {
                        var label = (p.codigo ? p.codigo + ' — ' : '') + p.nombre;
                        return '<option value="' + escHtml(p.id) + '">' + escHtml(label) + '</option>';
                    }).join('');
                if (_risksTcId) {
                    sel.value = _risksTcId;
                    if (!sel.value) {
                        _risksTcId = null; _risksMcId = null; _risksLabel = null;
                    } else {
                        // Restaurar datos del proyecto al cargar la página
                        window.onRisksProjectSelect();
                    }
                }
                updateRisksBanner();
                if (onDone) onDone();
            })
            .catch(function() {});
    }

    window.refreshRisksProjectList = function() { loadRisksProjectSelector(); };

    window.onRisksProjectSelect = function() {
        var sel = document.getElementById('risks-project-select');
        var val = sel ? sel.value : '';

        if (!val) {
            _risksTcId = null; _risksMcId = null; _risksLabel = null;
            try {
                localStorage.removeItem('mc-risks-tc-id');
                localStorage.removeItem('mc-risks-mc-id');
                localStorage.removeItem('mc-risks-tc-name');
            } catch(e) {}
            updateRisksBanner();
            return;
        }

        // Guardar el proyecto anterior antes de cambiar
        if (_risksTcId && _risksTcId !== val) saveRisksToProject(null);

        var opt    = sel.options[sel.selectedIndex];
        _risksTcId  = val;
        _risksLabel = opt ? opt.text : val;
        _risksMcId  = null;
        try {
            localStorage.setItem('mc-risks-tc-id',   _risksTcId);
            localStorage.setItem('mc-risks-tc-name', _risksLabel);
            localStorage.removeItem('mc-risks-mc-id');
        } catch(e) {}
        updateRisksBanner();

        // Cargar datos guardados para este proyecto
        fetch('?handler=RisksTabForTcProject&tcProjectId=' + encodeURIComponent(val))
            .then(function(r) { return r.json(); })
            .then(function(data) {
                if (data.error) return;
                if (data.id) {
                    _risksMcId = data.id;
                    try { localStorage.setItem('mc-risks-mc-id', data.id); } catch(e) {}
                }
                var hasInputs = data.inputJson && data.inputJson !== '{}';
                var hasResult = data.resultJson && data.resultJson.length > 0;

                if (hasInputs) {
                    try { localStorage.setItem('mc-tab-inputs-risks', data.inputJson); } catch(e) {}
                    // Usar window.* para que el envoltorio de la página ejecute hooks (p. ej. tcInitRiskTipoColors).
                    // restoreTabFromStorage interno ya deja la tabla en modo lectura (mcRisksInitReadMode).
                    if (typeof window.restoreTabFromStorage === 'function') window.restoreTabFromStorage('risks');
                    else restoreTabFromStorage('risks');
                }

                var sect = document.getElementById('risks-result-section');
                if (hasResult && hasInputs) {
                    try {
                        var result = JSON.parse(data.resultJson);
                        renderMcResult('risks', result);
                        saveResultToStorage('risks', result);
                        if (result.statistics) renderRisksVerification(result.statistics);
                    } catch(e) {}
                } else {
                    var vc = document.getElementById('risks-verification-card');
                    if (vc) vc.classList.add('hidden');
                    if (sect) sect.classList.add('hidden');
                }
                // Verificación final: ocultar si la tabla quedó sin datos reales
                if (typeof window.tcEnforceRisksResultVisibility === 'function')
                    window.tcEnforceRisksResultVisibility();
            })
            .catch(function(e) { console.error('[Risks] loadProject:', e); });
    };

    function updateRisksBanner() {
        var banner = document.getElementById('tc-risks-proj-banner');
        var text   = document.getElementById('tc-risks-proj-banner-text');
        if (!banner) return;
        if (_risksLabel) {
            if (text) text.textContent = 'Proyecto activo: ' + _risksLabel;
            banner.classList.remove('hidden');
        } else {
            banner.classList.add('hidden');
        }
    }

    function saveRisksToProject(resultData) {
        if (!_risksTcId) return;
        saveTabToStorage('risks');
        var inputJson  = '{}';
        var resultJson = resultData != null ? JSON.stringify(resultData) : '';
        try { inputJson = localStorage.getItem('mc-tab-inputs-risks') || '{}'; } catch(e) {}
        var tok = document.querySelector('input[name="__RequestVerificationToken"]');
        var body = {
            id:          _risksMcId || null,
            projectName: 'tc:' + _risksTcId,
            tabId:       'risks',
            tabData:     { inputJson: inputJson, resultJson: resultJson, completedAt: new Date().toISOString() }
        };
        fetch('?handler=SaveProjectTab', {
            method:  'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tok ? tok.value : '' },
            body:    JSON.stringify(body).replace(/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/g, '')
        })
        .then(function(r) { return r.json(); })
        .then(function(d) {
            if (d.id && !_risksMcId) {
                _risksMcId = d.id;
                try { localStorage.setItem('mc-risks-mc-id', d.id); } catch(e) {}
            }
        })
        .catch(function() {});
    }

    // Limpia el resultado guardado en BD para la pestaña de riesgos (llamado desde tcLimpiarProjTab)
    window.clearRisksResult = function() {
        saveRisksToProject(null);
    };

    // Devuelve true si la tabla de riesgos tiene al menos una fila con datos reales
    window.tcRisksTableHasData = function() {
        var rows = document.querySelectorAll('#risks-body .data-row');
        for (var i = 0; i < rows.length; i++) {
            var row = rows[i];
            var likely = parseFloat(row.querySelector('[name$=".MostLikelyImpact"]')?.value) || 0;
            var min    = parseFloat(row.querySelector('[name$=".MinImpact"]')?.value)         || 0;
            var max    = parseFloat(row.querySelector('[name$=".MaxImpact"]')?.value)         || 0;
            var desc   = (row.querySelector('[name$=".Description"]')?.value  || '').trim();
            var code   = (row.querySelector('[name$=".Code"]')?.value         || '').trim();
            if (likely !== 0 || min !== 0 || max !== 0 || desc !== '' || code !== '') return true;
        }
        return false;
    };

    // Oculta la sección de resultados de riesgos si la tabla no tiene datos reales
    window.tcEnforceRisksResultVisibility = function() {
        var sect = document.getElementById('risks-result-section');
        var vc   = document.getElementById('risks-verification-card');
        if (!sect) return;
        if (!window.tcRisksTableHasData()) {
            sect.classList.add('hidden');
            if (vc) vc.classList.add('hidden');
        }
    };

    // ── Correlación: selector de proyecto y fuente ──────────────────────────
    var _corrTcId  = null;
    var _corrLabel = null;
    var _corrSource = 'manual';   // 'manual' | 'contracts' | 'risks' | 'both'
    try {
        _corrTcId   = localStorage.getItem('mc-corr-tc-id')     || null;
        _corrLabel  = localStorage.getItem('mc-corr-tc-name')   || null;
        _corrSource = localStorage.getItem('mc-corr-source')    || 'manual';
    } catch(e) {}

    // Actualiza la UI del selector de fuente
    function updateCorrSourceUI() {
        ['manual','contracts','risks','both'].forEach(function(s) {
            var btn = document.getElementById('corr-src-btn-' + s);
            if (!btn) return;
            var active = s === _corrSource;
            btn.className = 'corr-src-btn px-3 py-1.5 font-medium transition-colors' +
                (s !== 'manual' ? ' border-l border-pink-200 dark:border-pink-700' : '') +
                (active ? ' bg-pink-600 text-white' : ' text-pink-700 dark:text-pink-300 hover:bg-pink-50 dark:hover:bg-pink-900/20');
        });
        // Mostrar/ocultar selector de proyecto
        var projRow = document.getElementById('corr-project-row');
        if (projRow) projRow.style.display = _corrSource === 'manual' ? 'none' : '';
        // Actualizar etiqueta de info
        var infoEl = document.getElementById('corr-src-info');
        if (infoEl) {
            var labels = { manual: '', contracts: 'Cada contrato del proyecto como variable (Comprometido → EAT → CAPEX).', risks: 'Cada riesgo del proyecto como variable (Mín → Probable → Máx de impacto).', both: 'Contratos y riesgos combinados como variables de correlación.' };
            infoEl.textContent = labels[_corrSource] || '';
            infoEl.style.display = _corrSource === 'manual' ? 'none' : '';
        }
    }

    window.setCorrSource = function(src) {
        _corrSource = src;
        try { localStorage.setItem('mc-corr-source', src); } catch(e) {}
        updateCorrSourceUI();
        // Si hay proyecto seleccionado y la fuente no es manual, recargar variables
        if (src !== 'manual' && _corrTcId) loadCorrVarsFromProject(_corrTcId, src);
        // Si es manual, limpiar y agregar 2 filas vacías
        if (src === 'manual') {
            var body = document.getElementById('corr-vars-body');
            if (body) {
                body.innerHTML = '';
                addCorrVar(); addCorrVar();
            }
            rebuildCorrMatrix();
        }
        // Ocultar panel IA al cambiar fuente
        var aiPanel = document.getElementById('corr-ai-panel');
        if (aiPanel) aiPanel.classList.add('hidden');
        var aiLabel = document.getElementById('corr-ai-scenario-label');
        if (aiLabel) aiLabel.classList.add('hidden');
        setCorrMatrixMode('manual');
    };

    function loadCorrProjectSelector(onDone) {
        var sel = document.getElementById('corr-project-select');
        if (!sel) return;
        if (typeof tcGet !== 'function') return;
        tcGet('ProyectosJson')
            .then(function(lista) {
                sel.innerHTML = '<option value="">— Seleccione un proyecto —</option>' +
                    lista.map(function(p) {
                        var label = (p.codigo ? p.codigo + ' — ' : '') + p.nombre;
                        return '<option value="' + escHtml(p.id) + '">' + escHtml(label) + '</option>';
                    }).join('');
                var hadSaved = !!_corrTcId;
                if (_corrTcId) {
                    sel.value = _corrTcId;
                    if (!sel.value) { _corrTcId = null; _corrLabel = null; hadSaved = false; }
                }
                updateCorrBanner();
                updateCorrSourceUI();
                // Auto-cargar vars si había un proyecto guardado y fuente no es manual
                if (hadSaved && _corrTcId && _corrSource !== 'manual') loadCorrVarsFromProject(_corrTcId, _corrSource);
                if (onDone) onDone();
            })
            .catch(function() {});
    }

    window.refreshCorrProjectList = function() { loadCorrProjectSelector(); };

    window.onCorrProjectSelect = function() {
        var sel = document.getElementById('corr-project-select');
        var val = sel ? sel.value : '';
        if (!val) {
            _corrTcId = null; _corrLabel = null;
            try { localStorage.removeItem('mc-corr-tc-id'); localStorage.removeItem('mc-corr-tc-name'); } catch(e) {}
            updateCorrBanner();
            return;
        }
        var opt   = sel.options[sel.selectedIndex];
        _corrTcId  = val;
        _corrLabel = opt ? opt.text : val;
        try { localStorage.setItem('mc-corr-tc-id', _corrTcId); localStorage.setItem('mc-corr-tc-name', _corrLabel); } catch(e) {}
        updateCorrBanner();
        if (_corrSource !== 'manual') loadCorrVarsFromProject(val, _corrSource);
    };

    function updateCorrBanner() {
        var banner = document.getElementById('tc-corr-proj-banner');
        var text   = document.getElementById('tc-corr-proj-banner-text');
        if (!banner) return;
        if (_corrLabel) {
            if (text) text.textContent = 'Proyecto activo: ' + _corrLabel;
            banner.classList.remove('hidden');
        } else {
            banner.classList.add('hidden');
        }
    }

    // Renderiza un array de {name, unit, dist, p1, p2, p3} en la tabla de variables.
    function renderCorrVars(vars) {
        var body = document.getElementById('corr-vars-body');
        var container = document.getElementById('corr-matrix-container');
        if (!body) return;
        body.innerHTML = '';
        if (!vars || vars.length === 0) {
            if (container) container.innerHTML = '<span class="text-xs text-slate-400 dark:text-slate-500 italic">No se encontraron variables para la fuente seleccionada en este proyecto.</span>';
            return;
        }
        var html = '';
        vars.forEach(function(v, i) { html += T_CORR_VAR(i); });
        body.innerHTML = html;
        var rows = body.querySelectorAll('.corr-var-row');
        vars.forEach(function(v, i) {
            var row = rows[i];
            if (!row) return;
            row.querySelector('[data-corr="name"]').value = v.name  || '';
            row.querySelector('[data-corr="unit"]').value = v.unit  || 'USD';
            var distSel = row.querySelector('[data-corr="dist"]');
            if (distSel) distSel.value = v.dist || 'Triangular';
            row.querySelector('[data-corr="p1"]').value = v.p1 != null ? v.p1 : '';
            row.querySelector('[data-corr="p2"]').value = v.p2 != null ? v.p2 : '';
            var p3inp = row.querySelector('[data-corr="p3"]');
            if (p3inp) { p3inp.value = v.p3 != null ? v.p3 : ''; p3inp.disabled = false; }
        });
        if (container) {
            container.innerHTML = '<span class="text-xs text-pink-600 dark:text-pink-400 italic">' +
                vars.length + ' variables cargadas. Haz clic en "Actualizar matriz" para generar la matriz de correlación.</span>';
        }
    }

    function loadCorrVarsFromProject(tcProjectId, source) {
        var loading = document.getElementById('corr-loading');
        var src = source || _corrSource || 'risks';
        // Siempre pide al backend: contratos (D-Rangos), riesgos (TallerRiesgos última rev.), o ambos.
        if (loading) loading.classList.remove('hidden');
        fetch('?handler=CorrVarsForTcProject&tcProjectId=' + encodeURIComponent(tcProjectId) +
              '&source=' + encodeURIComponent(src))
            .then(function(r) { return r.json(); })
            .then(function(data) {
                if (loading) loading.classList.add('hidden');
                renderCorrVars(data.vars || []);
            })
            .catch(function(err) {
                if (loading) loading.classList.add('hidden');
                console.error('CorrVars error:', err);
            });
    }

    // ── Tabs (sidebar) ───────────────────────────────────────────────────────
    var _currentTabId = window._MC_INIT_TAB || 'risks';
    window.activateTab = function(tabId) {
        // Persist current tab inputs before switching
        if (_currentTabId && _currentTabId !== tabId && FIELDS[_currentTabId]) {
            saveTabToStorage(_currentTabId);
        }
        document.querySelectorAll('.tab-content').forEach(function(el) { el.classList.add('hidden'); });
        document.querySelectorAll('.tab-btn').forEach(function(btn) { btn.classList.remove('mc-active'); });
        var content = document.getElementById('tab-' + tabId);
        if (content) content.classList.remove('hidden');
        var activeBtn = document.querySelector('[data-tab="' + tabId + '"]');
        if (activeBtn) activeBtn.classList.add('mc-active');
        if (tabId === 'history') loadHistory();
        if (tabId === 'projects') loadProjectsTab();
        if (tabId === 'risks') loadRisksProjectSelector();
        if (tabId === 'corr') { loadCorrProjectSelector(); }
        if (tabId === 'sra') syncSraFromSource();
        if (tabId === 'cra') syncCraFromSource();
        // Ocultar banner global de nombre de proyecto en pestaña Correlación (tiene su propio selector)
        var globalBanner = document.getElementById('mc-project-banner');
        if (globalBanner) globalBanner.style.display = tabId === 'corr' ? 'none' : '';
        // scroll contenido al inicio
        var cw = document.querySelector('#mc-sidebar + div');
        if (cw) cw.scrollTop = 0;
        _currentTabId = tabId;
    };

    // ── Sidebar toggle ───────────────────────────────────────────────────────
    window.toggleMcSidebar = function() {
        var sb   = document.getElementById('mc-sidebar');
        var icon = document.getElementById('mc-sidebar-icon');
        var col  = sb.classList.toggle('mc-collapsed');
        if (icon) icon.textContent = col ? 'chevron_right' : 'chevron_left';
        try { localStorage.setItem('mc-sb-col', col ? '1' : '0'); } catch(e) {}
    };

    // ── Help toggle ──────────────────────────────────────────────────────────
    window.toggleMcHelp = function() {
        var p = document.getElementById('mc-help-panel');
        if (p) { p.open = !p.open; if (p.open) p.scrollIntoView({ behavior:'smooth', block:'start' }); }
    };

    // ── Restaurar estado sidebar ─────────────────────────────────────────────
    (function() {
        try {
            if (localStorage.getItem('mc-sb-col') === '1') {
                var sb   = document.getElementById('mc-sidebar');
                var icon = document.getElementById('mc-sidebar-icon');
                if (sb)   sb.classList.add('mc-collapsed');
                if (icon) icon.textContent = 'chevron_right';
            }
        } catch(e) {}
    }());

    // ── Cargar ejemplos de prueba ────────────────────────────────────────────
    // ── Caso real: Ampliación Planta Concentradora — Minera Los Andes (USD ~47M) ──
    var EXAMPLES = {
        risks: [
            { Cause: 'Sobreexigencia operacional',  RiskEvent: 'Falla equipo SAG/molino',    Consequence: 'Detención planta',    Probability: 25, MinImpact: 500000,   MostLikelyImpact: 2000000, MaxImpact: 5000000,  Distribution: 'Triangular', Observations: '' },
            { Cause: 'Incumplimiento contractual',  RiskEvent: 'Retraso contratista',         Consequence: 'Atraso hitos',        Probability: 40, MinImpact: 200000,   MostLikelyImpact: 700000,  MaxImpact: 1800000,  Distribution: 'Triangular', Observations: '' },
            { Cause: 'Deficiencia en seguridad',    RiskEvent: 'Accidente laboral grave',     Consequence: 'Costos legales',      Probability: 8,  MinImpact: 1000000,  MostLikelyImpact: 3500000, MaxImpact: 9000000,  Distribution: 'Triangular', Observations: '' },
            { Cause: 'Burocracia regulatoria',      RiskEvent: 'Permiso ambiental demorado',  Consequence: 'Paralización obras',  Probability: 20, MinImpact: 300000,   MostLikelyImpact: 900000,  MaxImpact: 2500000,  Distribution: 'PERT',       Observations: '' },
            { Cause: 'Inflación materias primas',   RiskEvent: 'Alza materiales y equipos',   Consequence: 'Sobrecosto',          Probability: 55, MinImpact: 150000,   MostLikelyImpact: 450000,  MaxImpact: 1200000,  Distribution: 'Normal',     Observations: '' }
        ],
        costs: [
            { Name: 'Ingeniería y gestión',        BaseCost: 2500000,  EstimationClass: 'Clase 3', MinCost: 2000000,  MostLikelyCost: 2500000,  MaxCost: 3200000,  OpportunityProbability: 0.30, OpportunityAmount: 100000, ThreatProbability: 0.40, ThreatAmount: 200000, Distribution: 'Triangular', Observations: '' },
            { Name: 'Obras civiles',               BaseCost: 12000000, EstimationClass: 'Clase 3', MinCost: 9000000,  MostLikelyCost: 12000000, MaxCost: 16000000, OpportunityProbability: 0.20, OpportunityAmount: 500000, ThreatProbability: 0.50, ThreatAmount: 800000, Distribution: 'Triangular', Observations: '' },
            { Name: 'Equipos principales',         BaseCost: 25000000, EstimationClass: 'Clase 2', MinCost: 20000000, MostLikelyCost: 25000000, MaxCost: 32000000, OpportunityProbability: 0,    OpportunityAmount: 0,      ThreatProbability: 0.45, ThreatAmount: 1000000, Distribution: 'PERT',      Observations: '' },
            { Name: 'Inst. eléctrica y control',   BaseCost: 4500000,  EstimationClass: 'Clase 3', MinCost: 3500000,  MostLikelyCost: 4500000,  MaxCost: 6000000,  OpportunityProbability: 0.25, OpportunityAmount: 200000, ThreatProbability: 0.35, ThreatAmount: 300000, Distribution: 'Triangular', Observations: '' },
            { Name: 'Puesta en marcha',            BaseCost: 2200000,  EstimationClass: 'Clase 4', MinCost: 1500000,  MostLikelyCost: 2200000,  MaxCost: 3500000,  OpportunityProbability: 0,    OpportunityAmount: 0,      ThreatProbability: 0.30, ThreatAmount: 400000, Distribution: 'Triangular', Observations: '' }
        ],
        schedule: [
            { Name: 'Ingeniería de detalle',  MinDays: 40,  MostLikelyDays: 55,  MaxDays: 80,  PlannedDays: 60,  DependenciesText: '',                      Opportunities: 0, Threats: 5,  Distribution: 'Triangular', Observations: '' },
            { Name: 'Adquisición de equipos', MinDays: 100, MostLikelyDays: 135, MaxDays: 190, PlannedDays: 140, DependenciesText: 'Ingeniería de detalle',   Opportunities: 0, Threats: 15, Distribution: 'Triangular', Observations: '' },
            { Name: 'Obras civiles',          MinDays: 150, MostLikelyDays: 210, MaxDays: 300, PlannedDays: 220, DependenciesText: 'Ingeniería de detalle',   Opportunities: 5, Threats: 20, Distribution: 'PERT',       Observations: '' },
            { Name: 'Montaje de equipos',     MinDays: 60,  MostLikelyDays: 85,  MaxDays: 120, PlannedDays: 90,  DependenciesText: 'Adquisición de equipos', Opportunities: 0, Threats: 10, Distribution: 'Triangular', Observations: '' },
            { Name: 'Inst. eléctrica',        MinDays: 45,  MostLikelyDays: 60,  MaxDays: 90,  PlannedDays: 65,  DependenciesText: 'Montaje de equipos',     Opportunities: 0, Threats: 8,  Distribution: 'Triangular', Observations: '' },
            { Name: 'Comisionamiento',        MinDays: 20,  MostLikelyDays: 30,  MaxDays: 50,  PlannedDays: 35,  DependenciesText: 'Inst. eléctrica',        Opportunities: 0, Threats: 5,  Distribution: 'Triangular', Observations: '' }
        ],
        'sched-risks': [
            { Cause: 'Condiciones climáticas',  RiskEvent: 'Lluvias intensas invierno', Consequence: 'Paralización obras civiles', Probability: 0.35, MinImpact: 10, MostLikelyImpact: 25, MaxImpact: 45, Distribution: 'Triangular', Observations: '' },
            { Cause: 'Escasez de mano de obra', RiskEvent: 'Huelga de contratistas',    Consequence: 'Retraso montaje',           Probability: 0.15, MinImpact: 15, MostLikelyImpact: 30, MaxImpact: 60, Distribution: 'Triangular', Observations: '' },
            { Cause: 'Problemas logísticos',    RiskEvent: 'Demora en despacho equipos',Consequence: 'Atraso ruta crítica',       Probability: 0.25, MinImpact: 20, MostLikelyImpact: 40, MaxImpact: 80, Distribution: 'PERT',       Observations: '' }
        ],
        'var': [
            { Name: 'Portafolio minero',     InitialValue: 15000000, ExpectedAnnualReturn: 0.11,  AnnualVolatility: 0.26 },
            { Name: 'Bonos soberanos Chile', InitialValue: 8000000,  ExpectedAnnualReturn: 0.045, AnnualVolatility: 0.06 },
            { Name: 'Activos inmobiliarios', InitialValue: 4000000,  ExpectedAnnualReturn: 0.07,  AnnualVolatility: 0.12 }
        ]
    };

    var FIELDS = {
        risks:        { body: 'risks-body',       prefix: 'Risks',          type: 'risk',        keys: ['Tipo','Origin','Code','Description','Cause','ResponsePlan','Probability','MinImpact','MostLikelyImpact','MaxImpact','Distribution','EstimationBase','Opportunity','Threat'] },
        costs:        { body: 'costs-body',        prefix: 'CostComponents', type: 'cost',        keys: ['Name','BaseCost','EstimationClass','MinCost','MostLikelyCost','MaxCost','OpportunityProbability','OpportunityAmount','ThreatProbability','ThreatAmount','Distribution','Observations'] },
        schedule:     { body: 'schedule-body',     prefix: 'ScheduleTasks',  type: 'schedule',    keys: ['Name','MinDays','MostLikelyDays','MaxDays','PlannedDays','DependenciesText','Opportunities','Threats','Distribution','Observations'] },
        'sched-risks':{ body: 'sched-risks-body',  prefix: 'ScheduleRisks',  type: 'sched-risk',  keys: ['Cause','RiskEvent','Consequence','Probability','MinImpact','MostLikelyImpact','MaxImpact','Distribution','Observations'] },
        'var':        { body: 'assets-body',       prefix: 'Assets',         type: 'var',         keys: ['Name','InitialValue','ExpectedAnnualReturn','AnnualVolatility'] }
    };

    window.loadExample = function(tabId) {
        var cfg  = FIELDS[tabId];
        var data = EXAMPLES[tabId];
        if (!cfg || !data) return;
        var body = document.getElementById(cfg.body);
        if (!body) return;
        var rows = body.querySelectorAll('.data-row');
        for (var r = rows.length - 1; r >= 0; r--) { rows[r].remove(); }
        data.forEach(function(item, i) {
            body.insertAdjacentHTML('beforeend', T[cfg.type](i));
            var row = body.querySelectorAll('.data-row')[i];
            cfg.keys.forEach(function(k) {
                var el = row.querySelector('[name="' + cfg.prefix + '[' + i + '].' + k + '"]');
                if (el) el.value = item[k] !== undefined ? item[k] : '';
            });
        });
        if (tabId === 'risks') {
            window.formatAllRisksImpacts();
            window.recalcAllRisksVe();
        }
    };

    // Activar tab inicial desde el servidor
    activateTab(window._MC_INIT_TAB || 'risks');

    // ── Historial vía AJAX ───────────────────────────────────────────────────
    var historyLoaded = false;
    window.loadHistory = function() {
        var type = document.getElementById('history-type') ? document.getElementById('history-type').value : '';
        var url = '?handler=HistoryJson&page=1&pageSize=20' + (type ? '&simulationType=' + encodeURIComponent(type) : '');
        var el = document.getElementById('history-content');
        el.innerHTML = '<span class="italic text-slate-400">Cargando...</span>';
        fetch(url)
            .then(function(r) { return r.text(); })
            .then(function(json) {
                var pretty;
                try { pretty = JSON.stringify(JSON.parse(json), null, 2); } catch(e) { pretty = json; }
                el.innerHTML = '<pre class="mc-json">' + escHtml(pretty) + '</pre>';
                historyLoaded = true;
            })
            .catch(function(err) {
                el.innerHTML = '<p class="text-red-500 text-sm">Error: ' + escHtml(err.message) + '</p>';
            });
    };

    // ── Visualización KPI Monte Carlo ─────────────────────────────────────────
    var _mcCharts = {}, _mcCdfCharts = {}, _mcHistData = {}, _mcAllData = {};

    function fmtN(v) {
        if (v === null || v === undefined || isNaN(v)) return '—';
        var abs = Math.abs(v);
        if (abs === 0) return '0';
        if (abs < 0.001) return v.toExponential(3);
        if (abs < 1)     return v.toFixed(4);
        if (abs < 1000)  return v.toFixed(2);
        if (abs < 1e6)   return v.toLocaleString('es-CL', { maximumFractionDigits: 0 });
        if (abs < 1e9)   return (v / 1e6).toFixed(2) + ' M';
        return (v / 1e9).toFixed(2) + ' B';
    }

    function renderMcResult(tabId, data) {
        if (!data || data.error || !data.statistics || !data.histogram) {
            var kpiEl = document.getElementById('mc-kpi-' + tabId);
            if (kpiEl) kpiEl.innerHTML =
                '<div class="flex items-center gap-2 px-3 py-2 rounded-lg bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-700 text-sm text-red-700 dark:text-red-300">' +
                '<span class="material-icons text-base">error</span>' +
                '<span>' + (data && data.error ? data.error : 'La API no devolvió resultados válidos. Verifique que los impactos sean mayores a 0.') + '</span></div>';
            var sect = document.getElementById(tabId + '-result-section');
            if (sect) sect.classList.remove('hidden');
            return;
        }
        _mcAllData[tabId] = data;
        var s    = data.statistics;
        var hist = data.histogram;
        _mcHistData[tabId] = hist;

        // ── Tarjetas de decisión (P50 / P80 / P90) ────────────────────────────
        var cv = (s.mean && s.mean !== 0) ? Math.abs(s.stdDev / s.mean) * 100 : 0;
        var cvColor = cv < 20 ? 'emerald' : cv < 50 ? 'amber' : 'red';
        var kpiEl = document.getElementById('mc-kpi-' + tabId);
        if (kpiEl) {
            var cont80 = fmtN(s.p80 - s.mean), cont90 = fmtN(s.p90 - s.mean);
            kpiEl.innerHTML =
                // Fila 1: percentiles de decisión
                '<div class="grid grid-cols-3 gap-2">' +
                '<div class="rounded-xl border-2 border-indigo-300 dark:border-indigo-600 bg-indigo-50 dark:bg-indigo-900/30 p-3">' +
                  '<div class="flex items-center gap-1 text-indigo-600 dark:text-indigo-300 mb-1"><span class="material-icons" style="font-size:13px">radio_button_checked</span><span class="text-xs font-semibold uppercase tracking-wide">P50 — Base</span></div>' +
                  '<div class="text-xl font-extrabold text-indigo-700 dark:text-indigo-200 leading-tight">' + fmtN(s.p50) + '</div>' +
                  '<div class="text-xs text-indigo-500 dark:text-indigo-400 mt-0.5">50% prob. de no superar</div></div>' +
                '<div class="rounded-xl border-2 border-amber-300 dark:border-amber-600 bg-amber-50 dark:bg-amber-900/30 p-3">' +
                  '<div class="flex items-center gap-1 text-amber-600 dark:text-amber-300 mb-1"><span class="material-icons" style="font-size:13px">shield</span><span class="text-xs font-semibold uppercase tracking-wide">P80 — Conservador</span></div>' +
                  '<div class="text-xl font-extrabold text-amber-700 dark:text-amber-200 leading-tight">' + fmtN(s.p80) + '</div>' +
                  '<div class="text-xs text-amber-500 dark:text-amber-400 mt-0.5">Contingencia +' + cont80 + '</div></div>' +
                '<div class="rounded-xl border-2 border-red-300 dark:border-red-600 bg-red-50 dark:bg-red-900/30 p-3">' +
                  '<div class="flex items-center gap-1 text-red-600 dark:text-red-300 mb-1"><span class="material-icons" style="font-size:13px">emergency</span><span class="text-xs font-semibold uppercase tracking-wide">P90 — Pesimista</span></div>' +
                  '<div class="text-xl font-extrabold text-red-700 dark:text-red-200 leading-tight">' + fmtN(s.p90) + '</div>' +
                  '<div class="text-xs text-red-500 dark:text-red-400 mt-0.5">Contingencia +' + cont90 + '</div></div>' +
                '</div>' +
                // Fila 2: estadísticas compactas
                '<div class="grid grid-cols-5 gap-2">' +
                '<div class="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 p-2.5 flex flex-col justify-between">' +
                  '<span class="text-xs text-slate-400 uppercase tracking-wide">Media</span>' +
                  '<span class="text-sm font-bold text-slate-700 dark:text-slate-200">' + fmtN(s.mean) + '</span></div>' +
                '<div class="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 p-2.5 flex flex-col justify-between">' +
                  '<span class="text-xs text-slate-400 uppercase tracking-wide">Mín / Máx</span>' +
                  '<span class="text-xs font-bold text-slate-700 dark:text-slate-200">' + fmtN(s.min) + ' – ' + fmtN(s.max) + '</span></div>' +
                '<div class="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 p-2.5 flex flex-col justify-between">' +
                  '<span class="text-xs text-slate-400 uppercase tracking-wide">Desv. Est.</span>' +
                  '<span class="text-sm font-bold text-slate-700 dark:text-slate-200">± ' + fmtN(s.stdDev) + '</span></div>' +
                '<div class="rounded-xl border border-' + cvColor + '-200 dark:border-' + cvColor + '-700 bg-' + cvColor + '-50 dark:bg-' + cvColor + '-900/20 p-2.5 flex flex-col justify-between">' +
                  '<span class="text-xs text-' + cvColor + '-500 uppercase tracking-wide">CV (Incert.)</span>' +
                  '<span class="text-sm font-bold text-' + cvColor + '-700 dark:text-' + cvColor + '-300">' + cv.toFixed(1) + '%</span></div>' +
                '<div class="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 p-2.5 flex flex-col justify-between">' +
                  '<span class="text-xs text-slate-400 uppercase tracking-wide">Asimetría</span>' +
                  '<span class="text-sm font-bold text-slate-700 dark:text-slate-200">' + (s.skewness !== undefined ? s.skewness.toFixed(3) : '—') + '</span></div>' +
                '</div>';

        }

        // ── Tarjetas P50 por Tipo (solo pestaña Riesgos) ─────────────────────
        if (tabId === 'risks') {
            var tipoEl = document.getElementById('mc-kpi-risks-tipo');
            if (tipoEl) {
                var am        = data.additionalMetrics || {};
                var hasThreats = am.threatCount > 0;
                var hasOpps    = am.opportunityCount > 0;
                var tP50 = hasThreats ? fmtN(am.threatP50)      : '—';
                var oP50 = hasOpps    ? fmtN(am.opportunityP50) : '—';
                var tCnt = am.threatCount      || 0;
                var oCnt = am.opportunityCount || 0;

                tipoEl.innerHTML =
                  '<div class="rounded-xl border-2 border-rose-300 dark:border-rose-600 bg-rose-50 dark:bg-rose-900/30 p-3">' +
                    '<div class="flex items-center gap-1.5 mb-2">' +
                      '<span class="material-icons text-rose-500" style="font-size:16px">warning_amber</span>' +
                      '<span class="text-xs font-bold text-rose-700 dark:text-rose-300 uppercase tracking-wide">P50 — Amenazas</span>' +
                      '<span class="ml-auto text-xs font-semibold text-rose-400 bg-rose-100 dark:bg-rose-900/50 px-2 py-0.5 rounded-full">' + tCnt + '</span>' +
                    '</div>' +
                    '<div class="text-2xl font-extrabold text-rose-700 dark:text-rose-200 leading-tight tabular-nums">' + tP50 + '</div>' +
                    '<div class="text-xs text-rose-500 dark:text-rose-400 mt-1">' +
                      (hasThreats ? '50% prob. de no superar' : 'Clasifique riesgos como <strong>Amenaza</strong> en la columna Tipo') +
                    '</div>' +
                  '</div>' +
                  '<div class="rounded-xl border-2 border-emerald-300 dark:border-emerald-600 bg-emerald-50 dark:bg-emerald-900/30 p-3">' +
                    '<div class="flex items-center gap-1.5 mb-2">' +
                      '<span class="material-icons text-emerald-500" style="font-size:16px">trending_down</span>' +
                      '<span class="text-xs font-bold text-emerald-700 dark:text-emerald-300 uppercase tracking-wide">P50 — Oportunidades</span>' +
                      '<span class="ml-auto text-xs font-semibold text-emerald-400 bg-emerald-100 dark:bg-emerald-900/50 px-2 py-0.5 rounded-full">' + oCnt + '</span>' +
                    '</div>' +
                    '<div class="text-2xl font-extrabold text-emerald-700 dark:text-emerald-200 leading-tight tabular-nums">' + oP50 + '</div>' +
                    '<div class="text-xs text-emerald-500 dark:text-emerald-400 mt-1">' +
                      (hasOpps ? 'Ahorro esperado al percentil 50' : 'Clasifique riesgos como <strong>Oportunidad</strong> en la columna Tipo') +
                    '</div>' +
                  '</div>';
            }
        }

        renderMcHistChart(tabId, hist);
        renderMcCdfChart(tabId, hist);
        renderMcPercentiles(tabId, s);
        renderMcDecisions(tabId, data);
        if (tabId === 'schedule') renderPlannedDaysBadges('mc-schedule-planned', data);
        updatePdfPanel();
        // Mostrar sección de resultados si existe como elemento independiente (ej: risks-result-section)
        var resSect = document.getElementById(tabId + '-result-section');
        if (resSect) resSect.classList.remove('hidden');
    }

    function renderMcHistChart(tabId, hist) {
        var canvas = document.getElementById('mc-chart-' + tabId);
        if (!canvas || !hist || !hist.length) return;
        var s = (_mcAllData[tabId] || {}).statistics || {};
        var p50 = s.p50, p80 = s.p80, p90 = s.p90, mean = s.mean;

        var labels = hist.map(function(b) { return fmtN((b.rangeMin + b.rangeMax) / 2); });

        // Frecuencias con color acumulado por zona
        var cumFreq = 0;
        var freqs  = hist.map(function(b) { return +(b.frequency * 100).toFixed(2); });
        var bgColors = hist.map(function(b) {
            var mid = (b.rangeMin + b.rangeMax) / 2;
            if (p90 !== undefined && mid > p90) return 'rgba(239,68,68,0.70)';
            if (p80 !== undefined && mid > p80) return 'rgba(245,158,11,0.70)';
            if (p50 !== undefined && mid > p50) return 'rgba(99,102,241,0.60)';
            return 'rgba(16,185,129,0.60)';
        });
        var bdColors = bgColors.map(function(c) {
            return c.replace(/[\d.]+\)$/, '1)');
        });

        // Plugin inline para líneas de percentiles
        var pctLinesPlugin = {
            id: 'pctLines',
            afterDatasetsDraw: function(chart) {
                var ctx2 = chart.ctx;
                var meta = chart.getDatasetMeta(0);
                var yAxis = chart.scales.y;
                var n = hist.length;
                if (!n || !meta.data.length) return;

                function getPx(val) {
                    for (var j = 0; j < n; j++) {
                        if (val >= hist[j].rangeMin && val <= hist[j].rangeMax) {
                            var span = hist[j].rangeMax - hist[j].rangeMin;
                            var t = span > 0 ? (val - hist[j].rangeMin) / span : 0.5;
                            var cx = meta.data[j] ? meta.data[j].x : null;
                            if (cx === null) return null;
                            if (j < n - 1 && meta.data[j + 1]) {
                                return cx + (t - 0.5) * (meta.data[j + 1].x - cx);
                            }
                            return cx;
                        }
                    }
                    if (val < hist[0].rangeMin) return meta.data[0] ? meta.data[0].x : null;
                    return meta.data[n - 1] ? meta.data[n - 1].x : null;
                }

                var lines = [];
                if (mean !== undefined) lines.push({ v: mean, c: '#0ea5e9', l: 'μ', d: [3, 3] });
                if (p50 !== undefined)  lines.push({ v: p50,  c: '#6366f1', l: 'P50', d: [] });
                if (p80 !== undefined)  lines.push({ v: p80,  c: '#f59e0b', l: 'P80', d: [6, 3] });
                if (p90 !== undefined)  lines.push({ v: p90,  c: '#ef4444', l: 'P90', d: [10, 4] });

                lines.forEach(function(ln) {
                    var px = getPx(ln.v);
                    if (px === null) return;
                    ctx2.save();
                    ctx2.beginPath();
                    ctx2.setLineDash(ln.d);
                    ctx2.strokeStyle = ln.c;
                    ctx2.lineWidth = 2;
                    ctx2.moveTo(px, yAxis.top);
                    ctx2.lineTo(px, yAxis.bottom);
                    ctx2.stroke();
                    ctx2.setLineDash([]);
                    ctx2.font = 'bold 9px system-ui,sans-serif';
                    var tw = ctx2.measureText(ln.l).width;
                    ctx2.fillStyle = 'rgba(255,255,255,0.85)';
                    ctx2.fillRect(px - tw / 2 - 2, yAxis.top - 15, tw + 4, 13);
                    ctx2.fillStyle = ln.c;
                    ctx2.textAlign = 'center';
                    ctx2.fillText(ln.l, px, yAxis.top - 4);
                    ctx2.restore();
                });
            }
        };

        if (_mcCharts[tabId]) _mcCharts[tabId].destroy();
        _mcCharts[tabId] = new Chart(canvas, {
            type: 'bar',
            data: { labels: labels, datasets: [{ label: 'Frecuencia', data: freqs,
                backgroundColor: bgColors, borderColor: bdColors,
                borderWidth: 1, borderRadius: 2 }] },
            options: {
                responsive: true,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            title: function(it) {
                                var b = hist[it[0].dataIndex] || {};
                                return fmtN(b.rangeMin) + ' – ' + fmtN(b.rangeMax);
                            },
                            label: function(it) {
                                var b = hist[it.dataIndex] || {};
                                var cf = 0;
                                for (var k = 0; k <= it.dataIndex; k++) cf += (hist[k].frequency || 0);
                                return [
                                    'Frec: ' + it.raw + '%  |  N: ' + (b.count || 0),
                                    'P(X ≤ máx rango): ' + (cf * 100).toFixed(1) + '%'
                                ];
                            }
                        }
                    }
                },
                scales: {
                    x: { ticks: { maxTicksLimit: 8, font: { size: 10 } } },
                    y: { title: { display: true, text: 'Frecuencia (%)', font: { size: 10 } },
                         ticks: { font: { size: 10 } } }
                },
                layout: { padding: { top: 20 } }
            },
            plugins: [pctLinesPlugin]
        });
    }

    function renderMcCdfChart(tabId, hist) {
        var canvas = document.getElementById('mc-cdf-' + tabId);
        if (!canvas) return;
        var s = (_mcAllData[tabId] || {}).statistics || {};
        var cum = 0, labels = [], cdfVals = [];
        hist.forEach(function(b) {
            cum += b.frequency * 100;
            labels.push(fmtN((b.rangeMin + b.rangeMax) / 2));
            cdfVals.push(+Math.min(cum, 100).toFixed(2));
        });

        var markers = [
            { pct: 50, v: s.p50, c: '#6366f1', l: 'P50' },
            { pct: 80, v: s.p80, c: '#f59e0b', l: 'P80' },
            { pct: 90, v: s.p90, c: '#ef4444', l: 'P90' }
        ];

        var cdfAnnotPlugin = {
            id: 'cdfAnnot',
            afterDatasetsDraw: function(chart) {
                var ctx2 = chart.ctx;
                var xAxis = chart.scales.x;
                var yAxis = chart.scales.y;
                var meta = chart.getDatasetMeta(0);
                if (!meta.data.length) return;

                markers.forEach(function(mk) {
                    if (mk.v === undefined || mk.v === null) return;
                    var yPx = yAxis.getPixelForValue(mk.pct);
                    // Find first bar index where cdfVals crosses the percentile
                    var xPx = null;
                    for (var i = 0; i < cdfVals.length; i++) {
                        if (cdfVals[i] >= mk.pct) {
                            xPx = meta.data[i] ? meta.data[i].x : null;
                            break;
                        }
                    }
                    if (xPx === null) return;

                    ctx2.save();
                    // Línea horizontal desde eje Y hasta la curva
                    ctx2.beginPath();
                    ctx2.setLineDash([4, 3]);
                    ctx2.strokeStyle = mk.c;
                    ctx2.lineWidth = 1.4;
                    ctx2.moveTo(xAxis.left, yPx);
                    ctx2.lineTo(xPx, yPx);
                    ctx2.stroke();
                    // Drop-line vertical desde curva hasta eje X
                    ctx2.beginPath();
                    ctx2.moveTo(xPx, yPx);
                    ctx2.lineTo(xPx, yAxis.bottom);
                    ctx2.stroke();
                    ctx2.setLineDash([]);
                    // Punto en la curva
                    ctx2.beginPath();
                    ctx2.arc(xPx, yPx, 4.5, 0, Math.PI * 2);
                    ctx2.fillStyle = mk.c;
                    ctx2.fill();
                    ctx2.strokeStyle = '#fff';
                    ctx2.lineWidth = 1.5;
                    ctx2.stroke();
                    // Etiqueta en eje Y (izquierda)
                    ctx2.font = 'bold 9px system-ui,sans-serif';
                    ctx2.fillStyle = mk.c;
                    ctx2.textAlign = 'right';
                    ctx2.fillText(mk.l, xAxis.left - 3, yPx + 3);
                    // Valor en eje X (abajo)
                    ctx2.textAlign = 'center';
                    var lbl = fmtN(mk.v);
                    var tw = ctx2.measureText(lbl).width;
                    ctx2.fillStyle = 'rgba(255,255,255,0.85)';
                    ctx2.fillRect(xPx - tw / 2 - 2, yAxis.bottom + 3, tw + 4, 11);
                    ctx2.fillStyle = mk.c;
                    ctx2.fillText(lbl, xPx, yAxis.bottom + 12);
                    ctx2.restore();
                });
            }
        };

        if (_mcCdfCharts[tabId]) _mcCdfCharts[tabId].destroy();
        _mcCdfCharts[tabId] = new Chart(canvas, {
            type: 'line',
            data: { labels: labels, datasets: [{ label: 'P(X≤x) %', data: cdfVals,
                borderColor: 'rgba(99,102,241,1)',
                backgroundColor: function(ctx) {
                    var gradient = ctx.chart.ctx.createLinearGradient(0, 0, 0, ctx.chart.height || 200);
                    gradient.addColorStop(0, 'rgba(99,102,241,0.25)');
                    gradient.addColorStop(1, 'rgba(16,185,129,0.05)');
                    return gradient;
                },
                fill: true, tension: 0.4, pointRadius: 0, borderWidth: 2.5 }] },
            options: {
                responsive: true,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            title: function(it) {
                                var b = hist[it[0].dataIndex] || {};
                                return fmtN(b.rangeMin) + ' – ' + fmtN(b.rangeMax);
                            },
                            label: function(it) {
                                return 'P(X ≤ x) = ' + it.raw + '%';
                            }
                        }
                    }
                },
                scales: {
                    x: { ticks: { maxTicksLimit: 8, font: { size: 10 } } },
                    y: { min: 0, max: 100,
                         title: { display: true, text: 'Prob. acumulada (%)', font: { size: 10 } },
                         ticks: { font: { size: 10 }, stepSize: 10 } }
                },
                layout: { padding: { left: 30, bottom: 18 } }
            },
            plugins: [cdfAnnotPlugin]
        });
    }

    function renderMcPercentiles(tabId, s) {
        var el = document.getElementById('mc-pct-' + tabId);
        if (!el) return;
        var pcts = [['P5',s.p5],['P10',s.p10],['P15',s.p15],['P20',s.p20],
            ['P25',s.p25],['P30',s.p30],['P35',s.p35],['P40',s.p40],
            ['P45',s.p45],['P50',s.p50],['P55',s.p55],['P60',s.p60],
            ['P65',s.p65],['P70',s.p70],['P75',s.p75],['P80',s.p80],
            ['P85',s.p85],['P90',s.p90],['P95',s.p95]];

        var minV = s.p5 || 0, maxV = s.p95 || 1;
        var range = maxV - minV || 1;

        el.innerHTML = pcts.map(function(p) {
            var pct = parseFloat(p[0].replace('P',''));
            var isP50 = pct === 50, isP80 = pct === 80, isP90 = pct === 90;
            var rowCls, lblCls, valCls;
            if (isP90)      { rowCls='bg-red-50 dark:bg-red-900/20';    lblCls='text-red-600 dark:text-red-400 font-bold';    valCls='text-red-700 dark:text-red-300 font-extrabold'; }
            else if (isP80) { rowCls='bg-amber-50 dark:bg-amber-900/20'; lblCls='text-amber-600 dark:text-amber-400 font-bold'; valCls='text-amber-700 dark:text-amber-300 font-extrabold'; }
            else if (isP50) { rowCls='bg-indigo-50 dark:bg-indigo-900/20'; lblCls='text-indigo-600 dark:text-indigo-400 font-bold'; valCls='text-indigo-700 dark:text-indigo-300 font-extrabold'; }
            else            { rowCls='';                                  lblCls='text-slate-500 dark:text-slate-400';              valCls='text-slate-700 dark:text-slate-300 font-medium'; }

            var barW = Math.max(0, Math.min(100, ((p[1] - minV) / range) * 100)).toFixed(1);
            var barColor = isP90 ? '#ef4444' : isP80 ? '#f59e0b' : isP50 ? '#6366f1' : pct > 80 ? '#f87171' : pct > 50 ? '#a78bfa' : '#34d399';
            var badge = (isP50 || isP80 || isP90) ? '<span class="ml-1 text-xs opacity-60">' + (isP50?'Base':isP80?'Contrato':'Límite') + '</span>' : '';

            return '<div class="flex items-center gap-2 py-0.5 px-1.5 rounded ' + rowCls + '">' +
                '<span class="w-8 shrink-0 text-xs ' + lblCls + '">' + p[0] + '</span>' +
                badge +
                '<div class="flex-1 mx-1" style="height:4px;background:rgba(0,0,0,0.06);border-radius:2px">' +
                  '<div style="width:' + barW + '%;height:100%;background:' + barColor + ';border-radius:2px;opacity:0.7"></div>' +
                '</div>' +
                '<span class="w-24 text-right text-xs shrink-0 ' + valCls + '">' + fmtN(p[1]) + '</span>' +
                '</div>';
        }).join('');
    }

    function renderMcDecisions(tabId, data) {
        var el = document.getElementById('mc-decisions-' + tabId);
        if (!el) return;
        var s   = data.statistics;
        var m   = data.additionalMetrics || {};
        var typ = data.simulationType || '';
        var cv  = (s.mean && s.mean !== 0) ? Math.abs(s.stdDev / s.mean) * 100 : 0;

        // Nivel de riesgo
        var lvl, lvlColor, lvlBg, lvlIcon;
        if (cv < 20)      { lvl='BAJO';  lvlColor='text-emerald-700 dark:text-emerald-300'; lvlBg='bg-emerald-100 dark:bg-emerald-900/30 border-emerald-300 dark:border-emerald-700'; lvlIcon='check_circle'; }
        else if (cv < 50) { lvl='MEDIO'; lvlColor='text-amber-700 dark:text-amber-300';   lvlBg='bg-amber-100 dark:bg-amber-900/30 border-amber-300 dark:border-amber-700';   lvlIcon='warning'; }
        else              { lvl='ALTO';  lvlColor='text-red-700 dark:text-red-300';        lvlBg='bg-red-100 dark:bg-red-900/30 border-red-300 dark:border-red-700';           lvlIcon='dangerous'; }

        // Insights automáticos
        var insights = [];
        if (cv < 20)      insights.push({ c:'green', t: 'Baja variabilidad (CV='+cv.toFixed(1)+'%). El resultado es predecible.' });
        else if (cv < 50) insights.push({ c:'amber', t: 'Variabilidad moderada (CV='+cv.toFixed(1)+'%). Planificar con P75 como base.' });
        else              insights.push({ c:'red',   t: 'Alta variabilidad (CV='+cv.toFixed(1)+'%). Usar P90 como referencia conservadora.' });

        if (s.skewness > 1)       insights.push({ c:'red',   t: 'Asimetría derecha ('+s.skewness.toFixed(2)+'): existen escenarios extremos altos — la media subestima el peor caso.' });
        else if (s.skewness < -1) insights.push({ c:'amber', t: 'Asimetría izquierda ('+s.skewness.toFixed(2)+'): los escenarios negativos son frecuentes.' });
        else                      insights.push({ c:'green', t: 'Distribución simétrica ('+s.skewness.toFixed(2)+'): la media es un estimador confiable.' });

        if (s.kurtosis > 1)  insights.push({ c:'amber', t: 'Curtosis alta ('+s.kurtosis.toFixed(2)+'): cola pesada — mayor probabilidad de valores extremos.' });
        if (s.kurtosis < -1) insights.push({ c:'green', t: 'Curtosis baja ('+s.kurtosis.toFixed(2)+'): distribución más plana, menos valores extremos.' });

        var diffPct = s.mean && s.mean !== 0 ? Math.abs((s.mean - s.p50) / s.mean) * 100 : 0;
        if (diffPct > 10) insights.push({ c:'amber', t: 'Media ('+fmtN(s.mean)+') difiere del P50 ('+fmtN(s.p50)+') en '+diffPct.toFixed(1)+'%. Usar P50 para escenario base.' });

        // Insights tipo específico
        if ((typ === 'RiskAnalysis' || typ === 'ScheduleRiskAnalysis') && m.probabilityOfAnyRisk !== undefined)
            insights.push({ c:'blue', t: 'Probabilidad de que al menos un riesgo ocurra: '+(m.probabilityOfAnyRisk*100).toFixed(1)+'%.' });
        if (typ === 'ValueAtRisk' && m.valueAtRisk !== undefined)
            insights.push({ c:'red', t: 'VaR ('+((m.confidenceLevel||0.95)*100).toFixed(0)+'%): '+fmtN(m.valueAtRisk)+'. Pérdida potencial: '+fmtN(m.potentialLoss)+'.' });
        if (m.probabilityOnTime !== undefined)
            insights.push({ c: m.probabilityOnTime>=0.5?'green':'red', t: 'P(cumplir plazo determinístico): '+(m.probabilityOnTime*100).toFixed(1)+'%.' });

        // Recomendaciones
        var recs = [];
        var cont80 = s.p80 - s.mean, cont90 = s.p90 - s.mean;
        if (typ === 'CostEstimation') {
            recs = ['Presupuesto base (P50): '+fmtN(s.p50),
                    'Contingencia al P80: +'+fmtN(cont80)+' ('+( s.mean ? (cont80/s.mean*100).toFixed(1) : '?')+'%)',
                    'Contingencia al P90: +'+fmtN(cont90)+' ('+( s.mean ? (cont90/s.mean*100).toFixed(1) : '?')+'%)',
                    'Presupuesto conservador (P90): '+fmtN(s.p90)];
        } else if (typ === 'ScheduleEstimation') {
            recs = ['Plazo base P50 (50% prob.): '+fmtN(s.p50)+' días',
                    'Plazo P80 (80% prob.): '+fmtN(s.p80)+' días',
                    'Holgura P80 vs P50: +'+fmtN(s.p80-s.p50)+' días',
                    'Plazo pesimista P90: '+fmtN(s.p90)+' días'];
        } else if (typ === 'RiskAnalysis' || typ === 'ScheduleRiskAnalysis') {
            var unit = typ === 'ScheduleRiskAnalysis' ? ' días' : '';
            recs = ['Impacto esperado (media): '+fmtN(s.mean)+unit,
                    'Provisión al P80: '+fmtN(s.p80)+unit,
                    'Provisión al P90: '+fmtN(s.p90)+unit,
                    'Rango P25–P75: '+fmtN(s.p25)+' — '+fmtN(s.p75)+unit];
        } else if (typ === 'SRA' || typ === 'CRA') {
            var isS = typ === 'SRA';
            recs = [(isS?'Plazo combinado P50: ':'Costo combinado P50: ')+fmtN(s.p50)+(isS?' días':''),
                    (isS?'Plazo P80: ':'Costo P80: ')+fmtN(s.p80)+(isS?' días':''),
                    (isS?'Plazo P90: ':'Costo P90: ')+fmtN(s.p90)+(isS?' días':''),
                    'Desviación estándar: '+fmtN(s.stdDev)];
        } else if (typ === 'ValueAtRisk') {
            recs = ['Valor esperado portafolio: '+fmtN(s.mean),
                    'Escenario base (P50): '+fmtN(s.p50),
                    'Capital mínimo (P5): '+fmtN(s.p5),
                    'Escenario optimista (P95): '+fmtN(s.p95)];
        }

        var icoMap = { green:'check_circle', amber:'info', blue:'shield', red:'warning' };
        var clrMap = { green:'text-emerald-600 dark:text-emerald-400', amber:'text-amber-600 dark:text-amber-400',
                       blue:'text-blue-600 dark:text-blue-400', red:'text-red-600 dark:text-red-400' };

        el.innerHTML =
            '<div class="flex items-center gap-2 mb-3">' +
            '<span class="material-icons ' + lvlColor + '">' + lvlIcon + '</span>' +
            '<span class="text-xs font-bold uppercase tracking-wide ' + lvlColor + '">Nivel de incertidumbre</span>' +
            '<span class="ml-auto px-2 py-0.5 rounded-full text-xs font-bold border ' + lvlBg + ' ' + lvlColor + '">' + lvl + '</span>' +
            '</div>' +
            '<p class="text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wide mb-1">Análisis</p>' +
            '<div class="space-y-1 mb-3">' +
            insights.slice(0,4).map(function(ins) {
                return '<div class="flex gap-1.5 text-xs text-slate-700 dark:text-slate-300">' +
                    '<span class="material-icons ' + clrMap[ins.c] + ' flex-shrink-0" style="font-size:13px;margin-top:1px">' + icoMap[ins.c] + '</span>' +
                    '<span>' + ins.t + '</span></div>';
            }).join('') +
            '</div>' +
            '<p class="text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wide mb-1">Recomendaciones</p>' +
            '<ul class="space-y-0.5">' +
            recs.map(function(r) {
                return '<li class="text-xs text-slate-700 dark:text-slate-300 flex gap-1.5">' +
                    '<span class="material-icons text-indigo-500 flex-shrink-0" style="font-size:12px;margin-top:1px">arrow_right</span>' +
                    '<span>' + r + '</span></li>';
            }).join('') +
            '</ul>';
    }

    window.applyMcFilter = function(tabId) {
        var hist = _mcHistData[tabId];
        if (!hist) return;
        var fMin = parseFloat(document.getElementById('mc-fmin-' + tabId).value);
        var fMax = parseFloat(document.getElementById('mc-fmax-' + tabId).value);
        var filtered = hist.filter(function(b) {
            var mid = (b.rangeMin + b.rangeMax) / 2;
            if (!isNaN(fMin) && mid < fMin) return false;
            if (!isNaN(fMax) && mid > fMax) return false;
            return true;
        });
        var use = filtered.length > 0 ? filtered : hist;
        renderMcHistChart(tabId, use);
        renderMcCdfChart(tabId, use);
    };

    window.resetMcFilter = function(tabId) {
        document.getElementById('mc-fmin-' + tabId).value = '';
        document.getElementById('mc-fmax-' + tabId).value = '';
        renderMcHistChart(tabId, _mcHistData[tabId]);
        renderMcCdfChart(tabId, _mcHistData[tabId]);
    };

    // ── Probabilidad de plazo determinístico en Cronograma ───────────────────
    function renderPlannedDaysBadges(containerId, data) {
        var el = document.getElementById(containerId);
        if (!el) return;
        var m = data.additionalMetrics || {};
        var items = [];
        if (m.probabilityOnTime !== undefined)
            items.push({ label: 'P(cumplir plazo)', val: (m.probabilityOnTime * 100).toFixed(1) + '%', color: m.probabilityOnTime >= 0.5 ? 'emerald' : m.probabilityOnTime >= 0.3 ? 'amber' : 'red' });
        if (m.probabilityDelayed !== undefined)
            items.push({ label: 'P(retraso)', val: (m.probabilityDelayed * 100).toFixed(1) + '%', color: m.probabilityDelayed <= 0.5 ? 'emerald' : m.probabilityDelayed <= 0.7 ? 'amber' : 'red' });
        if (!items.length) { el.classList.add('hidden'); return; }
        var clr = { emerald:'bg-emerald-50 dark:bg-emerald-900/30 border-emerald-200 dark:border-emerald-700 text-emerald-700 dark:text-emerald-300',
                    amber:  'bg-amber-50 dark:bg-amber-900/30 border-amber-200 dark:border-amber-700 text-amber-700 dark:text-amber-300',
                    red:    'bg-red-50 dark:bg-red-900/30 border-red-200 dark:border-red-700 text-red-700 dark:text-red-300' };
        el.innerHTML = items.map(function(it) {
            return '<div class="rounded-xl border p-3 ' + clr[it.color] + '">' +
                '<div class="text-xs font-medium opacity-70 mb-1">' + it.label + '</div>' +
                '<div class="text-2xl font-bold">' + it.val + '</div></div>';
        }).join('');
        el.classList.remove('hidden');
    }

    // ── Helper: leer todos los inputs de una tabla ───────────────────────────
    function collectTableInputs(bodyId) {
        var body = document.getElementById(bodyId);
        if (!body) return [];
        var result = [];
        body.querySelectorAll('.data-row').forEach(function(row) {
            var obj = {};
            row.querySelectorAll('input[name], select[name]').forEach(function(inp) {
                var m = inp.name.match(/\[(\d+)\]\.(.+)/);
                if (m) {
                    var key = m[2];
                    obj[key] = (inp.type === 'number') ? (parseFloat(inp.value) || 0) : inp.value;
                }
            });
            result.push(obj);
        });
        return result;
    }

    // ── Copiar tabla fuente → tabla destino (para SRA/CRA) ──────────────────
    function populateBodyFromSource(sourceBodyId, destBodyId, type) {
        var srcRows = collectTableInputs(sourceBodyId);
        var destBody = document.getElementById(destBodyId);
        if (!destBody) return;
        destBody.innerHTML = '';
        if (!srcRows.length) {
            destBody.insertAdjacentHTML('beforeend', T[type](0));
            return;
        }
        srcRows.forEach(function(obj, i) {
            destBody.insertAdjacentHTML('beforeend', T[type](i));
            var row = destBody.querySelectorAll('.data-row')[i];
            row.querySelectorAll('input[name], select[name]').forEach(function(el) {
                var m = el.name.match(/\[(\d+)\]\.(.+)/);
                if (m && obj[m[2]] !== undefined) el.value = obj[m[2]];
            });
        });
    }

    function isBodyBlank(bodyId) {
        var rows = collectTableInputs(bodyId);
        if (!rows.length) return true;
        return rows.every(function(obj) {
            return !obj.Name && !obj.Cause && !obj.RiskEvent;
        });
    }

    window.syncSraFromSource = function(force) {
        if (force || isBodyBlank('sra-sched-body'))
            populateBodyFromSource('schedule-body', 'sra-sched-body', 'sra-sched');
        if (force || isBodyBlank('sra-risk-body'))
            populateBodyFromSource('sched-risks-body', 'sra-risk-body', 'sra-risk');
    };

    window.syncCraFromSource = function(force) {
        if (force || isBodyBlank('cra-cost-body'))
            populateBodyFromSource('costs-body', 'cra-cost-body', 'cra-cost');
        if (force || isBodyBlank('cra-risk-body'))
            populateBodyFromSource('risks-body', 'cra-risk-body', 'cra-risk');
    };

    // ── Calcular SRA ──────────────────────────────────────────────────────────
    window.calculateSra = function() {
        var errEl = document.getElementById('sra-error');
        var loadEl = document.getElementById('sra-loading');
        var resEl = document.getElementById('sra-result');
        errEl.classList.add('hidden'); resEl.classList.add('hidden');
        loadEl.classList.remove('hidden');

        var rawTasks = collectTableInputs('sra-sched-body');
        var tasks = rawTasks.filter(function(t) { return t.Name; }).map(function(t) {
            return { name: t.Name, minDays: t.MinDays||0, mostLikelyDays: t.MostLikelyDays||0, maxDays: t.MaxDays||0,
                     plannedDays: t.PlannedDays||0,
                     dependencies: t.DependenciesText ? t.DependenciesText.split(',').map(function(s){return s.trim();}).filter(Boolean) : [],
                     opportunities: t.Opportunities||0, threats: t.Threats||0,
                     distribution: t.Distribution||'Triangular' };
        });
        var rawRisks = collectTableInputs('sra-risk-body');
        var risks = rawRisks.filter(function(r) { return r.Cause || r.RiskEvent; }).map(function(r) {
            return { cause: r.Cause||'', riskEvent: r.RiskEvent||'', consequence: r.Consequence||'',
                     probability: r.Probability||0, minImpact: r.MinImpact||0, mostLikelyImpact: r.MostLikelyImpact||0,
                     maxImpact: r.MaxImpact||0, distribution: r.Distribution||'Triangular', observations: r.Observations||'' };
        });

        if (!tasks.length) { loadEl.classList.add('hidden'); errEl.textContent = 'Ingrese al menos una tarea en la tabla de Cronograma (sección SRA).'; errEl.classList.remove('hidden'); return; }

        var gpEl = document.getElementById('sra-global-planned');
        var simEl = document.getElementById('sra-simulations');
        var body = { tasks: tasks, risks: risks,
                     simulations: parseInt(simEl ? simEl.value : 10000) || 10000,
                     globalPlannedDays: gpEl && gpEl.value ? parseFloat(gpEl.value) : null };

        var tok = document.querySelector('input[name="__RequestVerificationToken"]');
        fetch('?handler=SraAjax', { method:'POST', headers:{ 'Content-Type':'application/json', 'RequestVerificationToken': tok ? tok.value : '' }, body: JSON.stringify(body) })
            .then(function(r) { return r.json(); })
            .then(function(d) {
                loadEl.classList.add('hidden');
                if (d.error) { errEl.textContent = d.error; errEl.classList.remove('hidden'); return; }
                resEl.classList.remove('hidden');
                _mcAllData['sra'] = d;
                renderMcResult('sra', d);
                renderPlannedDaysBadges('mc-schedule-planned-sra', d);
                updatePdfPanel();
            })
            .catch(function(e) { loadEl.classList.add('hidden'); errEl.textContent = e.message; errEl.classList.remove('hidden'); });
    };

    // ── Calcular CRA ──────────────────────────────────────────────────────────
    window.calculateCra = function() {
        var errEl = document.getElementById('cra-error');
        var loadEl = document.getElementById('cra-loading');
        var resEl = document.getElementById('cra-result');
        errEl.classList.add('hidden'); resEl.classList.add('hidden');
        loadEl.classList.remove('hidden');

        var rawComps = collectTableInputs('cra-cost-body');
        var components = rawComps.filter(function(c) { return c.Name; }).map(function(c) {
            return { name: c.Name, baseCost: c.BaseCost||0, estimationClass: c.EstimationClass||'',
                     minCost: c.MinCost||0, mostLikelyCost: c.MostLikelyCost||0, maxCost: c.MaxCost||0,
                     opportunityProbability: c.OpportunityProbability||0, opportunityAmount: c.OpportunityAmount||0,
                     threatProbability: c.ThreatProbability||0, threatAmount: c.ThreatAmount||0,
                     distribution: c.Distribution||'Triangular', observations: c.Observations||'' };
        });
        var rawRisks = collectTableInputs('cra-risk-body');
        var risks = rawRisks.filter(function(r) { return r.Cause || r.RiskEvent; }).map(function(r) {
            return { cause: r.Cause||'', riskEvent: r.RiskEvent||'', consequence: r.Consequence||'',
                     probability: r.Probability||0, minImpact: r.MinImpact||0, mostLikelyImpact: r.MostLikelyImpact||0,
                     maxImpact: r.MaxImpact||0, distribution: r.Distribution||'Triangular', observations: r.Observations||'' };
        });

        if (!components.length) { loadEl.classList.add('hidden'); errEl.textContent = 'Ingrese al menos un componente en la tabla de Costos (sección CRA).'; errEl.classList.remove('hidden'); return; }

        var simEl = document.getElementById('cra-simulations');
        var body = { components: components, risks: risks, simulations: parseInt(simEl ? simEl.value : 10000) || 10000 };

        var tok = document.querySelector('input[name="__RequestVerificationToken"]');
        fetch('?handler=CraAjax', { method:'POST', headers:{ 'Content-Type':'application/json', 'RequestVerificationToken': tok ? tok.value : '' }, body: JSON.stringify(body) })
            .then(function(r) { return r.json(); })
            .then(function(d) {
                loadEl.classList.add('hidden');
                if (d.error) { errEl.textContent = d.error; errEl.classList.remove('hidden'); return; }
                resEl.classList.remove('hidden');
                _mcAllData['cra'] = d;
                renderMcResult('cra', d);
                updatePdfPanel();
            })
            .catch(function(e) { loadEl.classList.add('hidden'); errEl.textContent = e.message; errEl.classList.remove('hidden'); });
    };

    // ── Correlaciones ─────────────────────────────────────────────────────────

    // Template para fila de variable de correlación
    var T_CORR_VAR = function(i) {
        return '<tr class="corr-var-row border-b border-slate-50 dark:border-slate-800" data-idx="' + i + '">' +
            '<td class="py-1 pr-2 text-slate-400 dark:text-slate-500 text-center w-6">' + (i+1) + '</td>' +
            '<td class="py-1 pr-2"><input type="text" class="mc-input w-full text-xs" placeholder="Nombre…" data-corr="name" /></td>' +
            '<td class="py-1 pr-2"><input type="text" class="mc-input w-full text-xs" placeholder="Unidad…" data-corr="unit" /></td>' +
            '<td class="py-1 pr-2"><select class="mc-input w-full text-xs" data-corr="dist" onchange="updateCorrP3Label(this)">' +
                '<option value="Triangular">Triangular</option>' +
                '<option value="PERT">PERT</option>' +
                '<option value="Normal">Normal</option>' +
                '<option value="Lognormal">Lognormal</option>' +
                '<option value="Uniform">Uniform</option>' +
            '</select></td>' +
            '<td class="py-1 pr-2 min-w-[8rem]"><input type="number" class="mc-input w-full text-xs" placeholder="P1" data-corr="p1" step="any" /></td>' +
            '<td class="py-1 pr-2 min-w-[8rem]"><input type="number" class="mc-input w-full text-xs" placeholder="P2" data-corr="p2" step="any" /></td>' +
            '<td class="py-1 pr-2 min-w-[8rem] corr-p3-cell"><input type="number" class="mc-input w-full text-xs" placeholder="P3" data-corr="p3" step="any" /></td>' +
            '<td class="py-1"><button type="button" onclick="removeCorrVar(this)" class="p-1 text-red-400 hover:text-red-600 dark:hover:text-red-400 rounded transition-colors"><span class="material-icons" style="font-size:14px">close</span></button></td>' +
        '</tr>';
    };

    window.addCorrVar = function() {
        var body = document.getElementById('corr-vars-body');
        if (!body) return;
        var idx = body.querySelectorAll('.corr-var-row').length;
        body.insertAdjacentHTML('beforeend', T_CORR_VAR(idx));
    };

    window.removeCorrVar = function(btn) {
        var body = document.getElementById('corr-vars-body');
        if (!body) return;
        if (body.querySelectorAll('.corr-var-row').length <= 2) return;
        btn.closest('.corr-var-row').remove();
        body.querySelectorAll('.corr-var-row').forEach(function(row, i) {
            row.dataset.idx = i;
            row.cells[0].textContent = i + 1;
        });
        rebuildCorrMatrix();
    };

    window.updateCorrP3Label = function(sel) {
        var dist = sel.value.toUpperCase();
        var p3cell = sel.closest('tr').querySelector('.corr-p3-cell input');
        if (!p3cell) return;
        if (dist === 'NORMAL' || dist === 'LOGNORMAL') {
            p3cell.placeholder = '(no aplica)';
            p3cell.disabled = true;
            p3cell.value = '';
        } else {
            p3cell.placeholder = 'P3 (Max)';
            p3cell.disabled = false;
        }
    };

    function readCorrVars() {
        var rows = document.querySelectorAll('#corr-vars-body .corr-var-row');
        return Array.from(rows).map(function(row) {
            return {
                name: row.querySelector('[data-corr="name"]').value.trim() || ('Var' + (parseInt(row.dataset.idx)+1)),
                unit: row.querySelector('[data-corr="unit"]').value.trim(),
                distribution: row.querySelector('[data-corr="dist"]').value,
                p1: parseFloat(row.querySelector('[data-corr="p1"]').value) || 0,
                p2: parseFloat(row.querySelector('[data-corr="p2"]').value) || 0,
                p3: parseFloat(row.querySelector('[data-corr="p3"]').value) || 0
            };
        });
    }

    /** Rellena el triángulo inferior con ρ uniforme; se reaplica tras "Actualizar matriz" si había un preset. */
    function _mcCorrPresetRefreshVisual(val) {
        var v = +val;
        if (!isFinite(v)) return;
        v = Math.max(-1, Math.min(1, v));
        var labels = { 0: 'Sin correlación (ρ = 0)', 0.2: 'Baja (ρ = 0.2)', 0.5: 'Media (ρ = 0.5)', 0.8: 'Alta (ρ = 0.8)' };
        var k = [0, 0.2, 0.5, 0.8].find(function(x) { return Math.abs(x - v) < 1e-9; });
        window._mcCorrPresetLabel = (k !== undefined && labels[k] !== undefined) ? labels[k] : ('ρ = ' + v);
        var lbl = document.getElementById('corr-mc-preset-label');
        if (lbl) {
            lbl.textContent = 'Usando: ' + window._mcCorrPresetLabel;
            lbl.classList.remove('hidden');
        }
        var hint = document.getElementById('corr-mc-preset-hint');
        if (hint) hint.classList.add('hidden');
        document.querySelectorAll('#corr-matrix-container input[data-pair]').forEach(function(inp) {
            inp.value = v;
        });
        var wrap = document.getElementById('corr-mc-preset-btns');
        if (wrap) {
            wrap.querySelectorAll('.corr-mc-preset-btn').forEach(function(b) {
                var p = parseFloat(b.getAttribute('data-mc-corr-preset'));
                var on = isFinite(p) && Math.abs(p - v) < 1e-9;
                b.classList.toggle('ring-2', on);
                b.classList.toggle('ring-pink-500', on);
                b.classList.toggle('ring-offset-1', on);
                b.classList.toggle('ring-offset-slate-50', on);
                b.classList.toggle('dark:ring-offset-slate-800', on);
                b.classList.toggle('bg-pink-50', on);
                b.classList.toggle('dark:bg-pink-900/20', on);
            });
        }
    }

    window.applyMcCorrPreset = function(val) {
        window._mcCorrPresetVal = +val;
        if (!isFinite(window._mcCorrPresetVal)) return;
        _mcCorrPresetRefreshVisual(window._mcCorrPresetVal);
    };

    window.rebuildCorrMatrix = function(clearValues) {
        var vars = readCorrVars();
        var n = vars.length;
        var container = document.getElementById('corr-matrix-container');
        if (n < 2) {
            container.innerHTML = '<span class="text-xs text-slate-400 dark:text-slate-500 italic">Agrega al menos 2 variables y haz clic en "Actualizar matriz".</span>';
            return;
        }

        // Preserve existing values unless explicitly clearing
        var existing = {};
        if (!clearValues) {
            container.querySelectorAll('input[data-pair]').forEach(function(inp) {
                existing[inp.dataset.pair] = inp.value;
            });
        }

        var html = '<div class="overflow-x-auto"><table class="text-xs border-collapse">';
        // Header row
        html += '<tr><th class="w-20 border border-slate-200 dark:border-slate-700 p-1 bg-slate-50 dark:bg-slate-800"></th>';
        for (var j = 0; j < n; j++) {
            html += '<th class="border border-slate-200 dark:border-slate-700 p-1 bg-slate-50 dark:bg-slate-800 text-center text-slate-600 dark:text-slate-300 max-w-20 truncate" title="' + escHtml(vars[j].name) + '">' +
                '<span class="block truncate max-w-20">' + escHtml(vars[j].name.length > 8 ? vars[j].name.substring(0,7)+'…' : vars[j].name) + '</span>' +
                '<span class="text-slate-400 font-normal">' + (vars[j].unit ? '('+escHtml(vars[j].unit)+')' : '') + '</span>' +
            '</th>';
        }
        html += '</tr>';

        // Data rows — only lower triangle is editable; upper triangle is blank
        for (var i = 0; i < n; i++) {
            html += '<tr>';
            html += '<td class="border border-slate-200 dark:border-slate-700 p-1 bg-slate-50 dark:bg-slate-800 text-slate-600 dark:text-slate-300 font-medium text-right max-w-20">' +
                '<span class="block truncate max-w-20" title="' + escHtml(vars[i].name) + '">' +
                escHtml(vars[i].name.length > 8 ? vars[i].name.substring(0,7)+'…' : vars[i].name) + '</span>' +
            '</td>';
            for (var jj = 0; jj < n; jj++) {
                if (i === jj) {
                    // Diagonal — always 1
                    html += '<td class="border border-slate-200 dark:border-slate-700 p-1 bg-slate-100 dark:bg-slate-800 text-center text-slate-500 dark:text-slate-400 font-bold w-16 select-none">1.00</td>';
                } else if (jj > i) {
                    // Upper triangle — always blank
                    html += '<td class="border border-slate-200 dark:border-slate-700 p-1 w-16 bg-slate-50 dark:bg-slate-900"></td>';
                } else {
                    // Lower triangle — editable input
                    var cKey  = i + '_' + jj;
                    var cPrev = existing[cKey] !== undefined ? existing[cKey]
                              : (existing[jj + '_' + i] !== undefined ? existing[jj + '_' + i] : '0');
                    html += '<td class="border border-slate-200 dark:border-slate-700 p-0 w-16 bg-pink-50/40 dark:bg-pink-950/20">' +
                        '<input type="number" ' +
                        'class="w-full px-1 py-1 text-center text-xs border-0 focus:outline-none focus:ring-1 focus:ring-pink-400 rounded-none bg-pink-50/40 dark:bg-pink-950/20 text-slate-700 dark:text-slate-300" ' +
                        'data-pair="' + cKey + '" min="-1" max="1" step="0.05" value="' + escHtml(cPrev) + '" ' +
                        'title="ρ ' + escHtml(vars[jj].name) + ' ↔ ' + escHtml(vars[i].name) + '" />' +
                    '</td>';
                }
            }
            html += '</tr>';
        }
        html += '</table></div>';
        html += '<p class="mt-2 text-xs text-slate-400 dark:text-slate-500 flex items-center gap-1">' +
            '<span class="inline-block w-3 h-3 rounded-sm bg-pink-50 border border-pink-200 dark:bg-pink-950/40 dark:border-pink-800"></span>' +
            'Solo el triángulo inferior (rosa) es editable. Diagonal siempre = 1.</p>';
        container.innerHTML = html;
        if (window._mcCorrPresetVal != null && isFinite(+window._mcCorrPresetVal)) {
            _mcCorrPresetRefreshVisual(window._mcCorrPresetVal);
        }
    };

    window.syncCorrMirror = function() { /* no-op: upper triangle is now blank */ };

    // ── Correlación con EAT total / CAPEX (mismo servicio que Resumen Global del Taller) ──
    function _mcBuildSymCorrMatrix(n) {
        var M = [];
        for (var i = 0; i < n; i++) {
            M[i] = [];
            for (var j = 0; j < n; j++) {
                if (i === j) { M[i][j] = 1; continue; }
                var r = Math.max(i, j), c = Math.min(i, j);
                var inp = document.querySelector('#corr-matrix-container input[data-pair="' + r + '_' + c + '"]');
                var v = inp ? (parseFloat(inp.value) || 0) : 0;
                M[i][j] = v;
            }
        }
        return M;
    }
    function _mapCorrSourceToTaller(src) {
        if (src === 'contracts') return 'contratos';
        if (src === 'both') return 'ambos';
        return 'riesgos';
    }
    function _mcFmtN(v, d) {
        if (v == null || !isFinite(v)) return '—';
        return new Intl.NumberFormat('es-CL', { minimumFractionDigits: d, maximumFractionDigits: d }).format(v);
    }
    function _mcUsd(v) { return '$' + _mcFmtN(v, 0) + ' USD'; }

    function _hideCorrLegacyPanels(hide) {
        var el = document.getElementById('corr-legacy-copula-panels');
        if (el) el.classList.toggle('hidden', !!hide);
    }
    function _showCorrTallerPanel(show) {
        var p = document.getElementById('corr-eat-taller-panel');
        if (p) p.classList.toggle('hidden', !show);
    }

    function renderCorrTallerEAT(d, resumen, proyectoIdGuia, varSourceTaller) {
        var panel = document.getElementById('corr-eat-taller-panel');
        if (!panel) return;
        _hideCorrLegacyPanels(false);
        _showCorrTallerPanel(true);
        Object.keys(_corrCharts).forEach(function(k) {
            try { _corrCharts[k].destroy(); } catch (e) {}
            delete _corrCharts[k];
        });

        var tot = resumen && resumen.totales ? resumen.totales : {};
        var capexRef   = tot.capex != null ? parseFloat(tot.capex) : 0;
        var eatDetRef  = tot.eat  != null ? parseFloat(tot.eat)  : 0;
        var proyIdR    = resumen && resumen.proyecto && resumen.proyecto.id != null ? String(resumen.proyecto.id) : '';
        var proyMatch  = proyectoIdGuia && proyIdR && String(proyectoIdGuia) === proyIdR;
        var budgetOk   = capexRef > 0 && proyMatch;
        var pctEatDet  = capexRef > 0 && isFinite(eatDetRef) ? (eatDetRef / capexRef) * 100 : null;

        var srcLabels = { contratos: 'Contratos', riesgos: 'Riesgos', ambos: 'Ambos (Contratos + Riesgos)' };
        var srcUsed   = d.varSource || varSourceTaller || 'riesgos';
        var srcLabel  = srcLabels[srcUsed] || srcUsed;
        var nV = srcUsed === 'contratos' ? (d.nContratos || 0)
            : srcUsed === 'ambos' ? ((d.nContratos || 0) + (d.nRiesgos || 0))
            : (d.nRiesgos || 0);

        var dP10 = d.deltaP10, dP50 = d.deltaP50, dP80 = d.deltaP80, dP90 = d.deltaP90, dCv = d.deltaCvar90;
        var sinP10 = d.refP10, sinP50 = d.refP50, sinP80 = d.refP80, sinP90 = d.refP90, sinCvar = d.refCvar90;
        if (sinP10 == null && d.p10 != null && dP10 != null) sinP10 = d.p10 / (1 + dP10 / 100);
        if (sinP50 == null && d.p50 != null && dP50 != null) sinP50 = d.p50 / (1 + dP50 / 100);
        if (sinP80 == null && d.p80 != null && dP80 != null) sinP80 = d.p80 / (1 + dP80 / 100);
        if (sinP90 == null && d.p90 != null && dP90 != null) sinP90 = d.p90 / (1 + dP90 / 100);
        if (sinCvar == null && d.cvar90 != null && dCv != null) sinCvar = d.cvar90 / (1 + dCv / 100);

        var absD90usd = (d.p90 != null && sinP90 != null) ? Math.abs(d.p90 - sinP90) : 0;
        var absD90 = Math.abs(dP90 || 0);
        var dSign = function(v) { return (v > 0 ? '+' : '') + (isFinite(v) ? v.toFixed(1) : '0') + '%'; };
        var U = _mcUsd;
        var pctC = function(v) {
            if (capexRef <= 0 || v == null || !isFinite(v)) return '<td class="px-3 py-2 text-xs text-right text-slate-400">—</td>';
            return '<td class="px-3 py-2 text-xs text-right tabular-nums text-slate-500 dark:text-slate-400">' + _mcFmtN((v / capexRef) * 100, 1) + '%</td>';
        };
        var row = function(lbl, a, b, dlt) {
            var cls = Math.abs(dlt) < 1 ? 'text-emerald-600' : Math.abs(dlt) < 3 ? 'text-amber-600' : Math.abs(dlt) < 5 ? 'text-orange-600' : 'text-red-600';
            return '<tr class="border-b border-slate-100 dark:border-slate-800">' +
                '<td class="px-4 py-2 text-xs font-bold text-slate-600 dark:text-slate-300">' + escHtml(lbl) + '</td>' +
                '<td class="px-4 py-2 text-xs text-right tabular-nums text-slate-500">' + (a != null ? U(a) : '—') + '</td>' + pctC(a) +
                '<td class="px-4 py-2 text-xs text-right tabular-nums font-semibold text-slate-800 dark:text-slate-100">' + U(b) + '</td>' + pctC(b) +
                '<td class="px-4 py-2 text-xs text-right tabular-nums font-semibold ' + cls + '">' + dSign(dlt) + '</td></tr>';
        };

        var impLvl = absD90 > 5 ? 'alto' : absD90 > 3 ? 'moderado' : 'bajo';
        var impEmoji = absD90 > 5 ? '🔴' : absD90 > 3 ? '🟡' : '🟢';
        var impLabel = absD90 > 5 ? 'ALTO' : absD90 > 3 ? 'MODERADO' : 'BAJO';
        var impBorder = absD90 > 5 ? 'border-red-200 dark:border-red-800' : absD90 > 3 ? 'border-orange-200' : 'border-emerald-200';
        var impBg = absD90 > 5 ? 'bg-red-50 dark:bg-red-900/20' : absD90 > 3 ? 'bg-orange-50 dark:bg-orange-900/20' : 'bg-emerald-50 dark:bg-emerald-900/20';
        var impTxt = absD90 > 5 ? 'text-red-700 dark:text-red-300' : absD90 > 3 ? 'text-orange-700' : 'text-emerald-700';
        var progW = Math.min(absD90 / 5 * 100, 100).toFixed(1);
        var barC = absD90 > 5 ? 'bg-red-500' : absD90 > 3 ? 'bg-orange-500' : 'bg-emerald-500';

        var budgetHtml = !budgetOk
            ? ('<div class="rounded-lg border border-amber-200 dark:border-amber-800 bg-amber-50/50 dark:bg-amber-900/15 px-3 py-2 text-xs text-amber-900 dark:text-amber-100 flex gap-2">' +
                '<span class="material-icons text-sm">info</span><span>Abre <strong>Taller de Costos → Resumen</strong> con este proyecto o confirma que el mismo esté activo, para alinear CAPEX y EAT determinístico con el backend.</span></div>')
            : ('<div class="rounded-xl border border-teal-200 dark:border-teal-800 bg-teal-50/60 dark:bg-teal-900/15 p-4">' +
                '<div class="text-xs font-bold text-teal-800 dark:text-teal-200 uppercase tracking-wide mb-2 flex items-center gap-1">' +
                '<span class="material-icons text-sm">account_balance</span> Puente con el presupuesto (mismo criterio que Resumen Global)</div>' +
                '<p class="text-xs text-teal-900/80 mb-3">Montos alineados con las tarjetas KPI del Taller: CAPEX y EAT determinístico; percentiles del <strong>EAT total simulado</strong> con la matriz de esta pestaña.</p>' +
                '<div class="grid grid-cols-2 lg:grid-cols-4 gap-3 text-xs">' +
                '<div class="rounded-lg bg-white/70 dark:bg-slate-900/40 p-2 border border-teal-100"><div class="text-slate-500 mb-0.5">CAPEX total</div><div class="font-bold tabular-nums">' + U(capexRef) + '</div></div>' +
                '<div class="rounded-lg bg-white/70 dark:bg-slate-900/40 p-2 border border-teal-100"><div class="text-slate-500 mb-0.5">EAT determinístico</div><div class="font-bold tabular-nums">' + U(eatDetRef) + '</div>' +
                (pctEatDet != null ? '<div class="text-slate-400">' + _mcFmtN(pctEatDet, 1) + '% del CAPEX</div>' : '') + '</div>' +
                '<div class="rounded-lg bg-white/70 dark:bg-slate-900/40 p-2 border border-teal-100"><div class="text-slate-500 mb-0.5">P90 con correlación</div><div class="font-bold tabular-nums text-rose-700">' + U(d.p90) + '</div><div class="text-slate-400">' + _mcFmtN((d.p90 / capexRef) * 100, 1) + '% del CAPEX</div></div>' +
                '<div class="rounded-lg bg-white/70 dark:bg-slate-900/40 p-2 border border-teal-100"><div class="text-slate-500 mb-0.5">Brecha P90 − CAPEX</div><div class="font-bold tabular-nums ' + (d.p90 > capexRef ? 'text-red-600' : 'text-emerald-600') + '">' + (d.p90 > capexRef ? '+' : '') + U(d.p90 - capexRef) + '</div><div class="text-slate-400">Cola vs techo</div></div>' +
                '</div></div>');

        var html = '<div class="space-y-4 border-t border-slate-200 dark:border-slate-700 pt-4">' +
            '<div class="flex flex-wrap items-center gap-2"><span class="material-icons text-rose-500 text-base">hub</span>' +
            '<h4 class="text-sm font-semibold text-slate-700 dark:text-slate-300">Resultado — Cópula Gaussiana (EAT total)</h4>' +
            '<span class="text-xs bg-rose-50 dark:bg-rose-900/30 text-rose-700 border border-rose-200 px-2 py-0.5 rounded-full font-medium">' + escHtml(srcLabel) + ' · ' + nV + ' vars</span>' +
            '<span class="text-xs text-slate-400 ml-auto">' + _mcFmtN(d.nSimulaciones, 0) + ' iter</span></div>' +
            budgetHtml +
            '<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">' +
            '<div class="rounded-xl border border-indigo-200 dark:border-indigo-800 bg-indigo-50 dark:bg-indigo-900/20 p-4">' +
            '<div class="text-xs font-bold text-indigo-600 uppercase tracking-wide mb-2">Hipótesis de Correlación</div>' +
            '<p class="text-xs text-slate-600 dark:text-slate-300">Matriz personalizada en esta pestaña, aplicada vía Cholesky al conjunto de variables del proyecto (orden: mismo que al cargar desde Taller / MC).</p></div>' +
            '<div class="rounded-xl border ' + impBorder + ' ' + impBg + ' p-4">' +
            '<div class="text-xs font-bold ' + impTxt + ' uppercase tracking-wide mb-2">Impacto en P90</div>' +
            '<div class="text-2xl font-bold ' + impTxt + ' tabular-nums">' + (dP90 >= 0 ? '↑' : '↓') + ' ' + dSign(dP90) + '</div>' +
            '<p class="text-xs text-slate-500 mt-1">≈ ' + U(absD90usd) + '</p>' +
            '<div class="mt-2 h-2.5 bg-white/60 rounded-full overflow-hidden"><div class="h-2.5 ' + barC + ' rounded-full" style="width:' + progW + '%"></div></div>' +
            '<p class="text-xs font-bold ' + impTxt + ' mt-1">' + impEmoji + ' Impacto: ' + impLabel + '</p></div></div>' +
            '<div class="rounded-xl border border-slate-200 dark:border-slate-700 overflow-hidden">' +
            '<div class="px-4 py-2.5 bg-slate-50 dark:bg-slate-800 border-b text-xs font-semibold">Comparativa — Sin vs Con correlación (EAT total)</div>' +
            '<div class="overflow-x-auto"><table class="w-full text-left"><thead><tr class="text-xs text-slate-400 border-b border-slate-200">' +
            '<th class="px-4 py-2">Percentil</th><th class="px-4 py-2 text-right">Sin</th><th class="px-3 py-2 text-right">% CAPEX</th>' +
            '<th class="px-4 py-2 text-right">Con</th><th class="px-3 py-2 text-right">% CAPEX</th><th class="px-4 py-2 text-right">Δ %</th></tr></thead><tbody>' +
            row('P10', sinP10, d.p10, dP10) + row('P50', sinP50, d.p50, dP50) + row('P80', sinP80, d.p80, dP80) + row('P90', sinP90, d.p90, dP90) + row('CVaR90', sinCvar, d.cvar90, dCv) +
            '</tbody></table></div></div></div>';
        panel.innerHTML = html;
    }

    window.runCorrelation = function() {
        var vars = readCorrVars();
        if (vars.length < 2) {
            mcDialogAlert('Agrega al menos 2 variables.', { title: 'Variables insuficientes', variant: 'warning' });
            return;
        }

        var simEl   = document.getElementById('corr-simulations');
        var iter    = Math.min(50000, Math.max(1000, parseInt(simEl ? simEl.value : 10000, 10) || 10000));
        var loadEl  = document.getElementById('corr-loading');
        var errEl   = document.getElementById('corr-error');
        var resEl   = document.getElementById('corr-result');
        var tok     = document.querySelector('input[name="__RequestVerificationToken"]');

        loadEl.classList.remove('hidden');
        errEl.classList.add('hidden');
        resEl.classList.add('hidden');
        _showCorrTallerPanel(false);
        var _eatP = document.getElementById('corr-eat-taller-panel');
        if (_eatP) _eatP.innerHTML = '';
        _hideCorrLegacyPanels(false);

        // Proyecto Taller + fuente automática: mismo backend que el bloque "Correlación" del Resumen Global
        if (_corrTcId && _corrSource && _corrSource !== 'manual') {
            var n = vars.length;
            var varSourceTaller = _mapCorrSourceToTaller(_corrSource);
            var matriz = _mcBuildSymCorrMatrix(n);
            var bodyTaller = { proyectoId: _corrTcId, matriz: matriz, varSource: varSourceTaller, iteraciones: iter };

            Promise.all([
                fetch('/TallerCostos?handler=ResumenGlobalJson&proyectoId=' + encodeURIComponent(_corrTcId), { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                    .then(function(r) { return r.ok ? r.json() : null; }).catch(function() { return null; }),
                fetch('/TallerCostos?handler=Correlacion', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tok ? tok.value : '', 'X-Requested-With': 'XMLHttpRequest' },
                    body: JSON.stringify(bodyTaller)
                }).then(function(r) { return r.json(); })
            ]).then(function(arr) {
                var resumen = arr[0];
                var d = arr[1];
                if (d && d.error) {
                    loadEl.classList.add('hidden');
                    errEl.textContent = d.error;
                    errEl.classList.remove('hidden');
                    return;
                }
                resEl.classList.remove('hidden');
                renderCorrTallerEAT(d, resumen, _corrTcId, varSourceTaller);
                document.getElementById('badge-corr').classList.remove('hidden');

                var pairsViz = [];
                document.querySelectorAll('#corr-matrix-container input[data-pair]').forEach(function(inp) {
                    var parts = inp.dataset.pair.split('_');
                    var a = parseInt(parts[0], 10), b = parseInt(parts[1], 10);
                    var v = parseFloat(inp.value) || 0;
                    if (v !== 0) pairsViz.push({ indexA: a, indexB: b, spearmanTarget: Math.max(-1, Math.min(1, v)) });
                });
                var bodyViz = { variables: vars, pairs: pairsViz, simulations: iter };
                return fetch('?handler=CorrelationAjax', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tok ? tok.value : '' },
                    body: JSON.stringify(bodyViz)
                })
                    .then(function(r) { return r.json(); })
                    .then(function(vd) {
                        loadEl.classList.add('hidden');
                        if (vd && !vd.error && vd.achievedMatrix) {
                            renderCorrResult(vd);
                            saveResultToStorage('corr', { mode: 'tallerEat', data: d, proyectoId: _corrTcId, corrViz: vd });
                        } else {
                            saveResultToStorage('corr', { mode: 'tallerEat', data: d, proyectoId: _corrTcId });
                        }
                    })
                    .catch(function() {
                        loadEl.classList.add('hidden');
                        saveResultToStorage('corr', { mode: 'tallerEat', data: d, proyectoId: _corrTcId });
                    });
            }).catch(function(e) {
                loadEl.classList.add('hidden');
                errEl.textContent = e.message || String(e);
                errEl.classList.remove('hidden');
            });
            return;
        }

        var pairs = [];
        document.querySelectorAll('#corr-matrix-container input[data-pair]').forEach(function(inp) {
            var parts = inp.dataset.pair.split('_');
            var a = parseInt(parts[0], 10), b = parseInt(parts[1], 10);
            var v = parseFloat(inp.value) || 0;
            if (v !== 0) pairs.push({ indexA: a, indexB: b, spearmanTarget: Math.max(-1, Math.min(1, v)) });
        });
        var body = { variables: vars, pairs: pairs, simulations: iter };

        fetch('?handler=CorrelationAjax', { method:'POST', headers:{ 'Content-Type':'application/json', 'RequestVerificationToken': tok ? tok.value : '' }, body: JSON.stringify(body) })
            .then(function(r) { return r.json(); })
            .then(function(d) {
                loadEl.classList.add('hidden');
                if (d.error) { errEl.textContent = d.error; errEl.classList.remove('hidden'); return; }
                resEl.classList.remove('hidden');
                _showCorrTallerPanel(false);
                var _e2 = document.getElementById('corr-eat-taller-panel');
                if (_e2) _e2.innerHTML = '';
                _hideCorrLegacyPanels(false);
                renderCorrResult(d);
                document.getElementById('badge-corr').classList.remove('hidden');
                saveResultToStorage('corr', d);
            })
            .catch(function(e) { loadEl.classList.add('hidden'); errEl.textContent = e.message; errEl.classList.remove('hidden'); });
    };

    // ── Modo de entrada de matriz (manual | ai) ──────────────────────────────
    var _corrMatrixMode = 'manual';

    window.setCorrMatrixMode = function(mode) {
        _corrMatrixMode = mode;
        ['manual','ai'].forEach(function(m) {
            var btn = document.getElementById('corr-mode-btn-' + m);
            if (!btn) return;
            var active = m === mode;
            btn.className = 'corr-mode-btn px-3 py-1 font-medium transition-colors' +
                (m !== 'manual' ? ' border-l border-slate-200 dark:border-slate-700' : '') +
                (active ? ' bg-slate-700 text-white dark:bg-slate-600' : ' text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800');
        });
    };

    // ── IA: sugerencia automática de correlaciones ──────────────────────────
    // Almacena el último resultado IA para generar evidencia .md
    var _corrLastAiResult = null;
    var _corrLastVars     = null;

    window.runCorrMatrixAI = function() {
        var vars = readCorrVars();
        if (vars.length < 2) {
            mcDialogAlert('Agrega al menos 2 variables antes de solicitar sugerencia IA.', { title: 'Variables insuficientes', variant: 'warning' });
            return;
        }

        var withJust   = !!(document.getElementById('corr-with-justification') || {checked: true}).checked;
        var nPairs     = vars.length * (vars.length - 1) / 2;
        var bigDataset = vars.length > 10; // >10 vars → reglas de dominio (sin Ollama)

        var loadEl    = document.getElementById('corr-ai-loading');
        var panelEl   = document.getElementById('corr-ai-panel');
        var errEl     = document.getElementById('corr-error');
        var btn       = document.getElementById('corr-ai-btn');
        var btnLabel  = document.getElementById('corr-ai-btn-label');
        var elapsedEl = document.getElementById('corr-ai-elapsed');
        var dlBtn     = document.getElementById('corr-download-md-btn');

        if (loadEl)  loadEl.classList.remove('hidden');
        if (panelEl) panelEl.classList.add('hidden');
        if (errEl)   errEl.classList.add('hidden');
        if (dlBtn)   dlBtn.classList.add('hidden');
        if (btn)     { btn.disabled = true; }
        if (btnLabel) btnLabel.textContent = bigDataset ? 'Calculando…' : 'Analizando…';
        // Mensaje spinner adaptado al modo
        var spinnerText = loadEl ? loadEl.querySelector('span:first-of-type') : null;
        if (spinnerText) {
            spinnerText.textContent = bigDataset
                ? 'IA MC: aplicando reglas de dominio CODELCO (' + vars.length + ' variables, ' + nPairs + ' pares)…'
                : 'IA MC analizando correlaciones del proyecto…';
        }

        // Contador de tiempo para feedback visual
        var startTs = Date.now();
        var elapsedTimer = setInterval(function() {
            var s = Math.round((Date.now() - startTs) / 1000);
            if (elapsedEl) elapsedEl.textContent = '(' + s + 's)';
        }, 1000);

        function stopSpinner() {
            clearInterval(elapsedTimer);
            if (loadEl)   loadEl.classList.add('hidden');
            if (elapsedEl) elapsedEl.textContent = '';
            if (btn)      btn.disabled = false;
            if (btnLabel) btnLabel.textContent = 'Sugerir con IA';
        }

        // Rebuild matrix first so cells exist
        rebuildCorrMatrix();

        var tok = document.querySelector('input[name="__RequestVerificationToken"]');
        var sel = document.getElementById('corr-project-select');
        var projLabel = (sel && sel.selectedIndex > 0) ? sel.options[sel.selectedIndex].text : '';

        var body = { variables: vars, projectName: projLabel, source: _corrSource, withJustification: withJust };

        fetch('?handler=CorrMatrixAi', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tok ? tok.value : '' },
            body: JSON.stringify(body)
        })
        .then(function(r) { return r.json(); })
        .then(function(d) {
            stopSpinner();
            if (d.error) {
                if (errEl) { errEl.textContent = 'Error IA: ' + d.error; errEl.classList.remove('hidden'); }
                return;
            }
            setCorrMatrixMode('ai');

            var pairs = d.pairs || [];
            if (pairs.length === 0) {
                if (errEl) { errEl.textContent = 'La IA no generó valores de correlación. Intente de nuevo o use el modo manual.'; errEl.classList.remove('hidden'); }
                return;
            }

            // Guardar para descarga de evidencia
            _corrLastAiResult = d;
            _corrLastVars     = vars;

            // Apply values to matrix cells (lower triangle only: key = max_min)
            pairs.forEach(function(p) {
                var row = Math.max(p.i, p.j);
                var col = Math.min(p.i, p.j);
                var key = row + '_' + col;
                var val = Math.max(-1, Math.min(1, parseFloat(p.value) || 0));
                var inp = document.querySelector('#corr-matrix-container input[data-pair="' + key + '"]');
                if (inp) {
                    inp.value = val.toFixed(2);
                    inp.style.transition = 'background 0.4s';
                    inp.style.background = 'rgba(139,92,246,0.18)';
                    setTimeout(function() { inp.style.background = ''; inp.style.transition = ''; }, 1200);
                }
            });

            // Show AI panel
            if (panelEl) panelEl.classList.remove('hidden');

            // Scenario label
            var scenarioLabel = document.getElementById('corr-ai-scenario-label');
            var scenarioBadge = document.getElementById('corr-ai-scenario-badge');
            if (scenarioLabel && d.scenario) { scenarioLabel.textContent = d.scenario; scenarioLabel.classList.remove('hidden'); }
            if (scenarioBadge && d.scenario) scenarioBadge.textContent = d.scenario;

            // Narrative analysis (solo si se solicitó con justificación)
            var analysisEl = document.getElementById('corr-ai-analysis-text');
            if (analysisEl) {
                if (d.analysis) {
                    var html = escHtml(d.analysis)
                        .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
                        .replace(/^##\s+(.+)$/gm, '<p class="font-bold text-violet-700 dark:text-violet-300 mt-2">$1</p>')
                        .replace(/^#\s+(.+)$/gm,  '<p class="font-bold text-violet-700 dark:text-violet-300 mt-2">$1</p>')
                        .replace(/\n\n/g, '</p><p class="mt-1">')
                        .replace(/\n/g, '<br>');
                    analysisEl.innerHTML = '<p>' + html + '</p>';
                } else {
                    analysisEl.innerHTML = '<p class="text-slate-400 dark:text-slate-500 italic">Análisis narrativo omitido (modo sin justificación).</p>';
                }
            }

            // Pair justifications table
            var pairsPanel  = document.getElementById('corr-ai-pairs-panel');
            var pairsBody   = document.getElementById('corr-ai-pairs-body');
            var pairsCount  = document.getElementById('corr-ai-pairs-count');
            if (!withJust) {
                if (pairsPanel) pairsPanel.style.display = 'none';
            } else {
                if (pairsPanel) pairsPanel.style.display = '';
                if (pairsCount) pairsCount.textContent = pairs.length + ' pares';
                if (pairsBody) {
                    pairsBody.innerHTML = pairs.map(function(p, idx) {
                        var v        = parseFloat(p.value) || 0;
                        var absV     = Math.abs(v);
                        var color    = v < -0.1 ? 'text-sky-600 dark:text-sky-400'
                                     : absV > 0.6 ? 'text-rose-600 dark:text-rose-400'
                                     : absV > 0.35 ? 'text-amber-600 dark:text-amber-400'
                                     : 'text-emerald-600 dark:text-emerald-400';
                        var colorBg  = v < -0.1 ? 'bg-sky-50 dark:bg-sky-950/20 border-sky-200 dark:border-sky-800'
                                     : absV > 0.6 ? 'bg-rose-50 dark:bg-rose-950/20 border-rose-200 dark:border-rose-800'
                                     : absV > 0.35 ? 'bg-amber-50 dark:bg-amber-950/20 border-amber-200 dark:border-amber-800'
                                     : 'bg-emerald-50 dark:bg-emerald-950/20 border-emerald-200 dark:border-emerald-800';
                        var strength = v < -0.1 ? 'Inversa'
                                     : absV > 0.7 ? 'Fuerte'
                                     : absV > 0.4 ? 'Moderada'
                                     : absV > 0.15 ? 'Débil' : 'Mínima';
                        var nameA = vars[p.i] ? vars[p.i].name : ('Var' + p.i);
                        var nameB = vars[p.j] ? vars[p.j].name : ('Var' + p.j);
                        var sign  = v > 0 ? '+' : '';
                        var hasReason = !!(p.reason && p.reason.trim());
                        return '<details class="border-b border-violet-100 dark:border-violet-900/60 last:border-b-0">' +
                            '<summary class="flex items-center gap-2 px-3 py-2 cursor-pointer select-none list-none hover:bg-violet-50/60 dark:hover:bg-violet-950/20 transition-colors group">' +
                                '<span class="flex-shrink-0 text-slate-400 dark:text-slate-500 group-open:rotate-90 transition-transform duration-150 material-icons" style="font-size:14px">chevron_right</span>' +
                                '<span class="flex-shrink-0 font-mono font-bold w-12 text-right text-sm ' + color + '">' + sign + v.toFixed(2) + '</span>' +
                                '<span class="flex-1 min-w-0 text-xs font-medium text-slate-700 dark:text-slate-200 truncate">' +
                                    escHtml(nameA) + ' <span class="text-slate-400 font-normal mx-0.5">↔</span> ' + escHtml(nameB) +
                                '</span>' +
                                '<span class="flex-shrink-0 px-1.5 py-0.5 rounded-full text-xs font-semibold ' + color + ' ' + colorBg + ' border">' + strength + '</span>' +
                            '</summary>' +
                            (hasReason
                                ? '<div class="px-4 pb-3 pt-1 text-xs text-slate-600 dark:text-slate-300 leading-relaxed border-t border-violet-100 dark:border-violet-900/40 bg-white dark:bg-slate-900/40">' +
                                      '<span class="font-semibold text-violet-600 dark:text-violet-400 mr-1">Justificación:</span>' +
                                      escHtml(p.reason) +
                                  '</div>'
                                : '') +
                        '</details>';
                    }).join('');
                }
            }

            // Nota de fallback si la IA no generó JSON
            var footerNote = document.querySelector('#corr-ai-panel p:last-child');
            if (footerNote) {
                if (d.domainRulesOnly) {
                    footerNote.innerHTML = '<span class="text-sky-600 dark:text-sky-400 font-medium">ℹ Reglas de dominio CODELCO (' + (d.nVars||'') + ' variables, ' + (d.nPairs||'') + ' pares)</span> — con más de 10 variables se omite el modelo IA para no saturar Ollama. Los valores son aplicables; ajuste manualmente los pares clave.';
                } else if (d.aiRaw) {
                    footerNote.innerHTML = '<span class="text-amber-600 dark:text-amber-400 font-medium">ℹ Valores calculados por reglas de dominio CODELCO</span> — la IA no generó JSON estructurado en esta consulta. Los valores son aplicables; puede ajustarlos manualmente.';
                }
            }

            // Mostrar botón de descarga evidencia
            if (dlBtn) dlBtn.classList.remove('hidden');
        })
        .catch(function(e) {
            stopSpinner();
            if (errEl) { errEl.textContent = 'Error: ' + e.message; errEl.classList.remove('hidden'); }
        });
    };

    // ── Expandir / colapsar todos los acordeones de pares ─────────────────────
    window.corrPairsExpandAll = function(expand) {
        document.querySelectorAll('#corr-ai-pairs-body details').forEach(function(d) {
            if (expand) d.setAttribute('open', '');
            else        d.removeAttribute('open');
        });
    };

    // ── Descarga de evidencia en formato Markdown ──────────────────────────────
    window.downloadCorrEvidence = function() {
        if (!_corrLastAiResult || !_corrLastVars) return;
        var d    = _corrLastAiResult;
        var vars = _corrLastVars;
        var pairs = d.pairs || [];

        function strengthLabel(v) {
            var abs = Math.abs(v);
            if (v < -0.1) return 'Inversa';
            if (abs > 0.7) return 'Fuerte';
            if (abs > 0.4) return 'Moderada';
            if (abs > 0.15) return 'Débil';
            return 'Mínima';
        }

        var lines = [];
        lines.push('# Evidencia — Sugerencia IA de Correlación');
        lines.push('');
        lines.push('| Campo | Valor |');
        lines.push('|---|---|');
        lines.push('| **Proyecto** | ' + (d.projectName || '—') + ' |');
        lines.push('| **Fuente** | ' + (d.source || '—') + ' |');
        lines.push('| **Escenario IA** | ' + (d.scenario || '—') + ' |');
        lines.push('| **Con justificación** | ' + (d.withJustification ? 'Sí' : 'No') + ' |');
        lines.push('| **Generado** | ' + (d.generatedAt || new Date().toLocaleString()) + ' |');
        lines.push('');
        lines.push('## Variables analizadas');
        lines.push('');
        lines.push('| # | Nombre | Unidad | Distribución | Mín/Media | Prob/Desv | Máx |');
        lines.push('|---|--------|--------|-------------|-----------|-----------|-----|');
        vars.forEach(function(v, i) {
            lines.push('| ' + i + ' | ' + (v.name||'') + ' | ' + (v.unit||'') + ' | ' + (v.distribution||'') +
                       ' | ' + (v.p1||0) + ' | ' + (v.p2||0) + ' | ' + (v.p3||0) + ' |');
        });
        lines.push('');
        lines.push('## Decisiones por par de variables');
        lines.push('');
        if (d.withJustification) {
            lines.push('| Par | Variable A | Variable B | Spearman | Intensidad | Justificación |');
            lines.push('|---|---|---|---|---|---|');
            pairs.forEach(function(p, idx) {
                var nameA = vars[p.i] ? vars[p.i].name : ('Var' + p.i);
                var nameB = vars[p.j] ? vars[p.j].name : ('Var' + p.j);
                var v = parseFloat(p.value) || 0;
                var sign = v > 0 ? '+' : '';
                lines.push('| ' + (idx+1) + ' | ' + nameA + ' | ' + nameB + ' | ' +
                    sign + v.toFixed(2) + ' | ' + strengthLabel(v) + ' | ' + (p.reason || '—') + ' |');
            });
        } else {
            lines.push('| Par | Variable A | Variable B | Spearman | Intensidad |');
            lines.push('|---|---|---|---|---|');
            pairs.forEach(function(p, idx) {
                var nameA = vars[p.i] ? vars[p.i].name : ('Var' + p.i);
                var nameB = vars[p.j] ? vars[p.j].name : ('Var' + p.j);
                var v = parseFloat(p.value) || 0;
                var sign = v > 0 ? '+' : '';
                lines.push('| ' + (idx+1) + ' | ' + nameA + ' | ' + nameB + ' | ' +
                    sign + v.toFixed(2) + ' | ' + strengthLabel(v) + ' |');
            });
        }
        if (d.analysis) {
            lines.push('');
            lines.push('## Análisis narrativo (IA MC)');
            lines.push('');
            lines.push(d.analysis);
        }
        if (d.aiRaw) {
            lines.push('');
            lines.push('## Nota');
            lines.push('');
            lines.push('> Valores calculados por reglas de dominio CODELCO — el modelo IA no generó JSON estructurado.');
        }
        lines.push('');
        lines.push('---');
        lines.push('*Generado por IA MC — Monte Carlo / Taller de Costos CODELCO*');

        var md   = lines.join('\n');
        var blob = new Blob([md], { type: 'text/markdown;charset=utf-8' });
        var url  = URL.createObjectURL(blob);
        var a    = document.createElement('a');
        var proj = (d.projectName || 'proyecto').replace(/[^a-zA-Z0-9_\-]/g, '_').toLowerCase();
        a.href     = url;
        a.download = 'evidencia_correlacion_' + proj + '.md';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    };

    var _corrCharts = {};
    var _mcLastCorrVizData = null;

    function resetCorrPairsPanel() {
        var grid = document.getElementById('corr-pairs-grid');
        var content = document.getElementById('corr-pairs-content');
        var loading = document.getElementById('corr-pairs-loading');
        if (grid) grid.innerHTML = '';
        Object.keys(_corrCharts).forEach(function(k) {
            try { _corrCharts[k].destroy(); } catch (e) {}
            delete _corrCharts[k];
        });
        if (content) content.classList.add('hidden');
        if (loading) loading.classList.add('hidden');
    }

    /** Dispersión por pares (opcional). Usa requestAnimationFrame en lotes para no congelar la UI. */
    function renderCorrResultPairsOnly(d, onDone) {
        var grid = document.getElementById('corr-pairs-grid');
        if (!grid) {
            if (onDone) onDone();
            return;
        }
        grid.innerHTML = '';
        Object.keys(_corrCharts).forEach(function(k) {
            try { _corrCharts[k].destroy(); } catch (e) {}
            delete _corrCharts[k];
        });
        var pairs = d.pairs || [];
        if (pairs.length === 0) {
            if (onDone) onDone();
            return;
        }
        var idx = 0;
        var chunk = 5;
        function step() {
            var end = Math.min(idx + chunk, pairs.length);
            for (; idx < end; idx++) {
                var p = pairs[idx];
                var strengthColor = { 'Muy fuerte':'text-red-600 dark:text-red-400', 'Fuerte':'text-orange-600 dark:text-orange-400',
                    'Moderada':'text-amber-600 dark:text-amber-400', 'Débil':'text-blue-500 dark:text-blue-400', 'Muy débil':'text-slate-400' };
                var sc = strengthColor[p.strength] || 'text-slate-500';
                var dirIcon = p.direction === 'Positiva' ? 'trending_up' : 'trending_down';
                var dirColor = p.direction === 'Positiva' ? 'text-emerald-500' : 'text-red-500';
                var canvasId = 'corr-scatter-' + idx;

                var targetHtml = (p.spearmanTarget !== 0)
                    ? '<span class="text-xs text-slate-400 dark:text-slate-500">Target: ' + p.spearmanTarget.toFixed(2) + '</span>'
                    : '';

                var jtPct = p.jointExceedanceP50 != null ? (p.jointExceedanceP50 * 100).toFixed(1) + '%' : '—';

                grid.insertAdjacentHTML('beforeend',
                    '<div class="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 p-3 shadow-sm">' +
                        '<div class="flex items-start justify-between mb-2">' +
                            '<div class="min-w-0">' +
                                '<p class="font-semibold text-sm text-slate-800 dark:text-slate-100 truncate">' + escHtml(p.nameA) + '</p>' +
                                '<p class="text-xs text-slate-400 dark:text-slate-500 flex items-center gap-1"><span class="material-icons text-pink-400" style="font-size:12px">swap_vert</span>' + escHtml(p.nameB) + '</p>' +
                            '</div>' +
                            '<div class="flex-shrink-0 text-right">' +
                                '<p class="font-bold text-base ' + sc + '">' + p.spearmanAchieved.toFixed(3) + '</p>' +
                                targetHtml +
                            '</div>' +
                        '</div>' +
                        '<div class="flex flex-wrap gap-x-3 gap-y-0.5 mb-2 text-xs">' +
                            '<span class="flex items-center gap-0.5 ' + sc + ' font-medium"><span class="material-icons ' + dirColor + '" style="font-size:13px">' + dirIcon + '</span>' + escHtml(p.strength) + ' ' + escHtml(p.direction) + '</span>' +
                            '<span class="text-slate-400 dark:text-slate-500">Pearson: ' + p.pearsonAchieved.toFixed(3) + '</span>' +
                            '<span class="text-slate-400 dark:text-slate-500">P(A>P50 ∧ B>P50): ' + jtPct + '</span>' +
                        '</div>' +
                        '<p class="text-xs text-slate-500 dark:text-slate-400 mb-2 italic">' + escHtml(p.interpretation) + '</p>' +
                        '<div style="position:relative;height:160px"><canvas id="' + canvasId + '"></canvas></div>' +
                    '</div>');

                if (p.scatterSamples && p.scatterSamples.length > 0) {
                    var pts = p.scatterSamples.map(function(s) { return { x: s[0], y: s[1] }; });
                    var ctx = document.getElementById(canvasId);
                    if (ctx) {
                        _corrCharts[canvasId] = new Chart(ctx, {
                            type: 'scatter',
                            data: { datasets: [{ label: p.nameA + ' vs ' + p.nameB, data: pts,
                                backgroundColor: 'rgba(236,72,153,0.35)', pointRadius: 2, pointHoverRadius: 3 }] },
                            options: {
                                responsive: true, maintainAspectRatio: false,
                                plugins: { legend: { display: false }, tooltip: { callbacks: {
                                    label: function(ctx) { return '(' + ctx.parsed.x.toFixed(2) + ', ' + ctx.parsed.y.toFixed(2) + ')'; }
                                }}},
                                scales: {
                                    x: { ticks: { font: { size: 9 } }, title: { display: true, text: p.nameA, font: { size: 9 } } },
                                    y: { ticks: { font: { size: 9 } }, title: { display: true, text: p.nameB, font: { size: 9 } } }
                                }
                            }
                        });
                    }
                }
            }
            if (idx < pairs.length) {
                requestAnimationFrame(step);
            } else if (onDone) {
                onDone();
            }
        }
        step();
    }

    window.mcGenerateCorrPairCharts = function() {
        var d = _mcLastCorrVizData;
        if (!d || !d.achievedMatrix) {
            mcDialogAlert('Ejecute primero la correlación para generar los gráficos por pares.', { title: 'Correlación pendiente', variant: 'info' });
            return;
        }
        var pairs = d.pairs || [];
        if (pairs.length === 0) {
            mcDialogAlert('No hay pares con correlación distinta de cero en la última simulación; no se generan gráficos de dispersión.', { title: 'Sin pares para graficar', variant: 'info' });
            return;
        }
        var n = pairs.length;
        var msg = 'Vas a generar ' + n + ' gráfico(s) de dispersión por pares.\n\n' +
            'Esta opción puede demorar entre 1 y 5 minutos según la cantidad de variables y pares, y la pestaña puede responder con lentitud mientras se dibujan.\n\n' +
            '¿Deseas continuar?';
        mcDialogConfirm(msg, {
            title: 'Generar gráficos por pares',
            variant: 'warning',
            confirmLabel: 'Sí, generar',
            cancelLabel: 'Cancelar'
        }).then(function(ok) {
            if (!ok) return;
            var loading = document.getElementById('corr-pairs-loading');
            var content = document.getElementById('corr-pairs-content');
            var btn = document.getElementById('corr-pairs-generate-btn');
            if (content) content.classList.remove('hidden');
            if (loading) loading.classList.remove('hidden');
            if (btn) {
                btn.disabled = true;
                btn.classList.add('opacity-50', 'cursor-not-allowed');
            }
            renderCorrResultPairsOnly(d, function() {
                if (loading) loading.classList.add('hidden');
                if (btn) {
                    btn.disabled = false;
                    btn.classList.remove('opacity-50', 'cursor-not-allowed');
                }
            });
        });
    };

    function corrColor(r) {
        // r in [-1, 1] → color: negative=red, zero=white, positive=blue
        if (!isFinite(r)) return '#f1f5f9';
        var abs = Math.min(1, Math.abs(r));
        var alpha = Math.round(abs * 180);
        if (r >= 0) return 'rgba(99,102,241,' + (abs * 0.7 + 0.05) + ')';  // indigo
        return 'rgba(239,68,68,' + (abs * 0.7 + 0.05) + ')'; // red
    }

    function corrTextColor(r) {
        return Math.abs(r) > 0.55 ? '#fff' : '';
    }

    function renderCorrResult(d) {
        // ── Heatmap ──────────────────────────────────────────────────────────
        var matrix = d.achievedMatrix;
        var varNames = (d.variables || []).map(function(v) { return v.name; });
        var n = varNames.length;
        var heatHtml = '<div class="overflow-x-auto"><table class="text-xs border-collapse">';
        heatHtml += '<tr><th class="border border-slate-200 dark:border-slate-700 p-1 bg-slate-50 dark:bg-slate-800"></th>';
        for (var j = 0; j < n; j++)
            heatHtml += '<th class="border border-slate-200 dark:border-slate-700 p-1 bg-slate-50 dark:bg-slate-800 text-center max-w-20"><span class="block truncate max-w-20 text-slate-600 dark:text-slate-300" title="' + escHtml(varNames[j]) + '">' +
                escHtml(varNames[j].length > 8 ? varNames[j].substring(0,7)+'…' : varNames[j]) + '</span></th>';
        heatHtml += '</tr>';
        for (var i = 0; i < n; i++) {
            heatHtml += '<tr><td class="border border-slate-200 dark:border-slate-700 p-1 bg-slate-50 dark:bg-slate-800 font-medium text-right text-slate-600 dark:text-slate-300 max-w-20"><span class="block truncate max-w-20" title="' + escHtml(varNames[i]) + '">' +
                escHtml(varNames[i].length > 8 ? varNames[i].substring(0,7)+'…' : varNames[i]) + '</span></td>';
            for (var jj = 0; jj < n; jj++) {
                if (jj > i) {
                    heatHtml += '<td class="border border-slate-200 dark:border-slate-700 p-1.5 w-16 bg-slate-50 dark:bg-slate-900"></td>';
                } else {
                    var r = matrix[i][jj];
                    var bg = corrColor(r);
                    var tc = corrTextColor(r);
                    heatHtml += '<td class="border border-slate-200 dark:border-slate-700 p-1.5 text-center font-semibold w-16" style="background:' + bg + ';color:' + (tc || 'inherit') + '">' + r.toFixed(2) + '</td>';
                }
            }
            heatHtml += '</tr>';
        }
        heatHtml += '</table></div>';
        document.getElementById('corr-heatmap').innerHTML = heatHtml;

        // ── Stats table ───────────────────────────────────────────────────────
        var statsBody = document.getElementById('corr-stats-body');
        var statsFoot = document.getElementById('corr-stats-foot');
        statsBody.innerHTML = '';
        if (statsFoot) statsFoot.innerHTML = '';

        var totMean = 0, totStd = 0, totP10 = 0, totP50 = 0, totP80 = 0, totP90 = 0;
        var vars = d.variables || [];
        vars.forEach(function(v) {
            var p80v = v.p80 != null ? v.p80 : (v.p50 != null && v.p90 != null ? v.p50 + (v.p90 - v.p50) * 0.5 : null);
            totMean += v.mean  || 0;
            totStd  += v.stdDev|| 0;
            totP10  += v.p10   || 0;
            totP50  += v.p50   || 0;
            totP80  += p80v    || 0;
            totP90  += v.p90   || 0;
            statsBody.insertAdjacentHTML('beforeend',
                '<tr class="border-b border-slate-50 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800/40">' +
                '<td class="py-1.5 pr-3 font-semibold text-slate-700 dark:text-slate-300">' + escHtml(v.name) + '</td>' +
                '<td class="py-1.5 pr-3 text-slate-500 dark:text-slate-400">' + escHtml(v.unit || '—') + '</td>' +
                '<td class="py-1.5 pr-3 text-right">' + fmt(v.mean) + '</td>' +
                '<td class="py-1.5 pr-3 text-right">' + fmt(v.stdDev) + '</td>' +
                '<td class="py-1.5 pr-3 text-right text-blue-600 dark:text-blue-400">' + fmt(v.p10) + '</td>' +
                '<td class="py-1.5 pr-3 text-right text-indigo-600 dark:text-indigo-400 font-semibold">' + fmt(v.p50) + '</td>' +
                '<td class="py-1.5 pr-3 text-right text-amber-600 dark:text-amber-400 font-semibold">' + (p80v != null ? fmt(p80v) : '—') + '</td>' +
                '<td class="py-1.5 pr-3 text-right text-violet-600 dark:text-violet-400">' + fmt(v.p90) + '</td>' +
                '<td class="py-1.5 pr-3 text-right text-slate-500">' + (v.cv*100).toFixed(1) + '%</td>' +
            '</tr>');
        });

        // Totals footer (only render when ≥2 variables)
        if (statsFoot && vars.length >= 2) {
            statsFoot.innerHTML =
                '<tr class="border-t-2 border-slate-200 dark:border-slate-600 bg-slate-50 dark:bg-slate-800/60 font-bold">' +
                '<td class="py-1.5 pr-3 text-slate-700 dark:text-slate-200 text-xs uppercase tracking-wide" colspan="2">Total</td>' +
                '<td class="py-1.5 pr-3 text-right text-slate-700 dark:text-slate-200">' + fmt(totMean) + '</td>' +
                '<td class="py-1.5 pr-3 text-right text-slate-700 dark:text-slate-200">' + fmt(totStd) + '</td>' +
                '<td class="py-1.5 pr-3 text-right text-blue-700 dark:text-blue-300">' + fmt(totP10) + '</td>' +
                '<td class="py-1.5 pr-3 text-right text-indigo-700 dark:text-indigo-300">' + fmt(totP50) + '</td>' +
                '<td class="py-1.5 pr-3 text-right text-amber-700 dark:text-amber-300">' + fmt(totP80) + '</td>' +
                '<td class="py-1.5 pr-3 text-right text-violet-700 dark:text-violet-300">' + fmt(totP90) + '</td>' +
                '<td class="py-1.5 pr-3 text-right text-slate-400">—</td>' +
                '</tr>';
        }

        resetCorrPairsPanel();
        _mcLastCorrVizData = d;
        var btnP = document.getElementById('corr-pairs-generate-btn');
        if (btnP) {
            var np = (d.pairs || []).length;
            btnP.disabled = np === 0;
            btnP.classList.toggle('opacity-50', np === 0);
            btnP.classList.toggle('cursor-not-allowed', np === 0);
            btnP.title = np === 0 ? 'No hay pares con ρ ≠ 0 en la última simulación.' : '';
        }
    }

    function fmt(v) {
        if (!isFinite(v)) return '—';
        if (Math.abs(v) >= 1e6) return (v/1e6).toFixed(2) + 'M';
        if (Math.abs(v) >= 1e3) return (v/1e3).toFixed(1) + 'K';
        return v.toFixed(2);
    }

    // Initialize corr tab
    (function() {
        if (_corrSource === 'manual') { addCorrVar(); addCorrVar(); }
        updateCorrSourceUI();
        setCorrMatrixMode('manual');
    })();

    // ── Panel PDF ─────────────────────────────────────────────────────────────
    var TAB_LABELS = { risks:'Riesgos', costs:'Costos', schedule:'Cronograma', 'sched-risks':'Riesgos Prog.', sra:'SRA', cra:'CRA', 'var':'VaR', corr:'Correlaciones' };
    var TAB_COLORS_HEX = { risks:'#dc2626', costs:'#059669', schedule:'#2563eb', 'sched-risks':'#d97706', sra:'#0891b2', cra:'#7c3aed', 'var':'#7c3aed' };

    function updatePdfPanel() {
        var completedTabs = ['risks','costs','schedule','sched-risks','sra','cra','var'].filter(function(t) { return !!_mcAllData[t]; });
        if (completedTabs.length === 0) return;
        var panel = document.getElementById('mc-pdf-panel');
        if (panel) panel.classList.remove('hidden');
        var summary = document.getElementById('mc-pdf-summary');
        if (summary) summary.textContent = 'Análisis completados: ' + completedTabs.map(function(t) { return TAB_LABELS[t]; }).join(', ');
        var badges = document.getElementById('mc-pdf-badges');
        if (badges) {
            badges.innerHTML = completedTabs.map(function(t) {
                return '<span class="px-2 py-0.5 rounded-full text-white font-medium" style="background:' + TAB_COLORS_HEX[t] + ';font-size:11px">' + TAB_LABELS[t] + '</span>';
            }).join('');
        }
        // Persist results + update sidebar badges + enable report button
        completedTabs.forEach(function(t) { saveResultToStorage(t, _mcAllData[t]); });
        updateTabBadges();
    }

    window.generatePDF = function() {
        var jsPDFLib = window.jspdf && window.jspdf.jsPDF;
        if (!jsPDFLib) {
            mcDialogAlert('La librería PDF aún no ha cargado. Intente en unos segundos.', { title: 'PDF no disponible', variant: 'warning' });
            return;
        }
        var tabs = ['risks','costs','schedule','var'];
        var hasSome = tabs.some(function(t) { return !!_mcAllData[t]; });
        if (!hasSome) {
            mcDialogAlert('Ejecute al menos un análisis antes de generar el PDF.', { title: 'Sin análisis', variant: 'info' });
            return;
        }
        var loadingEl = document.getElementById('mc-pdf-loading');
        if (loadingEl) loadingEl.classList.remove('hidden');
        setTimeout(function() {
            try {
                var doc = new jsPDFLib({ orientation: 'portrait', unit: 'mm', format: 'a4' });
                var pw = doc.internal.pageSize.getWidth();
                var ph = doc.internal.pageSize.getHeight();
                var ml = 14, mr = 14;
                var cw = pw - ml - mr;
                var nowStr = new Date().toLocaleString('es-CL');

                // ── Portada ──────────────────────────────────────────────────
                doc.setFillColor(67, 56, 202);
                doc.rect(0, 0, pw, 50, 'F');
                doc.setFillColor(99, 102, 241);
                doc.rect(0, 40, pw, 10, 'F');
                doc.setTextColor(255, 255, 255);
                doc.setFont('helvetica', 'bold');
                doc.setFontSize(22);
                doc.text('Simulación Monte Carlo', ml, 20);
                doc.setFontSize(13);
                doc.setFont('helvetica', 'normal');
                doc.text('Informe de Análisis Probabilístico', ml, 30);
                doc.setFontSize(9);
                doc.text('Generado: ' + nowStr, ml, 42);

                // Subtítulo portada
                doc.setTextColor(30, 41, 59);
                doc.setFont('helvetica', 'bold');
                doc.setFontSize(13);
                doc.text('Resumen ejecutivo', ml, 65);

                var y = 73;
                var completedTabs = tabs.filter(function(t) { return !!_mcAllData[t]; });
                var tabRgb = {
                    risks:[220,38,38], costs:[5,150,105], schedule:[37,99,235], 'var':[124,58,237]
                };
                var tabLabelsFull = {
                    risks:'Análisis de Riesgos', costs:'Estimación de Costos',
                    schedule:'Estimación de Cronograma', 'var':'Value at Risk (VaR)'
                };
                var unitLabel = {
                    risks:'USD', costs:'USD', schedule:'días', 'var':'USD'
                };

                completedTabs.forEach(function(t) {
                    var d = _mcAllData[t];
                    var s = d.statistics;
                    var rgb = tabRgb[t];
                    var cv = (s.mean && s.mean !== 0) ? Math.abs(s.stdDev / s.mean) * 100 : 0;
                    var lvl = cv < 20 ? 'BAJO' : cv < 50 ? 'MEDIO' : 'ALTO';
                    var lvlRgb = cv < 20 ? [16,185,129] : cv < 50 ? [245,158,11] : [239,68,68];

                    // Barra lateral coloreada
                    doc.setFillColor(rgb[0], rgb[1], rgb[2]);
                    doc.rect(ml, y, 3, 22, 'F');

                    // Título análisis
                    doc.setFont('helvetica', 'bold');
                    doc.setFontSize(10);
                    doc.setTextColor(30, 41, 59);
                    doc.text(tabLabelsFull[t], ml + 6, y + 5);

                    // Badge nivel
                    doc.setFillColor(lvlRgb[0], lvlRgb[1], lvlRgb[2]);
                    doc.roundedRect(pw - mr - 22, y + 1, 22, 7, 2, 2, 'F');
                    doc.setTextColor(255,255,255);
                    doc.setFont('helvetica','bold');
                    doc.setFontSize(8);
                    doc.text(lvl, pw - mr - 11, y + 6, {align:'center'});

                    // Stats
                    doc.setTextColor(71, 85, 105);
                    doc.setFont('helvetica', 'normal');
                    doc.setFontSize(8);
                    doc.text('Media: ' + fmtN(s.mean) + ' ' + unitLabel[t], ml + 6, y + 11);
                    doc.text('P50: ' + fmtN(s.p50) + '   P80: ' + fmtN(s.p80) + '   P90: ' + fmtN(s.p90) + '   Desv.: ' + fmtN(s.stdDev), ml + 6, y + 17);

                    // Línea separadora
                    doc.setDrawColor(226, 232, 240);
                    doc.line(ml, y + 23, pw - mr, y + 23);
                    y += 27;
                });

                // Nota portada
                doc.setFont('helvetica','italic');
                doc.setFontSize(8);
                doc.setTextColor(148, 163, 184);
                doc.text('Este informe fue generado automáticamente por la herramienta de Simulación Monte Carlo.', ml, ph - 15);
                doc.text('Los resultados son probabilísticos y no garantizan un resultado específico.', ml, ph - 10);

                // ── Una página por análisis ──────────────────────────────────
                completedTabs.forEach(function(t) {
                    var d = _mcAllData[t];
                    var s = d.statistics;
                    var m = d.additionalMetrics || {};
                    var typ = d.simulationType || '';
                    var rgb = tabRgb[t];
                    var cv = (s.mean && s.mean !== 0) ? Math.abs(s.stdDev / s.mean) * 100 : 0;
                    var lvl = cv < 20 ? 'BAJO' : cv < 50 ? 'MEDIO' : 'ALTO';
                    var lvlRgb = cv < 20 ? [16,185,129] : cv < 50 ? [245,158,11] : [239,68,68];

                    doc.addPage();
                    var y = 0;

                    // Header
                    doc.setFillColor(rgb[0], rgb[1], rgb[2]);
                    doc.rect(0, 0, pw, 18, 'F');
                    doc.setFillColor(Math.min(rgb[0]+30,255), Math.min(rgb[1]+30,255), Math.min(rgb[2]+30,255));
                    doc.rect(0, 14, pw, 4, 'F');
                    doc.setTextColor(255,255,255);
                    doc.setFont('helvetica','bold');
                    doc.setFontSize(14);
                    doc.text(tabLabelsFull[t], ml, 12);
                    doc.setFont('helvetica','normal');
                    doc.setFontSize(8);
                    doc.text('Sims.: ' + (d.totalSimulations||10000).toLocaleString() + '   Generado: ' + nowStr, pw - mr, 12, {align:'right'});
                    y = 25;

                    // ── Sección KPIs ──
                    doc.setFont('helvetica','bold'); doc.setFontSize(8); doc.setTextColor(71,85,105);
                    doc.text('ESTADÍSTICAS CLAVE', ml, y); y += 4;
                    var kpiData = [
                        ['Mínimo', fmtN(s.min)], ['Máximo', fmtN(s.max)], ['Media', fmtN(s.mean)],
                        ['Moda', fmtN(s.mode)], ['Desv. Estándar', fmtN(s.stdDev)], ['Varianza', fmtN(s.variance)],
                        ['Asimetría', typeof s.skewness==='number' ? s.skewness.toFixed(3) : '—'],
                        ['Curtosis',  typeof s.kurtosis==='number'  ? s.kurtosis.toFixed(3)  : '—'],
                        ['Errores', String(s.errors||0)]
                    ];
                    var colW3 = cw / 3;
                    kpiData.forEach(function(kv, idx) {
                        var col = idx % 3, row = Math.floor(idx / 3);
                        var kx = ml + col * colW3;
                        var ky = y + row * 11;
                        doc.setFillColor(248, 250, 252);
                        doc.setDrawColor(226, 232, 240);
                        doc.roundedRect(kx, ky, colW3 - 2, 10, 1.5, 1.5, 'FD');
                        doc.setFont('helvetica','normal'); doc.setFontSize(7); doc.setTextColor(100,116,139);
                        doc.text(kv[0], kx + 2.5, ky + 4);
                        doc.setFont('helvetica','bold'); doc.setFontSize(9); doc.setTextColor(30,41,59);
                        var vStr = String(kv[1]); if (vStr.length > 11) vStr = vStr.substring(0,11);
                        doc.text(vStr, kx + 2.5, ky + 8.5);
                    });
                    y += 36;

                    // ── Gráficos ──
                    doc.setFont('helvetica','bold'); doc.setFontSize(8); doc.setTextColor(71,85,105);
                    var chartH = 52, halfW = cw / 2 - 2;
                    doc.text('DISTRIBUCIÓN DE FRECUENCIAS', ml, y);
                    doc.text('DISTRIBUCIÓN ACUMULADA (CDF)', ml + cw/2 + 2, y);
                    y += 3;
                    var chartCanvas = document.getElementById('mc-chart-' + t);
                    var cdfCanvas   = document.getElementById('mc-cdf-' + t);
                    if (chartCanvas && chartCanvas.width > 0) {
                        try { doc.addImage(chartCanvas.toDataURL('image/png',1), 'PNG', ml, y, halfW, chartH); } catch(e){}
                    } else {
                        doc.setFillColor(248,250,252); doc.rect(ml, y, halfW, chartH, 'F');
                        doc.setFont('helvetica','italic'); doc.setFontSize(8); doc.setTextColor(148,163,184);
                        doc.text('Gráfico no disponible', ml + halfW/2, y + chartH/2, {align:'center'});
                    }
                    if (cdfCanvas && cdfCanvas.width > 0) {
                        try { doc.addImage(cdfCanvas.toDataURL('image/png',1), 'PNG', ml + cw/2 + 2, y, halfW, chartH); } catch(e){}
                    } else {
                        doc.setFillColor(248,250,252); doc.rect(ml + cw/2 + 2, y, halfW, chartH, 'F');
                    }
                    y += chartH + 7;

                    // ── Percentiles ──
                    doc.setFont('helvetica','bold'); doc.setFontSize(8); doc.setTextColor(71,85,105);
                    doc.text('TABLA DE PERCENTILES', ml, y); y += 4;
                    var pcts = [['P5',s.p5],['P10',s.p10],['P15',s.p15],['P20',s.p20],['P25',s.p25],
                                ['P30',s.p30],['P35',s.p35],['P40',s.p40],['P45',s.p45],['P50',s.p50],
                                ['P55',s.p55],['P60',s.p60],['P65',s.p65],['P70',s.p70],['P75',s.p75],
                                ['P80',s.p80],['P85',s.p85],['P90',s.p90],['P95',s.p95]];
                    var pColW = cw / 10;
                    pcts.forEach(function(p, idx) {
                        var col = idx % 10, row = Math.floor(idx / 10);
                        var px = ml + col * pColW;
                        var py = y + row * 11;
                        var isP50 = p[0]==='P50', isP80 = p[0]==='P80', isP90 = p[0]==='P90';
                        var bgR = isP50?238:isP80||isP90?255:248, bgG = isP50?242:isP80?247:isP90?240:250, bgB = isP50?255:isP80?231:isP90?230:252;
                        doc.setFillColor(bgR,bgG,bgB);
                        doc.setDrawColor(isP50?199:isP80?251:isP90?254:226, isP50?210:isP80?191:isP90?202:232, isP50?254:isP80?36:isP90?68:240);
                        doc.roundedRect(px, py, pColW - 1, 10, 1, 1, 'FD');
                        doc.setFont('helvetica','normal'); doc.setFontSize(6.5);
                        doc.setTextColor(isP50?79:isP80?146:isP90?185:100, isP50?70:isP80?64:isP90?28:116, isP50?229:isP80?51:isP90?26:139);
                        doc.text(p[0], px + 1.5, py + 4);
                        doc.setFont('helvetica','bold'); doc.setFontSize(7); doc.setTextColor(isP50?67:30, isP50?56:41, isP50?202:59);
                        var vs = fmtN(p[1]); if (vs.length > 7) vs = vs.substring(0,7);
                        doc.text(vs, px + 1.5, py + 8.5);
                    });
                    y += 24;

                    // ── Nivel de riesgo y recomendaciones ──
                    doc.setFillColor(lvlRgb[0], lvlRgb[1], lvlRgb[2]);
                    doc.roundedRect(ml, y, cw, 9, 2, 2, 'F');
                    doc.setTextColor(255,255,255); doc.setFont('helvetica','bold'); doc.setFontSize(9);
                    doc.text('Nivel de incertidumbre: ' + lvl + '   |   CV = ' + cv.toFixed(1) + '%   |   Skewness = ' + (typeof s.skewness==='number'?s.skewness.toFixed(2):'—') + '   |   Kurtosis = ' + (typeof s.kurtosis==='number'?s.kurtosis.toFixed(2):'—'), ml + 3, y + 6);
                    y += 13;

                    doc.setFont('helvetica','bold'); doc.setFontSize(8); doc.setTextColor(71,85,105);
                    doc.text('RECOMENDACIONES', ml, y); y += 4;
                    var recs2 = [];
                    var c80 = s.p80 - s.mean, c90 = s.p90 - s.mean;
                    if (typ==='CostEstimation') {
                        recs2 = [
                            'Presupuesto base (P50): ' + fmtN(s.p50),
                            'Contingencia P80: +' + fmtN(c80) + ' (' + (s.mean?(c80/s.mean*100).toFixed(1):'?') + '% sobre la media)',
                            'Presupuesto conservador (P90): ' + fmtN(s.p90),
                            'Rango intercuartil P25–P75: ' + fmtN(s.p25) + ' — ' + fmtN(s.p75)
                        ];
                    } else if (typ==='ScheduleEstimation') {
                        recs2 = [
                            'Plazo base (P50): ' + fmtN(s.p50) + ' días — compromiso contractual inicial',
                            'Plazo recomendado (P80): ' + fmtN(s.p80) + ' días — buffer: +' + fmtN(s.p80-s.p50) + ' días vs P50',
                            'Plazo pesimista (P90): ' + fmtN(s.p90) + ' días — reserva de gestión',
                            'Duración mínima (P5): ' + fmtN(s.p5) + ' días — escenario optimista'
                        ];
                    } else if (typ==='RiskAnalysis') {
                        recs2 = [
                            'Pérdida esperada (media): ' + fmtN(s.mean),
                            'Provisión estándar (P80): ' + fmtN(s.p80),
                            'Reserva de contingencia (P90): ' + fmtN(s.p90),
                            'Prob. al menos un riesgo: ' + (m.probabilityOfAnyRisk?(m.probabilityOfAnyRisk*100).toFixed(1)+'%':'—')
                        ];
                    } else if (typ==='ValueAtRisk') {
                        recs2 = [
                            'Valor esperado portafolio: ' + fmtN(s.mean),
                            'VaR al ' + ((m.confidenceLevel||0.95)*100).toFixed(0) + '%: ' + fmtN(m.valueAtRisk||s.p5),
                            'CVaR (pérdida promedio peor 5%): ' + fmtN(m.conditionalVaR||s.p5),
                            'Pérdida potencial máxima: ' + fmtN(m.potentialLoss||(m.initialPortfolioValue-s.p5))
                        ];
                    }
                    recs2.forEach(function(r) {
                        doc.setFillColor(rgb[0], rgb[1], rgb[2], 0.1);
                        doc.setDrawColor(rgb[0], rgb[1], rgb[2]);
                        doc.roundedRect(ml, y - 0.5, 2.5, 5, 0.5, 0.5, 'F');
                        doc.setFont('helvetica','normal'); doc.setFontSize(8.5); doc.setTextColor(30,41,59);
                        doc.text(r, ml + 5, y + 3.5);
                        y += 7;
                    });

                    // Footer
                    doc.setFont('helvetica','normal'); doc.setFontSize(7); doc.setTextColor(148,163,184);
                    doc.line(ml, ph-12, pw-mr, ph-12);
                    doc.text('Análisis Monte Carlo — Simulación Probabilística  |  ' + nowStr, ml, ph - 7);
                    doc.text('Pág. ' + doc.internal.getNumberOfPages(), pw - mr, ph - 7, {align:'right'});
                });

                window.open(doc.output('bloburl'), '_blank');
            } catch(ex) {
                console.error('PDF error:', ex);
                mcDialogAlert('Error al generar PDF: ' + ex.message, { title: 'Error al generar PDF', variant: 'danger' });
            } finally {
                if (loadingEl) loadingEl.classList.add('hidden');
            }
        }, 150);
    };

    // ══════════════════════════════════════════════════════════════════════════
    // ── Proyecto: nombre y persistencia ──────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════════

    var _projectName = '';
    var _projectId   = null;

    window.confirmProjectName = function() {
        var inp  = document.getElementById('proj-name-input');
        var name = (inp ? inp.value : '').trim();
        if (!name) { if (inp) inp.focus(); return; }
        _projectName = name;
        try { localStorage.setItem('mc-project-name', name); } catch(e) {}
        var elUnnamed = document.getElementById('proj-unnamed');
        var elNamed   = document.getElementById('proj-named');
        if (elUnnamed) elUnnamed.style.display = 'none';
        if (elNamed)   { elNamed.style.display = 'flex'; elNamed.classList.remove('hidden'); }
        var title = document.getElementById('proj-title');
        if (title) title.textContent = name;
        var btnSave = document.getElementById('btn-save-project');
        if (btnSave) btnSave.disabled = false;
    };

    window.editProjectName = function() {
        var elUnnamed = document.getElementById('proj-unnamed');
        var elNamed   = document.getElementById('proj-named');
        if (elNamed)   { elNamed.style.display = 'none'; elNamed.classList.add('hidden'); }
        if (elUnnamed) elUnnamed.style.display = '';
        var inp = document.getElementById('proj-name-input');
        if (inp) { inp.value = _projectName; inp.focus(); inp.select(); }
    };

    function updateProjectBanner() {
        try {
            var n  = localStorage.getItem('mc-project-name') || '';
            var id = localStorage.getItem('mc-project-id')   || null;
            if (n) {
                _projectName = n;
                _projectId   = id;
                var inp = document.getElementById('proj-name-input');
                if (inp) inp.value = n;
                confirmProjectName();
            }
        } catch(e) {}
    }

    // ── localStorage: tab inputs ──────────────────────────────────────────────

    function saveTabToStorage(tabId) {
        try {
            var content = document.getElementById('tab-' + tabId);
            if (!content || !FIELDS[tabId]) return;
            var data = {};
            content.querySelectorAll('input[name], select[name], textarea[name]').forEach(function(el) {
                data[el.name] = el.value;
            });
            localStorage.setItem('mc-tab-inputs-' + tabId, JSON.stringify(data));
        } catch(e) {}
    }
    window.saveTabToStorage = saveTabToStorage;

    function restoreTabFromStorage(tabId) {
        try {
            var json = localStorage.getItem('mc-tab-inputs-' + tabId);
            if (!json) return false;
            var data = JSON.parse(json);
            var cfg  = FIELDS[tabId];
            if (!cfg) return false;

            var maxIdx = -1;
            Object.keys(data).forEach(function(k) {
                var m = k.match(/\[(\d+)\]/);
                if (m) maxIdx = Math.max(maxIdx, parseInt(m[1]));
            });
            if (maxIdx < 0) return false;

            var body = document.getElementById(cfg.body);
            if (!body) return false;
            var rows = body.querySelectorAll('.data-row');
            for (var r = rows.length - 1; r >= 0; r--) rows[r].remove();
            for (var i = 0; i <= maxIdx; i++) {
                body.insertAdjacentHTML('beforeend', T[cfg.type](i));
            }

            var content = document.getElementById('tab-' + tabId);
            if (content) {
                content.querySelectorAll('input[name], select[name], textarea[name]').forEach(function(el) {
                    if (data[el.name] !== undefined) el.value = data[el.name];
                });
            }
            // Vista lectura por fila (las llamadas internas usan esta función, no window.restoreTabFromStorage)
            if (tabId === 'risks') {
                setTimeout(function() {
                    if (typeof window.formatAllRisksImpacts === 'function') window.formatAllRisksImpacts();
                    if (typeof window.mcRisksInitReadMode === 'function') window.mcRisksInitReadMode();
                    else if (typeof window.recalcAllRisksVe === 'function') window.recalcAllRisksVe();
                }, 0);
            }
            if (tabId === 'schedule') {
                setTimeout(function() {
                    if (typeof window.mcScheduleInitReadMode === 'function') window.mcScheduleInitReadMode();
                }, 0);
            }
            return true;
        } catch(e) { return false; }
    }
    window.restoreTabFromStorage = restoreTabFromStorage;

    function saveResultToStorage(tabId, resultData) {
        try { localStorage.setItem('mc-tab-result-' + tabId, JSON.stringify(resultData)); } catch(e) {}
    }

    function loadResultFromStorage(tabId) {
        try {
            var json = localStorage.getItem('mc-tab-result-' + tabId);
            return json ? JSON.parse(json) : null;
        } catch(e) { return null; }
    }

    function updateTabBadges() {
        ['risks','costs','schedule','sched-risks','sra','cra','var','corr'].forEach(function(t) {
            var badge    = document.getElementById('badge-' + t);
            var hasResult = !!_mcAllData[t] || !!localStorage.getItem('mc-tab-result-' + t);
            if (badge) badge.classList.toggle('hidden', !hasResult);
        });
        var completedCount = ['risks','costs','schedule','sched-risks','sra','cra','var','corr']
            .filter(function(t) { return !!_mcAllData[t] || !!localStorage.getItem('mc-tab-result-' + t); }).length;
        var btnRpt = document.getElementById('btn-generate-report');
        if (btnRpt) btnRpt.disabled = (completedCount === 0);
    }

    // ── Guardar proyecto en API ───────────────────────────────────────────────

    window.saveProject = function() {
        if (!_projectName) {
            mcDialogAlert('Primero confirme el nombre del proyecto.', { title: 'Nombre de proyecto', variant: 'warning' });
            return;
        }
        Object.keys(FIELDS).forEach(function(t) { saveTabToStorage(t); });
        // Flush in-memory results to localStorage so they are included in the payload
        Object.keys(_mcAllData).forEach(function(t) { saveResultToStorage(t, _mcAllData[t]); });

        var tabs = {};
        ['risks','costs','schedule','sched-risks','sra','cra','var','corr'].forEach(function(t) {
            var inputJson  = localStorage.getItem('mc-tab-inputs-' + t)  || '{}';
            var resultJson = localStorage.getItem('mc-tab-result-' + t)  || '';
            tabs[t] = { inputJson: inputJson, resultJson: resultJson,
                        completedAt: resultJson ? new Date().toISOString() : null };
        });

        var body = { id: _projectId || null, projectName: _projectName, status: 'InProgress', tabs: tabs };
        // Strip literal control characters that cause JSON parse errors on the server
        var bodyStr = JSON.stringify(body).replace(/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/g, '');
        var tok  = document.querySelector('input[name="__RequestVerificationToken"]');
        var btnSave = document.getElementById('btn-save-project');
        var lbl = btnSave ? btnSave.querySelector('span:last-child') : null;
        if (btnSave) btnSave.disabled = true;
        if (lbl) lbl.textContent = 'Guardando…';

        fetch('?handler=SaveProjectJson', {
            method:  'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tok ? tok.value : '' },
            body:    bodyStr
        })
        .then(function(r) { return r.json(); })
        .then(function(d) {
            if (d.error) {
                mcDialogAlert('Error al guardar: ' + d.error, { title: 'Error al guardar', variant: 'danger' });
                if (btnSave) { btnSave.disabled = false; if (lbl) lbl.textContent = 'Guardar Proyecto'; }
                return;
            }
            if (d.id) { _projectId = d.id; try { localStorage.setItem('mc-project-id', d.id); } catch(e2) {} }
            if (lbl) lbl.textContent = '✓ Guardado';
            if (_currentTabId === 'projects') loadProjectsTab();
            setTimeout(function() { if (btnSave) btnSave.disabled = false; if (lbl) lbl.textContent = 'Guardar Proyecto'; }, 2000);
        })
        .catch(function(e) {
            mcDialogAlert('Error: ' + e.message, { title: 'Error de red', variant: 'danger' });
            if (btnSave) btnSave.disabled = false;
            if (lbl) lbl.textContent = 'Guardar Proyecto';
        });
    };

    // ── Pestaña Mis Proyectos ─────────────────────────────────────────────────

    window.loadProjectsTab = function loadProjectsTab() {
        var container = document.getElementById('projects-tab-list');
        if (!container) return;
        container.innerHTML = '<div class="text-center py-10 text-slate-400 italic text-sm">Cargando proyectos…</div>';
        fetch('?handler=ProjectsJson&page=1&pageSize=50')
            .then(function(r) { return r.json(); })
            .then(function(d) {
                if (d.error || !d.items) {
                    container.innerHTML = '<p class="text-red-500 text-sm p-4">' + escHtml(d.error || 'Error al cargar.') + '</p>';
                    return;
                }
                if (d.items.length === 0) {
                    container.innerHTML = '<div class="flex flex-col items-center justify-center py-16 text-slate-400">' +
                        '<span class="material-icons text-5xl mb-3 opacity-30">folder_open</span>' +
                        '<p class="text-sm font-medium">No hay proyectos guardados</p>' +
                        '<p class="text-xs mt-1 opacity-70">Guarda un proyecto desde el botón "Guardar Proyecto"</p>' +
                        '</div>';
                    return;
                }
                var TAB_LABELS = { risks:'Riesgos', costs:'Costos', schedule:'Cronograma',
                    'sched-risks':'Rie.Prog.', sra:'SRA', cra:'CRA', 'var':'VaR', corr:'Corr.' };
                container.innerHTML = d.items.map(function(p) {
                    var sBg = p.status === 'Completed'
                        ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300'
                        : 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300';
                    var upd = new Date(p.updatedAt).toLocaleString('es-CL');
                    var tabs = p.completedTabs || [];
                    var badgesHtml = tabs.map(function(t) {
                        return '<span class="inline-block px-1.5 py-0.5 rounded text-xs font-medium bg-indigo-100 text-indigo-700 dark:bg-indigo-900/40 dark:text-indigo-300">' + escHtml(TAB_LABELS[t] || t) + '</span>';
                    }).join('');
                    return '<div class="p-4 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 shadow-sm hover:shadow-md transition-shadow">' +
                        '<div class="flex items-start gap-3">' +
                        '<div class="w-10 h-10 rounded-xl bg-indigo-50 dark:bg-indigo-900/30 flex items-center justify-center flex-shrink-0">' +
                        '<span class="material-icons text-indigo-500" style="font-size:22px">folder_open</span></div>' +
                        '<div class="flex-1 min-w-0">' +
                        '<div class="flex items-center gap-2 flex-wrap">' +
                        '<h3 class="font-semibold text-slate-800 dark:text-slate-100 truncate">' + escHtml(p.projectName) + '</h3>' +
                        '<span class="px-2 py-0.5 rounded-full text-xs font-medium flex-shrink-0 ' + sBg + '">' + escHtml(p.status) + '</span>' +
                        '</div>' +
                        '<p class="text-xs text-slate-400 dark:text-slate-500 mt-0.5">' + upd + ' · ' + tabs.length + '/7 análisis completados</p>' +
                        (badgesHtml ? '<div class="flex flex-wrap gap-1 mt-2">' + badgesHtml + '</div>' : '') +
                        '</div>' +
                        '<div class="flex gap-2 flex-shrink-0">' +
                        '<button type="button" onclick="loadProject(\'' + p.id + '\')" ' +
                        'class="proj-btn bg-indigo-600 hover:bg-indigo-700 text-white">' +
                        '<span class="material-icons" style="font-size:14px">folder_open</span>Cargar</button>' +
                        '<button type="button" onclick="deleteProject(\'' + p.id + '\',this)" ' +
                        'class="proj-btn border border-red-200 dark:border-red-800 text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20">' +
                        '<span class="material-icons" style="font-size:14px">delete</span></button>' +
                        '</div></div></div>';
                }).join('');
            })
            .catch(function(e) {
                container.innerHTML = '<p class="text-red-500 text-sm p-4">Error: ' + escHtml(e.message) + '</p>';
            });
    };

    window.deleteProject = function(id, btn) {
        mcDialogConfirm('¿Eliminar este proyecto? Esta acción no se puede deshacer.', {
            title: 'Eliminar proyecto',
            variant: 'danger',
            confirmLabel: 'Eliminar',
            cancelLabel: 'Cancelar'
        }).then(function(ok) {
            if (!ok) return;
            var tok = document.querySelector('input[name="__RequestVerificationToken"]');
            if (btn) btn.disabled = true;
            fetch('?handler=DeleteProjectJson', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tok ? tok.value : '' },
                body: JSON.stringify({ id: id })
            })
            .then(function(r) { return r.json(); })
            .then(function(d) {
                if (d.error) {
                    mcDialogAlert('Error al eliminar: ' + d.error, { title: 'Error', variant: 'danger' });
                    if (btn) btn.disabled = false;
                    return;
                }
                if (_projectId === id) { _projectId = null; try { localStorage.removeItem('mc-project-id'); } catch(e2) {} }
                loadProjectsTab();
            })
            .catch(function(e) {
                mcDialogAlert('Error: ' + e.message, { title: 'Error de red', variant: 'danger' });
                if (btn) btn.disabled = false;
            });
        });
    };

    // ── Modal Mis Proyectos ───────────────────────────────────────────────────

    window.openProjectsModal = function() {
        var modal = document.getElementById('modal-projects');
        if (modal) { modal.style.display = 'flex'; }
        var mbody = document.getElementById('projects-modal-body');
        if (mbody) mbody.innerHTML = '<div class="text-center py-8 text-slate-400 italic text-sm">Cargando…</div>';

        fetch('?handler=ProjectsJson&page=1&pageSize=30')
            .then(function(r) { return r.json(); })
            .then(function(d) {
                if (!mbody) return;
                if (d.error || !d.items) { mbody.innerHTML = '<p class="text-red-500 text-sm p-4">' + escHtml(d.error || 'Error al cargar.') + '</p>'; return; }
                if (d.items.length === 0) { mbody.innerHTML = '<div class="text-center py-8 text-slate-400 text-sm">No hay proyectos guardados.</div>'; return; }
                mbody.innerHTML = '<div class="space-y-2">' + d.items.map(function(p) {
                    var sBg = p.status === 'Completed'
                        ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300'
                        : 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300';
                    var upd = new Date(p.updatedAt).toLocaleString('es-CL');
                    var cnt = p.completedTabs ? p.completedTabs.length : 0;
                    return '<div class="flex items-center gap-3 p-3 rounded-xl border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors">' +
                        '<div class="flex-1 min-w-0">' +
                        '<p class="font-semibold text-slate-800 dark:text-slate-100 truncate">' + escHtml(p.projectName) + '</p>' +
                        '<p class="text-xs text-slate-400 mt-0.5">' + upd + ' · ' + cnt + '/7 análisis</p>' +
                        '</div>' +
                        '<span class="px-2 py-0.5 rounded-full text-xs font-medium flex-shrink-0 ' + sBg + '">' + escHtml(p.status) + '</span>' +
                        '<button type="button" onclick="loadProject(\'' + p.id + '\')" ' +
                        'class="proj-btn bg-indigo-600 hover:bg-indigo-700 text-white flex-shrink-0">Cargar</button>' +
                        '</div>';
                }).join('') + '</div>';
            })
            .catch(function(e) { if (mbody) mbody.innerHTML = '<p class="text-red-500 text-sm p-4">Error: ' + escHtml(e.message) + '</p>'; });
    };

    window.closeProjectsModal = function() {
        var modal = document.getElementById('modal-projects');
        if (modal) modal.style.display = 'none';
    };

    window.loadProject = function(id) {
        fetch('?handler=ProjectJson&id=' + id)
            .then(function(r) { return r.json(); })
            .then(function(d) {
                if (d.error || !d.id) {
                    mcDialogAlert('Error al cargar el proyecto.', { title: 'Error', variant: 'danger' });
                    return;
                }
                _projectName = d.projectName;
                _projectId   = d.id;
                try { localStorage.setItem('mc-project-name', d.projectName); localStorage.setItem('mc-project-id', d.id); } catch(e2) {}

                if (d.tabs) {
                    Object.keys(d.tabs).forEach(function(t) {
                        var tabData = d.tabs[t];
                        if (tabData.inputJson && tabData.inputJson !== '{}') {
                            try { localStorage.setItem('mc-tab-inputs-' + t, tabData.inputJson); } catch(e2) {}
                        }
                        if (tabData.resultJson) {
                            try { localStorage.setItem('mc-tab-result-' + t, tabData.resultJson); } catch(e2) {}
                        }
                    });
                }

                closeProjectsModal();
                activateTab('risks');
                Object.keys(FIELDS).forEach(function(t) { restoreTabFromStorage(t); });
                window.formatAllRisksImpacts();
                window.recalcAllRisksVe();
                ['risks','costs','schedule','sched-risks','var'].forEach(function(t) {
                    var res = loadResultFromStorage(t); if (res) renderMcResult(t, res);
                });
                ['sra','cra'].forEach(function(t) {
                    var res = loadResultFromStorage(t);
                    if (res) {
                        var el = document.getElementById(t + '-result');
                        if (el) el.classList.remove('hidden');
                        renderMcResult(t, res);
                        if (t === 'sra') renderPlannedDaysBadges('mc-schedule-planned-sra', res);
                    }
                });
                var corrRes = loadResultFromStorage('corr');
                if (corrRes) {
                    var corrEl = document.getElementById('corr-result');
                    if (corrEl) corrEl.classList.remove('hidden');
                    if (corrRes.mode === 'tallerEat' && corrRes.data && corrRes.proyectoId) {
                        fetch('/TallerCostos?handler=ResumenGlobalJson&proyectoId=' + encodeURIComponent(corrRes.proyectoId), { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                            .then(function(r) { return r.ok ? r.json() : null; }).catch(function() { return null; })
                            .then(function(resumen) {
                                renderCorrTallerEAT(corrRes.data, resumen, corrRes.proyectoId, corrRes.data.varSource || 'ambos');
                                if (corrRes.corrViz && corrRes.corrViz.achievedMatrix) renderCorrResult(corrRes.corrViz);
                            });
                    } else {
                        renderCorrResult(corrRes);
                    }
                }
                updateProjectBanner();
                updateTabBadges();
                updatePdfPanel();
            })
            .catch(function(e) { mcDialogAlert('Error: ' + e.message, { title: 'Error', variant: 'danger' }); });
    };

    // ── Limpiar todo ──────────────────────────────────────────────────────────

    window.confirmClearAll = function() {
        mcDialogConfirm('¿Eliminar todos los datos locales?\n\nSe perderán inputs y resultados no guardados en la API.', {
            title: 'Borrar datos locales',
            variant: 'warning',
            confirmLabel: 'Sí, borrar todo',
            cancelLabel: 'Cancelar'
        }).then(function(ok) {
            if (!ok) return;
            var keys = ['mc-project-name','mc-project-id'];
            ['risks','costs','schedule','sched-risks','sra','cra','var','corr'].forEach(function(t) {
                keys.push('mc-tab-inputs-' + t); keys.push('mc-tab-result-' + t);
            });
            try { keys.forEach(function(k) { localStorage.removeItem(k); }); } catch(e) {}
            location.reload();
        });
    };

    // ── Informe Final ─────────────────────────────────────────────────────────

    window.generateReport = function() {
        var ALL_TABS = ['risks','costs','schedule','sched-risks','sra','cra','var'];
        var done = ALL_TABS.filter(function(t) { return !!_mcAllData[t] || !!localStorage.getItem('mc-tab-result-' + t); });
        if (!done.length) {
            mcDialogAlert('Ejecute al menos un análisis antes de generar el informe.', { title: 'Sin análisis', variant: 'info' });
            return;
        }

        var allData = {};
        done.forEach(function(t) { allData[t] = _mcAllData[t] || loadResultFromStorage(t); });

        var TAB_NAMES  = { risks:'Riesgos en Costo', costs:'Estimación de Costos', schedule:'Cronograma',
            'sched-risks':'Riesgos del Programa', sra:'SRA (Cronograma + Riesgos)', cra:'CRA (Costos + Riesgos)', 'var':'VaR' };
        var TAB_COLORS = { risks:'#dc2626', costs:'#059669', schedule:'#2563eb', 'sched-risks':'#d97706',
            sra:'#0891b2', cra:'#7c3aed', 'var':'#7c3aed' };
        var nowStr   = new Date().toLocaleString('es-CL');
        var projName = _projectName || 'Sin nombre';

        var css = '<style>*{box-sizing:border-box}body{font-family:system-ui,-apple-system,sans-serif;color:#1e293b;background:#f8fafc;margin:0;padding:0}.page{max-width:920px;margin:0 auto;padding:2rem}.cover{background:linear-gradient(135deg,#4338ca,#6366f1);color:#fff;padding:2.5rem 2rem;border-radius:1rem;margin-bottom:2rem}.cover h1{margin:0 0 0.4rem;font-size:1.8rem}.cover .sub{opacity:.85;font-size:.95rem}.cover .meta{font-size:.75rem;opacity:.65;margin-top:.75rem}.section{background:#fff;border-radius:1rem;border:1px solid #e2e8f0;padding:1.5rem;margin-bottom:1.5rem}.sh{display:flex;align-items:center;gap:.75rem;margin-bottom:1rem;padding-bottom:.75rem;border-bottom:2px solid #f1f5f9}.kgrid{display:grid;grid-template-columns:repeat(auto-fit,minmax(130px,1fr));gap:.6rem;margin-bottom:1rem}.kpi{background:#f8fafc;border:1px solid #e2e8f0;border-radius:.5rem;padding:.65rem}.kpi .lbl{font-size:.65rem;color:#64748b;text-transform:uppercase;letter-spacing:.04em;margin-bottom:.2rem}.kpi .val{font-size:1.05rem;font-weight:700}.pgrid{display:grid;grid-template-columns:repeat(5,1fr);gap:.2rem;margin-bottom:.8rem}.pc{background:#f8fafc;border:1px solid #e2e8f0;border-radius:.35rem;padding:.3rem .4rem;font-size:.7rem}.pc.p50{background:#eef2ff;border-color:#c7d2fe;font-weight:700}.pc.p80{background:#fefce8;border-color:#fde047}.pc.p90{background:#fff7ed;border-color:#fdba74}.pc .pl{color:#64748b;font-size:.6rem}.pc .pv{color:#1e293b;font-weight:600}ul.recs{list-style:none;padding:0;margin:0}.recs li{padding:.3rem 0;border-bottom:1px solid #f1f5f9;font-size:.875rem;color:#475569}.recs li::before{content:"→ ";color:#6366f1;font-weight:700}.rbadge{display:inline-flex;align-items:center;padding:.2rem .6rem;border-radius:9999px;font-size:.7rem;font-weight:700}.rl{background:#d1fae5;color:#065f46}.rm{background:#fef3c7;color:#92400e}.rh{background:#fee2e2;color:#991b1b}.sgrid{display:grid;grid-template-columns:1fr 1fr;gap:1rem}.scard{border:1px solid #e2e8f0;border-radius:.75rem;padding:1rem}@media print{.page{padding:0}.section{break-inside:avoid}}</style>';

        var html = '<!DOCTYPE html><html lang="es"><head><meta charset="UTF-8"><title>Informe Monte Carlo — ' + escHtml(projName) + '</title>' + css + '</head><body><div class="page">';

        // Cover
        html += '<div class="cover"><h1>Informe de Análisis Monte Carlo</h1><div class="sub">Proyecto: <strong>' + escHtml(projName) + '</strong></div><div class="sub">Análisis: ' + done.map(function(t){return TAB_NAMES[t];}).join(' · ') + '</div><div class="meta">Generado: ' + nowStr + '</div></div>';

        // Executive Summary grid
        html += '<div class="section"><div class="sh"><h2 style="margin:0;font-size:1.05rem">Resumen Ejecutivo</h2></div><div class="sgrid">';
        done.forEach(function(t) {
            var d = allData[t]; if (!d||!d.statistics) return;
            var s = d.statistics, cv = s.mean&&s.mean!==0?Math.abs(s.stdDev/s.mean)*100:0;
            var lvl = cv<20?'BAJO':cv<50?'MEDIO':'ALTO', lc = cv<20?'rl':cv<50?'rm':'rh';
            html += '<div class="scard"><div style="display:flex;align-items:center;gap:.4rem;margin-bottom:.4rem">' +
                '<span style="width:10px;height:10px;border-radius:50%;background:'+TAB_COLORS[t]+';flex-shrink:0"></span>' +
                '<strong style="font-size:.85rem">'+TAB_NAMES[t]+'</strong>' +
                '<span class="rbadge '+lc+'" style="margin-left:auto;font-size:.65rem">'+lvl+'</span></div>' +
                '<div style="font-size:.78rem;color:#64748b">P50: <strong>'+fmtN(s.p50)+'</strong> · P80: <strong>'+fmtN(s.p80)+'</strong> · P90: <strong>'+fmtN(s.p90)+'</strong></div>' +
                '<div style="font-size:.78rem;color:#64748b;margin-top:.2rem">Media: '+fmtN(s.mean)+' · Desv.: '+fmtN(s.stdDev)+'</div></div>';
        });
        html += '</div></div>';

        // Per-tab detail
        done.forEach(function(t) {
            var d = allData[t]; if (!d||!d.statistics) return;
            var s = d.statistics, cv = s.mean&&s.mean!==0?Math.abs(s.stdDev/s.mean)*100:0;
            var lvl = cv<20?'BAJO':cv<50?'MEDIO':'ALTO', lc = cv<20?'rl':cv<50?'rm':'rh';
            html += '<div class="section"><div class="sh">' +
                '<span style="width:10px;height:24px;border-radius:3px;background:'+TAB_COLORS[t]+';flex-shrink:0"></span>' +
                '<h2 style="margin:0;font-size:1rem">'+TAB_NAMES[t]+'</h2>' +
                '<span class="rbadge '+lc+'" style="margin-left:auto">Incertidumbre: '+lvl+' (CV='+cv.toFixed(1)+'%)</span></div>';

            // KPIs
            html += '<div class="kgrid">';
            [['Mínimo',s.min],['Máximo',s.max],['Media',s.mean],['Moda',s.mode],['Desv. Est.',s.stdDev],['Asimetría',s.skewness],['Curtosis',s.kurtosis]].forEach(function(kv) {
                html += '<div class="kpi"><div class="lbl">'+kv[0]+'</div><div class="val">'+fmtN(kv[1])+'</div></div>';
            });
            html += '</div>';

            // Percentile grid
            html += '<h3 style="font-size:.72rem;color:#64748b;text-transform:uppercase;letter-spacing:.04em;margin:.6rem 0 .4rem">Percentiles clave</h3><div class="pgrid">';
            [['P5',s.p5],['P10',s.p10],['P20',s.p20],['P25',s.p25],['P30',s.p30],
             ['P40',s.p40],['P50',s.p50],['P60',s.p60],['P70',s.p70],['P75',s.p75],
             ['P80',s.p80],['P85',s.p85],['P90',s.p90],['P95',s.p95],['P99',s.p99||s.p95]].forEach(function(p) {
                var cls = p[0]==='P50'?'p50':p[0]==='P80'?'p80':p[0]==='P90'?'p90':'';
                html += '<div class="pc '+cls+'"><div class="pl">'+p[0]+'</div><div class="pv">'+fmtN(p[1])+'</div></div>';
            });
            html += '</div>';

            // Recommendations
            var recs=[], typ=d.simulationType||'', c80=s.p80-s.mean, c90=s.p90-s.mean, m2=d.additionalMetrics||{};
            if(typ==='CostEstimation')      recs=['Presupuesto base (P50): '+fmtN(s.p50),'Contingencia P80: +'+fmtN(c80)+' ('+( s.mean?(c80/s.mean*100).toFixed(1):'?')+'%)','Presupuesto conservador (P90): '+fmtN(s.p90),'Rango P25–P75: '+fmtN(s.p25)+' — '+fmtN(s.p75)];
            else if(typ==='ScheduleEstimation') recs=['Plazo base (P50): '+fmtN(s.p50)+' días','Plazo recomendado (P80): '+fmtN(s.p80)+' días','Buffer P80 vs P50: +'+fmtN(s.p80-s.p50)+' días','Plazo pesimista (P90): '+fmtN(s.p90)+' días'];
            else if(typ==='RiskAnalysis')   recs=['Pérdida esperada: '+fmtN(s.mean),'Provisión P80: '+fmtN(s.p80),'Reserva P90: '+fmtN(s.p90),'Rango P25–P75: '+fmtN(s.p25)+' — '+fmtN(s.p75)];
            else if(typ==='ScheduleRiskAnalysis') recs=['Impacto esperado: '+fmtN(s.mean)+' días','Provisión P80: '+fmtN(s.p80)+' días','Reserva P90: '+fmtN(s.p90)+' días'];
            else if(typ==='SRA') recs=['Plazo combinado P50: '+fmtN(s.p50)+' días','Plazo P80: '+fmtN(s.p80)+' días','Plazo P90: '+fmtN(s.p90)+' días','Desv. estándar: '+fmtN(s.stdDev)];
            else if(typ==='CRA') recs=['Costo combinado P50: '+fmtN(s.p50),'Costo P80: '+fmtN(s.p80),'Costo P90: '+fmtN(s.p90),'Desv. estándar: '+fmtN(s.stdDev)];
            else if(typ==='ValueAtRisk') recs=['Valor esperado: '+fmtN(s.mean),'Escenario base (P50): '+fmtN(s.p50),'VaR ('+((m2.confidenceLevel||0.95)*100).toFixed(0)+'%): '+fmtN(m2.valueAtRisk||s.p5),'Pérdida potencial: '+fmtN(m2.potentialLoss)];

            if(recs.length) {
                html += '<h3 style="font-size:.72rem;color:#64748b;text-transform:uppercase;letter-spacing:.04em;margin:.6rem 0 .4rem">Recomendaciones</h3><ul class="recs">';
                recs.forEach(function(r){ html += '<li>'+r+'</li>'; });
                html += '</ul>';
            }
            html += '</div>';
        });

        // Consolidated conclusion
        html += '<div class="section"><div class="sh"><h2 style="margin:0;font-size:1.05rem">Conclusión Consolidada</h2></div>';
        html += '<p style="font-size:.875rem;color:#475569;margin:0 0 .75rem">Basado en <strong>'+done.length+'</strong> análisis para el proyecto <strong>'+escHtml(projName)+'</strong>:</p><ul class="recs">';
        var cd = allData['costs']||allData['cra'], sd = allData['schedule']||allData['sra'], rd = allData['risks'];
        if(cd&&cd.statistics){ var cs=cd.statistics,ccv=cs.mean&&cs.mean!==0?Math.abs(cs.stdDev/cs.mean)*100:0; html += '<li>COSTO — Presupuesto recomendado (P80): <strong>'+fmtN(cs.p80)+'</strong>. Contingencia sobre media: +'+fmtN(cs.p80-cs.mean)+'. Nivel: <strong>'+(ccv<20?'BAJO':ccv<50?'MEDIO':'ALTO')+'</strong> (CV='+ccv.toFixed(1)+'%).</li>'; }
        if(sd&&sd.statistics){ var ss=sd.statistics,scv=ss.mean&&ss.mean!==0?Math.abs(ss.stdDev/ss.mean)*100:0; html += '<li>PLAZO — Cronograma recomendado (P80): <strong>'+fmtN(ss.p80)+' días</strong>. Buffer vs P50: +'+fmtN(ss.p80-ss.p50)+' días. Nivel: <strong>'+(scv<20?'BAJO':scv<50?'MEDIO':'ALTO')+'</strong>.</li>'; }
        if(rd&&rd.statistics){ var rs=rd.statistics; html += '<li>RIESGOS — Pérdida esperada: <strong>'+fmtN(rs.mean)+'</strong>. Provisión P80: <strong>'+fmtN(rs.p80)+'</strong>. Reserva P90: '+fmtN(rs.p90)+'.</li>'; }
        html += '<li>Revisar Tornado Charts para priorizar variables de mayor impacto y focalizar esfuerzos de mitigación.</li></ul></div>';

        html += '<p style="text-align:center;font-size:.72rem;color:#94a3b8;margin-top:1.5rem;padding-bottom:2rem">Generado automáticamente · Simulación Monte Carlo · '+nowStr+'</p>';
        html += '</div></body></html>';

        var win = window.open('','_blank');
        if (win) { win.document.write(html); win.document.close(); }
        else mcDialogAlert('El navegador bloqueó la ventana emergente. Permita ventanas emergentes para esta página.', { title: 'Ventana bloqueada', variant: 'warning' });
    };

    // ── Auto-render: se ejecuta DESPUÉS de que las funciones estén definidas ──
    (function() {
        // 0. Migración de formato: limpiar inputs guardados en formato div-card
        //    Detectamos si un valor guardado corresponde al formato antiguo chequeando
        //    si el body del tab es <tbody> (tabla) pero el localStorage tiene cero rows.
        //    La forma más segura: versionar el storage.
        try {
            var MC_STORAGE_VERSION = '4'; // incrementar al cambiar formato de inputs
            if (localStorage.getItem('mc-storage-ver') !== MC_STORAGE_VERSION) {
                ['risks','costs','schedule','sched-risks','var','corr'].forEach(function(t) {
                    localStorage.removeItem('mc-tab-inputs-' + t);
                });
                localStorage.setItem('mc-storage-ver', MC_STORAGE_VERSION);
            }
        } catch(e) {}

        // 1. Restaurar nombre de proyecto desde localStorage
        updateProjectBanner();

        // 2. Tabs con resultado del servidor (POST + redirect)
        var serverTabs = ['risks','costs','schedule','sched-risks','var'];
        serverTabs.forEach(function(t) {
            var el = document.getElementById('mc-data-' + t);
            if (!el) return;
            try {
                var d = JSON.parse(el.textContent);
                if (d) { renderMcResult(t, d); saveResultToStorage(t, d); saveTabToStorage(t); }
            } catch(e) { console.error('MC render error [' + t + ']:', e); }
        });

        // 3. Restaurar inputs y resultados desde localStorage para todos los demás tabs
        Object.keys(FIELDS).forEach(function(t) {
            var hasFresh = !!document.getElementById('mc-data-' + t);
            if (!hasFresh) restoreTabFromStorage(t);
        });
        recalcAllRisksVe();
        ['risks','costs','schedule','sched-risks','var'].forEach(function(t) {
            if (!_mcAllData[t]) {
                // Solo restaurar resultados si también hay inputs guardados
                var hasInputs = !!localStorage.getItem('mc-tab-inputs-' + t);
                if (!hasInputs) {
                    var resSect = document.getElementById(t + '-result-section');
                    if (resSect) resSect.classList.add('hidden');
                    return;
                }
                var res = loadResultFromStorage(t);
                if (res) {
                    renderMcResult(t, res);
                    if (t === 'risks' && res.statistics) renderRisksVerification(res.statistics);
                }
            }
        });
        ['sra','cra'].forEach(function(t) {
            var res = loadResultFromStorage(t);
            if (res) {
                var el = document.getElementById(t + '-result');
                if (el) el.classList.remove('hidden');
                renderMcResult(t, res);
                if (t === 'sra') renderPlannedDaysBadges('mc-schedule-planned-sra', res);
            }
        });
        (function() {
            var res = loadResultFromStorage('corr');
            if (res) {
                var el = document.getElementById('corr-result');
                if (el) el.classList.remove('hidden');
                if (res.mode === 'tallerEat' && res.data && res.proyectoId) {
                    fetch('/TallerCostos?handler=ResumenGlobalJson&proyectoId=' + encodeURIComponent(res.proyectoId), { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                        .then(function(r) { return r.ok ? r.json() : null; }).catch(function() { return null; })
                        .then(function(rsum) {
                            renderCorrTallerEAT(res.data, rsum, res.proyectoId, res.data.varSource || 'ambos');
                            if (res.corrViz && res.corrViz.achievedMatrix) renderCorrResult(res.corrViz);
                        });
                } else {
                    renderCorrResult(res);
                }
            }
        })();

        // 4. Actualizar badges y estado de botones
        updateTabBadges();

        // 5. Verificación final: si la tabla de riesgos no tiene datos reales, ocultar resultados
        if (typeof window.tcEnforceRisksResultVisibility === 'function')
            window.tcEnforceRisksResultVisibility();
    })();

    function escHtml(s) {
        return String(s).replace(/&/g,'&amp;').replace(/\x3C/g,'&lt;').replace(/\x3E/g,'&gt;');
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ── ASISTENTE GPR (integración Ollama / ARIA) ───────────────────────────
    // ══════════════════════════════════════════════════════════════════════════

    var _ariaHistory     = [];   // { role: 'user'|'aria', text: string }
    var _tcAriaContext   = null; // JSON string con análisis Taller de Costos cargado
    var _tcAriaProyId    = null; // UUID del proyecto TC activo en ARIA

    // Convierte markdown básico a HTML para los mensajes de ARIA
    function ariaMd(text) {
        return text
            // Escapar HTML primero (salvo que ya sea HTML)
            .replace(/&/g,'&amp;').replace(/\x3C/g,'&lt;').replace(/\x3E/g,'&gt;')
            // Títulos ##
            .replace(/^## (.+)$/gm,'<h2 class="text-sm font-bold mt-3 mb-1">$1</h2>')
            .replace(/^### (.+)$/gm,'<h3 class="text-xs font-bold mt-2 mb-0.5 text-slate-600 dark:text-slate-300">$1</h3>')
            // Negrita **texto**
            .replace(/\*\*([^*]+)\*\*/g,'<strong>$1</strong>')
            // Cursiva *texto*
            .replace(/\*([^*]+)\*/g,'<em>$1</em>')
            // Código `texto`
            .replace(/`([^`]+)`/g,'<code>$1</code>')
            // Listas con •
            .replace(/^[•·\-] (.+)$/gm,'<li>$1</li>')
            // Listas numéricas
            .replace(/^\d+\. (.+)$/gm,'<li>$1</li>')
            // Envolver series de li en ul
            .replace(/(<li>.*?<\/li>(\n|$))+/gs, function(m){ return '<ul>' + m + '</ul>'; })
            // Párrafos: líneas vacías separan párrafos
            .replace(/\n{2,}/g,'</p><p class="mt-2">')
            // Saltos de línea simples
            .replace(/\n/g,'<br>');
    }

    // Construye el JSON de contexto con todos los resultados disponibles
    function buildAriaContext() {
        var ctx = {};
        var tabNames = {
            risks: 'Riesgos en Costo', costs: 'Estimación de Costos',
            schedule: 'Cronograma', 'sched-risks': 'Riesgos del Programa',
            sra: 'SRA (Cronograma+Riesgos)', cra: 'CRA (Costos+Riesgos)',
            'var': 'VaR', corr: 'Correlaciones'
        };
        Object.keys(tabNames).forEach(function(t) {
            var d = _mcAllData[t] || loadResultFromStorage(t);
            if (d && d.statistics) {
                var s = d.statistics;
                var cv = s.mean && s.mean !== 0 ? (Math.abs(s.stdDev / s.mean) * 100).toFixed(1) : 'N/D';
                ctx[tabNames[t]] = {
                    tipo: d.simulationType || t,
                    simulaciones: d.totalSimulations || 10000,
                    estadisticas: {
                        min: s.min, max: s.max, media: s.mean, moda: s.mode,
                        desvEstd: s.stdDev, varianza: s.variance,
                        asimetria: s.skewness, curtosis: s.kurtosis,
                        coefVariacion_pct: cv, errores: s.errors
                    },
                    percentiles: {
                        P5: s.p5, P10: s.p10, P25: s.p25, P50: s.p50,
                        P75: s.p75, P80: s.p80, P85: s.p85, P90: s.p90, P95: s.p95
                    },
                    metricas_adicionales: d.additionalMetrics || {}
                };
            }
        });
        return Object.keys(ctx).length > 0 ? JSON.stringify(ctx, null, 2) : '';
    }

    // ─── Taller de Costos — carga y contexto en tiempo real ─────────────────

    /** Puebla el selector de proyectos TC en la pestaña ARIA. */
    async function ariaPopularProyectosTC() {
        var sel = document.getElementById('aria-tc-proy-sel');
        if (!sel) return;
        try {
            var resp = await fetch('/TallerCostos?handler=ProyectosJson', {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!resp.ok) return;
            var lista = await resp.json();
            var current = sel.value;
            sel.innerHTML = '<option value="">— Selecciona un proyecto del Taller —</option>' +
                lista.map(function(p) {
                    return '<option value="' + p.id + '">' + escHtml(p.codigo) + ' — ' + escHtml(p.nombre) + '</option>';
                }).join('');
            if (current && lista.some(function(p) { return p.id === current; })) sel.value = current;
        } catch(e) { /* silencioso */ }
    }

    /** Carga el análisis completo de un proyecto TC y lo guarda en _tcAriaContext. */
    window.ariaCargarProyectoTC = async function(proyectoId) {
        var statusEl  = document.getElementById('aria-tc-status');
        var infoEl    = document.getElementById('aria-tc-context-info');
        var labelEl   = document.getElementById('aria-tc-context-label');

        if (!proyectoId) {
            _tcAriaContext = null;
            _tcAriaProyId  = null;
            if (statusEl)  statusEl.textContent = 'Sin proyecto cargado';
            if (infoEl)    infoEl.classList.add('hidden');
            updateAriaContextBadges();
            return;
        }

        if (statusEl) statusEl.textContent = 'Cargando análisis…';
        if (infoEl)   infoEl.classList.add('hidden');

        try {
            var resp = await fetch('/TallerCostos?handler=ResumenGlobalJson&proyectoId=' + proyectoId, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!resp.ok) throw new Error('HTTP ' + resp.status);
            var data = await resp.json();
            _tcAriaContext = buildTCAriaContext(data);
            _tcAriaProyId  = proyectoId;

            var proy = data.proyecto || {};
            var label = (proy.codigo || '') + ' — ' + (proy.nombre || '') +
                        ' · EAT ' + (data.totales ? tcFmt(data.totales.pctEAT, 1) + '% CAPEX' : '');
            if (statusEl) statusEl.textContent = label;
            if (infoEl)   infoEl.classList.remove('hidden');
            if (labelEl)  labelEl.textContent  = 'Análisis cargado: ' + label;
            updateAriaContextBadges();
        } catch(e) {
            _tcAriaContext = null;
            if (statusEl) statusEl.textContent = 'Error: ' + e.message;
        }
    };

    /** Convierte la respuesta de ResumenGlobalJson en un contexto estructurado para ARIA. */
    function buildTCAriaContext(data) {
        if (!data || !data.totales) return null;
        var T   = data.totales;
        var EC  = data.eatCombinado;
        var RD  = data.riesgosDisplay;
        var TN  = data.tornado;
        var ESC = data.escenarios;
        var fmt = function(v) { return v != null ? Math.round(v).toLocaleString('es-CL') + ' USD' : 'N/D'; };
        var pct = function(v) { return v != null ? (Math.round(v * 10) / 10) + '%' : 'N/D'; };

        var ctx = {
            TALLER_DE_COSTOS: {
                proyecto: {
                    nombre: (data.proyecto || {}).nombre || '',
                    codigo: (data.proyecto || {}).codigo || '',
                    organizacion: (data.proyecto || {}).organizacion || '',
                    fecha_ejercicio: (data.proyecto || {}).fechaEjercicio || ''
                },
                estado_financiero: {
                    capex_usd: fmt(T.capex),
                    eat_deterministico_usd: fmt(T.eat),
                    comprometido_usd: fmt(T.comprometido),
                    por_comprometer_usd: fmt(T.porComprometer),
                    pct_eat_vs_capex: pct(T.pctEAT),
                    pct_comprometido_vs_capex: pct(T.pctComprometido),
                    slack_usd: fmt(T.slack),
                    semaforo: T.pctEAT > 115 ? 'ROJO — CRÍTICO (EAT supera 115% del CAPEX)' :
                              T.pctEAT > 105 ? 'ÁMBAR — ALERTA (EAT supera CAPEX)' :
                              T.pctEAT > 95  ? 'VERDE — DENTRO DE PRESUPUESTO' : 'VERDE — BAJO PRESUPUESTO',
                    contratos_totales: data.contratos ? data.contratos.length : 0,
                    contratos_con_mc: T.contratosMcOk,
                    contratos_sin_mc: T.contratosSinMc,
                    incertidumbre_usd: fmt(T.incertidumbre),
                    certeza_usd: fmt(T.certeza),
                    pct_incertidumbre: pct(T.incertidumbre / ((T.certeza || 0) + (T.incertidumbre || 1) || 1) * 100)
                }
            }
        };

        // EAT Probabilístico (MC combinado)
        if (EC) {
            ctx.TALLER_DE_COSTOS.eat_probabilistico = {
                nota: 'EAT Total = Comprometido + Certeza + Incertidumbre MC + Riesgos MC',
                eat_P10_usd: fmt(EC.eatP10),
                eat_P50_usd: fmt(EC.eatP50),
                eat_P80_usd: fmt(EC.eatP80),
                eat_P90_usd: fmt(EC.eatP90),
                eat_media_usd: fmt(EC.eatMedia),
                cvar90_usd: EC.cvar90 != null ? fmt(T.comprometido + EC.cvar90) : 'N/D',
                cvar80_usd: EC.cvar80 != null ? fmt(T.comprometido + EC.cvar80) : 'N/D',
                exceso_cola_cvar90_vs_P90: EC.excessP90 != null ? fmt(EC.excessP90) : 'N/D',
                spread_P10_P90_usd: fmt(EC.eatP90 - EC.eatP10),
                n_simulaciones: EC.nSimulaciones,
                n_items_incertidumbre: EC.nItems,
                n_riesgos_mc: EC.nRiesgos,
                contingencia_amenazas_P80_usd: fmt(EC.contingenciaAmenazasP80),
                impacto_oportunidades_P50_usd: EC.impactoOportunidadesP50 != null ? fmt(Math.abs(EC.impactoOportunidadesP50)) + ' (reducción)' : 'N/D'
            };
        }

        // MC por contrato (solo si hay datos)
        if (T.mcP50 != null) {
            ctx.TALLER_DE_COSTOS.mc_incertidumbre_contratos = {
                nota: 'Suma de MC individuales por contrato (solo incertidumbre, sin riesgos)',
                P10_usd: fmt(T.eatP10),
                P50_usd: fmt(T.eatP50),
                P80_usd: fmt(T.eatP80),
                P90_usd: fmt(T.eatP90)
            };
        }

        // Riesgos
        if (RD) {
            ctx.TALLER_DE_COSTOS.riesgos = {
                total: RD.nRiesgos,
                amenazas: RD.nAmenazas,
                oportunidades: RD.nOportunidades,
                valor_esperado_amenazas_usd: fmt(RD.valorEsperadoAmenazas),
                valor_esperado_neto_usd: fmt(RD.valorEsperadoNeto),
                distribucion_niveles: (RD.distribucionNiveles || []).map(function(n) {
                    return { nivel: n.nivel, label: n.label, cantidad: n.count };
                }),
                top_riesgos_por_VE: (RD.topRiesgos || []).slice(0, 8).map(function(r) {
                    return {
                        codigo: r.codigo,
                        descripcion: r.descripcion,
                        probabilidad: pct(r.probabilidad),
                        impacto_probable_usd: fmt(r.moda),
                        valor_esperado_usd: fmt(r.ve),
                        nivel_codelco: r.nivelLabel,
                        tipo: r.esAmenaza ? 'Amenaza' : 'Oportunidad',
                        tiene_plan_respuesta: r.tieneRespuesta ? 'Sí' : 'NO — SIN PLAN'
                    };
                })
            };
        }

        // Tornado Chart
        if (TN && TN.barras && TN.barras.length > 0) {
            ctx.TALLER_DE_COSTOS.tornado_sensibilidad = {
                eat_base_usd: fmt(TN.eatBase),
                interpretacion: 'Swing = cuánto varía el EAT al llevar un ítem de mínimo a máximo, fijando el resto. Mayor swing = mayor influencia en el presupuesto.',
                top_impulsores: TN.barras.slice(0, 7).map(function(b) {
                    return {
                        nombre: b.nombre,
                        tipo: b.esRiesgo ? 'Riesgo' : 'Ítem de incertidumbre',
                        swing_usd: fmt(b.swing),
                        eat_en_minimo: fmt(b.eatMin),
                        eat_en_maximo: fmt(b.eatMax)
                    };
                })
            };
        }

        // What-If Escenarios
        if (ESC) {
            ctx.TALLER_DE_COSTOS.escenarios_whatif = {
                interpretacion: 'Optimista: ítems en mínimo + amenazas -50% prob. Pesimista: ítems en máximo + amenazas +50% prob.',
                optimista: {
                    eat_usd: fmt(ESC.optimista.eat),
                    pct_capex: pct(ESC.optimista.pctCapex),
                    vs_base: (ESC.optimista.vsBase > 0 ? '+' : '') + pct(ESC.optimista.vsBase)
                },
                base: {
                    eat_usd: fmt(ESC.base.eat),
                    pct_capex: pct(ESC.base.pctCapex)
                },
                pesimista: {
                    eat_usd: fmt(ESC.pesimista.eat),
                    pct_capex: pct(ESC.pesimista.pctCapex),
                    vs_base: (ESC.pesimista.vsBase > 0 ? '+' : '') + pct(ESC.pesimista.vsBase)
                },
                spread_opt_pes_usd: fmt(ESC.pesimista.eat - ESC.optimista.eat)
            };
        }

        // Alertas derivadas (computadas en el cliente para que ARIA las analice)
        var alertas = [];
        if (T.pctEAT > 115) alertas.push('CRÍTICO: EAT supera el 115% del CAPEX — déficit proyectado de ' + fmt(Math.abs(T.slack)));
        else if (T.pctEAT > 105) alertas.push('ALERTA: EAT supera el CAPEX en ' + pct(T.pctEAT - 100));
        if (EC && EC.eatP80 > T.capex) alertas.push('EAT probabilístico P80 (' + fmt(EC.eatP80) + ') supera CAPEX — riesgo confirmado de sobrecosto');
        if (T.contratosSinMc > 0) alertas.push(T.contratosSinMc + ' contrato(s) sin Monte Carlo — EAT probabilístico incompleto');
        if (RD && (RD.topRiesgos || []).some(function(r) { return !r.tieneRespuesta && r.nivel >= 4; }))
            alertas.push('Hay riesgos Nivel 4-5 (Muy Probable / Casi Seguro) SIN plan de respuesta');
        if (EC && EC.excessP90 > (T.capex || 1) * 0.05)
            alertas.push('Cola gorda: CVaR90 excede P90 en ' + fmt(EC.excessP90) + ' — contingencia adicional recomendada');
        if (alertas.length > 0) ctx.TALLER_DE_COSTOS.alertas_activas = alertas;

        return JSON.stringify(ctx, null, 2);
    }

    // Actualiza los badges de contexto en la pestaña IA
    function updateAriaContextBadges() {
        var el = document.getElementById('aria-context-badges');
        if (!el) return;
        var tabNames = {
            risks: { label: 'Riesgos', color: 'red' },
            costs: { label: 'Costos', color: 'emerald' },
            schedule: { label: 'Cronograma', color: 'blue' },
            'sched-risks': { label: 'Riesgos Prog.', color: 'amber' },
            sra: { label: 'SRA', color: 'cyan' },
            cra: { label: 'CRA', color: 'violet' },
            'var': { label: 'VaR', color: 'violet' },
            corr: { label: 'Correlaciones', color: 'pink' }
        };
        var found = [];
        Object.keys(tabNames).forEach(function(t) {
            if (_mcAllData[t] || loadResultFromStorage(t)) found.push(tabNames[t]);
        });
        if (_tcAriaContext) found.push({ label: 'Taller de Costos', color: 'teal' });
        if (!found.length) {
            el.innerHTML = '<span class="text-xs text-slate-400 dark:text-slate-500 italic">Sin datos aún. Ejecuta simulaciones en las otras pestañas.</span>';
            return;
        }
        var colorMap = {
            red: 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300 border-red-200 dark:border-red-700',
            emerald: 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300 border-emerald-200 dark:border-emerald-700',
            blue: 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 border-blue-200 dark:border-blue-700',
            amber: 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300 border-amber-200 dark:border-amber-700',
            cyan: 'bg-cyan-100 dark:bg-cyan-900/40 text-cyan-700 dark:text-cyan-300 border-cyan-200 dark:border-cyan-700',
            violet: 'bg-violet-100 dark:bg-violet-900/40 text-violet-700 dark:text-violet-300 border-violet-200 dark:border-violet-700',
            pink: 'bg-pink-100 dark:bg-pink-900/40 text-pink-700 dark:text-pink-300 border-pink-200 dark:border-pink-700',
            teal: 'bg-teal-100 dark:bg-teal-900/40 text-teal-700 dark:text-teal-300 border-teal-200 dark:border-teal-700'
        };
        el.innerHTML = found.map(function(f) {
            var cls = colorMap[f.color] || '';
            return '<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium border ' + cls + '">' +
                '<span class="material-icons" style="font-size:11px">check_circle</span>' + f.label + '</span>';
        }).join('');
    }

    // Agrega un mensaje al historial visual de chat
    function ariaAddMessage(role, html) {
        var container = document.getElementById('aria-chat-history');
        if (!container) return;
        var isUser = role === 'user';
        var div = document.createElement('div');
        div.className = 'flex ' + (isUser ? 'justify-end' : 'justify-start') + ' gap-2.5 items-start';
        if (!isUser) {
            div.innerHTML =
                '<div class="w-8 h-8 rounded-full bg-indigo-100 dark:bg-indigo-900/50 flex items-center justify-center flex-shrink-0 mt-0.5">' +
                '<span class="material-icons text-indigo-500" style="font-size:16px">smart_toy</span></div>' +
                '<div class="aria-ai-bubble max-w-[85%] px-4 py-3 text-sm aria-md text-slate-700 dark:text-slate-200 shadow-sm">' +
                '<p class="text-xs font-semibold text-indigo-600 dark:text-indigo-400 mb-1.5">ASISTENTE GPR — Experto Monte Carlo</p>' +
                html + '</div>';
        } else {
            div.innerHTML =
                '<div class="aria-user-bubble max-w-[80%] px-4 py-3 text-sm text-indigo-900 dark:text-indigo-100">' + escHtml(html) + '</div>' +
                '<div class="w-8 h-8 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center flex-shrink-0 mt-0.5">' +
                '<span class="material-icons text-slate-500 dark:text-slate-400" style="font-size:16px">person</span></div>';
        }
        container.appendChild(div);
        div.scrollIntoView({ behavior: 'smooth', block: 'end' });
        return div;
    }

    // Muestra el indicador de escritura de ARIA
    function ariaShowThinking() {
        var container = document.getElementById('aria-chat-history');
        if (!container) return null;
        var div = document.createElement('div');
        div.id = 'aria-thinking-bubble';
        div.className = 'flex justify-start gap-2.5 items-start';
        div.innerHTML =
            '<div class="w-8 h-8 rounded-full bg-indigo-100 dark:bg-indigo-900/50 flex items-center justify-center flex-shrink-0">' +
            '<span class="material-icons text-indigo-500" style="font-size:16px">smart_toy</span></div>' +
            '<div class="aria-ai-bubble px-4 py-3">' +
            '<div class="aria-thinking"><span></span><span></span><span></span></div>' +
            '</div>';
        container.appendChild(div);
        div.scrollIntoView({ behavior: 'smooth', block: 'end' });
        return div;
    }

    // Envía la consulta al asistente IA
    window.sendAiConsult = function() {
        var inputEl = document.getElementById('aria-input');
        var sendBtn = document.getElementById('aria-send-btn');
        var question = inputEl ? inputEl.value.trim() : '';
        if (!question) return;

        // Mostrar mensaje del usuario
        ariaAddMessage('user', question);
        if (inputEl) inputEl.value = '';
        if (sendBtn) sendBtn.disabled = true;

        // Mostrar indicador de escritura
        var thinkEl = ariaShowThinking();

        // Construir contexto y proyecto
        var ctx = buildAriaContext();
        var projNameEl = document.getElementById('proj-title');
        var projName = projNameEl ? projNameEl.textContent.trim() : '';

        var tok = document.querySelector('input[name="__RequestVerificationToken"]');
        fetch('?handler=AiConsult', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': tok ? tok.value : ''
            },
            body: JSON.stringify({
                question: question,
                contextJson: ctx || null,
                tcContextJson: _tcAriaContext || null,
                tabId: _currentTabId,
                projectName: projName || null
            })
        })
        .then(function(r) { return r.json(); })
        .then(function(d) {
            if (thinkEl) thinkEl.remove();
            if (d.error) {
                ariaAddMessage('aria', '<p class="text-red-500">Error: ' + escHtml(d.error) + '</p>');
            } else {
                ariaAddMessage('aria', '<div>' + ariaMd(d.response || '') + '</div>');
            }
            updateAriaContextBadges();
        })
        .catch(function(e) {
            if (thinkEl) thinkEl.remove();
            ariaAddMessage('aria', '<p class="text-red-500">Error de conexión: ' + escHtml(e.message) + '. Verifique que la API Ollama esté en ejecución.</p>');
        })
        .finally(function() {
            if (sendBtn) sendBtn.disabled = false;
        });
    };

    // Prompts rápidos predefinidos
    var _ariaQuickPrompts = {
        'riesgos': 'Analiza la exposición total de riesgos del proyecto. ¿Cuál es la pérdida esperada, cuánta contingencia se necesita en P80 y P90, y cuáles son los riesgos dominantes? Evalúa el nivel de incertidumbre y da recomendaciones concretas.',
        'costos': 'Analiza el presupuesto probabilístico del proyecto. ¿Cuánto debería autorizar el directorio y qué contingencia usar? Compara P50, P80 y P90. Evalúa el nivel de incertidumbre (CV) e interpreta la asimetría de la distribución de costos.',
        'cronograma': '¿Qué plazo contractual debería comprometerse con el cliente para tener una alta probabilidad de cumplimiento? Analiza P50, P80 y la probabilidad de cumplir el plazo planeado. ¿Cuántos días de buffer son necesarios?',
        'cra-vs-costos': 'Compara los resultados del CRA versus el análisis de Costos puro. ¿Cuánto aumenta el presupuesto necesario al incluir los riesgos? ¿Los riesgos o la incertidumbre de estimación dominan el presupuesto? ¿Qué acción es más efectiva: mitigar riesgos o mejorar la estimación?',
        'sra-vs-schedule': 'Compara los resultados del SRA versus el Cronograma puro. ¿Cuánto aumenta el plazo al incorporar los eventos de riesgo del programa? ¿Los riesgos o la variabilidad de las tareas dominan el retraso? ¿Qué actividades o riesgos son los más críticos?',
        'nivel-riesgo': 'Proporciona un resumen ejecutivo completo del proyecto con todos los análisis disponibles. Evalúa el nivel de riesgo global (BAJO/MEDIO/ALTO), las principales fuentes de incertidumbre, el presupuesto y plazo recomendados, y las 5 acciones prioritarias de mitigación. Estructura el análisis como un informe ejecutivo.',
        'var-portfolio': 'Analiza el riesgo financiero del portafolio. Interpreta el VaR y CVaR: ¿qué capital de reserva se requiere? ¿La volatilidad del portafolio es aceptable? ¿Cómo se compara el VaR con el Expected Shortfall? ¿Qué ajustes en la composición del portafolio reducirían el riesgo?',
        'acciones': 'Con base en todos los análisis disponibles, define un plan de acción prioritario para reducir el riesgo del proyecto. Ordena las acciones por impacto potencial y facilidad de implementación. Incluye tanto acciones de mitigación de riesgos como de mejora de la estimación.',
        'metodologia': 'Explícame en detalle cómo funciona la simulación de Monte Carlo y por qué es superior al estimado puntual determinístico. Incluye: distribuciones de probabilidad usadas, cómo se genera cada resultado, qué significa la distribución resultante, y cómo se diferencia el P50 de la media y por qué importa esta diferencia.',
        'tc-resumen': 'Haz un diagnóstico completo del proyecto del Taller de Costos. Analiza el estado EAT vs CAPEX, el EAT probabilístico (P50/P80/P90 y CVaR90), los top impulsores del Tornado, los escenarios What-If y los riesgos críticos. Dame las 5 acciones prioritarias.',
        'tc-cvar': 'Analiza el CVaR90 y CVaR80 del proyecto. ¿Cuánto supera el CVaR90 al P90? ¿Hay evidencia de cola gorda? ¿Qué ítems del Tornado explican el exceso? ¿Qué contingencia adicional recomiendas sobre el P90?',
        'tc-tornado': 'Interpreta el análisis Tornado del proyecto. ¿Qué ítem o riesgo tiene el mayor swing? ¿Son ítems de incertidumbre o riesgos los que dominan? ¿Dónde debería enfocarse la mitigación para reducir más el EAT P90?',
        'tc-escenarios': 'Analiza los tres escenarios What-If del proyecto (Optimista/Base/Pesimista). ¿En qué escenarios el EAT supera el CAPEX? ¿Cuál es el rango de incertidumbre total? ¿Qué probabilidad tiene el proyecto de mantenerse dentro de presupuesto?',
        'tc-riesgos-criticos': 'Identifica y analiza los riesgos críticos (Nivel 4-5) del proyecto. ¿Cuáles tienen mayor Valor Esperado? ¿Cuáles no tienen plan de respuesta? Proporciona recomendaciones concretas de mitigación para los 3 riesgos más importantes.',
        'tc-correlacion': 'Explica el efecto de la correlación entre riesgos en este proyecto. Si los riesgos tienden a materializarse juntos (correlación positiva), ¿cómo afecta al P90 y CVaR90? ¿Qué nivel de correlación es más probable dado el tipo de riesgos del proyecto?'
    };

    window.sendQuickPrompt = function(key) {
        var prompt = _ariaQuickPrompts[key];
        if (!prompt) return;
        var inputEl = document.getElementById('aria-input');
        if (inputEl) inputEl.value = prompt;
        // Activar la pestaña IA si no está activa
        if (_currentTabId !== 'ia') activateTab('ia');
        sendAiConsult();
    };

    window.clearAriaChat = function() {
        var container = document.getElementById('aria-chat-history');
        if (container) container.innerHTML = '';
        _ariaHistory = [];
    };

    // Actualizar contexto cuando se activa la pestaña IA
    var _origActivateTab = window.activateTab;
    window.activateTab = function(tabId) {
        _origActivateTab(tabId);
        if (tabId === 'ia') {
            updateAriaContextBadges();
            ariaPopularProyectosTC();
        }
    };

})();