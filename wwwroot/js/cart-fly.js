(() => {
  const form = document.querySelector('[data-add-to-cart-form]');
  const cart = document.querySelector('.header-icon-button[href*="/Cart"]');
  const count = document.querySelector('[data-cart-count]');
  const productImage = document.querySelector('.detail-image img');
  if (!form || !cart || !window.fetch) return;
  const error = form.querySelector('[data-cart-error]');

  form.addEventListener('submit', event => {
    if (form.dataset.isAnimating === 'true') { event.preventDefault(); return; }
    event.preventDefault();
    form.dataset.isAnimating = 'true';

    const submit = form.querySelector('.product-order-controls__submit');
    error.hidden = true;
    submit?.classList.add('is-adding');
    let flying;
    let animation;
    if (productImage && productImage.animate) {
      const start = productImage.getBoundingClientRect();
      const finish = cart.getBoundingClientRect();
      flying = productImage.cloneNode(false);
      flying.className = 'cart-flying-product';
      flying.removeAttribute('data-variant-image');
      flying.setAttribute('alt', '');
      flying.setAttribute('aria-hidden', 'true');
      flying.style.left = `${start.left + start.width / 2 - 90}px`;
      flying.style.top = `${start.top + start.height / 2 - 60}px`;
      document.body.append(flying);

      const moveX = finish.left + finish.width / 2 - (start.left + start.width / 2);
      const moveY = finish.top + finish.height / 2 - (start.top + start.height / 2);
      animation = flying.animate([
        { transform: 'translate(0, 0) scale(1) rotate(0deg)', opacity: 1 },
        { transform: `translate(${moveX * .55}px, ${moveY * .32 - 80}px) scale(.72) rotate(-13deg)`, opacity: .9, offset: .46 },
        { transform: `translate(${moveX}px, ${moveY}px) scale(.08) rotate(18deg)`, opacity: 0 }
      ], { duration: 760, easing: 'cubic-bezier(.16,.84,.25,1)', fill: 'forwards' });

      window.setTimeout(() => {
        cart.classList.remove('is-cart-receiving');
        requestAnimationFrame(() => cart.classList.add('is-cart-receiving'));
      }, 540);
    }
    const request = fetch(form.action, {
      method: 'POST',
      body: new FormData(form),
      headers: { 'X-Requested-With': 'XMLHttpRequest' }
    });
    void (async () => {
      try {
        const [, response] = await Promise.all([animation?.finished.catch(() => {}), request]);
        const result = await response.json();
        if (!response.ok) throw new Error(result.message || 'Не удалось добавить товар');
        if (count && Number.isFinite(result.count)) {
          count.textContent = String(result.count);
          count.classList.add('is-visible');
          count.classList.remove('is-updating');
          requestAnimationFrame(() => count.classList.add('is-updating'));
          window.setTimeout(() => count.classList.remove('is-updating'), 560);
        }
      } catch (failure) {
        error.textContent = failure instanceof TypeError || failure instanceof SyntaxError
          ? 'Не удалось получить ответ. Проверьте корзину перед повторным добавлением.'
          : failure.message;
        error.hidden = false;
      } finally {
        flying?.remove();
        submit?.classList.remove('is-adding');
        form.dataset.isAnimating = 'false';
      }
    })();
  });
})();
