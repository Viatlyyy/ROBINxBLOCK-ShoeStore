(() => {
  if (window.matchMedia('(pointer: coarse)').matches) return;

  const root = document.scrollingElement || document.documentElement;
  let target = window.scrollY;
  let limit = 0;
  let frame = 0;
  let measureFrame = 0;
  let previousTime = 0;
  let animationDeadline = 0;
  root.style.scrollBehavior = 'auto';

  const clamp = value => Math.max(0, Math.min(limit, value));
  const measure = () => {
    measureFrame = 0;
    limit = Math.max(0, root.scrollHeight - window.innerHeight);
    target = clamp(target);
  };
  const scheduleMeasure = () => {
    // Scroll anchoring can move the viewport when lazy content changes the
    // document height. Do not fight that native correction with an old target.
    if (frame) stop();
    if (!measureFrame) measureFrame = requestAnimationFrame(measure);
  };
  const stop = () => {
    if (frame) cancelAnimationFrame(frame);
    frame = 0;
    previousTime = 0;
    animationDeadline = 0;
    target = window.scrollY;
  };
  const deltaOf = event => {
    if (event.deltaMode === WheelEvent.DOM_DELTA_LINE) return event.deltaY * 16;
    if (event.deltaMode === WheelEvent.DOM_DELTA_PAGE) return event.deltaY * window.innerHeight;
    return event.deltaY;
  };
  const nestedScrollerCanMove = (origin, delta) => {
    for (let node = origin instanceof Element ? origin : origin?.parentElement;
      node && node !== document.body;
      node = node.parentElement) {
      const style = getComputedStyle(node);
      if (!/(auto|scroll|overlay)/.test(style.overflowY) || node.scrollHeight <= node.clientHeight + 1) continue;
      if ((delta < 0 && node.scrollTop > 0) ||
          (delta > 0 && node.scrollTop + node.clientHeight < node.scrollHeight - 1)) return true;
    }
    return false;
  };
  const render = timestamp => {
    const current = window.scrollY;
    const distance = target - current;
    // A wheel gesture must have a finite lifetime. This prevents a stale
    // fractional target from nudging an otherwise idle page indefinitely.
    if (Math.abs(distance) < .5 || timestamp >= animationDeadline) {
      frame = 0;
      previousTime = 0;
      animationDeadline = 0;
      // Finish exactly where the preceding animation frame stopped. Snapping
      // to the old target here produces a visible one-pixel kick at rest.
      target = current;
      return;
    }
    const elapsed = Math.min(Math.max(timestamp - (previousTime || timestamp - 16.667), 1), 50);
    previousTime = timestamp;
    const easing = 1 - Math.pow(.84, elapsed / 16.667);
    window.scrollTo(0, current + distance * easing);
    frame = requestAnimationFrame(render);
  };

  measure();
  window.addEventListener('wheel', event => {
    if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    const delta = deltaOf(event);
    if (Math.abs(delta) < .25 || !limit || nestedScrollerCanMove(event.target, delta)) return;
    const current = window.scrollY;
    if ((delta < 0 && current <= 1) || (delta > 0 && current >= limit - 1)) {
      stop();
      return;
    }
    event.preventDefault();
    target = clamp((frame ? target : current) + delta);
    animationDeadline = performance.now() + 700;
    if (!frame) frame = requestAnimationFrame(render);
  }, { passive: false });

  window.addEventListener('scroll', () => { if (!frame) target = window.scrollY; }, { passive: true });
  window.addEventListener('keydown', stop, { passive: true });
  window.addEventListener('pointerdown', stop, { passive: true });
  window.addEventListener('blur', stop, { passive: true });
  window.addEventListener('resize', scheduleMeasure, { passive: true });
  if ('ResizeObserver' in window) new ResizeObserver(scheduleMeasure).observe(document.body);
  document.addEventListener('visibilitychange', () => { if (document.hidden) stop(); });
})();
