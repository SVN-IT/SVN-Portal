/**
 * Hàm dùng chung cho 2 màn tra cứu Viindoo (Lệnh sản xuất, Sản phẩm Viindoo).
 */
window.Lookup = (function () {
  'use strict';

  const WO_STATE = {
    draft: ['bg-label-secondary', 'Nháp'], confirmed: ['bg-label-info', 'Đã xác nhận'], progress: ['bg-label-warning', 'Đang làm'],
    to_close: ['bg-label-warning', 'Chờ đóng'], done: ['bg-label-success', 'Hoàn tất'], cancel: ['bg-label-danger', 'Đã huỷ']
  };
  const TRACKING = { none: 'Không', lot: 'Theo lô', serial: 'Theo serial' };

  // GET JSON; 401 -> về trang đăng nhập
  async function get(url, loginUrl) {
    const res = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
    if (res.status === 401) {
      window.location.href = loginUrl + '?returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search);
      throw new Error('Phiên đăng nhập đã hết hạn.');
    }
    if (!res.ok) throw new Error('Lỗi máy chủ (' + res.status + ').');
    const data = await res.json();
    if (!data.success) throw new Error(data.error);
    return data;
  }

  // Dự án · loại · loại con (+ khách) dạng badge
  function classBadges(c) {
    if (!c) return '';
    return (c.project ? '<span class="badge bg-label-primary me-1">' + esc(c.project) + '</span>' : '') +
      (c.subType ? '<span class="badge bg-label-info me-1">' + esc(c.subType) + '</span>' : '') +
      (c.kind ? '<span class="badge bg-label-secondary">' + esc(c.kind) + '</span>' : '') +
      (c.customer ? '<div class="small text-muted mt-1">' + esc(c.customer) + '</div>' : '');
  }

  function woState(s) {
    const m = WO_STATE[s] || ['bg-label-secondary', s || '?'];
    return '<span class="badge ' + m[0] + '">' + esc(m[1]) + '</span>';
  }

  // Viindoo trả giờ UTC "2026-07-15 01:28:30" -> giờ VN dd/MM/yyyy HH:mm
  function dt(s, dateOnly) {
    if (!s) return '<span class="text-muted">—</span>';
    const d = new Date(s.replace(' ', 'T') + 'Z');
    if (isNaN(d)) return esc(s);
    const p = function (n) { return String(n).padStart(2, '0'); };
    return p(d.getDate()) + '/' + p(d.getMonth() + 1) + '/' + d.getFullYear() + (dateOnly ? '' : ' ' + p(d.getHours()) + ':' + p(d.getMinutes()));
  }

  function num(n) { return Number(n || 0).toLocaleString('vi-VN'); }
  function val(v) { return v ? esc(v) : '<span class="text-muted">—</span>'; }
  function mono(v) { return v ? '<span class="lk-mono">' + esc(v) + '</span>' : '<span class="text-muted">—</span>'; }
  function row(label, html) { return '<dt>' + esc(label) + '</dt><dd>' + html + '</dd>'; }

  function alert(type, icon, lines) {
    return '<div class="alert alert-' + type + ' d-flex gap-2" role="' + (type === 'danger' ? 'alert' : 'status') + '">' +
      '<i class="icon-base ti ' + icon + ' mt-1 flex-shrink-0"></i><div>' +
      lines.map(function (l) { return '<div>' + esc(l) + '</div>'; }).join('') + '</div></div>';
  }

  // Nút Xem chi tiết ở cột thao tác cuối bảng (giống màn Phiên làm việc, <th style="width: 70px;"></th>): dòng bấm được bằng chuột,
  // nút này cho bàn phím / Vimium (Vimium chỉ gợi ý phần tử bấm được như button, không thấy dòng <tr> gắn sự kiện).
  function openButton() {
    return '<button type="button" class="btn btn-sm btn-icon btn-label-primary" data-row-open aria-label="Xem chi tiết" title="Xem chi tiết">' +
      '<i class="icon-base ti tabler-eye"></i></button>';
  }

  // Bấm nút openButton (chuột, Enter, Vimium) thì gọi handler(tr) ngay; bấm vào dòng = bấm nút đó, phân biệt bôi đen /
  // double-click (js/shared/row-click.js).
  function onRowClick(tbody, selector, handler) {
    tbody.addEventListener('click', function (e) {
      if (!e.target.closest('[data-row-open]')) return;
      const tr = e.target.closest(selector);
      if (tr) handler(tr);
    });
    window.ToastRowClick.button(tbody, '[data-row-open]');
  }

  // Nút sắp xếp trên tiêu đề cột (giống màn Mẫu tem): <th aria-sort="none"><button class="lk-sort" data-sort="khoá">…</button></th>
  // Bấm: tăng dần -> giảm dần -> bỏ (về thứ tự mặc định của server). Server sắp xếp (danh sách phân trang).
  // Trả về { key, dir, set(key, dir) }; onChange() gọi sau mỗi lần đổi.
  function sortHeaders(root, onChange) {
    const st = { key: null, dir: 'asc' };
    const buttons = Array.from(root.querySelectorAll('.lk-sort'));
    function paint() {
      buttons.forEach(function (btn) {
        const active = btn.dataset.sort === st.key;
        const icon = !active ? 'tabler-arrows-sort' : (st.dir === 'asc' ? 'tabler-sort-ascending' : 'tabler-sort-descending');
        btn.classList.toggle('is-active', active);
        btn.querySelector('i').className = 'icon-base ti ' + icon;
        btn.parentElement.setAttribute('aria-sort', !active ? 'none' : (st.dir === 'asc' ? 'ascending' : 'descending'));
      });
    }
    buttons.forEach(function (btn) {
      btn.addEventListener('click', function () {
        const key = btn.dataset.sort;
        if (st.key !== key) { st.key = key; st.dir = 'asc'; }
        else if (st.dir === 'asc') st.dir = 'desc';
        else { st.key = null; st.dir = 'asc'; }
        paint();
        onChange();
      });
    });
    st.set = function (key, dir) {
      st.key = buttons.some(function (b) { return b.dataset.sort === key; }) ? key : null;
      st.dir = dir === 'desc' ? 'desc' : 'asc';
      paint();
    };
    return st;
  }

  function esc(s) {
    return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  return { sortHeaders: sortHeaders, onRowClick: onRowClick, openButton: openButton, get: get, classBadges: classBadges, woState: woState, dt: dt, num: num, val: val, mono: mono, row: row, alert: alert, esc: esc, TRACKING: TRACKING };
})();
