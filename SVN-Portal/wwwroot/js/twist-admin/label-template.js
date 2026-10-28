/**
 * Quản lý mẫu tem - /twist/admin/label-template
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
    viewEdit: document.getElementById('lt-view-edit')
  };
  const modal = new bootstrap.Modal(el.modal);
  const viewModal = new bootstrap.Modal(el.viewModal);

  // sort.key: null = giữ thứ tự server trả về (partNumber, type, operation)
  const state = { rows: [], page: 1, editingId: null, viewing: null, sort: { key: null, dir: 'asc' } };
  const defaults = {
    type: el.type.value,
    operation: el.operation.value,
    size: el.size.value
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
    el.tbody.innerHTML = '<tr><td colspan="8" class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></td></tr>';
    try {
      const res = await api(cfg.apiUrl, { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      state.rows = res.data;
      el.summary.textContent = state.rows.length + ' mẫu tem';
      render();
    } catch (err) {
      el.summary.textContent = '';
      el.tbody.innerHTML = '<tr><td colspan="8" class="text-center text-danger py-5">' + escapeHtml(err.message) + '</td></tr>';
    }
  }

  // ===== Danh sách =====

  function filteredRows() {
    const q = el.search.value.trim().toLowerCase();
    const type = el.filterType.value;
    const size = el.filterSize.value;
    const rows = state.rows.filter(function (r) {
      return (!q || (r.partNumber || '').toLowerCase().includes(q))
        && (!type || r.type === type)
        && (!size || r.size === size);
    });
    return sortRows(rows);
  }

  // ===== Sắp xếp =====

  const sortValue = {
    id: function (r) { return r.id; },
    partNumber: function (r) { return (r.partNumber || '').toLowerCase(); },
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
      el.tbody.innerHTML = '<tr><td colspan="8" class="text-center text-muted py-5">Không có mẫu tem nào</td></tr>';
    } else {
      el.tbody.innerHTML = pageRows.map(rowHtml).join('');
    }

    el.info.textContent = rows.length === 0
      ? 'Không có bản ghi'
      : 'Hiển thị ' + (start + 1) + '–' + (start + pageRows.length) + ' / ' + rows.length + ' bản ghi';
    renderPagination(totalPages);
  }

  function rowHtml(r) {
    const typeClass = r.type === 'Carton' ? 'bg-label-info' : 'bg-label-primary';
    const mm = cfg.sizes[r.size];
    const zpl = (r.zplData || '').replace(/\s+/g, ' ');
    return '<tr>' +
      '<td class="text-muted">' + r.id + '</td>' +
      '<td class="fw-medium text-heading">' + escapeHtml(r.partNumber) + '</td>' +
      '<td><span class="badge ' + typeClass + '">' + escapeHtml(r.type) + '</span></td>' +
      '<td>' + escapeHtml(r.operation) + '</td>' +
      '<td><span class="fw-semibold text-heading">' + escapeHtml(r.size) + '</span>' + (mm ? ' <span class="text-muted small">(' + escapeHtml(mm) + ')</span>' : '') + '</td>' +
      '<td class="text-muted small text-truncate lt-zpl-preview" title="' + escapeHtml(zpl) + '">' + escapeHtml(zpl) + '</td>' +
      '<td class="small" title="' + formatDate(r.updatedAt || r.createdAt) + '">' + formatRelativeDate(r.updatedAt || r.createdAt) + '</td>' +
      '<td><div class="d-flex gap-1">' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-info" data-action="view" data-id="' + r.id + '" aria-label="Xem tem" title="Xem tem"><i class="icon-base ti tabler-eye"></i></button>' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-primary" data-action="edit" data-id="' + r.id + '" aria-label="Sửa" title="Sửa"><i class="icon-base ti tabler-edit"></i></button>' +
        '<button type="button" class="btn btn-sm btn-icon btn-label-danger" data-action="delete" data-id="' + r.id + '" aria-label="Xoá" title="Xoá"><i class="icon-base ti tabler-trash"></i></button>' +
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
    setFileName(null);
    updateZplCount();
    editPreview.reset();
    editPreview.refresh();
    modal.show();
  }

  async function openEdit(id) {
    try {
      const res = await api(cfg.apiUrl + '/' + id, { method: 'GET' });
      if (!res.success) throw new Error(res.error);
      const d = res.data;
      state.editingId = d.id;
      el.modalTitle.textContent = 'Sửa mẫu tem #' + d.id;
      el.form.reset();
      removeLegacyOptions();
      el.part.value = d.partNumber || '';
      setSelectValue(el.type, d.type);
      setSelectValue(el.operation, d.operation);
      setSelectValue(el.size, d.size);
      el.zpl.value = d.zplData || '';
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
      el.viewSubtitle.textContent = [res.data.partNumber, res.data.type, res.data.operation, res.data.size].join(' · ');
      viewPreview.reset();
      viewPreview.refresh();
      viewModal.show();
    } catch (err) {
      notyf.error(err.message);
    }
  }

  // Khung xem trước hình tem. getSource() trả về { zplData, size, fileName }
  // Điều khiển: xoay 90°, phóng to/thu nhỏ (nút, thanh trượt, Ctrl + lăn chuột), Shift + lăn chuột cuộn ngang, kéo chuột để di chuyển
  function createPreview(rootId, getSource) {
    const ZOOM_MIN = 0.1, ZOOM_MAX = 8, ZOOM_STEP = 1.25, WHEEL_STEP = 1.1;

    const root = document.getElementById(rootId);
    const po = root.querySelector('[data-role="po"]');
    const dpmm = root.querySelector('[data-role="dpmm"]');
    const stage = root.querySelector('[data-role="stage"]');
    const canvas = root.querySelector('[data-role="canvas"]');
    const note = root.querySelector('[data-role="note"]');
    const download = root.querySelector('[data-role="download"]');
    const zoomRange = root.querySelector('[data-role="zoom-range"]');
    const zoomLabel = root.querySelector('[data-role="zoom-label"]');
    let requestNo = 0, timer = null, images = [];
    // zoom: 1 = vừa khung (hiển thị 100%). Giữ nguyên khi vẽ lại lúc đang sửa ZPL.
    const view = { zoom: 1, rotation: 0 };

    function showMessage(icon, text, cls) {
      canvas.innerHTML = '<div class="small text-center ' + (cls || 'text-muted') + '"><i class="icon-base ti ' + icon + ' d-block mb-1" style="font-size: 2rem;"></i>' + escapeHtml(text) + '</div>';
    }

    async function refresh() {
      clearTimeout(timer);
      const src = getSource();
      const myNo = ++requestNo;
      images = [];
      download.disabled = true;
      note.textContent = '';
      if (!src.zplData.trim()) {
        root.classList.remove('is-loading');
        showMessage('tabler-photo', 'Chưa có nội dung ZPL');
        return;
      }
      root.classList.add('is-loading');
      if (!canvas.querySelector('img')) {
        canvas.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
      }
      try {
        const res = await api(cfg.apiUrl + '/preview', {
          method: 'POST',
          body: JSON.stringify({ zplData: src.zplData, size: src.size, po: po.value.trim(), dpmm: parseInt(dpmm.value, 10) })
        });
        if (myNo !== requestNo) return; // đã có yêu cầu mới hơn
        if (!res.success) throw new Error(res.error);
        images = res.images;
        canvas.innerHTML = images.map(function (b64, i) {
          return '<figure><div class="lt-preview-item"><img src="data:image/png;base64,' + b64 + '" alt="Hình tem ' + (i + 1) + '" draggable="false" /></div>' +
            (images.length > 1 ? '<figcaption class="small text-muted">Tem ' + (i + 1) + ' / ' + res.totalLabels + '</figcaption>' : '') +
            '</figure>';
        }).join('');
        await Promise.all(Array.from(canvas.querySelectorAll('img')).map(function (img) {
          return img.decode ? img.decode().catch(function () { }) : Promise.resolve();
        }));
        if (myNo !== requestNo) return;
        layout();
        download.disabled = false;
        const notes = [
          'Đang hiển thị theo ' + DPI_LABEL[res.dpmm] + ' (' + res.dpmm + ' dots/mm), khổ ' + res.widthMm.toFixed(1) + ' × ' + res.heightMm.toFixed(1) + ' mm ' + res.sizeSource + '.',
          'Bản xem gần đúng, font có thể khác máy in thật.'
        ];
        if (res.totalLabels > images.length) notes.push('Chỉ hiển thị ' + images.length + ' / ' + res.totalLabels + ' tem.');
        if (res.unsupportedCommands.length) notes.push('Lệnh chưa vẽ được: ' + res.unsupportedCommands.join(', ') + '.');
        note.innerHTML = (res.sizeWarning
          ? '<div class="alert alert-warning d-flex gap-2 py-2 px-3 mb-1" role="alert"><i class="icon-base ti tabler-alert-triangle flex-shrink-0 mt-1"></i><span>' + escapeHtml(res.sizeWarning) + '</span></div>'
          : '') + escapeHtml(notes.join(' '));
      } catch (err) {
        if (myNo !== requestNo) return;
        showMessage('tabler-alert-triangle', err.message, 'text-danger');
      } finally {
        if (myNo === requestNo) root.classList.remove('is-loading');
      }
    }

    function refreshDebounced() {
      clearTimeout(timer);
      timer = setTimeout(refresh, 800);
    }

    // ===== Xoay / phóng to =====

    // Tỉ lệ để tem (đã xoay) vừa khít khung nhìn
    function fitScale(img) {
      const pad = 32;
      const rotated = view.rotation % 180 !== 0;
      const w = rotated ? img.naturalHeight : img.naturalWidth;
      const h = rotated ? img.naturalWidth : img.naturalHeight;
      const availW = Math.max(stage.clientWidth - pad, 50);
      const availH = Math.max(stage.clientHeight - pad, 50);
      return Math.min(availW / w, availH / h);
    }

    // Đặt kích thước từng ảnh theo zoom + góc xoay. Khung bao (.lt-preview-item) đổi chiều khi xoay 90°/270°.
    function layout() {
      const imgs = canvas.querySelectorAll('img');
      if (imgs.length && imgs[0].naturalWidth && stage.clientWidth) {
        const scale = fitScale(imgs[0]) * view.zoom;
        const rotated = view.rotation % 180 !== 0;
        imgs.forEach(function (img) {
          const w = img.naturalWidth * scale, h = img.naturalHeight * scale;
          img.style.width = w + 'px';
          img.style.height = h + 'px';
          img.style.transform = 'translate(-50%, -50%) rotate(' + view.rotation + 'deg)';
          img.parentElement.style.width = (rotated ? h : w) + 'px';
          img.parentElement.style.height = (rotated ? w : h) + 'px';
        });
      }
      const pct = Math.round(view.zoom * 100);
      zoomLabel.textContent = pct + '%';
      zoomRange.value = Math.min(Math.max(pct, zoomRange.min), zoomRange.max);
    }

    // anchor: điểm (trong khung nhìn) giữ đứng yên khi phóng to, mặc định là tâm khung
    function setZoom(zoom, anchor) {
      zoom = Math.min(Math.max(zoom, ZOOM_MIN), ZOOM_MAX);
      if (zoom === view.zoom) return;
      const ax = anchor ? anchor.x : stage.clientWidth / 2;
      const ay = anchor ? anchor.y : stage.clientHeight / 2;
      const ratio = zoom / view.zoom;
      const cx = stage.scrollLeft + ax, cy = stage.scrollTop + ay;
      view.zoom = zoom;
      layout();
      stage.scrollLeft = cx * ratio - ax;
      stage.scrollTop = cy * ratio - ay;
    }

    function rotate(deg) {
      view.rotation = (view.rotation + deg + 360) % 360;
      layout();
    }

    function fit() {
      view.zoom = 1;
      layout();
      stage.scrollLeft = 0;
      stage.scrollTop = 0;
    }

    // Mở rộng khung xem thành khung lớn nổi trên modal. Tem vẫn giữ mức zoom, "vừa khung" tự tính lại theo khung lớn (ResizeObserver).
    const expandBtn = root.querySelector('[data-role="expand"]');
    const parentModal = root.closest('.modal');
    // Nền tối phía sau khung mở rộng, bấm vào để thu lại (nằm trong modal nên không làm modal đóng theo)
    const backdrop = document.createElement('div');
    backdrop.className = 'lt-preview-backdrop';
    backdrop.hidden = true;
    root.parentNode.insertBefore(backdrop, root);
    backdrop.addEventListener('click', function () { setExpanded(false); });
    root.querySelector('[data-role="collapse"]').addEventListener('click', function () { setExpanded(false); });

    function setExpanded(expanded) {
      root.classList.toggle('is-expanded', expanded);
      backdrop.hidden = !expanded;
      if (parentModal) parentModal.classList.toggle('lt-preview-expanded', expanded);
      expandBtn.setAttribute('aria-pressed', String(expanded));
      expandBtn.querySelector('i').className = 'icon-base ti me-1 ' + (expanded ? 'tabler-arrows-minimize' : 'tabler-arrows-maximize');
      expandBtn.querySelector('span').textContent = expanded ? 'Thu nhỏ khung' : 'Mở rộng';
      if (expanded) stage.focus({ preventScroll: true });
    }
    expandBtn.addEventListener('click', function () { setExpanded(!root.classList.contains('is-expanded')); });

    // Esc khi đang mở rộng: chỉ thu khung lại, không để Bootstrap đóng luôn modal (bắt ở capture phase, chạy trước modal)
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && root.classList.contains('is-expanded')) {
        e.preventDefault();
        e.stopPropagation();
        setExpanded(false);
      }
    }, true);

    if (parentModal) parentModal.addEventListener('hidden.bs.modal', function () { setExpanded(false); });

    // Mở tem khác: về mặc định (PO trống, không xoay, vừa khung, không mở rộng)
    function reset() {
      setExpanded(false);
      po.value = '';
      view.zoom = 1;
      view.rotation = 0;
      stage.scrollLeft = 0;
      stage.scrollTop = 0;
      layout();
    }

    root.querySelector('[data-role="rotate-left"]').addEventListener('click', function () { rotate(-90); });
    root.querySelector('[data-role="rotate-right"]').addEventListener('click', function () { rotate(90); });
    root.querySelector('[data-role="zoom-in"]').addEventListener('click', function () { setZoom(view.zoom * ZOOM_STEP); });
    root.querySelector('[data-role="zoom-out"]').addEventListener('click', function () { setZoom(view.zoom / ZOOM_STEP); });
    root.querySelector('[data-role="fit"]').addEventListener('click', fit);
    zoomRange.addEventListener('input', function () { setZoom(parseInt(zoomRange.value, 10) / 100); });

    // Ctrl + lăn: phóng to quanh con trỏ (cũng bắt pinch trên touchpad). Shift + lăn: cuộn ngang. Lăn thường: cuộn dọc mặc định.
    stage.addEventListener('wheel', function (e) {
      if (e.ctrlKey) {
        e.preventDefault();
        const rect = stage.getBoundingClientRect();
        setZoom(view.zoom * (e.deltaY < 0 ? WHEEL_STEP : 1 / WHEEL_STEP), { x: e.clientX - rect.left, y: e.clientY - rect.top });
      } else if (e.shiftKey) {
        e.preventDefault();
        stage.scrollLeft += e.deltaY || e.deltaX;
      }
    }, { passive: false });

    // Kéo chuột để di chuyển
    let drag = null;
    stage.addEventListener('pointerdown', function (e) {
      if (e.button !== 0) return;
      drag = { x: e.clientX, y: e.clientY, left: stage.scrollLeft, top: stage.scrollTop, id: e.pointerId };
      stage.setPointerCapture(e.pointerId);
      stage.classList.add('is-dragging');
    });
    stage.addEventListener('pointermove', function (e) {
      if (!drag || e.pointerId !== drag.id) return;
      stage.scrollLeft = drag.left - (e.clientX - drag.x);
      stage.scrollTop = drag.top - (e.clientY - drag.y);
    });
    ['pointerup', 'pointercancel'].forEach(function (name) {
      stage.addEventListener(name, function (e) {
        if (!drag || e.pointerId !== drag.id) return;
        drag = null;
        stage.classList.remove('is-dragging');
      });
    });

    // Phím tắt khi khung xem đang được chọn: + / - phóng to/thu nhỏ, 0 vừa khung, R xoay phải
    stage.addEventListener('keydown', function (e) {
      if (e.key === '+' || e.key === '=') { e.preventDefault(); setZoom(view.zoom * ZOOM_STEP); }
      else if (e.key === '-') { e.preventDefault(); setZoom(view.zoom / ZOOM_STEP); }
      else if (e.key === '0') { e.preventDefault(); fit(); }
      else if (e.key === 'r' || e.key === 'R') { e.preventDefault(); rotate(90); }
    });

    // Khung nhìn đổi kích thước (mở modal, đổi cỡ cửa sổ): tính lại "vừa khung"
    new ResizeObserver(function () { layout(); }).observe(stage);

    // Nhớ DPI đã chọn cho lần sau (chỉ trên trình duyệt này)
    dpmm.value = loadDpmm();
    dpmm.addEventListener('change', function () {
      saveDpmm(dpmm.value);
      refresh();
    });
    po.addEventListener('input', refreshDebounced);
    po.addEventListener('keydown', function (e) {
      if (e.key === 'Enter') { e.preventDefault(); refresh(); }
    });
    root.querySelector('[data-role="refresh"]').addEventListener('click', refresh);
    download.addEventListener('click', function () {
      const base = getSource().fileName;
      images.forEach(function (b64, i) {
        downloadUrl('data:image/png;base64,' + b64, base + (images.length > 1 ? '_' + (i + 1) : '') + '.png');
      });
    });

    return { refresh: refresh, refreshDebounced: refreshDebounced, reset: reset };
  }

  const DPI_LABEL = { 6: '152 dpi', 8: '203 dpi', 12: '300 dpi', 24: '600 dpi' };
  const DPMM_STORAGE_KEY = 'twistAdmin.labelTemplate.dpmm';

  function loadDpmm() {
    try {
      const v = localStorage.getItem(DPMM_STORAGE_KEY);
      if (v && DPI_LABEL[v]) return v;
    } catch (e) { /* trình duyệt chặn localStorage: dùng mặc định */ }
    return '24';
  }

  function saveDpmm(v) {
    try { localStorage.setItem(DPMM_STORAGE_KEY, v); } catch (e) { /* bỏ qua */ }
  }

  const editPreview = createPreview('lt-edit-preview', function () {
    return { zplData: el.zpl.value, size: el.size.value, fileName: fileBaseName(el.part.value, el.type.value, el.operation.value) };
  });

  const viewPreview = createPreview('lt-view-preview', function () {
    const v = state.viewing || {};
    return { zplData: v.zplData || '', size: v.size, fileName: fileBaseName(v.partNumber, v.type, v.operation) };
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

  // Tên file: PN-000415_Box_Twist (bỏ ký tự không hợp lệ trong tên file)
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
      partNumber: el.part.value.trim(),
      type: el.type.value,
      operation: el.operation.value,
      size: el.size.value,
      zplData: el.zpl.value
    };

    const missing = [
      [el.part, !payload.partNumber],
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
    const result = await Swal.fire({
      title: 'Xoá mẫu tem?',
      html: 'Mẫu tem <strong>' + escapeHtml(r.partNumber + ' · ' + r.type + ' · ' + r.operation) + '</strong> sẽ bị xoá vĩnh viễn. ' +
        'Trạm Twist sẽ không in được tem này cho đến khi tạo lại.',
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
    else if (btn.dataset.action === 'delete') confirmDelete(id);
  });

  el.form.addEventListener('submit', save);
  el.form.addEventListener('input', function (e) { e.target.classList.remove('is-invalid'); });
  el.form.addEventListener('change', function (e) { e.target.classList.remove('is-invalid'); });
  el.zpl.addEventListener('input', function () {
    updateZplCount();
    editPreview.refreshDebounced();
  });
  el.size.addEventListener('change', function () { editPreview.refresh(); });
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
