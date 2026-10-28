/**
 * In tem - /twist/admin/print
 * Nhập WO -> chọn mẫu tem -> điền biến {KEY} -> xem trước -> in.
 * Phương thức in: Server TCP (server gửi ZPL), TCP qua Node / USB qua Node (trình duyệt gửi ZPL tới agent localhost:8021).
 */
(function () {
  'use strict';

  const cfg = window.LabelPrintPage;
  const MAX_COPIES = 100;
  const AGENT_TIMEOUT_MS = 8000;
  const CONFIG_STORAGE_KEY = 'twistAdmin.print.config';
  const IPV4 = /^((25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(25[0-5]|2[0-4]\d|1?\d?\d)$/;
  const notyf = new Notyf({ duration: 3000, position: { x: 'right', y: 'top' }, dismissible: true });
  const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

  const MODE_LABEL = { server: 'Server TCP', node: 'TCP qua Node', usb: 'USB qua Node' };
  const SOURCE_BADGE = {
    wo: ['bg-label-success', 'Từ WO'],
    sku: ['bg-label-primary', 'Từ SKU'],
    po_eu: ['bg-label-info', 'PO EU'],
    system: ['bg-label-secondary', 'Hệ thống'],
    manual: ['bg-label-warning', 'Nhập tay']
  };

  const $ = function (id) { return document.getElementById(id); };
  const el = {
    woForm: $('lp-wo-form'), wo: $('lp-wo'), woSearch: $('lp-wo-search'), woInfo: $('lp-wo-info'), woHint: $('lp-wo-hint'),
    tplToggle: $('lp-tpl-toggle-search'), tplSearchGroup: $('lp-tpl-search-group'), tplSearch: $('lp-tpl-search'), tplList: $('lp-tpl-list'),
    refill: $('lp-refill'), vars: $('lp-vars'),
    printerSummary: $('lp-printer-summary'), configOpen: $('lp-config-open'),
    copies: $('lp-copies'), copiesMinus: $('lp-copies-minus'), copiesPlus: $('lp-copies-plus'),
    print: $('lp-print'), hint: $('lp-hint'), result: $('lp-result'),
    modal: $('lp-config-modal'), modes: document.querySelectorAll('#lp-config-modal [data-mode]'),
    agent: $('lp-agent'), agentUrl: $('lp-agent-url'), agentStatus: $('lp-agent-status'),
    tcpBlock: $('lp-tcp-block'), srcList: $('lp-src-list'), srcManual: $('lp-src-manual'),
    srcListBlock: $('lp-src-list-block'), srcManualBlock: $('lp-src-manual-block'),
    printer: $('lp-printer'), ip: $('lp-ip'), port: $('lp-port'),
    usbBlock: $('lp-usb-block'), usb: $('lp-usb'), usbRefresh: $('lp-usb-refresh'), usbStatus: $('lp-usb-status'),
    testBlock: $('lp-test-block'), test: $('lp-test'), testResult: $('lp-test-result'),
    configSave: $('lp-config-save')
  };
  const configModal = new bootstrap.Modal(el.modal);

  const state = {
    wo: null,            // thông tin WO đang chọn (null = in không theo WO)
    template: null,      // mẫu tem đang chọn (có zplData)
    variables: [],       // [{ key, label, source, value, optional }]
    config: loadConfig(),
    draftMode: 'server', // phương thức đang chọn trong modal (chưa lưu)
    printing: false
  };

  // ===== API =====

  async function api(url, options) {
    const res = await fetch(url, Object.assign({
      headers: {
        'Content-Type': 'application/json',
        'X-Requested-With': 'XMLHttpRequest',
        'RequestVerificationToken': token
      }
    }, options));
    if (res.status === 401) {
      window.location.href = cfg.loginUrl + '?returnUrl=' + encodeURIComponent(window.location.pathname);
      throw new Error('Phiên đăng nhập đã hết hạn.');
    }
    if (!res.ok) throw new Error('Lỗi máy chủ (' + res.status + ').');
    return res.json();
  }

  async function agent(path, options) {
    let res;
    try {
      res = await fetch(cfg.agentUrl + path, Object.assign({ signal: AbortSignal.timeout(AGENT_TIMEOUT_MS) }, options));
    } catch (err) {
      throw new Error('Agent ' + cfg.agentUrl + ' đang offline. Chế độ TCP/USB qua Node cần chạy agent trên máy này, hoặc chuyển sang Server TCP.');
    }
    const data = await res.json().catch(function () { return {}; });
    if (!res.ok || data.success === false) throw new Error(data.error || 'Agent trả về lỗi (' + res.status + ').');
    return data;
  }

  // ===== 1. WO =====

  async function searchWo(e) {
    e.preventDefault();
    const code = el.wo.value.trim();
    if (!code) { el.wo.focus(); return; }
    setBusy(el.woSearch, true, 'Đang tìm...');
    try {
      const res = await api(cfg.apiUrl + '/wo?wo=' + encodeURIComponent(code), { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      state.wo = res.wo;
      renderWoInfo();
      el.tplSearchGroup.classList.add('d-none');
      el.tplToggle.classList.remove('d-none');
      renderTemplates(res.templates, 'Sản phẩm ' + res.wo.product_code + ' chưa có mẫu tem. Tạo ở màn Mẫu tem.');
      // Đang chọn sẵn 1 mẫu của đúng sản phẩm này (VD mở từ màn Mẫu tem) thì giữ nguyên, chỉ điền lại biến theo WO
      const keep = state.template && res.templates.find(function (t) { return t.id === state.template.id; });
      if (keep) selectTemplate(keep.id);
      else if (res.templates.length) selectTemplate(res.templates[0].id);
      else clearTemplate();
    } catch (err) {
      notyf.error(err.message);
    } finally {
      setBusy(el.woSearch, false, '<i class="icon-base ti tabler-search me-1"></i>Tìm');
    }
  }

  function renderWoInfo() {
    const w = state.wo;
    el.woHint.classList.toggle('d-none', !!w);
    el.woInfo.classList.toggle('d-none', !w);
    el.refill.classList.toggle('d-none', !w || !state.template);
    if (!w) { el.woInfo.innerHTML = ''; return; }
    el.woInfo.innerHTML =
      row('Mã WO', '<span class="fw-medium">' + esc(w.name) + '</span>') +
      row('Sản phẩm', '<span class="fw-medium">' + esc(w.product_code) + '</span> · ' + esc(w.product_name)) +
      row('Số lượng WO', esc(w.product_qty)) +
      row('Chứng từ gốc', esc(w.origin) || '<span class="text-muted">—</span>');
    function row(label, value) { return '<dt>' + label + '</dt><dd>' + value + '</dd>'; }
  }

  // ===== 2. Mẫu tem =====

  let searchTimer = null;
  function searchTemplatesDebounced() {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(searchTemplates, 300);
  }

  async function searchTemplates() {
    const q = el.tplSearch.value.trim();
    if (!q) { renderTemplates([], null); return; }
    try {
      const res = await api(cfg.apiUrl + '/templates?q=' + encodeURIComponent(q), { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      renderTemplates(res.templates, 'Không tìm thấy mẫu tem nào khớp "' + q + '"');
    } catch (err) {
      notyf.error(err.message);
    }
  }

  function renderTemplates(list, emptyText) {
    if (!list.length) {
      el.tplList.innerHTML = '<div class="lp-empty">' + esc(emptyText || 'Nhập WO hoặc tìm theo part number để hiện các mẫu tem') + '</div>';
      return;
    }
    const selectedId = state.template && state.template.id;
    el.tplList.innerHTML = list.map(function (t) {
      const typeClass = t.type === 'Carton' ? 'bg-label-info' : 'bg-label-primary';
      return '<label class="lp-tpl' + (t.id === selectedId ? ' is-selected' : '') + '">' +
        '<input class="form-check-input mt-0" type="radio" name="lp-tpl" value="' + t.id + '"' + (t.id === selectedId ? ' checked' : '') + ' />' +
        '<span class="flex-grow-1"><span class="fw-medium text-heading">' + esc(t.partNumber) + '</span>' +
        ' <span class="badge ' + typeClass + ' ms-1">' + esc(t.type) + '</span>' +
        ' <span class="text-muted small ms-1">' + esc(t.operation) + (t.size ? ' · ' + esc(t.size) : '') + '</span></span>' +
        '</label>';
    }).join('');
  }

  async function selectTemplate(id) {
    el.tplList.querySelectorAll('.lp-tpl').forEach(function (l) {
      const input = l.querySelector('input');
      const on = parseInt(input.value, 10) === id;
      input.checked = on;
      l.classList.toggle('is-selected', on);
    });
    try {
      const res = await api(cfg.apiUrl + '/resolve', { method: 'POST', body: JSON.stringify({ templateId: id, wo: state.wo }) });
      if (!res.success) throw new Error(res.error);
      state.template = res.template;
      state.variables = res.variables;
      renderVariables();
      el.refill.classList.toggle('d-none', !state.wo);
      clearResult();
      preview.reset();
      preview.refresh();
      updatePrintState();
    } catch (err) {
      notyf.error(err.message);
    }
  }

  function clearTemplate() {
    state.template = null;
    state.variables = [];
    renderVariables();
    preview.refresh();
    updatePrintState();
  }

  // ===== 3. Biến =====

  function renderVariables() {
    if (!state.template) {
      el.vars.innerHTML = '<div class="lp-empty">Chọn mẫu tem để hiện các biến cần điền</div>';
      return;
    }
    if (!state.variables.length) {
      el.vars.innerHTML = '<div class="lp-empty">Mẫu tem này không có biến {...}, có thể in ngay</div>';
      return;
    }
    el.vars.innerHTML = state.variables.map(function (v, i) {
      const badge = SOURCE_BADGE[v.source] || SOURCE_BADGE.manual;
      const id = 'lp-var-' + i;
      return '<div>' +
        '<div class="d-flex justify-content-between align-items-center mb-1 gap-2">' +
        '<label class="form-label mb-0" for="' + id + '">' + esc(v.label) + ' <code class="ms-1">{' + esc(v.key) + '}</code>' +
        (v.optional ? '' : ' <span class="text-danger">*</span>') + '</label>' +
        '<span class="badge ' + badge[0] + '">' + badge[1] + '</span></div>' +
        '<input type="text" class="form-control" id="' + id + '" data-index="' + i + '" value="' + esc(v.value) + '"' +
        (v.source === 'manual' ? ' placeholder="Nhập ' + esc(v.label.toLowerCase()) + '"' : '') + ' />' +
        '<div class="invalid-feedback">Bắt buộc nhập trước khi in</div>' +
        '</div>';
    }).join('');
  }

  function onVariableInput(e) {
    const i = e.target.dataset.index;
    if (i === undefined) return;
    state.variables[i].value = e.target.value;
    e.target.classList.remove('is-invalid');
    clearResult();
    updatePrintState();
    preview.refreshDebounced();
  }

  function values() {
    const map = {};
    state.variables.forEach(function (v) { map[v.key] = v.value; });
    return map;
  }

  function missingVariables() {
    return state.variables.filter(function (v) { return !v.optional && !String(v.value || '').trim(); });
  }

  // ZPL đã điền để xem trước (biến chưa nhập giữ nguyên {KEY}). Khi in, server tự điền lại từ ZPL trong DB.
  function filledZpl() {
    if (!state.template) return '';
    const map = {};
    state.variables.forEach(function (v) { if (String(v.value || '').trim()) map[v.key.toLowerCase()] = v.value.trim(); });
    return (state.template.zplData || '').replace(/\{([A-Za-z_][A-Za-z0-9_]*)\}/g, function (m, key) {
      return Object.prototype.hasOwnProperty.call(map, key.toLowerCase()) ? map[key.toLowerCase()] : m;
    });
  }

  // ===== 4. Xem trước (wwwroot/js/twist-admin/zpl-preview.js) =====

  const preview = TwistZplPreview.create('lp-preview', {
    previewUrl: cfg.previewUrl,
    request: api,
    getSource: function () {
      const t = state.template || {};
      return { zplData: filledZpl(), size: t.size, fileName: [t.partNumber, t.type, t.operation].filter(Boolean).join('_').replace(/[^\w.-]+/g, '-') || 'label' };
    }
  });

  // ===== 5. Cấu hình máy in =====

  function loadConfig() {
    const fallback = { mode: 'server', source: 'list', printerId: cfg.printers.length ? cfg.printers[0].id : '', ip: '', port: 9100, usbName: '' };
    try {
      const saved = JSON.parse(localStorage.getItem(CONFIG_STORAGE_KEY) || 'null');
      return saved ? Object.assign(fallback, saved) : fallback;
    } catch (e) {
      return fallback;
    }
  }

  function saveConfig() {
    try { localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(state.config)); } catch (e) { /* trình duyệt chặn: chỉ dùng trong phiên này */ }
  }

  // Máy in thực sự dùng khi in: tra lại trong danh sách mới nhất từ server (không dùng IP cũ lưu trên trình duyệt)
  function currentPrinter() {
    const c = state.config;
    if (c.mode === 'usb') return c.usbName ? { name: c.usbName, detail: 'USB trên máy này' } : null;
    if (c.source === 'manual') return c.ip ? { name: 'Máy in nhập tay', ip: c.ip, port: c.port, detail: c.ip + ':' + c.port } : null;
    const p = cfg.printers.find(function (x) { return x.id === c.printerId; });
    return p ? { id: p.id, name: p.name, ip: p.ip, port: p.port, detail: p.ip + ':' + p.port } : null;
  }

  function renderPrinterSummary() {
    const p = currentPrinter();
    el.printerSummary.innerHTML = p
      ? '<div class="fw-medium text-heading text-truncate">' + esc(p.name) + '</div>' +
        '<div class="small text-muted">' + esc(p.detail) + ' <span class="badge bg-label-secondary ms-1">' + MODE_LABEL[state.config.mode] + '</span></div>'
      : '<div class="fw-medium text-heading">Chưa chọn máy in</div><div class="small text-muted">Bấm Đổi cấu hình để chọn</div>';
    updatePrintState();
  }

  function openConfig() {
    const c = state.config;
    state.draftMode = c.mode;
    (c.source === 'manual' ? el.srcManual : el.srcList).checked = true;
    if (c.printerId) el.printer.value = c.printerId;
    el.ip.value = c.ip || '';
    el.port.value = c.port || 9100;
    if (c.usbName && !Array.from(el.usb.options).some(function (o) { return o.value === c.usbName; })) {
      el.usb.add(new Option(c.usbName, c.usbName));
    }
    el.usb.value = c.usbName || '';
    el.testResult.textContent = '';
    renderModeUi();
    configModal.show();
  }

  function renderModeUi() {
    const mode = state.draftMode;
    el.modes.forEach(function (b) {
      const on = b.dataset.mode === mode;
      b.className = 'btn ' + (on ? 'btn-primary' : 'btn-label-secondary');
      b.setAttribute('aria-checked', String(on));
    });
    const manual = el.srcManual.checked;
    el.agent.classList.toggle('d-none', mode === 'server');
    el.tcpBlock.classList.toggle('d-none', mode === 'usb');
    el.usbBlock.classList.toggle('d-none', mode !== 'usb');
    el.srcListBlock.classList.toggle('d-none', manual);
    el.srcManualBlock.classList.toggle('d-none', !manual);
    // Kiểm tra kết nối chạy từ server, chỉ có ý nghĩa với Server TCP
    el.testBlock.classList.toggle('d-none', mode !== 'server');
    if (mode !== 'server') checkAgent();
    if (mode === 'usb' && el.usb.options.length <= 1) loadUsbPrinters();
  }

  async function checkAgent() {
    el.agentUrl.textContent = cfg.agentUrl.replace(/^https?:\/\//, '');
    el.agentStatus.className = 'badge bg-label-secondary';
    el.agentStatus.textContent = 'Đang kiểm tra...';
    try {
      await agent('/health', { method: 'GET' });
      el.agentStatus.className = 'badge bg-label-success';
      el.agentStatus.textContent = 'Online';
    } catch (err) {
      el.agentStatus.className = 'badge bg-label-danger';
      el.agentStatus.textContent = 'Offline';
    }
  }

  async function loadUsbPrinters() {
    el.usbStatus.textContent = 'Đang tìm máy in...';
    try {
      const data = await agent('/usb-printers', { method: 'GET' });
      const list = data.printers || [];
      const current = el.usb.value || state.config.usbName;
      el.usb.innerHTML = '<option value="">-- Chọn máy in USB --</option>' +
        list.map(function (n) { return '<option value="' + esc(n) + '">' + esc(n) + '</option>'; }).join('');
      if (current && list.indexOf(current) >= 0) el.usb.value = current;
      el.usbStatus.textContent = list.length ? 'Tìm thấy ' + list.length + ' máy in' : 'Không tìm thấy máy in nào trên máy này';
    } catch (err) {
      el.usbStatus.textContent = err.message;
    }
  }

  async function testConnection() {
    const target = readDraftTarget();
    if (target.error) { el.testResult.className = 'small text-danger'; el.testResult.textContent = target.error; return; }
    el.testResult.className = 'small text-muted';
    el.testResult.textContent = 'Đang kiểm tra...';
    try {
      const res = await api(cfg.apiUrl + '/test-connection', { method: 'POST', body: JSON.stringify(target.body) });
      el.testResult.className = 'small ' + (res.success ? 'text-success' : 'text-danger');
      el.testResult.innerHTML = res.success
        ? '<i class="icon-base ti tabler-circle-check me-1"></i>Kết nối được · ' + res.elapsedMs + ' ms'
        : '<i class="icon-base ti tabler-circle-x me-1"></i>' + esc(res.error);
    } catch (err) {
      el.testResult.className = 'small text-danger';
      el.testResult.textContent = err.message;
    }
  }

  // Đọc máy in TCP đang chọn trong modal: { body: { printerId } | { ip, port } } hoặc { error }
  function readDraftTarget() {
    if (el.srcManual.checked) {
      const ip = el.ip.value.trim(), port = parseInt(el.port.value, 10);
      if (!IPV4.test(ip)) return { error: 'Địa chỉ IP không hợp lệ.' };
      if (!(port >= 1 && port <= 65535)) return { error: 'Port phải từ 1 đến 65535.' };
      return { body: { ip: ip, port: port } };
    }
    if (!el.printer.value) return { error: 'Chưa có máy in trong danh sách.' };
    return { body: { printerId: el.printer.value } };
  }

  function saveDraftConfig() {
    const mode = state.draftMode;
    const next = Object.assign({}, state.config, { mode: mode });
    if (mode === 'usb') {
      if (!el.usb.value) { notyf.error('Vui lòng chọn máy in USB'); return; }
      next.usbName = el.usb.value;
    } else {
      const target = readDraftTarget();
      if (target.error) { notyf.error(target.error); return; }
      next.source = el.srcManual.checked ? 'manual' : 'list';
      if (next.source === 'manual') { next.ip = target.body.ip; next.port = target.body.port; }
      else next.printerId = target.body.printerId;
    }
    state.config = next;
    saveConfig();
    renderPrinterSummary();
    clearResult();
    configModal.hide();
  }

  // ===== In =====

  function copies() {
    const n = parseInt(el.copies.value, 10);
    return Math.min(Math.max(isNaN(n) ? 1 : n, 1), MAX_COPIES);
  }

  function setCopies(n) {
    el.copies.value = Math.min(Math.max(n, 1), MAX_COPIES);
  }

  function updatePrintState() {
    const missing = missingVariables();
    let hint = '';
    if (!state.template) hint = 'Chọn mẫu tem trước khi in.';
    else if (missing.length) hint = 'Còn ' + missing.length + ' biến chưa nhập: ' + missing.map(function (v) { return v.label; }).join(', ') + '.';
    else if (!currentPrinter()) hint = 'Chưa chọn máy in.';

    el.print.disabled = !!hint || state.printing;
    if (!hint) {
      el.hint.className = 'd-none';
      el.hint.innerHTML = '';
      return;
    }
    // Thiếu biến: cảnh báo vàng. Chưa chọn mẫu / máy in: dòng nhắc nhẹ.
    el.hint.className = state.template && missing.length ? 'alert alert-warning mb-0 py-2 small' : 'small text-muted';
    el.hint.innerHTML = '<i class="icon-base ti tabler-alert-triangle me-1"></i>' + esc(hint);
  }

  async function print() {
    const missing = missingVariables();
    if (missing.length) {
      el.vars.querySelectorAll('input[data-index]').forEach(function (input) {
        const v = state.variables[input.dataset.index];
        input.classList.toggle('is-invalid', !v.optional && !String(v.value || '').trim());
      });
      return;
    }
    const printer = currentPrinter();
    if (!state.template || !printer) return;

    state.printing = true;
    updatePrintState();
    setBusy(el.print, true, 'Đang gửi lệnh in...');
    clearResult();
    const n = copies();
    const started = performance.now();
    try {
      const mode = state.config.mode;
      let message, detail;
      if (mode === 'server') {
        const body = { templateId: state.template.id, values: values(), copies: n };
        if (printer.id) body.printerId = printer.id; else { body.ip = printer.ip; body.port = printer.port; }
        const res = await api(cfg.apiUrl + '/print', { method: 'POST', body: JSON.stringify(body) });
        if (!res.success) throw new Error(res.error);
        message = res.message;
        detail = MODE_LABEL.server + ' · ' + res.bytes.toLocaleString('vi-VN') + ' bytes · ' + res.elapsedMs + ' ms';
      } else {
        // Node: server điền biến từ ZPL trong DB, trình duyệt gửi tới agent trên máy này
        const res = await api(cfg.apiUrl + '/render', { method: 'POST', body: JSON.stringify({ templateId: state.template.id, values: values() }) });
        if (!res.success) throw new Error(res.error);
        const zpl = res.zpl.repeat(n);
        if (mode === 'usb') await agent('/print-usb', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ zpl: zpl, printerName: printer.name }) });
        else await agent('/print', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ zpl: zpl, printerIp: printer.ip, printerPort: printer.port }) });
        message = 'Đã gửi ' + n + ' bản tới ' + printer.name + (printer.detail && mode !== 'usb' ? ' (' + printer.detail + ')' : '');
        detail = MODE_LABEL[mode] + ' · ' + Math.round(performance.now() - started) + ' ms';
      }
      showResult('success', 'tabler-circle-check', message, detail + ' · ' + new Date().toLocaleTimeString('vi-VN'));
      notyf.success('Đã gửi lệnh in');
    } catch (err) {
      showResult('danger', 'tabler-circle-x', 'Không in được', err.message);
    } finally {
      state.printing = false;
      setBusy(el.print, false, '<i class="icon-base ti tabler-printer me-2"></i>In tem');
      updatePrintState();
    }
  }

  function showResult(type, icon, title, detail) {
    el.result.innerHTML = '<div class="alert alert-' + type + ' mb-0 d-flex gap-2" role="' + (type === 'success' ? 'status' : 'alert') + '">' +
      '<i class="icon-base ti ' + icon + ' flex-shrink-0 mt-1"></i><div><div class="fw-medium">' + esc(title) + '</div>' +
      '<div class="small">' + esc(detail) + '</div></div></div>';
  }

  function clearResult() { el.result.innerHTML = ''; }

  // ===== Tiện ích =====

  function setBusy(btn, busy, html) {
    btn.disabled = busy;
    btn.innerHTML = busy ? '<span class="spinner-border spinner-border-sm me-1"></span>' + html : html;
  }

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  // ===== Sự kiện =====

  el.woForm.addEventListener('submit', searchWo);
  el.tplSearch.addEventListener('input', searchTemplatesDebounced);
  el.tplToggle.addEventListener('click', function () {
    el.tplSearchGroup.classList.remove('d-none');
    el.tplSearch.focus();
  });
  el.tplList.addEventListener('change', function (e) {
    if (e.target.name === 'lp-tpl') selectTemplate(parseInt(e.target.value, 10));
  });
  el.refill.addEventListener('click', function () { if (state.template) selectTemplate(state.template.id); });
  el.vars.addEventListener('input', onVariableInput);

  el.copiesMinus.addEventListener('click', function () { setCopies(copies() - 1); });
  el.copiesPlus.addEventListener('click', function () { setCopies(copies() + 1); });
  el.copies.addEventListener('change', function () { setCopies(copies()); });
  el.print.addEventListener('click', print);

  el.configOpen.addEventListener('click', openConfig);
  el.modes.forEach(function (b) {
    b.addEventListener('click', function () { state.draftMode = b.dataset.mode; renderModeUi(); });
  });
  [el.srcList, el.srcManual].forEach(function (r) { r.addEventListener('change', renderModeUi); });
  el.usbRefresh.addEventListener('click', loadUsbPrinters);
  el.test.addEventListener('click', testConnection);
  el.configSave.addEventListener('click', saveDraftConfig);

  // Mở từ màn Mẫu tem (?templateId=...): chọn sẵn mẫu đó và liệt kê các mẫu cùng part number
  async function openTemplateFromLink(id) {
    await selectTemplate(id);
    if (!state.template) return;
    el.tplSearch.value = state.template.partNumber;
    await searchTemplates();
  }

  renderPrinterSummary();
  const linkedTemplateId = parseInt(new URLSearchParams(window.location.search).get('templateId'), 10);
  if (linkedTemplateId > 0) openTemplateFromLink(linkedTemplateId);
  el.wo.focus();
})();
