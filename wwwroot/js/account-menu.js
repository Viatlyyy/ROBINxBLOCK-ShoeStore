(() => {
  const menu = document.querySelector('[data-account-menu]');
  // На странице без меню обработчики не нужны.
  if (!menu) return;
  const trigger = menu.querySelector('[data-account-menu-trigger]');
  // CSS-состояние и aria-expanded обновляем вместе, чтобы видимость и озвучивание не разошлись.
  const setOpen = open => {
    menu.classList.toggle('is-open', open);
    trigger.setAttribute('aria-expanded', String(open));
  };
  trigger.addEventListener('click', () => setOpen(!menu.classList.contains('is-open')));
  // Внешний клик закрывает меню; внутри панели форму выхода не прерываем.
  document.addEventListener('click', event => { if (!menu.contains(event.target)) setOpen(false); });
  document.addEventListener('keydown', event => {
    // Возвращаем фокус на кнопку, чтобы меню можно было снова открыть с клавиатуры.
    if (event.key === 'Escape') { setOpen(false); trigger.focus(); }
  });
})();
