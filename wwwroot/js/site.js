(() => {
  // Web Animations are used deliberately so product motion also works when
  // operating-system animations are disabled.
  const duration = 700;
  const imageCache = new Map();
  const states = new WeakMap();

  const absoluteUrl = source => new URL(source, document.baseURI).href;
  const sameSource = (first, second) =>
    Boolean(first && second && absoluteUrl(first) === absoluteUrl(second));

  const preload = source => {
    const url = absoluteUrl(source);
    if (imageCache.has(url)) return imageCache.get(url);
    const task = new Promise(resolve => {
      const loader = new Image();
      loader.onload = async () => {
        try { await loader.decode?.(); } catch { /* A loaded image can still be shown. */ }
        resolve(true);
      };
      loader.onerror = () => resolve(false);
      loader.src = source;
    });
    imageCache.set(url, task);
    return task;
  };

  const stateFor = image => {
    let state = states.get(image);
    if (!state) {
      state = { requestId: 0, overlay: null, animations: [] };
      states.set(image, state);
    }
    return state;
  };

  const resetCardTransition = (image, state) => {
    state.animations.forEach(animation => animation.cancel());
    state.animations = [];
    state.overlay?.remove();
    state.overlay = null;
    image.style.removeProperty('opacity');
    image.style.removeProperty('transform');
    image.removeAttribute('aria-busy');
    image.closest('[data-product-card]')?.removeAttribute('data-active-animations');
  };

  const requestCardImage = async (image, source) => {
    if (!image || !source) return;
    const state = stateFor(image);
    const requestId = ++state.requestId;
    if (!await preload(source) || requestId !== state.requestId) return;

    resetCardTransition(image, state);
    if (sameSource(image.currentSrc || image.src, source)) return;

    const overlay = image.cloneNode(false);
    overlay.removeAttribute('data-variant-image');
    overlay.removeAttribute('aria-busy');
    overlay.removeAttribute('srcset');
    overlay.alt = '';
    overlay.setAttribute('aria-hidden', 'true');
    overlay.className = 'variant-image-previous';
    image.before(overlay);
    state.overlay = overlay;

    image.setAttribute('aria-busy', 'true');
    image.src = source;
    image.dataset.variantSource = source;
    state.animations = [
      overlay.animate(
        [{ opacity: 1, transform: 'translateX(0) scale(1)' }, { opacity: 0, transform: 'translateX(-22px) scale(.985)' }],
        { duration, easing: 'cubic-bezier(.16, 1, .3, 1)', fill: 'both' }),
      image.animate(
        [{ opacity: 0, transform: 'translateX(22px) scale(1.015)' }, { opacity: 1, transform: 'translateX(0) scale(1)' }],
        { duration, easing: 'cubic-bezier(.16, 1, .3, 1)', fill: 'both' })
    ];
    image.closest('[data-product-card]')?.setAttribute('data-active-animations', '2');

    try { await Promise.all(state.animations.map(animation => animation.finished)); } catch { return; }
    if (requestId !== state.requestId) return;
    resetCardTransition(image, state);
  };

  const selectVariant = (scope, button) => {
    const image = scope.querySelector('[data-variant-image]');
    const label = scope.querySelector('[data-variant-label]');

    void requestCardImage(image, button.dataset.variantImageUrl || '');
    if (label) label.textContent = button.dataset.variantName || '';

    scope.querySelectorAll('[data-variant-choice]').forEach(choice => {
      const active = choice === button;
      choice.classList.toggle('is-selected', active);
      choice.setAttribute('aria-pressed', String(active));
    });

  };

  document.querySelectorAll('[data-variant-image]').forEach(image => {
    image.dataset.variantSource = image.getAttribute('src') || '';
  });

  document.addEventListener('click', event => {
    const button = event.target.closest('[data-variant-choice]');
    const scope = button?.closest('[data-product-card]');
    if (!button || !scope) return;
    selectVariant(scope, button);
  });

  const warmCards = () => {
    document.querySelectorAll('[data-product-card] [data-variant-image-url]').forEach(button => {
      const source = button.dataset.variantImageUrl;
      if (source) void preload(source);
    });
  };
  if ('requestIdleCallback' in window) window.requestIdleCallback(warmCards, { timeout: 1800 });
  else window.setTimeout(warmCards, 500);
})();
