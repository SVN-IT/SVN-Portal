// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
$(document).ready(function () {
    //var idleTime = 0;

    //// Increment the idle time counter every minute.
    //var idleInterval = setInterval(timerIncrement, 60000); // 1 minute

    //// Zero the idle timer on mouse movement or key press.
    //$(this).mousemove(function (e) {
    //    idleTime = 0;
    //});
    //$(this).keypress(function (e) {
    //    idleTime = 0;
    //});

    //function timerIncrement() {
    //    idleTime++;
    //    if (idleTime >= timeBeforeReload) { // 5 minutes
    //        location.reload();
    //    }
    //}

    function updateDateTime() {
        var now = new Date();
        var date = now.toLocaleDateString();
        var time = now.toLocaleTimeString();
        document.getElementById('datetime').innerHTML = time;
    }

    setInterval(updateDateTime, 1000); // Cập nhật mỗi giây
    updateDateTime(); // Gọi hàm ngay lập tức để hiển thị thời gian ban đầu

    
});
