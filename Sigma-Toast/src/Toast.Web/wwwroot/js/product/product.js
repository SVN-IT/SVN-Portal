/**
 * Quy cách sản phẩm - /toast/admin/product
 * Danh sách lọc phía client (bảng nhỏ, tải 1 lần), Thêm/Sửa qua modal, Xoá qua SweetAlert2.
 * Nhập mã sản phẩm (default code) rồi Enter / rời ô / bấm nút ↻: đọc Viindoo (chỉ đọc), điền Viindoo ID, tên,
 * Pcs / box = x_quantity_per_code. Giá trị Viindoo lần đọc gần nhất giữ trong state.viindooQty để cảnh báo khi sửa tay khác đi.
 */
(function () {
  'use strict';

  const cfg = window.ProductPage;
  const notyf = new Notyf({ duration: 3000, position: { x: 'right', y: 'top' }, dismissible: true });
  const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

  const $ = function (id) { return document.getElementById(id); };
  const el = {
    summary: $('pd-summary'), search: $('pd-search'), filterOuter: $('pd-filter-outer'), tbody: $('pd-tbody'),
    modal: $('pd-modal'), modalTitle: $('pd-modal-title'), form: $('pd-form'), save: $('pd-save'),
    fetch: $('pd-fetch'), fetchResult: $('pd-fetch-result'),
    woToggle: $('pd-wo-toggle'), woBox: $('pd-wo-box'), wo: $('pd-wo'), woFetch: $('pd-wo-fetch'),
    vid: $('pd-vid'), part: $('pd-part'), name: $('pd-name'), barcode: $('pd-barcode'),
    scan: $('pd-scan'), scanHint: $('pd-scan-hint'), barcodeCol: $('pd-barcode-col'),
    pcs: $('pd-pcs'), outer: $('pd-outer'), boxPerOuter: $('pd-box-per-outer'),
    boxPerOuterLabel: $('pd-box-per-outer-label'), compare: $('pd-compare')
  };
  const modal = new bootstrap.Modal(el.modal);
  const FETCH_HINT = el.fetchResult.textContent;
  // fetchedCode: mã đã đọc Viindoo gần nhất (rời ô mà không đổi mã thì không đọc lại)
  const state = { rows: [], editingId: null, viindooQty: null, fetchedCode: '' };

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
      el.summary.textContent = state.rows.length + ' sản phẩm';
      render();
    } catch (err) {
      el.summary.textContent = '';
      el.tbody.innerHTML = '<tr><td colspan="8" class="text-center text-danger py-5">' + esc(err.message) + '</td></tr>';
    }
  }

  // ===== Danh sách =====

  function filteredRows() {
    const q = el.search.value.trim().toLowerCase();
    const outer = el.filterOuter.value;
    return state.rows.filter(function (r) {
      const text = [r.defaultCode, r.name, r.barcode, isLot(r) ? 'lot id' : '', String(r.productId)].join(' ').toLowerCase();
      return (!q || text.includes(q))
        && (!outer || (outer === '-' ? !r.outerType : r.outerType === outer));
    });
  }

  function render() {
    const rows = filteredRows();
    el.tbody.innerHTML = rows.length
      ? rows.map(rowHtml).join('')
      : '<tr><td colspan="8" class="text-center text-muted py-5">Không có sản phẩm nào</td></tr>';
  }

  function rowHtml(r) {
    return '<tr>' +
      '<td class="fw-medium text-heading">' + esc(r.defaultCode) + '</td>' +
      '<td>' + (esc(r.name) || '<span class="text-muted">—</span>') + '</td>' +
      '<td class="text-muted">' + r.productId + '</td>' +
      '<td>' + (isLot(r) ? '<span class="badge bg-label-primary">Lot ID của WO</span>' : '<span class="pd-mono">' + esc(r.barcode) + '</span>') + '</td>' +
      '<td>' + pcsCell(r) + '</td>' +
      '<td>' + (r.outerType ? '<span class="badge bg-label-info">' + esc(r.outerType) + '</span> ' + r.boxPerOuter + ' box' : '<span class="text-muted">Chỉ box</span>') + '</td>' +
      '<td class="small">' + formatDate(r.updatedAt || r.createdAt) + '</td>' +
      '<td><div class="d-flex gap-1">' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-primary" data-action="edit" data-id="' + r.id + '" aria-label="Sửa" title="Sửa"><i class="icon-base ti tabler-edit"></i></button>' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-danger" data-action="delete" data-id="' + r.id + '" aria-label="Xoá" title="Xoá"><i class="icon-base ti tabler-trash"></i></button>' +
      '</div></td>' +
      '</tr>';
  }

  function isLot(r) { return r.scanSource === 'LotId'; }

  // Quét theo Lot ID: ẩn ô barcode cố định
  function updateScan() {
    const lot = el.scan.value === 'LotId';
    el.barcodeCol.classList.toggle('d-none', lot);
    el.scanHint.classList.toggle('d-none', !lot);
    if (lot) el.barcode.classList.remove('is-invalid');
  }

  function pcsCell(r) {
    return '<span class="fw-semibold text-heading">' + r.pcsPerBox + '</span>';
  }

  // ===== Thêm / Sửa =====

  function openCreate() {
    state.editingId = null;
    el.modalTitle.textContent = 'Thêm sản phẩm';
    el.form.reset();
    clearInvalid();
    el.fetchResult.className = 'form-text';
    el.fetchResult.textContent = FETCH_HINT;
    state.viindooQty = null;
    state.fetchedCode = '';
    showWoBox(false);
    updateScan();
    updateOuter();
    updateCompare();
    modal.show();
    setTimeout(function () { el.part.focus(); }, 300);
  }

  async function openEdit(id) {
    try {
      const res = await api(cfg.apiUrl + '/' + id, { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      const d = res.data;
      state.editingId = d.id;
      el.modalTitle.textContent = 'Sửa sản phẩm ' + d.defaultCode;
      el.form.reset();
      clearInvalid();
      el.fetchResult.className = 'form-text';
      el.fetchResult.textContent = FETCH_HINT;
      el.vid.value = d.productId;
      el.part.value = d.defaultCode || '';
      el.name.value = d.name || '';
      el.scan.value = d.scanSource === 'LotId' ? 'LotId' : 'Barcode';
      el.barcode.value = d.barcode || '';
      updateScan();
      el.pcs.value = d.pcsPerBox;
      state.viindooQty = d.viindooQtyPerCode || null;
      state.fetchedCode = (d.defaultCode || '').toUpperCase();
      showWoBox(false);
      el.outer.value = d.outerType || '';
      el.boxPerOuter.value = d.boxPerOuter || '';
      updateOuter();
      updateCompare();
      modal.show();
    } catch (err) {
      notyf.error(err.message);
    }
  }

  // Đọc Viindoo theo mã sản phẩm: điền Viindoo ID, tên, Pcs / box (x_quantity_per_code > 0). Người dùng vẫn sửa được.
  // force = bấm nút ↻ (đọc lại dù mã không đổi)
  async function fetchFromViindoo(force) {
    const code = el.part.value.trim();
    if (!code) { el.part.focus(); return; }
    if (!force && code.toUpperCase() === state.fetchedCode) return;
    if (el.fetch.disabled) return;
    el.fetch.disabled = true;
    el.fetch.classList.add('is-loading');
    el.fetchResult.className = 'form-text';
    el.fetchResult.textContent = 'Đang đọc Viindoo...';
    try {
      const res = await api(cfg.apiUrl + '/viindoo?code=' + encodeURIComponent(code), { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      state.fetchedCode = (res.defaultCode || code).toUpperCase();
      el.part.value = res.defaultCode || code;
      el.vid.value = res.productId;
      el.name.value = res.name || '';
      state.viindooQty = res.viindooQtyPerCode || null;
      if (state.viindooQty) el.pcs.value = state.viindooQty;
      clearInvalid();
      updateCompare();

      const dup = res.existingId && res.existingId !== state.editingId;
      let msg = 'Đã lấy từ Viindoo: ID ' + res.productId + (res.name ? ' · ' + res.name : '') + '.';
      if (!state.viindooQty) msg += ' Viindoo chưa có pcs/box (x_quantity_per_code), nhập tay số lớn hơn 0.';
      if (dup) msg += ' Sản phẩm này đã có quy cách, lưu sẽ bị báo trùng.';
      el.fetchResult.className = 'form-text ' + (dup || !state.viindooQty ? 'text-warning' : 'text-success');
      el.fetchResult.textContent = msg;
      if (!state.viindooQty) el.pcs.focus();
      else if (el.scan.value !== 'LotId' && !el.barcode.value) el.barcode.focus();
    } catch (err) {
      state.fetchedCode = '';
      el.fetchResult.className = 'form-text text-danger';
      el.fetchResult.textContent = err.message;
    } finally {
      el.fetch.disabled = false;
      el.fetch.classList.remove('is-loading');
    }
  }

  // Tra theo WO: lấy mã sản phẩm của WO, điền vào ô mã rồi đọc Viindoo theo mã như bình thường
  function showWoBox(show) {
    el.woBox.classList.toggle('d-none', !show);
    el.woToggle.setAttribute('aria-expanded', show ? 'true' : 'false');
    if (!show) el.wo.value = '';
    else el.wo.focus();
  }

  async function fetchCodeFromWo() {
    const wo = el.wo.value.trim();
    if (!wo) { el.wo.focus(); return; }
    setBusy(el.woFetch, true, 'Đang tra...');
    try {
      const res = await api(cfg.apiUrl + '/viindoo-wo?wo=' + encodeURIComponent(wo), { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      el.part.value = res.defaultCode;
      showWoBox(false);
      await fetchFromViindoo(true);
    } catch (err) {
      el.fetchResult.className = 'form-text text-danger';
      el.fetchResult.textContent = err.message;
    } finally {
      setBusy(el.woFetch, false, 'Tra');
    }
  }

  function updateOuter() {
    const outer = el.outer.value;
    el.boxPerOuter.disabled = !outer;
    if (!outer) el.boxPerOuter.value = '';
    el.boxPerOuterLabel.textContent = outer ? 'Số box / ' + outer : 'Số box / cấp 2';
  }

  // Cảnh báo khi pcs/box Toast khác Viindoo (lưu được, nhưng mở phiên sẽ bị chặn)
  function updateCompare() {
    const pcs = toInt(el.pcs.value), v = state.viindooQty;
    const show = pcs > 0 && v > 0 && pcs !== v;
    el.compare.classList.toggle('d-none', !show);
    el.compare.innerHTML = show
      ? '<i class="icon-base ti tabler-alert-triangle me-1"></i>Pcs / box của Toast (' + pcs + ') khác Viindoo (' + v + '). Vẫn lưu được nhưng màn công nhân sẽ không mở được phiên cho đến khi hai bên khớp nhau.'
      : '';
  }

  async function save(e) {
    e.preventDefault();
    const payload = {
      productId: toInt(el.vid.value),
      defaultCode: el.part.value.trim(),
      name: el.name.value.trim(),
      scanSource: el.scan.value,
      barcode: el.scan.value === 'LotId' ? '' : el.barcode.value.trim(),
      pcsPerBox: toInt(el.pcs.value),
      viindooQtyPerCode: state.viindooQty,
      outerType: el.outer.value || null,
      boxPerOuter: el.outer.value ? toInt(el.boxPerOuter.value) : null
    };

    const invalid = [
      [el.vid, !(payload.productId > 0)],
      [el.part, !payload.defaultCode],
      [el.barcode, payload.scanSource !== 'LotId' && !payload.barcode],
      [el.pcs, !(payload.pcsPerBox > 0)],
      [el.boxPerOuter, !!payload.outerType && !(payload.boxPerOuter > 0)]
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
      title: 'Xoá sản phẩm?',
      html: 'Quy cách của <strong>' + esc(r.defaultCode) + '</strong> sẽ bị xoá vĩnh viễn.',
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

  el.search.addEventListener('input', render);
  el.filterOuter.addEventListener('change', render);
  $('pd-add').addEventListener('click', openCreate);
  $('pd-reload').addEventListener('click', function () {
    el.search.value = '';
    el.filterOuter.value = '';
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
  el.fetch.addEventListener('click', function () { fetchFromViindoo(true); });
  el.part.addEventListener('keydown', function (e) {
    if (e.key === 'Enter') { e.preventDefault(); fetchFromViindoo(false); }
  });
  el.part.addEventListener('change', function () { fetchFromViindoo(false); });
  el.woToggle.addEventListener('click', function () { showWoBox(el.woBox.classList.contains('d-none')); });
  el.woFetch.addEventListener('click', fetchCodeFromWo);
  el.wo.addEventListener('keydown', function (e) {
    if (e.key === 'Enter') { e.preventDefault(); fetchCodeFromWo(); }
  });
  el.scan.addEventListener('change', updateScan);
  el.outer.addEventListener('change', updateOuter);
  el.pcs.addEventListener('input', updateCompare);
  el.form.addEventListener('input', function (e) { e.target.classList.remove('is-invalid'); });
  el.form.addEventListener('submit', save);

  loadList();
})();
