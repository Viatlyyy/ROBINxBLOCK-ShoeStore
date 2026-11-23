(() => {
  const form = document.querySelector('[data-product-options]');
  if (!form) return;

  const picker = form.querySelector('.quantity-picker');
  const input = picker.querySelector('[data-quantity-input]');
  const decrease = picker.querySelector('[data-quantity-decrease]');
  const increase = picker.querySelector('[data-quantity-increase]');
  let pulseTimer;

  function update(value, animate) {
    const size = form.querySelector('input[name="size"]:checked');
    const maximum = Math.min(Number(size?.dataset.stock ?? 1), Number(form.dataset.quantityLimit));
    const quantity = Math.max(1, Math.min(value, maximum));
    input.max = String(maximum);
    input.value = String(quantity);
    decrease.disabled = quantity <= 1;
    increase.disabled = quantity >= maximum;
    if (animate) {
      clearTimeout(pulseTimer);
      picker.classList.remove('is-changing');
      requestAnimationFrame(() => picker.classList.add('is-changing'));
      pulseTimer = setTimeout(() => picker.classList.remove('is-changing'), 260);
    }
  }

  form.addEventListener('change', event => {
    if (event.target.name === 'size') update(1, input.value !== '1');
  });
  decrease.addEventListener('click', () => update(Number(input.value) - 1, true));
  increase.addEventListener('click', () => update(Number(input.value) + 1, true));
  window.addEventListener('pageshow', event => {
    if (event.persisted) form.reset();
    update(1, false);
  });
  update(1, false);
})();
