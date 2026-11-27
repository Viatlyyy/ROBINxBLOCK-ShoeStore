(() => {
  const instance = Symbol.for('storefront.smoothScroll');
  if (window[instance] || !window.requestAnimationFrame || !window.AbortController || !window.matchMedia || !document.body) return;

  const pointer = window.matchMedia('(any-hover: hover) and (any-pointer: fine)');
  const root = document.scrollingElement || document.documentElement;
  const listeners = new AbortController();
  const passive = { passive: true, signal: listeners.signal };
  const nativeArea = 'input, select, textarea, [contenteditable]:not([contenteditable="false"]), dialog, [data-scroll-native]';
  const modal = window.CSS?.supports?.('selector(dialog:modal)') ? 'dialog:modal' : 'dialog[open]';
  const scrollKeys = new Set(['Tab', 'Home', 'End', 'PageUp', 'PageDown', 'ArrowUp', 'ArrowDown', ' ']);
  const styleLifetime = 200;
  const settlingDistance = .5;
  const wheelTimeConstant = -16.667 / Math.log(.84);
  const fineInputTimeConstant = 42;
  let elementStyles = new WeakMap();
  let target = 0;
  let position = 0;
  let expected = null;
  let settled = null;
  let frame = 0;
  let previousTime = null;
  let lastWheel = -Infinity;
  let timeConstant = wheelTimeConstant;
  let viewport = 0;
  let limit = 0;
  let locked = false;
  let geometryDirty = true;
  let lockDirty = true;
  let resizeObserver;
  let mutations;

  const clamp = y => Math.min(limit, Math.max(0, y));
  const invalidate = () => { geometryDirty = true; };
  const reset = () => {
    if (frame) cancelAnimationFrame(frame);
    frame = 0;
    previousTime = null;
    expected = null;
    settled = null;
    lastWheel = -Infinity;
  };

  function measureLock() {
    const rootStyle = getComputedStyle(root);
    const bodyStyle = getComputedStyle(document.body);
    const blocksScroll = style => /^(hidden|clip)$/.test(style.overflowY) || style.position === 'fixed';
    locked = blocksScroll(rootStyle) || blocksScroll(bodyStyle) ||
      (rootStyle.scrollSnapType && rootStyle.scrollSnapType !== 'none') ||
      !!document.querySelector(modal + ', html[data-scroll-lock], body[data-scroll-lock]');
    lockDirty = false;
  }

  function measure() {
    viewport = root.clientHeight;
    limit = Math.max(0, root.scrollHeight - viewport);
    geometryDirty = false;
  }

  function nestedOwnsWheel(path, delta, now) {
    for (const element of path) {
      if (!(element instanceof Element)) continue;
      if (element.matches(nativeArea)) return true;
      if (element === document.body || element === root) continue;
      let info = elementStyles.get(element);
      if (!info || now >= info.until) {
        const style = getComputedStyle(element);
        info = {
          scrollable: /^(auto|scroll|overlay)$/.test(style.overflowY),
          contains: /^(contain|none)$/.test(style.overscrollBehaviorY),
          until: now + styleLifetime
        };
        elementStyles.set(element, info);
      }
      if (!info.scrollable) continue;
      if (info.contains) return true;
      const maximum = element.scrollHeight - element.clientHeight;
      if (maximum > 0 && (delta < 0 ? element.scrollTop > 0 : element.scrollTop < maximum - .5)) return true;
    }
    return false;
  }

  function reconcile(actual) {
    // A shrinking document can clamp our last write. Any other displacement is
    // already owned by the browser (including anchoring or programmatic jumps).
    if (expected !== null && Math.abs(actual - clamp(expected)) > 1) return false;
    target = clamp(target);
    position = clamp(position);
    return true;
  }

  function render(timestamp) {
    frame = 0;
    if (document.hidden || !pointer.matches) return reset();
    if (geometryDirty) measure();
    if (lockDirty) measureLock();
    if (locked) return reset();

    const actual = window.scrollY;
    if (!reconcile(actual)) return reset();
    const elapsed = previousTime === null ? 1000 / 60 : timestamp - previousTime;
    if (elapsed > 400) return reset();
    previousTime = timestamp;
    const blend = -Math.expm1(-Math.min(Math.max(0, elapsed), 32) / timeConstant);
    position += (target - position) * blend;
    const done = Math.abs(target - position) <= settlingDistance;
    const next = done ? target : position;
    // Avoid issuing writes for subpixel progress that can't move the viewport
    // yet. On settlement, ask the browser for the exact target once.
    if (done ? next !== actual : Math.abs(next - actual) >= .5 / Math.max(1, window.devicePixelRatio)) {
      expected = next;
      window.scrollTo({ top: next, behavior: 'instant' });
    }
    if (done) {
      const goal = target;
      reset();
      settled = goal;
    } else {
      frame = requestAnimationFrame(render);
    }
  }

  function wheel(event) {
    const mode = event.deltaMode;
    if (document.hidden || !pointer.matches || !event.cancelable || event.defaultPrevented ||
        event.ctrlKey || event.metaKey || event.shiftKey || event.altKey ||
        !event.deltaY || Math.abs(event.deltaX) >= Math.abs(event.deltaY)) {
      if (frame) reset();
      return;
    }

    if (geometryDirty || (!resizeObserver && !frame)) measure();
    if (lockDirty || !mutations) measureLock();
    if (locked || !limit) return reset();
    const delta = event.deltaY * (mode === WheelEvent.DOM_DELTA_PAGE ? viewport :
      mode === WheelEvent.DOM_DELTA_LINE ? 16 : 1);
    const now = performance.now();
    if (nestedOwnsWheel(event.composedPath(), delta, now)) return reset();

    const actual = window.scrollY;
    if (frame && !reconcile(actual)) reset();
    if (delta < 0 ? actual <= 0 : actual >= limit) return reset();
    if (!frame) {
      position = actual;
      expected = actual;
      const remainder = settled === null ? 0 : settled - actual;
      target = actual + (Math.abs(remainder) <= .5 ? remainder : 0);
    } else if ((target - position) * delta < 0) {
      target = position;
    }
    event.preventDefault();
    target = clamp(target + delta);
    // Closely spaced small impulses need less additional tail. This is an input
    // cadence adjustment and does not try to identify mouse or trackpad hardware.
    timeConstant = Math.abs(delta) <= 12 && now - lastWheel <= 28 ? fineInputTimeConstant : wheelTimeConstant;
    lastWheel = now;
    if (!frame) frame = requestAnimationFrame(render);
  }

  function destroy() {
    reset();
    resizeObserver?.disconnect();
    mutations?.disconnect();
    listeners.abort();
    delete window[instance];
  }

  window[instance] = destroy;
  try {
    if (typeof ResizeObserver === 'function') {
      resizeObserver = new ResizeObserver(invalidate);
      for (const element of new Set([root, document.body, document.querySelector('main')])) {
        if (element) resizeObserver.observe(element);
      }
    }
    if (typeof MutationObserver === 'function') {
      mutations = new MutationObserver(records => {
        for (const { target: element, type, attributeName } of records) {
          elementStyles.delete(element);
          if (type === 'childList' || element === root || element === document.body) invalidate();
          if (element === root || element === document.body || attributeName === 'open' ||
              type === 'childList') lockDirty = true;
          if (type === 'childList' || attributeName === 'class') elementStyles = new WeakMap();
        }
      });
      mutations.observe(root, {
        subtree: true,
        childList: true,
        attributes: true,
        attributeFilter: ['class', 'style', 'hidden', 'open', 'data-scroll-lock']
      });
    }
    window.addEventListener('wheel', wheel, { ...passive, passive: false });
    window.addEventListener('keydown', event => { if (scrollKeys.has(event.key)) reset(); }, passive);
    window.addEventListener('pointerdown', event => { if (event.pointerType !== 'mouse') reset(); }, passive);
    for (const type of ['focusin', 'blur', 'hashchange']) window.addEventListener(type, reset, passive);
    window.addEventListener('resize', () => {
      elementStyles = new WeakMap();
      invalidate();
      lockDirty = true;
    }, passive);
    document.addEventListener('load', invalidate, { ...passive, capture: true });
    document.addEventListener('visibilitychange', () => { if (document.hidden) reset(); }, passive);
    window.addEventListener('pagehide', event => { if (event.persisted) reset(); else destroy(); }, passive);
    window.addEventListener('pageshow', () => { invalidate(); lockDirty = true; }, passive);
    pointer.addEventListener?.('change', reset, passive);
  } catch {
    destroy();
  }
})();
