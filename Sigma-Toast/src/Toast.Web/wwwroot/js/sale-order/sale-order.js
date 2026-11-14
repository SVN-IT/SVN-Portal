/**
 * Tra cứu đơn bán - /toast/admin/sale-order. Đọc Viindoo (chỉ đọc) qua GET {apiUrl}?so=...
 * Hiện các dòng đơn (PO / Lot / deadline theo dòng) và nhóm WO ghép theo số lượng. WO mở /toastv2?wo=... ở tab mới.
 * Nhớ ?so= trên thanh địa chỉ để tải lại / gửi link vẫn ra đúng đơn.
 */
(function () {
  'use strict';

  const cfg = window.SaleOrderPage;
  const $ = function (id) { return document.getElementById(id); };
  const el = {
    form: $('so-form'), input: $('so-input'), submit: $('so-submit'), reload: $('so-reload'),
    alerts: $('so-alerts'), body: $('so-body'), title: $('so-title'), info: $('so-info'),
    lines: $('so-lines'), unmatchedCard: $('so-unmatched-card'), unmatched: $('so-unmatched')
  };
  const state = { so: '' };
  const STATE_TEXT = { draft: 'Báo giá', sent: 'Đã gửi', sale: 'Đơn bán', done: 'Đã khoá', cancel: 'Đã huỷ' };

  async function lookup(so) {
    so = (so || '').trim();
    if (!so) { el.input.focus(); return; }
    setBusy(true);
    try {
      const res = await fetch(cfg.apiUrl + '?so=' + encodeURIComponent(so), { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      if (res.status === 401) {
        window.location.href = cfg.loginUrl + '?returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search);
        return;
      }
      if (!res.ok) throw new Error('Lỗi máy chủ (' + res.status + ').');
      const data = await res.json();
      if (!data.success) throw new Error(data.error);
      state.so = data.order.name;
      el.input.value = data.order.name;
      history.replaceState(null, '', '?so=' + encodeURIComponent(data.order.name));
      render(data.order);
    } catch (err) {
      el.body.classList.add('d-none');
      el.alerts.innerHTML = alert('danger', 'tabler-alert-circle', [err.message]);
    } finally {
      setBusy(false);
    }
  }

  function render(o) {
    const matched = o.lines.filter(function (l) { return l.group; }).length;
    const notes = o.warnings.slice();
    if (o.lines.length && matched < o.lines.length) notes.push((o.lines.length - matched) + ' dòng chưa ghép được nhóm WO.');
    el.alerts.innerHTML = notes.length ? alert('warning', 'tabler-alert-triangle', notes) : '';

    el.title.innerHTML = esc(o.name) + ' <span class="badge bg-label-secondary ms-1">' + esc(STATE_TEXT[o.state] || o.state) + '</span>';
    el.info.innerHTML =
      row('Khách hàng', val(o.customer)) +
      row('Ngày đặt', val(o.dateOrder)) +
      row('PO from ERP', mono(o.rawPo)) +
      row('Lot ID', mono(o.rawLot)) +
      row('Deadline from ERP', mono(o.rawDeadline)) +
      row('Số lệnh sản xuất', o.woCount + ' lệnh có origin = ' + esc(o.name) + ' · ' + matched + '/' + o.lines.length + ' dòng ghép được nhóm WO');

    el.lines.innerHTML = o.lines.length ? o.lines.map(function (l) {
      const g = l.group;
      return '<tr>' +
        '<td class="text-muted">' + l.no + '</td>' +
        '<td>' + esc(l.product) + ' <span class="text-muted small">(ID ' + l.productId + ')</span></td>' +
        '<td class="text-end fw-semibold">' + num(l.qty) + '</td>' +
        '<td>' + mono(l.po) + '</td>' +
        '<td>' + mono(l.lot) + '</td>' +
        '<td>' + val(formatDeadline(l.deadline)) + '</td>' +
        (g
          ? '<td class="so-mono">' + esc(g.groupName) + ' <i class="icon-base ti tabler-check text-success" title="Tổng SL nhóm = SL dòng"></i></td>' +
            '<td class="text-end">' + g.count + '</td>' +
            '<td class="text-end">' + num(g.totalQty) + '</td>' +
            '<td>' + woLinks(g) + '</td>'
          : '<td colspan="4" class="text-muted small">Chưa ghép được nhóm WO</td>') +
        '</tr>';
    }).join('') : '<tr><td colspan="10" class="text-center text-muted py-4">Đơn không có dòng nào</td></tr>';

    el.unmatchedCard.classList.toggle('d-none', !o.unmatchedGroups.length);
    el.unmatched.innerHTML = o.unmatchedGroups.map(function (g) {
      return '<tr><td class="so-mono">' + esc(g.groupName) + '</td><td>' + esc(g.product) + '</td>' +
        '<td class="text-end">' + g.count + '</td><td class="text-end">' + num(g.totalQty) + '</td><td>' + woLinks(g) + '</td></tr>';
    }).join('');

    el.body.classList.remove('d-none');
  }

  // Lệnh đang mở (tối đa 3); không có thì lệnh mới nhất kèm trạng thái
  function woLinks(g) {
    const link = function (name) {
      return '<a class="so-mono" href="' + cfg.v2Url + '?wo=' + encodeURIComponent(name) + '" target="_blank" rel="noopener" title="Xem tem ở /toastv2">' + esc(name) + '</a>';
    };
    if (g.openWos.length) {
      const shown = g.openWos.slice(0, 3).map(link).join(', ');
      return '<span class="badge bg-label-success me-1">Đang mở</span>' + shown + (g.openWos.length > 3 ? ' <span class="text-muted small">+' + (g.openWos.length - 3) + '</span>' : '');
    }
    return '<span class="badge bg-label-secondary me-1">' + esc(g.latestState === 'done' ? 'Đã xong' : g.latestState) + '</span>' + link(g.latestWo);
  }

  // "20260714" -> "14/07/2026"
  function formatDeadline(s) {
    return /^\d{8}$/.test(s || '') ? s.slice(6, 8) + '/' + s.slice(4, 6) + '/' + s.slice(0, 4) : (s || '');
  }

  // ===== Helpers =====

  function alert(type, icon, lines) {
    return '<div class="alert alert-' + type + ' mb-0 d-flex gap-2" role="' + (type === 'danger' ? 'alert' : 'status') + '">' +
      '<i class="icon-base ti ' + icon + ' mt-1 flex-shrink-0"></i><div>' +
      lines.map(function (l) { return '<div>' + esc(l) + '</div>'; }).join('') + '</div></div>';
  }

  function row(label, html) { return '<dt>' + esc(label) + '</dt><dd>' + html + '</dd>'; }
  function val(v) { return v ? esc(v) : '<span class="text-muted">—</span>'; }
  function mono(v) { return v ? '<span class="so-mono">' + esc(v) + '</span>' : '<span class="text-muted">—</span>'; }
  function num(n) { return Number(n || 0).toLocaleString('vi-VN'); }

  function setBusy(busy) {
    el.submit.disabled = busy;
    el.reload.disabled = busy;
    el.submit.innerHTML = busy
      ? '<span class="spinner-border spinner-border-sm me-1" role="status"></span>Đang đọc...'
      : '<i class="icon-base ti tabler-search me-1"></i>Tra';
  }

  function esc(s) {
    return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  el.form.addEventListener('submit', function (e) { e.preventDefault(); lookup(el.input.value); });
  el.reload.addEventListener('click', function () { lookup(state.so); });

  const initial = new URLSearchParams(window.location.search).get('so');
  if (initial) { el.input.value = initial; lookup(initial); }
})();
