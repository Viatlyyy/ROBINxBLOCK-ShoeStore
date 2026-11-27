(() => {
  const feedback = document.querySelector('[data-favorite-announcement]');
  const errorFeedback = document.querySelector('[data-favorite-error]');
  let failedProduct;
  const motion = window.storefrontMotion;
  const here = () => location.pathname + location.search + location.hash;
  const revisionKey = 'storefront:favorites-revision';
  let revision = readRevision();

  function readRevision() {
    try { return localStorage.getItem(revisionKey); }
    catch { return null; }
  }

  function markChanged() {
    try {
      const next = Date.now().toString(36) + Math.random().toString(36).slice(2);
      localStorage.setItem(revisionKey, next);
      revision = next;
    } catch { revision = null; }
  }

  window.addEventListener('pageshow', event => {
    if (event.persisted && revision !== readRevision()) location.reload();
  });

  function showError(message, productId) {
    if (!errorFeedback) return;
    failedProduct = productId;
    errorFeedback.textContent = message;
    errorFeedback.hidden = false;
  }

  function clearError(productId) {
    if (failedProduct !== productId) return;
    failedProduct = undefined;
    errorFeedback.textContent = '';
    errorFeedback.hidden = true;
  }

  function loginUrl(source) {
    const url = new URL(source, location.origin);
    url.searchParams.set('returnUrl', here());
    return url.href;
  }

  function removeFromGrid(card, grid, moveFocus) {
    const remaining = [...grid.children].filter(item => item !== card && !item.hasAttribute('data-motion-exit'));
    const neighbors = [card.nextElementSibling, card.previousElementSibling, ...remaining]
      .filter(item => remaining.includes(item));
    const animate = remaining.length && motion?.canAnimate(card);
    const before = animate ? remaining.map(item => item.getBoundingClientRect()) : [];
    const focusWasInCard = card.contains(document.activeElement);
    if (animate) motion.retire(card, '--motion-control', [{ opacity: 1, scale: 1 }, { opacity: 0, scale: .97 }]);
    else card.remove();

    const content = grid.closest('#account-content');
    const count = content.querySelector('[data-favorites-count]');
    count.textContent = remaining.length;

    const empty = content.querySelector('[data-favorite-empty]');
    if (remaining.length === 0) {
      grid.hidden = true;
      empty.hidden = false;
    } else if (animate) {
      remaining.forEach(motion.settle);
      const after = remaining.map(item => item.getBoundingClientRect());
      remaining.forEach((item, index) => {
        const x = before[index].left - after[index].left;
        const y = before[index].top - after[index].top;
        if (x || y) {
          motion.animate(item, [{ translate: x + 'px ' + y + 'px' }, { translate: 'none' }],
            '--motion-control');
        }
      });
    }
    if (focusWasInCard || (moveFocus && document.activeElement === document.body)) {
      const next = neighbors.find(item => item !== card && item?.querySelector('.favorite-button:not(:disabled)'));
      const fallback = remaining.length === 0 ? empty.querySelector('a') : content.querySelector('h1');
      (next?.querySelector('.favorite-button') || fallback)?.focus();
    }
  }

  document.addEventListener('click', event => {
    const link = event.target.closest('[data-favorite-login]');
    if (link) link.href = loginUrl(link.href);
  });

  document.addEventListener('submit', async event => {
    const form = event.target.closest('[data-favorite-form]');
    if (!form || form.closest('[data-motion-exit]')) return;
    form.elements.returnUrl.value = here();
    if (!window.fetch) return;
    event.preventDefault();
    const button = form.querySelector('button');
    if (button.disabled) return;
    const hadFocus = document.activeElement === button;
    button.disabled = true;
    form.setAttribute('aria-busy', 'true');
    const productId = form.elements.productId.value;
    clearError(productId);
    if (feedback) feedback.textContent = '';

    try {
      const response = await fetch(form.action, {
        method: 'POST',
        body: new FormData(form),
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
      });
      if (response.status === 401 || response.redirected) {
        location.assign(loginUrl(form.dataset.favoriteLoginUrl));
        return;
      }
      if (!response.ok) {
        if (response.status === 404) throw new Error('Товар сейчас недоступен.');
        throw new Error('Не удалось обновить избранное. Повторите попытку.');
      }

      const result = await response.json();
      if (typeof result.isFavorite !== 'boolean') throw new Error('Не удалось проверить избранное. Обновите страницу.');
      markChanged();
      for (const current of document.querySelectorAll('[data-favorite-form]')) {
        if (current.elements.productId.value !== String(result.productId)) continue;
        current.elements.isFavorite.value = String(!result.isFavorite);
        const control = current.querySelector('button');
        control.setAttribute('aria-pressed', String(result.isFavorite));
        control.title = result.isFavorite ? 'Убрать из избранного' : 'Добавить в избранное';
      }

      const card = form.closest('[data-product-card]');
      const grid = card?.closest('[data-favorites-list]');
      if (grid && !result.isFavorite) {
        removeFromGrid(card, grid, hadFocus);
      } else {
        motion?.animate(button.querySelector('svg'), [{ scale: '1' }, { scale: '1.27' }, { scale: '1' }],
          '--motion-menu');
      }
      if (feedback) feedback.textContent = result.isFavorite ? 'Товар добавлен в избранное.' : 'Товар убран из избранного.';
    } catch (error) {
      showError(error instanceof TypeError || error instanceof SyntaxError
        ? 'Не удалось получить ответ. Обновите страницу и проверьте избранное.' : error.message, productId);
    } finally {
      button.disabled = false;
      form.removeAttribute('aria-busy');
      if (hadFocus && button.isConnected && document.activeElement === document.body) button.focus();
    }
  });

})();
