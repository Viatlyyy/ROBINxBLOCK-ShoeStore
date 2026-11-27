(() => {
  const items = document.querySelectorAll('[data-reveal]');
  if (!items.length || typeof IntersectionObserver !== 'function') return;

  try {
    const observer = new IntersectionObserver(entries => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        observer.unobserve(entry.target);
        if (!document.hidden) entry.target.classList.add('is-revealing');
      }
    }, { threshold: .14 });
    items.forEach(item => observer.observe(item));
  } catch { /* All reveal targets are visible in the base stylesheet. */ }

  function settle() {
    items.forEach(item => item.classList.remove('is-revealing'));
  }
  window.addEventListener('pagehide', settle);
  document.addEventListener('visibilitychange', () => { if (document.hidden) settle(); });
})();
