/**
 * Trạm quét v2 (nháp) - /toastv2. Chỉ xem: nhập WO -> dữ liệu Viindoo, sản phẩm / mẫu tem Toast, giá trị từng biến, xem trước tem.
 * API: GET {apiUrl}/wo?wo=..., POST {apiUrl}/preview (vẽ tem, wwwroot/js/label-template/zpl-preview.js).
 */
(function () {
  'use strict';

  const cfg = window.StationV2Page;
  const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
  const $ = function (id) { return document.getElementById(id); };
  const el = {
    form: $('v2-form'), wo: $('v2-wo'), open: $('v2-open'), reload: $('v2-reload'),
    alerts: $('v2-alerts'), body: $('v2-body'),
    woInfo: $('v2-wo-info'), toastInfo: $('v2-toast-info'), vars: $('v2-vars')
  };
  const state = { data: null, wo: '' };

  // Trường của nguồn "wo" -> lấy từ đâu trên Viindoo
  const WO_FIELD_SOURCE = {
    customer_part_number: 'Sản phẩm Viindoo · x_custcode',
    qty_per_code: 'Sản phẩm Viindoo · x_quantity_per_code',
    sale_order: 'Đơn bán', po: 'Đơn bán · x_ERP_PO', lot_id: 'Đơn bán · x_LotID'
  };
  const SOURCE_TEXT = { template: 'Lưu trong mẫu', product: 'Sản phẩm Toast', 'package': 'Box (chưa có, chỉ xem)', system: 'Hệ thống', manual: 'Không có nguồn' };

  async function api(url, options) {
    const res = await fetch(url, Object.assign({
      headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': token }
    }, options));
    if (!res.ok) throw new Error('Lỗi máy chủ (' + res.status + ').');
    return res.json();
  }

  const preview = ZplPreview.create('v2-preview', {
    previewUrl: cfg.apiUrl + '/preview',
    request: api,
    getSource: function () {
      const d = state.data || {}, t = d.template || {};
      return {
        zplData: t.zplData || '',
        size: t.size,
        dpmm: t.dpmm,
        variables: (d.variables || []).map(function (v) { return { key: v.key, value: v.value }; }),
        fileName: [d.wo && d.wo.name, t.defaultCode].filter(Boolean).join('_').replace(/[^\w.-]+/g, '-') || 'label'
      };
    }
  });

  async function openWo(wo) {
    wo = (wo || '').trim();
    if (!wo) { el.wo.focus(); return; }
    setBusy(true);
    try {
      const res = await api(cfg.apiUrl + '/wo?wo=' + encodeURIComponent(wo), { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      state.data = res;
      state.wo = wo;
      render();
      preview.reset();
      preview.refresh();
    } catch (err) {
      state.data = null;
      el.body.classList.add('d-none');
      el.alerts.innerHTML = alert('danger', 'tabler-alert-circle', [err.message]);
    } finally {
      setBusy(false);
    }
  }

  function render() {
    const d = state.data, w = d.wo, p = d.product, t = d.template;
    el.alerts.innerHTML = (d.problems.length ? alert('danger', 'tabler-alert-circle', d.problems) : '') +
      (d.warnings.length ? alert('warning', 'tabler-alert-triangle', d.warnings) : '') +
      (!d.problems.length && !d.warnings.length ? alert('success', 'tabler-circle-check', ['Đủ dữ liệu cho tem.']) : '');

    el.woInfo.innerHTML =
      row('WO', '<span class="fw-semibold">' + esc(w.name) + '</span>' + (w.state ? ' <span class="badge bg-label-secondary ms-1">' + esc(w.state) + '</span>' : '') + (d.isTest ? ' <span class="badge bg-label-warning ms-1">WO thử</span>' : '')) +
      row('Sản phẩm', '<span class="pd-mono">' + esc(w.product_code) + '</span> · ' + esc(w.product_name) + ' <span class="text-muted small">(ID ' + esc(w.product_id) + ')</span>') +
      row('Số lượng WO', esc(w.product_qty)) +
      row('Origin', val(w.origin)) +
      row('Đơn bán', val(w.sale_order)) +
      row('PO (x_ERP_PO)', val(w.po)) +
      row('Lot ID (x_LotID)', val(w.lot_id)) +
      row('Part number khách (x_custcode)', val(w.customer_part_number)) +
      row('x_quantity_per_code', val(w.qty_per_code));

    el.toastInfo.innerHTML =
      row('Sản phẩm', p
        ? '<span class="pd-mono">' + esc(p.defaultCode) + '</span>' + (p.name ? ' · ' + esc(p.name) : '')
        : '<span class="text-danger fw-semibold">Chưa có</span>') +
      (p ? row('Mã quét pcs', p.scanSource === 'LotId' ? 'Lot ID của WO: ' + val(w.lot_id) : '<code>' + esc(p.barcode) + '</code>') + row('Pcs / box', '<span class="fw-semibold">' + p.pcsPerBox + '</span>') : '') +
      row('Mẫu tem Box', t
        ? '<span class="pd-mono">' + esc(t.defaultCode) + '</span> · ' + esc(t.type) + ' · ' + esc(t.size) + ' · ' + t.dpi + ' dpi <span class="text-muted small">(#' + t.id + ')</span>'
        : '<span class="text-danger fw-semibold">Chưa có</span>');

    el.vars.innerHTML = d.variables.length ? d.variables.map(function (v) {
      const empty = !String(v.value || '').trim();
      const status = empty
        ? (v.optional ? '<span class="badge bg-label-secondary">Trống (tuỳ chọn)</span>' : '<span class="badge bg-label-danger">THIẾU</span>')
        : '';
      return '<tr class="' + (empty && !v.optional ? 'table-danger' : '') + '">' +
        '<td class="v2-key">{' + esc(v.key) + '}' + (v.label && v.label !== v.key ? '<div class="text-muted small">' + esc(v.label) + '</div>' : '') + '</td>' +
        '<td class="v2-value">' + (empty ? status : '<span class="fw-medium text-heading">' + esc(v.value) + '</span>') + '</td>' +
        '<td class="small text-muted">' + esc(sourceText(v)) + '</td>' +
        '</tr>';
    }).join('') : '<tr><td colspan="3" class="text-center text-muted py-4">' + (t ? 'Mẫu tem không có biến' : 'Chưa có mẫu tem') + '</td></tr>';

    el.body.classList.remove('d-none');
  }

  function sourceText(v) {
    if (v.source === 'wo') return WO_FIELD_SOURCE[v.field] || ('WO · ' + (v.field || ''));
    const base = SOURCE_TEXT[v.source] || v.source;
    return v.source === 'template' || v.source === 'manual' || !v.field ? base : base + ' · ' + v.field;
  }

  // ===== Helpers =====

  function alert(type, icon, lines) {
    return '<div class="alert alert-' + type + ' mb-0 d-flex gap-2" role="' + (type === 'danger' ? 'alert' : 'status') + '">' +
      '<i class="icon-base ti ' + icon + ' mt-1 flex-shrink-0"></i><div>' +
      lines.map(function (l) { return '<div>' + esc(l) + '</div>'; }).join('') + '</div></div>';
  }

  function row(label, html) { return '<dt>' + esc(label) + '</dt><dd>' + html + '</dd>'; }
  function val(v) { return v ? esc(v) : '<span class="text-muted">—</span>'; }

  function setBusy(busy) {
    el.open.disabled = busy;
    el.reload.disabled = busy;
    el.open.innerHTML = busy
      ? '<span class="spinner-border spinner-border-sm me-1" role="status"></span>Đang đọc...'
      : '<i class="icon-base ti tabler-search me-1"></i>Mở WO';
  }

  function esc(s) {
    return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  el.form.addEventListener('submit', function (e) { e.preventDefault(); openWo(el.wo.value); });
  el.reload.addEventListener('click', function () { openWo(state.wo); });

  // Mở từ màn Tra cứu đơn bán: /toastv2?wo=NM/MO/03975-261
  const initial = new URLSearchParams(window.location.search).get('wo');
  if (initial) { el.wo.value = initial; openWo(initial); }
})();
