(() => {
  const badge = document.querySelector('[data-cart-count]');
  if (!badge) return;
  let revision = 0;

  function update(count) {
    if (!Number.isSafeInteger(count) || count < 0) return;
    revision++;
    badge.textContent = String(count);
    badge.classList.toggle('is-visible', count > 0);
  }

  window.storefrontCartBadge = { update };
  window.addEventListener('pageshow', async event => {
    if (!event.persisted || !window.fetch || document.querySelector('.cart-page')) return;
    const version = ++revision;
    try {
      const response = await fetch(badge.dataset.cartCountUrl, { cache: 'no-store' });
      if (!response.ok) return;
      const result = await response.json();
      if (version === revision) update(result.count);
    } catch { /* The current page and its forms can still be used. */ }
  });
  window.addEventListener('pagehide', () => { revision++; });
})();
