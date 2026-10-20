'use strict';
document.addEventListener('DOMContentLoaded', () => {
    const filterToggle = document.querySelector('[data-filter-toggle]');
    const filters = document.querySelector('[data-catalog-filters]');
    filterToggle?.addEventListener('click', () => {
        const visible = filters?.classList.toggle('mobile-collapsed') === false;
        filterToggle.setAttribute('aria-expanded', String(visible));
    });
    const updateButton = document.getElementById('cart-update');
    const quantities = [...document.querySelectorAll('[data-cart-quantity]')];
    const error = document.getElementById('cart-update-error');
    const initial = quantities.map(input => input.value);
    const validate = () => {
        const valid = quantities.every(input => input.checkValidity());
        const changed = quantities.some((input, index) => input.value !== initial[index]);
        if (updateButton) updateButton.disabled = !valid || !changed;
        if (error) error.textContent = valid ? '' : 'Số lượng cần là số nguyên không âm.';
    };
    quantities.forEach(input => input.addEventListener('input', validate));
    validate();
    updateButton?.addEventListener('click', async () => {
        if (!quantities.every(input => input.reportValidity())) return;
        updateButton.disabled = true;
        updateButton.setAttribute('aria-busy', 'true');
        try {
            const response = await fetch('/Cart-Update', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ UpdateItem: quantities.map(input => ({ ProductId: Number(input.dataset.productId), Quantity: Number(input.value) })) }) });
            if (!response.ok || !(await response.json()).success) throw new Error('Update failed');
            window.location.assign('/Cart');
        } catch {
            validate();
            if (error) error.textContent = 'Chưa cập nhật được giỏ hàng. Vui lòng thử lại.';
        } finally { updateButton.removeAttribute('aria-busy'); }
    });
});
