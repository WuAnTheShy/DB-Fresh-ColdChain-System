// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// 操作提示自动淡出：加载 1 秒后淡出并移除（提现成功、审核成功、登录/注册错误等提示）
document.addEventListener('DOMContentLoaded', function () {
    var autoDismissAlerts = document.querySelectorAll('[data-auto-dismiss]');
    for (var i = 0; i < autoDismissAlerts.length; i++) {
        (function (el) {
            setTimeout(function () {
                el.style.transition = 'opacity 0.6s ease';
                el.style.opacity = '0';
                setTimeout(function () {
                    if (el.parentNode) {
                        el.parentNode.removeChild(el);
                    }
                }, 600);
            }, 1000);
        })(autoDismissAlerts[i]);
    }
});
