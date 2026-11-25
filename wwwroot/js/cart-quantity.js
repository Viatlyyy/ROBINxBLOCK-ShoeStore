(() => {
  const cart = document.querySelector('.cart-page');
  const list = cart.querySelector('[data-cart-lines]');
  const contents = cart.querySelector('[data-cart-content]');
  const empty = cart.querySelector('[data-cart-empty]');
  const label = cart.querySelector('[data-cart-items-label]');
  const summaryCount = cart.querySelector('[data-cart-summary-count]');
  const subtotal = cart.querySelector('[data-cart-subtotal]');
  const total = cart.querySelector('[data-cart-total]');
  const checkout = cart.querySelector('[data-cart-checkout]');
  const error = cart.querySelector('[data-cart-error]');
  const notice = cart.querySelector('[data-cart-notice]');
  const announcement = cart.querySelector('[data-cart-announcement]');
  const badge = document.querySelector('[data-cart-count]');
  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
  const easing = getComputedStyle(cart).getPropertyValue('--purchase-ease').trim();
  const currency = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 });
  const money = value => currency.format(value) + ' ₽';
  let pending = false;

  function animate(element, frames, duration) {
    if (!reducedMotion.matches && element.animate) {
      return element.animate(frames, { duration, easing }).finished.catch(() => {});
    }
  }

  function updateText(element, text) {
    if (element.textContent === String(text)) return;
    element.textContent = text;
    animate(element, [{ opacity: .55, transform: 'translateY(3px)' }, { opacity: 1, transform: 'none' }], 190);
  }

  function updateLine(row, state) {
    const active = document.activeElement;
    const minus = row.querySelector('button[value="-1"]');
    const plus = row.querySelector('button[value="1"]');
    minus.disabled = state.quantity <= 1;
    plus.disabled = state.quantity >= state.maximumQuantity;
    if ((active === minus || active === plus) && active.disabled) {
      (active === plus && !minus.disabled ? minus : !plus.disabled ? plus : row.querySelector('h2 a'))
        .focus({ preventScroll: true });
    }

    updateText(row.querySelector('[data-cart-line-quantity]'), state.quantity);
    updateText(row.querySelector('[data-cart-line-total]'), money(state.total));
  }

  async function removeRows(rows, removed, source) {
    const focusWasRemoved = removed.some(row => row.contains(document.activeElement));
    await Promise.all(removed.map(row => animate(row, [{ opacity: 1 }, { opacity: 0, transform: 'translateX(-6px)' }], 135)));
    const remaining = rows.filter(row => !removed.includes(row));
    const positions = remaining.map(row => row.getBoundingClientRect().top);
    removed.forEach(row => row.remove());
    remaining.forEach((row, index) => {
      const shift = positions[index] - row.getBoundingClientRect().top;
      if (shift) animate(row, [{ transform: 'translateY(' + shift + 'px)' }, { transform: 'none' }], 200);
    });
    if (focusWasRemoved && remaining.length) {
      const next = rows.slice(rows.indexOf(source) + 1).find(row => remaining.includes(row)) || remaining.at(-1);
      next.querySelector('[data-cart-remove-form] button').focus({ preventScroll: true });
    }
    return focusWasRemoved;
  }

  async function render(result, source, removing) {
    const rows = [...list.querySelectorAll('[data-cart-line]')];
    const currentKeys = new Set(rows.map(row => row.dataset.cartKey));
    const byKey = new Map(result.lines.map(line => [line.variantId + ':' + line.size, line]));
    if ([...byKey.keys()].some(key => !currentKeys.has(key))) {
      location.reload();
      return;
    }

    const previousQuantity = Number(source.querySelector('[data-cart-line-quantity]').textContent);
    const selected = byKey.get(source.dataset.cartKey);
    const removed = rows.filter(row => !byKey.has(row.dataset.cartKey));
    const removedFocus = removed.length && await removeRows(rows, removed, source);

    for (const row of rows) {
      const state = byKey.get(row.dataset.cartKey);
      if (state) updateLine(row, state);
    }

    label.textContent = result.itemsLabel + ' в корзине';
    label.hidden = !result.count;
    updateText(summaryCount, result.count);
    updateText(subtotal, money(result.total));
    updateText(total, money(result.total));
    badge.textContent = result.count;
    badge.classList.toggle('is-visible', result.count > 0);
    if (result.count) animate(badge, [{ scale: '.85' }, { scale: '1' }], 190);

    contents.hidden = !result.count;
    empty.hidden = Boolean(result.count);
    if (removedFocus && !result.count) empty.querySelector('h2').focus({ preventScroll: true });

    if (removed.some(row => row !== source || !removing)) {
      notice.textContent = 'Недоступные размеры удалены. Проверьте товары в корзине.';
    } else if (selected && selected.quantity === previousQuantity) {
      notice.textContent = 'Доступное количество для этого размера не изменилось.';
    }
    announcement.textContent = removing
      ? source.dataset.cartName + ' удалён. ' + label.textContent + '.'
      : label.textContent + '. Сумма за товары ' + money(result.total) + '.';
  }

  cart.addEventListener('submit', async event => {
    const form = event.target.closest('[data-cart-quantity-form], [data-cart-remove-form]');
    if (!form) return;
    event.preventDefault();
    if (pending || !event.submitter) return;

    pending = true;
    form.setAttribute('aria-busy', 'true');
    checkout.setAttribute('aria-disabled', 'true');
    error.hidden = true;
    notice.textContent = '';

    const data = new FormData(form, event.submitter);
    const source = form.closest('[data-cart-line]');
    try {
      const response = await fetch(form.action, {
        method: 'POST',
        body: data,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
      });
      if (!response.ok) throw new Error('Cart update failed');
      await render(await response.json(), source, form.hasAttribute('data-cart-remove-form'));
    } catch {
      error.querySelector('[data-cart-error-message]').textContent = 'Не удалось проверить изменения. Обновите корзину перед оформлением.';
      error.hidden = false;
    } finally {
      pending = false;
      form.removeAttribute('aria-busy');
      if (error.hidden) checkout.removeAttribute('aria-disabled');
    }
  });

  checkout.addEventListener('click', event => {
    if (checkout.getAttribute('aria-disabled') === 'true') event.preventDefault();
  });

  window.addEventListener('pageshow', event => {
    if (event.persisted) location.reload();
  });
})();
