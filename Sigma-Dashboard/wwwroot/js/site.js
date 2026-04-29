// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
$(document).ready(function () {
    $('#btnCloseSearchBar').on('click', function () {
        // Chỉ đóng thanh search bằng cách xóa class của AdminLTE
        $('.navbar-search-block').removeClass('navbar-search-open');
        $('.navbar-search-block').css('display', 'none');
    });

    // Đảm bảo khi bấm nút mở (kính lúp) thì hiện lại bình thường
    $('[data-widget="navbar-search"]').on('click', function () {
        $('.navbar-search-block').css('display', 'flex');
    });
});