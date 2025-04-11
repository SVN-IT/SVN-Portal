// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
$(document).ready(function () {
    var idleTime = 0;

    // Increment the idle time counter every minute.
    var idleInterval = setInterval(timerIncrement, 60000); // 1 minute

    // Zero the idle timer on mouse movement or key press.
    $(this).mousemove(function (e) {
        idleTime = 0;
    });
    $(this).keypress(function (e) {
        idleTime = 0;
    });

    function timerIncrement() {
        idleTime++;
        if (idleTime >= timeBeforeReload) { // 5 minutes
            location.reload();
        }
    }

    function updateDateTime() {
        var now = new Date();
        var date = now.toLocaleDateString();
        var time = now.toLocaleTimeString();
        document.getElementById('datetime').innerHTML = time;
    }

    setInterval(updateDateTime, 1000); // Cập nhật mỗi giây
    updateDateTime(); // Gọi hàm ngay lập tức để hiển thị thời gian ban đầu

    var opts = {
        angle: 0, // The span of the gauge arc
        lineWidth: 0.3, // The line thickness
        radiusScale: 0.9, // Relative radius
        pointer: {
            length: 0.42, // // Relative to gauge radius
            strokeWidth: 0.029, // The thickness
            color: '#000000' // Fill color
        },
        limitMax: true,     // If false, max value increases automatically if value > maxValue
        limitMin: true,     // If true, the min value of the gauge will be fixed
        colorStart: '#6F6EA0',   // Colors
        colorStop: '#C0C0DB',    // just experiment with them
        strokeColor: '#EEEEEE',  // to see which ones work best for you
        generateGradient: true,
        highDpiSupport: true,     // High resolution support
        // renderTicks is Optional
        // renderTicks: {
        //   divisions: 0,
        //   divWidth: 0.1,
        //   divLength: 0.41,
        //   divColor: '#333333',
        //   subDivisions: 0,
        //   subLength: 0.14,
        //   subWidth: 3.1,
        //   subColor: '#ffffff'
        // },
        staticZones: [
            { strokeStyle: "#F03E3E", min: 0, max: 20 },
            { strokeStyle: "#f29c6a", min: 20, max: 40 },
            { strokeStyle: "#FFDD00", min: 40, max: 60 }, // Red from 70 to 80
            { strokeStyle: "#83f281", min: 60, max: 80 }, // Yellow 80 to 90
            { strokeStyle: "#30B32D", min: 80, max: 100 }, // Green 90 to 100
        ],
        staticLabels: {
            font: "10px sans-serif",  // Specifies font
            labels: [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100],  // Print labels at these values
            color: "#000000",  // Optional: Label text color
            fractionDigits: 0  // Optional: Numerical precision. 0=round off.
        },

    };
    var target = document.getElementById('foo'); // your canvas element
    var gauge = new Gauge(target).setOptions(opts); // create sexy gauge!
    gauge.maxValue = 100; // set max gauge value
    gauge.setMinValue(0);  // Prefer setter over gauge.minValue = 0
    gauge.animationSpeed = 10; // set animation speed (32 is default value)
    gauge.set(92); // set actual value
});
