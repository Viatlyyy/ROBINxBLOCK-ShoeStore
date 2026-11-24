(() => {
  const body = document.body;
  body.classList.add('is-page-entering');
  document.addEventListener('DOMContentLoaded', () => {
    requestAnimationFrame(() => requestAnimationFrame(() => {
      window.setTimeout(() => {
        body.classList.remove('is-page-entering');
        body.classList.add('is-page-ready');
      }, 100);
    }));
  }, { once: true });
})();
