(() => {
  const ticker = document.querySelector('.brand-ticker');
  if (!ticker) return;

  const toggle = ticker.querySelector('[data-ticker-pause]');
  let inView = false;
  let manuallyPaused = false;
  let leaving = false;
  function sync() {
    ticker.classList.toggle('is-paused',
      leaving || !inView || manuallyPaused || document.hidden);
  }

  try {
    if (typeof IntersectionObserver !== 'function') return;
    new IntersectionObserver(([entry]) => {
      inView = entry.isIntersecting;
      sync();
    }).observe(ticker);
  } catch { return; }

  ticker.classList.add('is-enhanced');
  toggle.hidden = false;
  toggle.addEventListener('click', () => {
    manuallyPaused = !manuallyPaused;
    toggle.setAttribute('aria-pressed', String(manuallyPaused));
    sync();
  });
  document.addEventListener('visibilitychange', sync);
  window.addEventListener('pagehide', () => { leaving = true; sync(); });
  window.addEventListener('pageshow', () => { leaving = false; sync(); });
})();
