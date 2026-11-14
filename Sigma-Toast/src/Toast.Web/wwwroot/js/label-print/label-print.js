/**
 * In tem - /toast/admin/print (chuyển từ Twist: SVN-Portal/wwwroot/js/twist-admin/label-print.js)
 * Nhập WO -> chọn mẫu tem -> điền biến {KEY} -> xem trước -> in.
 * Phương thức in: Server TCP (server gửi ZPL), TCP qua Node / USB qua Node (trình duyệt gửi ZPL tới agent localhost:8021).
 * Modal Cấu hình máy in dùng chung với màn công nhân: Shared/_PrinterConfigModal.cshtml + js/shared/printer-config.js.
 */
(function () {
  'use strict';

  const cfg = window.LabelPrintPage;
  const MAX_COPIES = 100;
  // DPI lưu trong mẫu tem -> dots/mm cho khung xem trước
  const DPMM_BY_DPI = { 152: 6, 203: 8, 300: 12, 600: 24 };
  const notyf = new Notyf({ duration: 3000, position: { x: 'right', y: 'top' }, dismissible: true });
  const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

  const MODE_LABEL = window.ToastPrinterConfig.MODE_LABEL;
  const SOURCE_BADGE = {
    product: ['bg-label-info', 'Từ sản phẩm'],
    template: ['bg-label-primary', 'Từ mẫu tem'],
    'package': ['bg-label-info', 'Từ box'],
    wo: ['bg-label-success', 'Từ WO'],
    system: ['bg-label-secondary', 'Hệ thống'],
    manual: ['bg-label-warning', 'Nhập tay']
  };

  const $ = function (id) { return document.getElementById(id); };
  const el = {
    woForm: $('lp-wo-form'), wo: $('lp-wo'), woSearch: $('lp-wo-search'), woClear: $('lp-wo-clear'), woInfo: $('lp-wo-info'), woWarn: $('lp-wo-warn'),
    tplToggle: $('lp-tpl-toggle-search'), tplSearchGroup: $('lp-tpl-search-group'), tplSearch: $('lp-tpl-search'), tplList: $('lp-tpl-list'),
    refill: $('lp-refill'), vars: $('lp-vars'),
    printerSummary: $('lp-printer-summary'), configOpen: $('lp-config-open'),
    copies: $('lp-copies'), copiesMinus: $('lp-copies-minus'), copiesPlus: $('lp-copies-plus'),
    print: $('lp-print'), hint: $('lp-hint'), result: $('lp-result')
  };

  const state = {
    wo: null,            // thông tin WO đang chọn (null = in không theo WO): gồm cả mã khách, đơn bán, PO, Lot
    woWarnings: [],      // cảnh báo khi đọc WO (backorder, lệnh đã xong, PO / Lot chọn theo số lượng...)
    template: null,      // mẫu tem đang chọn (có zplData)
    variables: [],       // [{ key, label, source, value }]
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
      state.woWarnings = res.warnings || [];
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

  // Bỏ chọn WO: in không theo WO. Giữ mẫu tem đang chọn, điền lại biến (biến lấy từ WO thành trống, biến nhập tay giữ nguyên)
  function clearWo() {
    state.wo = null;
    state.woWarnings = [];
    el.wo.value = '';
    renderWoInfo();
    el.tplSearchGroup.classList.remove('d-none');
    el.tplToggle.classList.add('d-none');
    if (state.template) selectTemplate(state.template.id);
    el.wo.focus();
  }

  function renderWoInfo() {
    const w = state.wo;
    el.woInfo.classList.toggle('d-none', !w);
    el.refill.classList.toggle('d-none', !w || !state.template);
    el.woClear.disabled = !w;
    const warnings = w ? state.woWarnings : [];
    el.woWarn.classList.toggle('d-none', !warnings.length);
    el.woWarn.innerHTML = warnings.map(esc).join('<br>');
    if (!w) { el.woInfo.innerHTML = ''; return; }
    el.woInfo.innerHTML =
      row('Mã WO', '<span class="fw-medium">' + esc(w.name) + '</span>') +
      row('Sản phẩm', '<span class="fw-medium">' + esc(w.product_code) + '</span> · ' + esc(w.product_name)) +
      row('Số lượng WO', esc(w.product_qty)) +
      row('Chứng từ gốc', orDash(w.origin)) +
      row('Đơn bán', orDash(w.sale_order)) +
      row('PO', orDash(w.po)) +
      row('Lot ID', orDash(w.lot_id)) +
      row('Mã khách', orDash(w.customer_part_number));
    function row(label, value) { return '<dt>' + label + '</dt><dd>' + value + '</dd>'; }
    function orDash(v) { return esc(v) || '<span class="text-muted">—</span>'; }
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
      el.tplList.innerHTML = '<div class="lp-empty">' + esc(emptyText || 'Nhập WO hoặc tìm theo mã sản phẩm để hiện các mẫu tem') + '</div>';
      return;
    }
    const selectedId = state.template && state.template.id;
    el.tplList.innerHTML = list.map(function (t) {
      const typeClass = t.type === 'Carton' ? 'bg-label-info' : t.type === 'Plate' ? 'bg-label-warning' : 'bg-label-primary';
      return '<label class="lp-tpl' + (t.id === selectedId ? ' is-selected' : '') + '">' +
        '<input class="form-check-input mt-0" type="radio" name="lp-tpl" value="' + t.id + '"' + (t.id === selectedId ? ' checked' : '') + ' />' +
        '<span class="flex-grow-1"><span class="fw-medium text-heading">' + esc(t.defaultCode) + '</span>' +
        ' <span class="badge ' + typeClass + ' ms-1">' + esc(t.type) + '</span>' +
        ' <span class="text-muted small ms-1">' + esc(t.operation) + (t.size ? ' · ' + esc(t.size) : '') + (t.dpi ? ' · ' + t.dpi + ' dpi' : '') + '</span></span>' +
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
      // Biến tự điền (mẫu / WO / sản phẩm / hệ thống) lấy lại theo WO hiện tại; biến nhập tay giữ giá trị đã gõ (theo tên biến)
      const typed = {};
      state.variables.forEach(function (v) { if (v.source === 'manual' && v.value) typed[v.key.toUpperCase()] = v.value; });
      res.variables.forEach(function (v) {
        if (v.source === 'manual' && !v.value && typed[v.key.toUpperCase()]) v.value = typed[v.key.toUpperCase()];
      });
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
        (v.optional ? ' <span class="text-muted small">(tuỳ chọn)</span>' : ' <span class="text-danger">*</span>') + '</label>' +
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

  // Biến bắt buộc phải có giá trị trước khi in (biến tuỳ chọn được để trống)
  function missingVariables() {
    return state.variables.filter(function (v) { return !v.optional && !String(v.value || '').trim(); });
  }

  // ===== 4. Xem trước (wwwroot/js/label-template/zpl-preview.js) =====
  // Server thay các biến đã nhập vào ZPL khi vẽ, biến chưa nhập giữ nguyên {KEY}. Khi in, server lấy lại ZPL trong DB.

  const preview = ZplPreview.create('lp-preview', {
    previewUrl: cfg.previewUrl,
    request: api,
    getSource: function () {
      const t = state.template || {};
      return {
        zplData: t.zplData || '',
        size: t.size,
        dpmm: DPMM_BY_DPI[t.dpi],
        variables: state.variables.map(function (v) { return { key: v.key, value: v.value }; }),
        fileName: [t.defaultCode, t.type, t.operation].filter(Boolean).join('_').replace(/[^\w.-]+/g, '-') || 'label'
      };
    }
  });

  // ===== 5. Cấu hình máy in =====

  // Modal dùng chung (js/shared/printer-config.js)
  const printerConfig = window.ToastPrinterConfig.create({
    printers: cfg.printers,
    agentUrl: cfg.agentUrl,
    testConnection: function (body) { return api(cfg.apiUrl + '/test-connection', { method: 'POST', body: JSON.stringify(body) }); },
    notifyError: function (m) { notyf.error(m); },
    onSave: function () { renderPrinterSummary(); clearResult(); }
  });

  function currentPrinter() { return printerConfig.current(); }

  function renderPrinterSummary() {
    el.printerSummary.innerHTML = printerConfig.summaryHtml();
    updatePrintState();
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
      const mode = printerConfig.config().mode;
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
        await printerConfig.send(mode === 'usb'
          ? { path: '/print-usb', zpl: zpl, printerName: printer.name }
          : { path: '/print', zpl: zpl, printerIp: printer.ip, printerPort: printer.port });
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
  el.woClear.addEventListener('click', clearWo);
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

  el.configOpen.addEventListener('click', printerConfig.open);

  // Mở từ màn Mẫu tem (?templateId=...): chọn sẵn mẫu đó và liệt kê các mẫu cùng mã sản phẩm
  async function openTemplateFromLink(id) {
    await selectTemplate(id);
    if (!state.template) return;
    el.tplSearch.value = state.template.defaultCode;
    await searchTemplates();
  }

  renderPrinterSummary();
  const linkedTemplateId = parseInt(new URLSearchParams(window.location.search).get('templateId'), 10);
  if (linkedTemplateId > 0) openTemplateFromLink(linkedTemplateId);
  el.wo.focus();
})();
