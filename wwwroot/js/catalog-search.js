(() => {
  const form = document.getElementById('catalog-filters');
  const search = document.getElementById('catalog-search');
  const results = document.getElementById('catalog-results');
  const error = document.getElementById('catalog-error');
  const panel = document.querySelector('.catalog-filter-panel');
  if (panel) {
    const mobile = window.matchMedia('(max-width: 760px)');
    const updatePanel = () => { panel.open = !mobile.matches; };
    updatePanel();
    mobile.addEventListener('change', updatePanel);
  }
  if (!form || !search || !results || !error || !window.fetch || typeof AbortController !== 'function') return;

  // Поиск работает сразу, остальные настройки применяются только кнопкой «Показать».
  let appliedFilters = new URLSearchParams(new FormData(form));
  let pendingRequest;
  let pendingFilters;

  async function updateResults(filters = appliedFilters) {
    const request = new AbortController();
    pendingRequest = request;
    const parameters = new URLSearchParams(filters);
    const searchText = search.value.trim();
    if (searchText) parameters.set('search', searchText);
    else parameters.delete('search');

    const url = new URL(results.dataset.url, window.location.href);
    url.search = parameters.toString();
    error.hidden = true;

    try {
      const response = await fetch(url, { signal: request.signal });
      if (!response.ok) throw new Error('Не удалось загрузить каталог');

      const html = await response.text();
      if (request.signal.aborted) return;

      appliedFilters = filters;
      results.innerHTML = html;
      const pageUrl = new URL(form.action);
      pageUrl.search = parameters.toString();
      history.replaceState(null, '', pageUrl);
    } catch {
      if (!request.signal.aborted) error.hidden = false;
    } finally {
      if (pendingRequest === request) {
        pendingRequest = undefined;
        if (!request.signal.aborted) pendingFilters = undefined;
      }
    }
  }

  window.addEventListener('pagehide', () => {
    pendingRequest?.abort();
  });

  window.addEventListener('pageshow', event => {
    if (event.persisted && pendingFilters) void updateResults(pendingFilters);
  });

  function refresh(filters) {
    pendingRequest?.abort();
    pendingFilters = filters;
    void updateResults(filters);
  }

  search.addEventListener('input', () => {
    const filters = new URLSearchParams(pendingFilters || appliedFilters);
    refresh(filters);
  });
  form.addEventListener('submit', event => {
    event.preventDefault();
    refresh(new URLSearchParams(new FormData(form)));
  });
})();
