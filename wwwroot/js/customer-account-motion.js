window.addEventListener('pagereveal', event => {
  const root = document.documentElement;
  const active = event.viewTransition;
  root.classList.toggle('account-navigation-transition', Boolean(active));
  if (active) active.ready.catch(() => root.classList.remove('account-navigation-transition'));
});
