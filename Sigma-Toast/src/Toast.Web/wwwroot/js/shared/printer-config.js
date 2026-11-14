/**
 * Modal "Cấu hình máy in" dùng chung (Shared/_PrinterConfigModal.cshtml): màn In tem /toast/admin/print và màn công nhân /toast.
 * Phương thức: Server TCP (server gửi ZPL tới IP), TCP qua Node / USB qua Node (trình duyệt gửi ZPL tới agent localhost:8021).
 * Cấu hình { mode, source, printerId, ip, port, usbName } lưu localStorage toast.print.config: 2 màn trên cùng máy dùng chung.
 *
 *   const pc = ToastPrinterConfig.create({ printers, agentUrl, testConnection(body) => Promise<{success, error, elapsedMs}>,
 *                                          notifyError(msg), onSave(config), noDefault });
 *   noDefault: chưa lưu cấu hình thì không tự chọn máy đầu danh sách (màn công nhân bắt chọn rõ ràng).
 *   pc.config()   cấu hình đang dùng      pc.current()   máy in đang dùng { id?, name, ip?, port?, detail } hoặc null
 *   pc.open()     mở modal                pc.summaryHtml() thẻ tên + chi tiết + phương thức
 *   pc.agent(path, options)               gọi agent     pc.send(job) gửi việc in { path, zpl, printerIp, printerPort, printerName }
 */
(function () {
  'use strict';

  const CONFIG_STORAGE_KEY = 'toast.print.config';
  const AGENT_TIMEOUT_MS = 8000;
  const IPV4 = /^((25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(25[0-5]|2[0-4]\d|1?\d?\d)$/;
  const MODE_LABEL = { server: 'Server TCP', node: 'TCP qua Node', usb: 'USB qua Node' };

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function create(opts) {
    const printers = opts.printers || [];
    const agentUrl = opts.agentUrl || 'http://localhost:8021';
    const notifyError = opts.notifyError || function (m) { window.alert(m); };
    const $ = function (id) { return document.getElementById(id); };
    const el = {
      modal: $('lp-config-modal'), modes: document.querySelectorAll('#lp-config-modal [data-mode]'),
      agent: $('lp-agent'), agentUrl: $('lp-agent-url'), agentStatus: $('lp-agent-status'),
      tcpBlock: $('lp-tcp-block'), srcList: $('lp-src-list'), srcManual: $('lp-src-manual'),
      srcListBlock: $('lp-src-list-block'), srcManualBlock: $('lp-src-manual-block'),
      printer: $('lp-printer'), ip: $('lp-ip'), port: $('lp-port'),
      usbBlock: $('lp-usb-block'), usb: $('lp-usb'), usbRefresh: $('lp-usb-refresh'), usbStatus: $('lp-usb-status'),
      testBlock: $('lp-test-block'), test: $('lp-test'), testResult: $('lp-test-result'),
      configSave: $('lp-config-save')
    };
    const modal = new bootstrap.Modal(el.modal);
    let config = loadConfig();
    let draftMode = config.mode;

    function loadConfig() {
      const fallback = { mode: 'server', source: 'list', printerId: printers.length && !opts.noDefault ? printers[0].id : '', ip: '', port: 9100, usbName: '' };
      try {
        const saved = JSON.parse(localStorage.getItem(CONFIG_STORAGE_KEY) || 'null');
        return saved ? Object.assign(fallback, saved) : fallback;
      } catch (e) {
        return fallback;
      }
    }

    function saveConfig() {
      try { localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(config)); } catch (e) { /* trình duyệt chặn: chỉ dùng trong phiên này */ }
    }

    // Máy in thực sự dùng khi in: tra lại trong danh sách mới nhất từ server (không dùng IP cũ lưu trên trình duyệt)
    function current() {
      const c = config;
      if (c.mode === 'usb') return c.usbName ? { name: c.usbName, detail: 'USB trên máy này' } : null;
      if (c.source === 'manual') return c.ip ? { name: 'Máy in nhập tay', ip: c.ip, port: c.port, detail: c.ip + ':' + c.port } : null;
      const p = printers.find(function (x) { return x.id === c.printerId; });
      return p ? { id: p.id, name: p.name, code: p.code, ip: p.ip, port: p.port, detail: (p.code ? p.code + ' · ' : '') + p.ip + ':' + p.port } : null;
    }

    function summaryHtml() {
      const p = current();
      return p
        ? '<div class="fw-medium text-heading text-truncate">' + esc(p.name) + '</div>' +
          '<div class="small text-muted">' + esc(p.detail) + ' <span class="badge bg-label-secondary ms-1">' + MODE_LABEL[config.mode] + '</span></div>'
        : '<div class="fw-medium text-heading">Chưa chọn máy in</div><div class="small text-muted">Bấm Đổi cấu hình để chọn</div>';
    }

    function open() {
      const c = config;
      draftMode = c.mode;
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
      modal.show();
    }

    function renderModeUi() {
      const mode = draftMode;
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

    async function agent(path, options) {
      let res;
      try {
        res = await fetch(agentUrl + path, Object.assign({ signal: AbortSignal.timeout(AGENT_TIMEOUT_MS) }, options));
      } catch (err) {
        throw new Error('Agent ' + agentUrl + ' đang offline. Chế độ TCP/USB qua Node cần chạy agent trên máy này, hoặc chuyển sang Server TCP.');
      }
      const data = await res.json().catch(function () { return {}; });
      if (!res.ok || data.success === false) throw new Error(data.error || 'Agent trả về lỗi (' + res.status + ').');
      return data;
    }

    // Gửi việc in (server đã điền biến) tới agent: /print-usb { zpl, printerName } hoặc /print { zpl, printerIp, printerPort }
    function send(job) {
      const body = job.path === '/print-usb'
        ? { zpl: job.zpl, printerName: job.printerName }
        : { zpl: job.zpl, printerIp: job.printerIp, printerPort: job.printerPort };
      return agent(job.path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    }

    async function checkAgent() {
      el.agentUrl.textContent = agentUrl.replace(/^https?:\/\//, '');
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
        const cur = el.usb.value || config.usbName;
        el.usb.innerHTML = '<option value="">-- Chọn máy in USB --</option>' +
          list.map(function (n) { return '<option value="' + esc(n) + '">' + esc(n) + '</option>'; }).join('');
        if (cur && list.indexOf(cur) >= 0) el.usb.value = cur;
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
        const res = await opts.testConnection(target.body);
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

    function saveDraft() {
      const mode = draftMode;
      const next = Object.assign({}, config, { mode: mode });
      if (mode === 'usb') {
        if (!el.usb.value) { notifyError('Vui lòng chọn máy in USB'); return; }
        next.usbName = el.usb.value;
      } else {
        const target = readDraftTarget();
        if (target.error) { notifyError(target.error); return; }
        next.source = el.srcManual.checked ? 'manual' : 'list';
        if (next.source === 'manual') { next.ip = target.body.ip; next.port = target.body.port; }
        else next.printerId = target.body.printerId;
      }
      config = next;
      saveConfig();
      modal.hide();
      if (opts.onSave) opts.onSave(config);
    }

    el.modes.forEach(function (b) {
      b.addEventListener('click', function () { draftMode = b.dataset.mode; renderModeUi(); });
    });
    [el.srcList, el.srcManual].forEach(function (r) { r.addEventListener('change', renderModeUi); });
    el.usbRefresh.addEventListener('click', loadUsbPrinters);
    el.test.addEventListener('click', testConnection);
    el.configSave.addEventListener('click', saveDraft);

    return {
      config: function () { return config; },
      current: current,
      modeLabel: function () { return MODE_LABEL[config.mode]; },
      summaryHtml: summaryHtml,
      open: open,
      agent: agent,
      send: send,
      modalElement: el.modal
    };
  }

  window.ToastPrinterConfig = { create: create, MODE_LABEL: MODE_LABEL };
})();
