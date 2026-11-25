(() => {
  const root = document.documentElement;
  const purchaseRoot = document.currentScript.dataset.purchaseRoot.toLowerCase();
  root.classList.add('purchase-document');
  if ('onpageswap' in window) root.classList.add('purchase-transition-ready');

  window.addEventListener('pagereveal', event => {
    const transition = event.viewTransition;
    if (transition) {
      root.classList.add('purchase-has-transition');
      transition.ready.catch(() => root.classList.remove('purchase-has-transition'));
    }
  });

  window.addEventListener('pageswap', event => {
    const transition = event.viewTransition;
    if (!transition) return;
    transition.ready.catch(() => {});

    const url = event.activation?.entry?.url;
    const destination = url ? new URL(url) : null;
    const path = destination?.pathname.toLowerCase();
    if (!destination || destination.origin !== location.origin ||
        !(path === purchaseRoot || path.startsWith(purchaseRoot + '/'))) {
      transition.skipTransition();
    }
  });

  function hideBrokenPhoto(image) {
    if (image instanceof HTMLImageElement && image.matches('[data-purchase-photo]')) {
      image.hidden = true;
    }
  }

  document.addEventListener('error', event => hideBrokenPhoto(event.target), true);
  document.addEventListener('DOMContentLoaded', () => {
    for (const image of document.querySelectorAll('[data-purchase-photo]')) {
      if (image.complete && !image.naturalWidth) hideBrokenPhoto(image);
    }
  }, { once: true });
})();
