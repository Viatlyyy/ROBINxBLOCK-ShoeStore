(() => {
  const root = document.documentElement;
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  let avatarUploadPending = false;

  const cabinetPath = new URL(document.currentScript.dataset.cabinetRoot, location.href)
    .pathname.toLowerCase().replace(/\/$/, '');

  window.addEventListener('pageswap', event => {
    const transition = event.viewTransition;
    if (!transition) return;
    transition.ready.catch(() => {});

    const destination = event.activation?.entry?.url;
    const path = destination ? new URL(destination).pathname.toLowerCase().replace(/\/$/, '') : '';
    const isCabinet = path === cabinetPath ||
      ["/profile", "/orders", "/favorites"].some(section => path === cabinetPath + section) ||
      path.startsWith(cabinetPath + '/orders/');
    if (!isCabinet || avatarUploadPending) transition.skipTransition();
  });

  window.addEventListener('pagereveal', event => {
    const transition = event.viewTransition;
    root.classList.toggle('account-navigation-transition', Boolean(transition));
    transition?.ready.catch(() => root.classList.remove('account-navigation-transition'));
  });

  function markMissingPhoto(image) {
    image.parentElement.setAttribute('data-photo-unavailable', '');
  }

  document.addEventListener('error', event => {
    if (event.target instanceof HTMLImageElement && event.target.matches('[data-account-photo]')) {
      markMissingPhoto(event.target);
    }
  }, true);

  document.addEventListener('DOMContentLoaded', () => {
    for (const image of document.querySelectorAll('[data-account-photo]')) {
      if (image.complete && image.naturalWidth === 0) markMissingPhoto(image);
    }

    const controls = document.querySelector('[data-recommendation-controls]');
    if (controls) {
      const slides = [...document.querySelectorAll('[data-recommendation]')];
      const buttons = [...controls.querySelectorAll('button')];
      const duration = parseFloat(getComputedStyle(root).getPropertyValue('--account-normal'));
      const easing = getComputedStyle(root).getPropertyValue('--account-ease').trim();
      let selected = 0;
      let animation;
      slides.forEach((slide, index) => {
        slide.hidden = false;
        slide.inert = index !== selected;
        slide.classList.toggle('is-inactive', index !== selected);
      });
      controls.hidden = false;
      controls.addEventListener('click', event => {
        const index = buttons.indexOf(event.target.closest('button'));
        if (index < 0 || index === selected) return;
        animation?.cancel();
        slides[selected].classList.add('is-inactive');
        slides[selected].inert = true;
        buttons[selected].setAttribute('aria-pressed', 'false');
        selected = index;
        const slide = slides[selected];
        slide.classList.remove('is-inactive');
        slide.inert = false;
        buttons[selected].setAttribute('aria-pressed', 'true');
        if (!reducedMotion.matches && slide.animate) {
          animation = slide.animate([{ opacity: 0, translate: '0 6px' }, { opacity: 1, translate: 'none' }],
            { duration, easing });
        }
      });
    }

    const photo = document.querySelector('[data-avatar-max-bytes]');
    if (photo) {
      photo.form.addEventListener('submit', event => {
        if (avatarUploadPending) {
          event.preventDefault();
          return;
        }
        avatarUploadPending = true;
      });
      window.addEventListener('pageshow', () => { avatarUploadPending = false; });
      photo.addEventListener('change', () => {
        const tooLarge = photo.files[0]?.size > Number(photo.dataset.avatarMaxBytes);
        photo.setCustomValidity(tooLarge ? 'Размер файла должен быть не больше 5 МБ.' : '');
        if (photo.files.length > 0) photo.form.requestSubmit();
      });
    }

    const profile = document.querySelector('[data-customer-profile]');
    if (profile) {
      const save = profile.querySelector('button[type="submit"]');
      const label = save.querySelector('[data-profile-label]');
      const progress = profile.querySelector('[data-profile-progress]');
      const initialLabel = label.textContent;
      profile.addEventListener('submit', event => {
        if (save.disabled) {
          event.preventDefault();
          return;
        }
        save.disabled = true;
        save.setAttribute('aria-busy', 'true');
        label.textContent = 'Сохранение…';
        progress.textContent = 'Сохраняем контактные данные.';
      });
      window.addEventListener('pageshow', () => {
        save.disabled = false;
        save.removeAttribute('aria-busy');
        label.textContent = initialLabel;
        progress.textContent = '';
      });
    }
  });
})();
