(() => {
  const body = document.body;
  if (!body?.classList.contains('is-page-entering')) return;
  // Два кадра дают браузеру показать начальное скрытое состояние до переключения CSS-классов.
  requestAnimationFrame(() => requestAnimationFrame(() => {
    window.setTimeout(() => {
      body.classList.remove('is-page-entering');
      body.classList.add('is-page-ready');
    }, 100);
  }));
})();
