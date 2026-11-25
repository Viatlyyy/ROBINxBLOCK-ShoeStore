(() => {
  const form = document.querySelector('[data-checkout-form]');
  const city = form.querySelector('[data-delivery-city]');
  const address = form.querySelector('[data-delivery-address]');
  const fields = form.querySelector('[data-delivery-fields]');
  const submit = form.querySelector('button[type="submit"]');
  const label = submit.querySelector('[data-checkout-submit-label]');
  const arrow = submit.querySelector('svg');
  const spinner = submit.querySelector('.checkout-spinner');
  const progress = form.querySelector('[data-checkout-progress]');
  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
  const easing = getComputedStyle(form).getPropertyValue('--purchase-ease').trim();
  let addressAnimation;
  let sending = false;

  function setVisible(group, visible) {
    group.hidden = !visible;
    for (const control of group.querySelectorAll('input, select')) control.disabled = !visible;
  }

  function updateDelivery(animate) {
    const selected = form.querySelector('[data-delivery-option]:checked')?.dataset.deliveryOption;
    const isCourier = selected === 'courier';
    const needsCity = isCourier || selected === 'collection';
    const previousHeight = fields.getBoundingClientRect().height;
    addressAnimation?.cancel();

    setVisible(city, needsCity);
    setVisible(address, isCourier);
    form.elements.City.required = needsCity;
    form.elements.Street.required = isCourier;
    form.elements.House.required = isCourier;

    const height = fields.getBoundingClientRect().height;
    if (animate && !reducedMotion.matches && fields.animate && height !== previousHeight) {
      addressAnimation = fields.animate(
        [{ height: previousHeight + 'px', overflow: 'clip' }, { height: height + 'px', overflow: 'clip' }],
        { duration: 240, easing }
      );
    }
  }

  function setPending(pending) {
    sending = pending;
    if (pending) {
      submit.setAttribute('aria-busy', 'true');
      submit.setAttribute('aria-disabled', 'true');
    } else {
      submit.removeAttribute('aria-busy');
      submit.removeAttribute('aria-disabled');
    }
    label.textContent = pending ? 'Подтверждаем…' : 'Подтвердить заказ';
    arrow.toggleAttribute('hidden', pending);
    spinner.hidden = !pending;
    progress.textContent = pending ? 'Подтверждаем заказ.' : '';
  }

  updateDelivery(false);

  form.addEventListener('change', event => {
    if (event.target.matches('[data-delivery-option]')) updateDelivery(true);
  });

  form.addEventListener('submit', event => {
    if (sending || submit.disabled) {
      event.preventDefault();
      return;
    }
    if (!event.defaultPrevented) setPending(true);
  });

  window.addEventListener('pageshow', () => {
    updateDelivery(false);
    setPending(false);
  });

  for (const input of form.querySelectorAll('.input-validation-error')) input.setAttribute('aria-invalid', 'true');
  const error = form.querySelector('.validation-summary-errors') ||
    form.querySelector('.input-validation-error:not(:disabled)');
  if (error) error.focus();
})();
