'use strict';
document.addEventListener('DOMContentLoaded', () => {
    const sidebar = document.getElementById('admin-sidebar');
    const toggle = document.querySelector('.admin-menu-trigger');
    const scrim = document.querySelector('.admin-scrim');
    let previousFocus;
    const setOpen = open => {
        sidebar?.classList.toggle('is-open', open);
        toggle?.setAttribute('aria-expanded', String(open));
        if (scrim) scrim.hidden = !open;
        if (open) { previousFocus = document.activeElement; sidebar?.querySelector('a')?.focus(); }
        else previousFocus?.focus();
        document.body.style.overflow = open ? 'hidden' : '';
    };
    toggle?.addEventListener('click', () => setOpen(!sidebar?.classList.contains('is-open')));
    scrim?.addEventListener('click', () => setOpen(false));
    document.addEventListener('keydown', event => {
        if (!sidebar?.classList.contains('is-open')) return;
        if (event.key === 'Escape') setOpen(false);
        if (event.key === 'Tab') {
            const links = [...sidebar.querySelectorAll('a,button')];
            const first = links[0], last = links[links.length - 1];
            if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
            else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
        }
    });
    const currentPath = window.location.pathname.toLowerCase();
    document.querySelectorAll('.admin-nav a').forEach(link => {
        const href = link.getAttribute('href').toLowerCase();
        if (href === currentPath || (href === '/product-list' && currentPath.startsWith('/edit/')) || (href === '/bloglist' && currentPath.startsWith('/article'))) link.setAttribute('aria-current', 'page');
    });
    const modal = document.getElementById('confirm-delete');
    document.querySelectorAll('[data-confirm-delete]').forEach(link => link.addEventListener('click', event => {
        if (!modal || !window.bootstrap) return;
        event.preventDefault();
        document.getElementById('confirm-delete-text').textContent = 'Bạn muốn xóa “' + link.dataset.confirmDelete + '”?';
        document.getElementById('confirm-delete-link').href = link.href;
        bootstrap.Modal.getOrCreateInstance(modal).show();
    }));
    document.querySelectorAll('[data-admin-table]').forEach(container => {
        const rows = [...container.querySelectorAll('tbody tr[data-row]')];
        const search = container.querySelector('[data-table-search]');
        const stock = container.querySelector('[data-table-stock]');
        const sort = container.querySelector('[data-table-sort]');
        const count = container.querySelector('[data-table-count]');
        const empty = container.querySelector('[data-table-empty]');
        const previous = container.querySelector('[data-table-previous]');
        const next = container.querySelector('[data-table-next]');
        let page = 1;
        const normalize = text => text.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');
        const render = () => {
            const query = normalize(search?.value || '');
            const matching = rows.filter(row => normalize(row.dataset.search || '').includes(query) && (!stock?.value || (stock.value === 'in' ? Number(row.dataset.stock) > 0 : Number(row.dataset.stock) <= 0)));
            const order = sort?.value || 'original';
            if (order !== 'original') matching.sort((a,b) => order === 'name' ? (a.dataset.name || '').localeCompare(b.dataset.name || '', 'vi') : order === 'price' ? Number(a.dataset.price) - Number(b.dataset.price) : Number(b.dataset.date) - Number(a.dataset.date));
            const pages = Math.max(1, Math.ceil(matching.length / 20));
            page = Math.min(page, pages);
            rows.forEach(row => row.hidden = true);
            matching.forEach((row,index) => { row.parentElement.appendChild(row); row.hidden = index < (page-1)*20 || index >= page*20; });
            if (empty) empty.hidden = matching.length !== 0;
            if (count) count.textContent = matching.length ? ((page-1)*20+1) + '–' + Math.min(page*20,matching.length) + ' / ' + matching.length : '0 kết quả';
            if (previous) previous.disabled = page <= 1;
            if (next) next.disabled = page >= pages;
        };
        [search,stock,sort].forEach(input => input?.addEventListener('input', () => { page = 1; render(); }));
        previous?.addEventListener('click', () => { page--; render(); });
        next?.addEventListener('click', () => { page++; render(); });
        render();
    });
});
