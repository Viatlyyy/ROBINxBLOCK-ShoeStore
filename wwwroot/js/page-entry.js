(() => {
  const root = document.documentElement;
  const navigation = performance.getEntriesByType?.('navigation')[0];
  if (!document.hidden && navigation?.type !== 'back_forward' && !location.hash) {
    root.dataset.motionEntry = '';
  }

  window.addEventListener('pagehide', () => { delete root.dataset.motionEntry; });
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) delete root.dataset.motionEntry;
  });
})();
