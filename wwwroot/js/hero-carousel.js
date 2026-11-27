(() => {
  const root = document.querySelector('.hero-carousel');
  if (!root) return;

  const slides = [...root.querySelectorAll('.hero-slide')];
  const dots = [...root.querySelectorAll('[data-hero-dot]')];
  const controls = root.querySelector('[data-hero-controls]');
  const progress = root.querySelector('[data-hero-progress]');
  if (!slides.length) return;

  const duration = () => parseFloat(getComputedStyle(root).getPropertyValue('--motion-hero-cycle'));
  let current = 0;
  let phase = 0;
  let timer = 0;
  let started = 0;
  let remaining = 0;
  let inView = true;
  let focusedSlide = false;
  let leaving = false;
  const warmIndices = new Set();
  let warmScheduled = false;

  function hydrate(index, selector = '.hero-scene') {
    const images = slides[index].querySelectorAll(selector + '[data-src]');
    for (const image of images) {
      if (image.dataset.srcset) image.srcset = image.dataset.srcset;
      image.src = image.dataset.src;
      delete image.dataset.src;
      delete image.dataset.srcset;
    }
  }

  function warmNextSlides() {
    warmIndices.add(current);
    warmIndices.add((current + 1) % slides.length);
    if (warmScheduled) return;
    warmScheduled = true;
    const load = () => {
      warmScheduled = false;
      if (!leaving) warmIndices.forEach(index => hydrate(index));
      warmIndices.clear();
    };
    if (typeof requestIdleCallback === 'function') requestIdleCallback(load, { timeout: 800 });
    else setTimeout(load, 180);
  }

  function setSlide(index, active) {
    const slide = slides[index];
    slide.classList.toggle('is-active', active);
    slide.toggleAttribute('inert', !active);
    if (active) slide.removeAttribute('aria-hidden');
    else slide.setAttribute('aria-hidden', 'true');
    for (const link of slide.querySelectorAll('a')) {
      if (active) link.removeAttribute('tabindex');
      else link.tabIndex = -1;
    }
    dots[index]?.classList.toggle('is-active', active);
  }

  function pause() {
    if (timer) remaining = Math.max(0, remaining - (performance.now() - started));
    clearTimeout(timer);
    timer = 0;
    root.classList.remove('is-playing');
  }

  function sync() {
    if (leaving || !inView || focusedSlide || document.hidden) {
      pause();
      return;
    }
    if (timer) return;
    if (!remaining) remaining = duration() / 2;
    if (!(remaining > 0)) return;
    root.classList.add('is-playing');
    started = performance.now();
    timer = setTimeout(advance, remaining);
  }

  function resetCycle() {
    pause();
    remaining = 0;
    phase = 0;
    slides[current].classList.remove('is-pair');
    // Replacing the decorative bar restarts its CSS timeline without forcing layout.
    progress.firstElementChild.replaceWith(progress.firstElementChild.cloneNode(false));
  }

  function show(next) {
    resetCycle();
    root.classList.add('has-transitioned');
    setSlide(current, false);
    current = (next + slides.length) % slides.length;
    hydrate(current, '.hero-scene--feet');
    warmNextSlides();
    setSlide(current, true);
    sync();
  }

  function advance() {
    timer = 0;
    remaining = 0;
    if (phase === 0) {
      const pair = slides[current].querySelector('.hero-scene--pair');
      if (pair?.naturalWidth) slides[current].classList.add('is-pair');
      phase = 1;
      sync();
    } else {
      show(current + 1);
    }
  }

  root.querySelector('[data-hero-prev]').addEventListener('click', () => show(current - 1));
  root.querySelector('[data-hero-next]').addEventListener('click', () => show(current + 1));
  root.addEventListener('focusin', event => {
    focusedSlide = Boolean(event.target.closest('.hero-slide'));
    sync();
  });
  root.addEventListener('focusout', event => {
    focusedSlide = Boolean(event.relatedTarget?.closest?.('.hero-slide'));
    sync();
  });
  root.addEventListener('error', event => {
    if (!event.target.matches('.hero-scene')) return;
    event.target.hidden = true;
    if (event.target.matches('.hero-scene--pair')) event.target.closest('.hero-slide').classList.remove('is-pair');
  }, true);

  if (typeof IntersectionObserver === 'function') {
    try {
      new IntersectionObserver(([entry]) => {
        inView = entry.isIntersecting;
        sync();
      }).observe(root);
    } catch { /* Manual controls and the normal timer are still available. */ }
  }
  document.addEventListener('visibilitychange', sync);
  window.addEventListener('pagehide', () => { leaving = true; pause(); });
  window.addEventListener('pageshow', event => {
    leaving = false;
    if (event.persisted) warmNextSlides();
    sync();
  });

  slides.forEach((slide, index) => setSlide(index, index === current));
  warmNextSlides();
  controls.hidden = progress.hidden = false;
  sync();
})();
