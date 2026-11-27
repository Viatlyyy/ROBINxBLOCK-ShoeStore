(() => {
  if (!window.requestAnimationFrame) return;
  const pointer = window.matchMedia?.('(hover: hover) and (pointer: fine)');

  const root = document.scrollingElement || document.documentElement;
  let target = window.scrollY;
  let limit = 0;
  let frame = 0;
  let measureFrame = 0;
  let previousTime = 0;
  let animationDeadline = 0;

  const clamp = value => Math.max(0, Math.min(limit, value));
  const measure = () => {
    measureFrame = 0;
    limit = Math.max(0, root.scrollHeight - window.innerHeight);
    target = clamp(target);
  };
  const scheduleMeasure = () => {
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
    if (document.hidden || !pointer?.matches) {
      stop();
      return;
    }
    if (Math.abs(distance) <= 1 || timestamp >= animationDeadline) {
      window.scrollTo(0, target);
      frame = 0;
      previousTime = 0;
      animationDeadline = 0;
      target = window.scrollY;
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
    if (document.hidden || !pointer?.matches || !event.cancelable ||
        event.defaultPrevented || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey ||
        event.target.closest?.('input, select, textarea, [contenteditable], dialog')) return;
    if (!frame) measure();
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
  try {
    if (typeof ResizeObserver === 'function') new ResizeObserver(scheduleMeasure).observe(document.body);
  } catch { /* Resize and scroll can still run normally. */ }
  window.addEventListener('pagehide', stop);
  window.addEventListener('pageshow', measure);
  document.addEventListener('visibilitychange', () => { if (document.hidden) stop(); });
})();
