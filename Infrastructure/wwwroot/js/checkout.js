'use strict';
document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('checkout-form');
    const address = document.getElementById('address');
    if (!form || !address) return;
    form.addEventListener('submit', event => {
        const cod = event.submitter?.id === 'cod-confirm';
        if (cod && !address.value.trim()) {
            event.preventDefault();
            address.setCustomValidity('Vui lòng nhập địa chỉ nhận hàng khi đặt COD.');
            address.reportValidity();
            address.focus();
            return;
        }
        // Cancel a rapid second click without disabling the submitter: its formaction must
        // remain part of the browser's native submission (including when JavaScript is off).
        if (form.dataset.submitting === 'true') { event.preventDefault(); return; }
        form.dataset.submitting = 'true';
        event.submitter?.setAttribute('aria-busy', 'true');
    });
    address.addEventListener('input', () => address.setCustomValidity(''));
    document.getElementById('bank-confirm')?.addEventListener('click', () => address.setCustomValidity(''));
    window.addEventListener('pageshow', () => { delete form.dataset.submitting; form.querySelectorAll('[aria-busy]').forEach(button => button.removeAttribute('aria-busy')); });
});
