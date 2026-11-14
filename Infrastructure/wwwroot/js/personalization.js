'use strict';
(() => {
  const token = () => document.querySelector('#live-chat-form input[name="__RequestVerificationToken"]')?.value || '';
  const post = (url, data) => fetch(url, { method:'POST', credentials:'same-origin', headers:{ 'Content-Type':'application/json', 'RequestVerificationToken':token() }, body:JSON.stringify(data) });
  const render = (container, items) => {
    container.replaceChildren();
    for (const item of items) {
      const card = document.createElement('article'); card.className = 'product-card';
      const link = document.createElement('a'); link.href = '/Product/' + encodeURIComponent(item.id); link.className = 'product-card-image';
      const image = document.createElement('img'); image.src = '/uploads/products/' + item.image; image.alt = item.name; image.loading = 'lazy';
      image.onerror = () => { image.onerror = null; image.src = '/images/motoshop-placeholder.svg'; }; link.append(image);
      const body = document.createElement('div'); body.className = 'product-card-body';
      const title = document.createElement('h3'); title.textContent = item.name;
      const price = document.createElement('p'); price.className = 'price'; price.textContent = new Intl.NumberFormat('vi-VN').format(item.price) + ' ₫';
      const reason = document.createElement('p'); reason.textContent = [`Còn hàng (${item.quantity})`, item.compatibility, ...item.reasons].join(' · ');
      body.append(title, price, reason);
      if (item.isSample) { const note = document.createElement('p'); note.textContent = 'Giá và tồn kho phục vụ kiểm thử.'; body.append(note); }
      card.append(link, body); container.append(card);
    }
  };
  window.MotoPersonalization = { post, render };
  async function loadControls() {
    const controls = document.querySelector('[data-personalization-controls]'); if (!controls) return;
    const status = controls.querySelector('[data-personalization-status]');
    try {
      const response = await fetch('/api/recommendations/preferences'); if (!response.ok) throw new Error();
      const value = await response.json();
      status.textContent = value.enabled ? 'Cá nhân hóa đang bật. Bạn có thể tắt hoặc xóa lịch sử.' : value.chosen ? 'Cá nhân hóa đang tắt.' : 'Bật cá nhân hóa để nhận gợi ý từ sản phẩm bạn quan tâm.';
      controls.querySelector('[data-personalization-enable]').hidden = value.enabled;
      controls.querySelector('[data-personalization-disable]').hidden = !value.enabled;
      controls.querySelector('[data-personalization-clear]').hidden = !value.chosen;
    } catch { status.textContent = 'Chưa tải được tùy chọn cá nhân hóa.'; }
  }
  async function home() {
    const root = document.getElementById('personalized-home'); if (!root) return;
    const status = root.querySelector('[data-home-status]');
    try {
      const response = await fetch('/api/recommendations/personalized?placement=home'); if (!response.ok) throw new Error();
      const data = await response.json();
      root.querySelector('[data-home-title]').textContent = data.mode === 'Personalized' ? 'Dành cho bạn' : 'Khám phá sản phẩm';
      status.textContent = data.status === 'Ready' ? (data.items.length ? '' : 'Chưa có sản phẩm phù hợp.') : data.clarification;
      render(root.querySelector('[data-home-results]'), data.items);
    } catch { status.textContent = 'Chưa tải được gợi ý. Vui lòng thử lại.'; }
  }
  document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-personalization-controls]').forEach(controls => {
      controls.addEventListener('click', async event => {
        const button = event.target.closest('button'); if (!button) return;
        const clear = button.hasAttribute('data-personalization-clear');
        if (clear && !confirm('Xóa lịch sử cá nhân hóa và thông tin xe đã lưu?')) return;
        const enabled = button.hasAttribute('data-personalization-enable');
        const url = clear ? '/api/recommendations/history/delete' : '/api/recommendations/preferences';
        button.disabled = true;
        try { const response = await post(url, clear ? {} : {enabled}); if (!response.ok) throw new Error(); await loadControls(); await home(); document.dispatchEvent(new CustomEvent('personalization:changed', {detail:{clear, enabled}})); }
        catch { controls.querySelector('[data-personalization-status]').textContent = 'Không thể cập nhật. Vui lòng thử lại.'; }
        finally { button.disabled = false; }
      });
    });
    loadControls(); home();
    window.addEventListener('pageshow', event => { if (event.persisted) { loadControls(); home(); } });
  });
})();
