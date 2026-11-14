/**
 * Quản lý mẫu tem - /toast/admin/label-template (chuyển từ Twist: SVN-Portal/wwwroot/js/twist-admin/label-template.js)
 * Danh sách lọc + phân trang phía client (bảng nhỏ, tải 1 lần), Thêm/Sửa qua modal, Xoá qua SweetAlert2.
 */
(function () {
  'use strict';

  const cfg = window.LabelTemplatePage;
  const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10 MB
  const notyf = new Notyf({ duration: 3000, position: { x: 'right', y: 'top' }, dismissible: true });
  const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

  const el = {
    summary: document.getElementById('lt-summary'),
    search: document.getElementById('lt-search'),
    filterType: document.getElementById('lt-filter-type'),
    filterSize: document.getElementById('lt-filter-size'),
    tbody: document.getElementById('lt-tbody'),
    info: document.getElementById('lt-info'),
    pagination: document.getElementById('lt-pagination'),
    pageSize: document.getElementById('lt-page-size'),
    modal: document.getElementById('lt-modal'),
    modalTitle: document.getElementById('lt-modal-title'),
    form: document.getElementById('lt-form'),
    part: document.getElementById('lt-part'),
    type: document.getElementById('lt-type'),
    operation: document.getElementById('lt-operation'),
    size: document.getElementById('lt-size'),
    dpi: document.getElementById('lt-dpi'),
    vars: document.getElementById('lt-vars'),
    zpl: document.getElementById('lt-zpl'),
    zplCount: document.getElementById('lt-zpl-count'),
    save: document.getElementById('lt-save'),
    file: document.getElementById('lt-file'),
    fileName: document.getElementById('lt-file-name'),
    dropzone: document.getElementById('lt-dropzone'),
    copy: document.getElementById('lt-copy'),
    downloadZpl: document.getElementById('lt-download-zpl'),
    viewModal: document.getElementById('lt-view-modal'),
    viewSubtitle: document.getElementById('lt-view-subtitle'),
    viewEdit: document.getElementById('lt-view-edit'),
    viewPrint: document.getElementById('lt-view-print')
  };
  const modal = new bootstrap.Modal(el.modal);
  const viewModal = new bootstrap.Modal(el.viewModal);

  // sort.key: null = giữ thứ tự server trả về (defaultCode, type, operation)
  // varValues: giá trị biến đang nhập trong form, key viết HOA. varKeys: danh sách biến đang hiển thị
  const state = { rows: [], page: 1, editingId: null, viewing: null, sort: { key: null, dir: 'asc' }, varValues: {}, varKeys: [] };
  // DPI lưu trong mẫu -> dots/mm cho khung xem trước
  const DPMM_BY_DPI = { 152: 6, 203: 8, 300: 12, 600: 24 };
  const defaults = {
    type: el.type.value,
    operation: el.operation.value,
    size: el.size.value,
    dpi: el.dpi.value
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
    if (!res.ok) {
      throw new Error('Lỗi máy chủ (' + res.status + ').');
    }
    return res.json();
  }

  async function loadList() {
    el.tbody.innerHTML = '<tr><td colspan="9" class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></td></tr>';
    try {
      const res = await api(cfg.apiUrl, { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      state.rows = res.data;
      el.summary.textContent = state.rows.length + ' mẫu tem';
      render();
    } catch (err) {
      el.summary.textContent = '';
      el.tbody.innerHTML = '<tr><td colspan="9" class="text-center text-danger py-5">' + escapeHtml(err.message) + '</td></tr>';
    }
  }

  // ===== Danh sách =====

  function filteredRows() {
    const q = el.search.value.trim().toLowerCase();
    const type = el.filterType.value;
    const size = el.filterSize.value;
    const rows = state.rows.filter(function (r) {
      return (!q || (r.defaultCode || '').toLowerCase().includes(q))
        && (!type || r.type === type)
        && (!size || r.size === size);
    });
    return sortRows(rows);
  }

  // ===== Sắp xếp =====

  const sortValue = {
    id: function (r) { return r.id; },
    defaultCode: function (r) { return (r.defaultCode || '').toLowerCase(); },
    updated: function (r) { return new Date(r.updatedAt || r.createdAt).getTime() || 0; }
  };

  function sortRows(rows) {
    const key = state.sort.key;
    if (!key) return rows;
    const get = sortValue[key];
    const dir = state.sort.dir === 'asc' ? 1 : -1;
    return rows.slice().sort(function (a, b) {
      const va = get(a), vb = get(b);
      if (va < vb) return -dir;
      if (va > vb) return dir;
      return a.id - b.id;
    });
  }

  // Bấm lần 1: tăng dần, lần 2: giảm dần, lần 3: bỏ sắp xếp
  function toggleSort(key) {
    if (state.sort.key !== key) {
      state.sort = { key: key, dir: 'asc' };
    } else if (state.sort.dir === 'asc') {
      state.sort.dir = 'desc';
    } else {
      state.sort = { key: null, dir: 'asc' };
    }
    state.page = 1;
    updateSortHeaders();
    render();
  }

  function updateSortHeaders() {
    document.querySelectorAll('.lt-sort').forEach(function (btn) {
      const active = btn.dataset.sort === state.sort.key;
      const icon = !active ? 'tabler-arrows-sort' : (state.sort.dir === 'asc' ? 'tabler-sort-ascending' : 'tabler-sort-descending');
      btn.classList.toggle('is-active', active);
      btn.querySelector('i').className = 'icon-base ti ' + icon;
      btn.parentElement.setAttribute('aria-sort', !active ? 'none' : (state.sort.dir === 'asc' ? 'ascending' : 'descending'));
    });
  }

  function render() {
    const rows = filteredRows();
    const pageSize = parseInt(el.pageSize.value, 10);
    const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
    state.page = Math.min(state.page, totalPages);
    const start = (state.page - 1) * pageSize;
    const pageRows = rows.slice(start, start + pageSize);

    if (pageRows.length === 0) {
      el.tbody.innerHTML = '<tr><td colspan="9" class="text-center text-muted py-5">Không có mẫu tem nào</td></tr>';
    } else {
      el.tbody.innerHTML = pageRows.map(rowHtml).join('');
    }

    el.info.textContent = rows.length === 0
      ? 'Không có bản ghi'
      : 'Hiển thị ' + (start + 1) + '–' + (start + pageRows.length) + ' / ' + rows.length + ' bản ghi';
    renderPagination(totalPages);
  }

  function rowHtml(r) {
    const typeClass = r.type === 'Carton' ? 'bg-label-info' : r.type === 'Plate' ? 'bg-label-warning' : 'bg-label-primary';
    const mm = cfg.sizes[r.size];
    const zpl = (r.zplData || '').replace(/\s+/g, ' ');
    const vars = r.variables || [];
    const filled = vars.filter(function (v) { return v.value; }).length;
    const lockTitle = r.isLocked ? 'Đang khoá, bấm để mở khoá' : 'Bấm để khoá (chặn sửa / xoá)';
    const lockedAttr = r.isLocked ? ' disabled title="Mẫu đang khoá"' : '';
    return '<tr>' +
      '<td class="text-muted">' + r.id + '</td>' +
      '<td class="fw-medium text-heading">' + (r.isLocked ? '<i class="icon-base ti tabler-lock text-warning me-1" title="Đang khoá"></i>' : '') + escapeHtml(r.defaultCode) + '</td>' +
      '<td><span class="badge ' + typeClass + '">' + escapeHtml(r.type) + '</span></td>' +
      '<td>' + escapeHtml(r.operation) + '</td>' +
      '<td><span class="fw-semibold text-heading">' + escapeHtml(r.size) + '</span>' + (mm ? ' <span class="text-muted small">(' + escapeHtml(mm) + ')</span>' : '') + '<div class="text-muted small">' + r.dpi + ' dpi</div></td>' +
      '<td class="small" title="' + escapeHtml(vars.map(function (v) { return v.key + ' = ' + (v.value || '(điền lúc in)'); }).join('\n')) + '">' +
        (vars.length ? filled + ' / ' + vars.length + ' đã nhập' : '<span class="text-muted">—</span>') + '</td>' +
      '<td class="text-muted small text-truncate lt-zpl-preview" title="' + escapeHtml(zpl) + '">' + escapeHtml(zpl) + '</td>' +
      '<td class="small" title="' + formatDate(r.updatedAt || r.createdAt) + '">' + formatRelativeDate(r.updatedAt || r.createdAt) + '</td>' +
      '<td><div class="d-flex gap-1">' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-info" data-action="view" data-id="' + r.id + '" aria-label="Xem tem" title="Xem tem"><i class="icon-base ti tabler-eye"></i></button>' +
        '<a href="' + printUrl(r.id) + '" class="btn btn-sm btn-icon btn-label-success" aria-label="In tem" title="In tem"><i class="icon-base ti tabler-printer"></i></a>' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-secondary" data-action="clone" data-id="' + r.id + '" aria-label="Nhân bản" title="Nhân bản thành mẫu mới"><i class="icon-base ti tabler-copy"></i></button>' +
        '<button type="button" class="btn btn-sm btn-icon ' + (r.isLocked ? 'btn-warning' : 'btn-label-warning') + '" data-action="lock" data-id="' + r.id + '" aria-label="' + lockTitle + '" title="' + lockTitle + '"><i class="icon-base ti ' + (r.isLocked ? 'tabler-lock' : 'tabler-lock-open') + '"></i></button>' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-primary" data-action="edit" data-id="' + r.id + '" aria-label="Sửa"' + (r.isLocked ? lockedAttr : ' title="Sửa"') + '><i class="icon-base ti tabler-edit"></i></button>' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-danger" data-action="delete" data-id="' + r.id + '" aria-label="Xoá"' + (r.isLocked ? lockedAttr : ' title="Xoá"') + '><i class="icon-base ti tabler-trash"></i></button>' +
      '</div></td>' +
      '</tr>';
  }

  function renderPagination(totalPages) {
    const p = state.page;
    const pages = [];
    for (let i = Math.max(1, p - 2); i <= Math.min(totalPages, p + 2); i++) pages.push(i);

    let html = pageItem(p - 1, '<i class="icon-base ti tabler-chevron-left"></i>', p === 1, false, 'Trang trước');
    pages.forEach(function (i) { html += pageItem(i, String(i), false, i === p); });
    html += pageItem(p + 1, '<i class="icon-base ti tabler-chevron-right"></i>', p === totalPages, false, 'Trang sau');
    el.pagination.innerHTML = html;
  }

  function pageItem(page, label, disabled, active, ariaLabel) {
    return '<li class="page-item' + (disabled ? ' disabled' : '') + (active ? ' active' : '') + '">' +
      '<a class="page-link" href="#" data-page="' + page + '"' + (ariaLabel ? ' aria-label="' + ariaLabel + '"' : '') + '>' + label + '</a></li>';
  }

  // ===== Thêm / Sửa =====

  function openCreate() {
    state.editingId = null;
    el.modalTitle.textContent = 'Thêm mẫu tem';
    el.form.reset();
    removeLegacyOptions();
    el.type.value = defaults.type;
    el.operation.value = defaults.operation;
    el.size.value = defaults.size;
    el.dpi.value = defaults.dpi;
    setVarValues([]);
    setFileName(null);
    updateZplCount();
    editPreview.reset();
    editPreview.refresh();
    modal.show();
  }

  function openEdit(id) { return openForm(id, false); }

  // Nhân bản: mở form Thêm với ZPL + biến của mẫu cũ, mã sản phẩm để trống để nhập mới
  function openClone(id) { return openForm(id, true); }

  async function openForm(id, clone) {
    try {
      const res = await api(cfg.apiUrl + '/' + id, { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      const d = res.data;
      if (!clone && d.isLocked) throw new Error('Mẫu tem đang khoá. Mở khoá trước khi sửa.');
      state.editingId = clone ? null : d.id;
      el.modalTitle.textContent = clone ? 'Nhân bản từ mẫu #' + d.id + ' (' + d.defaultCode + ')' : 'Sửa mẫu tem #' + d.id;
      el.form.reset();
      removeLegacyOptions();
      el.part.value = clone ? '' : d.defaultCode || '';
      setSelectValue(el.type, d.type);
      setSelectValue(el.operation, d.operation);
      setSelectValue(el.size, d.size);
      el.dpi.value = String(d.dpi || defaults.dpi);
      el.zpl.value = d.zplData || '';
      setVarValues(d.variables);
      setFileName(null);
      updateZplCount();
      editPreview.reset();
      editPreview.refresh();
      modal.show();
    } catch (err) {
      notyf.error(err.message);
    }
  }

  // ===== Xem tem =====

  async function openView(id) {
    try {
      const res = await api(cfg.apiUrl + '/' + id, { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      state.viewing = res.data;
      el.viewSubtitle.textContent = [res.data.defaultCode, res.data.type, res.data.operation, res.data.size, res.data.dpi + ' dpi'].join(' · ') + (res.data.isLocked ? ' · Đang khoá' : '');
      el.viewEdit.disabled = res.data.isLocked;
      el.viewPrint.href = printUrl(res.data.id);
      viewPreview.reset();
      viewPreview.refresh();
      viewModal.show();
    } catch (err) {
      notyf.error(err.message);
    }
  }


  // Khung xem trước: wwwroot/js/label-template/zpl-preview.js
  const editPreview = ZplPreview.create('lt-edit-preview', {
    previewUrl: cfg.apiUrl + '/preview',
    request: api,
    getSource: function () {
      return { zplData: el.zpl.value, size: el.size.value, dpmm: DPMM_BY_DPI[el.dpi.value], variables: collectVars(), fileName: fileBaseName(el.part.value, el.type.value, el.operation.value) };
    }
  });

  const viewPreview = ZplPreview.create('lt-view-preview', {
    previewUrl: cfg.apiUrl + '/preview',
    request: api,
    getSource: function () {
      const v = state.viewing || {};
      return { zplData: v.zplData || '', size: v.size, dpmm: DPMM_BY_DPI[v.dpi], variables: v.variables || [], fileName: fileBaseName(v.defaultCode, v.type, v.operation) };
    }
  });

  // ===== Copy / tải ZPL =====

  async function copyZpl() {
    const text = el.zpl.value;
    if (!text) { notyf.error('Chưa có nội dung ZPL'); return; }
    try {
      // navigator.clipboard chỉ chạy trên https hoặc localhost, truy cập bằng IP nội bộ qua http thì dùng cách cũ
      if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(text);
      } else {
        el.zpl.select();
        if (!document.execCommand('copy')) throw new Error();
        el.zpl.setSelectionRange(0, 0);
      }
      notyf.success('Đã copy nội dung ZPL');
    } catch (err) {
      notyf.error('Không copy được, hãy bôi đen rồi Ctrl+C');
    }
  }

  function downloadZpl() {
    const text = el.zpl.value;
    if (!text) { notyf.error('Chưa có nội dung ZPL'); return; }
    const url = URL.createObjectURL(new Blob([text], { type: 'text/plain;charset=utf-8' }));
    downloadUrl(url, fileBaseName(el.part.value, el.type.value, el.operation.value) + '.zpl');
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  }

  function downloadUrl(url, fileName) {
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
  }

  // Mở màn In tem với mẫu này được chọn sẵn
  function printUrl(id) {
    return cfg.printUrl + '?templateId=' + encodeURIComponent(id);
  }

  // Tên file: PN-000415_Box_Toast (bỏ ký tự không hợp lệ trong tên file)
  function fileBaseName(part, type, operation) {
    const name = [part, type, operation].filter(Boolean).join('_').replace(/[^\w.-]+/g, '-');
    return name || 'label';
  }

  // Giá trị cũ trong DB không có trong file cấu hình: thêm tạm vào dropdown để không bị mất khi sửa
  function setSelectValue(select, value) {
    value = value || '';
    if (value && !Array.from(select.options).some(function (o) { return o.value === value; })) {
      const opt = new Option(value + ' (giá trị cũ)', value);
      opt.dataset.legacy = '1';
      select.add(opt);
    }
    select.value = value;
  }

  function removeLegacyOptions() {
    el.form.querySelectorAll('option[data-legacy]').forEach(function (o) { o.remove(); });
  }

  async function save(e) {
    e.preventDefault();
    const payload = {
      defaultCode: el.part.value.trim(),
      type: el.type.value,
      operation: el.operation.value,
      size: el.size.value,
      dpi: parseInt(el.dpi.value, 10),
      zplData: el.zpl.value,
      variables: collectVars()
    };

    const missing = [
      [el.part, !payload.defaultCode],
      [el.size, !payload.size],
      [el.zpl, !payload.zplData.trim()]
    ];
    missing.forEach(function (m) { m[0].classList.toggle('is-invalid', m[1]); });
    if (missing.some(function (m) { return m[1]; })) {
      notyf.error('Vui lòng nhập đủ các trường bắt buộc');
      return;
    }

    setSaving(true);
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
      setSaving(false);
    }
  }

  function setSaving(saving) {
    el.save.disabled = saving;
    el.save.innerHTML = saving
      ? '<span class="spinner-border spinner-border-sm me-1"></span>Đang lưu...'
      : '<i class="icon-base ti tabler-device-floppy me-1"></i>Lưu';
  }

  // ===== Xoá =====

  async function confirmDelete(id) {
    const r = state.rows.find(function (x) { return x.id === id; });
    if (!r) return;
    if (r.isLocked) { notyf.error('Mẫu tem đang khoá. Mở khoá trước khi xoá.'); return; }
    const result = await Swal.fire({
      title: 'Xoá mẫu tem?',
      html: 'Mẫu tem <strong>' + escapeHtml(r.defaultCode + ' · ' + r.type + ' · ' + r.operation) + '</strong> sẽ bị xoá vĩnh viễn.',
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

  // ===== Tải file ZPL =====

  async function loadFile(file) {
    if (!file) return;
    if (!/\.(zpl|prn|txt)$/i.test(file.name)) {
      notyf.error('Chỉ nhận file .zpl, .prn hoặc .txt');
      return;
    }
    if (file.size > MAX_FILE_SIZE) {
      notyf.error('File quá lớn (tối đa 10 MB)');
      return;
    }
    if (el.zpl.value.trim()) {
      const result = await Swal.fire({
        title: 'Thay nội dung ZPL?',
        text: 'Nội dung hiện tại trong ô ZPL sẽ được thay bằng nội dung file ' + file.name + '.',
        icon: 'question',
        showCancelButton: true,
        confirmButtonText: 'Thay',
        cancelButtonText: 'Huỷ',
        buttonsStyling: false,
        customClass: { confirmButton: 'btn btn-primary me-2', cancelButton: 'btn btn-label-secondary' }
      });
      if (!result.isConfirmed) return;
    }
    el.zpl.value = await file.text();
    el.zpl.classList.remove('is-invalid');
    setFileName(file);
    updateZplCount();
    renderVars();
    editPreview.refresh();
  }

  function setFileName(file) {
    el.fileName.classList.toggle('d-none', !file);
    el.fileName.textContent = file ? file.name + ' · ' + (file.size / 1024).toFixed(1) + ' KB' : '';
  }

  function updateZplCount() {
    const text = el.zpl.value;
    const lines = text ? text.split(/\r?\n/).length : 0;
    el.zplCount.textContent = lines + ' dòng · ' + text.length + ' ký tự';
  }

  // ===== Biến {KEY} =====

  // Giống server (ZplVariables.ExtractKeys): theo thứ tự xuất hiện, không trùng, không phân biệt hoa thường
  function extractKeys(zpl) {
    const keys = [];
    const re = /\{([A-Za-z_][A-Za-z0-9_]*)\}/g;
    let m;
    while ((m = re.exec(zpl || '')) !== null) {
      if (!keys.some(function (k) { return k.toUpperCase() === m[1].toUpperCase(); })) keys.push(m[1]);
    }
    return keys;
  }

  function setVarValues(list) {
    state.varValues = {};
    (list || []).forEach(function (v) { state.varValues[v.key.toUpperCase()] = v.value || ''; });
    state.varKeys = [];
    renderVars(true); // mở form mới: luôn vẽ lại, không giữ ô biến của mẫu mở trước
  }

  function collectVars() {
    return extractKeys(el.zpl.value).map(function (k) { return { key: k, value: (state.varValues[k.toUpperCase()] || '').trim() }; });
  }

  // Vẽ lại ô nhập khi danh sách biến trong ZPL thay đổi (giá trị đã nhập được giữ lại); force = luôn vẽ lại (lúc mở form)
  function renderVars(force) {
    const keys = extractKeys(el.zpl.value);
    if (!force && keys.join('|') === state.varKeys.join('|') && el.vars.childElementCount) return;
    state.varKeys = keys;
    if (keys.length === 0) {
      el.vars.innerHTML = '<div class="col-12 text-muted small">ZPL chưa có biến nào dạng <code>{TEN_BIEN}</code>.</div>';
      return;
    }
    el.vars.innerHTML = keys.map(function (k) {
      const id = 'lt-var-' + k;
      return '<div class="col-md-6">' +
        '<label class="form-label" for="' + id + '"><code>{' + escapeHtml(k) + '}</code></label>' +
        '<input type="text" class="form-control" id="' + id + '" data-var-key="' + escapeHtml(k) + '" value="' + escapeHtml(state.varValues[k.toUpperCase()] || '') + '" placeholder="Để trống = điền lúc in" />' +
        '</div>';
    }).join('');
  }

  // ===== Khoá =====

  async function toggleLock(id) {
    const r = state.rows.find(function (x) { return x.id === id; });
    if (!r) return;
    try {
      const res = await api(cfg.apiUrl + '/' + id + '/lock', { method: 'POST', body: JSON.stringify({ locked: !r.isLocked }) });
      if (!res.success) throw new Error(res.error);
      notyf.success(res.message);
      await loadList();
    } catch (err) {
      notyf.error(err.message);
    }
  }

  // ===== Tiện ích =====

  function escapeHtml(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function formatDate(value) {
    if (!value) return '—';
    const d = new Date(value);
    if (isNaN(d)) return '—';
    const pad = function (n) { return String(n).padStart(2, '0'); };
    return pad(d.getDate()) + '/' + pad(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + pad(d.getHours()) + ':' + pad(d.getMinutes());
  }

  // Trong vòng 1 ngày: "10 giây trước", "5 phút trước", "3 giờ trước". Quá 1 ngày: ngày giờ đầy đủ.
  function formatRelativeDate(value) {
    if (!value) return '—';
    const d = new Date(value);
    if (isNaN(d)) return '—';
    const seconds = Math.floor((Date.now() - d.getTime()) / 1000);
    if (seconds < 0 || seconds >= 86400) return formatDate(value);
    if (seconds < 5) return 'Vừa xong';
    if (seconds < 60) return seconds + ' giây trước';
    if (seconds < 3600) return Math.floor(seconds / 60) + ' phút trước';
    return Math.floor(seconds / 3600) + ' giờ trước';
  }

  // ===== Sự kiện =====

  el.search.addEventListener('input', function () { state.page = 1; render(); });
  el.filterType.addEventListener('change', function () { state.page = 1; render(); });
  el.filterSize.addEventListener('change', function () { state.page = 1; render(); });
  el.pageSize.addEventListener('change', function () { state.page = 1; render(); });
  // Tải lại: xoá ô tìm kiếm + bộ lọc, về trang 1 rồi tải lại dữ liệu
  document.getElementById('lt-reload').addEventListener('click', function () {
    el.search.value = '';
    el.filterType.value = '';
    el.filterSize.value = '';
    state.page = 1;
    loadList();
  });
  document.getElementById('lt-add').addEventListener('click', openCreate);

  el.pagination.addEventListener('click', function (e) {
    const a = e.target.closest('a[data-page]');
    if (!a) return;
    e.preventDefault();
    if (a.parentElement.classList.contains('disabled')) return;
    state.page = parseInt(a.dataset.page, 10);
    render();
  });

  el.tbody.addEventListener('click', function (e) {
    const btn = e.target.closest('button[data-action]');
    if (!btn) return;
    const id = parseInt(btn.dataset.id, 10);
    if (btn.dataset.action === 'view') openView(id);
    else if (btn.dataset.action === 'edit') openEdit(id);
    else if (btn.dataset.action === 'clone') openClone(id);
    else if (btn.dataset.action === 'lock') toggleLock(id);
    else if (btn.dataset.action === 'delete') confirmDelete(id);
  });
  // Bấm 1 lần vào dòng = Xem tem (bôi đen / double-click thì không mở)
  window.ToastRowClick.button(el.tbody, '[data-action="view"]');

  el.form.addEventListener('submit', save);
  el.form.addEventListener('input', function (e) { e.target.classList.remove('is-invalid'); });
  el.form.addEventListener('change', function (e) { e.target.classList.remove('is-invalid'); });
  el.zpl.addEventListener('input', function () {
    updateZplCount();
    renderVars();
    editPreview.refreshDebounced();
  });
  el.size.addEventListener('change', function () { editPreview.refresh(); });
  el.dpi.addEventListener('change', function () { editPreview.reset(); editPreview.refresh(); });
  el.vars.addEventListener('input', function (e) {
    const key = e.target.dataset.varKey;
    if (!key) return;
    state.varValues[key.toUpperCase()] = e.target.value;
    editPreview.refreshDebounced();
  });
  el.copy.addEventListener('click', copyZpl);
  el.downloadZpl.addEventListener('click', downloadZpl);
  el.viewEdit.addEventListener('click', function () {
    if (!state.viewing) return;
    viewModal.hide();
    openEdit(state.viewing.id);
  });

  document.getElementById('lt-upload').addEventListener('click', function () { el.file.click(); });
  el.file.addEventListener('change', function () {
    loadFile(el.file.files[0]);
    el.file.value = '';
  });

  ['dragenter', 'dragover'].forEach(function (name) {
    el.dropzone.addEventListener(name, function (e) {
      e.preventDefault();
      el.dropzone.classList.add('is-dragover');
    });
  });
  ['dragleave', 'drop'].forEach(function (name) {
    el.dropzone.addEventListener(name, function (e) {
      e.preventDefault();
      el.dropzone.classList.remove('is-dragover');
    });
  });
  el.dropzone.addEventListener('drop', function (e) {
    loadFile(e.dataTransfer.files[0]);
  });

  document.querySelectorAll('.lt-sort').forEach(function (btn) {
    btn.addEventListener('click', function () { toggleSort(btn.dataset.sort); });
  });

  loadList();

  // Cập nhật lại chữ "x phút trước" mỗi 30 giây (chỉ vẽ lại bảng, không gọi server)
  setInterval(function () {
    if (state.rows.length && !document.hidden) render();
  }, 30000);
})();
