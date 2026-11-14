/**
 * Tra cứu lệnh sản xuất - /toast/admin/work-order (đọc Viindoo, chỉ đọc).
 * Danh sách: GET {apiUrl}?q=&project=&kind=&state=&offset=  ·  Chi tiết: GET {apiUrl}/detail?wo=
 * ?wo=... trên thanh địa chỉ mở thẳng chi tiết; ?q= / bộ lọc giữ khi tải lại.
 */
(function () {
  'use strict';

  const cfg = window.WorkOrderLookupPage;
  const L = window.Lookup, esc = L.esc;
  const $ = function (id) { return document.getElementById(id); };
  const el = {
    form: $('wl-form'), q: $('wl-q'), project: $('wl-project'), kind: $('wl-kind'), state: $('wl-state'),
    tbody: $('wl-tbody'), summary: $('wl-summary'), moreBox: $('wl-more-box'), more: $('wl-more'),
    modal: $('wl-modal'), title: $('wl-modal-title'), detail: $('wl-detail'), actions: $('wl-detail-actions')
  };
  const modal = new bootstrap.Modal(el.modal);
  const $j = window.jQuery;
  const state = { items: [], offset: 0, loading: false };
  const sort = L.sortHeaders(document.querySelector('#wl-tbody').closest('table'), function () { search(false); });

  // ===== Danh sách =====

  function params() {
    return { q: el.q.value.trim(), project: el.project.value, kind: el.kind.value, state: el.state.value, sort: sort.key || '', dir: sort.key ? sort.dir : '' };
  }

  async function search(append) {
    if (state.loading) return;
    state.loading = true;
    const p = params();
    const offset = append ? state.offset : 0;
    if (!append) el.tbody.innerHTML = '<tr><td colspan="8" class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></td></tr>';
    el.more.disabled = true;
    try {
      const qs = new URLSearchParams(Object.assign({}, p, { offset: offset }));
      const res = await L.get(cfg.apiUrl + '?' + qs, cfg.loginUrl);
      const d = res.data;
      state.items = append ? state.items.concat(d.items) : d.items;
      state.offset = d.offset + d.items.length;
      render();
      el.moreBox.classList.toggle('d-none', !d.more);
      el.summary.textContent = state.items.length + ' lệnh' + (d.more ? ' (còn nữa)' : '') + (sort.key ? '' : ' · mới nhất trước');
      saveUrl(p);
    } catch (err) {
      el.tbody.innerHTML = '<tr><td colspan="8" class="text-center text-danger py-5">' + esc(err.message) + '</td></tr>';
    } finally {
      state.loading = false;
      el.more.disabled = false;
    }
  }

  function render() {
    el.tbody.innerHTML = state.items.length ? state.items.map(function (w) {
      return '<tr data-wo="' + esc(w.name) + '">' +
        '<td class="lk-mono fw-medium text-heading">' + esc(w.name) + '</td>' +
        '<td>' + L.classBadges(w.class) + '</td>' +
        '<td class="wl-product" title="' + esc(w.product) + '">' + esc(w.product) + '</td>' +
        '<td class="text-end text-nowrap">' + L.num(w.qtyProduced) + ' / <span class="fw-semibold">' + L.num(w.productQty) + '</span></td>' +
        '<td>' + L.woState(w.state) + '</td>' +
        '<td class="text-nowrap">' + L.dt(w.deadline, true) + '</td>' +
        '<td>' + originCell(w) + '</td>' +
        '<td>' + L.openButton() + '</td>' +
        '</tr>';
    }).join('') : '<tr><td colspan="8" class="text-center text-muted py-5">Không có lệnh nào</td></tr>';
  }

  // Đơn bán; lệnh con (origin là lệnh khác) thì ghi thêm lệnh cha
  function originCell(w) {
    if (!w.parentWo) return L.mono(w.saleOrder);
    return L.mono(w.saleOrder) + '<div class="small text-muted">lệnh cha <span class="lk-mono">' + esc(w.parentWo) + '</span></div>';
  }

  function saveUrl(p, wo) {
    const qs = new URLSearchParams();
    Object.keys(p).forEach(function (k) { if (p[k] && !(k === 'state' && p[k] === 'open')) qs.set(k, p[k]); });
    if (wo) qs.set('wo', wo);
    history.replaceState(null, '', qs.toString() ? '?' + qs : window.location.pathname);
  }

  // ===== Chi tiết =====

  async function openDetail(wo) {
    el.title.textContent = wo;
    el.detail.innerHTML = '<div class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></div>';
    el.actions.innerHTML = '';
    modal.show();
    saveUrl(params(), wo);
    try {
      const d = (await L.get(cfg.apiUrl + '/detail?wo=' + encodeURIComponent(wo), cfg.loginUrl)).detail;
      if (d.notFound) renderGroupOnly(wo, d.group); else renderDetail(d);
    } catch (err) {
      el.detail.innerHTML = L.alert('danger', 'tabler-alert-circle', [err.message]);
    }
  }

  // Nhập tên gốc đã tách backorder: chỉ có nhóm
  function renderGroupOnly(wo, g) {
    el.detail.innerHTML = L.alert('info', 'tabler-info-circle',
      ['Không còn lệnh nào tên ' + wo + ' (đã tách backorder). Đây là nhóm ' + g.name + ':']) + groupHtml(g);
  }

  function renderDetail(d) {
    const w = d.wo, p = d.product, lb = d.label;
    el.title.innerHTML = '<span class="lk-mono">' + esc(w.name) + '</span> ' + L.woState(w.state);
    el.detail.innerHTML =
      (d.warnings.length ? L.alert('warning', 'tabler-alert-triangle', d.warnings) : '') +
      '<div class="row g-4">' +
      '<div class="col-lg-6"><div class="lk-section"><h6>Lệnh sản xuất</h6><dl class="lk-info">' +
        L.row('Đã làm / Cần', L.num(w.qtyProduced) + ' / <span class="fw-semibold">' + L.num(w.productQty) + '</span> ' + esc(w.uom)) +
        L.row('Đơn bán', lb.sale_order ? '<a class="lk-mono" href="' + cfg.saleOrderUrl + '?so=' + encodeURIComponent(lb.sale_order) + '">' + esc(lb.sale_order) + '</a>' : L.val('')) +
        (w.parentWo ? L.row('Lệnh cha', '<a href="#" class="lk-mono" data-open-wo="' + esc(w.parentWo) + '">' + esc(w.parentWo) + '</a> <span class="text-muted small">(lệnh này làm bán thành phẩm cho lệnh cha)</span>') : '') +
        L.row('Dự kiến bắt đầu', L.dt(w.plannedStart)) +
        L.row('Deadline', L.dt(w.deadline)) +
        L.row('Bắt đầu thật', L.dt(w.started)) +
        L.row('Hoàn tất', L.dt(w.finished)) +
        L.row('Tạo lúc', L.dt(w.created) + (w.createdBy ? ' · ' + esc(w.createdBy) : '')) +
        L.row('Phụ trách', L.val(w.responsible)) +
        L.row('BoM', L.val(w.bom)) +
        L.row('NVL', L.val(w.componentsAvailability) + (w.reservationState ? ' <span class="text-muted small">(' + esc(w.reservationState) + ')</span>' : '')) +
        L.row('Thứ tự backorder', w.backorderSequence ? '-' + String(w.backorderSequence).padStart(3, '0') : 'Lệnh gốc') +
      '</dl></div></div>' +
      '<div class="col-lg-6"><div class="lk-section"><h6>Sản phẩm</h6><dl class="lk-info">' +
        (p
          ? L.row('Mã', '<a class="lk-mono" href="' + cfg.productUrl + '?id=' + p.id + '">' + esc(p.defaultCode || '(trống)') + '</a> <span class="text-muted small">(ID ' + p.id + ')</span>') +
            L.row('Tên', esc(p.name)) +
            L.row('Phân loại', L.classBadges(p.class)) +
            L.row('Nhóm Viindoo', L.val(p.category)) +
            L.row('Mã khách (x_custcode)', L.mono(p.customerCode)) +
            L.row('Pcs / box (x_qty)', L.val(p.qtyPerCode)) +
            L.row('Theo dõi', esc(L.TRACKING[p.tracking] || p.tracking))
          : L.row('Sản phẩm', '<span class="text-danger">Không đọc được</span>')) +
      '</dl><h6 class="mt-4">Từ đơn bán</h6><dl class="lk-info">' +
        L.row('PO', L.mono(lb.po)) +
        L.row('Lot ID', L.mono(lb.lot_id)) +
      '</dl></div></div>' +
      '</div>' +
      (d.group ? '<div class="lk-section mt-4">' + groupHtml(d.group) + '</div>' : '') +
      '<div class="lk-section mt-4"><h6>Nguyên vật liệu (' + d.components.length + ')</h6>' + componentsHtml(d.components) + '</div>';

    el.actions.innerHTML =
      '<a class="btn btn-label-primary" href="' + cfg.v2Url + '?wo=' + encodeURIComponent(w.name) + '" target="_blank" rel="noopener"><i class="icon-base ti tabler-tag me-1"></i>Xem tem (/toastv2)</a>' +
      (lb.sale_order ? '<a class="btn btn-label-secondary" href="' + cfg.saleOrderUrl + '?so=' + encodeURIComponent(lb.sale_order) + '"><i class="icon-base ti tabler-file-search me-1"></i>Đơn bán</a>' : '') +
      '<button type="button" class="btn btn-label-secondary" data-bs-dismiss="modal">Đóng</button>';
  }

  function groupHtml(g) {
    return '<h6>Nhóm backorder <span class="lk-mono">' + esc(g.name) + '</span></h6><dl class="lk-info">' +
      L.row('Số lệnh', g.count + ' (' + g.doneCount + ' đã xong)') +
      L.row('Tổng SL nhóm', '<span class="fw-semibold">' + L.num(g.totalQty) + '</span> · đã xong ' + L.num(g.doneQty)) +
      L.row('Lệnh đầu → cuối', L.mono(g.first) + ' → ' + L.mono(g.last)) +
      L.row('Xong gần nhất', L.dt(g.lastFinished)) +
      L.row('Đang mở', g.open.length ? g.open.map(function (n) { return '<a href="#" class="lk-mono" data-open-wo="' + esc(n) + '">' + esc(n) + '</a>'; }).join(', ') : '<span class="text-muted">Không còn lệnh mở</span>') +
      '</dl>';
  }

  function componentsHtml(list) {
    if (!list.length) return '<div class="text-muted small">Không có</div>';
    return '<div class="table-responsive"><table class="table table-sm mb-0"><thead><tr><th>NVL</th><th class="text-end">Cần</th><th class="text-end">Đã dùng</th><th class="text-end">Sẵn có (dự báo)</th><th>Trạng thái</th></tr></thead><tbody>' +
      list.map(function (c) {
        return '<tr><td>' + esc(c.product) + '</td><td class="text-end">' + L.num(c.required) + '</td><td class="text-end">' + L.num(c.done) +
          '</td><td class="text-end">' + L.num(c.available) + '</td><td class="small text-muted">' + esc(c.state) + '</td></tr>';
      }).join('') + '</tbody></table></div>';
  }

  // ===== Khởi tạo =====

  async function loadProjects() {
    try {
      const res = await L.get(cfg.apiUrl + '/projects', cfg.loginUrl);
      el.project.insertAdjacentHTML('beforeend', res.projects.map(function (p) {
        return '<option value="' + esc(p.name) + '">' + esc(p.name) + (p.customer ? ' · ' + esc(p.customer) : '') + '</option>';
      }).join(''));
    } catch (err) { /* lọc dự án không có cũng vẫn tra được */ }
  }

  el.form.addEventListener('submit', function (e) { e.preventDefault(); search(false); });
  [el.kind, el.state].forEach(function (x) { x.addEventListener('change', function () { search(false); }); });
  el.more.addEventListener('click', function () { search(true); });
  // Tải lại: xoá ô tìm + bộ lọc (trạng thái về Đang mở), bỏ sắp xếp rồi tìm lại
  document.getElementById('wl-reload').addEventListener('click', function () {
    el.q.value = '';
    el.kind.value = '';
    el.state.value = 'open';
    $j(el.project).val('').trigger('change.select2');
    sort.set(null);
    search(false);
  });
  L.onRowClick(el.tbody, 'tr[data-wo]', function (tr) { openDetail(tr.dataset.wo); });
  el.detail.addEventListener('click', function (e) {
    const a = e.target.closest('[data-open-wo]');
    if (a) { e.preventDefault(); openDetail(a.dataset.openWo); }
  });
  el.modal.addEventListener('hidden.bs.modal', function () { saveUrl(params()); });

  (async function init() {
    const qs = new URLSearchParams(window.location.search);
    el.q.value = qs.get('q') || '';
    el.kind.value = qs.get('kind') || '';
    el.state.value = qs.get('state') || 'open';
    sort.set(qs.get('sort'), qs.get('dir'));
    await loadProjects();
    // Ô dự án có tìm kiếm (select2 của Vuexy); select2 báo đổi qua sự kiện jQuery
    $j(el.project).val(qs.get('project') || '').select2({ placeholder: 'Tất cả dự án', allowClear: true, width: '100%' })
      .on('change', function () { search(false); });
    search(false);
    if (qs.get('wo')) openDetail(qs.get('wo'));
  })();
})();
