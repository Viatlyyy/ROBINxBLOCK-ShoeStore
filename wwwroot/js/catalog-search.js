(() => {
  const form = document.getElementById('catalog-filters');
  const search = document.getElementById('catalog-search');
  const results = document.getElementById('catalog-results');
  const error = document.getElementById('catalog-error');
  if (!form || !search || !results || !error) return;

  // При вводе используем настройки, которые уже применили кнопкой «Показать».
  let appliedFilters = new URLSearchParams(new FormData(form));
  let timer;
  let pendingRequest;

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
    }
  }

  search.addEventListener('input', () => {
    clearTimeout(timer);
    pendingRequest?.abort();
    timer = setTimeout(updateResults, 300);
  });

  form.addEventListener('submit', event => {
    event.preventDefault();
    clearTimeout(timer);
    pendingRequest?.abort();
    void updateResults(new URLSearchParams(new FormData(form)));
  });
})();
