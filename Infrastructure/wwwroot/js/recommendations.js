'use strict';
document.addEventListener('DOMContentLoaded', async () => {
    const form = document.getElementById('recommendation-form');
    if (!form) return;
    const get = id => document.getElementById(id);
    const status = get('rec-status'), results = get('rec-results');
    const appendOptions = (id, values, name, value) => values.forEach(item => {
        const option = document.createElement('option'); option.textContent = name(item); option.value = value(item); get(id).append(option);
    });
    try {
        const response = await fetch('/api/recommendations/options');
        if (!response.ok) throw new Error();
        const data = await response.json();
        appendOptions('rec-category', data.categories, c => c.name, c => c.id);
        appendOptions('rec-vehicle', data.vehicles, v => [v.brand, v.model, v.variant, v.market].filter(Boolean).join(' '), v => v.id);
        appendOptions('rec-brand', data.brands, x => x, x => x);
        appendOptions('rec-use', data.useCases, x => x, x => x);
    } catch { status.textContent = 'Không tải được lựa chọn. Vui lòng thử lại sau.'; return; }
    form.addEventListener('submit', async event => {
        event.preventDefault(); results.replaceChildren(); status.textContent = 'Đang tìm sản phẩm…';
        const numeric = id => get(id).value === '' ? null : Number(get(id).value);
        const request = { query: get('rec-query').value.trim(), categoryId: numeric('rec-category'), useCase: get('rec-use').value,
            brand: get('rec-brand').value, vehicleModelId: numeric('rec-vehicle'), year: numeric('rec-year'), minPrice: numeric('rec-min'), maxPrice: numeric('rec-max'), preferLowPrice: get('rec-cheap').checked, limit: 6 };
        try {
            const response = await fetch('/api/recommendations', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request) });
            if (!response.ok) throw new Error();
            const data = await response.json();
            status.textContent = data.status === 'Ready' ? `Tìm thấy ${data.items.length} sản phẩm.` : data.clarification;
            for (const item of data.items) {
                const card = document.createElement('article'); card.className = 'product-card';
                const link = document.createElement('a'); link.href = '/Product/' + encodeURIComponent(item.id); link.className = 'product-card-image';
                const image = document.createElement('img'); image.src = '/uploads/products/' + item.image; image.alt = item.name; image.loading = 'lazy';
                image.onerror = () => { image.onerror = null; image.src = '/images/motoshop-placeholder.svg'; };
                link.append(image); const body = document.createElement('div'); body.className = 'product-card-body';
                const title = document.createElement('h3'); title.textContent = item.name;
                const price = document.createElement('p'); price.className = 'price'; price.textContent = new Intl.NumberFormat('vi-VN').format(item.price) + ' ₫';
                const reason = document.createElement('p'); reason.textContent = [`Còn hàng (${item.quantity})`, item.compatibility, ...item.reasons].join(' · ');
                body.append(title, price, reason);
                if (item.isSample) { const sample = document.createElement('p'); sample.textContent = 'Giá và tồn kho phục vụ kiểm thử.'; body.append(sample); }
                card.append(link, body); results.append(card);
            }
        } catch { status.textContent = 'Chưa tải được gợi ý. Vui lòng thử lại.'; }
    });
    form.requestSubmit();
});
