/**
 * Main
 */

'use strict';

window.isRtl = window.Helpers.isRtl();
window.isDarkStyle = window.Helpers.isDarkStyle();
let menu,
  animate,
  isHorizontalLayout = false;

if (document.getElementById('layout-menu')) {
  isHorizontalLayout = document.getElementById('layout-menu').classList.contains('menu-horizontal');
}
document.addEventListener('DOMContentLoaded', function () {
  // class for ios specific styles
  if (navigator.userAgent.match(/iPhone|iPad|iPod/i)) {
    document.body.classList.add('ios');
  }
});

(function () {
  // Window scroll function for navbar
  function onScroll() {
    var layoutPage = document.querySelector('.layout-page');
    if (layoutPage) {
      if (window.scrollY > 0) {
        layoutPage.classList.add('window-scrolled');
      } else {
        layoutPage.classList.remove('window-scrolled');
      }
    }
  }
  // On load time out
  setTimeout(() => {
    onScroll();
  }, 200);

  // On window scroll
  window.onscroll = function () {
    onScroll();
  };

  setTimeout(function () {
    window.Helpers.initCustomOptionCheck();
  }, 1000);

  // To remove russian country specific scripts from Sweet Alert 2
  if (
    typeof window !== 'undefined' &&
    /^ru\b/.test(navigator.language) &&
    location.host.match(/\.(ru|su|by|xn--p1ai)$/)
  ) {
    localStorage.removeItem('swal-initiation');

    document.body.style.pointerEvents = 'system';
    setInterval(() => {
      if (document.body.style.pointerEvents === 'none') {
        document.body.style.pointerEvents = 'system';
      }
    }, 100);
    HTMLAudioElement.prototype.play = function () {
      return Promise.resolve();
    };
  }

  if (typeof Waves !== 'undefined') {
    Waves.init();
    Waves.attach(
      ".btn[class*='btn-']:not(.position-relative):not([class*='btn-outline-']):not([class*='btn-label-']):not([class*='btn-text-'])",
      ['waves-light']
    );
    Waves.attach("[class*='btn-outline-']:not(.position-relative)");
    Waves.attach("[class*='btn-label-']:not(.position-relative)");
    Waves.attach("[class*='btn-text-']:not(.position-relative)");
    Waves.attach('.pagination:not([class*="pagination-outline-"]) .page-item.active .page-link', ['waves-light']);
    Waves.attach('.pagination .page-item .page-link');
    Waves.attach('.dropdown-menu .dropdown-item');
    Waves.attach('[data-bs-theme="light"] .list-group .list-group-item-action');
    Waves.attach('[data-bs-theme="dark"] .list-group .list-group-item-action', ['waves-light']);
    Waves.attach('.nav-tabs:not(.nav-tabs-widget) .nav-item .nav-link');
    Waves.attach('.nav-pills .nav-item .nav-link', ['waves-light']);
  }

  // Initialize menu
  //-----------------

  let layoutMenuEl = document.querySelectorAll('#layout-menu');
  layoutMenuEl.forEach(function (element) {
    menu = new Menu(element, {
      orientation: isHorizontalLayout ? 'horizontal' : 'vertical',
      closeChildren: isHorizontalLayout ? true : false,
      // ? This option only works with Horizontal menu
      showDropdownOnHover: localStorage.getItem('templateCustomizer-' + templateName + '--ShowDropdownOnHover') // If value(showDropdownOnHover) is set in local storage
        ? localStorage.getItem('templateCustomizer-' + templateName + '--ShowDropdownOnHover') === 'true' // Use the local storage value
        : window.templateCustomizer !== undefined // If value is set in config.js
          ? window.templateCustomizer.settings.defaultShowDropdownOnHover // Use the config.js value
          : true // Use this if you are not using the config.js and want to set value directly from here
    });
    // Change parameter to true if you want scroll animation
    window.Helpers.scrollToActive((animate = false));
    window.Helpers.mainMenu = menu;
  });

  // Initialize menu togglers and bind click on each
  let menuToggler = document.querySelectorAll('.layout-menu-toggle');
  menuToggler.forEach(item => {
    item.addEventListener('click', event => {
      event.preventDefault();
      window.Helpers.toggleCollapsed();
      // Enable menu state with local storage support if enableMenuLocalStorage = true from config.js
      if (config.enableMenuLocalStorage && !window.Helpers.isSmallScreen()) {
        try {
          localStorage.setItem(
            'templateCustomizer-' + templateName + '--LayoutCollapsed',
            String(window.Helpers.isCollapsed())
          );
          // Update customizer checkbox state on click of menu toggler
          let layoutCollapsedCustomizerOptions = document.querySelector('.template-customizer-layouts-options');
          if (layoutCollapsedCustomizerOptions) {
            let layoutCollapsedVal = window.Helpers.isCollapsed() ? 'collapsed' : 'expanded';
            layoutCollapsedCustomizerOptions.querySelector(`input[value="${layoutCollapsedVal}"]`).click();
          }
        } catch (e) {}
      }
    });
  });

  // Menu swipe gesture

  // Detect swipe gesture on the target element and call swipe In
  window.Helpers.swipeIn('.drag-target', function (e) {
    window.Helpers.setCollapsed(false);
  });

  // Detect swipe gesture on the target element and call swipe Out
  window.Helpers.swipeOut('#layout-menu', function (e) {
    if (window.Helpers.isSmallScreen()) window.Helpers.setCollapsed(true);
  });

  // Display in main menu when menu scrolls
  let menuInnerContainer = document.getElementsByClassName('menu-inner'),
    menuInnerShadow = document.getElementsByClassName('menu-inner-shadow')[0];
  if (menuInnerContainer.length > 0 && menuInnerShadow) {
    menuInnerContainer[0].addEventListener('ps-scroll-y', function () {
      if (this.querySelector('.ps__thumb-y').offsetTop) {
        menuInnerShadow.style.display = 'block';
      } else {
        menuInnerShadow.style.display = 'none';
      }
    });
  }

  // Get style from local storage or use 'system' as default
  let storedStyle =
    localStorage.getItem('templateCustomizer-' + templateName + '--Theme') || // if no template style then use Customizer style
    (window.templateCustomizer?.settings?.defaultStyle ?? document.documentElement.getAttribute('data-bs-theme')); //!if there is no Customizer then use default style as light

  // Run switchImage function based on the stored style
  window.Helpers.switchImage(storedStyle);

  // Update light/dark image based on current style
  window.Helpers.setTheme(window.Helpers.getPreferredTheme());

  window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
    const storedTheme = window.Helpers.getStoredTheme();
    if (storedTheme !== 'light' && storedTheme !== 'dark') {
      window.Helpers.setTheme(window.Helpers.getPreferredTheme());
    }
  });

  function getScrollbarWidth() {
    const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
    document.body.style.setProperty('--bs-scrollbar-width', `${scrollbarWidth}px`);
  }
  getScrollbarWidth();
  window.addEventListener('DOMContentLoaded', () => {
    window.Helpers.showActiveTheme(window.Helpers.getPreferredTheme());
    getScrollbarWidth();
    // Toggle Universal Sidebar
    window.Helpers.initSidebarToggle();
    document.querySelectorAll('[data-bs-theme-value]').forEach(toggle => {
      toggle.addEventListener('click', () => {
        const theme = toggle.getAttribute('data-bs-theme-value');
        window.Helpers.setStoredTheme(templateName, theme);
        window.Helpers.setTheme(theme);
        window.Helpers.showActiveTheme(theme, true);
        window.Helpers.syncCustomOptions(theme);
        let currTheme = theme;
        if (theme === 'system') {
          currTheme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        }
        const semiDarkL = document.querySelector('.template-customizer-semiDark');
        if (semiDarkL) {
          if (theme === 'dark') {
            semiDarkL.classList.add('d-none');
          } else {
            semiDarkL.classList.remove('d-none');
          }
        }
        window.Helpers.switchImage(currTheme);
      });
    });
  });

  // Internationalization (Language Dropdown)
  // ---------------------------------------

  if (typeof i18next !== 'undefined' && typeof i18NextHttpBackend !== 'undefined') {
    i18next
      .use(i18NextHttpBackend)
      .init({
        lng: window.templateCustomizer ? window.templateCustomizer.settings.lang : 'en',
        debug: false,
        fallbackLng: 'en',
        backend: {
          loadPath: assetsPath + 'json/locales/{{lng}}.json'
        },
        returnObjects: true
      })
      .then(function (t) {
        localize();
      });
  }

  let languageDropdown = document.getElementsByClassName('dropdown-language');

  if (languageDropdown.length) {
    let dropdownItems = languageDropdown[0].querySelectorAll('.dropdown-item');

    for (let i = 0; i < dropdownItems.length; i++) {
      dropdownItems[i].addEventListener('click', function () {
        let currentLanguage = this.getAttribute('data-language');
        let textDirection = this.getAttribute('data-text-direction');

        for (let sibling of this.parentNode.children) {
          var siblingEle = sibling.parentElement.parentNode.firstChild;

          // Loop through each sibling and push to the array
          while (siblingEle) {
            if (siblingEle.nodeType === 1 && siblingEle !== siblingEle.parentElement) {
              siblingEle.querySelector('.dropdown-item').classList.remove('active');
            }
            siblingEle = siblingEle.nextSibling;
          }
        }
        this.classList.add('active');

        i18next.changeLanguage(currentLanguage, (err, t) => {
          window.templateCustomizer ? window.templateCustomizer.setLang(currentLanguage) : '';
          directionChange(textDirection);
          if (err) return console.log('something went wrong loading', err);
          localize();
          window.Helpers.syncCustomOptionsRtl(textDirection);
        });
      });
    }
    function directionChange(textDirection) {
      document.documentElement.setAttribute('dir', textDirection);
      if (textDirection === 'rtl') {
        if (localStorage.getItem('templateCustomizer-' + templateName + '--Rtl') !== 'true')
          window.templateCustomizer ? window.templateCustomizer.setRtl(true) : '';
      } else {
        if (localStorage.getItem('templateCustomizer-' + templateName + '--Rtl') === 'true')
          window.templateCustomizer ? window.templateCustomizer.setRtl(false) : '';
      }
    }
  }

  function localize() {
    let i18nList = document.querySelectorAll('[data-i18n]');
    // Set the current language in dd
    let currentLanguageEle = document.querySelector('.dropdown-item[data-language="' + i18next.language + '"]');

    if (currentLanguageEle) {
      currentLanguageEle.click();
    }

    i18nList.forEach(function (item) {
      item.innerHTML = i18next.t(item.dataset.i18n);
      /* FIX: Uncomment the following line to hide elements with the i18n attribute before translation to prevent text change flicker */
      // item.style.visibility = 'visible';
    });
  }

  // Notification
  // ------------
  const notificationMarkAsReadAll = document.querySelector('.dropdown-notifications-all');
  const notificationMarkAsReadList = document.querySelectorAll('.dropdown-notifications-read');

  // Notification: Mark as all as read
  if (notificationMarkAsReadAll) {
    notificationMarkAsReadAll.addEventListener('click', event => {
      notificationMarkAsReadList.forEach(item => {
        item.closest('.dropdown-notifications-item').classList.add('marked-as-read');
      });
    });
  }
  // Notification: Mark as read/unread onclick of dot
  if (notificationMarkAsReadList) {
    notificationMarkAsReadList.forEach(item => {
      item.addEventListener('click', event => {
        item.closest('.dropdown-notifications-item').classList.toggle('marked-as-read');
      });
    });
  }

  // Notification: Mark as read/unread onclick of dot
  const notificationArchiveMessageList = document.querySelectorAll('.dropdown-notifications-archive');
  notificationArchiveMessageList.forEach(item => {
    item.addEventListener('click', event => {
      item.closest('.dropdown-notifications-item').remove();
    });
  });

  // Init helpers & misc
  // --------------------

  // Init BS Tooltip
  const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
  tooltipTriggerList.map(function (tooltipTriggerEl) {
    return new bootstrap.Tooltip(tooltipTriggerEl);
  });

  // Accordion active class
  const accordionActiveFunction = function (e) {
    if (e.type == 'show.bs.collapse' || e.type == 'show.bs.collapse') {
      e.target.closest('.accordion-item').classList.add('active');
    } else {
      e.target.closest('.accordion-item').classList.remove('active');
    }
  };

  const accordionTriggerList = [].slice.call(document.querySelectorAll('.accordion'));
  const accordionList = accordionTriggerList.map(function (accordionTriggerEl) {
    accordionTriggerEl.addEventListener('show.bs.collapse', accordionActiveFunction);
    accordionTriggerEl.addEventListener('hide.bs.collapse', accordionActiveFunction);
  });

  // Auto update layout based on screen size
  window.Helpers.setAutoUpdate(true);

  // Toggle Password Visibility
  window.Helpers.initPasswordToggle();

  // Speech To Text
  window.Helpers.initSpeechToText();

  // Init PerfectScrollbar in Navbar Dropdown (i.e notification)
  window.Helpers.initNavbarDropdownScrollbar();

  let horizontalMenuTemplate = document.querySelector("[data-template^='horizontal-menu']");
  if (horizontalMenuTemplate) {
    // if screen size is small then set navbar fixed
    if (window.innerWidth < window.Helpers.LAYOUT_BREAKPOINT) {
      window.Helpers.setNavbarFixed('fixed');
    } else {
      window.Helpers.setNavbarFixed('');
    }
  }

  // On window resize listener
  // -------------------------
  window.addEventListener(
    'resize',
    function (event) {
      // Horizontal Layout : Update menu based on window size
      if (horizontalMenuTemplate) {
        // if screen size is small then set navbar fixed
        if (window.innerWidth < window.Helpers.LAYOUT_BREAKPOINT) {
          window.Helpers.setNavbarFixed('fixed');
        } else {
          window.Helpers.setNavbarFixed('');
        }
        setTimeout(function () {
          if (window.innerWidth < window.Helpers.LAYOUT_BREAKPOINT) {
            if (document.getElementById('layout-menu')) {
              if (document.getElementById('layout-menu').classList.contains('menu-horizontal')) {
                menu.switchMenu('vertical');
              }
            }
          } else {
            if (document.getElementById('layout-menu')) {
              if (document.getElementById('layout-menu').classList.contains('menu-vertical')) {
                menu.switchMenu('horizontal');
              }
            }
          }
        }, 100);
      }
    },
    true
  );

  // Manage menu expanded/collapsed with templateCustomizer & local storage
  //------------------------------------------------------------------

  // If current layout is horizontal OR current window screen is small (overlay menu) than return from here
  if (isHorizontalLayout || window.Helpers.isSmallScreen()) {
    return;
  }

  // If current layout is vertical and current window screen is > small
  // Auto update menu collapsed/expanded based on the themeConfig
  if (typeof window.templateCustomizer !== 'undefined') {
    if (window.templateCustomizer.settings.defaultMenuCollapsed) {
      window.Helpers.setCollapsed(true, false);
    } else {
      window.Helpers.setCollapsed(false, false);
    }

    if (window.templateCustomizer.settings.semiDark) {
      document.querySelector('#layout-menu').setAttribute('data-bs-theme', 'dark');
    }
  }

  // Manage menu expanded/collapsed state with local storage support If enableMenuLocalStorage = true in config.js
  if (typeof config !== 'undefined') {
    if (config.enableMenuLocalStorage) {
      try {
        if (localStorage.getItem('templateCustomizer-' + templateName + '--LayoutCollapsed') !== null)
          window.Helpers.setCollapsed(
            localStorage.getItem('templateCustomizer-' + templateName + '--LayoutCollapsed') === 'true',
            false
          );
      } catch (e) {}
    }
  }
})();

// Search Configuration
const SearchConfig = {
  container: '#autocomplete',
  placeholder: 'Search [CTRL + K]',
  classNames: {
    detachedContainer: 'd-flex flex-column',
    detachedFormContainer: 'd-flex align-items-center justify-content-between border-bottom',
    form: 'd-flex align-items-center',
    input: 'search-control border-none',
    detachedCancelButton: 'btn-search-close',
    panel: 'flex-grow content-wrapper overflow-hidden position-relative',
    panelLayout: 'h-100',
    clearButton: 'd-none',
    item: 'd-block'
  }
};

// Search state and data
let data = {};
let currentFocusIndex = -1;

// Utils
function isMacOS() {
  return /Mac|iPod|iPhone|iPad/.test(navigator.userAgent);
}

// Load search data
function loadSearchData() {
  const searchJson = $('#layout-menu').hasClass('menu-horizontal') ? 'search-horizontal.json' : 'search-vertical.json';

  fetch(assetsPath + 'json/' + searchJson)
    .then(response => {
      if (!response.ok) throw new Error('Failed to fetch data');
      return response.json();
    })
    .then(json => {
      data = json;
      initializeAutocomplete();
    })
    .catch(error => console.error('Error loading JSON:', error));
}

// Initialize autocomplete
function initializeAutocomplete() {
  const searchElement = document.getElementById('autocomplete');
  if (!searchElement) return;

  return autocomplete({
    ...SearchConfig,
    openOnFocus: true,
    onStateChange({ state, setQuery }) {
      // When autocomplete is opened
      if (state.isOpen) {
        // Hide body scroll and add padding to prevent layout shift
        document.body.style.overflow = 'hidden';
        document.body.style.paddingRight = 'var(--bs-scrollbar-width)';
        // Replace "Cancel" text with icon
        const cancelIcon = document.querySelector('.aa-DetachedCancelButton');
        if (cancelIcon) {
          cancelIcon.innerHTML =
            '<span class="text-body-secondary">[esc]</span> <span class="icon-base icon-md ti tabler-x text-heading"></span>';
        }

        // Perfect Scrollbar
        if (!window.autoCompletePS) {
          const panel = document.querySelector('.aa-Panel');
          if (panel) {
            window.autoCompletePS = new PerfectScrollbar(panel);
          }
        }
      } else {
        // When autocomplete is closed
        if (state.status === 'idle' && state.query) {
          setQuery('');
        }

        // Restore body scroll and padding when autocomplete is closed
        document.body.style.overflow = 'auto';
        document.body.style.paddingRight = '';
      }
    },
    render(args, root) {
      const { render, html, children, state } = args;

      // Initial Suggestions
      if (!state.query) {
        const initialSuggestions = html`
          <div class="p-5 p-lg-12">
            <div class="row g-4">
              ${Object.entries(data.suggestions || {}).map(
                ([section, items]) => html`
                  <div class="col-md-6 suggestion-section">
                    <p class="search-headings mb-2">${section}</p>
                    <div class="suggestion-items">
                      ${items.map(
                        item => html`
                          <a href="${item.url}" class="suggestion-item d-flex align-items-center">
                            <i class="icon-base ti ${item.icon}"></i>
                            <span>${item.name}</span>
                          </a>
                        `
                      )}
                    </div>
                  </div>
                `
              )}
            </div>
          </div>
        `;

        render(initialSuggestions, root);
        return;
      }

      // No items
      if (!args.sections.length) {
        render(
          html`
            <div class="search-no-results-wrapper">
              <div class="d-flex justify-content-center align-items-center h-100">
                <div class="text-center text-heading">
                  <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 24 24">
                    <g
                      fill="none"
                      stroke="currentColor"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      stroke-width="0.6">
                      <path d="M14 3v4a1 1 0 0 0 1 1h4" />
                      <path d="M17 21H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h7l5 5v11a2 2 0 0 1-2 2m-5-4h.01M12 11v3" />
                    </g>
                  </svg>
                  <h5 class="mt-2">No results found</h5>
                </div>
              </div>
            </div>
          `,
          root
        );
        return;
      }

      render(children, root);
      window.autoCompletePS?.update();
    },
    getSources() {
      const sources = [];

      // Add navigation sources if available
      if (data.navigation) {
        // Add other navigation sources first
        const navigationSources = Object.keys(data.navigation)
          .filter(section => section !== 'files' && section !== 'members')
          .map(section => ({
            sourceId: `nav-${section}`,
            getItems({ query }) {
              const items = data.navigation[section];
              if (!query) return items;
              return items.filter(item => item.name.toLowerCase().includes(query.toLowerCase()));
            },
            getItemUrl({ item }) {
              return item.url;
            },
            templates: {
              header({ items, html }) {
                if (items.length === 0) return null;
                return html`<span class="search-headings">${section}</span>`;
              },
              item({ item, html }) {
                return html`
                  <a href="${item.url}" class="d-flex justify-content-between align-items-center">
                    <span class="item-wrapper"><i class="icon-base ti ${item.icon}"></i>${item.name}</span>
                    <svg xmlns="http://www.w3.org/2000/svg" width="20px" height="20px" viewBox="0 0 24 24">
                      <g
                        fill="none"
                        stroke="currentColor"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="1.8"
                        color="currentColor">
                        <path d="M11 6h4.5a4.5 4.5 0 1 1 0 9H4" />
                        <path d="M7 12s-3 2.21-3 3s3 3 3 3" />
                      </g>
                    </svg>
                  </a>
                `;
              }
            }
          }));
        sources.push(...navigationSources);

        // Add Files source second
        if (data.navigation.files) {
          sources.push({
            sourceId: 'files',
            getItems({ query }) {
              const items = data.navigation.files;
              if (!query) return items;
              return items.filter(item => item.name.toLowerCase().includes(query.toLowerCase()));
            },
            getItemUrl({ item }) {
              return item.url;
            },
            templates: {
              header({ items, html }) {
                if (items.length === 0) return null;
                return html`<span class="search-headings">Files</span>`;
              },
              item({ item, html }) {
                return html`
                  <a href="${item.url}" class="d-flex align-items-center position-relative px-4 py-2">
                    <div class="file-preview me-2">
                      <img src="${assetsPath}${item.src}" alt="${item.name}" class="rounded" width="42" />
                    </div>
                    <div class="flex-grow-1">
                      <h6 class="mb-0">${item.name}</h6>
                      <small class="text-body-secondary">${item.subtitle}</small>
                    </div>
                    ${item.meta
                      ? html`
                          <div class="position-absolute end-0 me-4">
                            <span class="text-body-secondary small">${item.meta}</span>
                          </div>
                        `
                      : ''}
                  </a>
                `;
              }
            }
          });
        }

        // Add Members source last
        if (data.navigation.members) {
          sources.push({
            sourceId: 'members',
            getItems({ query }) {
              const items = data.navigation.members;
              if (!query) return items;
              return items.filter(item => item.name.toLowerCase().includes(query.toLowerCase()));
            },
            getItemUrl({ item }) {
              return item.url;
            },
            templates: {
              header({ items, html }) {
                if (items.length === 0) return null;
                return html`<span class="search-headings">Members</span>`;
              },
              item({ item, html }) {
                return html`
                  <a href="${item.url}" class="d-flex align-items-center py-2 px-4">
                    <div class="avatar me-2">
                      <img src="${assetsPath}${item.src}" alt="${item.name}" class="rounded-circle" width="32" />
                    </div>
                    <div class="flex-grow-1">
                      <h6 class="mb-0">${item.name}</h6>
                      <small class="text-body-secondary">${item.subtitle}</small>
                    </div>
                  </a>
                `;
              }
            }
          });
        }
      }

      return sources;
    }
  });
}

// Initialize search shortcut
document.addEventListener('keydown', event => {
  if ((event.ctrlKey || event.metaKey) && event.key === 'k') {
    event.preventDefault();
    document.querySelector('.aa-DetachedSearchButton').click();
  }
});

// Load search data on page load
if (document.documentElement.querySelector('#autocomplete')) {
  loadSearchData();
}

class CustomNotyf extends Notyf {
  _renderNotification(options) {
    const notification = super._renderNotification(options);

    // Replace textContent with innerHTML to render HTML content
    if (options.message) {
      notification.message.innerHTML = options.message;
    }

    return notification;
  }
}

const notyf = new CustomNotyf({
  duration: 3000,
  ripple: true,
  dismissible: false,
  position: { x: 'right', y: 'top' },
  types: [
    {
      type: 'info',
      background: config.colors.info,
      className: 'notyf__info',
      icon: {
        className: 'icon-base ti tabler-info-circle-filled icon-md text-white',
        tagName: 'i'
      }
    },
    {
      type: 'warning',
      background: config.colors.warning,
      className: 'notyf__warning',
      icon: {
        className: 'icon-base ti tabler-alert-triangle-filled icon-md text-white',
        tagName: 'i'
      }
    },
    {
      type: 'success',
      background: config.colors.success,
      className: 'notyf__success',
      icon: {
        className: 'icon-base ti tabler-circle-check-filled icon-md text-white',
        tagName: 'i'
      }
    },
    {
      type: 'error',
      background: config.colors.danger,
      className: 'notyf__error',
      icon: {
        className: 'icon-base ti tabler-xbox-x-filled icon-md text-white',
        tagName: 'i'
      }
    }
  ]
});

$(document).ajaxError(function(event, jqxhr, settings, thrownError) {
  // Gọi /api/*: màn hình tự báo lỗi bằng message server trả về — không bật thêm toast chung (tránh báo 2 lần).
  if (settings && /(^|\/)api\//.test(String(settings.url || '').replace(/^https?:\/\/[^\/]+/, ''))) {
    return;
  }
  // Kiểm tra nếu lỗi là 403 (Forbidden)
  if (jqxhr.status == 403) {
    const notificationOptions = {
      type: 'error',
      message: 'Bạn không có quyền truy cập vào chức năng này.',
      duration: 4000,
      dismissible: false,
      ripple: true,
      position: { x: 'right', y: 'top' },
    };
    notyf.open(notificationOptions);
  }
  else if (jqxhr.status == 500) {
    const notificationOptions = {
      type: 'warning',
      message: 'Có lỗi trong quá trình xử lý dữ liệu.',
      duration: 4000,
      dismissible: false,
      ripple: true,
      position: { x: 'right', y: 'top' },
    };
    notyf.open(notificationOptions);
  }
  else if (jqxhr.status == 404) {
    const notificationOptions = {
      type: 'warning',
      message: 'Không tìm thấy trang',
      duration: 4000,
      dismissible: false,
      ripple: true,
      position: { x: 'right', y: 'top' },
    };
    notyf.open(notificationOptions);
  }
});
function getCurrentPageFromView(domId) {
  var selector = '.view-dom-id-' + domId;
  var currentPageEl = $(selector).find('.pager .pager-current');
  if (currentPageEl.length > 0) {
    var currentPage = parseInt(currentPageEl.text());
    return isNaN(currentPage) ? 0 : currentPage - 1; // vì page tính từ 0
  }
  return 0;
}
function reloadViewPreservePage(domId) {
  var viewSelector = '.view-dom-id-' + domId;
  var view = $(viewSelector);
  var viewSettings = Drupal.settings.views.ajaxViews;

  for (var key in viewSettings) {
    if (viewSettings[key].view_dom_id === domId) {
      var settings = viewSettings[key];

      // Lấy trang hiện tại
      var currentPage = getCurrentPageFromView(domId);

      var newSubmit = $.extend({}, settings);
      newSubmit.page = currentPage;

      var elementSettings = {
        url: Drupal.settings.views.ajax_path,
        submit: newSubmit,
        setClick: false,
        event: 'RefreshViewManual',
        selector: viewSelector,
        progress: { type: 'throbber' }
      };

      var ajax = new Drupal.ajax(null, view, elementSettings);
      ajax.eventResponse({ type: 'RefreshView' }); // dùng event giả hợp lệ
      // ajax.execute();
      break;
    }
  }
}
//.view.view-quan-ly-don-hang.view-id-quan_ly_don_hang
function refeshCurentPage($classView){
  var classes = $($classView).attr('class');
  var match = classes.match(/view-dom-id-([a-zA-Z0-9]+)/);
  if (match) {
    reloadViewPreservePage(match[1]); // Thay XXXX bằng giá trị thật
  }
}
/**
 * Gắn sự kiện tự động cập nhật datetime vào input ẩn
 * @param {string} dateSelector - ID hoặc class của input type=date
 * @param {string} timeSelector - ID hoặc class của select time
 * @param {string} targetSelector - ID hoặc class của input hidden
 */
function bindDatetimeSelector(dateSelector, timeSelector, targetSelector ) {
  function update() {
    const date = $(dateSelector).val();
    const time = $(timeSelector).val() ;
    if (date && time) {
      $(targetSelector).val(date + ' ' + time );
    } else if (date) {
      $(targetSelector).val(date + ' ' + '00:00' );
    }  else {
      $(targetSelector).val('');
    }
  }

  $(dateSelector + ',' + timeSelector + ',' + targetSelector).on('change', update);
  update(); // Gọi ngay khi khởi tạo
}

function convertDateToUnix(dateStr) {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  return Math.floor(date.getTime() / 1000); // Unix timestamp
}

function convertUnixToDate(timestampStr, format = 'dd/MM/yyyy HH:mm') {
  if (!timestampStr) return '';
  var datetime = parseInt(timestampStr, 10);
  if (datetime < 1000000000000) {
    datetime *= 1000;
  }
  return formatDate(datetime, format);
}


function formatDate(datetime, format) {
  var date = new Date(datetime);
  var day = ('0' + date.getDate()).slice(-2);
  var month = ('0' + (date.getMonth() + 1)).slice(-2); // Tháng bắt đầu từ 0
  var year = date.getFullYear();
  var hours = ('0' + date.getHours()).slice(-2);
  var minutes = ('0' + date.getMinutes()).slice(-2);

  switch (format) {
    case 'dd/MM/yyyy':
      return `${day}/${month}/${year}`;
    case 'dd/MM/yyyy HH:mm':
      return `${day}/${month}/${year} ${hours}:${minutes}`;
    case 'yyyy-MM-dd':
      return `${year}-${month}-${day}`;
    case 'dd-MM-yyyy':
      return `${day}-${month}-${year}`;
    case 'yyyy-MM-dd HH:mm':
      return `${year}-${month}-${day}`;
    case 'yyyy-MM-dd HH:mm:ss':
      return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
    default:
      return `${day}/${month}/${year} ${hours}:${minutes}`;
  }
}

function initializeFlatpickrWithUnixRange( inputId, fromInputId, toInputId){
  const flatpickrRange = document.getElementById(inputId);
  const fromEl = document.getElementById(fromInputId);
  const toEl = document.getElementById(toInputId);

  if (!flatpickrRange || !fromEl || !toEl) return;

  const fromTimestamp = parseInt(fromEl.value);
  const toTimestamp = parseInt(toEl.value);

  let flatpickrOptions = {
    mode: 'range',
    static: true,
    dateFormat: 'd-m-Y',
    appendTo: document.body,
    onChange: function (selectedDates) {
      if (selectedDates.length === 2) {
        const fromDate = selectedDates[0];
        const toDate = selectedDates[1];

        const fromUnix = convertDateToUnix(fromDate);
        const toUnix = convertDateToUnix(toDate);

        fromEl.value = fromUnix;
        toEl.value = toUnix;

        flatpickrRange.value = `${formatDate(fromDate, 'dd/MM/yyyy')} đến ${formatDate(toDate, 'dd/MM/yyyy')}`;
      }
    }
  };

  if (!isNaN(fromTimestamp) && !isNaN(toTimestamp)) {
    const startDate = convertUnixToDate(fromTimestamp);
    const endDate = convertUnixToDate(toTimestamp);

    flatpickrRange.value = `${formatDate(startDate, 'dd/MM/yyyy')} đến ${formatDate(endDate, 'dd/MM/yyyy')}`;
    flatpickrOptions.defaultDate = [startDate, endDate];
  }

  flatpickr(flatpickrRange, flatpickrOptions);
}

function initializeFlatpickr(inputId, withTime = false) {
  const flatpickrDate = document.getElementById(inputId);
  if (flatpickrDate) {
    flatpickr(flatpickrDate, {
      dateFormat: withTime ? 'd-m-Y H:i' : 'd-m-Y',
      enableTime: withTime,
      time_24hr: true,             // Định dạng giờ 24h
      static: true,
      defaultDate: flatpickrDate.value || null,
      allowInput: true
    });
  }
}

function initializeFlatpickr_v2(inputSelect, withTime = false) {
  const flatpickrDate = $(inputSelect);
  if (flatpickrDate) {
    flatpickr(flatpickrDate, {
      dateFormat: withTime ? 'd-m-Y H:i' : 'd-m-Y',
      enableTime: withTime,
      time_24hr: true,             // Định dạng giờ 24h
      static: true,
      defaultDate: flatpickrDate.value || null,
      allowInput: true
    });
  }
}

function applyCurrencyFormat(selector = '.currency') {
  const inputs = document.querySelectorAll(selector);

  inputs.forEach(input => {
    function formatValue() {
      let raw = input.value.replace(/\D/g, '');
      if (raw.length > 0) {
        input.value = Number(raw).toLocaleString('vi-VN');
      } else {
        input.value = '';
      }
    }

    input.addEventListener('keypress', function (e) {
      if (!/[0-9]/.test(e.key)) {
        e.preventDefault();
      }
    });

    input.addEventListener('input', formatValue);
    input.addEventListener('blur', formatValue);

    // 🔹 Format lại nếu có dữ liệu sẵn
    if (input.value.trim() !== '') {
      formatValue();
    }
  });
}
applyCurrencyFormat();
function getRawValue(input) {
  let val = (input.value || '').trim();

  // Nếu có dấu phân cách hàng nghìn → bỏ đi
  val = val.replace(/\./g, '');

  // Nếu có dấu phẩy → đổi thành dấu chấm (chuẩn số thập phân)
  val = val.replace(',', '.');

  // Chỉ giữ lại ký tự số và 1 dấu chấm duy nhất
  val = val.replace(/[^0-9.]/g, '');
  val = val.replace(/(\..*)\./g, '$1'); // bỏ dấu chấm thừa

  // Nếu rỗng → trả về "0"
  return val === '' ? '0' : val;
}
function initSelect2($select, dropdownParent) {
  $select.select2({
    placeholder: $select.data('placeholder') || "Chọn tỉnh/thành phố",
    allowClear: $select.data('allow-clear') === true || $select.data('allow-clear') === "true",
    dropdownParent: dropdownParent || $('body'),
  });
}

function initFormatMoney(selector = '.format-money') {

  /**
   * Format khi người dùng đang nhập:
   * 1234567,89 => 1.234.567,89
   */
  function formatNumber(value) {
    if (value === '' || value === null || value === undefined) {
      return '';
    }

    value = String(value)
      // Chỉ giữ số và dấu phẩy
      .replace(/[^\d,]/g, '')
      // Chỉ giữ một dấu phẩy đầu tiên
      .replace(/(,.*),/g, '$1');

    const parts = value.split(',');

    let integerPart = parts[0].replace(/^0+(?=\d)/, '');
    const decimalPart = parts[1];

    // Cho phép nhập ",5" thành "0,5"
    if (integerPart === '' && decimalPart !== undefined) {
      integerPart = '0';
    }

    // Phân cách hàng nghìn bằng dấu chấm
    integerPart = integerPart.replace(
      /\B(?=(\d{3})+(?!\d))/g,
      '.'
    );

    return decimalPart !== undefined
      ? integerPart + ',' + decimalPart
      : integerPart;
  }

  /**
   * Chuyển định dạng Việt Nam về số chuẩn:
   * 1.234.567,89 => 1234567.89
   */
  function parseNumber(value) {
    if (value === '' || value === null || value === undefined) {
      return '';
    }

    value = String(value).trim();

    // Bỏ dấu chấm phân cách hàng nghìn
    value = value.replace(/\./g, '');

    // Chỉ giữ số và dấu phẩy
    value = value.replace(/[^\d,]/g, '');

    // Chỉ giữ một dấu phẩy
    value = value.replace(/(,.*),/g, '$1');

    // Đổi dấu phẩy thập phân thành dấu chấm chuẩn
    return value.replace(',', '.');
  }

  // Format khi nhập, hỗ trợ cả input được sinh bằng AJAX
  $(document)
    .off('input.formatMoney', selector)
    .on('input.formatMoney', selector, function () {
      this.value = formatNumber(this.value);
    });

  // Khi rời khỏi input, bỏ dấu phẩy cuối
  $(document)
    .off('blur.formatMoney', selector)
    .on('blur.formatMoney', selector, function () {
      this.value = this.value.replace(/,$/, '');
    });

  // Format dữ liệu có sẵn khi load
  $(selector).each(function () {
    const value = $(this).val();

    if (value === '' || value === null || value === undefined) {
      return;
    }

    /*
     * Dữ liệu từ server thường là:
     * 1234567
     * 1234567.89
     *
     * Chuyển dấu chấm thập phân từ server thành dấu phẩy
     * trước khi format.
     */
    let normalizedValue = String(value).trim();

    if (!normalizedValue.includes(',') && normalizedValue.includes('.')) {
      const parts = normalizedValue.split('.');

      // Nếu chỉ có một dấu chấm thì coi là dấu thập phân từ server
      if (parts.length === 2) {
        normalizedValue = parts[0] + ',' + parts[1];
      }
    }

    $(this).val(formatNumber(normalizedValue));
  });

  // Chuyển lại về số chuẩn trước khi submit
  $('form')
    .off('submit.formatMoney')
    .on('submit.formatMoney', function () {
      $(this).find(selector).each(function () {
        let rawValue = parseNumber($(this).val());

        // Bỏ dấu thập phân cuối nếu có
        rawValue = rawValue.replace(/\.$/, '');

        $(this).val(rawValue);
      });
    });
}


/**
 * Chuyển tiền định dạng Việt Nam thành number.
 *
 * 1.234.567,89 => 1234567.89
 */
function parseMoney(value) {
  if (value === '' || value === null || value === undefined) {
    return 0;
  }

  const normalizedValue = String(value)
    .trim()
    .replace(/\./g, '')
    .replace(',', '.')
    .replace(/[^0-9.-]/g, '');

  const number = parseFloat(normalizedValue);

  return isNaN(number) ? 0 : number;
}


/**
 * Chuyển số thành định dạng tiền Việt Nam.
 *
 * 1234567.89 => 1.234.567,89
 */
function formatMoney(value) {
  if (value === '' || value === null || value === undefined) {
    return '0';
  }

  let normalizedValue = String(value).trim();

  /*
   * Nếu giá trị đã ở định dạng Việt Nam:
   * 1.234.567,89
   */
  if (normalizedValue.includes(',')) {
    normalizedValue = normalizedValue
      .replace(/\./g, '')
      .replace(',', '.');
  } else {
    /*
     * Nếu dữ liệu là dạng server:
     * 1234567.89
     */
    normalizedValue = normalizedValue.replace(/,/g, '');
  }

  const number = parseFloat(normalizedValue);

  if (isNaN(number)) {
    return '0';
  }

  return number.toLocaleString('vi-VN', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 20
  });
}


initFormatMoney();

