'use strict';
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.notice-dismiss').forEach(button => button.addEventListener('click', () => button.closest('.notice')?.remove()));
    document.querySelectorAll('img[data-fallback-image]').forEach(image => {
        const fallback = () => { if (image.dataset.fallbackApplied) return; image.dataset.fallbackApplied = 'true'; image.src = '/images/motoshop-placeholder.svg'; };
        image.addEventListener('error', fallback);
        if (image.complete && image.naturalWidth === 0) fallback();
    });
    document.querySelectorAll('input[data-image-preview]').forEach(input => {
        let objectUrl;
        input.addEventListener('change', () => {
            const image = document.getElementById(input.dataset.imagePreview);
            const file = input.files?.[0];
            if (!image || !file || !file.type.startsWith('image/')) return;
            if (objectUrl) URL.revokeObjectURL(objectUrl);
            objectUrl = URL.createObjectURL(file);
            image.src = objectUrl;
            image.hidden = false;
        });
    });
});
