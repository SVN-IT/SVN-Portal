/**
 * =========================================================================
 * Sigma-Tools Unified Client-Side i18n Library
 * =========================================================================
 * - Quản lý ngôn ngữ tập trung qua file JSON (vi.json, en.json, zh.json)
 * - Cung cấp hàm toàn cục t(key, fallback, params) cho JavaScript
 * - Tự động quét và dịch các phần tử HTML (data-i18n, data-key, placeholder)
 * - Tương thích ngược hoàn toàn với hệ thống hiện tại
 */

(function (window, $) {
    'use strict';

    var STORAGE_KEY = 'app_lang';
    var DEFAULT_LANG = 'vi';
    var currentLang = localStorage.getItem(STORAGE_KEY) || DEFAULT_LANG;
    var dictionary = {};
    var isInitialized = false;

    var i18n = {
        /**
         * Lấy mã ngôn ngữ hiện tại ('vi', 'en', 'zh')
         */
        getLanguage: function () {
            return currentLang;
        },

        /**
         * Thay đổi ngôn ngữ
         * @param {string} lang - 'vi', 'en', hoặc 'zh'
         * @param {boolean} [reload=true] - Tải lại trang để áp dụng toàn diện
         */
        setLanguage: function (lang, reload) {
            if (!lang) return;
            localStorage.setItem(STORAGE_KEY, lang);
            currentLang = lang;

            if (reload !== false) {
                window.location.reload();
            } else {
                this.load(lang, function () {
                    i18n.apply();
                });
            }
        },

        /**
         * Tải file từ điển JSON
         */
        load: function (lang, callback) {
            lang = lang || currentLang;
            var basePath = window.__I18N_BASE_PATH__ || '/json/';
            if (!basePath.endsWith('/')) basePath += '/';

            var jsonUrl = basePath + lang + '.json?v=' + new Date().getTime();

            $.getJSON(jsonUrl, function (data) {
                dictionary = data || {};
                isInitialized = true;
                if (typeof callback === 'function') {
                    callback(dictionary);
                }
            }).fail(function () {
                console.warn('[i18n] Could not load dictionary from: ' + jsonUrl);
                if (typeof callback === 'function') {
                    callback({});
                }
            });
        },

        /**
         * Dịch một chuỗi / khóa từ điển
         * @param {string} key - Khóa từ điển
         * @param {string} [fallback] - Giá trị dự phòng nếu không tìm thấy
         * @param {object} [params] - Tham số thay thế {count: 5} -> {count}
         * @returns {string}
         */
        t: function (key, fallback, params) {
            if (!key) return '';
            var text = (dictionary && dictionary[key] !== undefined)
                ? dictionary[key]
                : (fallback !== undefined ? fallback : key);

            // Thay thế tham số {paramName} nếu có
            if (params && typeof params === 'object') {
                Object.keys(params).forEach(function (p) {
                    text = text.replace(new RegExp('\\{' + p + '\\}', 'g'), params[p]);
                });
            }
            return text;
        },

        /**
         * Quét và dịch tự động toàn bộ phần tử trong container
         * @param {string|jQuery} [container=document]
         */
        apply: function (container) {
            var $root = container ? $(container) : $(document);

            // 1. Dịch theo data-i18n
            $root.find('[data-i18n]').each(function () {
                var $el = $(this);
                var key = $el.attr('data-i18n');
                var translated = i18n.t(key);

                var $icon = $el.children('i, svg').clone();
                if ($icon.length > 0) {
                    $el.text(' ' + translated).prepend($icon);
                } else {
                    $el.text(translated);
                }
            });

            // 2. Dịch theo data-key (hỗ trợ trang Login và AccessDenied cũ)
            $root.find('[data-key]').each(function () {
                var $el = $(this);
                var key = $el.attr('data-key');
                if (key) {
                    var translated = i18n.t(key);
                    var $icon = $el.children('i, svg').clone();
                    if ($icon.length > 0) {
                        $el.text(' ' + translated).prepend($icon);
                    } else {
                        $el.text(translated);
                    }
                }
            });

            // 3. Dịch Placeholder input (data-i18n-placeholder hoặc input[placeholder])
            $root.find('[data-i18n-placeholder]').each(function () {
                var $input = $(this);
                var key = $input.attr('data-i18n-placeholder');
                $input.attr('placeholder', i18n.t(key));
            });

            // 4. Dịch các input[placeholder] khớp từ điển
            $root.find('input[placeholder]').each(function () {
                var $input = $(this);
                var orig = $input.data('orig-placeholder') || $input.attr('placeholder');
                if (!$input.data('orig-placeholder') && orig) {
                    $input.data('orig-placeholder', orig);
                }
                if (orig && dictionary[orig]) {
                    $input.attr('placeholder', dictionary[orig]);
                }
            });

            // 5. Dịch title / tooltip
            $root.find('[data-i18n-title]').each(function () {
                var $el = $(this);
                var key = $el.attr('data-i18n-title');
                $el.attr('title', i18n.t(key));
            });

            // 6. Quét các thẻ có class .lang-text hoặc thẻ text thông thường (tương thích ngược)
            $root.find('.lang-text, p, span, a, h1, h2, h3, h4, h5, h6, button, b, strong, .breadcrumb-item').each(function () {
                var $el = $(this);
                if ($el.attr('data-i18n') || $el.attr('data-key')) return; // Đã xử lý ở trên

                var origText = $el.data('orig-text');
                if (!origText) {
                    origText = $el.clone().children().remove().end().text().trim();
                    if (origText) {
                        $el.data('orig-text', origText);
                    }
                }

                if (origText && dictionary[origText]) {
                    if ($el.hasClass('breadcrumb-item') && $el.find('a').length > 0) {
                        return;
                    }
                    var $icon = $el.children('i, svg').clone();
                    if ($icon.length > 0) {
                        $el.text(' ' + dictionary[origText]).prepend($icon);
                    } else if ($el.children().length === 0) {
                        $el.text(dictionary[origText]);
                    }
                }
            });
        },

        /**
         * Khởi tạo thư viện
         * @param {string} [basePath]
         */
        init: function (basePath) {
            if (basePath) {
                window.__I18N_BASE_PATH__ = basePath;
            }
            this.load(currentLang, function () {
                i18n.apply();
            });
        }
    };

    // Đăng ký toàn cục
    window.i18n = i18n;
    window.t = function (key, fallback, params) {
        return i18n.t(key, fallback, params);
    };

    // Hàm alias tương thích với giao diện cũ
    window.setAppLanguage = function (lang, reload) {
        i18n.setLanguage(lang, reload !== false);
    };

})(window, jQuery);
