// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

window.addEventListener("scroll", function () {
    const header = document.getElementById("site-header");
    // Khi cuộn vượt quá 50px, giảm chiều cao header
    if (window.scrollY > 50) {
        header.classList.add("header-shrink");
    } else {
        header.classList.remove("header-shrink");
    }
});


// Menu header
    document.addEventListener('DOMContentLoaded', function() {
    const menuLinks = document.querySelectorAll('.centered-menu li a');
    menuLinks.forEach(function(link) {
      if(link.href === window.location.href) {
        link.classList.add('active');
      }
    });
  });





