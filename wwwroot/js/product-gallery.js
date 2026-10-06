(() => {
  const gallery = document.querySelector('[data-product-gallery]');
  const details = document.querySelector('.details');
  const frame = gallery?.querySelector('.detail-image');
  const main = gallery?.querySelector('[data-gallery-main]');
  const buffer = gallery?.querySelector('[data-gallery-buffer]');
  if (!gallery || !details || !frame || !main || !buffer) return;

  const groups = [...gallery.querySelectorAll('[data-gallery-images]')];
  const previous = gallery.querySelector('[data-gallery-previous]');
  const next = gallery.querySelector('[data-gallery-next]');
  const counter = gallery.querySelector('[data-gallery-counter]');
  const preloadCache = new Map();
  let activeGroup = groups.find(group => !group.hidden) || groups[0];
  let requestId = 0;
  let animations = [];

  const absoluteUrl = source => new URL(source, document.baseURI).href;
  const sameSource = (first, second) => Boolean(first && second && absoluteUrl(first) === absoluteUrl(second));
  const buttons = () => activeGroup ? [...activeGroup.querySelectorAll('[data-gallery-image]')] : [];

  const preload = source => {
    const url = absoluteUrl(source);
    if (preloadCache.has(url)) return preloadCache.get(url);
    const task = new Promise(resolve => {
      const loader = new Image();
      loader.onload = async () => {
        try { await loader.decode?.(); } catch { /* Loaded images remain displayable. */ }
        resolve(true);
      };
      loader.onerror = () => resolve(false);
      loader.src = source;
    });
    preloadCache.set(url, task);
    return task;
  };

  const stopAnimations = () => {
    animations.forEach(animation => animation.cancel());
    animations = [];
    gallery.dataset.activeAnimations = '0';
  };

  const resetLayers = () => {
    stopAnimations();
    if (buffer.dataset.gallerySource) {
      main.src = buffer.src;
      main.dataset.gallerySource = buffer.dataset.gallerySource;
    }
    buffer.removeAttribute('src');
    delete buffer.dataset.gallerySource;
    main.style.removeProperty('opacity');
    main.style.removeProperty('transform');
    buffer.style.removeProperty('opacity');
    buffer.style.removeProperty('transform');
    frame.removeAttribute('aria-busy');
  };

  const updateControls = activeButton => {
    const images = buttons();
    const selected = activeButton || images.find(button => button.classList.contains('is-active')) || images[0];
    images.forEach(button => {
      const active = button === selected;
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-pressed', String(active));
    });
    const index = Math.max(0, images.indexOf(selected));
    if (counter) counter.textContent = `${index + 1} / ${Math.max(1, images.length)}`;
    if (previous) previous.hidden = images.length < 2;
    if (next) next.hidden = images.length < 2;
  };

  const changeImage = async (source, transition = 'photo') => {
    if (!source) return;
    const currentRequest = ++requestId;
    if (!await preload(source) || currentRequest !== requestId) return;

    resetLayers();
    if (sameSource(main.dataset.gallerySource || main.currentSrc || main.src, source)) return;

    buffer.src = source;
    buffer.dataset.gallerySource = source;
    frame.dataset.currentSource = source;
    frame.setAttribute('aria-busy', 'true');

    const colour = transition === 'colour';
    const duration = colour ? 560 : 720;
    const easing = 'cubic-bezier(.16, 1, .3, 1)';
    animations = [
      main.animate(
        colour
          ? [{ opacity: 1, transform: 'translateY(0) scale(1)' }, { opacity: 0, transform: 'translateY(-28px) scale(.985)' }]
          : [{ opacity: 1, transform: 'translateX(0) scale(1)' }, { opacity: 0, transform: 'translateX(-24px) scale(.99)' }],
        { duration, easing, fill: 'both' }),
      buffer.animate(
        colour
          ? [{ opacity: 0, transform: 'translateY(24px) scale(.985)' }, { opacity: 1, transform: 'translateY(0) scale(1)' }]
          : [{ opacity: 0, transform: 'translateX(24px) scale(1.01)' }, { opacity: 1, transform: 'translateX(0) scale(1)' }],
        { duration, easing, fill: 'both' })
    ];
    gallery.dataset.activeAnimations = String(animations.length);

    try { await Promise.all(animations.map(animation => animation.finished)); } catch { return; }
    if (currentRequest !== requestId) return;
    resetLayers();
  };

  const show = (button, transition = 'photo') => {
    if (!button) return;
    updateControls(button);
    void changeImage(button.dataset.gallerySource || '', transition);
  };

  const move = direction => {
    const images = buttons();
    if (images.length < 2) return;
    const current = images.findIndex(button => button.classList.contains('is-active'));
    const index = current < 0 ? 0 : current;
    show(images[(index + direction + images.length) % images.length]);
  };

  gallery.addEventListener('click', event => {
    const thumbnail = event.target.closest('[data-gallery-image]');
    if (thumbnail && activeGroup?.contains(thumbnail)) {
      show(thumbnail);
      return;
    }
    if (event.target.closest('[data-gallery-previous]')) move(-1);
    else if (event.target.closest('[data-gallery-next]')) move(1);
  });
  frame.addEventListener('dragstart', event => event.preventDefault());

  details.addEventListener('productvariantchange', event => {
    const variantId = String(event.detail?.variantId ?? '');
    const group = groups.find(item => item.dataset.galleryImages === variantId);
    if (!group || group === activeGroup) return;
    ++requestId;
    resetLayers();
    groups.forEach(item => { item.hidden = item !== group; });
    activeGroup = group;
    const first = buttons()[0];
    show(first, 'colour');
    buttons().slice(1).forEach(button => void preload(button.dataset.gallerySource || ''));
  });

  main.dataset.gallerySource = main.currentSrc || main.getAttribute('src') || '';
  frame.dataset.currentSource = main.dataset.gallerySource;
  gallery.dataset.activeAnimations = '0';
  updateControls(buttons()[0]);
  const warm = () => buttons().slice(1).forEach(button => void preload(button.dataset.gallerySource || ''));
  if ('requestIdleCallback' in window) window.requestIdleCallback(warm, { timeout: 1200 });
  else window.setTimeout(warm, 300);
})();
