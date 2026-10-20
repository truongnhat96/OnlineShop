'use strict';

document.addEventListener('DOMContentLoaded', () => {
    let running = 0;
    const queue = [];
    const schedule = work => new Promise((resolve, reject) => {
        queue.push({ work, resolve, reject });
        pump();
    });
    function pump() {
        while (running < 2 && queue.length) {
            const item = queue.shift();
            running++;
            Promise.resolve().then(item.work).then(item.resolve, item.reject).finally(() => { running--; pump(); });
        }
    }
    document.querySelectorAll('textarea[data-rich-editor]').forEach(textarea => {
        const form = textarea.form;
        if (!form) return;
        const $ = window.jQuery;
        const cloud = textarea.dataset.cloudName || '';
        const maxBytes = Number(textarea.dataset.maxImageBytes) || 5242880;
        const error = form.querySelector('#editor-error');
        const submitFeedback = document.createElement('p');
        submitFeedback.dataset.editorSubmitFeedback = '';
        submitFeedback.setAttribute('role', 'alert');
        submitFeedback.setAttribute('tabindex', '-1');
        submitFeedback.hidden = true;
        const actions = form.querySelector('.form-actions');
        if (actions) actions.before(submitFeedback);
        else form.append(submitFeedback);
        let submitError = '';
        const messages = document.createElement('div');
        messages.className = 'editor-image-messages';
        messages.setAttribute('aria-live', 'polite');
        textarea.insertAdjacentElement('afterend', messages);
        const status = document.createElement('p');
        status.className = 'muted mb-2';
        const retry = document.createElement('button');
        retry.type = 'button'; retry.className = 'btn btn-outline btn-sm'; retry.textContent = 'Thử lại ảnh lỗi';
        const remove = document.createElement('button');
        remove.type = 'button'; remove.className = 'btn btn-outline btn-sm ms-2'; remove.textContent = 'Xóa các ảnh lỗi';
        messages.append(status, retry, remove);
        const jobs = new Map();
        const failures = new Map();
        const cache = new Map();
        const objectUrls = new Set();
        const submitButtons = [...form.querySelectorAll('button[type="submit"],input[type="submit"]')];
        let editable;
        let container;
        let observer;
        let active = false;
        let preparing = false;
        let readyToSubmit = false;
        let structuralError = '';
        const hasSummernote = () => Boolean($?.fn.summernote && $(textarea).data('summernote'));
        const isCodeview = () => hasSummernote() && $(textarea).summernote('codeview.isActivated');
        const owned = value => {
            try {
                const url = new URL(value);
                return cloud && url.protocol === 'https:' && url.hostname === 'res.cloudinary.com'
                    && !url.port && !url.username && !url.password && url.pathname.startsWith('/' + encodeURIComponent(cloud) + '/image/upload/');
            } catch { return false; }
        };
        function showSubmitFeedback() {
            submitFeedback.textContent = jobs.size || failures.size ? status.textContent
                : preparing ? 'Đang kiểm tra nội dung trước khi lưu…' : submitError;
            submitFeedback.hidden = !submitFeedback.textContent;
            submitFeedback.className = jobs.size || preparing ? 'muted' : 'inline-error';
        }
        const setError = value => {
            submitError = value;
            if (error) error.textContent = value;
            showSubmitFeedback();
        };
        function validateFields() {
            // Native validation otherwise cancels the click before our submit handler runs.
            const invalid = [...form.elements].find(field => field.willValidate && !field.validity.valid);
            if (invalid) {
                const label = invalid.labels?.[0]?.textContent.trim() || invalid.name;
                throw new Error(invalid.validity.valueMissing ? `Vui lòng nhập hoặc chọn ${label.toLocaleLowerCase('vi')}.` : `${label}: ${invalid.validationMessage}`);
            }
            const validator = $ && $(form).data('validator');
            if (validator && !validator.form())
                throw new Error(validator.errorList.map(item => item.message).join(' ') || 'Vui lòng kiểm tra các trường thông tin trước khi lưu.');
        }
        function renderState() {
            for (const image of failures.keys()) if (!editable?.contains(image)) failures.delete(image);
            const firstFailure = failures.values().next().value;
            status.textContent = jobs.size ? `Đang xử lý ${jobs.size} ảnh…` : failures.size ? `${failures.size} ảnh chưa upload được. ${firstFailure}` : '';
            messages.hidden = !jobs.size && !failures.size;
            retry.hidden = remove.hidden = !failures.size;
            submitButtons.forEach(button => button.disabled = preparing || jobs.size > 0 || failures.size > 0);
            if (structuralError) setError(structuralError);
            showSubmitFeedback();
        }
        async function request(endpoint, body, json = false) {
            const controller = new AbortController();
            const timeout = setTimeout(() => controller.abort(), 65000);
            try {
                const token = form.querySelector('[name="__RequestVerificationToken"]')?.value || '';
                const response = await fetch('/admin/editor-images/' + endpoint, {
                    method: 'POST', credentials: 'same-origin', signal: controller.signal,
                    headers: { RequestVerificationToken: token, ...(json ? { 'Content-Type': 'application/json' } : {}) },
                    body: json ? JSON.stringify(body) : body
                });
                const result = await response.json().catch(() => null);
                if (!response.ok) throw new Error(result?.message || 'Không upload được ảnh. Vui lòng kiểm tra phiên đăng nhập và thử lại.');
                if (!owned(result?.url)) throw new Error('Máy chủ trả về URL ảnh Cloudinary không hợp lệ.');
                return result.url;
            } catch (failure) {
                if (failure.name === 'AbortError') throw new Error('Upload ảnh quá thời gian chờ. Vui lòng thử lại.');
                throw failure;
            } finally { clearTimeout(timeout); }
        }
        async function fileKey(file) {
            const bytes = await file.arrayBuffer();
            const digest = await crypto.subtle.digest('SHA-256', bytes);
            return 'file:' + [...new Uint8Array(digest)].map(byte => byte.toString(16).padStart(2, '0')).join('');
        }
        async function uploadFile(file) {
            if (file.size > maxBytes) throw new Error(`Ảnh vượt quá giới hạn ${maxBytes / 1024 / 1024} MiB.`);
            if (!/^image\/(png|jpe?g|webp|gif)$/i.test(file.type)) throw new Error('Chỉ hỗ trợ ảnh JPEG, PNG, WebP và GIF.');
            const key = await fileKey(file);
            if (!cache.has(key)) {
                const promise = schedule(() => {
                    const body = new FormData(); body.append('file', file, file.name || 'editor-image');
                    return request('upload', body);
                }).catch(failure => { cache.delete(key); throw failure; });
                cache.set(key, promise);
            }
            return cache.get(key);
        }
        async function resolveSource(source, file) {
            if (owned(source)) return source;
            if (file) return uploadFile(file);
            if (/^data:/i.test(source)) {
                const match = /^data:(image\/(?:png|jpe?g|webp|gif));base64,([a-z\d+/=\s]+)$/i.exec(source);
                if (!match) throw new Error('Ảnh Base64 không đúng định dạng được hỗ trợ.');
                if (match[2].replace(/\s/g, '').length > Math.ceil(maxBytes / 3) * 4 + 4) throw new Error('Ảnh Base64 vượt quá dung lượng cho phép.');
                const bytes = Uint8Array.from(atob(match[2].replace(/\s/g, '')), value => value.charCodeAt(0));
                return uploadFile(new File([bytes], 'clipboard.' + match[1].split('/')[1], { type: match[1] }));
            }
            if (source.startsWith('blob:')) {
                const blob = await fetch(source).then(response => { if (!response.ok) throw new Error('Không đọc được ảnh tạm.'); return response.blob(); });
                return uploadFile(new File([blob], 'clipboard-image', { type: blob.type }));
            }
            let url;
            try { url = new URL(source, location.href); } catch { throw new Error('URL ảnh không hợp lệ.'); }
            if (!['https:', 'http:'].includes(url.protocol) || url.username || url.password) throw new Error('Chỉ hỗ trợ URL ảnh HTTP/HTTPS.');
            const key = 'url:' + url.href;
            if (!cache.has(key)) cache.set(key, schedule(() => request('import', { url: url.href }, true)).catch(failure => { cache.delete(key); throw failure; }));
            return cache.get(key);
        }
        function checkMarkup(root) {
            if (root.querySelector('picture:not(:has(img[src])),img:not([src]),svg,object,embed,iframe,video,audio'))
                throw new Error('Nội dung chứa ảnh hoặc media chưa hỗ trợ. Vui lòng chèn lại bằng nút hình ảnh.');
            for (const element of root.querySelectorAll('[style],style')) {
                const css = (element.getAttribute('style') || '') + (element.tagName === 'STYLE' ? element.textContent : '');
                const decoded = css.replace(/\\([a-f\d]{1,6})\s?|\\(.)/gi, (_, hex, char) => hex ? String.fromCodePoint(Math.min(parseInt(hex, 16), 0x10ffff)) : char).replace(/\/\*[\s\S]*?\*\//g, '');
                // Copied articles often carry computed resets such as border-image: initial.
                // These do not reference an image; the server sanitizer drops the properties.
                const imageCss = decoded.replace(/(^|[;{])\s*(?:background-image|border-image|list-style-image|mask-image)\s*:\s*(?:none|initial|inherit|unset|revert(?:-layer)?)\s*(?:!important\s*)?(?=;|}|$)/gi, '$1');
                if (/(?:url|image-set|image|cross-fade|element)\s*\(|(?:background-image|border-image|list-style-image|mask-image)\s*:/i.test(imageCss))
                    throw new Error('Ảnh nền CSS chưa được hỗ trợ. Vui lòng sử dụng nút chèn ảnh.');
            }
            for (const source of root.querySelectorAll('source')) {
                if (source.parentElement.tagName !== 'PICTURE') throw new Error('Nguồn ảnh chưa được hỗ trợ. Vui lòng chèn lại ảnh.');
                source.remove();
            }
            for (const image of root.querySelectorAll('img')) { image.removeAttribute('srcset'); image.removeAttribute('sizes'); }
        }
        function processImage(image, file) {
            const source = image.getAttribute('src') || '';
            if (owned(source) || jobs.has(image) || failures.has(image)) return;
            image.classList.add('editor-image-pending');
            const job = resolveSource(source, file).then(url => {
                if (editable.contains(image) && image.getAttribute('src') === source) {
                    image.setAttribute('src', url);
                    image.removeAttribute('data-editor-upload');
                    image.classList.remove('editor-image-error');
                    $(textarea).summernote('editor.afterCommand');
                }
            }).catch(failure => {
                if (editable.contains(image)) { failures.set(image, failure.message || 'Chưa upload được ảnh.'); image.classList.add('editor-image-error'); }
            }).finally(() => {
                image.classList.remove('editor-image-pending');
                jobs.delete(image);
                renderState();
                if (editable.contains(image) && image.getAttribute('src') !== source) scan();
            });
            jobs.set(image, job);
            renderState();
        }
        function scan() {
            if (!active || !editable || isCodeview()) return;
            try {
                checkMarkup(editable);
                structuralError = '';
                editable.querySelectorAll('img').forEach(image => processImage(image));
            } catch (failure) { structuralError = failure.message; }
            renderState();
        }
        function insertFiles(files) {
            if (isCodeview()) $(textarea).summernote('codeview.deactivate');
            active = true;
            ensureRange();
            [...files].forEach(file => {
                const image = document.createElement('img');
                const url = URL.createObjectURL(file); objectUrls.add(url);
                image.src = url; image.alt = file.name || 'Ảnh đang upload'; image.setAttribute('data-editor-upload', 'pending');
                // Reserve this node before Summernote emits change, so the file callback wins over blob normalization.
                failures.set(image, '');
                $(textarea).summernote('insertNode', image);
                failures.delete(image);
                processImage(image, file);
            });
        }
        function insertUrl(url) {
            if (isCodeview()) $(textarea).summernote('codeview.deactivate');
            active = true;
            ensureRange();
            const image = document.createElement('img'); image.setAttribute('src', url);
            $(textarea).summernote('insertNode', image);
            scan();
        }
        function ensureRange() {
            const selection = window.getSelection();
            if (selection?.rangeCount && editable.contains(selection.anchorNode) && editable.contains(selection.focusNode)) {
                $(textarea).summernote('editor.setLastRange');
                return;
            }
            const saved = $(textarea).summernote('editor.getLastRange');
            if (!saved || !editable.contains(saved.sc) || !editable.contains(saved.ec))
                $(textarea).summernote('editor.setLastRange', $.summernote.range.createFromBodyElement(editable));
        }
        function transfer(event) {
            if (isCodeview()) return; // Raw HTML in codeview is normalized by the final submit pass.
            const data = event.clipboardData || event.dataTransfer;
            if (!data) return;
            const files = [...data.files];
            const html = data.getData('text/html');
            if (!files.length && !html) return;
            event.preventDefault(); event.stopImmediatePropagation(); active = true;
            if (event.type === 'drop') {
                // Capture prevents Summernote inserting the same image a second time, but also
                // bypasses its document drop cleanup. Reset its drag tracking and overlay too.
                const dropzone = $(textarea).data('summernote').modules.dropzone;
                dropzone?.documentEventHandlers?.onDrop?.();
                container.classList.remove('dragover');
                container.querySelector('.note-dropzone')?.classList.remove('hover');
            }
            if (files.length) { insertFiles(files); return; }
            try {
                const document = new DOMParser().parseFromString(html, 'text/html');
                checkMarkup(document.body);
                document.querySelectorAll('script,style,link,meta,form,input,button').forEach(node => node.remove());
                document.querySelectorAll('*').forEach(element => [...element.attributes].forEach(attribute => {
                    if (/^on/i.test(attribute.name) || (attribute.name === 'href' && /^\s*javascript:/i.test(attribute.value))) element.removeAttribute(attribute.name);
                }));
                // One insertion preserves child order even when Summernote splits a mixed block/inline paste range.
                const wrapper = window.document.createElement(document.body.querySelector('p,div,h1,h2,h3,h4,h5,h6,ul,ol,table,blockquote,pre') ? 'div' : 'span');
                wrapper.innerHTML = document.body.innerHTML;
                ensureRange();
                $(textarea).summernote('insertNode', wrapper);
                scan();
            } catch (failure) { setError(failure.message); }
        }
        if ($?.fn.summernote) {
            $(textarea).summernote({
                height: 350, placeholder: textarea.name === 'Content' ? 'Nhập nội dung bài viết…' : 'Nhập mô tả sản phẩm…',
                toolbar: [['style', ['style']], ['font', ['bold', 'italic', 'underline', 'clear']], ['para', ['ul', 'ol', 'paragraph']], ['insert', ['link', 'picture']], ['view', ['codeview']]],
                callbacks: { onImageUpload: insertFiles, onImageLinkInsert: insertUrl, onChange: () => { if (active) queueMicrotask(scan); } }
            });
            container = $(textarea).next('.note-editor')[0];
            editable = container.querySelector('.note-editable');
            container.addEventListener('paste', transfer, true);
            container.addEventListener('drop', transfer, true);
            container.addEventListener('input', () => { active = true; queueMicrotask(scan); });
            $(textarea).on('summernote.change summernote.change.codeview', () => { active = true; queueMicrotask(scan); });
            observer = new MutationObserver(() => { if (active) scan(); });
            observer.observe(editable, { childList: true, subtree: true, attributes: true, attributeFilter: ['src', 'srcset', 'style'] });
        }
        retry.addEventListener('click', () => { failures.clear(); active = true; scan(); });
        remove.addEventListener('click', () => {
            for (const image of failures.keys()) image.remove();
            failures.clear(); structuralError = ''; scan();
            if (hasSummernote()) $(textarea).summernote('editor.afterCommand');
        });
        renderState();
        form.addEventListener('submit', async event => {
            if (readyToSubmit) return;
            event.preventDefault(); event.stopImmediatePropagation();
            if (preparing) return;
            const submitter = event.submitter;
            preparing = true; active = true; structuralError = ''; setError(''); renderState();
            try {
                if (hasSummernote()) {
                    if (isCodeview()) {
                        const raw = new DOMParser().parseFromString($(textarea).summernote('code'), 'text/html');
                        checkMarkup(raw.body);
                        $(textarea).summernote('codeview.deactivate');
                    }
                    editable.setAttribute('contenteditable', 'false');
                    container.querySelectorAll('.note-toolbar button,.note-statusbar').forEach(element => element.setAttribute('inert', ''));
                    scan();
                    while (jobs.size) await Promise.all([...jobs.values()]);
                    scan();
                    if (failures.size) throw new Error('Còn ảnh upload lỗi. Vui lòng thử lại hoặc xóa ảnh lỗi trước khi lưu.');
                    if (structuralError) throw new Error(structuralError);
                    textarea.value = $(textarea).summernote('code');
                }
                const finalDocument = new DOMParser().parseFromString(textarea.value, 'text/html');
                checkMarkup(finalDocument.body);
                if ([...finalDocument.querySelectorAll('img')].some(image => !owned(image.getAttribute('src')))) throw new Error('Ảnh chưa được upload lên Cloudinary. Vui lòng bật editor để xử lý ảnh.');
                if (textarea.dataset.editorRequired === 'true' && !finalDocument.body.textContent.trim() && !finalDocument.querySelector('img')) throw new Error('Vui lòng nhập nội dung bài viết.');
                if (new Blob([textarea.value]).size > 2621440) throw new Error('Nội dung vượt quá 2,5 MiB. Vui lòng giảm kích thước.');
                validateFields();
                // A native form ignores a nested requestSubmit during its original submit dispatch.
                // Yield a task even when all images were already uploaded.
                await new Promise(resolve => setTimeout(resolve, 0));
                preparing = false; readyToSubmit = true; renderState();
                form.requestSubmit(submitter || undefined);
                // Another validator may cancel the second submit; leave the editor usable.
                readyToSubmit = false;
            } catch (failure) {
                setError(failure.message || 'Chưa lưu được nội dung.');
                submitFeedback.focus({ preventScroll: true });
                submitFeedback.scrollIntoView({ block: 'center' });
            }
            finally {
                preparing = false;
                editable?.setAttribute('contenteditable', 'true');
                container?.querySelectorAll('[inert]').forEach(element => element.removeAttribute('inert'));
                renderState();
            }
        }, true);
        // Our handler checks native constraints, editor content and jQuery validation explicitly,
        // so a failed save always has a visible, persistent explanation beside the save button.
        form.noValidate = true;
        window.addEventListener('pagehide', () => { observer?.disconnect(); objectUrls.forEach(url => URL.revokeObjectURL(url)); }, { once: true });
    });
});
