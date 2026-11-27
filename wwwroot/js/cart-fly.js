(() => {
  const form = document.querySelector('[data-add-to-cart-form]');
  const cart = document.querySelector('.header-icon-button[href*="/Cart"]');
  if (!form || !cart || !window.fetch) return;

  const count = cart.querySelector('[data-cart-count]');
  const error = form.querySelector('[data-cart-error]');
  const status = form.querySelector('[data-cart-status]');
  const submit = form.querySelector('.product-order-controls__submit');
  const gallery = document.querySelector('.detail-image');
  let pending = false;
  let flight;

  function flightSource() {
    if (!window.storefrontMotion?.canAnimate(cart)) return;
    const incoming = gallery?.querySelector('[data-gallery-buffer][src]');
    const image = incoming?.naturalWidth ? incoming : gallery?.querySelector('[data-gallery-main]:not([hidden])');
    if (!image?.naturalWidth) return;
    return { src: image.currentSrc || image.src, start: image.getBoundingClientRect() };
  }

  function celebrate(source) {
    const motion = window.storefrontMotion;
    if (!motion || document.hidden) return;

    flight?.cancel();
    if (source && motion.canAnimate(cart)) {
      const finish = cart.getBoundingClientRect();
      const x = source.start.left + source.start.width / 2;
      const y = source.start.top + source.start.height / 2;
      const moveX = finish.left + finish.width / 2 - x;
      const moveY = finish.top + finish.height / 2 - y;
      const flying = new Image();
      flying.className = 'cart-flying-product';
      flying.alt = '';
      flying.setAttribute('aria-hidden', 'true');
      flying.src = source.src;
      flying.style.left = x + 'px';
      flying.style.top = y + 'px';
      document.body.append(flying);

      flight = motion.animate(flying, [
        { transform: 'translate(-50%, -50%) scale(1) rotate(0deg)', opacity: 1 },
        { transform: 'translate(calc(-50% + ' + moveX * .55 + 'px), calc(-50% + ' + (moveY * .32 - 80) +
            'px)) scale(.72) rotate(-13deg)', opacity: .9, offset: .46 },
        { transform: 'translate(calc(-50% + ' + moveX + 'px), calc(-50% + ' + moveY +
            'px)) scale(.08) rotate(18deg)', opacity: 0 }
      ], '--motion-cart-flight');
      if (flight) flight.finished.then(() => flying.remove());
      else flying.remove();
    }

    motion.animate(cart, [
      { scale: '1', rotate: '0deg' },
      { scale: '1.35', rotate: '-9deg', offset: .32 },
      { scale: '.92', rotate: '7deg', offset: .62 },
      { scale: '1', rotate: '0deg' }
    ], '--motion-cart-receive');
    motion.animate(count, [{ scale: '1' }, { scale: '1.48', offset: .42 }, { scale: '1' }], '--motion-accent');
  }

  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (pending) return;
    pending = true;
    error.hidden = true;
    status.textContent = '';
    submit.classList.add('is-adding');
    submit.setAttribute('aria-busy', 'true');
    submit.setAttribute('aria-disabled', 'true');
    let source;

    try {
      source = flightSource();
      const response = await fetch(form.action, {
        method: 'POST',
        body: new FormData(form),
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
      });
      const result = await response.json();
      if (!response.ok) throw new Error(result.message || 'Не удалось добавить товар');
      if (!Number.isFinite(result.count)) throw new SyntaxError('Invalid cart count');
      if (window.storefrontCartBadge) window.storefrontCartBadge.update(result.count);
      else if (count) {
        count.textContent = String(result.count);
        count.classList.toggle('is-visible', result.count > 0);
      }
      status.textContent = 'Товар добавлен в корзину.';
    } catch (failure) {
      error.textContent = failure instanceof TypeError || failure instanceof SyntaxError
        ? 'Не удалось получить ответ. Проверьте корзину перед повторным добавлением.'
        : failure.message;
      error.hidden = false;
    } finally {
      pending = false;
      submit.classList.remove('is-adding');
      submit.removeAttribute('aria-busy');
      submit.removeAttribute('aria-disabled');
    }
    if (!error.hidden) return;
    celebrate(source);
  });
})();
