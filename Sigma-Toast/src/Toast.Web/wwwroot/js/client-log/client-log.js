/**
 * Log trạm - /toast/admin/client-log: log lỗi gửi từ trình duyệt màn công nhân (file logs/client-yyyyMMdd.log trên server).
 * Chọn ngày (mặc định ngày mới nhất có log) -> server trả tối đa 5000 dòng mới nhất; lọc mức / tìm ở trình duyệt.
 * Tải lại = tải lại ngày đang xem + xoá lọc / tìm. Bấm 1 lần vào dòng (hoặc nút xem) mở chi tiết + dữ liệu JSON.
 */
(function () {
  'use strict';

  const cfg = window.ClientLogPage;
  const $ = function (id) { return document.getElementById(id); };
  const el = {
    summary: $('cl-summary'), download: $('cl-download'), day: $('cl-day'), level: $('cl-level'), search: $('cl-search'),
    reload: $('cl-reload'), tbody: $('cl-tbody'), modal: $('cl-modal'), title: $('cl-modal-title'), detail: $('cl-detail')
  };
  const modal = new bootstrap.Modal(el.modal);
  const state = { rows: [], day: null };
  const LEVELS = {
    error: ['bg-label-danger', 'Lỗi'], warn: ['bg-label-warning', 'Cảnh báo'], info: ['bg-label-info', 'Thông tin']
  };

  async function load(day) {
    el.tbody.innerHTML = '<tr><td colspan="8" class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></td></tr>';
    try {
      const res = await fetch(cfg.apiUrl + (day ? '?day=' + encodeURIComponent(day) : ''), { headers: { 'X-Requested-With': 'XMLHttpRequest' }, cache: 'no-store' });
      if (res.status === 401) {
        window.location.href = cfg.loginUrl + '?returnUrl=' + encodeURIComponent(window.location.pathname);
        return;
      }
      if (!res.ok) throw new Error('Lỗi máy chủ (' + res.status + ').');
      const data = await res.json();
      if (!data.success) throw new Error(data.error);
      state.rows = data.rows;
      state.day = data.day;
      renderDays(data.days, data.day);
      el.download.classList.toggle('d-none', !data.day);
      if (data.day) el.download.href = cfg.downloadUrl + '?day=' + encodeURIComponent(data.day);
      const errors = state.rows.filter(function (r) { return r.level === 'error'; }).length;
      el.summary.textContent = data.day
        ? state.rows.length + ' dòng · ' + errors + ' lỗi' + (data.limited ? ' (ngày này có ' + data.total + ' dòng, chỉ hiện ' + state.rows.length + ' dòng mới nhất; tải file để xem hết)' : '')
        : 'Chưa có log nào (thư mục ' + data.directory + '). Thư mục được tạo ở lần đầu trạm gửi log lên.';
      // Server không ghi được file (VD app pool IIS không có quyền ghi): báo đỏ kèm thư mục
      if (data.writeError) {
        el.summary.innerHTML = esc(el.summary.textContent) + '<div class="text-danger mt-1"><i class="icon-base ti tabler-alert-triangle me-1"></i>Lần ghi log gần nhất lỗi (' +
          esc(new Date(data.writeError.at).toLocaleString('vi-VN')) + '): ' + esc(String(data.writeError.message).replace(/\.+$/, '')) +
          '. Log trên các máy trạm vẫn được giữ, ghi được sẽ tự gửi lên. Cấp quyền Modify cho tài khoản app pool (IIS AppPool\\&lt;tên pool&gt;) vào thư mục ' + esc(data.directory) + ' hoặc thư mục cha.</div>';
      }
      table.render(true);
    } catch (err) {
      el.summary.textContent = '';
      el.tbody.innerHTML = '<tr><td colspan="8" class="text-center text-danger py-5">' + esc(err.message) + '</td></tr>';
    }
  }

  function renderDays(days, selected) {
    el.day.innerHTML = days.length
      ? days.map(function (d) { return '<option value="' + d + '">' + fmtDay(d) + '</option>'; }).join('')
      : '<option value="">Chưa có log</option>';
    if (selected) el.day.value = selected;
  }

  function fmtDay(d) { return d.slice(6, 8) + '/' + d.slice(4, 6) + '/' + d.slice(0, 4); }
  // "2026-11-11 10:41:15" / "2026-11-11T10:41:15" -> giờ
  function timeOnly(v) { const m = /(\d{2}:\d{2}:\d{2})/.exec(v || ''); return m ? m[1] : (v || ''); }
  function shortDevice(d) { return d ? d.slice(0, 8) : ''; }

  function filteredRows() {
    const level = el.level.value;
    const q = el.search.value.trim().toLowerCase();
    return state.rows.filter(function (r) {
      if (level && r.level !== level) return false;
      if (!q) return true;
      return [r.wo, r.device, r.ip, r.event, r.message, r.data].some(function (v) { return (v || '').toLowerCase().indexOf(q) >= 0; });
    });
  }

  function levelBadge(level) {
    const b = LEVELS[level] || ['bg-label-secondary', level || '—'];
    return '<span class="badge ' + b[0] + '">' + esc(b[1]) + '</span>';
  }

  function rowHtml(r) {
    return '<tr data-no="' + r.no + '">' +
      '<td class="small">' + esc(timeOnly(r.serverTime)) + '</td>' +
      '<td class="small text-muted">' + esc(timeOnly(r.clientTime)) + '</td>' +
      '<td>' + levelBadge(r.level) + '</td>' +
      '<td class="small"><div>' + esc(r.ip) + '</div><div class="text-muted" title="' + esc(r.device) + '">' + esc(shortDevice(r.device)) + '</div></td>' +
      '<td class="small fw-medium text-heading">' + esc(r.wo) + '</td>' +
      '<td class="small"><code>' + esc(r.event) + '</code></td>' +
      '<td class="small cl-msg">' + esc(r.message) + '</td>' +
      '<td><button type="button" class="btn btn-sm btn-icon btn-label-primary" data-action="view" aria-label="Xem chi tiết" title="Xem chi tiết"><i class="icon-base ti tabler-eye"></i></button></td>' +
      '</tr>';
  }

  const table = ListTable({
    tbody: el.tbody, info: $('cl-info'), pagination: $('cl-pagination'), pageSize: $('cl-page-size'),
    colspan: 8, emptyText: 'Không có dòng log nào', rows: filteredRows, rowHtml: rowHtml,
    sortValue: {
      server: function (r) { return r.serverTime; },
      client: function (r) { return r.clientTime; },
      level: function (r) { return r.level; },
      device: function (r) { return r.ip + ' ' + r.device; },
      wo: function (r) { return r.wo; },
      event: function (r) { return r.event; }
    }
  });

  function openDetail(no) {
    const r = state.rows.find(function (x) { return x.no === no; });
    if (!r) return;
    el.title.innerHTML = levelBadge(r.level) + ' <code>' + esc(r.event) + '</code> <span class="text-muted small fw-normal">dòng ' + r.no + '</span>';
    let data = r.data;
    try { if (data) data = JSON.stringify(JSON.parse(data), null, 2); } catch (e) { /* không phải JSON: hiện nguyên */ }
    el.detail.innerHTML = '<dl class="cl-info mb-4">' +
      item('Giờ server', r.serverTime) + item('Giờ máy trạm', r.clientTime) + item('IP máy trạm', r.ip) +
      item('Mã máy', r.device) + item('WO', r.wo) + item('Sự kiện', r.event) + item('Nội dung', r.message) + '</dl>' +
      '<div class="form-label">Dữ liệu</div>' +
      (data ? '<pre class="cl-data">' + esc(data) + '</pre>' : '<div class="text-muted">Không có</div>');
    modal.show();
    function item(label, value) { return '<dt>' + label + '</dt><dd>' + (value ? esc(value) : '<span class="text-muted">—</span>') + '</dd>'; }
  }

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  el.day.addEventListener('change', function () { load(el.day.value); });
  el.level.addEventListener('change', function () { table.render(true); });
  el.search.addEventListener('input', function () { table.render(true); });
  // Tải lại: tải lại ngày đang xem + xoá lọc / tìm / sắp xếp
  el.reload.addEventListener('click', function () {
    el.level.value = '';
    el.search.value = '';
    table.reset();
    load(state.day);
  });
  el.tbody.addEventListener('click', function (e) {
    if (!e.target.closest('[data-action="view"]')) return;
    const tr = e.target.closest('tr[data-no]');
    if (tr) openDetail(parseInt(tr.dataset.no, 10));
  });
  window.ToastRowClick.button(el.tbody, '[data-action="view"]');

  load(null);
})();
