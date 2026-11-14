/**
 * Bấm 1 lần vào dòng bảng để xem / sửa (dùng chung các màn admin, giống màn Tra cứu lệnh sản xuất).
 * - Không mở khi người dùng bôi đen chữ (kéo chuột) hoặc double / triple click để chọn từ: chờ 1 nhịp double-click (250 ms),
 *   có lần bấm thứ 2 thì huỷ; hết nhịp mà đang có chữ được bôi đen thì cũng không mở.
 * - Bấm vào nút / link / ô nhập trong dòng thì để nút đó tự xử lý.
 *
 * ToastRowClick.button(tbody, buttonSelector): bấm dòng = bấm nút buttonSelector trong dòng đó (VD nút Xem / Sửa ở cột thao tác,
 *   nút bị disabled thì thôi). Nút vẫn là button thật nên bàn phím / Vimium dùng được.
 * ToastRowClick.on(container, rowSelector, handler): bấm dòng khớp rowSelector thì gọi handler(tr) (dòng không có nút).
 * Dòng bấm được có con trỏ bàn tay (class .row-click, site.css).
 */
(function () {
  'use strict';

  const DOUBLE_CLICK_MS = 250;

  function hasSelection() {
    const sel = window.getSelection ? window.getSelection() : null;
    return !!sel && sel.toString().trim().length > 0;
  }

  function on(container, rowSelector, handler) {
    let timer = null;
    container.classList.add('row-click');
    container.addEventListener('click', function (e) {
      const tr = e.target.closest(rowSelector);
      if (!tr || !container.contains(tr) || e.target.closest('a, button, input, select, textarea, label')) return;
      clearTimeout(timer);
      if (e.detail > 1) return; // lần bấm thứ 2 trở đi của double / triple click
      timer = setTimeout(function () {
        if (!hasSelection()) handler(tr);
      }, DOUBLE_CLICK_MS);
    });
  }

  function button(tbody, buttonSelector) {
    on(tbody, 'tr', function (tr) {
      const btn = tr.querySelector(buttonSelector);
      if (btn && !btn.disabled) btn.click();
    });
  }

  window.ToastRowClick = { on: on, button: button };
})();
