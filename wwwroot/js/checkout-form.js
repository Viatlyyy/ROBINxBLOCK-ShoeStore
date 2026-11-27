(() => {
  const form = document.querySelector('[data-checkout-form]');
  if (!form) return;
  const city = form.querySelector('[data-delivery-city]');
  const address = form.querySelector('[data-delivery-address]');
  const fields = form.querySelector('[data-delivery-fields]');
  const submit = form.querySelector('button[type="submit"]');
  const label = submit.querySelector('[data-checkout-submit-label]');
  const arrow = submit.querySelector('svg');
  const spinner = submit.querySelector('.checkout-spinner');
  const progress = form.querySelector('[data-checkout-progress]');
  let addressAnimation;
  let sending = false;
  form.setAttribute('data-delivery-enhanced', '');

  function setVisible(group, visible) {
    group.hidden = !visible;
    for (const control of group.querySelectorAll('input, select')) control.disabled = !visible;
  }

  function updateDelivery(animate) {
    const selected = form.querySelector('[data-delivery-option]:checked')?.dataset.deliveryOption;
    const isCourier = selected === 'courier';
    const needsCity = isCourier || selected === 'collection';
    const animateHeight = animate && window.storefrontMotion?.canAnimate(fields);
    const previousHeight = animateHeight ? fields.getBoundingClientRect().height : 0;
    addressAnimation?.cancel();

    setVisible(city, needsCity);
    setVisible(address, isCourier);
    form.elements.City.required = needsCity;
    form.elements.Street.required = isCourier;
    form.elements.House.required = isCourier;

    const height = animateHeight ? fields.getBoundingClientRect().height : 0;
    if (height !== previousHeight) {
      addressAnimation = window.storefrontMotion.animate(fields,
        [{ height: previousHeight + 'px', overflow: 'clip' }, { height: height + 'px', overflow: 'clip' }],
        '--motion-expand'
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
    label.textContent = pending ? 'Оформляем заказ…' : 'Подтвердить заказ';
    arrow.toggleAttribute('hidden', pending);
    spinner.hidden = !pending;
    progress.textContent = pending ? 'Оформляем заказ.' : '';
  }

  updateDelivery(false);

  form.addEventListener('change', event => {
    if (event.target.matches('[data-delivery-option]')) updateDelivery(true);
  });

  function fieldError(input, message) {
    const error = document.getElementById(input.getAttribute('aria-describedby'));
    if (!error) return false;
    if (message) input.setAttribute('aria-invalid', 'true');
    else input.removeAttribute('aria-invalid');
    error.textContent = message;
    error.classList.toggle('field-validation-error', Boolean(message));
    error.classList.toggle('field-validation-valid', !message);
    return true;
  }

  form.addEventListener('invalid', event => {
    const input = event.target;
    const requiredMessage = input.dataset.checkoutRequiredMessage || input.getAttribute('data-val-required');
    const emailMessage = input.getAttribute('data-val-email');
    const message = (input.validity.valueMissing && requiredMessage) ||
      (input.validity.typeMismatch && emailMessage) || input.validationMessage;
    if (fieldError(input, message)) {
      event.preventDefault();
      if (input === [...form.elements].find(element => element.willValidate && !element.validity.valid)) {
        input.focus();
      }
    }
  }, true);

  form.addEventListener('input', event => {
    const input = event.target;
    if (input.hasAttribute('aria-invalid') && input.validity?.valid && (!input.required || input.value.trim())) {
      input.classList.remove('input-validation-error');
      fieldError(input, '');
    }
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
