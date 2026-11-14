/**
 * Tra cứu sản phẩm Viindoo - /toast/admin/viindoo-product (đọc Viindoo, chỉ đọc).
 * Danh sách: GET {apiUrl}?q=&project=&kind=&offset=  ·  Chi tiết: GET {apiUrl}/{id}
 * ?id=... trên thanh địa chỉ mở thẳng chi tiết.
 */
(function () {
  'use strict';

  const cfg = window.ProductLookupPage;
  const L = window.Lookup, esc = L.esc;
  const $ = function (id) { return document.getElementById(id); };
  const el = {
    form: $('pl-form'), q: $('pl-q'), project: $('pl-project'), kind: $('pl-kind'),
    tbody: $('pl-tbody'), summary: $('pl-summary'), moreBox: $('pl-more-box'), more: $('pl-more'),
    modal: $('pl-modal'), title: $('pl-modal-title'), detail: $('pl-detail')
  };
  const modal = new bootstrap.Modal(el.modal);
  const $j = window.jQuery;
  const state = { items: [], offset: 0, loading: false };
  const sort = L.sortHeaders(document.querySelector('#pl-tbody').closest('table'), function () { search(false); });

  function params() {
    return { q: el.q.value.trim(), project: el.project.value, kind: el.kind.value, sort: sort.key || '', dir: sort.key ? sort.dir : '' };
  }

  async function search(append) {
    if (state.loading) return;
    state.loading = true;
    const p = params();
    const offset = append ? state.offset : 0;
    if (!append) el.tbody.innerHTML = '<tr><td colspan="8" class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></td></tr>';
    el.more.disabled = true;
    try {
      const res = await L.get(cfg.apiUrl + '?' + new URLSearchParams(Object.assign({}, p, { offset: offset })), cfg.loginUrl);
      const d = res.data;
      state.items = append ? state.items.concat(d.items) : d.items;
      state.offset = d.offset + d.items.length;
      render();
      el.moreBox.classList.toggle('d-none', !d.more);
      el.summary.textContent = state.items.length + ' sản phẩm' + (d.more ? ' (còn nữa)' : '') + (sort.key ? '' : ' · xếp theo mã');
      saveUrl(p);
    } catch (err) {
      el.tbody.innerHTML = '<tr><td colspan="8" class="text-center text-danger py-5">' + esc(err.message) + '</td></tr>';
    } finally {
      state.loading = false;
      el.more.disabled = false;
    }
  }

  function render() {
    el.tbody.innerHTML = state.items.length ? state.items.map(function (p) {
      return '<tr data-id="' + p.id + '">' +
        '<td class="lk-mono fw-medium text-heading">' + L.val(p.defaultCode) + '</td>' +
        '<td>' + esc(p.name) + '</td>' +
        '<td>' + L.classBadges(p.class) + '</td>' +
        '<td>' + L.mono(p.customerCode) + '</td>' +
        '<td class="text-end">' + L.val(p.qtyPerCode) + '</td>' +
        '<td class="small">' + esc(L.TRACKING[p.tracking] || p.tracking) + '</td>' +
        '<td class="text-end text-muted">' + p.id + '</td>' +
        '<td>' + L.openButton() + '</td>' +
        '</tr>';
    }).join('') : '<tr><td colspan="8" class="text-center text-muted py-5">Không có sản phẩm nào</td></tr>';
  }

  function saveUrl(p, id) {
    const qs = new URLSearchParams();
    Object.keys(p).forEach(function (k) { if (p[k] && !(k === 'kind' && p[k] === 'F/G')) qs.set(k, p[k]); });
    if (p.kind === '') qs.set('kind', '');
    if (id) qs.set('id', id);
    history.replaceState(null, '', qs.toString() ? '?' + qs : window.location.pathname);
  }

  async function openDetail(id) {
    el.title.textContent = 'Sản phẩm ID ' + id;
    el.detail.innerHTML = '<div class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></div>';
    modal.show();
    saveUrl(params(), id);
    try {
      renderDetail((await L.get(cfg.apiUrl + '/' + id, cfg.loginUrl)).detail);
    } catch (err) {
      el.detail.innerHTML = L.alert('danger', 'tabler-alert-circle', [err.message]);
    }
  }

  function renderDetail(d) {
    const p = d.product, t = d.toast;
    el.title.innerHTML = '<span class="lk-mono">' + esc(p.defaultCode || '(không mã)') + '</span> · ' + esc(p.name);
    const tpl = t.templates.length
      ? t.templates.map(function (x) { return '<a href="' + cfg.templateUrl + '">' + esc(x.type + ' · ' + x.operation + ' · ' + x.size) + '</a>'; }).join(', ')
      : '<span class="text-muted">Chưa có</span>';
    el.detail.innerHTML =
      '<div class="row g-4">' +
      '<div class="col-lg-6"><div class="lk-section"><h6>Thông tin</h6><dl class="lk-info">' +
        L.row('ID Viindoo', String(p.id)) +
        L.row('Mã (default code)', L.mono(p.defaultCode)) +
        L.row('Tên', esc(p.name)) +
        L.row('Phân loại', L.classBadges(p.class)) +
        L.row('Nhóm Viindoo', L.val(p.category)) +
        L.row('Mã khách (x_custcode)', L.mono(p.customerCode)) +
        L.row('Pcs / box (x_qty)', L.val(p.qtyPerCode)) +
        L.row('Đơn vị', L.val(p.uom)) +
        L.row('Theo dõi', esc(L.TRACKING[p.tracking] || p.tracking)) +
      '</dl></div></div>' +
      '<div class="col-lg-6"><div class="lk-section"><h6>Toast</h6><dl class="lk-info">' +
        L.row('Quy cách (Sản phẩm)', t.product
          ? '<a href="' + cfg.toastProductUrl + '">Có</a>' + (t.product.scanSource === 'LotId' ? ' · quét Lot ID · ' : ' · barcode <code>' + esc(t.product.barcode) + '</code> · ') + t.product.pcsPerBox + ' pcs/box'
          : '<span class="text-muted">Chưa có</span>') +
        L.row('Mẫu tem', tpl) +
      '</dl><h6 class="mt-4">Đơn bán gần đây</h6>' +
        (d.saleOrders.length ? '<ul class="list-unstyled mb-0">' + d.saleOrders.map(function (s) {
          return '<li><a class="lk-mono" href="' + cfg.saleOrderUrl + '?so=' + encodeURIComponent(s.order) + '">' + esc(s.order) + '</a> · ' + L.num(s.qty) + ' · ' + L.dt(s.created, true) + '</li>';
        }).join('') + '</ul>' : '<div class="text-muted small">Không có</div>') +
      '</div></div>' +
      '</div>' +
      '<div class="lk-section mt-4"><h6>WO đang mở (' + d.openWos.length + ')</h6>' +
        (d.openWos.length ? '<div class="table-responsive"><table class="table table-sm mb-0"><thead><tr><th>WO</th><th class="text-end">Đã làm / Cần</th><th>Trạng thái</th><th>Deadline</th><th>Đơn bán</th></tr></thead><tbody>' +
          d.openWos.map(function (w) {
            return '<tr><td><a class="lk-mono" href="' + cfg.workOrderUrl + '?wo=' + encodeURIComponent(w.name) + '">' + esc(w.name) + '</a></td>' +
              '<td class="text-end">' + L.num(w.qtyProduced) + ' / ' + L.num(w.productQty) + '</td><td>' + L.woState(w.state) + '</td>' +
              '<td>' + L.dt(w.deadline, true) + '</td><td>' + L.mono(w.origin) + '</td></tr>';
          }).join('') + '</tbody></table></div>' : '<div class="text-muted small">Không có</div>') +
      '</div>' +
      '<div class="lk-section mt-4"><h6>BoM' + (d.bom ? ': ' + esc(d.bom.name) + (d.bom.count > 1 ? ' <span class="text-muted small">(' + d.bom.count + ' bản, đang xem bản mới nhất)</span>' : '') : '') + '</h6>' +
        (d.bom && d.bom.lines.length ? '<div class="table-responsive"><table class="table table-sm mb-0"><thead><tr><th>NVL</th><th class="text-end">SL / ' + L.num(d.bom.qty) + ' sp</th><th>Đơn vị</th></tr></thead><tbody>' +
          d.bom.lines.map(function (l) { return '<tr><td>' + esc(l.product) + '</td><td class="text-end">' + L.num(l.qty) + '</td><td>' + esc(l.uom) + '</td></tr>'; }).join('') +
          '</tbody></table></div>' : '<div class="text-muted small">Không có BoM</div>') +
      '</div>';
  }

  async function loadProjects() {
    try {
      const res = await L.get(cfg.apiUrl + '/projects', cfg.loginUrl);
      el.project.insertAdjacentHTML('beforeend', res.projects.map(function (p) {
        return '<option value="' + esc(p.name) + '">' + esc(p.name) + (p.customer ? ' · ' + esc(p.customer) : '') + '</option>';
      }).join(''));
    } catch (err) { /* không có danh sách dự án vẫn tra được */ }
  }

  el.form.addEventListener('submit', function (e) { e.preventDefault(); search(false); });
  el.kind.addEventListener('change', function () { search(false); });
  el.more.addEventListener('click', function () { search(true); });
  // Tải lại: xoá ô tìm + bộ lọc (loại về Thành phẩm như lúc mở màn), bỏ sắp xếp rồi tìm lại
  document.getElementById('pl-reload').addEventListener('click', function () {
    el.q.value = '';
    el.kind.value = 'F/G';
    $j(el.project).val('').trigger('change.select2');
    sort.set(null);
    search(false);
  });
  L.onRowClick(el.tbody, 'tr[data-id]', function (tr) { openDetail(parseInt(tr.dataset.id, 10)); });
  el.modal.addEventListener('hidden.bs.modal', function () { saveUrl(params()); });

  (async function init() {
    const qs = new URLSearchParams(window.location.search);
    el.q.value = qs.get('q') || '';
    if (qs.has('kind')) el.kind.value = qs.get('kind');
    sort.set(qs.get('sort'), qs.get('dir'));
    await loadProjects();
    // Ô dự án có tìm kiếm (select2 của Vuexy, giống màn Lệnh sản xuất); select2 báo đổi qua sự kiện jQuery
    $j(el.project).val(qs.get('project') || '').select2({ placeholder: 'Tất cả dự án', allowClear: true, width: '100%' })
      .on('change', function () { search(false); });
    search(false);
    if (qs.get('id')) openDetail(parseInt(qs.get('id'), 10));
  })();
})();
