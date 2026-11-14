/**
 * Chuẩn hoá ZPL - nút "Chuẩn hoá" trong modal Thêm / Sửa mẫu tem (/toast/admin/label-template).
 * ZPL xuất từ ZebraDesigner (giá trị mẫu) -> chọn biến cho từng chữ / barcode + tick căn giữa
 * -> server (LabelTemplateController api/normalize/*, ZplNormalizer) trả ZPL mới -> "Dùng ZPL này" điền vào ô ZPL của form.
 * Không lưu gì: người dùng vẫn bấm Lưu ở form như bình thường.
 */
(function () {
  'use strict';

  const cfg = window.LabelTemplatePage;
  const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
  const notyf = new Notyf({ duration: 4000, position: { x: 'right', y: 'top' }, dismissible: true });
  const DPMM_BY_DPI = { 152: 6, 203: 8, 300: 12, 600: 24 };

  const el = {
    open: document.getElementById('lt-normalize'),
    zpl: document.getElementById('lt-zpl'),
    size: document.getElementById('lt-size'),
    dpi: document.getElementById('lt-dpi'),
    editModal: document.getElementById('lt-modal'),
    modal: document.getElementById('lt-norm-modal'),
    tbody: document.getElementById('lt-norm-tbody'),
    datalist: document.getElementById('lt-norm-vars'),
    apply: document.getElementById('lt-norm-apply')
  };
  const editModal = bootstrap.Modal.getOrCreateInstance(el.editModal);
  const modal = new bootstrap.Modal(el.modal);

  // original: ZPL lúc mở (id của field = vị trí ^FD trong chuỗi này). result: ZPL sau khi áp thay đổi (bản xem trước "Sau")
  const state = { original: '', fields: [], result: '', mode: 'after', backToEdit: false, timer: null, requestNo: 0 };

  async function api(url, body) {
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': token },
      body: JSON.stringify(body)
    });
    if (res.status === 401) {
      window.location.href = cfg.loginUrl + '?returnUrl=' + encodeURIComponent(window.location.pathname);
      throw new Error('Phiên đăng nhập đã hết hạn.');
    }
    if (!res.ok) throw new Error('Lỗi máy chủ (' + res.status + ').');
    return res.json();
  }

  // Khung xem trước dùng chung (zpl-preview.js). "Sau" = ZPL mới + giá trị thử của từng biến; "Trước" = ZPL gốc.
  const preview = ZplPreview.create('lt-norm-preview', {
    previewUrl: cfg.apiUrl + '/preview',
    request: function (url, options) { return api(url, JSON.parse(options.body)); },
    getSource: function () {
      const after = state.mode === 'after';
      return {
        zplData: after ? state.result : state.original,
        size: el.size.value,
        dpmm: DPMM_BY_DPI[el.dpi.value],
        variables: after ? testValues() : [],
        fileName: 'chuan-hoa'
      };
    }
  });

  // ===== Mở =====

  async function open() {
    const zpl = el.zpl.value;
    if (!zpl.trim()) {
      notyf.error('Chưa có nội dung ZPL. Dán hoặc tải file ZPL xuất từ ZebraDesigner trước.');
      return;
    }
    el.open.disabled = true;
    try {
      const res = await api(cfg.apiUrl + '/normalize/analyze', { zplData: zpl });
      if (!res.success) throw new Error(res.error);
      state.original = zpl;
      state.result = zpl;
      // Thứ tự trên tem: trên xuống dưới, trái sang phải
      state.fields = res.fields.slice().sort(function (a, b) { return a.y - b.y || a.x - b.x; });
      renderVariables(res.variables, zpl);
      renderRows();
      setMode('after');
      state.backToEdit = true;
      editModal.hide();
      modal.show();
      preview.reset();
      refresh();
    } catch (err) {
      notyf.error(err.message);
    } finally {
      el.open.disabled = false;
    }
  }

  // Gợi ý biến: biến khai báo trong Configs/label-variables.json + biến đang có trong ZPL
  function renderVariables(list, zpl) {
    const seen = {};
    const options = [];
    (list || []).forEach(function (v) {
      if (seen[v.key.toUpperCase()]) return;
      seen[v.key.toUpperCase()] = true;
      options.push('<option value="' + escapeHtml(v.key) + '">' + escapeHtml(v.label || '') + '</option>');
    });
    const re = /\{([A-Za-z_][A-Za-z0-9_]*)\}/g;
    let m;
    while ((m = re.exec(zpl)) !== null) {
      if (seen[m[1].toUpperCase()]) continue;
      seen[m[1].toUpperCase()] = true;
      options.push('<option value="' + escapeHtml(m[1]) + '">Đang có trong ZPL</option>');
    }
    el.datalist.innerHTML = options.join('');
  }

  function renderRows() {
    el.tbody.innerHTML = state.fields.map(function (f, i) {
      const isBarcode = f.kind === 'barcode';
      const type = isBarcode
        ? '<span class="badge bg-label-info">Barcode</span><div class="text-muted small">' + escapeHtml(f.symbology) + '</div>'
        : '<span class="badge bg-label-secondary">Chữ</span>';
      const pos = '<div class="text-muted small">x ' + f.x + ' · y ' + f.y + '</div>';
      const varLocked = f.mixedVariable;
      const varTitle = varLocked ? 'Nội dung có biến lẫn chữ khác, giữ nguyên' : 'Gõ hoặc chọn tên biến, để trống = giữ nguyên';
      const centerDisabled = f.centered || !f.canCenter;
      const centerTitle = f.centered ? 'Đã căn giữa' : (!f.canCenter ? (f.note || 'Không căn giữa được') : 'Căn giữa trong ô khung');
      return '<tr data-index="' + i + '">' +
        '<td>' + type + pos + '</td>' +
        '<td><input type="text" class="form-control form-control-sm lt-norm-sample" data-role="sample" value="' + escapeHtml(f.sample) + '"' +
          (f.variable || f.mixedVariable ? '' : ' readonly') + ' aria-label="Nội dung" title="' +
          (f.variable || f.mixedVariable ? 'Sửa để thử giá trị khác' : 'Chọn biến để thử giá trị khác') + '" /></td>' +
        '<td><input type="text" class="form-control form-control-sm" data-role="variable" list="lt-norm-vars" spellcheck="false"' +
          ' value="' + escapeHtml(f.variable || '') + '" placeholder="giữ nguyên"' + (varLocked ? ' disabled' : '') +
          ' title="' + escapeHtml(varTitle) + '" aria-label="Thay bằng biến" /></td>' +
        '<td class="text-center"><input type="checkbox" class="form-check-input" data-role="center"' +
          (f.centered ? ' checked' : '') + (centerDisabled ? ' disabled' : '') + ' title="' + escapeHtml(centerTitle) + '" aria-label="Căn giữa" /></td>' +
        '</tr>';
    }).join('');
  }

  // ===== Thay đổi =====

  function rowOf(target) { return target.closest('tr[data-index]'); }

  function variableOf(row) { return row.querySelector('[data-role="variable"]').value.trim().replace(/^\{|\}$/g, '').trim(); }

  function collectChanges() {
    return Array.from(el.tbody.querySelectorAll('tr[data-index]')).map(function (row) {
      const f = state.fields[parseInt(row.dataset.index, 10)];
      const center = row.querySelector('[data-role="center"]');
      return { id: f.id, variable: variableOf(row) || null, center: center.checked && !center.disabled };
    }).filter(function (c, i) {
      const f = state.fields.find(function (x) { return x.id === c.id; });
      return (c.variable && c.variable.toUpperCase() !== (f.variable || '').toUpperCase()) || c.center;
    });
  }

  // Giá trị thử cho từng biến (xem trước "Sau"): ô Nội dung của dòng đầu tiên dùng biến đó
  function testValues() {
    const values = {};
    el.tbody.querySelectorAll('tr[data-index]').forEach(function (row) {
      const key = variableOf(row);
      if (!key || values[key.toUpperCase()] !== undefined) return;
      const v = row.querySelector('[data-role="sample"]').value;
      if (/^\{[A-Za-z_][A-Za-z0-9_]*\}$/.test(v.trim())) return; // chưa nhập giá trị thử
      values[key.toUpperCase()] = { key: key, value: v };
    });
    return Object.keys(values).map(function (k) { return values[k]; });
  }

  // Áp thay đổi ở server (ZPL mới cho bản "Sau"), sau đó vẽ lại
  async function refresh() {
    const no = ++state.requestNo;
    try {
      const res = await api(cfg.apiUrl + '/normalize/apply', { zplData: state.original, changes: collectChanges() });
      if (no !== state.requestNo) return;
      if (!res.success) throw new Error(res.error);
      state.result = res.zplData;
      preview.refresh();
    } catch (err) {
      notyf.error(err.message);
    }
  }

  function refreshDebounced() {
    clearTimeout(state.timer);
    state.timer = setTimeout(refresh, 500);
  }

  function setMode(mode) {
    state.mode = mode;
    document.getElementById('lt-norm-mode-' + mode).checked = true;
  }

  el.tbody.addEventListener('input', function (e) {
    const row = rowOf(e.target);
    if (!row) return;
    if (e.target.dataset.role === 'variable') {
      const sample = row.querySelector('[data-role="sample"]');
      const center = row.querySelector('[data-role="center"]');
      const hasVar = !!variableOf(row);
      // Chọn biến -> cho sửa giá trị thử, tự tick căn giữa (nội dung thay đổi theo đơn)
      sample.readOnly = !hasVar && !state.fields[parseInt(row.dataset.index, 10)].variable;
      if (hasVar && !center.disabled && !row.dataset.centerTouched) center.checked = true;
    }
    refreshDebounced();
  });
  el.tbody.addEventListener('change', function (e) {
    if (e.target.dataset.role === 'center') rowOf(e.target).dataset.centerTouched = '1';
    refreshDebounced();
  });
  document.querySelectorAll('input[name="lt-norm-mode"]').forEach(function (r) {
    r.addEventListener('change', function () { state.mode = r.value; preview.refresh(); });
  });

  // ===== Dùng ZPL này =====

  async function apply() {
    el.apply.disabled = true;
    try {
      const res = await api(cfg.apiUrl + '/normalize/apply', { zplData: state.original, changes: collectChanges() });
      if (!res.success) throw new Error(res.error);
      (res.warnings || []).forEach(function (w) { notyf.error(w); });
      if (res.zplData !== state.original) {
        el.zpl.value = res.zplData;
        // để form Mẫu tem cập nhật số dòng, ô biến, xem trước (label-template.js lắng nghe sự kiện input)
        el.zpl.dispatchEvent(new Event('input', { bubbles: true }));
        notyf.success('Đã cập nhật ZPL. Kiểm tra xem trước rồi bấm Lưu.');
      } else {
        notyf.success('Không có thay đổi.');
      }
      modal.hide();
    } catch (err) {
      notyf.error(err.message);
    } finally {
      el.apply.disabled = false;
    }
  }

  el.open.addEventListener('click', open);
  el.apply.addEventListener('click', apply);
  // Đóng modal Chuẩn hoá (Huỷ / Dùng ZPL này) -> quay lại form Sửa mẫu tem
  el.modal.addEventListener('hidden.bs.modal', function () {
    clearTimeout(state.timer);
    if (state.backToEdit) {
      state.backToEdit = false;
      editModal.show();
    }
  });

  function escapeHtml(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }
})();
