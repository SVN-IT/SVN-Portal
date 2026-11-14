/**
 * Kiện đóng gói - /toast/admin/package (bảng Toast_Package). Sửa / xoá, không thêm, không in tem.
 * Cột Log: dòng SVN_ProductionInputLogs (dashboard) của kiện. Sửa số pcs thì sửa luôn dòng log, xoá / mở lại kiện thì xoá dòng log.
 */
(function () {
  'use strict';

  const cfg = window.PackagePage;
  const notyf = new Notyf({ duration: 3000, position: { x: 'right', y: 'top' }, dismissible: true });
  const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
  const $ = function (id) { return document.getElementById(id); };
  const el = {
    summary: $('pk-summary'), search: $('pk-search'), status: $('pk-filter-status'), tbody: $('pk-tbody'),
    modal: $('pk-modal'), title: $('pk-modal-title'), form: $('pk-form'), save: $('pk-save'),
    wo: $('pk-wo'), part: $('pk-part'), no: $('pk-no'), qty: $('pk-qty'), cap: $('pk-cap'), pkStatus: $('pk-status')
  };
  const modal = new bootstrap.Modal(el.modal);
  const state = { rows: [], printers: {}, editingId: null };

  async function api(url, options) {
    const res = await fetch(url, Object.assign({
      headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': token }
    }, options));
    if (res.status === 401) {
      window.location.href = cfg.loginUrl + '?returnUrl=' + encodeURIComponent(window.location.pathname);
      throw new Error('Phiên đăng nhập đã hết hạn.');
    }
    if (!res.ok) throw new Error('Lỗi máy chủ (' + res.status + ').');
    return res.json();
  }

  async function loadList() {
    el.tbody.innerHTML = '<tr><td colspan="10" class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></td></tr>';
    try {
      const res = await api(cfg.apiUrl + '?q=' + encodeURIComponent(el.search.value.trim()), { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      state.rows = res.data;
      state.printers = res.printers || {};
      const pcs = state.rows.reduce(function (sum, r) { return sum + r.qty; }, 0);
      el.summary.textContent = state.rows.length + ' box · ' + pcs.toLocaleString('vi-VN') + ' pcs' + (res.limited ? ' (chỉ tải 2000 box mới nhất, gõ WO để tìm box cũ hơn)' : '');
      render();
    } catch (err) {
      el.summary.textContent = '';
      el.tbody.innerHTML = '<tr><td colspan="10" class="text-center text-danger py-5">' + esc(err.message) + '</td></tr>';
    }
  }

  function filteredRows() {
    const status = el.status.value;
    return state.rows.filter(function (r) { return !status || r.status === status; });
  }

  function rowHtml(r) {
    const open = r.status === 'Open';
    const print = open ? '<span class="text-muted">—</span>'
      : r.printedAt ? '<span class="badge bg-label-success">Đã in</span> <span class="small text-muted">' + esc(state.printers[r.printerId] || '') + '</span>'
        : '<span class="badge bg-label-danger">Lỗi</span> <span class="small text-muted">' + esc(r.printError) + '</span>';
    const log = r.inputLogId
      ? '<span class="badge bg-label-success" title="id dòng SVN_ProductionInputLogs">#' + r.inputLogId + '</span>'
      : '<span class="text-muted">—</span>';
    return '<tr>' +
      '<td class="text-muted">' + r.id + '</td>' +
      '<td class="fw-medium text-heading">' + esc(r.woName) + '</td>' +
      '<td>' + esc(r.defaultCode) + '</td>' +
      '<td>' + r.packageNo + '</td>' +
      '<td>' + r.qty + ' / ' + r.capacity + '</td>' +
      '<td>' + (open ? '<span class="badge bg-label-primary">Đang quét</span>' : '<span class="badge bg-label-secondary">Đã đóng</span>') + '</td>' +
      '<td>' + print + '</td>' +
      '<td>' + log + '</td>' +
      '<td class="small">' + (r.closedAt ? new Date(r.closedAt).toLocaleString('vi-VN') : '—') + '</td>' +
      '<td><div class="d-flex gap-1">' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-primary" data-action="edit" data-id="' + r.id + '" aria-label="Sửa" title="Sửa"><i class="icon-base ti tabler-edit"></i></button>' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-danger" data-action="delete" data-id="' + r.id + '" aria-label="Xoá" title="Xoá"><i class="icon-base ti tabler-trash"></i></button>' +
      '</div></td></tr>';
  }

  // Phân trang + sắp xếp (giống màn Mẫu tem); mặc định box mới nhất trước (thứ tự server)
  const table = ListTable({
    tbody: el.tbody, info: $('pk-info'), pagination: $('pk-pagination'), pageSize: $('pk-page-size'),
    colspan: 10, emptyText: 'Không có box nào', rows: filteredRows, rowHtml: rowHtml,
    sortValue: {
      id: function (r) { return r.id; },
      wo: function (r) { return r.woName; },
      code: function (r) { return r.defaultCode; },
      no: function (r) { return r.packageNo; },
      closed: function (r) { return r.closedAt ? new Date(r.closedAt).getTime() : 0; }
    }
  });

  function render(firstPage) { table.render(firstPage); }

  function openForm(r) {
    state.editingId = r.id;
    el.title.textContent = 'Sửa box ' + r.packageNo + ' · ' + r.woName;
    el.form.reset();
    el.wo.value = r.woName;
    el.part.value = r.defaultCode;
    el.no.value = r.packageNo;
    el.qty.value = r.qty;
    el.cap.value = r.capacity;
    el.pkStatus.value = r.status;
    modal.show();
  }

  async function save(e) {
    e.preventDefault();
    const payload = {
      packageNo: toInt(el.no.value),
      qty: toInt(el.qty.value),
      capacity: toInt(el.cap.value),
      status: el.pkStatus.value
    };
    setBusy(true);
    try {
      const res = await api(cfg.apiUrl + '/' + state.editingId, { method: 'POST', body: JSON.stringify(payload) });
      if (!res.success) throw new Error(res.error);
      modal.hide();
      notyf.success(res.message);
      await loadList();
    } catch (err) {
      notyf.error(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function confirmDelete(r) {
    const result = await Swal.fire({
      title: 'Xoá box?',
      html: 'Box <strong>' + r.packageNo + '</strong> của <strong>' + esc(r.woName) + '</strong> (' + r.qty + ' pcs) sẽ bị xoá vĩnh viễn.' +
        (r.inputLogId ? '<br><span class="small text-muted">Dòng log dashboard #' + r.inputLogId + ' cũng bị xoá.</span>' : ''),
      icon: 'warning', showCancelButton: true, confirmButtonText: 'Xoá', cancelButtonText: 'Huỷ', focusCancel: true,
      buttonsStyling: false, customClass: { confirmButton: 'btn btn-danger me-2', cancelButton: 'btn btn-label-secondary' }
    });
    if (!result.isConfirmed) return;
    try {
      const res = await api(cfg.apiUrl + '/' + r.id + '/delete', { method: 'POST' });
      if (!res.success) throw new Error(res.error);
      notyf.success(res.message);
      await loadList();
    } catch (err) {
      notyf.error(err.message);
    }
  }

  function toInt(v) { const n = parseInt(String(v || '').trim(), 10); return isNaN(n) ? 0 : n; }

  function setBusy(busy) {
    el.save.disabled = busy;
    el.save.innerHTML = busy ? '<span class="spinner-border spinner-border-sm me-1"></span>Đang lưu...' : '<i class="icon-base ti tabler-device-floppy me-1"></i>Lưu';
  }

  function esc(s) {
    return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  // Tìm ở server (tìm được cả box cũ ngoài 2000 box mới nhất), gõ xong 0,4 giây thì tải
  let searchTimer = null;
  el.search.addEventListener('input', function () {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(function () { table.reset(); loadList(); }, 400);
  });
  el.status.addEventListener('change', function () { render(true); });
  // Tải lại: xoá ô tìm + bộ lọc rồi tải lại dữ liệu
  $('pk-reload').addEventListener('click', function () {
    el.search.value = '';
    el.status.value = '';
    table.reset();
    loadList();
  });
  el.tbody.addEventListener('click', function (e) {
    const btn = e.target.closest('button[data-action]');
    if (!btn) return;
    const r = state.rows.find(function (x) { return x.id === parseInt(btn.dataset.id, 10); });
    if (!r) return;
    if (btn.dataset.action === 'edit') openForm(r); else confirmDelete(r);
  });
  // Bấm 1 lần vào dòng = Sửa (bôi đen / double-click thì không mở)
  window.ToastRowClick.button(el.tbody, '[data-action="edit"]');
  el.form.addEventListener('submit', save);

  loadList();
})();
