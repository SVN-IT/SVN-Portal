/**
 * Bảng danh sách lọc ở trình duyệt có phân trang + sắp xếp, giống màn Mẫu tem (/toast/admin/label-template).
 * Dùng cho Đóng gói, Máy in, Phiên làm việc. HTML cần: <tbody>, ô thông tin "Hiển thị x–y / n bản ghi", <ul class="pagination">,
 * select số dòng / trang, và tiêu đề cột sắp xếp được: <th aria-sort="none"><button type="button" class="list-sort" data-sort="khoá">…<i></i></button></th>.
 *
 * const table = ListTable({ tbody, info, pagination, pageSize, colspan, emptyText, rows: () => mảng đã lọc, rowHtml, sortValue: { khoá: r => giá trị } });
 * table.render() sau khi lọc đổi (về trang 1: table.render(true)); table.reset() khi bấm Tải lại (về trang 1, bỏ sắp xếp).
 * Bấm tiêu đề: tăng dần -> giảm dần -> bỏ (về thứ tự server trả).
 */
window.ListTable = function (opts) {
  'use strict';
  const state = { page: 1, key: null, dir: 'asc' };
  const buttons = Array.from(opts.tbody.closest('table').querySelectorAll('.list-sort'));

  function sorted(rows) {
    if (!state.key) return rows;
    const get = opts.sortValue[state.key];
    const dir = state.dir === 'asc' ? 1 : -1;
    return rows.slice().sort(function (a, b) {
      const va = get(a), vb = get(b);
      if (va < vb) return -dir;
      if (va > vb) return dir;
      return 0;
    });
  }

  function paintSort() {
    buttons.forEach(function (btn) {
      const active = btn.dataset.sort === state.key;
      const icon = !active ? 'tabler-arrows-sort' : (state.dir === 'asc' ? 'tabler-sort-ascending' : 'tabler-sort-descending');
      btn.classList.toggle('is-active', active);
      btn.querySelector('i').className = 'icon-base ti ' + icon;
      btn.parentElement.setAttribute('aria-sort', !active ? 'none' : (state.dir === 'asc' ? 'ascending' : 'descending'));
    });
  }

  function render(firstPage) {
    if (firstPage) state.page = 1;
    const rows = sorted(opts.rows());
    const pageSize = parseInt(opts.pageSize.value, 10) || 25;
    const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
    state.page = Math.min(state.page, totalPages);
    const start = (state.page - 1) * pageSize;
    const pageRows = rows.slice(start, start + pageSize);
    opts.tbody.innerHTML = pageRows.length
      ? pageRows.map(opts.rowHtml).join('')
      : '<tr><td colspan="' + opts.colspan + '" class="text-center text-muted py-5">' + opts.emptyText + '</td></tr>';
    opts.info.textContent = rows.length === 0
      ? 'Không có bản ghi'
      : 'Hiển thị ' + (start + 1) + '–' + (start + pageRows.length) + ' / ' + rows.length + ' bản ghi';
    renderPagination(totalPages);
  }

  function renderPagination(totalPages) {
    const p = state.page;
    let html = pageItem(p - 1, '<i class="icon-base ti tabler-chevron-left"></i>', p === 1, false, 'Trang trước');
    for (let i = Math.max(1, p - 2); i <= Math.min(totalPages, p + 2); i++) html += pageItem(i, String(i), false, i === p);
    html += pageItem(p + 1, '<i class="icon-base ti tabler-chevron-right"></i>', p === totalPages, false, 'Trang sau');
    opts.pagination.innerHTML = html;
  }

  function pageItem(page, label, disabled, active, ariaLabel) {
    return '<li class="page-item' + (disabled ? ' disabled' : '') + (active ? ' active' : '') + '">' +
      '<a class="page-link" href="#" data-page="' + page + '"' + (ariaLabel ? ' aria-label="' + ariaLabel + '"' : '') + '>' + label + '</a></li>';
  }

  buttons.forEach(function (btn) {
    btn.addEventListener('click', function () {
      const key = btn.dataset.sort;
      if (state.key !== key) { state.key = key; state.dir = 'asc'; }
      else if (state.dir === 'asc') state.dir = 'desc';
      else state.key = null;
      paintSort();
      render(true);
    });
  });
  opts.pagination.addEventListener('click', function (e) {
    const a = e.target.closest('a[data-page]');
    if (!a) return;
    e.preventDefault();
    if (a.parentElement.classList.contains('disabled')) return;
    state.page = parseInt(a.dataset.page, 10);
    render();
  });
  opts.pageSize.addEventListener('change', function () { render(true); });

  return {
    render: render,
    reset: function () { state.page = 1; state.key = null; state.dir = 'asc'; paintSort(); }
  };
};
