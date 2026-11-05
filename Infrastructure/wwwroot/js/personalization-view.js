'use strict';
document.addEventListener('DOMContentLoaded', async () => {
  const node = document.getElementById('recommendation-view-product');
  const ticket = node?.dataset.ticket;
  const token = document.querySelector('#live-chat-form input[name="__RequestVerificationToken"]')?.value;
  if (!ticket || !token) return;
  try {
    const response = await fetch('/api/recommendations/preferences');
    if (!response.ok || !(await response.json()).enabled) return;
  } catch { return; }
  let elapsed = 0, last = performance.now();
  const timer = setInterval(async () => {
    const now = performance.now();
    if (document.visibilityState === 'visible' && document.hasFocus()) elapsed += Math.min(now - last, 1000);
    last = now;
    if (elapsed < 8000) return;
    clearInterval(timer);
    try { await fetch('/api/recommendations/events', {method:'POST', credentials:'same-origin', headers:{'Content-Type':'application/json','RequestVerificationToken':token}, body:JSON.stringify({ticket})}); }
    catch { /* Viewing a product must work even when tracking fails. */ }
  }, 250);
  document.addEventListener('visibilitychange', () => { last = performance.now(); });
});
