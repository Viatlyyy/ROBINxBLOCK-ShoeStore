(() => {
  const money = value => `${new Intl.NumberFormat('ru-RU').format(value)} ₽`;
  const itemsLabel = count => {
    const remainder = count % 10;
    const tens = count % 100;
    const word = remainder === 1 && tens !== 11 ? 'товар' : remainder >= 2 && remainder <= 4 && (tens < 12 || tens > 14) ? 'товара' : 'товаров';
    return `${count} ${word}`;
  };
  const pulse = element => {
    if (!element) return;
    element.classList.remove('is-updated');
    requestAnimationFrame(() => element.classList.add('is-updated'));
    window.setTimeout(() => element.classList.remove('is-updated'), 480);
  };

  let previousUpdate = Promise.resolve();
  document.querySelectorAll('[data-cart-quantity-form]').forEach(form => {
    form.addEventListener('submit', async event => {
      event.preventDefault();
      const submitter = event.submitter;
      if (!submitter || form.classList.contains('is-updating')) return;

      const body = new FormData(form);
      body.append(submitter.name, submitter.value);
      form.classList.add('is-updating');
      const pending = previousUpdate;
      let finish;
      previousUpdate = new Promise(resolve => { finish = resolve; });
      await pending;

      try {
        const response = await fetch(form.action, {
          method: 'POST',
          body,
          headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        if (!response.ok) throw new Error('Could not update cart quantity');

        const result = await response.json();
        if (result.quantity === 0 || result.lineCount !== document.querySelectorAll('[data-cart-line]').length) {
          window.location.reload();
          return;
        }
        const line = form.closest('[data-cart-line]');
        const quantity = line?.querySelector('[data-cart-line-quantity]');
        const lineTotal = line?.querySelector('[data-cart-line-total]');
        const subtotal = document.querySelector('[data-cart-subtotal]');
        const total = document.querySelector('[data-cart-total]');
        const label = document.querySelector('[data-cart-items-label]');
        const cartCount = document.querySelector('[data-cart-count]');

        if (quantity) { quantity.textContent = result.quantity; pulse(quantity); }
        const displayedCount = [...document.querySelectorAll('[data-cart-line-quantity]')]
          .reduce((sum, item) => sum + Number(item.textContent), 0);
        if (displayedCount !== result.count) {
          window.location.reload();
          return;
        }
        if (lineTotal) { lineTotal.textContent = money(result.lineTotal); pulse(lineTotal); }
        if (subtotal) { subtotal.textContent = money(result.total); pulse(subtotal); }
        if (total) { total.textContent = money(result.total); pulse(total); }
        if (label) label.textContent = itemsLabel(result.count);
        if (cartCount) {
          cartCount.textContent = result.count;
          cartCount.classList.toggle('is-visible', result.count > 0);
          cartCount.classList.remove('is-updating');
          requestAnimationFrame(() => cartCount.classList.add('is-updating'));
          window.setTimeout(() => cartCount.classList.remove('is-updating'), 560);
        }
      } catch {
        window.location.reload();
      } finally {
        finish();
        form.classList.remove('is-updating');
      }
    });
  });
})();
