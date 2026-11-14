/**
 * Máy in - /toast/admin/printer (bảng Toast_Printer)
 * Danh sách lọc phía client (bảng nhỏ, tải 1 lần), Thêm/Sửa qua modal, Xoá qua SweetAlert2.
 * Máy đã in box thì server không cho xoá, chỉ chuyển Ngừng dùng.
 */
(function () {
  'use strict';

  const cfg = window.PrinterPage;
  const notyf = new Notyf({ duration: 3000, position: { x: 'right', y: 'top' }, dismissible: true });
  const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
  const IPV4 = /^((25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(25[0-5]|2[0-4]\d|1?\d?\d)$/;

  const $ = function (id) { return document.getElementById(id); };
  const el = {
    summary: $('pr-summary'), search: $('pr-search'), filterActive: $('pr-filter-active'), tbody: $('pr-tbody'),
    modal: $('pr-modal'), modalTitle: $('pr-modal-title'), form: $('pr-form'), save: $('pr-save'),
    code: $('pr-code'), name: $('pr-name'), ip: $('pr-ip'), port: $('pr-port'), dpi: $('pr-dpi'),
    location: $('pr-location'), active: $('pr-active'), test: $('pr-test'), testResult: $('pr-test-result')
  };
  const modal = new bootstrap.Modal(el.modal);
  const TEST_HINT = el.testResult.textContent;
  const TEST_BUTTON = el.test.innerHTML;
  const state = { rows: [], editingId: null };

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

  async function loadList() {
    el.tbody.innerHTML = '<tr><td colspan="8" class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></td></tr>';
    try {
      const res = await api(cfg.apiUrl, { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      state.rows = res.data;
      const active = state.rows.filter(function (r) { return r.isActive; }).length;
      el.summary.textContent = state.rows.length + ' máy in · ' + active + ' đang dùng';
      render();
    } catch (err) {
      el.summary.textContent = '';
      el.tbody.innerHTML = '<tr><td colspan="8" class="text-center text-danger py-5">' + esc(err.message) + '</td></tr>';
    }
  }

  // ===== Danh sách =====

  function filteredRows() {
    const q = el.search.value.trim().toLowerCase();
    const active = el.filterActive.value;
    return state.rows.filter(function (r) {
      const text = [r.code, r.name, r.ip + ':' + r.port, r.location].join(' ').toLowerCase();
      return (!q || text.includes(q)) && (!active || r.isActive === (active === '1'));
    });
  }

  // Phân trang + sắp xếp (giống màn Mẫu tem)
  const table = ListTable({
    tbody: el.tbody, info: $('pr-info'), pagination: $('pr-pagination'), pageSize: $('pr-page-size'),
    colspan: 8, emptyText: 'Không có máy in nào', rows: filteredRows, rowHtml: rowHtml,
    sortValue: {
      code: function (r) { return (r.code || '').toLowerCase(); },
      name: function (r) { return (r.name || '').toLowerCase(); },
      updated: function (r) { return new Date(r.updatedAt || r.createdAt).getTime() || 0; }
    }
  });

  function render(firstPage) { table.render(firstPage); }

  function rowHtml(r) {
    return '<tr' + (r.isActive ? '' : ' class="text-muted"') + '>' +
      '<td class="fw-medium text-heading">' + esc(r.code) + '</td>' +
      '<td>' + esc(r.name) + '</td>' +
      '<td class="pr-mono">' + esc(r.ip) + ':' + r.port + '</td>' +
      '<td>' + (r.dpi || '<span class="text-muted">—</span>') + '</td>' +
      '<td>' + (esc(r.location) || '<span class="text-muted">—</span>') + '</td>' +
      '<td>' + (r.isActive ? '<span class="badge bg-label-success">Đang dùng</span>' : '<span class="badge bg-label-secondary">Ngừng dùng</span>') + '</td>' +
      '<td class="small">' + formatDate(r.updatedAt || r.createdAt) + '</td>' +
      '<td><div class="d-flex gap-1">' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-primary" data-action="edit" data-id="' + r.id + '" aria-label="Sửa" title="Sửa"><i class="icon-base ti tabler-edit"></i></button>' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-danger" data-action="delete" data-id="' + r.id + '" aria-label="Xoá" title="Xoá"><i class="icon-base ti tabler-trash"></i></button>' +
      '</div></td>' +
      '</tr>';
  }

  // ===== Thêm / Sửa =====

  function resetForm() {
    el.form.reset();
    clearInvalid();
    el.testResult.className = 'form-text';
    el.testResult.textContent = TEST_HINT;
  }

  function openCreate() {
    state.editingId = null;
    el.modalTitle.textContent = 'Thêm máy in';
    resetForm();
    el.port.value = cfg.defaultPort;
    el.active.checked = true;
    modal.show();
    setTimeout(function () { el.code.focus(); }, 300);
  }

  async function openEdit(id) {
    try {
      const res = await api(cfg.apiUrl + '/' + id, { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      const d = res.data;
      state.editingId = d.id;
      el.modalTitle.textContent = 'Sửa máy in ' + d.code;
      resetForm();
      el.code.value = d.code;
      el.name.value = d.name;
      el.ip.value = d.ip;
      el.port.value = d.port;
      el.dpi.value = d.dpi || '';
      el.location.value = d.location || '';
      el.active.checked = d.isActive;
      modal.show();
    } catch (err) {
      notyf.error(err.message);
    }
  }

  async function testConnection() {
    const ip = el.ip.value.trim(), port = toInt(el.port.value);
    el.ip.classList.toggle('is-invalid', !IPV4.test(ip));
    el.port.classList.toggle('is-invalid', !(port >= 1 && port <= 65535));
    if (!IPV4.test(ip) || !(port >= 1 && port <= 65535)) {
      el.testResult.className = 'form-text text-danger';
      el.testResult.textContent = 'Nhập IP và port hợp lệ trước khi thử.';
      return;
    }
    setBusy(el.test, true, 'Đang thử...');
    try {
      const res = await api(cfg.apiUrl + '/test', { method: 'POST', body: JSON.stringify({ ip: ip, port: port }) });
      el.testResult.className = 'form-text ' + (res.success ? 'text-success' : 'text-danger');
      el.testResult.textContent = res.success ? 'Kết nối được ' + ip + ':' + port + ' (' + res.elapsedMs + ' ms).' : res.error;
    } catch (err) {
      el.testResult.className = 'form-text text-danger';
      el.testResult.textContent = err.message;
    } finally {
      setBusy(el.test, false, TEST_BUTTON);
    }
  }

  async function save(e) {
    e.preventDefault();
    const payload = {
      code: el.code.value.trim().toUpperCase(),
      name: el.name.value.trim(),
      ip: el.ip.value.trim(),
      port: toInt(el.port.value),
      dpi: toInt(el.dpi.value) || null,
      location: el.location.value.trim() || null,
      isActive: el.active.checked
    };

    const invalid = [
      [el.code, !payload.code],
      [el.name, !payload.name],
      [el.ip, !IPV4.test(payload.ip)],
      [el.port, !(payload.port >= 1 && payload.port <= 65535)]
    ];
    invalid.forEach(function (m) { m[0].classList.toggle('is-invalid', m[1]); });
    if (invalid.some(function (m) { return m[1]; })) {
      notyf.error('Vui lòng nhập đủ và đúng các trường bắt buộc');
      return;
    }

    setBusy(el.save, true, 'Đang lưu...');
    try {
      const url = state.editingId ? cfg.apiUrl + '/' + state.editingId : cfg.apiUrl;
      const res = await api(url, { method: 'POST', body: JSON.stringify(payload) });
      if (!res.success) throw new Error(res.error);
      modal.hide();
      notyf.success(res.message);
      await loadList();
    } catch (err) {
      notyf.error(err.message);
    } finally {
      setBusy(el.save, false, '<i class="icon-base ti tabler-device-floppy me-1"></i>Lưu');
    }
  }

  // ===== Xoá =====

  async function confirmDelete(id) {
    const r = state.rows.find(function (x) { return x.id === id; });
    if (!r) return;
    const result = await Swal.fire({
      title: 'Xoá máy in?',
      html: 'Máy in <strong>' + esc(r.code) + '</strong> (' + esc(r.ip) + ':' + r.port + ') sẽ bị xoá vĩnh viễn.',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Xoá',
      cancelButtonText: 'Huỷ',
      focusCancel: true,
      buttonsStyling: false,
      customClass: { confirmButton: 'btn btn-danger me-2', cancelButton: 'btn btn-label-secondary' }
    });
    if (!result.isConfirmed) return;
    try {
      const res = await api(cfg.apiUrl + '/' + id + '/delete', { method: 'POST' });
      if (!res.success) throw new Error(res.error);
      notyf.success(res.message);
      await loadList();
    } catch (err) {
      notyf.error(err.message);
    }
  }

  // ===== Tiện ích =====

  function toInt(v) {
    const n = parseInt(String(v || '').trim(), 10);
    return isNaN(n) ? 0 : n;
  }

  function clearInvalid() {
    el.form.querySelectorAll('.is-invalid').forEach(function (x) { x.classList.remove('is-invalid'); });
  }

  function setBusy(btn, busy, html) {
    btn.disabled = busy;
    btn.innerHTML = busy ? '<span class="spinner-border spinner-border-sm me-1"></span>' + html : html;
  }

  function formatDate(value) {
    if (!value) return '—';
    const d = new Date(value);
    if (isNaN(d)) return '—';
    const pad = function (n) { return String(n).padStart(2, '0'); };
    return pad(d.getDate()) + '/' + pad(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + pad(d.getHours()) + ':' + pad(d.getMinutes());
  }

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  // ===== Sự kiện =====

  el.search.addEventListener('input', function () { render(true); });
  el.filterActive.addEventListener('change', function () { render(true); });
  $('pr-add').addEventListener('click', openCreate);
  $('pr-reload').addEventListener('click', function () {
    el.search.value = '';
    el.filterActive.value = '';
    table.reset();
    loadList();
  });
  el.tbody.addEventListener('click', function (e) {
    const btn = e.target.closest('button[data-action]');
    if (!btn) return;
    const id = parseInt(btn.dataset.id, 10);
    if (btn.dataset.action === 'edit') openEdit(id);
    else if (btn.dataset.action === 'delete') confirmDelete(id);
  });
  // Bấm 1 lần vào dòng = Sửa (bôi đen / double-click thì không mở)
  window.ToastRowClick.button(el.tbody, '[data-action="edit"]');
  el.test.addEventListener('click', testConnection);
  el.form.addEventListener('input', function (e) { e.target.classList.remove('is-invalid'); });
  el.form.addEventListener('submit', save);

  loadList();
})();
