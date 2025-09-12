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


document.addEventListener('DOMContentLoaded', function () {
    const toggle = document.getElementById('live-chat-toggle');
    const popup = document.getElementById('live-chat-popup');
    const closeBtn = document.getElementById('live-chat-close');
    const form = document.getElementById('live-chat-form');
    const input = document.getElementById('live-chat-input');
    const body = document.getElementById('live-chat-body');


    // --- Chatbot timeout & persistent logic ---
    const CHAT_TIMEOUT_MS = 10 * 60 * 1000; // 10 phút
    let chatTimeoutId = null;
    let chatLocked = false;
    let isLocked = sessionStorage.getItem('truongshop_chat_locked') === '1';

    if (isLocked) {
        lockChat();
    }


    function saveChatToStorage() {
        const body = document.getElementById('live-chat-body');
        if (body) {
            sessionStorage.setItem('truongshop_chat_content', body.innerHTML);
        }
    }
    function loadChatFromStorage() {
        const body = document.getElementById('live-chat-body');
        const saved = sessionStorage.getItem('truongshop_chat_content');
        if (body && saved) {
            body.innerHTML = saved;
            resetChatTimeout();
        }
    }
    function clearChatStorage() {
        sessionStorage.removeItem('truongshop_chat_content');
    }
    function resetChatTimeout() {
        if (chatTimeoutId) clearTimeout(chatTimeoutId);
        if (!chatLocked) {
            chatTimeoutId = setTimeout(lockChat, CHAT_TIMEOUT_MS);
        }
    }



    function lockChat() {
        chatLocked = true;
        sessionStorage.setItem('truongshop_chat_locked', '1');
        const input = document.getElementById('live-chat-input');
        const form = document.getElementById('live-chat-form');
        if (input) input.disabled = true;
        if (form) {
            form.querySelector('button[type="submit"]').disabled = true;
            form.style.display = 'none';
        }
        // Xóa nút cũ nếu có
        let restartBtn = document.getElementById('chat-restart-btn');
        if (restartBtn) restartBtn.remove();
        // Xóa thông báo cũ nếu có
        let notice = document.getElementById('chat-end-notice');
        if (notice) notice.remove();
        // Thêm thông báo
        notice = document.createElement('div');
        notice.id = 'chat-end-notice';
        notice.innerText = 'Cuộc trò chuyện kết thúc do không hoạt động';
        notice.style.textAlign = 'center';
        notice.style.color = '#888';
        notice.style.margin = '16px 0 8px 0';
        notice.style.fontSize = '16px';
        // Thêm nút bắt đầu mới
        restartBtn = document.createElement('button');
        restartBtn.id = 'chat-restart-btn';
        restartBtn.className = 'chat-restart-btn';
        restartBtn.innerText = 'Bắt đầu cuộc trò chuyện mới';

        restartBtn.onclick = function() {
            clearChatStorage();
            chatLocked = false;
            sessionStorage.removeItem('truongshop_chat_locked');
            // Reset nội dung chat về mặc định
            const body = document.getElementById('live-chat-body');
            if (body) {
                body.innerHTML = `<div class=\"chat-message bot\">Xin chào! 👋<br>Bạn cần hỗ trợ đặt hàng trực tuyến? Tôi luôn sẵn sàng hỗ trợ bạn trong từng bước!</div>`;
            }
            // Hiện lại form
            if (form) {
                form.style.display = '';
                form.querySelector('button[type="submit"]').disabled = false;
            }
            if (input) {
                input.disabled = false;
                input.value = '';
                input.focus();
            }
            restartBtn.remove();
            notice.remove();
        };
        // Thêm vào popup
        const popup = document.getElementById('live-chat-popup');
        if (popup) {
            popup.appendChild(notice);
            popup.appendChild(restartBtn);
        }
    }
    // Khi load lại trang, chỉ load chat nếu chưa từng load (dựa vào storage flag)
    (function () {
        if (!sessionStorage.getItem('truongshop_chat_loaded')) {
            sessionStorage.setItem('truongshop_chat_loaded', '1');
            clearChatStorage();
        }
        loadChatFromStorage();
    })();

    input.addEventListener('keydown', e => {
        if (chatLocked) return;
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            form.dispatchEvent(new Event('submit'));
        }
    })

    // Mở popup khi bấm icon
    toggle.addEventListener('click', () => {
        popup.classList.add('open');
        toggle.style.display = 'none';
        input.focus();
    });

    // Đóng popup
    closeBtn.addEventListener('click', () => {
        popup.classList.remove('open');
        toggle.style.display = 'flex';
    });

    // Gửi tin nhắn
    form.addEventListener('submit', async function (e) {
        if (chatLocked) return;
        e.preventDefault();
        const text = input.value.trim();
        if (!text) return;
        // Hiển thị tin nhắn user
        const userMsg = document.createElement('div');
        userMsg.className = 'chat-message user';
        userMsg.innerHTML = `<span class="chat-text">${escapeHtml(text)}</span> ${getTimeHtml()}`;
        body.appendChild(userMsg);
        input.value = '';
        body.scrollTop = body.scrollHeight;
        saveChatToStorage();
        resetChatTimeout();
        // Hiển thị hiệu ứng đang soạn tin nhắn
        showTypingIndicator();

        const responses = await fetch('/api/chat/ask', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(text)
        })
        if (responses.ok) {
            const data = await responses.json();
            hideTypingIndicator();
            const botMsg = document.createElement('div');
            botMsg.className = 'chat-message bot';
            botMsg.innerHTML = `<span class="chat-text">${data.choices[0].message.content}</span>${getTimeHtml()}`;
            body.appendChild(botMsg);
            body.scrollTop = body.scrollHeight;
            saveChatToStorage();
            resetChatTimeout();
        } else {
            hideTypingIndicator();
            const botMsg = document.createElement('div');
            botMsg.className = 'chat-message bot error';
            botMsg.innerHTML = `<span class="chat-text">Xin lỗi, đã có lỗi xảy ra. Vui lòng thử lại sau.</span>${getTimeHtml()}`;
            body.appendChild(botMsg);
            body.scrollTop = body.scrollHeight;
            saveChatToStorage();
            resetChatTimeout();
        }
    });

    // Hiệu ứng đang soạn tin nhắn
    function showTypingIndicator() {
        // Xóa nếu đã có
        const old = document.getElementById('typing-indicator');
        if (old) old.remove();
        const typing = document.createElement('div');
        typing.className = 'typing-indicator';
        typing.id = 'typing-indicator';
        typing.innerHTML = '<div class="dot"></div><div class="dot"></div><div class="dot"></div>';
        body.appendChild(typing);
        body.scrollTop = body.scrollHeight;
    }
    function hideTypingIndicator() {
        const typing = document.getElementById('typing-indicator');
        if (typing) typing.remove();
    }

    // Hiển thị thời gian gửi
    function getTimeHtml() {
        const now = new Date();
        const h = now.getHours().toString().padStart(2, '0');
        const m = now.getMinutes().toString().padStart(2, '0');
        const iso = now.toISOString();
        return `<time class="chat-time" datetime="${iso}">${h}:${m}</time>`;
    }
    // Escape HTML để tránh lỗi XSS
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
});

