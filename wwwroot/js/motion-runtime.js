(() => {
  const active = new Set();
  const targets = new WeakMap();
  const canAnimate = element => !document.hidden && typeof element?.animate === 'function';

  function animate(element, frames, durationToken, options = {}) {
    if (!canAnimate(element)) return;
    const style = getComputedStyle(element);
    const duration = parseFloat(style.getPropertyValue(durationToken));
    if (!(duration > 0)) return;

    targets.get(element)?.cancel();
    let animation;
    try {
      animation = element.animate(frames, {
        duration,
        easing: style.getPropertyValue('--motion-ease-out').trim(),
        ...options
      });
    } catch { return; }

    let complete;
    const finished = new Promise(resolve => { complete = resolve; });
    function cancel() {
      if (!active.delete(handle)) return;
      targets.delete(element);
      clearTimeout(deadline);
      try { animation.cancel(); } catch { /* Some implementations only support playback. */ }
      complete();
    }
    const handle = { cancel, finished };
    const deadline = setTimeout(cancel, duration + 120);
    active.add(handle);
    targets.set(element, handle);
    // Cancellation rejects the browser promise. Consumers can always commit their final state.
    if (animation.finished) animation.finished.then(cancel, cancel);
    else animation.addEventListener?.('finish', cancel, { once: true });
    return handle;
  }

  function retire(element, durationToken, frames) {
    const parent = element.parentElement;
    if (!canAnimate(element) || !parent || parent.children.length < 2) {
      element.remove();
      return;
    }
    const start = element.getBoundingClientRect();
    const bounds = parent.getBoundingClientRect();
    element.inert = true;
    element.setAttribute('aria-hidden', 'true');
    element.setAttribute('data-motion-exit', '');
    Object.assign(element.style, {
      position: 'absolute',
      margin: '0',
      pointerEvents: 'none',
      top: (start.top - bounds.top + parent.scrollTop - parent.clientTop) + 'px',
      left: (start.left - bounds.left + parent.scrollLeft - parent.clientLeft) + 'px',
      width: start.width + 'px'
    });
    const animation = animate(element, frames, durationToken, { fill: 'both' });
    if (animation) animation.finished.then(() => element.remove());
    else element.remove();
  }

  const settle = element => targets.get(element)?.cancel();
  const cancelAll = () => active.forEach(animation => animation.cancel());
  document.addEventListener('visibilitychange', () => { if (document.hidden) cancelAll(); });
  window.addEventListener('pagehide', cancelAll);
  window.storefrontMotion = { canAnimate, animate, retire, settle };
})();
