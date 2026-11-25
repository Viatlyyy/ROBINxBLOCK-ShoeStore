(() => {
  const body = document.body;
  const purchaseRoot = document.currentScript.dataset.purchaseRoot;
  if (purchaseRoot && document.referrer) {
    const previous = new URL(document.referrer);
    const path = purchaseRoot.toLowerCase();
    const previousPath = previous.pathname.toLowerCase();
    if (previous.origin === location.origin && (previousPath === path || previousPath.startsWith(path + '/'))) {
      body.setAttribute('data-page-entry-from-purchase', '');
    }
  }
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
