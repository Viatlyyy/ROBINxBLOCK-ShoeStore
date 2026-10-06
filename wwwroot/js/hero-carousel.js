(() => {
  const root = document.querySelector('.hero-carousel');
  if (!root) return;

  const slides = [...root.querySelectorAll('.hero-slide')];
  const dots = [...root.querySelectorAll('[data-hero-dot]')];
  const progress = root.querySelector('.hero-progress span');
  if (!slides.length) return;

  let current = 0;
  let phase = 0;
  let timer = 0;
  let progressAnimation = null;
  let isVisible = true;
  const sceneDuration = 4500;
  const pairDuration = 4500;
  const cycleDuration = sceneDuration + pairDuration;

  const runWhenIdle = callback => {
    if ('requestIdleCallback' in window) window.requestIdleCallback(callback, { timeout: 800 });
    else window.setTimeout(callback, 180);
  };

  const hydrate = (index, selector) => {
    const image = slides[index]?.querySelector(`${selector}[data-src]`);
    const source = image?.dataset.src;
    if (!image || !source) return;
    image.src = source;
    if (image.dataset.srcset) image.srcset = image.dataset.srcset;
    image.removeAttribute('data-src');
    image.removeAttribute('data-srcset');
  };

  const warm = index => runWhenIdle(() => {
    hydrate(index, '.hero-scene--feet');
    hydrate(index, '.hero-scene--pair');
  });

  const pause = () => {
    window.clearTimeout(timer);
    timer = 0;
    progressAnimation?.pause();
  };

  const playProgress = () => {
    progressAnimation?.cancel();
    progressAnimation = progress?.animate(
      [{ transform: 'scaleX(0)' }, { transform: 'scaleX(1)' }],
      { duration: cycleDuration, fill: 'forwards', easing: 'linear' }
    ) ?? null;
  };

  const schedule = () => {
    window.clearTimeout(timer);
    if (!isVisible || document.hidden) return;
    timer = window.setTimeout(() => {
      if (phase === 0) {
        phase = 1;
        slides[current].classList.add('is-pair');
        schedule();
      } else {
        show(current + 1);
      }
    }, phase === 0 ? sceneDuration : pairDuration);
  };

  const show = next => {
    root.classList.add('has-transitioned');
    slides[current].classList.remove('is-active', 'is-pair');
    current = (next + slides.length) % slides.length;
    phase = 0;
    hydrate(current, '.hero-scene--feet');
    slides[current].classList.add('is-active');
    dots.forEach((dot, index) => dot.classList.toggle('is-active', index === current));
    warm(current);
    warm((current + 1) % slides.length);
    if (isVisible && !document.hidden) playProgress();
    schedule();
  };

  root.querySelector('[data-hero-prev]')?.addEventListener('click', () => show(current - 1));
  root.querySelector('[data-hero-next]')?.addEventListener('click', () => show(current + 1));
  dots.forEach((dot, index) => dot.addEventListener('click', () => show(index)));

  if ('IntersectionObserver' in window) {
    new IntersectionObserver(entries => {
      isVisible = entries.some(entry => entry.isIntersecting);
      if (isVisible && !document.hidden) {
        playProgress();
        schedule();
      } else {
        pause();
      }
    }, { threshold: 0 }).observe(root);
  }

  document.addEventListener('visibilitychange', () => {
    if (document.hidden) pause();
    else if (isVisible) {
      playProgress();
      schedule();
    }
  });

  warm(current);
  warm(1 % slides.length);
  playProgress();
  schedule();
})();
