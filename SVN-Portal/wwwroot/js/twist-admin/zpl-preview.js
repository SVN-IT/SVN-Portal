/**
 * Khung xem trước hình tem dùng chung trong khu quản trị Twist (Mẫu tem, In tem).
 * Markup: Areas/TwistAdmin/Views/Shared/_ZplPreviewPanel.cshtml, CSS: wwwroot/css/twist-admin/zpl-preview.css
 * Dùng: TwistZplPreview.create('id-khung', { previewUrl, getSource: () => ({ zplData, size, fileName }), request })
 */
window.TwistZplPreview = (function () {
  'use strict';
  // Khung xem trước hình tem. getSource() trả về { zplData, size, fileName }
  // Điều khiển: xoay 90°, phóng to/thu nhỏ (nút, thanh trượt, Ctrl + lăn chuột), Shift + lăn chuột cuộn ngang, kéo chuột để di chuyển
  // options: { previewUrl, getSource, request(url, fetchOptions) -> JSON }
  function create(rootId, options) {
    const getSource = options.getSource, request = options.request, previewUrl = options.previewUrl;
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
        const res = await request(previewUrl, {
          method: 'POST',
          body: JSON.stringify({ zplData: src.zplData, size: src.size, po: po ? po.value.trim() : '', dpmm: parseInt(dpmm.value, 10) })
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
        const warnings = [];
        if (res.sizeWarning) warnings.push(escapeHtml(res.sizeWarning));
        if (res.skippedElements && res.skippedElements.length) {
          warnings.push('Có phần tử không vẽ được nên bị bỏ qua (biến chưa điền hoặc dữ liệu không hợp lệ với loại mã vạch):<br>' +
            res.skippedElements.map(function (m) { return '• ' + escapeHtml(m); }).join('<br>'));
        }
        note.innerHTML = warnings.map(function (w) {
          return '<div class="alert alert-warning d-flex gap-2 py-2 px-3 mb-1" role="alert"><i class="icon-base ti tabler-alert-triangle flex-shrink-0 mt-1"></i><span>' + w + '</span></div>';
        }).join('') + escapeHtml(notes.join(' '));
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
      if (po) po.value = '';
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
    if (po) {
      po.addEventListener('input', refreshDebounced);
      po.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); refresh(); }
      });
    }
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

  function escapeHtml(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function downloadUrl(url, fileName) {
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
  }

  return { create: create };
})();
