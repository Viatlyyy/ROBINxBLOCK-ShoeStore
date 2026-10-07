(() => {
  document.querySelectorAll('[data-quantity-picker]').forEach(picker => {
    const input = picker.querySelector('[data-quantity-input]');
    const decrease = picker.querySelector('[data-quantity-decrease]');
    const increase = picker.querySelector('[data-quantity-increase]');
    const min = Number(input.min) || 1;
    const max = Number(input.max) || 10;

    const setQuantity = value => {
      input.value = String(Math.min(max, Math.max(min, value)));
      decrease.disabled = Number(input.value) <= min;
      increase.disabled = Number(input.value) >= max;
      picker.classList.remove('is-changing');
      requestAnimationFrame(() => picker.classList.add('is-changing'));
      window.setTimeout(() => picker.classList.remove('is-changing'), 260);
    };

    decrease.addEventListener('click', () => setQuantity(Number(input.value) - 1));
    increase.addEventListener('click', () => setQuantity(Number(input.value) + 1));
    setQuantity(Number(input.value));
  });
})();
