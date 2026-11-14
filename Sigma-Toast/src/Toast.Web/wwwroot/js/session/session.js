/**
 * Phiên làm việc - /toast/admin/session (Toast_Session + Toast_SessionConfirm). Xem + kết thúc phiên đang mở (box dở giữ lại).
 * Nhập KQSX lên Viindoo khi kết thúc phiên: bật / tắt bằng Toast:PushProduction (appsettings), màn chỉ hiện trạng thái. Cột KQSX = kết quả nhập lúc kết thúc phiên; phiên chưa nhập
 * (công tắc tắt) / nhập lỗi có nút Đẩy KQSX trong modal.
 * Phiên đang mở hiện thời gian không hoạt động = giờ hiện tại − lần quét / cập nhật box mới nhất của lệnh (server tính lastActivityAt).
 * Danh sách: 500 phiên mới nhất + mọi phiên đang mở; tìm / lọc ở trình duyệt. Tải lại = xoá tìm + lọc rồi tải lại.
 * Chi tiết (modal): thông tin phiên · các lần xác nhận (mã NV, tên, điện thoại, máy in; mở dòng xem giá trị tem + cảnh báo) · box.
 */
(function () {
  'use strict';

  const cfg = window.SessionAdminPage;
  const notyf = window.notyf;
  const $ = function (id) { return document.getElementById(id); };
  const el = {
    summary: $('ss-summary'), search: $('ss-search'), status: $('ss-filter-status'), reload: $('ss-reload'), tbody: $('ss-tbody'),
    modal: $('ss-modal'), title: $('ss-modal-title'), detail: $('ss-detail'), end: $('ss-end'), push: $('ss-push'),
    pushStatus: $('ss-push-status')
  };
  const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
  const modal = new bootstrap.Modal(el.modal);
  const state = { rows: [], detailId: null, pushEnabled: false };
  const SPINNER = '<div class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></div>';

  async function api(url, method, body) {
    const res = await fetch(url, {
      method: method || 'GET',
      headers: { 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': token, 'Content-Type': 'application/json' },
      body: body ? JSON.stringify(body) : undefined
    });
    if (res.status === 401) {
      window.location.href = cfg.loginUrl + '?returnUrl=' + encodeURIComponent(window.location.pathname);
      throw new Error('Phiên đăng nhập đã hết hạn.');
    }
    if (!res.ok) throw new Error('Lỗi máy chủ (' + res.status + ').');
    return res.json();
  }

  // ===== Danh sách =====

  async function loadList() {
    el.tbody.innerHTML = '<tr><td colspan="12" class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></td></tr>';
    try {
      const res = await api(cfg.apiUrl);
      if (!res.success) throw new Error(res.error);
      state.rows = res.data;
      state.pushEnabled = !!res.pushEnabled;
      el.pushStatus.innerHTML = 'Nhập KQSX khi kết thúc phiên: ' + (state.pushEnabled
        ? '<span class="badge bg-label-success">Bật</span>' : '<span class="badge bg-label-secondary">Tắt</span>') +
        ' <span class="text-muted">(Toast:PushProduction)</span>';
      const open = state.rows.filter(function (r) { return r.status === 'Open'; }).length;
      el.summary.textContent = state.rows.length + ' phiên · ' + open + ' đang mở' + (res.limited ? ' (chỉ hiện 500 phiên mới nhất và các phiên đang mở)' : '');
      render();
    } catch (err) {
      el.summary.textContent = '';
      el.tbody.innerHTML = '<tr><td colspan="12" class="text-center text-danger py-5">' + esc(err.message) + '</td></tr>';
    }
  }

  function filteredRows() {
    const term = el.search.value.trim().toLowerCase();
    const status = el.status.value;
    return state.rows.filter(function (r) {
      if (status && r.status !== status) return false;
      if (!term) return true;
      return [r.woName, r.defaultCode, r.productName].concat(r.employees || []).some(function (v) {
        return String(v || '').toLowerCase().indexOf(term) !== -1;
      });
    });
  }

  function rowHtml(r) {
      const ended = r.endedAt
        ? fmt(r.endedAt) + (r.endedBy ? '<div class="small text-muted">bởi ' + esc(r.endedBy) + '</div>' : '')
        : '<span class="text-muted">—</span>';
      const last = r.lastEmployeeCode
        ? '<span class="fw-medium text-heading">' + esc(r.lastEmployeeCode) + '</span>' + (r.lastEmployeeName ? ' · ' + esc(r.lastEmployeeName) : '') +
          '<div class="small text-muted">' + fmt(r.lastConfirmedAt) + '</div>'
        : '<span class="text-muted">—</span>';
      const box = r.closedBoxes + ' box · ' + num(r.closedPcs) + ' pcs' +
        (r.openBox ? '<div class="small text-muted">dở box ' + r.openBox.packageNo + ': ' + r.openBox.qty + '/' + r.openBox.capacity + '</div>' : '');
      return '<tr data-id="' + r.id + '">' +
        '<td class="text-muted">' + r.id + '</td>' +
        '<td class="fw-medium text-heading">' + esc(r.woName) + '</td>' +
        '<td>' + esc(r.defaultCode) + (r.productName ? '<div class="small text-muted">' + esc(r.productName) + '</div>' : '') + '</td>' +
        '<td>' + statusBadge(r.status) + '</td>' +
        '<td class="small">' + fmt(r.startedAt) + '</td>' +
        '<td class="small">' + ended + '</td>' +
        '<td>' + last + '</td>' +
        '<td class="text-center">' + r.confirmCount + '</td>' +
        '<td>' + box + '</td>' +
        '<td>' + pushHtml(r, true) + '</td>' +
        '<td class="small">' + idleHtml(r) + '</td>' +
        '<td><button type="button" class="btn btn-sm btn-icon btn-label-primary" data-action="view" aria-label="Xem chi tiết" title="Xem chi tiết"><i class="icon-base ti tabler-eye"></i></button></td>' +
        '</tr>';
  }

  // Phân trang + sắp xếp (giống màn Mẫu tem); mặc định phiên mới nhất trước
  const table = ListTable({
    tbody: el.tbody, info: $('ss-info'), pagination: $('ss-pagination'), pageSize: $('ss-page-size'),
    colspan: 12, emptyText: 'Không có phiên nào', rows: filteredRows, rowHtml: rowHtml,
    sortValue: {
      id: function (r) { return r.id; },
      wo: function (r) { return r.woName; },
      code: function (r) { return r.defaultCode; },
      started: function (r) { return new Date(r.startedAt).getTime() || 0; },
      ended: function (r) { return r.endedAt ? new Date(r.endedAt).getTime() : Number.MAX_SAFE_INTEGER; },
      idle: function (r) { return r.lastActivityAt ? Date.now() - new Date(r.lastActivityAt).getTime() : -1; }
    }
  });

  function render(firstPage) { table.render(firstPage); }

  // ===== Chi tiết =====

  async function openDetail(id) {
    state.detailId = id;
    el.end.classList.add('d-none');
    el.push.classList.add('d-none');
    el.title.textContent = 'Phiên #' + id;
    el.detail.innerHTML = SPINNER;
    modal.show();
    try {
      const res = await api(cfg.apiUrl + '/' + id);
      if (!res.success) throw new Error(res.error);
      renderDetail(res);
    } catch (err) {
      el.detail.innerHTML = '<div class="text-center text-danger py-5">' + esc(err.message) + '</div>';
      if (notyf) notyf.error(err.message);
    }
  }

  function renderDetail(res) {
    const s = res.session, confirms = res.confirms || [], boxes = res.boxes || [];
    el.title.innerHTML = 'Phiên #' + s.id + ' · ' + esc(s.woName) + ' ' + statusBadge(s.status);
    el.end.classList.toggle('d-none', s.status !== 'Open');
    // Đẩy KQSX: phiên đã kết thúc của lệnh thật, chưa nhập (công tắc tắt lúc kết thúc) hoặc nhập lỗi
    el.push.classList.toggle('d-none', !(s.status === 'Ended' && s.viindooMoId && (!s.pushStatus || s.pushStatus === 'Off' || s.pushStatus === 'Error' || s.pushStatus === 'Mismatch')));

    const info =
      '<dl class="ss-info mb-0">' +
        item('Sản phẩm', esc(s.defaultCode) + (s.productName ? '<div class="small text-muted fw-normal">' + esc(s.productName) + '</div>' : '')) +
        item('Bắt đầu', fmt(s.startedAt)) +
        item('Kết thúc', s.endedAt ? fmt(s.endedAt) + (s.endedBy ? '<div class="small text-muted fw-normal">bởi ' + esc(s.endedBy) + '</div>' : '') : '<span class="text-success">Đang mở</span>') +
        item('Thời lượng', duration(s.startedAt, s.endedAt)) +
        (s.status === 'Open' ? item('Inactive', idleHtml(s) + '<div class="small text-muted fw-normal">từ ' + fmt(s.lastActivityAt) + '</div>') : '') +
        (s.status === 'Ended' ? item('KQSX Viindoo', pushHtml(s, false)) : '') +
      '</dl>';

    const confirmRows = confirms.length ? confirms.map(function (c, i) {
      const isLast = i === confirms.length - 1;
      return '<tr class="ss-confirm-row' + (isLast ? ' is-open' : '') + '" data-confirm="' + c.id + '">' +
          '<td class="text-center"><i class="icon-base ti tabler-chevron-right ss-chevron"></i></td>' +
          '<td class="text-center">' + c.no + '</td>' +
          '<td class="small">' + fmt(c.confirmedAt) + '</td>' +
          '<td class="fw-medium text-heading">' + esc(c.employeeCode) + '</td>' +
          '<td>' + dash(c.employeeName) + '</td>' +
          '<td>' + dash(c.employeePhone) + '</td>' +
          '<td class="small">' + dash(c.printTarget) + '</td>' +
          '<td class="text-center">' + (c.warnings.length ? '<span class="badge bg-label-warning">' + c.warnings.length + '</span>' : '<span class="text-muted">—</span>') + '</td>' +
        '</tr>' +
        '<tr class="ss-confirm-detail' + (isLast ? '' : ' d-none') + '" data-detail="' + c.id + '"><td colspan="8" class="bg-body-tertiary">' +
          '<div class="row g-4 py-2">' +
            '<div class="col-lg-7"><div class="fw-semibold text-heading mb-2">Giá trị tem đã chốt</div>' + values(c.values) + '</div>' +
            '<div class="col-lg-5"><div class="fw-semibold text-heading mb-2">Cảnh báo lúc xác nhận</div>' + warnings(c.warnings) + '</div>' +
          '</div>' +
        '</td></tr>';
    }).join('') : '<tr><td colspan="8" class="text-center text-muted py-4">Chưa có lần xác nhận nào</td></tr>';

    // Box mới nhất trước; hiện BOX_PAGE box, bấm Xem thêm để hiện thêm
    state.boxes = boxes.slice().sort(function (a, b) { return b.packageNo - a.packageNo; });
    state.boxFilter = 'all';
    state.boxShown = BOX_PAGE;

    el.detail.innerHTML =
      '<div class="d-flex flex-column gap-6">' +
        section('Thông tin phiên', info) +
        pushDetailSection(s) +
        section('Lần xác nhận (' + confirms.length + ')',
          '<div class="small text-muted mb-2">Mỗi lần mở phiên / vào lại phiên là 1 lần xác nhận. Bấm vào dòng để xem giá trị tem và cảnh báo.</div>' +
          '<div class="table-responsive"><table class="table table-bordered mb-0" style="min-width: 900px;">' +
            '<thead class="table-light"><tr><th style="width: 40px;"></th><th style="width: 50px;" class="text-center">#</th><th style="width: 150px;">Thời gian</th>' +
            '<th style="width: 100px;">Mã NV</th><th>Tên</th><th style="width: 140px;">Điện thoại</th><th>Máy in</th>' +
            '<th style="width: 90px;" class="text-center">Cảnh báo</th></tr></thead>' +
            '<tbody id="ss-confirms">' + confirmRows + '</tbody></table></div>') +
        section('Box', '<div class="small text-muted mb-2">Box đã đóng trong phiên này' + (s.status === 'Open' ? ' và box đang dở của WO' : '') +
          '. Sửa / xoá box ở màn <a href="' + cfg.packageUrl + '">Đóng gói</a>.</div><div id="ss-boxes"></div>') +
      '</div>';
    renderBoxes();
  }

  // ===== Box của phiên =====
  // Loại box: open = đang quét, error = đã đóng nhưng in lỗi, short = đóng thiếu pcs, ok = đủ pcs + đã in
  function boxKind(b) {
    if (b.status === 'Open') return 'open';
    if (!b.printedAt) return 'error';
    return b.qty < b.capacity ? 'short' : 'ok';
  }

  const BOX_FILTERS = [['all', 'Tất cả'], ['error', 'Lỗi in'], ['short', 'Thiếu pcs'], ['open', 'Đang dở']];
  const BOX_PAGE = 10;

  // Tóm tắt + nút lọc + bảng (box mới nhất trước, BOX_PAGE box / lần, nút Xem thêm)
  function renderBoxes() {
    const host = $('ss-boxes');
    if (!host) return;
    const boxes = state.boxes;
    const count = { all: boxes.length, error: 0, short: 0, open: 0, ok: 0 };
    let closedPcs = 0;
    boxes.forEach(function (b) { count[boxKind(b)]++; if (b.status !== 'Open') closedPcs += b.qty; });
    const openBox = boxes.find(function (b) { return b.status === 'Open'; });

    const summary = '<div class="d-flex flex-wrap gap-3 align-items-center mb-3">' +
      '<span><span class="fw-semibold text-heading">' + num(boxes.length - count.open) + '</span> box đã đóng · <span class="fw-semibold text-heading">' + num(closedPcs) + '</span> pcs</span>' +
      (count.error ? '<span class="badge bg-label-danger">' + count.error + ' lỗi in</span>' : '') +
      (count.short ? '<span class="badge bg-label-warning" title="Box đã đóng nhưng ít hơn pcs/box (đóng box thiếu hoặc sửa số pcs)">' + count.short + ' thiếu pcs</span>' : '') +
      (openBox ? '<span class="badge bg-label-primary">Đang dở box ' + openBox.packageNo + ': ' + openBox.qty + '/' + openBox.capacity + '</span>' : '') +
      '</div>';
    const filters = boxes.length ? '<div class="btn-group btn-group-sm mb-3" role="group" aria-label="Lọc box">' + BOX_FILTERS.map(function (f) {
      const n = count[f[0]];
      return '<button type="button" class="btn ' + (state.boxFilter === f[0] ? 'btn-primary' : 'btn-label-secondary') + '" data-box-filter="' + f[0] + '"' +
        (n || f[0] === 'all' ? '' : ' disabled') + '>' + f[1] + ' (' + n + ')</button>';
    }).join('') + '</div>' : '';

    const list = state.boxFilter === 'all' ? boxes : boxes.filter(function (b) { return boxKind(b) === state.boxFilter; });
    const shown = list.slice(0, state.boxShown);
    const rows = shown.length ? shown.map(function (b) { return boxRow(b); }).join('')
      : '<tr><td colspan="7" class="text-center text-muted py-4">Chưa có box nào</td></tr>';
    const more = list.length > shown.length
      ? '<div class="d-flex justify-content-center align-items-center gap-3 mt-3"><span class="small text-muted">Đang hiện ' + shown.length + ' / ' + list.length + ' box</span>' +
        '<button type="button" class="btn btn-sm btn-label-primary" data-box-more>Xem thêm ' + Math.min(BOX_PAGE, list.length - shown.length) + ' box</button></div>'
      : '';

    host.innerHTML = summary + filters +
      '<div class="table-responsive"><table class="table table-bordered mb-0" style="min-width: 800px;">' +
        '<thead class="table-light"><tr><th style="width: 80px;">#</th><th style="width: 80px;">Box</th><th style="width: 100px;">Pcs</th><th style="width: 120px;">Trạng thái</th>' +
        '<th style="width: 170px;">Đóng lúc</th><th>In tem</th><th style="width: 80px;">Log</th></tr></thead>' +
        '<tbody>' + rows + '</tbody></table></div>' + more;
  }

  function boxRow(b) {
    const kind = boxKind(b);
    const print = kind === 'open' ? '<span class="text-muted">—</span>'
      : b.printedAt ? '<span class="badge bg-label-success">Đã in</span><div class="small text-muted">' + esc(b.printTarget || '') + '</div>'
        : '<span class="badge bg-label-danger">Lỗi</span><div class="small text-muted">' + esc(b.printError || '') + '</div>';
    return '<tr>' +
      '<td class="small text-muted">' + b.id + '</td>' +
      '<td class="fw-medium text-heading">' + b.packageNo + '</td>' +
      '<td' + (kind === 'short' ? ' class="text-warning fw-semibold"' : '') + '>' + b.qty + ' / ' + b.capacity + '</td>' +
      '<td>' + (kind === 'open' ? '<span class="badge bg-label-primary">Đang quét</span>' : '<span class="badge bg-label-secondary">Đã đóng</span>') + '</td>' +
      '<td class="small">' + (b.closedAt ? fmt(b.closedAt) : '—') + '</td>' +
      '<td>' + print + '</td>' +
      '<td>' + (b.inputLogId ? '<span class="badge bg-label-success" title="id dòng SVN_ProductionInputLogs">#' + b.inputLogId + '</span>' : '<span class="text-muted">—</span>') + '</td>' +
      '</tr>';
  }


  // Chi tiết lần nhập KQSX gần nhất (Toast_Session.PushDetail): lệnh trước / sau, số lượng, tiêu hao NVL, các bước (bước lỗi tô đỏ)
  const STEPS = { read: 'Đọc lệnh', write: 'Ghi số lượng + tiêu hao', mark_done: 'Hoàn thành lệnh', wizard: 'Xác nhận wizard', verify: 'Đọc lại lệnh', reconcile: 'Đối chiếu (lệnh đã hoàn thành)' };
  function pushDetailSection(s) {
    if (!s.pushDetail) return '';
    let d;
    try { d = JSON.parse(s.pushDetail); } catch (e) { return ''; }
    const mo = d.mo || {};
    const info = '<dl class="ss-info mb-4">' +
      item('Lệnh', esc(mo.nameBefore || '') + (mo.nameAfter && mo.nameAfter !== mo.nameBefore ? ' → ' + esc(mo.nameAfter) : '') +
        '<div class="small text-muted fw-normal">id ' + mo.id + ' · ' + esc(mo.stateBefore || '?') + ' → ' + esc(mo.stateAfter || '?') + '</div>') +
      item('SL lệnh', num(mo.planQty)) +
      item('Pcs đã đóng box', num(d.toastQty) + ' pcs') +
      item('Nhập lên Viindoo', num(d.pushQty) + ' pcs' + (d.excess ? '<div class="small text-warning fw-normal">thừa ' + num(d.excess) + ' pcs không nhập</div>' : '')) +
      item('Backorder', d.backorder ? esc(d.backorder) : '<span class="text-muted fw-normal">—</span>') +
      '</dl>';
    const materials = (d.materials || []).length
      ? '<div class="table-responsive mb-4"><table class="table table-bordered table-sm mb-0"><thead class="table-light"><tr>' +
          '<th>NVL</th><th style="width: 140px;" class="text-end">Tỉ lệ / pcs</th><th style="width: 140px;" class="text-end">Tiêu hao đã gửi</th></tr></thead><tbody>' +
          d.materials.map(function (m) {
            return '<tr><td class="fw-medium text-heading">' + esc(m.code) + '</td><td class="text-end">' + m.unitFactor + '</td><td class="text-end">' + m.qtyDone + '</td></tr>';
          }).join('') + '</tbody></table></div>'
      : '';
    const steps = '<ol class="list-unstyled d-flex flex-column gap-2 mb-0">' + (d.steps || []).map(function (st) {
      return '<li class="d-flex gap-2"><i class="icon-base ti ' + (st.ok ? 'tabler-circle-check text-success' : 'tabler-alert-circle text-danger') + ' flex-shrink-0"></i><div>' +
        '<span class="fw-medium text-heading">' + esc(STEPS[st.step] || st.step) + '</span>' + (st.wizard ? ' <code>' + esc(st.wizard) + '</code>' : '') +
        ' <span class="small text-muted">' + fmt(st.at) + '</span>' +
        (st.error ? '<div class="small text-danger">' + esc(st.error) + '</div>' : '') +
        (st.note ? '<div class="small text-muted">' + esc(st.note) + '</div>' : '') + '</div></li>';
    }).join('') + '</ol>';
    return section('Chi tiết KQSX', info + materials + '<div class="fw-semibold text-heading mb-2">Các bước</div>' + steps);
  }

  function section(title, body) {
    return '<section><h6 class="mb-3 text-heading">' + title + '</h6>' + body + '</section>';
  }

  function item(label, value) { return '<div><dt>' + label + '</dt><dd>' + value + '</dd></div>'; }

  function values(list) {
    if (!list || !list.length) return '<div class="text-muted small">Không có giá trị nào</div>';
    return '<dl class="ss-values">' + list.map(function (v) {
      return '<dt>' + esc(v.label || v.key) + ' <span class="small">{' + esc(v.key) + '}</span></dt><dd>' + esc(v.value) + '</dd>';
    }).join('') + '</dl>';
  }

  function warnings(list) {
    if (!list || !list.length) return '<div class="text-muted small">Không có cảnh báo</div>';
    return '<ul class="list-unstyled d-flex flex-column gap-2 mb-0">' + list.map(function (w) {
      return '<li class="d-flex gap-2"><i class="icon-base ti tabler-alert-triangle text-warning flex-shrink-0"></i><span>' + esc(w) + '</span></li>';
    }).join('') + '</ul>';
  }

  // Thời gian không hoạt động của phiên đang mở (từ lastActivityAt đến giờ); phiên đã kết thúc: —
  function idleHtml(r) {
    if (r.status !== 'Open' || !r.lastActivityAt) return '<span class="text-muted">—</span>';
    const mins = Math.max(0, Math.floor((Date.now() - new Date(r.lastActivityAt)) / 60000));
    const days = Math.floor(mins / 1440), hours = Math.floor((mins % 1440) / 60), m = mins % 60;
    const text = mins < 1 ? 'Vừa hoạt động' : (days ? days + ' ngày ' : '') + (days || hours ? hours + ' giờ ' : '') + (days ? '' : m + ' phút');
    // Quá 1 giờ không quét: tô vàng để dễ thấy phiên bị bỏ quên
    return mins >= 60 ? '<span class="text-warning fw-semibold">' + text + '</span>' : text;
  }

  // Kết quả nhập KQSX của phiên. short = ô trong danh sách (badge + số pcs, chi tiết ở tooltip)
  const PUSH = {
    Done: ['success', 'Đã nhập'], Error: ['danger', 'Lỗi'], Off: ['secondary', 'Chưa nhập'], Skipped: ['secondary', 'Không cần'],
    Mismatch: ['warning', 'Lệch']
  };
  function pushHtml(r, short) {
    if (!r.pushStatus) return '<span class="text-muted">—</span>';
    const p = PUSH[r.pushStatus] || ['secondary', r.pushStatus];
    const badge = '<span class="badge bg-label-' + p[0] + '"' + (short && r.pushMessage ? ' title="' + esc(r.pushMessage) + '"' : '') + '>' + p[1] + '</span>';
    if (short) return badge + (r.pushStatus === 'Done' ? '<div class="small text-muted">' + num(r.pushQty) + ' pcs</div>' : '');
    return badge + (r.pushMessage ? '<div class="small fw-normal mt-1">' + esc(r.pushMessage) + '</div>' : '') +
      (r.pushedAt ? '<div class="small text-muted fw-normal">' + fmt(r.pushedAt) + '</div>' : '');
  }

  // Kết quả nhập KQSX sau khi kết thúc / đẩy: lỗi thì báo đỏ (để lâu), xong thì báo xanh
  function notifyPush(push) {
    if (!push || !notyf || !push.message) return;
    if (push.status === 'Error') notyf.open({ type: 'error', message: 'Nhập KQSX lỗi: ' + push.message, duration: 10000 });
    else if (push.status === 'Skipped') notyf.success(push.message);
    else if (push.status === 'Done') notyf.success(push.message);
    else if (push.status === 'Mismatch') notyf.open({ type: 'error', message: 'KQSX lệch: ' + push.message, duration: 10000 });
  }

  // Đẩy KQSX cho phiên đã kết thúc chưa nhập / nhập lỗi
  async function pushSession() {
    const id = state.detailId;
    if (!id) return;
    const result = await Swal.fire({
      title: 'Nhập KQSX phiên #' + id + ' lên Viindoo?',
      html: 'Nhập tổng pcs các box đã đóng của lệnh (tối đa SL còn lại). Lệnh sẽ hoàn thành trên Viindoo, phần còn lại sang lệnh mới.',
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Nhập KQSX',
      cancelButtonText: 'Huỷ',
      focusCancel: true,
      buttonsStyling: false,
      customClass: { confirmButton: 'btn btn-primary me-2', cancelButton: 'btn btn-label-secondary' }
    });
    if (!result.isConfirmed) return;
    el.push.disabled = true;
    try {
      const res = await api(cfg.apiUrl + '/' + id + '/push', 'POST');
      if (!res.success) throw new Error(res.error);
      notifyPush(res.push);
    } catch (err) {
      if (notyf) notyf.open({ type: 'error', message: err.message, duration: 10000 });
    } finally {
      el.push.disabled = false;
      await openDetail(id);
      await loadList();
    }
  }

  // Kết thúc phiên đang mở (box dở giữ lại cho phiên sau)
  async function endSession() {
    const id = state.detailId;
    if (!id) return;
    const result = await Swal.fire({
      title: 'Kết thúc phiên #' + id + '?',
      html: 'Box đang dở (nếu có) được giữ lại, lần sau mở lệnh sẽ quét tiếp.' +
        (state.pushEnabled ? '<br><strong>Nhập KQSX đang bật: sẽ nhập KQSX lên Viindoo.</strong>' : ''),
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Kết thúc',
      cancelButtonText: 'Huỷ',
      focusCancel: true,
      buttonsStyling: false,
      customClass: { confirmButton: 'btn btn-danger me-2', cancelButton: 'btn btn-label-secondary' }
    });
    if (!result.isConfirmed) return;
    el.end.disabled = true;
    try {
      const res = await api(cfg.apiUrl + '/' + id + '/end', 'POST');
      if (!res.success) throw new Error(res.error);
      if (notyf) notyf.success(res.message);
      notifyPush(res.push);
      await openDetail(id);
      await loadList();
    } catch (err) {
      if (notyf) notyf.error(err.message);
    } finally {
      el.end.disabled = false;
    }
  }

  function statusBadge(status) {
    return status === 'Open' ? '<span class="badge bg-label-success">Đang mở</span>' : '<span class="badge bg-label-secondary">Đã kết thúc</span>';
  }

  function duration(from, to) {
    const ms = (to ? new Date(to) : new Date()) - new Date(from);
    if (!(ms >= 0)) return '—';
    const mins = Math.floor(ms / 60000), days = Math.floor(mins / 1440), hours = Math.floor((mins % 1440) / 60), m = mins % 60;
    return (days ? days + ' ngày ' : '') + (days || hours ? hours + ' giờ ' : '') + m + ' phút' + (to ? '' : ' <span class="small text-muted fw-normal">(đến giờ)</span>');
  }

  function fmt(v) { return v ? new Date(v).toLocaleString('vi-VN') : '—'; }
  function num(n) { return Number(n || 0).toLocaleString('vi-VN'); }
  function dash(v) { return v ? esc(v) : '<span class="text-muted">—</span>'; }

  function esc(s) {
    return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  // ===== Sự kiện =====

  el.search.addEventListener('input', function () { render(true); });
  el.status.addEventListener('change', function () { render(true); });
  // Tải lại: xoá ô tìm + bộ lọc rồi tải lại dữ liệu
  el.reload.addEventListener('click', function () {
    el.search.value = '';
    el.status.value = '';
    table.reset();
    loadList();
  });
  el.end.addEventListener('click', endSession);
  el.push.addEventListener('click', pushSession);
  el.tbody.addEventListener('click', function (e) {
    if (!e.target.closest('[data-action="view"]')) return;
    const tr = e.target.closest('tr[data-id]');
    if (tr) openDetail(parseInt(tr.dataset.id, 10));
  });
  // Bấm 1 lần vào dòng = Xem chi tiết (bôi đen / double-click thì không mở)
  window.ToastRowClick.button(el.tbody, '[data-action="view"]');
  // Box: lọc nhanh (về 10 box đầu) + Xem thêm
  el.detail.addEventListener('click', function (e) {
    const f = e.target.closest('[data-box-filter]');
    if (f) { state.boxFilter = f.dataset.boxFilter; state.boxShown = BOX_PAGE; renderBoxes(); return; }
    if (e.target.closest('[data-box-more]')) { state.boxShown += BOX_PAGE; renderBoxes(); }
  });
  // Bấm dòng xác nhận: mở / đóng phần giá trị tem + cảnh báo
  window.ToastRowClick.on(el.detail, 'tr[data-confirm]', function (tr) {
    const detail = el.detail.querySelector('tr[data-detail="' + tr.dataset.confirm + '"]');
    if (!detail) return;
    tr.classList.toggle('is-open', detail.classList.toggle('d-none') === false);
  });

  loadList();
})();
