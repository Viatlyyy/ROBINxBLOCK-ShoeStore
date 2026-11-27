(() => {
  const cart = document.querySelector('.cart-page');
  if (!cart || !window.fetch) return;
  const motion = window.storefrontMotion;
  const list = cart.querySelector('[data-cart-lines]');
  const contents = cart.querySelector('[data-cart-content]');
  const empty = cart.querySelector('[data-cart-empty]');
  const label = cart.querySelector('[data-cart-items-label]');
  const subtotal = cart.querySelector('[data-cart-subtotal]');
  const total = cart.querySelector('[data-cart-total]');
  const checkout = cart.querySelector('[data-cart-checkout]');
  const error = cart.querySelector('[data-cart-error]');
  const notice = cart.querySelector('[data-cart-notice]');
  const announcement = cart.querySelector('[data-cart-announcement]');
  const badge = document.querySelector('[data-cart-count]');
  const currency = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 });
  const money = value => currency.format(value) + ' ₽';
  const queue = [];
  let processing = false;
  let needsRefresh = false;

  function updateText(element, text) {
    if (element.textContent === String(text)) return;
    element.textContent = text;
    motion?.animate(element, [{ opacity: .55, transform: 'translateY(3px)' }, { opacity: 1, transform: 'none' }], '--motion-value');
  }

  function updateLine(row, state) {
    const active = document.activeElement;
    const minus = row.querySelector('button[value="-1"]');
    const plus = row.querySelector('button[value="1"]');
    minus.disabled = state.quantity <= 1;
    plus.disabled = state.quantity >= state.maximumQuantity;
    if ((active === minus || active === plus) && active.disabled) {
      (active === plus && !minus.disabled ? minus : !plus.disabled ? plus : row.querySelector('h3 a'))
        .focus({ preventScroll: true });
    }

    updateText(row.querySelector('[data-cart-line-quantity]'), state.quantity);
    updateText(row.querySelector('[data-cart-line-total]'), money(state.total));
  }

  function removeRows(rows, removed, source) {
    const remaining = rows.filter(row => !removed.includes(row));
    const animate = remaining.length && motion?.canAnimate(remaining[0]);
    const positions = animate ? remaining.map(row => row.getBoundingClientRect().top) : [];
    const focusWasRemoved = removed.some(row => row.contains(document.activeElement));
    for (const row of removed) {
      if (animate) motion.retire(row, '--motion-exit',
        [{ opacity: 1 }, { opacity: 0, transform: 'translateX(-6px)' }]);
      else row.remove();
    }
    if (animate) {
      remaining.forEach(motion.settle);
      const destinations = remaining.map(row => row.getBoundingClientRect().top);
      remaining.forEach((row, index) => {
        const shift = positions[index] - destinations[index];
        if (shift) motion.animate(row, [{ transform: 'translateY(' + shift + 'px)' }, { transform: 'none' }],
          '--motion-control');
      });
    }
    if (focusWasRemoved && remaining.length) {
      const next = rows.slice(rows.indexOf(source) + 1).find(row => remaining.includes(row)) || remaining.at(-1);
      next.querySelector('[data-cart-remove-form] button').focus({ preventScroll: true });
    }
    return focusWasRemoved;
  }

  function render(result, source, removing) {
    const rows = [...list.querySelectorAll('[data-cart-line]:not([data-motion-exit])')];
    const currentKeys = new Set(rows.map(row => row.dataset.cartKey));
    const byKey = new Map(result.lines.map(line => [line.variantId + ':' + line.size, line]));
    if ([...byKey.keys()].some(key => !currentKeys.has(key))) {
      needsRefresh = true;
      queue.length = 0;
      location.reload();
      return;
    }

    const previousQuantity = Number(source.querySelector('[data-cart-line-quantity]').textContent);
    const selected = byKey.get(source.dataset.cartKey);
    const removed = rows.filter(row => !byKey.has(row.dataset.cartKey));
    const removedFocus = removed.length && removeRows(rows, removed, source);

    for (const row of rows) {
      const state = byKey.get(row.dataset.cartKey);
      if (state) updateLine(row, state);
    }

    updateText(label, result.itemsLabel);
    updateText(subtotal, money(result.total));
    updateText(total, money(result.total));
    if (window.storefrontCartBadge) window.storefrontCartBadge.update(result.count);
    else {
      badge.textContent = result.count;
      badge.classList.toggle('is-visible', result.count > 0);
    }
    if (result.count) motion?.animate(badge, [{ scale: '.85' }, { scale: '1' }], '--motion-value');

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

  async function processQueue() {
    if (processing) return;
    processing = true;
    checkout.setAttribute('aria-disabled', 'true');
    try {
      while (queue.length) {
        const { form, submitter, data, removing } = queue.shift();
        if (form.isConnected && !form.closest('[data-motion-exit]') && !submitter.disabled) {
          const response = await fetch(form.action, {
            method: 'POST',
            body: data,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
          });
          if (!response.ok) throw new Error('Cart update failed');
          render(await response.json(), form.closest('[data-cart-line]'), removing);
        }
        if (!queue.some(item => item.form === form)) form.removeAttribute('aria-busy');
      }
    } catch {
      // The server may have saved the last change even if the response was lost.
      // Discard queued writes until the page has read the latest server state.
      needsRefresh = true;
      queue.length = 0;
      cart.querySelectorAll('form[aria-busy]').forEach(form => form.removeAttribute('aria-busy'));
      error.querySelector('[data-cart-error-message]').textContent = 'Не удалось проверить изменения. Обновите корзину перед оформлением.';
      error.hidden = false;
    } finally {
      processing = false;
      if (!needsRefresh) checkout.removeAttribute('aria-disabled');
    }
  }

  cart.addEventListener('submit', event => {
    const form = event.target.closest('[data-cart-quantity-form], [data-cart-remove-form]');
    if (!form || !event.submitter || form.closest('[data-motion-exit]')) return;
    event.preventDefault();
    if (needsRefresh) {
      error.querySelector('a').focus();
      return;
    }

    const removing = form.hasAttribute('data-cart-remove-form');
    if (removing && form.hasAttribute('aria-busy')) return;
    if (!processing) notice.textContent = '';
    queue.push({ form, submitter: event.submitter, data: new FormData(form, event.submitter), removing });
    form.setAttribute('aria-busy', 'true');
    void processQueue();
  });

  checkout.addEventListener('click', event => {
    if (checkout.getAttribute('aria-disabled') === 'true') event.preventDefault();
  });

  window.addEventListener('pageshow', event => {
    if (event.persisted) location.reload();
  });
})();
