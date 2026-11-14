/**
 * Màn công nhân - /toast
 * Máy in cài ở Menu → Máy in (modal Cấu hình máy in dùng chung với màn In tem, lưu trên trình duyệt) -> nhập WO -> modal mở phiên (danh sách kiểm tra, tick xác nhận, mã nhân viên)
 * Mã nhân viên: "SVN" cố định + ô chỉ nhập 4 số. Server kiểm tra mã có trên Viindoo, không có thì báo "Nhân viên không tồn tại" + viền đỏ ô mã.
 * -> quét barcode pcs: máy này tự so mã và đếm ngay (không chờ mạng), pcs đã quét nằm trong hàng chờ ở localStorage (OUTBOX_KEY + nhóm lệnh);
 *    đủ box (hoặc đủ Toast:ScanSyncEvery pcs) thì gửi cả lô lên server (server đóng box, in tem). Gửi kèm mã máy (DEVICE_KEY) + số thứ tự (SEQ_KEY): gửi lại không cộng đôi,
 *    máy khác đã vào làm phiên thì lô của máy này bị từ chối (bỏ, không cộng thừa). Gửi lỗi (mất mạng) thì giữ hàng chờ, quét tiếp,
 *    tự thử lại; box đủ lúc mất mạng được đóng + in bù khi có mạng. Trước thao tác Menu → Box cũng gửi hàng chờ.
 * Refresh / tắt máy: hàng chờ còn trong localStorage, mở lại WO + xác nhận (cùng máy) thì gửi tiếp. Đổi máy: máy mới xác nhận thì giữ phiên.
 * Mất mạng tới server (icon mạng đỏ): công nhân đổi máy in sang USB qua Node / TCP qua Node (Menu → Máy in, lúc mất mạng không hỏi PIN),
 *    box đủ được in ra máy đó qua agent bằng mẫu tem lưu trên máy
 *    (LABEL_KEY, tải từ label/offline, tải lại khi mẫu / lần xác nhận đổi), ngày giờ theo đồng hồ máy (lệch > 1 giờ thì không in offline);
 *    có mạng lại thì gửi kèm danh sách box đã in, server đóng box và không in lại.
 * Log lỗi (để debug máy công nhân): console [Toast] + 300 dòng gần nhất trong localStorage (LOG_KEY; gõ toastLog() / toastLog.download()
 *    trong console) + gửi về server (/client-log -> logs/client-yyyyMMdd.log), mất mạng thì gửi sau.
 * Trên 1 máy, 1 WO chỉ làm ở 1 tab (navigator.locks theo nhóm lệnh, khoá lúc vào làm việc); tab khác làm WO khác vẫn được.
 * Menu (F2 hoặc nút Menu) khoá bằng PIN leader: Máy in, In tem (in lại box / tem plate / carton), Box (in lại / đóng thiếu / sửa số / xoá).
 * Phiên tự kết thúc khi không hoạt động (Toast:AutoEndInactiveHours) hoặc admin kết thúc.
 * Nhập PIN đúng thì 2 phút sau khi đóng menu không phải nhập lại.
 * In qua TCP / USB qua Node: server điền biến rồi trả việc in, trình duyệt gửi tới agent localhost:8021 và báo kết quả cho box.
 */
(function () {
  'use strict';

  const cfg = window.StationPage;
  const OLD_PRINTER_KEY = 'toast.station.printerId';
  const PIN_GRACE_MS = 2 * 60 * 1000;
  const WO_KEY = 'toast.station.wo';
  const DEVICE_KEY = 'toast.station.device';
  // Số thứ tự quét / hàng chờ riêng từng nhóm lệnh (thêm ':' + nhóm): 2 tab làm 2 WO khác nhau không lẫn
  const SEQ_KEY = 'toast.station.seq:';
  const OUTBOX_KEY = 'toast.station.outbox:';
  const SYNC_RETRY_MS = 5000;
  const LABEL_KEY = 'toast.station.label:';
  const LOG_KEY = 'toast.station.log';
  const LOG_MAX = 300;
  const LOG_SEND_MAX = 50;
  const PING_MS = 10000;
  // Đang mất mạng: ping thưa hơn (mỗi lần lỗi trình duyệt tự in 1 dòng đỏ ở console); máy báo có mạng lại thì ping ngay
  const PING_OFFLINE_MS = 30000;
  const API_TIMEOUT_MS = 30000;
  // Gửi lô / ping: chờ ngắn để mất mạng thì sớm in qua agent
  const SYNC_TIMEOUT_MS = 10000;
  const PING_TIMEOUT_MS = 5000;
  // Đồng hồ máy lệch giờ server: > 5 phút cảnh báo, > 1 giờ (thường là sai múi giờ) không in offline
  const CLOCK_WARN_MS = 5 * 60 * 1000;
  const CLOCK_BLOCK_MS = 60 * 60 * 1000;
  const EMPLOYEE_DIGITS_RE = /^\d{4}$/;
  // Mã chống giả mạo form: server restart / deploy bản mới mà khoá đổi thì mã cũ bị từ chối (400) -> refreshToken() lấy mã mới
  let token = document.querySelector('input[name="__RequestVerificationToken"]').value;
  let tokenRefreshedAt = 0;
  const notyf = window.notyf;

  const $ = function (id) { return document.getElementById(id); };
  const el = {
    printerName: $('st-printer-name'), menuOpen: $('st-menu-open'), net: $('st-net'),
    noticesBody: $('st-notices-body'), woGroup: $('st-wo-group'), woError: $('st-wo-error'),
    woStep: $('st-wo-step'), woForm: $('st-wo-form'), wo: $('st-wo'), woOpen: $('st-wo-open'),
    run: $('st-run'), woName: $('st-wo-name'), info: $('st-info'), warnings: $('st-warnings'),
    boxLabel: $('st-box-label'), boxQty: $('st-box-qty'), boxCap: $('st-box-cap'), boxBar: $('st-box-bar'),
    closed: $('st-closed'), total: $('st-total'), planned: $('st-planned'), last: $('st-last'), sync: $('st-sync'),
    scanForm: $('st-scan-form'), scan: $('st-scan'), scanBlock: $('st-scan-block'), boxes: $('st-boxes'),
    boxesBadges: $('st-boxes-badges'), boxesMore: $('st-boxes-more'), boxesCount: $('st-boxes-count'), boxesToggle: $('st-boxes-toggle'),
    manualModal: $('st-manual-modal'), manualForm: $('st-manual-form'), copies: $('st-copies'), manualPrint: $('st-manual-print'),
    lblWo: $('st-lbl-wo'), lblLoad: $('st-lbl-load'), lblClear: $('st-lbl-clear'), lblWoInfo: $('st-lbl-wo-info'),
    lblVars: $('st-lbl-vars'), lblWarnings: $('st-lbl-warnings'), lblTpls: $('st-lbl-tpls'), lblTplQ: $('st-lbl-tpl-q'),
    lblTplSearch: $('st-lbl-tpl-search'), lblTplToggle: $('st-lbl-tpl-toggle'),
    sessionModal: $('st-session-modal'), sessionForm: $('st-session-form'), sessionWo: $('st-session-wo'),
    sessionResume: $('st-session-resume'), sessionItems: $('st-session-items'), sessionValues: $('st-session-values'),
    sessionConfirm: $('st-session-confirm'), sessionEmployee: $('st-session-employee'),
    sessionEmployeeError: $('st-session-employee-error'), sessionStart: $('st-session-start'),
    menuModal: $('st-menu-modal'), menuTitle: $('st-menu-title'), menuBack: $('st-menu-back'),
    menuPinView: $('st-menu-pin-view'), menuPin: $('st-menu-pin'), menuHome: $('st-menu-home'),
    menuBox: $('st-menu-box'), menuBoxList: $('st-menu-box-list'), menuBoxActions: $('st-menu-box-actions'),
    menuBoxTitle: $('st-menu-box-title'), menuBoxHint: $('st-menu-box-hint'), menuQtyForm: $('st-menu-qty-form'), menuQty: $('st-menu-qty')
  };
  const sessionModal = new bootstrap.Modal(el.sessionModal);
  const manualModal = new bootstrap.Modal(el.manualModal);
  const menuModal = new bootstrap.Modal(el.menuModal);

  // check: kết quả kiểm tra của modal mở phiên; checkWo: mã WO đã nhập (gửi lại khi xác nhận)
  // pin: PIN leader đã mở khoá menu (chỉ giữ khi menu / in thủ công đang mở); boxId: box đang chọn trong Menu → Box
  // pinUntil: hết hạn PIN (2 phút tính từ lúc đóng menu); next: modal mở tiếp sau khi menu đóng ('manual' | 'printer')
  const s = { wo: null, state: null, check: null, checkWo: '', pin: '', pinUntil: 0, boxId: null, deleteArmed: false, next: null, boxesExpanded: false };

  // ===== API =====

  // Không gọi được server (mất mạng / quá thời gian) hoặc server lỗi 5xx (VD mất kết nối DB): lỗi có network = true, icon mạng đỏ
  // Lấy mã chống giả mạo mới: tải ngầm trang /toast (kèm cookie mới), đọc ô ẩn __RequestVerificationToken
  async function refreshToken() {
    const res = await fetch(cfg.apiUrl.replace(/\/api$/, ''), { cache: 'no-store', signal: AbortSignal.timeout(10000) });
    const m = /name="__RequestVerificationToken"[^>]*value="([^"]+)"/.exec(await res.text());
    if (!m) throw new Error('Không lấy được mã xác thực mới.');
    token = m[1];
    tokenRefreshedAt = Date.now();
    const input = document.querySelector('input[name="__RequestVerificationToken"]');
    if (input) input.value = token;
  }

  // retried: đã thử lại sau khi lấy mã mới (chỉ 1 lần)
  async function api(path, body, timeoutMs, retried) {
    let res;
    try {
      res = await fetch(cfg.apiUrl + path, Object.assign({ signal: AbortSignal.timeout(timeoutMs || API_TIMEOUT_MS), cache: 'no-store' }, body === undefined
        ? { headers: { 'X-Requested-With': 'XMLHttpRequest' } }
        : {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': token },
          body: JSON.stringify(body)
        }));
    } catch (e) {
      // Chỉ ghi lần lỗi đầu (đang mất mạng rồi thì thôi); ping lỗi không ghi (setOnline ghi lúc đổi trạng thái)
      if (path !== '/ping' && net.online !== false)
        log('warn', 'api.network', path + ': ' + (e.name === 'TimeoutError' ? 'quá ' + ((timeoutMs || API_TIMEOUT_MS) / 1000) + ' giây' : e.message));
      setOnline(false);
      const err = new Error('Mất kết nối tới server.');
      err.network = true;
      throw err;
    }
    if (res.status >= 500) {
      if (path !== '/ping') log('error', 'api.server', path + ': HTTP ' + res.status, { status: res.status });
      setOnline(false);
      const err = new Error('Server không ghi được dữ liệu (lỗi ' + res.status + ').');
      err.network = true;
      throw err;
    }
    // 400 khi gửi dữ liệu = thường là mã chống giả mạo cũ (server vừa restart / deploy): lấy mã mới rồi gửi lại 1 lần
    if (res.status === 400 && body !== undefined && !retried) {
      log('warn', 'api.token', path + ': HTTP 400, lấy mã xác thực mới rồi gửi lại');
      try {
        await refreshToken();
      } catch (e) {
        log('error', 'api.token', 'Lấy mã xác thực mới lỗi: ' + e.message);
        throw new Error('Phiên trang đã hết hạn, tải lại trang (F5).');
      }
      return api(path, body, timeoutMs, true);
    }
    if (!res.ok) log('error', 'api.http', path + ': HTTP ' + res.status, { status: res.status });
    // Ping tự quyết trạng thái (server lên nhưng DB không được vẫn là mất mạng)
    if (path !== '/ping') setOnline(true);
    if (!res.ok) throw new Error('Lỗi máy chủ (' + res.status + ').');
    return res.json();
  }

  // ===== Mạng + đồng hồ =====

  // waitNoticedBox: số box lớn nhất đã báo vào card là đủ hàng mà chưa in được khi mất mạng (mỗi box 1 lần)
  const net = { online: null, skewMs: null, clockWarned: false, waitNoticedBox: null };

  function setOnline(on) {
    if (net.online === on) return;
    const wasOffline = net.online === false;
    if (net.online !== null || !on) log(on ? 'info' : 'warn', 'network', on ? 'Có mạng lại (server + DB)' : 'Mất mạng (không ghi được lên server)');
    net.online = on;
    el.net.className = 'badge st-net ' + (on ? 'bg-label-success' : 'bg-danger');
    // Chỉ icon; chữ ở title / aria-label (rê chuột, trình đọc màn hình)
    el.net.innerHTML = '<i class="icon-base ti ' + (on ? 'tabler-wifi' : 'tabler-wifi-off') + '"></i>';
    const text = on ? 'Có mạng: server và cơ sở dữ liệu đang kết nối' : 'Mất mạng: không ghi được dữ liệu lên server (mất mạng tới server hoặc server mất kết nối DB)';
    el.net.title = text;
    el.net.setAttribute('aria-label', text);
    // Đang ở màn làm việc: báo vào card lúc mất kết nối / có kết nối lại (lần đầu trang mở mà có mạng thì không báo)
    if (!on && isRunning())
      notice('warning', lines('Mất kết nối tới server Toast',
        'Vẫn quét và in tem bình thường, dữ liệu lưu trên máy, có kết nối lại sẽ tự gửi',
        ['Xử lý', 'Không đóng tab, không tải lại trang đến khi có kết nối lại']));
    if (on && wasOffline && isRunning())
      notice('success', lines('Đã kết nối lại server', typeof flushDue === 'function' && flushDue() ? 'Đang gửi dữ liệu lưu trên máy' : ''));
    if (on) net.waitNoticedBox = null;
    // Có mạng lại: gửi ngay pcs đang chờ (box đủ / đủ N pcs)
    if (on && typeof flushDue === 'function' && flushDue()) flush();
  }

  // Giờ server (chuỗi giờ địa phương không múi giờ) -> độ lệch đồng hồ máy này; lệch nhiều thì cảnh báo 1 lần
  function noteServerTime(now) {
    const t = now ? new Date(now).getTime() : NaN;
    if (isNaN(t)) return;
    net.skewMs = Date.now() - t;
    if (Math.abs(net.skewMs) > CLOCK_WARN_MS && !net.clockWarned) {
      net.clockWarned = true;
      alertError('Đồng hồ máy này lệch giờ server khoảng ' + Math.round(Math.abs(net.skewMs) / 60000) + ' phút. Chỉnh lại giờ máy (tem in khi mất mạng lấy giờ máy này' +
        (Math.abs(net.skewMs) > CLOCK_BLOCK_MS ? '; lệch quá 1 giờ nên sẽ không in khi mất mạng' : '') + ').');
    }
  }

  async function ping() {
    try {
      const res = await api('/ping', undefined, PING_TIMEOUT_MS);
      noteServerTime(res.now);
      if (!res.db && net.online !== false) log('warn', 'ping.db', res.error || 'Server không kết nối được DB');
      setOnline(!!res.db);
      if (res.db) sendLogs();
    } catch (e) { /* api đã đổi icon */ }
  }

  // ===== Log lỗi =====
  // log(level 'error' | 'warn' | 'info', sự kiện, nội dung, dữ liệu): console + localStorage (giữ qua F5) + gửi về server khi có mạng

  function logRead() {
    try { return JSON.parse(localStorage.getItem(LOG_KEY) || '[]'); } catch (e) { return []; }
  }
  function logWrite(list) {
    try { localStorage.setItem(LOG_KEY, JSON.stringify(list.slice(-LOG_MAX))); } catch (e) { /* đầy / bị chặn */ }
  }

  function log(level, event, message, data) {
    let wo = '';
    try { wo = (s.state && s.state.woName) || s.checkWo || ''; } catch (e) { /* lỗi lúc trang chưa khởi tạo xong */ }
    const entry = {
      id: Date.now().toString(36) + Math.random().toString(36).slice(2, 6),
      time: localIso(new Date()), level: level, event: event, message: String(message == null ? '' : message),
      wo: wo, data: data === undefined ? null : data, sent: false
    };
    try {
      const out = level === 'error' ? console.error : level === 'warn' ? console.warn : console.info;
      out.call(console, '[Toast] ' + entry.time + ' ' + event + (wo ? ' (' + wo + ')' : '') + ': ' + entry.message, data === undefined ? '' : data);
    } catch (e) { /* không có console */ }
    const list = logRead();
    list.push(entry);
    logWrite(list);
    scheduleLogSend();
  }

  let logTimer = null, logSending = false;
  function scheduleLogSend() {
    clearTimeout(logTimer);
    logTimer = setTimeout(sendLogs, 2000);
  }

  // Gửi log chưa gửi về server (theo lô 50 dòng). Dùng fetch riêng: lỗi gửi log không ghi log (tránh lặp)
  async function sendLogs() {
    if (logSending) return;
    try { if (net.online === false) return; } catch (e) { return; }
    const batch = logRead().filter(function (e) { return !e.sent; }).slice(0, LOG_SEND_MAX);
    if (!batch.length) return;
    logSending = true;
    try {
      const res = await fetch(cfg.apiUrl + '/client-log', {
        method: 'POST', cache: 'no-store', signal: AbortSignal.timeout(10000),
        headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': token },
        body: JSON.stringify({
          device: storage(DEVICE_KEY) || '',
          entries: batch.map(function (e) { return { time: e.time, level: e.level, event: e.event, message: e.message, wo: e.wo, data: e.data }; })
        })
      });
      // Mã cũ: lấy mã mới rồi gửi lại (không lặp nếu vừa lấy mã mới mà vẫn 400)
      if (res.status === 400) { if (Date.now() - tokenRefreshedAt > 30000) { await refreshToken(); scheduleLogSend(); } return; }
      // Server chưa ghi được (VD thiếu quyền thư mục log, 503): giữ lại, lần ping sau gửi lại
      if (!res.ok) return;
      const sent = {};
      batch.forEach(function (e) { sent[e.id] = true; });
      const list = logRead();
      list.forEach(function (e) { if (sent[e.id]) e.sent = true; });
      logWrite(list);
      if (list.some(function (e) { return !e.sent; })) scheduleLogSend();
    } catch (e) {
      /* mất mạng: lần sau gửi */
    } finally {
      logSending = false;
    }
  }

  // Trong console (F12): toastLog() in 100 dòng gần nhất, toastLog(300) nhiều hơn, toastLog.download() tải file, toastLog.clear() xoá
  window.toastLog = function (n) {
    const list = logRead().slice(-(n || 100));
    console.table(list.map(function (e) {
      return { time: e.time, level: e.level, event: e.event, wo: e.wo, message: e.message, sent: e.sent };
    }));
    return list;
  };
  window.toastLog.download = function () {
    const text = logRead().map(function (e) {
      return [e.time, e.level, e.event, e.wo, e.message, e.data == null ? '' : JSON.stringify(e.data), e.sent ? 'sent' : 'not-sent'].join(' | ');
    }).join('\n');
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([text], { type: 'text/plain;charset=utf-8' }));
    a.download = 'toast-log-' + localIso(new Date()).replace(/[:T]/g, '-') + '.txt';
    a.click();
    URL.revokeObjectURL(a.href);
  };
  window.toastLog.clear = function () { logWrite([]); };

  // Lỗi JS không bắt được
  window.addEventListener('error', function (e) {
    log('error', 'js', e.message, { file: e.filename, line: e.lineno, col: e.colno, stack: e.error && e.error.stack });
  });
  window.addEventListener('unhandledrejection', function (e) {
    const r = e.reason || {};
    log('error', 'promise', r.message || String(r), { stack: r.stack });
  });

  // ===== Thông báo =====
  // Màn làm việc: card "Thông báo" bên phải Box gần nhất: ô mới nhất to (nền màu theo loại) + danh sách "Trước đó".
  // Chỉ trong trang (tải lại là mất), tối đa NOTICE_MAX, trùng liên tiếp gộp ×n. Bước nhập WO: lỗi hiện dưới ô WO (woError).
  const NOTICE_MAX = 50;
  const NOTICE_ICON = { error: 'tabler-circle-x', warning: 'tabler-alert-triangle', success: 'tabler-circle-check' };
  const NOTICE_COLOR = { error: 'text-danger', warning: 'text-warning', success: 'text-success' };
  const notices = []; // { type, message, count, at: Date }, mới nhất ở đầu

  function isRunning() { return !el.run.classList.contains('d-none'); }

  function notice(type, message) {
    message = String(message == null ? '' : message);
    if (!message) return;
    if (!NOTICE_ICON[type]) type = 'warning';
    if (!isRunning()) {
      if (type !== 'success') woError(message);
      return;
    }
    const at = new Date();
    const top = notices[0];
    if (top && top.type === type && top.message === message) {
      top.count++;
      top.at = at;
    } else {
      notices.unshift({ type: type, message: message, count: 1, at: at });
      if (notices.length > NOTICE_MAX) notices.length = NOTICE_MAX;
    }
    renderNotices();
  }

  function renderNotices() {
    if (!notices.length) {
      el.noticesBody.innerHTML = '<div class="st-notice-empty"><i class="icon-base ti tabler-bell"></i>Chưa có thông báo</div>';
      return;
    }
    const n = notices[0];
    let html = '<div class="st-notice-latest is-' + n.type + '" role="status">' +
      '<i class="icon-base ti ' + NOTICE_ICON[n.type] + '"></i>' +
      '<div class="st-notice-msg">' + noticeHtml(n.message, 'st-notice-title', 'st-notice-line') +
        '<div class="st-notice-time fw-normal">' + esc(n.at.toLocaleTimeString('vi-VN')) + '</div></div>' +
      (n.count > 1 ? '<span class="st-notice-count">×' + n.count + '</span>' : '') +
      '</div>';
    if (notices.length > 1) {
      html += '<div class="st-notice-prev-title">Trước đó</div><div class="st-notice-list">' +
        notices.slice(1).map(function (p) {
          return '<div class="st-notice-item is-' + p.type + '">' +
            // danh sách "Trước đó": chỉ giờ:phút
            '<span class="st-notice-time">' + esc(p.at.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })) + '</span>' +
            '<i class="icon-base ti ' + NOTICE_ICON[p.type] + ' ' + NOTICE_COLOR[p.type] + '"></i>' +
            '<div class="st-notice-msg">' + noticeHtml(p.message, 'st-notice-title', 'st-notice-line') + '</div>' +
            (p.count > 1 ? '<span class="st-notice-count badge ' + (p.type === 'error' ? 'bg-danger' : 'bg-secondary') + '">×' + p.count + '</span>' : '') +
            '</div>';
        }).join('') + '</div>';
    }
    el.noticesBody.innerHTML = html;
  }

  // ===== Câu thông báo in tem (1 chỗ, để chuẩn hoá) =====
  // Nhiều dòng (\n): dòng 1 = việc gì (to, đậm), các dòng sau = từng ý "Nhãn: nội dung" (Lý do / Xử lý / Máy in / Mã).
  // PRINT_HINT: hướng giải quyết, hiện ở dòng "Xử lý:" (VD "Liên hệ trưởng line."), rỗng = không có dòng này. Sửa ở đây, không sửa từng câu.
  const PRINT_HINT = {
    box: 'Kiểm tra máy in rồi in lại ở Menu (F2) → Box.', // in tự động khi đủ box
    reprint: '',                                         // Menu → Box → In lại
    manual: ''                                           // Menu → In tem
  };

  function sentence(text) {
    text = String(text || '').trim();
    return !text || /[.!?]$/.test(text) ? text : text + '.';
  }

  const SENTENCE_LABELS = { 'Lý do': true, 'Xử lý': true };

  // lines(tiêu đề, ['Nhãn', nội dung] | 'dòng thường', ...): bỏ dòng có nội dung rỗng
  function lines(title) {
    const out = [title];
    for (let k = 1; k < arguments.length; k++) {
      const a = arguments[k];
      // Lý do / Xử lý là câu (thêm dấu chấm); Máy in / Mã là dữ liệu (giữ nguyên)
      if (Array.isArray(a)) { if (a[1]) out.push(a[0] + ': ' + (SENTENCE_LABELS[a[0]] ? sentence(a[1]) : String(a[1]).trim())); }
      else if (a) out.push(sentence(a));
    }
    return out.join('\n');
  }

  const printMsg = {
    ok: function (boxNo, printer) { return lines('Box ' + boxNo + ' đã in tem', ['Máy in', printer]); },
    fail: function (boxNo, reason) { return lines('Box ' + boxNo + ' chưa in được tem', ['Lý do', reason], ['Xử lý', PRINT_HINT.box]); },
    offlineOk: function (boxNo, printer) { return lines('Box ' + boxNo + ' đã in tem (đang mất mạng)', ['Máy in', printer]); },
    reprintOk: function (boxNo, printer) { return lines('Box ' + boxNo + ' đã in lại tem', ['Máy in', printer]); },
    reprintFail: function (boxNo, reason) { return lines('Box ' + boxNo + ' chưa in lại được tem', ['Lý do', reason], ['Xử lý', PRINT_HINT.reprint]); },
    manualOk: function (copies, type, code, printer) { return lines('Đã in ' + copies + ' tem ' + type, ['Mã', code], ['Máy in', printer]); },
    manualFail: function (type, code, reason) { return lines('Chưa in được tem ' + type, ['Mã', code], ['Lý do', reason], ['Xử lý', PRINT_HINT.manual]); }
  };

  // Một dòng cho chỗ không xuống dòng được (toast, dòng lỗi dưới ô WO)
  function oneLine(message) { return String(message || '').split('\n').join(' · '); }

  // Thông báo nhiều dòng -> HTML: dòng 1 tiêu đề, dòng "Nhãn: ..." in đậm nhãn
  function noticeHtml(message, titleClass, lineClass) {
    const parts = String(message).split('\n');
    return '<div class="' + titleClass + '">' + esc(parts[0]) + '</div>' + parts.slice(1).map(function (line) {
      const m = /^([^:]{1,20}): (.*)$/.exec(line);
      return '<div class="' + lineClass + '">' + (m ? '<span class="st-notice-label">' + esc(m[1]) + ':</span> ' + esc(m[2]) : esc(line)) + '</div>';
    }).join('');
  }

  // Lỗi ở bước nhập WO: dòng đỏ dưới ô WO + viền đỏ; message rỗng = xoá
  function woError(message) {
    el.woError.innerHTML = message ? '<i class="icon-base ti tabler-circle-x"></i><span>' + esc(oneLine(message)) + '</span>' : '';
    el.woError.classList.toggle('d-none', !message);
    el.woGroup.classList.toggle('is-invalid', !!message);
  }

  // Mọi notyf trên màn này (cả module dùng chung như cấu hình máy in) vào card; lỗi vẫn ghi log.
  // Đang mở modal (Menu, xác nhận phiên...) thì card bị che -> hiện thêm toast như cũ.
  if (notyf) {
    const toastError = notyf.error.bind(notyf);
    const toastSuccess = notyf.success.bind(notyf);
    const toastOpen = notyf.open.bind(notyf);
    const text = function (m) { return typeof m === 'string' ? m : (m && m.message) || ''; };
    const inModal = function () { return !!document.querySelector('.modal.show'); };
    // notyf.error / success bên trong gọi this.open: lúc đó open phải đi thẳng, không ghi card / log lần 2 (trước đây 1 lỗi tính ×2)
    let inner = false;
    // toast không xuống dòng: các ý của thông báo nhiều dòng nối bằng " · "
    const toast = function (fn, arg) {
      inner = true;
      try {
        return fn(typeof arg === 'string' ? oneLine(arg) : arg && arg.message ? Object.assign({}, arg, { message: oneLine(arg.message) }) : arg);
      } finally { inner = false; }
    };
    notyf.error = function (m) {
      log('error', 'notify', text(m));
      notice('error', text(m));
      return inModal() ? toast(toastError, m) : null;
    };
    notyf.success = function (m) {
      notice('success', text(m));
      return inModal() ? toast(toastSuccess, m) : null;
    };
    notyf.open = function (o) {
      if (inner) return toastOpen(o);
      const type = o && o.type === 'error' ? 'error' : o && o.type === 'success' ? 'success' : 'warning';
      if (type === 'error') log('error', 'notify', text(o));
      notice(type, text(o));
      return inModal() ? toast(toastOpen, o) : null;
    };
  }

  function storage(key, value) {
    try {
      if (value === undefined) return localStorage.getItem(key);
      if (value === null) localStorage.removeItem(key); else localStorage.setItem(key, value);
    } catch (e) { /* trình duyệt chặn: chỉ nhớ trong phiên này */ }
    return null;
  }

  // Lỗi vào card Thông báo. logLevel / logData: ghi log kèm dữ liệu (VD quét sai mã là 'warn' + mã đã quét)
  function alertError(message, logData, logLevel) {
    log(logLevel || 'error', 'alert', message, logData);
    notice('error', message);
  }

  // ===== Máy in (modal Cấu hình máy in dùng chung) =====

  // Máy trước đây chọn ở màn này (toast.station.printerId) chuyển sang cấu hình chung nếu máy chưa có cấu hình
  try {
    const old = localStorage.getItem(OLD_PRINTER_KEY);
    if (old && !localStorage.getItem('toast.print.config'))
      localStorage.setItem('toast.print.config', JSON.stringify({ mode: 'server', source: 'list', printerId: old }));
    localStorage.removeItem(OLD_PRINTER_KEY);
  } catch (e) { /* trình duyệt chặn */ }

  const printerConfig = window.ToastPrinterConfig.create({
    printers: cfg.printers,
    agentUrl: cfg.agentUrl,
    noDefault: true,
    testConnection: function (body) { return api('/test-connection', Object.assign({ source: body.ip ? 'manual' : 'list' }, body)); },
    notifyError: function (m) { if (notyf) notyf.error(m); else alertError(m); },
    onSave: function () {
      showPrinterName();
      updateScanState();
      // Mất mạng mà vừa đổi sang máy in qua agent: in luôn các box đủ đang chờ (máy mới lỗi thì báo lại)
      offlineFail.message = '';
      if (net.online === false) printOfflineBoxes().then(renderCounts);
      if (notyf) notyf.success('Đang dùng máy in ' + (printerConfig.current() || {}).name);
    }
  });

  // Cấu hình gửi kèm mỗi lần in (null = chưa chọn máy in)
  function printTarget() { return printerConfig.current() ? printerConfig.config() : null; }

  // Thẻ máy in đang dùng: tên + mã · IP:port + phương thức
  function printerCard() {
    const p = printerConfig.current();
    return p ? '<div class="fw-semibold text-heading text-truncate">' + esc(p.name) + '</div>' +
      '<div class="small text-muted">' + esc(p.detail) + ' <span class="badge bg-label-secondary ms-1">' + esc(printerConfig.modeLabel()) + '</span></div>'
      : '<span class="text-danger fw-medium">Chưa chọn máy in</span>';
  }

  function showPrinterName() {
    el.printerName.innerHTML = printerCard();
  }

  // Server trả việc in cho agent (TCP / USB qua Node): gửi tới agent trên máy này rồi báo kết quả cho box (nếu có)
  async function deliver(printed, boxId) {
    if (!printed || !printed.agent) return printed;
    let ok = true, error = null;
    try {
      await printerConfig.send(printed.agent);
    } catch (err) {
      ok = false;
      error = err.message;
      log('error', 'agent.print', error, { boxId: boxId, path: printed.agent.path, printer: printed.printerName });
    }
    if (boxId) {
      try {
        const res = await api('/box/' + boxId + '/print-result', { ok: ok, error: error });
        if (res.state) applyState(res.state);
      } catch (e) { /* box giữ lỗi "chưa nhận được kết quả", in lại ở Menu → Box */ }
    }
    return Object.assign({}, printed, { ok: ok, error: error, agent: null });
  }

  // ===== WO =====

  // Nhập WO -> kiểm tra ở server -> hiện modal mở phiên
  async function openWo(code) {
    code = (code || '').trim();
    if (!code) { el.wo.focus(); return; }
    setBusy(el.woOpen, true, 'Đang kiểm tra...');
    try {
      const res = await api('/session/check?wo=' + encodeURIComponent(code));
      if (!res.success) throw new Error(res.error);
      showSessionModal(code, res);
    } catch (err) {
      alertError(err.message);
      storage(WO_KEY, null);
      el.wo.select();
    } finally {
      setBusy(el.woOpen, false, '<i class="icon-base ti tabler-player-play me-1"></i>Mở WO');
    }
  }

  // ===== Modal mở phiên =====

  const CHECK_ICONS = {
    ok: 'tabler-circle-check text-success', warn: 'tabler-alert-triangle text-warning',
    error: 'tabler-circle-x text-danger', info: 'tabler-info-circle text-info'
  };

  function showSessionModal(code, res) {
    s.check = res;
    s.checkWo = code;
    el.sessionWo.textContent = res.wo.name;
    const ss = res.session;
    el.sessionResume.classList.toggle('d-none', !ss);
    el.sessionResume.innerHTML = ss
      ? '<i class="icon-base ti tabler-history me-1"></i>Tiếp tục phiên #' + ss.id + ' (mở ' + esc(fmtTime(ss.startedAt)) + ')' +
        (ss.employeeCode ? ' · xác nhận gần nhất ' + esc(ss.employeeCode) : '') +
        (ss.openBox ? ' · box ' + ss.openBox.boxNo + ' đang dở ' + ss.openBox.qty + ' / ' + ss.openBox.capacity : '')
      : '';
    // Máy in không chọn ở đây: dùng máy in đang cài cho trạm (Menu → Máy in), chưa có thì báo lỗi
    const p = printerConfig.current();
    // Phiên đang ở máy khác: vào làm ở máy này thì máy kia dừng, pcs máy kia chưa gửi bị bỏ
    const deviceItems = ss && ss.deviceId && ss.deviceId !== deviceId
      ? [{ status: 'warn', text: 'Phiên này đang làm ở máy khác. Vào làm ở máy này thì máy kia dừng, pcs máy kia chưa gửi lên server sẽ bị bỏ: kiểm tra lại số pcs của box đang dở.' }]
      : [];
    const mine = outbox(res.wo.group || res.wo.name);
    if (mine)
      deviceItems.push({ status: 'info', text: 'Máy này còn ' + outboxCount(mine) + ' pcs chưa gửi của WO này: vào làm việc sẽ gửi tiếp.' });
    const items = deviceItems.concat(res.items || []).concat([p
      ? { status: 'ok', text: 'Máy in: ' + p.name + ' · ' + p.detail + ' (' + printerConfig.modeLabel() + ')' }
      : { status: 'error', text: 'Chưa chọn máy in. Đóng cửa sổ này, vào Menu (F2) → Máy in để chọn.' }]);
    el.sessionItems.innerHTML = items.map(function (i) {
      return '<li class="d-flex gap-2 align-items-start' + (i.status === 'error' ? ' text-danger fw-medium' : '') + '">' +
        '<i class="icon-base ti ' + (CHECK_ICONS[i.status] || CHECK_ICONS.info) + ' icon-20px flex-shrink-0 mt-n1" aria-hidden="true"></i>' +
        '<span>' + esc(i.text) + '</span></li>';
    }).join('');
    const values = res.values || [];
    el.sessionValues.classList.toggle('d-none', !values.length);
    el.sessionValues.innerHTML = values.map(function (v) {
      return '<dt title="{' + esc(v.key) + '}">' + esc(v.label) + '</dt><dd class="fw-semibold">' + esc(v.value) + '</dd>';
    }).join('');
    // Mặc định đã tick xác nhận (bỏ tick được); có lỗi thì không tick và khoá
    el.sessionConfirm.checked = !!res.canStart;
    el.sessionConfirm.disabled = !res.canStart;
    el.sessionEmployee.value = '';
    el.sessionEmployee.disabled = !res.canStart;
    showEmployeeError('');
    updateSessionButton();
    sessionModal.show();
  }

  // Chỉ bật nút khi không có lỗi, đã chọn máy in, đã tick và đã nhập mã nhân viên (sai dạng thì báo lúc bấm Vào làm việc)
  function updateSessionButton() {
    const code = el.sessionEmployee.value;
    el.sessionStart.disabled = !(s.check && s.check.canStart && printTarget() && el.sessionConfirm.checked && code.length > 0);
  }

  async function startSession(e) {
    e.preventDefault();
    if (el.sessionStart.disabled) return;
    const digits = el.sessionEmployee.value;
    if (!EMPLOYEE_DIGITS_RE.test(digits)) { showEmployeeError('Mã nhân viên gồm 4 chữ số, VD 0215.'); return; }
    const employeeCode = 'SVN' + digits;
    // WO đang làm ở tab khác trên máy này: không vào (2 tab cùng đếm 1 WO sẽ lệch số)
    const group = (s.check && s.check.wo && (s.check.wo.group || s.check.wo.name)) || s.checkWo;
    if (!(await lockGroup(group))) {
      if (notyf) notyf.error(lines('WO ' + group + ' đang làm ở tab khác trên máy này', ['Xử lý', 'Sử dụng tab đó, hoặc đóng tab đó rồi thử lại']));
      return;
    }
    setBusy(el.sessionStart, true, 'Đang mở...');
    try {
      const res = await api('/session/start', { wo: s.checkWo, employeeCode: employeeCode, confirmed: true, printer: printTarget(), deviceId: deviceId });
      if (!res.success && res.field === 'employee') { showEmployeeError(res.error); return; }
      if (!res.success) throw new Error(res.error);
      sessionModal.hide();
      enterRun(res);
    } catch (err) {
      if (notyf) notyf.error(err.message); else alertError(err.message);
    } finally {
      setBusy(el.sessionStart, false, '<i class="icon-base ti tabler-player-play me-1"></i>Vào làm việc');
      updateSessionButton();
    }
  }

  // ===== Mã nhân viên =====

  // message: lỗi hiện dưới ô + viền đỏ; trống = bỏ lỗi
  function showEmployeeError(message) {
    el.sessionEmployeeError.textContent = message || '';
    el.sessionEmployeeError.classList.toggle('d-block', !!message);
    el.sessionEmployee.parentElement.classList.toggle('is-invalid', !!message);
  }

  // Đã mở phiên: vào màn quét
  function enterRun(res) {
    s.wo = res.wo;
    storage(WO_KEY, res.wo.name);
    el.warnings.innerHTML = (res.warnings || []).map(function (w) {
      return '<div class="alert alert-warning py-2 small mb-2"><i class="icon-base ti tabler-alert-triangle me-1"></i>' + esc(w) + '</div>';
    }).join('');
    applyState(res.state);
    el.woStep.classList.add('d-none');
    el.run.classList.remove('d-none');
    woError('');
    focusScan();
    // Còn pcs chưa gửi của WO này (refresh / phiên vừa kết thúc) mà đã đủ box: gửi luôn để in tem
    if (flushDue()) flush();
  }

  function fmtTime(v) {
    return v ? new Date(v).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }) : '';
  }

  // Phiên đã kết thúc: về ô nhập WO
  function leaveRun() {
    releaseLock();
    s.wo = null;
    s.state = null;
    storage(WO_KEY, null);
    el.run.classList.add('d-none');
    el.woStep.classList.remove('d-none');
    el.last.innerHTML = '';
    el.wo.value = '';
    el.wo.focus();
  }

  // ===== Hiển thị trạng thái =====

  // Mã quét pcs: mã đã xác nhận trong phiên; chưa có phiên thì barcode cố định (quét theo Lot: Lot hiện ở modal mở phiên)
  function scanCodeHtml(p, session) {
    if (session && session.scanCode) return '<code>' + esc(session.scanCode) + '</code>';
    return p.scanSource === 'LotId' ? '<span class="text-muted">Lot ID của WO (xác nhận khi mở phiên)</span>' : '<code>' + esc(p.barcode) + '</code>';
  }

  function applyState(state) {
    s.state = state;
    const p = state.product || {};
    const t = state.template;
    el.woName.innerHTML = esc(state.woName) + (s.wo && s.wo.isTest
      ? ' <span class="badge bg-warning text-dark align-middle fs-6" title="Không gọi Viindoo, không ghi log dashboard">WO THỬ</span>' : '') +
      (state.session ? ' <span class="badge bg-label-success align-middle fs-6">Phiên #' + state.session.id +
        (state.session.employeeCode ? ' · ' + esc(state.session.employeeCode) : '') + '</span>' : '');
    el.info.innerHTML =
      row('Sản phẩm', '<span class="fw-semibold">' + esc(p.defaultCode) + '</span>' + (p.name ? ' · ' + esc(p.name) : '')) +
      row(p.scanSource === 'LotId' ? 'Mã quét (Lot ID)' : 'Barcode pcs', scanCodeHtml(p, state.session)) +
      row('Pcs / box', '<span class="fw-semibold">' + (p.pcsPerBox || 0) + '</span>') +
      row('Mẫu tem Box', t ? esc(t.defaultCode) + ' · ' + esc(t.size) + ' · ' + t.dpi + ' dpi' : '<span class="text-danger fw-semibold">Chưa có</span>') +
      row('SL kế hoạch', esc(s.wo ? s.wo.productQty : '')) +
      (s.wo && s.wo.origin ? row('Chứng từ gốc', esc(s.wo.origin)) : '');

    el.planned.textContent = s.wo ? Number(s.wo.productQty).toLocaleString('vi-VN') : '—';
    renderCounts();
    ensureOfflineLabel();
    renderBoxes();
    updateScanState();

    function row(label, value) { return '<dt>' + label + '</dt><dd>' + value + '</dd>'; }
  }

  // Số đếm trên màn = số server đã nhận + pcs trong hàng chờ của máy này. Hàng chờ đủ box (mất mạng) thì box đó "chờ gửi / in".
  function renderCounts() {
    const st = s.state;
    if (!st) return;
    if (offlineNow()) keepBoxes();
    const m = runModel();
    const pending = netCount(m.ob);
    const full = m.boxes.length;
    // Box đang quét chưa có pcs (mới mở / vừa xoá): như bình thường là "Box tiếp theo", quét pcs đầu tiên mới thành box mới
    el.boxLabel.textContent = m.openQty > 0 ? 'Box ' + m.openNo + ' đang quét' : 'Box tiếp theo';
    el.boxQty.textContent = m.openQty;
    el.boxCap.textContent = m.cap;
    el.boxBar.style.width = (m.cap ? Math.min(100, m.openQty * 100 / m.cap) : 0) + '%';
    el.closed.textContent = st.closedBoxes + full;
    el.total.textContent = (st.totalPcs + pending).toLocaleString('vi-VN');
    renderSync(m);
    renderBoxes(); // box vừa đủ hàng ở máy này (chưa đồng bộ) hiện ngay trong Box gần nhất
  }

  // Dòng trạng thái gửi: đang có pcs chưa gửi / gửi lỗi (mất mạng) / box đủ đang chờ gửi để in
  function renderSync(m) {
    const pending = netCount(m.ob);
    const waiting = m.boxes.filter(function (b) { return !b.printed; });
    const printedBoxes = m.boxes.filter(function (b) { return b.printed; });
    let tone = '', text = '', detail = '';
    if (sync.error && m.ob) {
      const keep = Math.max(0, pending) + ' pcs lưu trên máy, có kết nối sẽ tự gửi.';
      const blocker = offlineBlocker();
      detail = sync.error + (blocker ? ' · ' + blocker.text : '') + (offlineFail.message ? ' · ' + offlineFail.message : '');
      if (!sync.network) {
        tone = 'warning';
        text = 'Chưa gửi được ' + Math.max(0, pending) + ' pcs, số vẫn được giữ trên máy. Báo leader kiểm tra.';
      } else if (waiting.length && (blocker || offlineFail.message)) {
        // Box in lần lượt: box chưa in đầu tiên lỗi thì các box sau cũng đang chờ
        const range = waiting.length > 1
          ? 'Box ' + waiting[0].boxNo + '–' + waiting[waiting.length - 1].boxNo + ' (' + waiting.length + ' box)'
          : 'Box ' + waiting[0].boxNo;
        let reason, after;
        if (blocker && blocker.kind === 'printer') {
          tone = 'warning';
          text = 'Mất kết nối server. ' + range + ' đủ hàng, chưa in được tem (chưa chọn máy in). Cứ quét tiếp, không mất số.';
          reason = 'Mất kết nối server và chưa chọn máy in';
          after = ['Xử lý', 'Chọn máy in ở Menu (F2) → Máy in'];
        } else if (!blocker) {
          // In qua agent lỗi (agent chưa chạy / máy in không phản hồi): box chờ, có kết nối lại thì server in bù
          tone = 'warning';
          text = 'Mất kết nối server. ' + range + ' đủ hàng, chưa in được tem, sẽ in khi có kết nối lại. Cứ quét tiếp, không mất số.';
          reason = 'Mất kết nối server, ' + (offlineFail.agentDown ? 'agent in trên máy này chưa chạy' : 'máy in không phản hồi');
          after = 'Sẽ in khi có kết nối lại';
        } else {
          tone = 'info';
          text = 'Mất kết nối server. ' + range + ' sẽ in tem khi có kết nối lại. Cứ quét tiếp, không mất số.';
        }
        // Báo vào card mỗi box chưa in 1 lần (dòng trạng thái vẽ lại sau mỗi lần quét)
        if (reason) {
          waiting.forEach(function (b) {
            if (b.boxNo <= (net.waitNoticedBox || 0)) return;
            notice('warning', lines('Box ' + b.boxNo + ' đủ hàng, chưa in được tem', ['Lý do', reason], after));
            net.waitNoticedBox = b.boxNo;
          });
        }
      } else if (waiting.length) {
        tone = 'info';
        text = 'Mất kết nối server. Box ' + waiting[0].boxNo + ' sẽ in tem khi có kết nối lại. Cứ quét tiếp, không mất số.';
      } else if (printedBoxes.length) {
        tone = 'info';
        text = 'Mất kết nối server. Box ' + printedBoxes[printedBoxes.length - 1].boxNo + ' đã in tem. ' + keep + ' Cứ quét tiếp.';
      } else {
        tone = 'info';
        text = 'Mất kết nối server. ' + keep + ' Cứ quét tiếp bình thường.';
      }
    } else if (m.boxes.length) {
      tone = 'info';
      text = 'Đang in tem box ' + m.boxes[0].boxNo + '…';
    }
    // Mất kết nối: luôn nhắc không đóng tab / tải lại (server sập thì trang không mở lại được; số pcs chỉ nằm trên máy này)
    if (sync.error && m.ob && sync.network) text += ' Không đóng tab, không tải lại trang.';
    // Chữ cho công nhân: không từ kỹ thuật; chi tiết lỗi (server, agent...) ở tooltip cho leader
    el.sync.innerHTML = text
      ? '<span class="text-' + tone + ' fw-semibold"><i class="icon-base ti ' + (tone === 'warning' ? 'tabler-alert-triangle' : 'tabler-info-circle') + ' me-1"></i>' + esc(text) + '</span>'
      : '';
    el.sync.title = detail;
    el.sync.classList.toggle('d-none', !text);
  }

  // Box server (mới nhất trước) bỏ các dòng mà máy này đang giữ: box cùng số với box chưa đồng bộ, và box đang mở của server khi máy này
  // có dữ liệu chưa đồng bộ (box đang quét lấy theo máy này: còn pcs thì dòng "Đang quét" ở máy, đã xoá / 0 pcs thì không hiện)
  function serverBoxesShown(m, localNos) {
    return ((s.state && s.state.boxes) || []).filter(function (b) {
      return localNos.indexOf(b.boxNo) < 0 && !(m && m.ob && b.status === 'Open');
    });
  }

  // Box đang quét ở máy này (dữ liệu chưa đồng bộ, còn pcs): dòng "Đang quét" thay cho box đang mở của server
  function localOpenBox(m) {
    return m && m.ob && m.openQty > 0
      ? { id: -m.openNo, local: true, open: true, boxNo: m.openNo, qty: m.openQty, capacity: m.cap, printed: null, closedAt: null }
      : null;
  }

  // Box đủ hàng ở máy này nhưng chưa đồng bộ lên server (mất kết nối / đang gửi), mới nhất trước.
  // Hiện đầu bảng Box gần nhất, cột # (mã trên server) trống = chưa đồng bộ; printed = đã in qua agent lúc mất kết nối
  function localBoxes() {
    const m = runModel();
    if (!m) return [];
    return m.boxes.slice().reverse().map(function (b) {
      return { local: true, boxNo: b.boxNo, qty: b.qty, capacity: m.cap, printed: b.printed || null, closedAt: b.closedAt || null };
    });
  }

  // ----- Box chưa đồng bộ (mất kết nối) -----
  // Hàng chờ: { group, productId, barcode, fromSeq, toSeq, adj, boxes }. Pcs thật = toSeq - fromSeq + 1 + adj (adj: pcs thêm / bớt khi sửa,
  // xoá box ở trạm). boxes = box trạm tự giữ khi mất kết nối (đủ hàng / đóng thiếu), cũ trước: { boxNo, qty, endSeq, closedAt,
  // printed: { printer, version } | null }. Có boxes / adj thì gửi server dạng danh sách box (ScanRequest.Boxes + OpenQty).
  function netCount(ob) { return ob ? outboxCount(ob) + (ob.adj || 0) : 0; }
  function keptBoxes(ob) { return (ob && ob.boxes) || []; }
  function offlineNow() { return net.online === false || (!!sync.error && sync.network); }

  // Ở máy này: box đủ / đã đóng chưa đồng bộ (cũ trước; box trạm giữ + box vừa đủ theo quy cách) và box đang quét (openNo, openQty)
  function runModel() {
    const st = s.state;
    if (!st) return null;
    const open = st.openBox;
    const cap = open ? open.capacity : ((st.product && st.product.pcsPerBox) || 0);
    const ob = outbox();
    const kept = keptBoxes(ob);
    const lastNo = (st.boxes && st.boxes.length) ? st.boxes[0].boxNo : 0;
    let no = kept.length ? kept[kept.length - 1].boxNo + 1 : (open ? open.boxNo : lastNo + 1);
    let rest = (open ? open.qty : 0) + netCount(ob) - kept.reduce(function (a, b) { return a + b.qty; }, 0);
    const derived = [];
    while (cap > 0 && rest >= cap) {
      derived.push({ boxNo: no++, qty: cap, endSeq: ob ? ob.toSeq : 0, closedAt: null, printed: null, derived: true });
      rest -= cap;
    }
    return { cap: cap, ob: ob, boxes: kept.concat(derived), openQty: Math.max(0, rest), openNo: no };
  }

  // Mất kết nối: box vừa đủ hàng thành box trạm giữ (sửa / xoá / in được, server đóng đúng box đó khi có kết nối)
  function keepBoxes() {
    const m = runModel();
    if (!m || !m.ob) return;
    const derived = m.boxes.filter(function (b) { return b.derived; });
    if (!derived.length) return;
    m.ob.boxes = keptBoxes(m.ob).concat(derived.map(function (b) {
      return { boxNo: b.boxNo, qty: b.qty, endSeq: b.endSeq, closedAt: null, printed: null };
    }));
    saveOutbox(m.ob);
  }

  // Hàng chờ để ghi thao tác sửa ở máy này. newSeq: lấy thêm 1 số thứ tự (không thêm pcs) để thao tác có EndSeq mới (đóng box thiếu)
  function editableOutbox(newSeq) {
    const group = runGroup();
    let ob = outbox(group);
    if (!ob || newSeq) {
      const seq = nextSeq(group);
      if (!ob) ob = { group: group, productId: s.state.product.productId, barcode: expectedCode(), fromSeq: seq, toSeq: seq, adj: -1, boxes: [] };
      else { ob.toSeq = seq; ob.adj = (ob.adj || 0) - 1; }
    }
    if (!ob.boxes) ob.boxes = [];
    return ob;
  }

  function afterLocalEdit(ob) {
    saveOutbox(ob);
    renderCounts();
    renderMenuBoxes();
    if (flushDue()) flush();
  }

  // Sửa số pcs: box đang quét (open) hoặc box trạm giữ. Box đã in thì in lại tem (số mới)
  // remove: xoá box đang quét (= 0 pcs, màn hiện "Box tiếp theo")
  function qtyLocal(b, qty, remove) {
    keepBoxes();
    const ob = editableOutbox(false);
    if (b.open) {
      const before = runModel().openQty;
      ob.adj = (ob.adj || 0) + qty - before;
      afterLocalEdit(ob);
      if (notyf) notyf.success(remove
        ? lines('Đã xoá box ' + b.boxNo + ' (' + before + ' pcs)', 'Chưa đồng bộ, có kết nối sẽ gửi')
        : lines('Box ' + b.boxNo + ': ' + qty + ' pcs', 'Chưa đồng bộ, có kết nối sẽ gửi'));
      return;
    }
    const kb = ob.boxes.find(function (x) { return x.boxNo === b.boxNo; });
    if (!kb) return;
    ob.adj = (ob.adj || 0) + qty - kb.qty;
    kb.qty = qty;
    const reprint = !!kb.printed;
    if (reprint) { kb.printed = null; kb.closedAt = null; }
    afterLocalEdit(ob);
    if (notyf) notyf.success(lines('Box ' + b.boxNo + ': ' + qty + ' pcs', 'Chưa đồng bộ, có kết nối sẽ gửi', reprint ? 'Tem sẽ in lại theo số mới' : ''));
  }

  // Xoá box trạm giữ: bỏ pcs của box, các box sau lùi 1 số (box đã in thì in lại tem số mới)
  function deleteLocal(b) {
    keepBoxes();
    const ob = editableOutbox(false);
    const idx = ob.boxes.findIndex(function (x) { return x.boxNo === b.boxNo; });
    if (idx < 0) return;
    const removed = ob.boxes.splice(idx, 1)[0];
    ob.adj = (ob.adj || 0) - removed.qty;
    const renumbered = [];
    ob.boxes.slice(idx).forEach(function (x) {
      x.boxNo -= 1;
      if (x.printed) { x.printed = null; x.closedAt = null; renumbered.push(x.boxNo); }
    });
    afterLocalEdit(ob);
    if (notyf) notyf.success(lines('Đã xoá box ' + b.boxNo + ' (' + removed.qty + ' pcs)', 'Chưa đồng bộ, có kết nối sẽ gửi',
      removed.printed ? 'Huỷ tem đã in của box này' : '',
      renumbered.length ? 'Box ' + renumbered.join(', ') + ' đổi số, tem sẽ in lại' : ''));
  }

  // Đóng box đang quét dù chưa đủ (mất kết nối): thành box trạm giữ, in tem như box đủ
  function closeLocal() {
    keepBoxes();
    const m = runModel();
    if (!m || m.openQty < 1) return;
    const ob = editableOutbox(true);
    ob.boxes.push({ boxNo: m.openNo, qty: m.openQty, endSeq: ob.toSeq, closedAt: null, printed: null });
    afterLocalEdit(ob);
    if (notyf) notyf.success(lines('Đã đóng box ' + m.openNo + ' (' + m.openQty + ' pcs)', 'Chưa đồng bộ, có kết nối sẽ gửi'));
  }

  // Box gần nhất (server gửi tối đa 20, mới nhất trước) + box chưa đồng bộ ở đầu: mặc định 5 box, Xem thêm / Thu gọn.
  // Box in lỗi tô đỏ + đếm ở tiêu đề; box chưa đồng bộ đếm riêng
  const BOXES_COLLAPSED = 5;
  function renderBoxes() {
    const m = runModel();
    const local = localBoxes();
    const openLocal = localOpenBox(m);
    if (openLocal) local.unshift(openLocal);
    const server = serverBoxesShown(m, local.map(function (b) { return b.boxNo; }));
    const list = local.concat(server);
    const errors = server.filter(function (b) { return b.status !== 'Open' && !b.printedAt; }).length;
    const unsyncedCount = local.filter(function (b) { return !b.open; }).length;
    el.boxesBadges.innerHTML = (unsyncedCount ? '<span class="badge bg-label-warning me-1">' + unsyncedCount + ' box chưa đồng bộ</span>' : '') +
      (errors ? '<span class="badge bg-label-danger">' + errors + ' box lỗi in</span>' : '');
    const hidden = Math.max(0, list.length - BOXES_COLLAPSED);
    el.boxesMore.classList.toggle('d-none', !hidden);
    if (hidden) {
      el.boxesCount.textContent = s.boxesExpanded ? 'Đang hiện ' + list.length + ' box gần nhất' : 'Đang hiện ' + BOXES_COLLAPSED + ' / ' + list.length + ' box gần nhất';
      el.boxesToggle.textContent = s.boxesExpanded ? 'Thu gọn' : 'Xem thêm ' + hidden + ' box';
    }
    if (!list.length) {
      el.boxes.innerHTML = '<tr><td colspan="6" class="text-center text-muted py-4">Chưa có box nào</td></tr>';
      return;
    }
    el.boxes.innerHTML = (s.boxesExpanded ? list : list.slice(0, BOXES_COLLAPSED)).map(function (b) {
      if (b.local && b.open) {
        return '<tr class="st-box-local">' +
          '<td class="small text-muted"></td>' +
          '<td class="fw-semibold text-heading">' + b.boxNo + '</td>' +
          '<td>' + b.qty + ' / ' + b.capacity + '</td>' +
          '<td><span class="badge bg-label-primary">Đang quét</span></td>' +
          '<td><span class="text-muted">—</span></td><td class="small">—</td></tr>';
      }
      if (b.local) {
        return '<tr class="st-box-local">' +
          '<td class="small text-muted"></td>' +
          '<td class="fw-semibold text-heading">' + b.boxNo + '</td>' +
          '<td>' + b.qty + ' / ' + b.capacity + '</td>' +
          '<td><span class="badge bg-label-warning" title="Đủ hàng ở máy này, có kết nối sẽ gửi lên server">Chưa đồng bộ</span></td>' +
          '<td>' + (b.printed
            ? '<span class="badge bg-label-info" title="' + esc(b.printed.printer) + '">Đã in (mất kết nối)</span>'
            : '<span class="badge bg-label-secondary" title="Sẽ in khi có kết nối lại">Chưa in</span>') + '</td>' +
          '<td class="small">' + (b.closedAt ? new Date(b.closedAt).toLocaleString('vi-VN') : '—') + '</td></tr>';
      }
      const open = b.status === 'Open';
      return '<tr' + (!open && !b.printedAt ? ' class="st-box-error"' : '') + '>' +
        '<td class="small text-muted">' + b.id + '</td>' +
        '<td class="fw-semibold text-heading">' + b.boxNo + '</td>' +
        '<td>' + b.qty + ' / ' + b.capacity + '</td>' +
        '<td>' + (open ? '<span class="badge bg-label-primary">Đang quét</span>' : '<span class="badge bg-label-secondary">Đã đóng</span>') + '</td>' +
        '<td>' + printBadge(b) + '</td>' +
        '<td class="small">' + (b.closedAt ? new Date(b.closedAt).toLocaleString('vi-VN') : '—') + '</td></tr>';
    }).join('');
  }

  function printBadge(b) {
    if (b.status === 'Open') return '<span class="text-muted">—</span>';
    // In lúc mất mạng (qua agent); ⚠ = số box / mẫu tem lệch, leader kiểm tra
    if (b.printedAt && b.printTarget && b.printTarget.indexOf('(offline)') >= 0)
      return b.printTarget.indexOf('⚠') >= 0
        ? '<span class="badge bg-label-warning" title="' + esc(b.printTarget) + '">Cần kiểm tra</span>'
        : '<span class="badge bg-label-info" title="' + esc(b.printTarget) + '">In khi mất mạng</span>';
    return b.printedAt ? '<span class="badge bg-label-success">Đã in</span>'
      : '<span class="badge bg-label-danger" title="' + esc(b.printError) + '">Lỗi in</span>';
  }

  // Chỉ cho quét khi đã chọn máy in và có đủ quy cách + mẫu tem
  function updateScanState() {
    let reason = '';
    if (!printTarget()) reason = 'Chưa chọn máy in: vào Menu (F2) → Máy in.';
    else if (s.state && s.state.blocker) reason = s.state.blocker;
    el.scan.disabled = !!reason || !s.state;
    el.scanBlock.classList.toggle('d-none', !reason);
    el.scanBlock.textContent = reason;
  }

  function focusScan() {
    if (!el.scan.disabled && !document.querySelector('.modal.show')) el.scan.focus();
  }

  // ===== Quét =====

  // ----- Hàng chờ pcs của máy này (localStorage) -----

  // Mã máy: sinh 1 lần, giữ qua refresh / khởi động lại; đổi trình duyệt / xoá dữ liệu = máy mới
  const deviceId = (function () {
    let id = storage(DEVICE_KEY);
    if (!id) {
      id = (window.crypto && crypto.randomUUID) ? crypto.randomUUID() : 'dev-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2);
      storage(DEVICE_KEY, id);
    }
    return id;
  })();

  function runGroup() { return (s.wo && s.wo.group) || (s.state && s.state.woName) || ''; }

  // Hàng chờ của nhóm lệnh: { group, productId, barcode, fromSeq, toSeq } = các lần quét fromSeq..toSeq chưa gửi.
  // Không truyền group = WO đang làm
  function outbox(group) {
    const g = group || runGroup();
    if (!g) return null;
    try { return JSON.parse(storage(OUTBOX_KEY + g) || 'null'); } catch (e) { return null; }
  }
  function saveOutbox(ob, group) {
    const g = group || (ob && ob.group) || runGroup();
    if (g) storage(OUTBOX_KEY + g, ob ? JSON.stringify(ob) : null);
  }
  function outboxCount(ob) { return ob ? ob.toSeq - ob.fromSeq + 1 : 0; }

  // Pcs chưa gửi của WO đang làm (đã tính phần sửa / xoá ở trạm)
  function pendingForRun() {
    return s.state ? netCount(outbox()) : 0;
  }
  // Còn gì chưa đồng bộ (pcs, box trạm giữ, thao tác sửa)
  function unsynced() { return !!(s.state && outbox()); }

  // Số thứ tự quét của máy theo nhóm lệnh: chỉ tăng, không quay lại (server bỏ các số đã nhận)
  function nextSeq(group) {
    const n = (parseInt(storage(SEQ_KEY + group), 10) || 0) + 1;
    storage(SEQ_KEY + group, String(n));
    return n;
  }

  // Khoá WO theo tab (navigator.locks): 1 WO chỉ làm ở 1 tab trên máy này, tab khác làm WO khác vẫn được.
  // Giữ khoá đến khi rời màn quét (leaveRun) hoặc đóng tab
  const runLock = { group: null, release: null };
  function lockGroup(group) {
    if (runLock.group === group) return Promise.resolve(true);
    releaseLock();
    if (!navigator.locks || !navigator.locks.request) return Promise.resolve(true);
    return new Promise(function (resolve) {
      navigator.locks.request('toast-station:' + group, { ifAvailable: true }, function (lock) {
        if (!lock) { resolve(false); return null; }
        runLock.group = group;
        resolve(true);
        return new Promise(function (done) { runLock.release = done; });
      });
    });
  }
  function releaseLock() {
    if (runLock.release) runLock.release();
    runLock.group = null;
    runLock.release = null;
  }

  // Mã đúng = mã quét đã xác nhận trong phiên (barcode cố định / Lot ID); xác nhận cũ chưa lưu mã thì barcode của sản phẩm
  function expectedCode() {
    const st = s.state;
    if (!st || !st.session) return null;
    if (st.session.scanCode) return st.session.scanCode;
    return st.product && st.product.scanSource !== 'LotId' ? st.product.barcode : null;
  }

  // ----- Quét -----

  function scan(e) {
    e.preventDefault();
    const code = el.scan.value.trim();
    el.scan.value = '';
    if (!code || !s.state) return;
    const expected = expectedCode();
    if (!expected) {
      alertError('Phiên chưa có mã quét đã xác nhận. Nhập lại WO và xác nhận thông tin.');
      return;
    }
    // Sai mã: báo đỏ, không tính, quét tiếp được (không gọi server)
    if (code.toLowerCase() !== String(expected).trim().toLowerCase()) {
      alertError('Mã sai.', { scanned: code, expected: expected }, 'warn');
      focusScan();
      return;
    }
    const group = runGroup();
    const seq = nextSeq(group);
    const ob = outbox(group);
    if (ob) { ob.toSeq = seq; saveOutbox(ob); }
    else saveOutbox({ group: group, productId: s.state.product.productId, barcode: code, fromSeq: seq, toSeq: seq });
    renderCounts();
    if (flushDue()) flush();
    focusScan();
  }

  // Cần gửi: hàng chờ đã đủ ít nhất 1 box, hoặc đủ N pcs (Toast:ScanSyncEvery, box lớn VD 70 pcs ghi DB mỗi 5 pcs)
  function flushDue() {
    const m = runModel();
    if (!m || !m.ob) return false;
    // Có box trạm giữ / thao tác sửa ở trạm: gửi (mất kết nối thì chỉ in qua agent) để đồng bộ khi có kết nối
    if (keptBoxes(m.ob).length || m.ob.adj) return true;
    const pending = netCount(m.ob);
    if (pending <= 0) return false;
    if (cfg.syncEvery > 0 && pending >= cfg.syncEvery) return true;
    return m.boxes.length > 0;
  }

  // ----- Gửi hàng chờ -----

  // error: lỗi lần gửi gần nhất; network: lỗi do mất mạng / server không ghi được (khác lỗi dữ liệu)
  const sync = { running: null, again: false, error: '', network: false, timer: null };

  // Gửi hàng chờ của WO đang làm lên server (1 lô). Trả true = không còn gì chưa gửi. Đang gửi thì chờ lượt đó rồi gửi tiếp nếu cần.
  function flush() {
    if (sync.running) { sync.again = true; return sync.running; }
    sync.running = (async function () {
      // Chụp hàng chờ lúc gửi: quét thêm trong lúc chờ phản hồi thì giữ lại cho lần sau
      const ob = s.state ? outbox() : null;
      if (!ob) return true;
      // Đã biết đang mất mạng: không gọi server (có mạng lại thì setOnline / ping gửi ngay), chỉ in box đủ qua agent
      if (net.online === false) {
        if (!sync.error) sync.error = 'Mất kết nối tới server.';
        sync.network = true;
        await printOfflineBoxes();
        renderCounts();
        scheduleRetry(); // thử in lại box chưa in (agent / máy in có thể hoạt động lại)
        return false;
      }
      const count = outboxCount(ob);
      const sentAdj = ob.adj || 0;
      const body = {
        wo: s.state.woName, productId: ob.productId, barcode: ob.barcode, printer: printTarget(),
        deviceId: deviceId, fromSeq: ob.fromSeq, toSeq: ob.toSeq, count: count
      };
      if (keptBoxes(ob).length || sentAdj) {
        // Box trạm giữ / đã sửa ở trạm: gửi danh sách box (đúng số pcs, đã in chưa) + số pcs box đang quét
        const m = runModel();
        body.boxes = m.boxes.map(function (b) {
          return { boxNo: b.boxNo, qty: b.qty, endSeq: b.endSeq, closedAt: b.closedAt, printed: !!b.printed,
            printer: b.printed ? b.printed.printer : null, version: b.printed ? b.printed.version : null };
        });
        body.openQty = m.openQty;
      } else if (ob.offline) {
        body.offlineBoxes = ob.offline.filter(function (o) { return o.endSeq <= ob.toSeq; }); // hàng chờ của bản cũ
      }
      try {
        const res = await api('/scan', body, SYNC_TIMEOUT_MS);
        // Máy khác đã vào làm phiên: bỏ hàng chờ (không cộng thừa)
        if (!res.success && res.otherDevice) {
          const dropped = outboxCount(outbox());
          log('warn', 'sync.otherDevice', res.error, { dropped: dropped, fromSeq: ob.fromSeq, toSeq: ob.toSeq });
          saveOutbox(null, ob.group);
          sync.error = '';
          // rời màn làm việc trước: lý do hiện ở dòng lỗi dưới ô WO
          leaveRun();
          alertError(res.error + ' Bỏ ' + dropped + ' pcs chưa gửi của máy này. Muốn làm ở máy này thì nhập lại WO và xác nhận.');
          return false;
        }
        // Phiên đã kết thúc (tự động / admin): giữ hàng chờ, xác nhận lại (cùng máy) thì gửi vào phiên mới
        if (!res.success && res.needSession) {
          log('warn', 'sync.needSession', res.error, { pending: count });
          sync.error = '';
          reopenSession(res.error, count);
          return false;
        }
        if (!res.success) throw new Error(res.error);
        // Chỉ ghi lúc vừa hết lỗi / có box in khi mất mạng (gửi bình thường mỗi vài pcs, ghi hết sẽ đẩy mất dòng lỗi)
        if (sync.error || (ob.offline || []).length) log('info', 'sync', 'Đã gửi ' + count + ' pcs', { fromSeq: ob.fromSeq, toSeq: ob.toSeq, offline: (ob.offline || []).length, closed: (res.closed || []).length });
        if (res.result === 'wrong') {
          // Mã quét của phiên đã đổi (xác nhận lại với Lot khác): các pcs này không hợp lệ
          dropSent(ob.toSeq, sentAdj);
          alertError('Bỏ ' + count + ' pcs chưa gửi: mã đã quét không còn đúng với mã quét của phiên.');
        } else {
          dropSent(ob.toSeq, sentAdj);
        }
        sync.error = '';
        applyState(res.state);
        for (const c of (res.closed || [])) {
          if (c.offline) {
            // Đã in lúc mất mạng (qua agent): server chỉ ghi nhận, không in lại
            el.last.innerHTML = '<span class="text-success"><i class="icon-base ti tabler-circle-check me-1"></i>Box ' + c.boxNo + ' đã được ghi nhận</span>';
            if (notyf) notyf.success(lines('Box ' + c.boxNo + ' đã ghi nhận lên server', 'Tem đã in lúc mất kết nối'));
            if (c.printedBoxNo !== c.boxNo)
              alertError('Tem box vừa in ghi số ' + c.printedBoxNo + ', đúng ra là box ' + c.boxNo + '. Báo leader in lại tem box ' + c.boxNo + ' (F2 → Box).');
          } else {
            showPrinted(c.boxNo, await deliver(c.printed, c.boxId));
          }
        }
        return !unsynced();
      } catch (err) {
        // Chỉ ghi lần lỗi đầu / lỗi khác nội dung (thử lại nhiều lần cùng 1 lỗi không ghi thêm)
        if (sync.error !== err.message) log('warn', 'sync.fail', err.message, { fromSeq: ob.fromSeq, toSeq: ob.toSeq, pending: count, network: !!err.network });
        sync.error = err.message;
        sync.network = !!err.network;
        // Mất mạng: box đủ in qua agent (máy in đang chọn) bằng mẫu đã lưu
        if (err.network) await printOfflineBoxes();
        renderCounts();
        scheduleRetry();
        return false;
      }
    })();
    return sync.running.finally(function () {
      sync.running = null;
      if (sync.again) { sync.again = false; if (flushDue()) flush(); }
    });
  }

  // Bỏ phần đã gửi (đến toSeq); các lần quét sau khi bắt đầu gửi vẫn giữ lại
  function dropSent(toSeq, sentAdj) {
    const ob = outbox();
    if (!ob) return;
    if (ob.toSeq <= toSeq) saveOutbox(null, ob.group);
    else {
      ob.fromSeq = toSeq + 1;
      ob.adj = (ob.adj || 0) - (sentAdj || 0);
      if (!ob.adj) delete ob.adj;
      if (ob.offline) ob.offline = ob.offline.filter(function (o) { return o.endSeq > toSeq; });
      if (ob.boxes) ob.boxes = ob.boxes.filter(function (b) { return b.endSeq > toSeq; });
      saveOutbox(ob);
    }
  }

  // ----- In khi mất mạng -----

  // Mẫu tem Box lưu trên máy cho phiên đang làm: { sessionId, confirmedAt, version, zpl, dynamic }. Tải lại khi mẫu sửa (version)
  // hoặc xác nhận lại (giá trị tem có thể đổi). Chỉ dùng khi mất mạng.
  const offlineLabel = { loading: false, error: '' };
  function labelKey() { return LABEL_KEY + runGroup(); }
  function cachedLabel() {
    try { return JSON.parse(storage(labelKey()) || 'null'); } catch (e) { return null; }
  }
  function labelFresh(c) {
    const st = s.state;
    return !!(c && st && st.session && st.template && c.sessionId === st.session.id &&
      c.confirmedAt === st.session.confirmedAt && c.version === st.template.version);
  }
  async function ensureOfflineLabel() {
    const st = s.state;
    if (!st || !st.session || !st.template || offlineLabel.loading || labelFresh(cachedLabel())) return;
    offlineLabel.loading = true;
    try {
      const res = await api('/label/offline?wo=' + encodeURIComponent(st.woName));
      noteServerTime(res.now);
      if (!res.success) throw new Error(res.error);
      if (s.state && s.state.session && s.state.session.id === res.sessionId) {
        storage(labelKey(), JSON.stringify(Object.assign({ sessionId: res.sessionId, confirmedAt: s.state.session.confirmedAt }, res.label)));
        offlineLabel.error = '';
      }
    } catch (err) {
      if (!err.network) { offlineLabel.error = err.message; log('error', 'offline.label', err.message); }
    } finally {
      offlineLabel.loading = false;
    }
  }

  // Việc in gửi agent trên máy này theo máy in đang chọn. USB qua Node: /print-usb; TCP qua Node và Server TCP: /print tới IP:port máy in
  // (Server TCP bình thường do server in; mất kết nối server thì trạm tự in qua agent tới đúng máy in đó, không cần đổi cấu hình)
  function offlineJob(zpl) {
    const c = printerConfig.config();
    const p = printerConfig.current();
    if (!p) return null;
    if (c.mode === 'usb') return { path: '/print-usb', zpl: zpl, printerName: c.usbName };
    return p.ip ? { path: '/print', zpl: zpl, printerIp: p.ip, printerPort: p.port } : null;
  }

  // Lý do chưa in offline được: { kind: 'printer' | 'label' | 'clock', text } (text kỹ thuật, cho tooltip); null = in được
  function offlineBlocker() {
    if (!offlineJob('')) return { kind: 'printer', text: 'Chưa chọn máy in (Menu F2 → Máy in)' };
    const c = cachedLabel();
    if (!c || !s.state || !s.state.session || c.sessionId !== s.state.session.id)
      return { kind: 'label', text: 'Chưa lưu được mẫu tem trên máy' + (offlineLabel.error ? ': ' + offlineLabel.error : '') };
    if (net.skewMs != null && Math.abs(net.skewMs) > CLOCK_BLOCK_MS) return { kind: 'clock', text: 'Đồng hồ máy lệch giờ server quá 1 giờ' };
    return null;
  }

  // Định dạng ngày giờ kiểu .NET (dd/MM/yyyy, HH:mm...) như server điền biến {DATE} / {TIME}
  function formatDate(d, fmt) {
    const p2 = function (n) { return String(n).padStart(2, '0'); };
    const map = {
      yyyy: d.getFullYear(), yy: p2(d.getFullYear() % 100), MM: p2(d.getMonth() + 1), M: d.getMonth() + 1,
      dd: p2(d.getDate()), d: d.getDate(), HH: p2(d.getHours()), H: d.getHours(), mm: p2(d.getMinutes()), m: d.getMinutes(),
      ss: p2(d.getSeconds()), s: d.getSeconds()
    };
    return String(fmt || 'dd/MM/yyyy').replace(/yyyy|yy|MM|M|dd|d|HH|H|mm|m|ss|s/g, function (t) { return String(map[t]); });
  }

  // Giờ địa phương dạng yyyy-MM-ddTHH:mm:ss (server đọc như giờ của nó, không đổi múi giờ)
  function localIso(d) { return formatDate(d, 'yyyy-MM-dd') + 'T' + formatDate(d, 'HH:mm:ss'); }

  // Điền biến theo box vào ZPL đã lưu: số box, số pcs, ngày giờ đóng
  function fillOfflineLabel(label, boxNo, qty, at) {
    const values = {};
    (label.dynamic || []).forEach(function (v) {
      values[v.key.toUpperCase()] = v.source === 'package'
        ? (v.field === 'no' ? String(boxNo) : v.field === 'qty' ? String(qty) : '')
        : formatDate(at, v.format);
    });
    return label.zpl.replace(/\{([A-Za-z_][A-Za-z0-9_]*)\}/g, function (m, k) {
      return Object.prototype.hasOwnProperty.call(values, k.toUpperCase()) ? values[k.toUpperCase()] : m;
    });
  }

  // Box đủ trong hàng chờ mà chưa in: in qua agent (máy in đang chọn), ghi vào hàng chờ (số box, giờ, máy, mẫu, lần quét làm đủ box)
  let offlinePrinting = false;
  // Lỗi in khi mất mạng gần nhất (đã báo đỏ); đổi máy in / in được thì xoá để lỗi mới báo lại
  const offlineFail = { message: '', agentDown: false };
  async function printOfflineBoxes() {
    if (offlinePrinting || offlineBlocker()) return;
    offlinePrinting = true;
    try {
      keepBoxes();
      while (true) {
        const m = runModel();
        if (!m || !m.ob) return;
        const b = keptBoxes(m.ob).find(function (x) { return !x.printed; });
        if (!b) return;
        const label = cachedLabel();
        const p = printerConfig.current();
        const viaAgent = printerConfig.config().mode === 'server' ? 'Server TCP, in qua agent khi mất kết nối' : printerConfig.modeLabel();
        const printer = p.name + ' · ' + p.detail + ' (' + viaAgent + ')';
        const at = new Date();
        try {
          await printerConfig.send(offlineJob(fillOfflineLabel(label, b.boxNo, b.qty, at)));
        } catch (err) {
          // Cứ 5 giây thử lại; cùng box + cùng lỗi chỉ ghi log 1 lần. Không báo đỏ: dòng trạng thái + card báo cảnh báo (cam)
          // cho từng box chưa in được (renderSync); lỗi gốc ở tooltip dòng trạng thái
          const message = 'In box ' + b.boxNo + ' ra ' + p.name + ' lỗi: ' + err.message;
          if (offlineFail.message !== message) log('error', 'offline.print', message, { boxNo: b.boxNo, qty: b.qty, endSeq: b.endSeq, printer: printer });
          offlineFail.message = message;
          offlineFail.agentDown = /agent/i.test(err.message);
          return;
        }
        offlineFail.message = '';
        // Đọc lại hàng chờ (có thể vừa quét / sửa) rồi ghi box đã in
        const cur = outbox();
        const kb = keptBoxes(cur).find(function (x) { return x.boxNo === b.boxNo && x.endSeq === b.endSeq && !x.printed; });
        if (!kb) return;
        kb.printed = { printer: printer, version: label.version };
        kb.closedAt = localIso(at);
        saveOutbox(cur);
        log('info', 'offline.print', 'Box ' + b.boxNo + ' đã in khi mất mạng', { qty: b.qty, endSeq: b.endSeq, printer: printer, version: label.version });
        el.last.innerHTML = '<span class="text-success" title="In khi mất mạng · ' + esc(printer) + '"><i class="icon-base ti tabler-circle-check me-1"></i>Box ' + b.boxNo + ' đã in tem</span>';
        if (notyf) notyf.success(printMsg.offlineOk(b.boxNo, p.name));
        renderCounts();
      }
    } finally {
      offlinePrinting = false;
    }
  }

  // Gửi lỗi: thử lại sau vài giây khi vẫn còn box đủ chờ gửi
  function scheduleRetry() {
    clearTimeout(sync.timer);
    sync.timer = setTimeout(function () { if (flushDue()) flush(); }, SYNC_RETRY_MS);
  }

  // Gửi hết hàng chờ trước thao tác cần số thật trên server (Menu → Box). false = chưa gửi được
  async function flushAll() {
    // Đang gửi dở 1 lô cũ thì gửi thêm lượt nữa cho phần quét sau đó
    for (let i = 0; i < 3 && unsynced(); i++) {
      await flush();
      if (sync.error) break;
    }
    const left = unsynced() ? Math.max(0, pendingForRun()) : 0;
    // Mất kết nối: không báo lỗi (Box mở ở chế độ mất kết nối, dòng trạng thái đã báo); lỗi khác thì báo
    if (left && !sync.network && notyf) notyf.error('Chưa gửi được ' + left + ' pcs: ' + sync.error + ' Báo leader kiểm tra.');
    return !unsynced();
  }

  // Phiên đã kết thúc (tự động khi không hoạt động / admin) trong lúc đang ở màn quét: mở lại modal xác nhận theo tên nhóm lệnh
  // (đã nhập KQSX thì lệnh cũ thành -001 done, trạm tự lấy lệnh đang mở của nhóm). Pcs chưa gửi giữ lại, xác nhận xong thì gửi tiếp
  function reopenSession(message, pending) {
    const code = (s.wo && s.wo.group) || s.state.woName;
    leaveRun();
    // sau leaveRun: lý do hiện ở dòng lỗi dưới ô WO (modal xác nhận mở ra thì có thêm toast)
    if (notyf) notyf.open({ type: 'error', message: message + (pending ? ' ' + pending + ' pcs chưa gửi được giữ lại, xác nhận xong sẽ gửi tiếp.' : ''), duration: 10000 });
    el.wo.value = code;
    openWo(code);
  }

  function showPrinted(boxNo, printed) {
    if (printed.ok) {
      el.last.innerHTML = '<span class="text-success"><i class="icon-base ti tabler-circle-check me-1"></i>Box ' + boxNo + ' đã in · ' + esc(printed.printerName) + '</span>';
      if (notyf) notyf.success(printMsg.ok(boxNo, printed.printerName));
    } else {
      el.last.innerHTML = '<span class="text-danger"><i class="icon-base ti tabler-circle-x me-1"></i>Box ' + boxNo + ' in lỗi</span>';
      alertError(printMsg.fail(boxNo, printed.error));
    }
  }

  // ===== Menu F2 (PIN leader) =====

  const MENU_VIEWS = { pin: 'menuPinView', home: 'menuHome', box: 'menuBox' };
  const MENU_TITLES = { pin: 'Menu · nhập PIN leader', home: 'Menu', box: 'Box', end: 'Kết thúc phiên' };
  let menuView = 'pin';

  // Mở được cả ngoài phiên (cài máy in, in thủ công); Box / Kết thúc chỉ có trong phiên
  function openMenu() {
    if (document.querySelector('.modal.show')) return;
    // Không yêu cầu PIN (Toast:RequireLeaderPin = false), PIN còn hạn (2 phút từ lần đóng menu trước), hoặc đang mất mạng
    // (không kiểm PIN được, chỉ có ô Máy in để đổi sang máy in qua agent): vào thẳng các ô
    if (!cfg.requirePin || (s.pin && Date.now() < s.pinUntil) || net.online === false) {
      showMenuView('home');
    } else {
      s.pin = '';
      el.menuPin.value = '';
      showMenuView('pin');
    }
    menuModal.show();
  }

  // Đóng menu / in thủ công / cấu hình máy in: PIN còn dùng được thêm 2 phút
  function startPinGrace() {
    if (s.pin) s.pinUntil = Date.now() + PIN_GRACE_MS;
  }

  function showMenuView(view) {
    menuView = view;
    Object.keys(MENU_VIEWS).forEach(function (k) {
      const node = el[MENU_VIEWS[k]];
      node.classList.toggle('d-none', k !== view);
      if (node !== el.menuHome) node.classList.toggle('d-flex', k === view);
    });
    el.menuTitle.textContent = MENU_TITLES[view];
    el.menuBack.classList.toggle('d-none', view === 'pin' || view === 'home');
    if (view === 'pin' && el.menuModal.classList.contains('show')) el.menuPin.focus();
    if (view === 'home') {
      const tiles = visibleTiles();
      el.menuHome.querySelectorAll('[data-view]').forEach(function (t) { t.classList.toggle('d-none', tiles.indexOf(t) < 0); });
      tiles.forEach(function (t, i) { t.querySelector('.st-key').textContent = i + 1; });
      if (tiles[0]) tiles[0].focus();
    }
    if (view === 'box') renderMenuBoxes();
  }

  async function unlockMenu(e) {
    e.preventDefault();
    const pin = el.menuPin.value;
    try {
      const res = await api('/pin', { pin: pin });
      if (!res.success) throw new Error(res.error);
      s.pin = pin;
      showMenuView('home');
    } catch (err) {
      if (notyf) notyf.error(err.message);
      el.menuPin.select();
    }
  }

  function visibleTiles() {
    return Array.prototype.filter.call(el.menuHome.querySelectorAll('[data-view]'), function (t) {
      // Mất kết nối: Máy in + Box (Box chỉ xem; box chưa đồng bộ đã in thì In lại tem qua agent)
      if (net.online === false) return t.dataset.view === 'printer' || (t.dataset.view === 'box' && !!s.state);
      return !!s.state || !t.hasAttribute('data-session-only');
    });
  }

  function chooseTile(view) {
    // Mất kết nối: không kiểm PIN được (cần server) -> Máy in, Box vào thẳng
    if (cfg.requirePin && !s.pin && !(net.online === false && (view === 'printer' || view === 'box'))) return;
    if (view === 'box' && !s.state) return;
    if (view === 'manual' || view === 'printer') { s.next = view; menuModal.hide(); return; }
    // Box: gửi hết pcs chưa gửi trước để số trên server đúng (đóng thiếu / sửa số / xoá). Mất kết nối thì không gửi được, mở xem luôn
    if (view === 'box' && unsynced() && net.online !== false) {
      // Gửi lỗi vì mất kết nối (trạm có thể chưa kịp biết qua ping): vẫn mở Box ở chế độ mất kết nối (chỉ xem / in lại box chưa đồng bộ)
      flushAll().then(function (ok) { if (ok || sync.network) showMenuView('box'); });
      return;
    }
    showMenuView(view);
  }

  // Gọi API cần PIN từ menu; có state mới thì cập nhật màn
  async function menuCall(path, body) {
    const res = await api(path, Object.assign({ pin: s.pin, printer: printTarget() }, body || {}));
    if (res.state) applyState(res.state);
    if (!res.success) throw new Error(res.error);
    return res;
  }

  // ----- Box -----

  // Box trong Menu: box chưa đồng bộ ở máy này (id âm = -số box) + box trên server (bỏ box đang mở mà máy này đã đủ hàng)
  function menuBoxes() {
    const m = runModel();
    if (!m) return [];
    const local = localBoxes().map(function (b) { return Object.assign({ id: -b.boxNo }, b); });
    // Box đang quét ở máy này (dữ liệu chưa đồng bộ, còn pcs): đóng thiếu / sửa số / xoá được; đã xoá / 0 pcs thì không có dòng
    const openLocal = localOpenBox(m);
    if (openLocal) local.unshift(openLocal);
    return local.concat(serverBoxesShown(m, local.map(function (b) { return b.boxNo; })));
  }

  function renderMenuBoxes() {
    const list = menuBoxes();
    if (!list.some(function (b) { return b.id === s.boxId; })) s.boxId = null;
    el.menuBoxList.innerHTML = list.length ? list.map(function (b) {
      const badge = b.local
        ? '<span class="badge bg-label-warning me-1">Chưa đồng bộ</span>' + (b.open ? '<span class="badge bg-label-primary">Đang quét</span>'
          : b.printed ? '<span class="badge bg-label-info">Đã in (mất kết nối)</span>' : '<span class="badge bg-label-secondary">Chưa in</span>')
        : (b.status === 'Open' ? '<span class="badge bg-label-primary">Đang quét</span>' : printBadge(b));
      return '<button type="button" class="list-group-item list-group-item-action d-flex align-items-center gap-3' + (b.id === s.boxId ? ' active' : '') + '" data-id="' + b.id + '">' +
        '<span class="fw-semibold">Box ' + b.boxNo + '</span><span>' + b.qty + ' / ' + b.capacity + ' pcs</span>' +
        '<span class="ms-auto">' + badge + '</span></button>';
    }).join('') : '<div class="text-muted text-center py-4">Chưa có box nào</div>';
    renderBoxActions();
  }

  function renderBoxActions() {
    const b = findBox(s.boxId);
    s.deleteArmed = false;
    el.menuBoxActions.classList.toggle('d-none', !b);
    el.menuBoxActions.classList.toggle('d-flex', !!b);
    el.menuQtyForm.classList.add('d-none');
    el.menuQtyForm.classList.remove('d-flex');
    if (!b) return;
    const btn = function (action) { return el.menuBoxActions.querySelector('[data-box-action="' + action + '"]'); };
    // Box chưa đồng bộ (ở máy này): sửa số / xoá / đóng thiếu / in lại ngay trên máy, đồng bộ khi có kết nối.
    // Mất kết nối mà box đã đồng bộ (trên server): khoá
    // Box chưa đồng bộ: đủ thao tác như bình thường (đang quét: Đóng box thiếu / Sửa số / Xoá; đã đóng: In lại tem / Sửa số / Xoá)
    if (b.local) {
      btn('reprint').classList.toggle('d-none', !!b.open);
      btn('close').classList.toggle('d-none', !b.open || b.qty < 1);
      btn('qty').classList.remove('d-none');
      btn('delete').classList.remove('d-none');
      el.menuBoxActions.querySelector('[data-box-action="delete"] span').textContent = 'Xoá box';
      el.menuBoxTitle.textContent = 'Box ' + b.boxNo + ' · ' + b.qty + ' / ' + b.capacity + ' pcs · ' + (b.open ? 'đang quét' : 'đã đóng') + ' · chưa đồng bộ';
      el.menuBoxHint.textContent = b.open
        ? 'Sửa đủ ' + b.capacity + ' pcs thì box tự đóng và in tem. Có kết nối sẽ gửi lên server.'
        : (b.printed ? 'Sửa số / xoá thì các tem liên quan tự in lại. ' : 'Chưa in tem: bấm In lại tem để in ngay (qua agent). ') +
          'Có kết nối sẽ gửi lên server.';
      return;
    }
    if (net.online === false) {
      ['reprint', 'close', 'qty', 'delete'].forEach(function (a) { btn(a).classList.add('d-none'); });
      el.menuBoxTitle.textContent = 'Box ' + b.boxNo + ' · ' + b.qty + ' / ' + b.capacity + ' pcs · ' + (b.status === 'Open' ? 'đang quét' : 'đã đóng');
      el.menuBoxHint.textContent = 'Box đã đồng bộ: cần kết nối server để sửa.';
      return;
    }
    ['qty', 'delete'].forEach(function (a) { btn(a).classList.remove('d-none'); });
    const open = b.status === 'Open';
    el.menuBoxTitle.textContent = 'Box ' + b.boxNo + ' · ' + b.qty + ' / ' + b.capacity + ' pcs · ' + (open ? 'đang quét' : 'đã đóng');
    el.menuBoxActions.querySelector('[data-box-action="reprint"]').classList.toggle('d-none', open);
    el.menuBoxActions.querySelector('[data-box-action="close"]').classList.toggle('d-none', !open || b.qty < 1);
    el.menuBoxActions.querySelector('[data-box-action="delete"] span').textContent = 'Xoá box';
    el.menuBoxHint.textContent = open
      ? 'Sửa đủ ' + b.capacity + ' pcs thì box tự đóng và in tem.'
      : (b.printError ? 'Lần in trước lỗi: ' + b.printError : 'Sửa số không in lại tem; cần tem mới thì bấm In lại tem.');
  }

  async function onBoxActionClick(action) {
    const b = findBox(s.boxId);
    if (!b) return;
    if (action === 'qty') {
      el.menuQtyForm.classList.remove('d-none');
      el.menuQtyForm.classList.add('d-flex');
      el.menuQty.value = b.qty;
      el.menuQty.select();
      return;
    }
    if (b.local && action !== 'qty') {
      if (action === 'reprint' && !b.open) await reprintLocal(b);
      else if (action === 'close' && b.open) closeLocal();
      else if (action === 'delete') {
        if (!s.deleteArmed) {
          s.deleteArmed = true;
          el.menuBoxActions.querySelector('[data-box-action="delete"] span').textContent = 'Bấm lần nữa để xoá box ' + b.boxNo;
          return;
        }
        if (b.open) qtyLocal(b, 0, true); else deleteLocal(b);
      }
      return;
    }
    if (net.online === false && !b.local) return;
    if (action === 'delete' && !s.deleteArmed) {
      s.deleteArmed = true;
      el.menuBoxActions.querySelector('[data-box-action="delete"] span').textContent = 'Bấm lần nữa để xoá box ' + b.boxNo;
      return;
    }
    try {
      const path = { reprint: '/box/' + b.id + '/reprint', close: '/box/' + b.id + '/close', delete: '/box/' + b.id + '/delete' }[action];
      const res = await menuCall(path);
      const printed = await deliver(res.printed, res.boxId);
      if (action === 'reprint' && printed && !printed.ok) throw new Error(printMsg.reprintFail(b.boxNo, printed.error));
      if (notyf) notyf.success(action === 'reprint' && printed ? printMsg.reprintOk(b.boxNo, printed.printerName) : res.message);
      if (action === 'close' && printed) showPrinted(b.boxNo, printed);
      renderMenuBoxes();
    } catch (err) {
      if (notyf) notyf.error(err.message);
      renderMenuBoxes();
    }
  }

  async function submitQty(e) {
    e.preventDefault();
    const b = findBox(s.boxId);
    if (!b) return;
    if (b.local) {
      const qty = parseInt(el.menuQty.value, 10) || 0;
      if (qty < (b.open ? 0 : 1)) { if (notyf) notyf.error(b.open ? 'Số pcs không hợp lệ.' : 'Số pcs phải từ 1 trở lên. Muốn bỏ box thì xoá box.'); return; }
      qtyLocal(b, qty);
      return;
    }
    try {
      const res = await menuCall('/box/' + b.id + '/qty', { qty: parseInt(el.menuQty.value, 10) || 0 });
      if (notyf) notyf.success(res.message);
      if (res.printed) showPrinted(b.boxNo, await deliver(res.printed, res.boxId));
      renderMenuBoxes();
    } catch (err) {
      if (notyf) notyf.error(err.message);
    }
  }

  function findBox(id) {
    return s.state ? menuBoxes().find(function (b) { return b.id === id; }) || null : null;
  }

  // In lại tem box chưa đồng bộ (đã in lúc mất kết nối): ZPL lưu trên máy + giờ in lần đầu, gửi agent. Không đổi hàng chờ.
  async function reprintLocal(b) {
    try {
      const label = cachedLabel();
      if (!label) throw new Error('Chưa lưu được mẫu tem trên máy');
      const at = b.closedAt ? new Date(b.closedAt) : new Date();
      const job = offlineJob(fillOfflineLabel(label, b.boxNo, b.qty, at));
      if (!job) throw new Error('Chưa chọn máy in');
      await printerConfig.send(job);
      if (!b.printed) {
        // Box chưa in: ghi đã in để trạm không tự in lại
        keepBoxes();
        const ob = outbox();
        const kb = keptBoxes(ob).find(function (x) { return x.boxNo === b.boxNo; });
        const p = printerConfig.current() || {};
        if (kb && !kb.printed) {
          kb.printed = { printer: p.name + ' · ' + p.detail, version: label.version };
          kb.closedAt = localIso(at);
          saveOutbox(ob);
        }
        renderCounts();
        renderMenuBoxes();
        if (notyf) notyf.success(printMsg.offlineOk(b.boxNo, p.name));
        return;
      }
      if (notyf) notyf.success(printMsg.reprintOk(b.boxNo, (printerConfig.current() || {}).name));
    } catch (err) {
      log('error', 'offline.reprint', err.message, { boxNo: b.boxNo });
      if (notyf) notyf.error(printMsg.reprintFail(b.boxNo, err.message));
    }
  }

  // ===== In tem (Menu → In tem): giống màn In tem admin (1. WO → 2. Mẫu tem → 3. Biến), trong hoặc ngoài phiên =====
  // Có WO: phần 2 chỉ hiện mẫu tem của sản phẩm (tự chọn mẫu Box / mẫu đầu), "Tìm mẫu khác" mở ô tìm. Không có WO: chỉ hiện kết quả tìm
  // theo mã (không liệt kê hết mẫu). WO chỉ để điền dữ liệu. Ô đã gõ tay giữ giá trị khi đổi mẫu / WO. Biến bắt buộc trống thì chưa cho in.

  const DPMM_BY_DPI = { 152: 6, 203: 8, 300: 12, 600: 24 };
  const SOURCE_LABEL = {
    template: ['secondary', 'mẫu tem'], wo: ['info', 'Viindoo'], session: ['success', 'Từ WO'], product: ['secondary', 'sản phẩm'],
    system: ['secondary', 'hệ thống'], spec: ['primary', 'quy cách'], manual: ['warning', 'nhập tay']
  };
  const TPL_EMPTY = 'Nhập WO hoặc tìm theo mã sản phẩm để hiện các mẫu tem';
  // wo: dữ liệu WO đã tìm (null = không theo WO); woTemplates: mẫu của sản phẩm thuộc WO; ctx: mẫu đang chọn + biến gợi ý
  // unlocked: biến đang mở khoá để sửa (mặc định khoá, chỉ đọc; biến bắt buộc đang trống thì mở sẵn). Đổi WO / mẫu thì khoá lại
  const lbl = { wo: null, inSession: false, woWarnings: [], woTemplates: [], templateId: null, ctx: null, values: {}, typed: {}, unlocked: {}, previewTpl: null };

  const preview = ZplPreview.create('st-lbl-preview', {
    previewUrl: cfg.apiUrl + '/label/preview',
    request: async function (url, options) {
      const send = function () {
        return fetch(url, Object.assign({}, options, {
          headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': token }
        }));
      };
      let res = await send();
      // Mã chống giả mạo cũ (server restart / deploy): lấy mã mới, gửi lại 1 lần
      if (res.status === 400) { await refreshToken(); res = await send(); }
      if (!res.ok) throw new Error('Lỗi máy chủ (' + res.status + ').');
      return res.json();
    },
    getSource: function () {
      const t = (lbl.ctx && lbl.ctx.template) || {};
      return {
        zplData: t.zplData || '', size: t.size, dpmm: DPMM_BY_DPI[t.dpi],
        variables: labelVars().map(function (v) { return { key: v.key, value: lbl.values[v.key] || '' }; }),
        fileName: [t.defaultCode, t.type].filter(Boolean).join('_').replace(/[^\w.-]+/g, '-') || 'label'
      };
    }
  });

  function labelVars() { return (lbl.ctx && lbl.ctx.variables) || []; }

  // Mở modal: trong phiên thì tự tìm WO đang làm (chọn sẵn mẫu Box của sản phẩm)
  function openManual() {
    Object.assign(lbl, { wo: null, inSession: false, woWarnings: [], woTemplates: [], templateId: null, ctx: null, values: {}, typed: {}, unlocked: {} });
    el.lblWo.value = s.state ? s.state.woName : '';
    el.lblWo.classList.remove('is-invalid');
    el.lblTplQ.value = '';
    el.copies.value = 1;
    renderWoInfo();
    showTemplateSearch(true);
    renderTemplates([], TPL_EMPTY);
    renderLabel();
    manualModal.show();
    if (el.lblWo.value) searchLabelWo(s.state && s.state.template ? s.state.template.id : null);
    else setTimeout(function () { el.lblWo.focus(); }, 300);
  }

  // ----- 1. WO -----

  async function searchLabelWo(preferId) {
    const code = el.lblWo.value.trim();
    if (!code) { el.lblWo.focus(); return; }
    setBusy(el.lblLoad, true, 'Đang tìm...');
    try {
      const res = await api('/label/wo?wo=' + encodeURIComponent(code));
      if (!res.success) throw new Error(res.error);
      el.lblWo.classList.remove('is-invalid');
      Object.assign(lbl, { wo: res.wo, inSession: res.inSession, woWarnings: res.warnings || [], woTemplates: res.templates || [] });
      renderWoInfo();
      showTemplateSearch(false);
      renderTemplates(lbl.woTemplates, 'Sản phẩm ' + res.wo.productCode + ' chưa có mẫu tem. Bấm "Tìm mẫu khác" hoặc tạo ở màn Mẫu tem.');
      // Giữ mẫu đang chọn nếu thuộc sản phẩm này; không thì mẫu được gợi ý / mẫu Box / mẫu đầu
      const list = lbl.woTemplates;
      const pick = list.find(function (t) { return t.id === lbl.templateId; }) || list.find(function (t) { return t.id === preferId; }) ||
        list.find(function (t) { return t.type === 'Box'; }) || list[0];
      if (pick) selectLabelTemplate(pick.id); else { lbl.templateId = null; lbl.ctx = null; renderLabel(); }
    } catch (err) {
      el.lblWo.classList.add('is-invalid');
      if (notyf) notyf.error(err.message);
    } finally {
      setBusy(el.lblLoad, false, '<i class="icon-base ti tabler-search me-1"></i>Tìm');
    }
  }

  // Bỏ WO: in không theo WO. Giữ mẫu đang chọn, điền lại biến (biến theo WO thành nhập tay)
  function clearLabelWo() {
    Object.assign(lbl, { wo: null, inSession: false, woWarnings: [], woTemplates: [] });
    el.lblWo.value = '';
    el.lblWo.classList.remove('is-invalid');
    renderWoInfo();
    showTemplateSearch(true);
    if (el.lblTplQ.value.trim()) searchLabelTemplates(); else renderTemplates(lbl.ctx ? [lbl.ctx.template] : [], TPL_EMPTY);
    if (lbl.templateId) loadLabelContext();
    el.lblWo.focus();
  }

  function renderWoInfo() {
    const w = lbl.wo;
    el.lblClear.disabled = !w;
    el.lblWoInfo.classList.toggle('d-none', !w);
    if (!w) { el.lblWoInfo.innerHTML = ''; return; }
    const row = function (label, value) { return '<dt>' + label + '</dt><dd>' + (value ? esc(value) : '<span class="text-muted">—</span>') + '</dd>'; };
    el.lblWoInfo.innerHTML =
      '<dt>Mã WO</dt><dd><span class="fw-medium">' + esc(w.name) + '</span> ' +
        (lbl.inSession ? '<span class="badge bg-label-success">Trong phiên</span>' : '<span class="badge bg-label-secondary">Ngoài phiên · đọc Viindoo</span>') + '</dd>' +
      row('Sản phẩm', w.productCode + (w.productName ? ' · ' + w.productName : '')) +
      row('Số lượng WO', w.productQty) + row('Đơn bán', w.saleOrder) + row('PO', w.po) + row('Lot ID', w.lotId) + row('Mã khách', w.customerPartNumber);
  }

  // ----- 2. Mẫu tem -----

  // Có WO: ẩn ô tìm, hiện nút "Tìm mẫu khác". Không có WO: luôn có ô tìm
  function showTemplateSearch(show) {
    el.lblTplSearch.classList.toggle('d-none', !show);
    el.lblTplToggle.classList.toggle('d-none', show || !lbl.wo);
  }

  let tplTimer = null;
  async function searchLabelTemplates() {
    const q = el.lblTplQ.value.trim();
    if (!q) { renderTemplates(lbl.wo ? lbl.woTemplates : [], TPL_EMPTY); return; }
    try {
      const res = await api('/label/templates?q=' + encodeURIComponent(q));
      renderTemplates(res.templates || [], 'Không tìm thấy mẫu tem nào khớp "' + q + '"');
    } catch (err) {
      if (notyf) notyf.error(err.message);
    }
  }

  function renderTemplates(list, emptyText) {
    if (!list.length) { el.lblTpls.innerHTML = '<div class="st-lbl-empty">' + esc(emptyText) + '</div>'; return; }
    el.lblTpls.innerHTML = list.map(function (t) {
      const on = t.id === lbl.templateId;
      const typeClass = t.type === 'Carton' ? 'bg-label-info' : t.type === 'Plate' ? 'bg-label-warning' : 'bg-label-primary';
      return '<label class="st-tpl' + (on ? ' is-selected' : '') + '">' +
        '<input class="form-check-input mt-0" type="radio" name="st-lbl-tpl" value="' + t.id + '"' + (on ? ' checked' : '') + ' />' +
        '<span class="flex-grow-1"><span class="fw-medium text-heading">' + esc(t.defaultCode) + '</span> <span class="badge ' + typeClass + ' ms-1">' + esc(t.type) + '</span>' +
        ' <span class="text-muted small ms-1">' + esc(t.operation) + (t.size ? ' · ' + esc(t.size) : '') + (t.dpi ? ' · ' + t.dpi + ' dpi' : '') + '</span></span></label>';
    }).join('');
  }

  function selectLabelTemplate(id) {
    lbl.templateId = id;
    el.lblTpls.querySelectorAll('.st-tpl').forEach(function (l) {
      const input = l.querySelector('input');
      const on = parseInt(input.value, 10) === id;
      input.checked = on;
      l.classList.toggle('is-selected', on);
    });
    loadLabelContext();
  }

  // ----- 3. Biến -----

  // Giá trị gợi ý theo mẫu + WO; ô đã gõ tay giữ nguyên
  async function loadLabelContext() {
    if (!lbl.templateId) { lbl.ctx = null; renderLabel(); return; }
    try {
      const res = await api('/label/context?templateId=' + lbl.templateId + (lbl.wo ? '&wo=' + encodeURIComponent(lbl.wo.name) : ''));
      if (!res.success) throw new Error(res.error);
      lbl.ctx = res;
      const values = {};
      (res.variables || []).forEach(function (v) { values[v.key] = lbl.typed[v.key] ? (lbl.values[v.key] || '') : v.value; });
      lbl.values = values;
      // Khoá lại hết, chỉ mở sẵn biến bắt buộc đang trống (khoá thì không in được)
      lbl.unlocked = {};
      (res.variables || []).forEach(function (v) { if (!v.optional && !String(values[v.key] || '').trim()) lbl.unlocked[v.key] = true; });
    } catch (err) {
      if (notyf) notyf.error(err.message);
      lbl.ctx = null;
    }
    renderLabel();
  }

  function renderLabel() {
    const c = lbl.ctx;
    const vars = labelVars();
    el.lblVars.innerHTML = !c ? '<tr><td colspan="3" class="text-muted small text-center py-3">Chọn mẫu tem.</td></tr>'
      : vars.length ? vars.map(function (v) {
        const value = lbl.values[v.key] || '';
        const src = lbl.typed[v.key] ? SOURCE_LABEL.manual : (SOURCE_LABEL[v.source] || ['secondary', v.source]);
        const missing = !v.optional && !value.trim();
        const open = !!lbl.unlocked[v.key];
        const lockText = open ? 'Khoá lại {' + v.key + '}' : 'Mở khoá để sửa {' + v.key + '}';
        return '<tr><td><div class="st-lbl-key">{' + esc(v.key) + '}</div>' + (v.label !== v.key ? '<div class="small text-muted">' + esc(v.label) + '</div>' : '') + '</td>' +
          '<td><div class="input-group input-group-sm' + (missing ? ' has-validation' : '') + '">' +
            '<input type="text" class="form-control' + (missing ? ' is-invalid' : '') + (open ? '' : ' st-lbl-locked') + '" data-lbl-var="' + esc(v.key) + '" value="' + esc(value) + '"' +
            (missing ? ' placeholder="Thiếu"' : '') + (open ? '' : ' readonly tabindex="-1"') + ' aria-label="' + esc(v.label) + '" />' +
            '<button type="button" class="btn ' + (open ? 'btn-label-warning' : 'btn-label-secondary') + '" data-lbl-lock="' + esc(v.key) + '" aria-label="' + esc(lockText) + '" title="' + esc(lockText) + '">' +
              '<i class="icon-base ti ' + (open ? 'tabler-lock-open' : 'tabler-lock') + '"></i></button>' +
          '</div></td>' +
          '<td><span class="badge bg-label-' + src[0] + '">' + src[1] + '</span></td></tr>';
      }).join('') : '<tr><td colspan="3" class="text-muted small text-center py-3">Mẫu tem không có biến.</td></tr>';

    const warnings = c ? c.warnings || [] : lbl.woWarnings;
    el.lblWarnings.innerHTML = warnings.map(function (x) {
      return '<div class="alert alert-warning py-2 small mb-2"><i class="icon-base ti tabler-alert-triangle me-1"></i>' + esc(x) + '</div>';
    }).join('');
    updateLabelPrintable();
    // Đổi mẫu tem: đặt lại khung xem (DPI theo mẫu) rồi vẽ
    const tplId = c ? c.template.id : null;
    if (tplId !== lbl.previewTpl) { lbl.previewTpl = tplId; preview.reset(); }
    lbl.previewSig = JSON.stringify(lbl.values);
    preview.refresh();
  }

  function labelMissing() {
    return labelVars().filter(function (v) { return !v.optional && !String(lbl.values[v.key] || '').trim(); });
  }

  function updateLabelPrintable() {
    el.manualPrint.disabled = !lbl.ctx || labelMissing().length > 0;
  }

  async function submitManual(e) {
    e.preventDefault();
    const c = lbl.ctx;
    if (!c) return;
    const missing = labelMissing();
    if (missing.length) { if (notyf) notyf.error('Còn thiếu: ' + missing.map(function (v) { return v.label; }).join(', ')); return; }
    if (!printTarget()) { if (notyf) notyf.error('Chọn máy in ở Menu → Máy in'); return; }
    setBusy(el.manualPrint, true, 'Đang in...');
    const copies = parseInt(el.copies.value, 10) || 1;
    try {
      const res = await api('/label/print', {
        templateId: c.template.id, values: lbl.values, copies: copies, pin: s.pin, printer: printTarget()
      });
      if (!res.success) throw new Error(res.error);
      const printed = await deliver(res.printed, null);
      if (printed && !printed.ok) throw new Error(printed.error);
      if (notyf) notyf.success(printMsg.manualOk(copies, c.template.type, c.template.defaultCode, printed && printed.printerName));
    } catch (err) {
      if (notyf) notyf.error(printMsg.manualFail(c.template.type, c.template.defaultCode, err.message));
    } finally {
      setBusy(el.manualPrint, false, '<i class="icon-base ti tabler-printer me-1"></i>In tem');
      updateLabelPrintable();
    }
  }

  // ===== Tiện ích =====

  function setBusy(btn, busy, html) {
    btn.disabled = busy;
    btn.innerHTML = busy ? '<span class="spinner-border spinner-border-sm me-1"></span>' + html : html;
  }

  function esc(v) {
    return String(v == null ? '' : v)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  // ===== Sự kiện =====

  el.sessionForm.addEventListener('submit', startSession);
  el.sessionConfirm.addEventListener('change', updateSessionButton);
  // Ô mã nhân viên (SVN có sẵn bên trái): chỉ giữ số, tối đa 4
  el.sessionEmployee.addEventListener('input', function () {
    const v = el.sessionEmployee.value.replace(/\D/g, '').slice(0, 4);
    if (v !== el.sessionEmployee.value) el.sessionEmployee.value = v;
    showEmployeeError('');
    updateSessionButton();
  });
  // Enter ở ô mã: chưa tick xác nhận thì chuyển sang checkbox, đã tick thì Vào làm việc
  el.sessionEmployee.addEventListener('keydown', function (e) {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    if (!el.sessionConfirm.checked && !el.sessionConfirm.disabled) el.sessionConfirm.focus();
    else el.sessionForm.requestSubmit();
  });
  el.sessionModal.addEventListener('shown.bs.modal', function () {
    if (!s.check || !s.check.canStart) return;
    // Ô mã nhân viên ở trên, checkbox xác nhận ở dưới: con trỏ vào ô mã
    if (printTarget()) el.sessionEmployee.focus();
  });
  // Huỷ modal: quay về ô nhập WO (phiên đang mở ở server vẫn giữ nguyên)
  el.sessionModal.addEventListener('hidden.bs.modal', function () {
    if (!s.state) { storage(WO_KEY, null); el.wo.select(); }
  });

  el.woForm.addEventListener('submit', function (e) { e.preventDefault(); openWo(el.wo.value); });
  // Ô WO (tiền tố NM/MO/ có sẵn): chỉ giữ số và dấu - (backorder). Quét / dán cả "NM/MO/06229" thì bỏ phần chữ
  el.wo.addEventListener('input', function () {
    const v = el.wo.value.replace(/[^\d-]/g, '');
    if (v !== el.wo.value) el.wo.value = v;
  });
  el.boxesToggle.addEventListener('click', function () {
    s.boxesExpanded = !s.boxesExpanded;
    renderBoxes();
    if (!el.scan.disabled) el.scan.focus(); // trả con trỏ về ô quét để quét tiếp
  });
  el.scanForm.addEventListener('submit', scan);

  // Menu
  el.wo.addEventListener('input', function () { woError(''); });
  // Còn pcs / box chưa gửi lên server (thường là lúc mất mạng): đóng tab / tải lại / chuyển trang thì trình duyệt hỏi xác nhận.
  // Chrome / Edge chỉ hiện câu chung của trình duyệt, không đặt được chữ riêng (cảnh báo bằng chữ ở dòng trạng thái + card)
  window.addEventListener('beforeunload', function (e) {
    if (!isRunning() || !outbox()) return;
    e.preventDefault();
    e.returnValue = '';
  });
  renderNotices();
  el.menuOpen.addEventListener('click', openMenu);
  el.menuPinView.addEventListener('submit', unlockMenu);
  el.menuBack.addEventListener('click', function () { showMenuView('home'); });
  el.menuHome.addEventListener('click', function (e) {
    const tile = e.target.closest('[data-view]');
    if (tile) chooseTile(tile.dataset.view);
  });
  el.menuBoxList.addEventListener('click', function (e) {
    const item = e.target.closest('[data-id]');
    if (!item) return;
    s.boxId = parseInt(item.dataset.id, 10);
    renderMenuBoxes();
  });
  el.menuBoxActions.addEventListener('click', function (e) {
    const btn = e.target.closest('[data-box-action]');
    if (btn) onBoxActionClick(btn.dataset.boxAction);
  });
  el.menuQtyForm.addEventListener('submit', submitQty);

  // Ô số (Sửa số pcs, Số bản in thủ công): chỉ cho nhập số; nút − / + tăng giảm 1, giữ trong khoảng data-min / data-max
  function clampNumber(input, value) {
    const min = parseInt(input.dataset.min, 10), max = parseInt(input.dataset.max, 10);
    if (!isNaN(min) && value < min) value = min;
    if (!isNaN(max) && value > max) value = max;
    return value;
  }
  [el.menuQty, el.copies].forEach(function (input) {
    input.addEventListener('input', function () {
      const v = input.value.replace(/\D/g, '');
      if (v !== input.value) input.value = v;
    });
  });
  document.addEventListener('click', function (e) {
    const btn = e.target.closest('[data-step-for]');
    if (!btn) return;
    const input = $(btn.dataset.stepFor);
    const current = parseInt(input.value, 10) || 0;
    input.value = clampNumber(input, current + parseInt(btn.dataset.step, 10));
    input.focus();
  });
  // Mở menu: con trỏ vào ô PIN để nhập luôn
  el.menuModal.addEventListener('shown.bs.modal', function () { if (menuView === 'pin') el.menuPin.focus(); });
  el.menuModal.addEventListener('hidden.bs.modal', function () {
    s.boxId = null;
    startPinGrace();
    const next = s.next;
    s.next = null;
    if (next === 'manual') openManual();
    else if (next === 'printer') printerConfig.open();
    else focusScan();
  });
  el.manualModal.addEventListener('hidden.bs.modal', function () { startPinGrace(); focusScan(); });
  printerConfig.modalElement.addEventListener('hidden.bs.modal', function () { startPinGrace(); focusScan(); });

  // F2 mở menu; trong menu (đã mở khoá) bấm số chọn ô theo thứ tự đang hiện
  document.addEventListener('keydown', function (e) {
    if (e.key === 'F2') {
      e.preventDefault();
      if (!el.menuModal.classList.contains('show')) openMenu();
      return;
    }
    if (el.menuModal.classList.contains('show') && menuView === 'home' && /^[1-9]$/.test(e.key)) {
      const tile = visibleTiles()[parseInt(e.key, 10) - 1];
      if (tile) { e.preventDefault(); chooseTile(tile.dataset.view); }
    }
  });

  el.manualForm.addEventListener('submit', submitManual);
  el.lblLoad.addEventListener('click', function () { searchLabelWo(null); });
  el.lblWo.addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); searchLabelWo(null); } });
  el.lblClear.addEventListener('click', clearLabelWo);
  el.lblTplToggle.addEventListener('click', function () { showTemplateSearch(true); el.lblTplQ.focus(); });
  el.lblTplQ.addEventListener('input', function () { clearTimeout(tplTimer); tplTimer = setTimeout(searchLabelTemplates, 300); });
  el.lblTpls.addEventListener('change', function (e) {
    if (e.target.name === 'st-lbl-tpl') selectLabelTemplate(parseInt(e.target.value, 10));
  });
  // Nút ổ khoá: mở khoá dòng để sửa (con trỏ vào ô luôn) / khoá lại (giữ giá trị đã gõ). Chỉ đổi ô đó, không vẽ lại xem trước;
  // khoá lại sau khi sửa thì vẽ lại (nếu giá trị đã đổi)
  el.lblVars.addEventListener('click', function (e) {
    const btn = e.target.closest('[data-lbl-lock]');
    if (!btn) return;
    const key = btn.dataset.lblLock;
    const open = !lbl.unlocked[key];
    lbl.unlocked[key] = open;
    const input = btn.closest('.input-group').querySelector('input[data-lbl-var]');
    const text = open ? 'Khoá lại {' + key + '}' : 'Mở khoá để sửa {' + key + '}';
    input.readOnly = !open;
    input.tabIndex = open ? 0 : -1;
    input.classList.toggle('st-lbl-locked', !open);
    btn.className = 'btn ' + (open ? 'btn-label-warning' : 'btn-label-secondary');
    btn.title = text;
    btn.setAttribute('aria-label', text);
    btn.querySelector('i').className = 'icon-base ti ' + (open ? 'tabler-lock-open' : 'tabler-lock');
    if (open) { input.focus(); input.select(); } else refreshLabelPreview();
  });
  // Vẽ lại xem trước khi gõ xong (rời ô / Enter / khoá lại), chỉ khi giá trị khác lần vẽ trước
  function refreshLabelPreview() {
    const sig = JSON.stringify(lbl.values);
    if (sig === lbl.previewSig) return;
    lbl.previewSig = sig;
    preview.refresh();
  }
  el.lblVars.addEventListener('change', function (e) { if (e.target.dataset.lblVar) refreshLabelPreview(); });
  el.lblVars.addEventListener('keydown', function (e) {
    // Enter vẫn gửi form (in tem) như trước, chỉ vẽ lại xem trước luôn
    if (e.key === 'Enter' && e.target.dataset.lblVar) refreshLabelPreview();
  });
  // Gõ giá trị: đánh dấu nhập tay (giữ khi đổi mẫu / WO); xem trước vẽ lại khi gõ xong (refreshLabelPreview)
  el.lblVars.addEventListener('input', function (e) {
    const key = e.target.dataset.lblVar;
    if (!key) return;
    lbl.values[key] = e.target.value;
    lbl.typed[key] = true;
    const v = labelVars().find(function (x) { return x.key === key; });
    e.target.classList.toggle('is-invalid', !!v && !v.optional && !e.target.value.trim());
    const badge = e.target.closest('tr').querySelector('.badge');
    if (badge) { badge.className = 'badge bg-label-warning'; badge.textContent = 'nhập tay'; }
    updateLabelPrintable();
  });
  // Bấm ra ngoài thì đưa con trỏ về ô quét (máy quét gõ như bàn phím)
  document.addEventListener('click', function (e) {
    if (!e.target.closest('input, select, button, textarea, .modal, a, label')) focusScan();
  });

  // Kiểm tra mạng: ngay khi mở trang, rồi mỗi 10 giây (mất mạng: 30 giây). Trình duyệt báo có mạng lại (cắm dây / wifi) thì ping ngay
  let pingTimer = null;
  async function pingLoop() {
    clearTimeout(pingTimer);
    await ping();
    pingTimer = setTimeout(pingLoop, net.online === false ? PING_OFFLINE_MS : PING_MS);
  }
  window.addEventListener('online', pingLoop);
  pingLoop();

  // Có WO đang làm (refresh / mất điện) thì mở lại modal để xác nhận lại.
  showPrinterName();
  updateScanState();
  const savedWo = storage(WO_KEY);
  if (savedWo) openWo(savedWo); else el.wo.focus();
})();
