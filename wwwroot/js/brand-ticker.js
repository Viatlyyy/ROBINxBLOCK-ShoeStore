(() => {
  const ticker = document.querySelector('.brand-ticker');
  if (!ticker || !('IntersectionObserver' in window)) return;

  let isVisible = false;
  const setPlaying = () => ticker.classList.toggle('is-paused', !isVisible || document.hidden);
  setPlaying(false);

  const observer = new IntersectionObserver(entries => {
    isVisible = entries.some(entry => entry.isIntersecting);
    setPlaying();
  }, { threshold: 0 });

  observer.observe(ticker);
  document.addEventListener('visibilitychange', setPlaying);
})();
