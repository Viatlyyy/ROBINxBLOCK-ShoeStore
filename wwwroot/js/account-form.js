(() => {
  // Кнопка ищет поле в своём контейнере, поэтому пароль и подтверждение переключаются отдельно.
  document.querySelectorAll('[data-password-toggle]').forEach(button => {
    button.addEventListener('click', () => {
      const input = button.closest('.account-password')?.querySelector('input');
      if (!input) return;
      // Меняем только видимость значения. Подпись кнопки также обновляем для скринридера.
      const visible = input.type === 'text';
      input.type = visible ? 'password' : 'text';
      button.textContent = visible ? '◉' : '◌';
      button.setAttribute('aria-label', visible ? 'Показать пароль' : 'Скрыть пароль');
    });
  });
})();
