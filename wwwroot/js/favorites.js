(() => {
  const feedback = document.querySelector('[data-favorite-announcement]');
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const here = () => location.pathname + location.search + location.hash;

  function announce(message, isError = false) {
    if (!feedback) return;
    feedback.textContent = message;
    feedback.classList.toggle('sr-only', !isError);
    feedback.classList.toggle('is-error', isError);
  }

  function loginUrl(source) {
    const url = new URL(source, location.origin);
    url.searchParams.set('returnUrl', here());
    return url.href;
  }

  async function removeFromGrid(card, grid, moveFocus) {
    const next = card.nextElementSibling || card.previousElementSibling;
    if (!reducedMotion.matches && card.animate) {
      await card.animate(
        [{ opacity: 1, transform: 'none' }, { opacity: 0, transform: 'translateY(-5px) scale(.99)' }],
        { duration: 200, easing: 'cubic-bezier(.16,1,.3,1)', fill: 'forwards' }
      ).finished.catch(() => {});
    }
    card.remove();
    if (!grid.querySelector('[data-product-card]')) {
      grid.hidden = true;
      document.querySelector('[data-favorite-empty]').hidden = false;
    }
    if (moveFocus) {
      (next?.querySelector('.favorite-button') || document.querySelector('[data-favorite-list-link]'))?.focus();
    }
  }

  document.addEventListener('click', event => {
    const link = event.target.closest('[data-favorite-login]');
    if (link) link.href = loginUrl(link.href);
  });

  document.addEventListener('submit', async event => {
    const form = event.target.closest('[data-favorite-form]');
    if (!form) return;
    form.elements.returnUrl.value = here();

    // The overview retrieves the next most recent favorite on the server after removal.
    if (!window.fetch || form.closest('[data-account-preview]')) return;
    event.preventDefault();
    const button = form.querySelector('button');
    if (button.disabled) return;
    const hadFocus = document.activeElement === button;
    button.disabled = true;
    form.setAttribute('aria-busy', 'true');
    announce('');

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
        await removeFromGrid(card, grid, hadFocus);
      } else if (!reducedMotion.matches) {
        button.classList.add('is-favorite-pulsing');
        setTimeout(() => button.classList.remove('is-favorite-pulsing'), 260);
      }
      announce(result.isFavorite ? 'Товар добавлен в избранное.' : 'Товар убран из избранного.');
    } catch (error) {
      announce(error instanceof TypeError || error instanceof SyntaxError
        ? 'Не удалось получить ответ. Обновите страницу и проверьте избранное.' : error.message, true);
    } finally {
      button.disabled = false;
      form.removeAttribute('aria-busy');
      if (hadFocus && button.isConnected && document.activeElement === document.body) button.focus();
    }
  });

  const profile = document.querySelector('[data-customer-profile]');
  if (!profile) return;
  const save = profile.querySelector('button[type="submit"]');
  const progress = profile.querySelector('[data-profile-progress]');
  const initialLabel = save.textContent;
  profile.addEventListener('submit', () => {
    save.disabled = true;
    save.textContent = 'Сохранение…';
    progress.textContent = 'Сохраняем контактные данные.';
  });
  window.addEventListener('pageshow', () => {
    save.disabled = false;
    save.textContent = initialLabel;
    progress.textContent = '';
  });
})();
