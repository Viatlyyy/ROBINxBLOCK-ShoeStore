(() => {
  const form = document.getElementById('catalog-filters');
  const search = document.getElementById('catalog-search');
  const results = document.getElementById('catalog-results');
  const error = document.getElementById('catalog-error');
  if (!form || !search || !results || !error || !window.fetch || typeof AbortController !== 'function') return;

  // При вводе используем настройки, которые уже применили кнопкой «Показать».
  let appliedFilters = new URLSearchParams(new FormData(form));
  let timer;
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
    clearTimeout(timer);
    pendingRequest?.abort();
  });

  window.addEventListener('pageshow', event => {
    if (event.persisted && pendingFilters) void updateResults(pendingFilters);
  });

  function schedule(filters, delay) {
    clearTimeout(timer);
    pendingRequest?.abort();
    pendingFilters = filters;
    if (delay) timer = setTimeout(() => updateResults(filters), delay);
    else void updateResults(filters);
  }

  search.addEventListener('input', () => schedule(appliedFilters, 300));
  form.addEventListener('submit', event => {
    event.preventDefault();
    schedule(new URLSearchParams(new FormData(form)), 0);
  });
})();
