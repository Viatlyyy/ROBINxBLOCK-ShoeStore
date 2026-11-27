(() => {
  const menu = document.querySelector('[data-account-menu]');
  if (!menu) return;
  const trigger = menu.querySelector('[data-account-menu-trigger]');
  const fallback = menu.querySelector('[data-account-menu-fallback]');
  const panel = menu.querySelector('[data-account-menu-panel]');
  const items = [...panel.querySelectorAll('[role="menuitem"]')];

  function setOpen(open) {
    menu.classList.toggle('is-open', open);
    trigger.setAttribute('aria-expanded', String(open));
    panel.toggleAttribute('inert', !open);
    panel.setAttribute('aria-hidden', String(!open));
    for (const item of items) item.tabIndex = -1;
  }

  trigger.hidden = false;
  fallback.hidden = true;
  setOpen(false);
  trigger.addEventListener('click', () => {
    const open = trigger.getAttribute('aria-expanded') !== 'true';
    setOpen(open);
    if (open) items[0]?.focus();
  });
  document.addEventListener('click', event => { if (!menu.contains(event.target)) setOpen(false); });
  menu.addEventListener('focusout', event => {
    if (event.relatedTarget && !menu.contains(event.relatedTarget)) setOpen(false);
  });

  menu.addEventListener('keydown', event => {
    if (!['Escape', 'ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
    const open = trigger.getAttribute('aria-expanded') === 'true';
    if (event.key === 'Escape') {
      if (!open) return;
      event.preventDefault();
      trigger.focus();
      setOpen(false);
      return;
    }
    event.preventDefault();
    if (!open) setOpen(true);
    const index = items.indexOf(document.activeElement);
    const target = {
      ArrowDown: (index + 1) % items.length,
      ArrowUp: index <= 0 ? items.length - 1 : index - 1,
      Home: 0,
      End: items.length - 1
    }[event.key];
    items[target]?.focus();
  });
  window.addEventListener('pagehide', () => { if (panel.contains(document.activeElement)) trigger.focus(); setOpen(false); });
})();
