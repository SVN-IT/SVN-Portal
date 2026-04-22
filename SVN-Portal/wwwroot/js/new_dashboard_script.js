// ── Chart instances ────────────────────────────────────────────────────
let chartProcess = null, chartVendor = null, chartCode = null;
let chartDailyProcess = null, chartDailyVendor = null;
let chartAvgManPower = null;

// ── Code map: product_id → Operation text ──────────────────────────────
let codeMap = {};

// ── Palette color ────────────────────────────────────────────────────────────
const COLORS = [
    '#1B4F8A', '#2563a8', '#0d9488', '#8b5cf6', '#ea580c', '#10b981',
    '#f59e0b', '#ef4444', '#06b6d4', '#84cc16', '#ec4899', '#6366f1',
    '#14b8a6', '#f97316', '#a855f7', '#22c55e', '#3b82f6', '#e11d48'
];

// ── Tắt datalabels global, chỉ dùng per-chart ─────────────────────────
Chart.unregister(ChartDataLabels);

// ── Init ───────────────────────────────────────────────────────────────
window.addEventListener('DOMContentLoaded', async () => {
    setDefaultDates();
    await Promise.all([loadAllCodes(), loadVendors(), loadProcesses()]);
    loadReport();
});

// ── Default Dates: From Date empty, To Date = today ────────────────────
function setDefaultDates() {
    const now = new Date();
    const y = now.getFullYear();
    const m = String(now.getMonth() + 1).padStart(2, '0');
    const d = String(now.getDate()).padStart(2, '0');
    document.getElementById('fDateFrom').value = '';
    document.getElementById('fDateTo').value = `${y}-${m}-${d}`;
}

// ── Load All Code Mappings ─────────────────────────────────────────────
async function loadAllCodes() {
    try {
        const res = await fetch(`${pathBase}/Production/GetAllCodes`);
        const codes = await res.json();
        codes.forEach(c => { codeMap[c.value] = c.text; });
    } catch (e) { console.error('loadAllCodes', e); }
}

async function loadVendors() {
    try {
        const res = await fetch(`${pathBase}/Production/GetVendors`);
        const data = await res.json();
        const sel = document.getElementById('fVendor');
        data.forEach(v => {
            const o = new Option(v, v);
            sel.appendChild(o);
        });
    } catch (e) { console.error('loadVendors', e); }
}

async function loadProcesses() {
    try {
        const res = await fetch(`${pathBase}/Production/GetProcesses`);
        const data = await res.json();
        const sel = document.getElementById('fProcess');
        data.forEach(p => {
            const o = new Option(p, p);
            sel.appendChild(o);
        });
    } catch (e) { console.error('loadProcesses', e); }
}

// ── Filter toggle ──────────────────────────────────────────────────────
function toggleFilter() {
    const body = document.getElementById('filterBody');
    const icon = document.getElementById('filterToggle');
    body.classList.toggle('collapsed');
    icon.classList.toggle('collapsed');
}

function resetFilters() {
    document.getElementById('fProcess').value = '';
    document.getElementById('fProductId').value = '';
    document.getElementById('fVendor').value = '';
    document.getElementById('fDateFrom').value = '';
    const now = new Date();
    const y = now.getFullYear();
    const m = String(now.getMonth() + 1).padStart(2, '0');
    const d = String(now.getDate()).padStart(2, '0');
    document.getElementById('fDateTo').value = `${y}-${m}-${d}`;
    loadReport();
}

// ── Resolve operation text from product_id ─────────────────────────────
function getOperation(productId) {
    if (!productId) return '—';
    return codeMap[productId] || `ID:${productId}`;
}

// ── Load Report ────────────────────────────────────────────────────────
async function loadReport() {
    const params = new URLSearchParams();
    const vendor = document.getElementById('fVendor').value;
    const process = document.getElementById('fProcess').value;
    const productId = document.getElementById('fProductId').value;
    const dateFrom = document.getElementById('fDateFrom').value;
    const dateTo = document.getElementById('fDateTo').value;

    if (vendor) params.append('vendor', vendor);
    if (process) params.append('process', process);
    if (dateFrom) params.append('dateFrom', dateFrom);
    if (dateTo) params.append('dateTo', dateTo);

    showLoading(true);
    try {
        const res = await fetch(`${pathBase}/Production/GetHistory?` + params.toString());
        const data = await res.json();

        let filtered = data;
        if (productId) {
            filtered = data.filter(x => String(x.product_id) === productId);
        }

        renderStats(filtered);
        renderBarCharts(filtered);
        renderLineCharts(filtered);
        renderVendorDetail(filtered);
        refreshProductIdFilter(filtered);
    } catch (e) {
        console.error('loadReport', e);
        showToast(t('report.error.load'), 'error');
    } finally {
        showLoading(false);
    }
}

// ── Stats ──────────────────────────────────────────────────────────────
function renderStats(data) {
    const prodData = data.filter(x => x.type_value === 'Production Qty');
    const totalQty = prodData.reduce((s, x) => s + (x.product_qty || 0), 0);
    const vendors = new Set(data.map(x => x.vendor).filter(Boolean));
    const processes = new Set(data.map(x => x.process).filter(Boolean));

    document.getElementById('statTotalQty').textContent = totalQty.toLocaleString();
    document.getElementById('statRecords').textContent = data.length.toLocaleString();
    document.getElementById('statVendors').textContent = vendors.size;
    document.getElementById('statProcesses').textContent = processes.size;
}

// ── Populate operation filter ──────────────────────────────────────────
function refreshProductIdFilter(data) {
    const current = document.getElementById('fProductId').value;
    const ids = [...new Set(data.map(x => x.product_id).filter(Boolean))].sort((a, b) => a - b);
    const sel = document.getElementById('fProductId');
    sel.innerHTML = `<option value="">${t('report.all.operations')}</option>`;
    ids.forEach(id => {
        const label = codeMap[id] ? `${codeMap[id]}` : `ID:${id}`;
        const o = new Option(label, id);
        if (String(id) === current) o.selected = true;
        sel.appendChild(o);
    });
}

// ── Bar / Donut Charts ─────────────────────────────────────────────────
function renderBarCharts(data) {
    const prodData = data.filter(x => x.type_value === 'Production Qty');
    const byProcess = groupAndSum(prodData, 'process');
    const byVendor = groupAndSum(prodData, 'vendor');
    const byOperation = groupAndSumByOperation(prodData, 10);

    renderDoughnut('chartProcess', 'chartProcessEmpty', byProcess);
    renderDoughnut('chartVendor', 'chartVendorEmpty', byVendor);
    renderHBar('chartCode', 'chartCodeEmpty', byOperation);
}

function groupAndSum(data, field, limit = null) {
    const map = {};
    data.forEach(x => {
        const key = x[field] || '(unknown)';
        map[key] = (map[key] || 0) + (x.product_qty || 0);
    });
    let entries = Object.entries(map).sort((a, b) => b[1] - a[1]);
    if (limit) entries = entries.slice(0, limit);
    return { labels: entries.map(e => e[0]), values: entries.map(e => e[1]) };
}

function groupAndSumByOperation(data, limit = null) {
    const map = {};
    data.forEach(x => {
        const opName = codeMap[x.product_id] || (x.product_id ? `ID:${x.product_id}` : '(unknown)');
        map[opName] = (map[opName] || 0) + (x.product_qty || 0);
    });
    let entries = Object.entries(map).sort((a, b) => b[1] - a[1]);
    if (limit) entries = entries.slice(0, limit);
    return { labels: entries.map(e => e[0]), values: entries.map(e => e[1]) };
}

// ── Doughnut (Process & Vendor) — có datalabels ────────────────────────
function renderDoughnut(canvasId, emptyId, { labels, values }) {
    const canvas = document.getElementById(canvasId);
    const empty = document.getElementById(emptyId);

    if (chartProcess && canvasId === 'chartProcess') { chartProcess.destroy(); chartProcess = null; }
    if (chartVendor && canvasId === 'chartVendor') { chartVendor.destroy(); chartVendor = null; }

    if (!labels.length) {
        canvas.style.display = 'none';
        empty.style.display = 'flex';
        return;
    }
    canvas.style.display = '';
    empty.style.display = 'none';

    const total = values.reduce((a, b) => a + b, 0);
    const bgColors = COLORS.slice(0, labels.length);

    const chart = new Chart(canvas, {
        type: 'doughnut',
        data: {
            labels,
            datasets: [{ data: values, backgroundColor: bgColors, borderWidth: 2, borderColor: '#fff' }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: { font: { size: 11, family: 'DM Sans' }, boxWidth: 12, padding: 8 }
                },
                tooltip: {
                    callbacks: {
                        label: ctx => ` ${ctx.label}: ${ctx.raw.toLocaleString()} (${Math.round(ctx.raw / total * 100)}%)`
                    }
                },
                datalabels: {
                    color: '#fff',
                    textAlign: 'center',
                    font: { size: 11, weight: '700', family: 'DM Sans' },
                    formatter: (value) => {
                        const pct = Math.round(value / total * 100);
                        if (pct < 5) return '';
                        return value.toLocaleString() + '\n' + pct + '%';
                    }
                }
            },
            cutout: '55%'
        },
        plugins: [ChartDataLabels]
    });

    if (canvasId === 'chartProcess') chartProcess = chart;
    if (canvasId === 'chartVendor') chartVendor = chart;
}

// ── Horizontal Bar (Qty by Product) ───────────────────
function renderHBar(canvasId, emptyId, { labels, values }) {
    const canvas = document.getElementById(canvasId);
    const empty = document.getElementById(emptyId);
    if (chartCode) { chartCode.destroy(); chartCode = null; }

    if (!labels.length) {
        canvas.style.display = 'none';
        empty.style.display = 'flex';
        return;
    }
    canvas.style.display = '';
    empty.style.display = 'none';

    chartCode = new Chart(canvas, {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                data: values,
                backgroundColor: COLORS.slice(0, labels.length),
                borderRadius: 4,
                borderSkipped: false
            }]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            layout: { padding: { right: 55 } },
            plugins: {
                legend: { display: false },
                tooltip: { callbacks: { label: ctx => ` ${ctx.raw.toLocaleString()}` } },
                datalabels: {
                    anchor: 'end',
                    align: 'end',
                    color: '#1e293b',
                    font: { size: 10, weight: '700', family: 'DM Sans' },
                    formatter: value => value.toLocaleString()
                }
            },
            scales: {
                x: { grid: { color: '#f1f5f9' }, ticks: { font: { size: 10, family: 'DM Sans' } } },
                y: { grid: { display: false }, ticks: { font: { size: 10, family: 'DM Sans' }, maxRotation: 0 } }
            }
        },
        plugins: [ChartDataLabels]
    });
}

// ── Phần Line Charts ────────────────────────────────────────────────────────
function renderLineCharts(data) {
    const prodData = data.filter(x => x.type_value === 'Production Qty');
    renderAvgManPower(data);
    renderDailyLine('chartDailyProcess', 'chartDailyProcessEmpty', prodData, 'process');
    renderDailyLine('chartDailyVendor', 'chartDailyVendorEmpty', prodData, 'vendor');
}

// ── Phần Avg Man Power ─────────────────────────
function renderAvgManPower(data) {
    const canvas = document.getElementById('chartAvgManPower');
    const empty = document.getElementById('chartAvgManPowerEmpty');
    if (chartAvgManPower) { chartAvgManPower.destroy(); chartAvgManPower = null; }

    const manData = data.filter(x =>
        x.type_value && x.type_value.toLowerCase().includes('man') && x.product_qty > 0
    );

    if (!manData.length) {
        canvas.style.display = 'none';
        empty.style.display = 'flex';
        return;
    }
    canvas.style.display = '';
    empty.style.display = 'none';

    const map = {};
    manData.forEach(x => {
        const k = x.process || '(unknown)';
        if (!map[k]) map[k] = { sum: 0, count: 0 };
        map[k].sum += (x.product_qty || 0);
        map[k].count += 1;
    });

    const entries = Object.entries(map)
        .map(([k, v]) => ({ label: k, avg: Math.ceil(v.sum / v.count) }))
        .sort((a, b) => b.avg - a.avg);

    const labels = entries.map(e => e.label);
    const values = entries.map(e => e.avg);

    chartAvgManPower = new Chart(canvas, {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                label: "Avg Man Q'ty",
                data: values,
                backgroundColor: COLORS.slice(0, labels.length).map(c => c + 'cc'),
                borderColor: COLORS.slice(0, labels.length),
                borderWidth: 2,
                borderRadius: 6,
                borderSkipped: false
            }]
        },
        options: {
            responsive: true,
            layout: { padding: { top: 28 } },
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: ctx => ` Avg Man Power: ${ctx.raw.toLocaleString()}`
                    }
                },
                datalabels: {
                    anchor: 'end',
                    align: 'top',
                    color: '#1e293b',
                    font: { size: 14, weight: '700', family: 'DM Sans' },
                    formatter: value => value.toLocaleString()
                }
            },
            scales: {
                x: {
                    grid: { display: false },
                    ticks: { font: { size: 12, family: 'DM Sans', weight: '600' } }
                },
                y: {
                    grid: { color: '#f1f5f9' },
                    ticks: { font: { size: 11, family: 'DM Sans' }, stepSize: 1 },
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: "Avg Man Q'ty / record",
                        font: { size: 11, family: 'DM Sans' },
                        color: '#94a3b8'
                    }
                }
            }
        },
        plugins: [ChartDataLabels]
    });
}

// ── Phần Daily Line Charts ──────────────────────────
function renderDailyLine(canvasId, emptyId, data, groupField) {
    const canvas = document.getElementById(canvasId);
    const empty = document.getElementById(emptyId);

    if (canvasId === 'chartDailyProcess' && chartDailyProcess) { chartDailyProcess.destroy(); chartDailyProcess = null; }
    if (canvasId === 'chartDailyVendor' && chartDailyVendor) { chartDailyVendor.destroy(); chartDailyVendor = null; }

    if (!data.length) {
        canvas.style.display = 'none';
        empty.style.display = 'flex';
        return;
    }
    canvas.style.display = '';
    empty.style.display = 'none';

    const dateSet = new Set();
    const groupSet = new Set();
    data.forEach(x => {
        const d = x.date_finished ? x.date_finished.split('T')[0] : null;
        if (d) dateSet.add(d);
        if (x[groupField]) groupSet.add(x[groupField]);
    });

    const dates = [...dateSet].sort();
    const groups = [...groupSet].sort();

    const map = {};
    groups.forEach(g => { map[g] = {}; dates.forEach(d => { map[g][d] = 0; }); });
    data.forEach(x => {
        const d = x.date_finished ? x.date_finished.split('T')[0] : null;
        const g = x[groupField];
        if (d && g) map[g][d] = (map[g][d] || 0) + (x.product_qty || 0);
    });

    const datasets = groups.map((g, i) => ({
        label: g,
        data: dates.map(d => map[g][d]),
        borderColor: COLORS[i % COLORS.length],
        backgroundColor: COLORS[i % COLORS.length] + '22',
        tension: 0.35,
        fill: false,
        pointRadius: dates.length > 30 ? 2 : 4,
        pointHoverRadius: 6,
        borderWidth: 2
    }));

    const chart = new Chart(canvas, {
        type: 'line',
        data: { labels: dates, datasets },
        options: {
            responsive: true,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: {
                    position: 'top',
                    labels: { font: { size: 11, family: 'DM Sans' }, boxWidth: 14, padding: 10 }
                },
                tooltip: {
                    callbacks: {
                        label: ctx => ` ${ctx.dataset.label}: ${ctx.raw.toLocaleString()}`
                    }
                }
            },
            scales: {
                x: {
                    grid: { color: '#f1f5f9' },
                    ticks: { font: { size: 10, family: 'DM Sans' }, maxRotation: 45, maxTicksLimit: 20 }
                },
                y: {
                    grid: { color: '#f1f5f9' },
                    ticks: { font: { size: 10, family: 'DM Sans' } },
                    beginAtZero: true
                }
            }
        }
    });

    if (canvasId === 'chartDailyProcess') chartDailyProcess = chart;
    if (canvasId === 'chartDailyVendor') chartDailyVendor = chart;
}

// ── Phần Vendor Detail ──────────────────────────────────────────────────────
function renderVendorDetail(data) {
    const container = document.getElementById('vendorDetailContainer');
    const countEl = document.getElementById('detailCount');
    countEl.dataset.count = data.length;
    countEl.textContent = t('report.detail.records', { n: data.length });

    if (!data.length) {
        container.innerHTML = `
                        <div class="empty-state">
                            <div class="empty-icon">📭</div>
                            <p>${t('report.empty.hint')}</p>
                        </div>`;
        return;
    }

    const vendorMap = {};
    data.forEach(x => {
        const v = x.vendor || '(unknown)';
        if (!vendorMap[v]) vendorMap[v] = [];
        vendorMap[v].push(x);
    });

    let html = '';
    const vendors = Object.keys(vendorMap).sort();
    vendors.forEach((vendor, vi) => {
        const rows = vendorMap[vendor];
        const total = rows
            .filter(x => x.type_value === 'Production Qty')
            .reduce((s, x) => s + (x.product_qty || 0), 0);
        const isFirst = vi === 0;

        html += `
                    <div class="vendor-block">
                        <button class="vendor-accordion-btn ${isFirst ? 'open' : ''}"
                                onclick="toggleVendor(this, 'vendor-body-${vi}')">
                            <div class="vendor-title">
                                🏭 ${vendor}
                                <span class="vendor-badge">${rows.length}</span>
                            </div>
                            <div class="vendor-meta">
                                <span>Prod Qty: <strong class="vendor-qty">${total.toLocaleString()}</strong></span>
                                <span class="vendor-chevron ${isFirst ? 'open' : ''}">▼</span>
                            </div>
                        </button>
                        <div class="vendor-content ${isFirst ? 'open' : ''}" id="vendor-body-${vi}">
                            <div class="table-wrap">
                                <table>
                                    <thead>
                                        <tr>
                                            <th>${t('report.col.no')}</th>
                                            <th>${t('label.datetime')}</th>
                                            <th>${t('report.col.process')}</th>
                                            <th>${t('report.col.product')}</th>
                                            <th>${t('report.col.type')}</th>
                                            <th>${t('report.col.qty')}</th>
                                            <th>${t('report.col.desc')}</th>
                                        </tr>
                                    </thead>
                                    <tbody>`;

        rows.forEach((r, ri) => {
            const dt = r.date_finished ? new Date(r.date_finished).toLocaleString('vi-VN') : '—';
            const desc = r.description ? (r.description.length > 40 ? r.description.substring(0, 40) + '…' : r.description) : '—';
            const typeCls = (r.type_value || '').includes('Man') ? 'man' : '';
            const opName = getOperation(r.product_id);

            html += `<tr>
                            <td style="color:var(--gray-400)">${ri + 1}</td>
                            <td>${dt}</td>
                            <td><span class="badge badge-process">${r.process || '—'}</span></td>
                            <td><strong>${opName}</strong></td>
                            <td><span class="badge badge-type ${typeCls}">${r.type_value || '—'}</span></td>
                            <td><strong style="color:var(--denim)">${(r.product_qty || 0).toLocaleString()}</strong></td>
                            <td style="color:var(--gray-600)">${desc}</td>
                        </tr>`;
        });

        html += `</tbody></table></div></div></div>`;
    });

    container.innerHTML = html;
}

function toggleVendor(btn, bodyId) {
    const body = document.getElementById(bodyId);
    const chevron = btn.querySelector('.vendor-chevron');
    const isOpen = body.classList.contains('open');
    body.classList.toggle('open', !isOpen);
    btn.classList.toggle('open', !isOpen);
    chevron.classList.toggle('open', !isOpen);
}

// ── Nút Excel Export ───────────────────────────────────────────────────────
async function exportExcel() {
    const vendor = document.getElementById('fVendor').value;
    const process = document.getElementById('fProcess').value;
    const dateFrom = document.getElementById('fDateFrom').value;
    const dateTo = document.getElementById('fDateTo').value;
    const productId = document.getElementById('fProductId').value;

    const params = new URLSearchParams();
    if (vendor) params.append('vendor', vendor);
    if (process) params.append('process', process);
    if (dateFrom) params.append('dateFrom', dateFrom);
    if (dateTo) params.append('dateTo', dateTo);

    showLoading(true);
    try {
        const res = await fetch(`${pathBase}/Production/GetHistory?` + params.toString());
        let data = await res.json();
        if (productId) data = data.filter(x => String(x.product_id) === productId);

        if (!data.length) { showToast(t('report.export.nodata'), 'error'); return; }

        const wb = XLSX.utils.book_new();
        const prodData = data.filter(x => x.type_value === 'Production Qty');
        const now = new Date();

        const totalQty = prodData.reduce((s, x) => s + (x.product_qty || 0), 0);
        const vendors = [...new Set(data.map(x => x.vendor).filter(Boolean))];
        const processes = [...new Set(data.map(x => x.process).filter(Boolean))];

        const byProcessMap = {};
        prodData.forEach(x => {
            const k = x.process || '(unknown)';
            byProcessMap[k] = (byProcessMap[k] || 0) + (x.product_qty || 0);
        });
        const byVendorMap = {};
        prodData.forEach(x => {
            const k = x.vendor || '(unknown)';
            byVendorMap[k] = (byVendorMap[k] || 0) + (x.product_qty || 0);
        });
        const byOpMap = {};
        prodData.forEach(x => {
            const k = codeMap[x.product_id] || (x.product_id ? `ID:${x.product_id}` : '(unknown)');
            byOpMap[k] = (byOpMap[k] || 0) + (x.product_qty || 0);
        });

        const sortDesc = obj => Object.entries(obj).sort((a, b) => b[1] - a[1]);

        const summaryAOA = [
            ['PRODUCTION REPORT'],
            ['Generated at', now.toLocaleString('vi-VN')],
            ['Period From', dateFrom || '(All time)'],
            ['Period To', dateTo || now.toLocaleDateString('vi-VN')],
            [],
            ['=== SUMMARY STATISTICS (Production Qty only) ==='],
            ['Total Production Qty', totalQty],
            ['Total Records (all types)', data.length],
            ['  → Production Qty records', prodData.length],
            ['Active Vendors', vendors.length],
            ['Active Processes', processes.length],
            [],
            ['=== QTY BY PROCESS ==='],
            ['Process', 'Quantity'],
            ...sortDesc(byProcessMap).map(([k, v]) => [k, v]),
            [],
            ['=== QTY BY VENDOR ==='],
            ['Vendor', 'Quantity'],
            ...sortDesc(byVendorMap).map(([k, v]) => [k, v]),
            [],
            ['=== QTY BY OPERATION ==='],
            ['Operation', 'Quantity'],
            ...sortDesc(byOpMap).map(([k, v]) => [k, v]),
        ];
        const ws1 = XLSX.utils.aoa_to_sheet(summaryAOA);
        ws1['!cols'] = [{ wch: 35 }, { wch: 22 }];
        XLSX.utils.book_append_sheet(wb, ws1, 'Summary');

        const dateSet = new Set(prodData.map(x => x.date_finished ? x.date_finished.split('T')[0] : null).filter(Boolean));
        const procSet = new Set(prodData.map(x => x.process).filter(Boolean));
        const vendorSet = new Set(prodData.map(x => x.vendor).filter(Boolean));
        const dates = [...dateSet].sort();
        const procGrps = [...procSet].sort();
        const vendorGrps = [...vendorSet].sort();

        const procMx = {};
        procGrps.forEach(g => { procMx[g] = {}; dates.forEach(d => { procMx[g][d] = 0; }); });
        prodData.forEach(x => {
            const d = x.date_finished ? x.date_finished.split('T')[0] : null;
            if (d && x.process) procMx[x.process][d] = (procMx[x.process][d] || 0) + (x.product_qty || 0);
        });
        const vendMx = {};
        vendorGrps.forEach(g => { vendMx[g] = {}; dates.forEach(d => { vendMx[g][d] = 0; }); });
        prodData.forEach(x => {
            const d = x.date_finished ? x.date_finished.split('T')[0] : null;
            if (d && x.vendor) vendMx[x.vendor][d] = (vendMx[x.vendor][d] || 0) + (x.product_qty || 0);
        });

        const dailyAOA = [
            ['=== DAILY TREND BY PROCESS (Production Qty only) ==='],
            ['Date', ...procGrps],
            ...dates.map(d => [d, ...procGrps.map(g => procMx[g][d] || 0)]),
            [],
            ['=== DAILY TREND BY VENDOR (Production Qty only) ==='],
            ['Date', ...vendorGrps],
            ...dates.map(d => [d, ...vendorGrps.map(g => vendMx[g][d] || 0)]),
        ];
        const ws2 = XLSX.utils.aoa_to_sheet(dailyAOA);
        ws2['!cols'] = [{ wch: 14 }, ...Array(Math.max(procGrps.length, vendorGrps.length) + 1).fill({ wch: 14 })];
        XLSX.utils.book_append_sheet(wb, ws2, 'Daily Trend');

        const detailRows = data.map((x, i) => ({
            '#': i + 1,
            'Vendor': x.vendor || '',
            'Process': x.process || '',
            'Operation': getOperation(x.product_id),
            'Type': x.type_value || '',
            'Quantity': x.product_qty || 0,
            'Date': x.date_finished ? new Date(x.date_finished).toLocaleString('vi-VN') : '',
            'Description': x.description || ''
        }));
        const ws3 = XLSX.utils.json_to_sheet(detailRows);
        ws3['!cols'] = [{ wch: 5 }, { wch: 14 }, { wch: 18 }, { wch: 28 }, { wch: 20 }, { wch: 12 }, { wch: 22 }, { wch: 40 }];
        XLSX.utils.book_append_sheet(wb, ws3, 'Detail Records');

        const fname = `Production_Report_${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}${String(now.getDate()).padStart(2, '0')}.xlsx`;
        XLSX.writeFile(wb, fname);
        showToast(t('report.export.success', { n: data.length, p: prodData.length }), 'success');
    } catch (e) {
        console.error('exportExcel', e);
        showToast(t('report.export.fail'), 'error');
    } finally {
        showLoading(false);
    }
}

// ── Re-render dynamic content on language change ───────────────────────
document.addEventListener('langChanged', () => {
    // Re-render vendor detail table headers
    const container = document.getElementById('vendorDetailContainer');
    if (container && container.querySelector('table')) {
        // Trigger a soft re-render by reloading report data
        loadReport();
    }
    // Update detailCount label
    const countEl = document.getElementById('detailCount');
    if (countEl) {
        const n = parseInt(countEl.dataset.count || '0');
        countEl.textContent = t('report.detail.records', { n });
    }
});

// ── Helpers ────────────────────────────────────────────────────────────
function showLoading(show) {
    document.getElementById('loadingOverlay').classList.toggle('show', show);
}

function showToast(msg, type) {
    const toast = document.getElementById('toast');
    toast.className = 'toast ' + type;
    document.getElementById('toastIcon').textContent = type === 'success' ? '✅' : '❌';
    document.getElementById('toastMsg').textContent = msg;
    toast.classList.add('show');
    setTimeout(() => toast.classList.remove('show'), 3500);
}