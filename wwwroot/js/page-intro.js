(() => {
  const body = document.body;
  if (!body?.classList.contains('is-page-entering')) return;
  requestAnimationFrame(() => requestAnimationFrame(() => {
    window.setTimeout(() => {
      body.classList.remove('is-page-entering');
      body.classList.add('is-page-ready');
    }, 100);
  }));
})();
