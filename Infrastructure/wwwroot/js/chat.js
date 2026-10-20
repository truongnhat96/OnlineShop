(() => {
  'use strict';
  const widget = document.querySelector('[data-chat-widget]');
  if (!widget) return;
  const launcher = widget.querySelector('#live-chat-toggle');
  const panel = widget.querySelector('#live-chat-popup');
  const close = widget.querySelector('#live-chat-close');
  const form = widget.querySelector('#live-chat-form');
  const input = widget.querySelector('#live-chat-input');
  const log = widget.querySelector('#live-chat-body');
  const sendButton = form.querySelector('button');
  const status = widget.querySelector('#shop-chat-status');
  const errorBox = widget.querySelector('#shop-chat-error');
  const errorText = widget.querySelector('#shop-chat-error-text');
  const retry = widget.querySelector('#shop-chat-retry');
  const reset = widget.querySelector('#shop-chat-reset');
  const suggestions = widget.querySelector('.shop-chat-suggestions');
  const assistantName = widget.querySelector('#shop-chat-title').textContent;
  const storageKey = 'motoshop_chat_v1';
  const failureMessage = widget.dataset.chatError;
  let history = [];
  let pending = false;
  let failedPrompt = null;
  let returnFocus = launcher;

  // Rebuild only a small formatting allowlist. Provider HTML is never inserted into the page.
  function renderAnswer(target, text) {
    const template = document.createElement('template');
    template.innerHTML = text;
    const allowed = new Set(['BR', 'B', 'STRONG', 'I', 'EM', 'P', 'UL', 'OL', 'LI', 'CODE']);
    const blocked = new Set(['SCRIPT', 'STYLE', 'IFRAME', 'OBJECT', 'EMBED', 'SVG', 'MATH', 'IMG', 'VIDEO', 'AUDIO', 'TEMPLATE']);
    function copy(node, parent, depth = 0) {
      if (depth > 40) return;
      if (node.nodeType === Node.TEXT_NODE) { parent.append(document.createTextNode(node.textContent)); return; }
      if (node.nodeType !== Node.ELEMENT_NODE || blocked.has(node.tagName)) return;
      let destination = parent;
      if (allowed.has(node.tagName)) {
        destination = document.createElement(node.tagName.toLowerCase());
        parent.append(destination);
      } else if (node.tagName === 'A') {
        let url;
        try { url = new URL(node.getAttribute('href') || '', location.origin); } catch { /* Keep invalid links as plain text. */ }
        if (url?.origin === location.origin && /^\/(Product\/\d+|Category\/\d+|Search)(?:[/?#]|$)/i.test(url.pathname)) {
          destination = document.createElement('a');
          destination.href = url.pathname + url.search + url.hash;
          parent.append(destination);
        }
      }
      node.childNodes.forEach(child => copy(child, destination, depth + 1));
    }
    template.content.childNodes.forEach(node => copy(node, target));
  }

  function appendMessage(message) {
    const item = document.createElement('div');
    item.className = 'shop-chat-message ' + (message.role === 'user' ? 'user' : 'bot');
    const author = document.createElement('span');
    author.className = 'shop-chat-author';
    author.textContent = message.role === 'user' ? 'Bạn' : assistantName;
    const bubble = document.createElement('div');
    bubble.className = 'shop-chat-bubble';
    if (message.role === 'user') bubble.textContent = message.text;
    else renderAnswer(bubble, message.text);
    const time = document.createElement('time');
    time.className = 'shop-chat-time';
    time.dateTime = new Date(message.time).toISOString();
    time.textContent = new Date(message.time).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    item.append(author, bubble, time);
    log.append(item);
    log.scrollTop = log.scrollHeight;
  }

  function saveHistory() {
    try { sessionStorage.setItem(storageKey, JSON.stringify({ updated: Date.now(), messages: history.slice(-40) })); }
    catch { /* Chat remains usable when browser storage is unavailable. */ }
  }

  function showError(message, canRetry = true) {
    errorText.textContent = message;
    retry.hidden = !canRetry;
    errorBox.hidden = false;
  }

  try {
    const saved = JSON.parse(sessionStorage.getItem(storageKey));
    if (saved && Date.now() - saved.updated < 30 * 60 * 1000 && Array.isArray(saved.messages)) {
      history = saved.messages.slice(-40).filter(message =>
        ['user', 'assistant'].includes(message?.role) && typeof message.text === 'string' &&
        message.text.length <= 32000 && Number.isFinite(message.time) && !Number.isNaN(new Date(message.time).getTime()));
      history.forEach(appendMessage);
      suggestions.hidden = history.length > 0;
      if (history.at(-1)?.role === 'user') {
        failedPrompt = history.at(-1).text;
        showError(failureMessage);
      }
    }
  } catch { /* Ignore invalid or unavailable browser storage. */ }

  function resizeInput() {
    input.style.height = 'auto';
    input.style.height = Math.min(input.scrollHeight, 110) + 'px';
    sendButton.disabled = pending || !input.value.trim();
  }

  function setOpen(open, restoreFocus = true) {
    if (open) returnFocus = document.activeElement;
    panel.hidden = !open;
    launcher.hidden = open;
    launcher.setAttribute('aria-expanded', String(open));
    if (open) { resizeInput(); log.scrollTop = log.scrollHeight; input.focus({ preventScroll: true }); }
    else if (restoreFocus) (returnFocus?.isConnected && returnFocus !== document.body ? returnFocus : launcher).focus({ preventScroll: true });
  }
  launcher.addEventListener('click', () => setOpen(true));
  close.addEventListener('click', () => setOpen(false));
  widget.addEventListener('keydown', event => {
    if (event.key === 'Escape' && !panel.hidden) { event.preventDefault(); setOpen(false); }
  });
  // Bootstrap navigation/modal layers retain focus and sit above the chat widget.
  document.addEventListener('show.bs.offcanvas', () => { if (!panel.hidden) setOpen(false, false); });
  document.addEventListener('show.bs.modal', () => { if (!panel.hidden) setOpen(false, false); });
  function updateViewport() {
    const viewport = window.visualViewport;
    widget.style.setProperty('--chat-viewport-height', (viewport?.height || innerHeight) + 'px');
    widget.style.setProperty('--chat-keyboard-offset', Math.max(0, innerHeight - (viewport?.height || innerHeight) - (viewport?.offsetTop || 0)) + 'px');
  }
  window.visualViewport?.addEventListener('resize', updateViewport);
  window.visualViewport?.addEventListener('scroll', updateViewport);
  window.addEventListener('resize', updateViewport);
  updateViewport();
  input.addEventListener('input', resizeInput);
  input.addEventListener('keydown', event => {
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing && event.keyCode !== 229) {
      event.preventDefault();
      if (!pending) form.requestSubmit();
    }
  });

  async function send(text, isRetry = false) {
    if (pending || !text || text.length > 1000) return;
    pending = true;
    if (!isRetry) {
      const message = { role: 'user', text, time: Date.now() };
      history.push(message);
      appendMessage(message);
      input.value = '';
      saveHistory();
    }
    failedPrompt = null;
    errorBox.hidden = true;
    suggestions.hidden = true;
    status.textContent = 'Trợ lý đang trả lời…';
    reset.disabled = retry.disabled = true;
    sendButton.disabled = true;
    resizeInput();
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 50000);
    let httpStatus = null;
    let failureKind = 'network';
    try {
      const response = await fetch('/api/chat/ask', {
        method: 'POST', headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
        body: JSON.stringify(text), signal: controller.signal, credentials: 'same-origin'
      });
      httpStatus = response.status;
      failureKind = 'invalid_response';
      const data = await response.json().catch(() => null);
      if (!response.ok) {
        failureKind = 'http';
        const error = new Error();
        error.retryable = data?.retryable !== false;
        throw error;
      }
      const answer = data?.choices?.[0]?.message?.content;
      if (typeof answer !== 'string' || !answer.trim() || answer.length > 32000)
        throw new Error();
      const message = { role: 'assistant', text: answer, time: Date.now() };
      appendMessage(message);
      history.push(message);
      saveHistory();
    } catch (error) {
      failedPrompt = text;
      // Only technical metadata: never emit raw response/error messages or user text.
      console.warn('Chat request failed.', { kind: error.name === 'AbortError' ? 'timeout' : failureKind, httpStatus });
      showError(failureMessage, error.retryable !== false);
    } finally {
      clearTimeout(timeout);
      pending = false;
      status.textContent = '';
      reset.disabled = retry.disabled = false;
      resizeInput();
    }
  }

  form.addEventListener('submit', event => { event.preventDefault(); send(input.value.trim()); });
  retry.addEventListener('click', () => { if (failedPrompt) send(failedPrompt, true); });
  suggestions.querySelectorAll('[data-chat-prompt]').forEach(button => button.addEventListener('click', () => {
    input.value = button.dataset.chatPrompt;
    resizeInput();
    form.requestSubmit();
  }));
  reset.addEventListener('click', () => {
    if (pending) return;
    history = [];
    failedPrompt = null;
    log.querySelectorAll('.shop-chat-message:not(.shop-chat-greeting)').forEach(message => message.remove());
    suggestions.hidden = false;
    errorBox.hidden = true;
    input.value = '';
    saveHistory();
    resizeInput();
    input.focus();
  });
})();
