// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

window.quantityKey = 'productQuantity';
window.apiEndpoint = '/api/get/products';

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




async function updateNewQuantity() {
    if (!localStorage.getItem(quantityKey)) {
        const response = await fetch(apiEndpoint);
        if (response.ok) {
            const products = await response.json();
            let initialQuantity = {}
            products.forEach(product => {
                initialQuantity['Id' + product.id] = product.quantity || 0;
            });
            localStorage.setItem(quantityKey, JSON.stringify(initialQuantity));
        } else {
            console.error('Failed to fetch products from API');
        }
    }
}


window.addEventListener('DOMContentLoaded', updateNewQuantity)




