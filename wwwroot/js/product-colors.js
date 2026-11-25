(() => {
  const duration = 700;
  const easing = 'cubic-bezier(.16, 1, .3, 1)';
  const states = new WeakMap();
  const preloads = new Map();

  const preload = source => {
    if (!preloads.has(source)) {
      const task = new Promise(resolve => {
        const image = new Image();
        image.onload = async () => {
          try { await image.decode(); } catch { /* Loaded images can still be displayed. */ }
          resolve(true);
        };
        image.onerror = () => {
          preloads.delete(source);
          resolve(false);
        };
        image.src = source;
      });
      preloads.set(source, task);
    }
    return preloads.get(source);
  };

  const reset = (image, state) => {
    state.animations.forEach(animation => animation.cancel());
    state.animations = [];
    state.overlay?.remove();
    state.overlay = null;
    image.removeAttribute('aria-busy');
  };

  const changeCardImage = async (card, source) => {
    const image = card.querySelector('[data-variant-image]');
    const placeholder = card.querySelector('[data-variant-image-empty]');
    if (!image) return;
    if (!states.has(image)) states.set(image, { request: 0, overlay: null, animations: [] });
    const state = states.get(image);
    const request = ++state.request;
    reset(image, state);

    const url = source ? new URL(source, document.baseURI).href : '';
    if (url && image.getAttribute('src') && image.src === url) return;
    const loaded = url && await preload(url);
    if (request !== state.request) return;
    if (!loaded) {
      image.hidden = true;
      image.removeAttribute('src');
      placeholder.hidden = false;
      return;
    }

    if (image.hidden || !image.naturalWidth) {
      image.src = url;
      image.hidden = false;
      placeholder.hidden = true;
      return;
    }

    const overlay = image.cloneNode(false);
    overlay.removeAttribute('data-variant-image');
    overlay.removeAttribute('srcset');
    overlay.alt = '';
    overlay.setAttribute('aria-hidden', 'true');
    overlay.className = 'variant-image-previous';
    image.before(overlay);
    state.overlay = overlay;
    image.src = url;
    image.setAttribute('aria-busy', 'true');
    state.animations = [
      overlay.animate(
        [{ opacity: 1, transform: 'translateX(0) scale(1)' }, { opacity: 0, transform: 'translateX(-22px) scale(.985)' }],
        { duration, easing, fill: 'both' }),
      image.animate(
        [{ opacity: 0, transform: 'translateX(22px) scale(1.015)' }, { opacity: 1, transform: 'translateX(0) scale(1)' }],
        { duration, easing, fill: 'both' })
    ];
    await Promise.allSettled(state.animations.map(animation => animation.finished));
    if (request === state.request) reset(image, state);
  };

  document.addEventListener('click', event => {
    const button = event.target.closest('[data-variant-choice]');
    const scope = button?.closest('[data-product-card], .details');
    if (!scope || button.getAttribute('aria-pressed') === 'true') return;
    const variantId = button.dataset.variantId;
    scope.querySelectorAll('[data-variant-choice]').forEach(choice => {
      const active = choice === button;
      choice.classList.toggle('is-selected', active);
      choice.setAttribute('aria-pressed', String(active));
    });
    const label = scope.querySelector('[data-variant-label]');
    if (label) label.textContent = button.dataset.variantName;

    const link = scope.querySelector('.product-card__link');
    if (link) {
      const url = new URL(link.href);
      url.searchParams.set('variantId', variantId);
      link.href = url.href;
      void changeCardImage(scope, button.dataset.variantImageUrl);
      return;
    }

    scope.querySelector('[data-variant-input]').value = variantId;
    scope.querySelectorAll('[data-size-options]').forEach(group => {
      const active = group.dataset.sizeOptions === variantId;
      group.hidden = !active;
      group.querySelectorAll('input').forEach(input => {
        input.disabled = !active;
        input.checked = false;
      });
    });
    const url = new URL(location.href);
    url.searchParams.set('variantId', variantId);
    history.replaceState(history.state, '', url.href);
    scope.dispatchEvent(new CustomEvent('productvariantchange', { detail: { variantId } }));
  });

  const warmCards = () => {
    document.querySelectorAll('[data-variant-image-url]').forEach(choice => {
      const source = choice.dataset.variantImageUrl;
      if (source) void preload(new URL(source, document.baseURI).href);
    });
  };
  if ('requestIdleCallback' in window) window.requestIdleCallback(warmCards, { timeout: 1800 });
  else window.setTimeout(warmCards, 500);
})();
