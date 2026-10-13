(() => {
  const menu = document.querySelector('[data-account-menu]');
  if (!menu) return;
  const trigger = menu.querySelector('[data-account-menu-trigger]');
  const setOpen = open => {
    menu.classList.toggle('is-open', open);
    trigger.setAttribute('aria-expanded', String(open));
  };
  trigger.addEventListener('click', () => setOpen(!menu.classList.contains('is-open')));
  document.addEventListener('click', event => { if (!menu.contains(event.target)) setOpen(false); });
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape') { setOpen(false); trigger.focus(); }
  });
})();
