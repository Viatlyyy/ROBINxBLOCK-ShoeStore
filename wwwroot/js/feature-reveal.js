(() => {
  const items = document.querySelectorAll('[data-reveal]');
  if (!items.length) return;

  document.documentElement.classList.add('has-reveal-motion');
  const reveal = element => element.classList.add('is-visible');

  if (!('IntersectionObserver' in window)) {
    items.forEach(reveal);
    return;
  }

  const observer = new IntersectionObserver(entries => {
    entries.forEach(entry => {
      if (!entry.isIntersecting) return;
      reveal(entry.target);
      observer.unobserve(entry.target);
    });
  }, { threshold: .14 });

  items.forEach(item => observer.observe(item));
})();
