(() => {
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
