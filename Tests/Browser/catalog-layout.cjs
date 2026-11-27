// Requires a running store with test products, Node.js and Playwright.
// SHOESTORE_BASE_URL sets the server; SHOESTORE_SCREENSHOTS optionally saves previews.
const { chromium } = require('playwright');
const { expect } = require('playwright/test');
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const baseURL = process.env.SHOESTORE_BASE_URL || 'http://127.0.0.1:5189';

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ baseURL, viewport: { width: 1920, height: 1080 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    const cards = page.locator('[data-product-card]');
    const search = page.locator('#catalog-search');
    const sort = page.locator('#catalog-sort');
    await page.goto('/Catalog');
    await page.locator('.catalog-page').waitFor();
    assert.ok(await cards.count() >= 4, 'test data needs at least four products');
    const bounds = await page.locator('.catalog-page').boundingBox();
    assert.ok(bounds.width >= 1920 * .9, 'catalog uses at least 90% of a wide screen');
    const first = await cards.nth(0).boundingBox();
    const fourth = await cards.nth(3).boundingBox();
    assert.ok(first.width >= 300 && Math.abs(first.y - fourth.y) < 2, 'four large cards per desktop row');
    assert.equal(await page.locator('header nav a').count(), 1);
    assert.ok(await page.locator('[data-product-badge="new"]').count() > 0, 'new product badges are visible');
    assert.ok(await page.locator('[data-product-badge="popular"]').count() > 0, 'popular product badges are visible');
    const form = await page.locator('#catalog-filters').evaluate(el => [...new FormData(el).keys()]);
    assert.ok(form.includes('sort') && form.includes('search'), 'external controls belong to filter form');
    assert.equal(await sort.evaluate(el => el.closest('form')?.id), 'catalog-filters', 'price sorting is in the left filter panel');
    const tagBox = await page.locator('#catalog-tag').boundingBox();
    const sortBox = await sort.boundingBox();
    assert.ok(sortBox.y > tagBox.y + tagBox.height, 'sorting follows the product selection');
    const searchBox = await search.boundingBox();
    assert.ok(Math.abs(searchBox.x + searchBox.width / 2 - 960) < 2, 'search is centered on the page');

    async function changeAndWait(action) {
      const response = page.waitForResponse(r => r.url().includes('/Catalog/SearchResults') && r.ok());
      await action();
      await response;
    }
    await page.evaluate(() => {
      window.catalogRequests = [];
      const fetchResults = window.fetch.bind(window);
      window.fetch = (...args) => {
        window.catalogRequests.push(String(args[0]));
        return fetchResults(...args);
      };
    });
    const automaticRequests = await page.evaluate(() => {
      for (const [id, value] of [['catalog-sort', 'price-desc'], ['catalog-tag', 'new']]) {
        const input = document.getElementById(id);
        input.value = value;
        input.dispatchEvent(new Event('change', { bubbles: true }));
      }
      return window.catalogRequests.length;
    });
    assert.equal(automaticRequests, 0, 'changing category or price waits for Show');
    const immediateSearch = await page.evaluate(() => {
      const input = document.getElementById('catalog-search');
      input.value = 'New Balance';
      input.dispatchEvent(new Event('input', { bubbles: true }));
      return window.catalogRequests.at(-1);
    });
    assert.ok(immediateSearch, 'search starts a request in the input event without a timer');
    assert.equal(new URL(immediateSearch).searchParams.get('sort'), 'default', 'search ignores unconfirmed price sort');
    assert.equal(new URL(immediateSearch).searchParams.get('tag'), 'all', 'search ignores unconfirmed category');
    await expect.poll(() => cards.count()).toBe(3);
    await page.goto('/Catalog');
    for (const tag of ['new', 'popular']) {
      await changeAndWait(async () => {
        await page.locator('#catalog-tag').selectOption(tag);
        await page.locator('.catalog-submit').click();
      });
      await page.waitForFunction(expected => new URL(location.href).searchParams.get('tag') === expected, tag);
      assert.equal(await page.locator('#catalog-tag').inputValue(), tag, 'new/popular selection remains available');
    }
    await page.goto('/Catalog');
    await changeAndWait(async () => { await sort.selectOption('price-asc'); await page.locator('.catalog-submit').click(); });
    await page.waitForFunction(() => location.search.includes('sort=price-asc'));
    const ascending = (await cards.locator('strong').allTextContents()).map(text => Number(text.replace(/\D/g, '')));
    assert.deepEqual(ascending, [...ascending].sort((a, b) => a - b), 'ascending price sorting remains available');
    await changeAndWait(async () => { await sort.selectOption('price-desc'); await page.locator('.catalog-submit').click(); });
    await page.waitForFunction(() => location.search.includes('sort=price-desc'));
    const prices = await cards.locator('strong').allTextContents();
    const numbers = prices.map(text => Number(text.replace(/\D/g, '')));
    assert.deepEqual(numbers, [...numbers].sort((a, b) => b - a), 'Show applies descending price sort');
    await page.goto('/Catalog');
    let sortingStarted;
    const sortingRequest = new Promise(resolve => { sortingStarted = resolve; });
    await page.route('**/Catalog/SearchResults?**', async route => {
      if (!new URL(route.request().url()).searchParams.has('search')) {
        sortingStarted();
        await new Promise(resolve => setTimeout(resolve, 1200));
      }
      await route.continue().catch(() => {});
    });
    await sort.selectOption('price-desc');
    await page.locator('.catalog-submit').click();
    await sortingRequest;
    await changeAndWait(() => search.fill('New Balance'));
    await page.waitForFunction(() => new URL(location.href).searchParams.get('search') === 'New Balance');
    assert.equal(new URL(page.url()).searchParams.get('sort'), 'price-desc', 'search preserves pending sort');
    await page.unroute('**/Catalog/SearchResults?**');
    await page.goto('/Catalog');
    const brand = page.locator('.catalog-brand').filter({ hasText: 'New Balance' }).locator('input');
    await brand.check();
    await changeAndWait(() => page.locator('.catalog-submit').click());
    await expect.poll(() => cards.locator('.brand').allTextContents()).toEqual(['New Balance', 'New Balance', 'New Balance']);
    // Changing an unapplied checkbox must not change the next live search.
    await page.locator('.catalog-brand').filter({ hasText: 'Adidas' }).locator('input').check();
    await changeAndWait(() => search.fill('9060'));
    await expect.poll(() => cards.count()).toBe(1);
    await expect.poll(() => cards.locator('h3').first().textContent()).toBe('9060');
    assert.ok(!new URL(page.url()).searchParams.getAll('brandIds').includes(
      await page.locator('.catalog-brand').filter({ hasText: 'Adidas' }).locator('input').inputValue()));
    await changeAndWait(() => search.fill('nonexistent-model-test'));
    await expect.poll(() => cards.count()).toBe(0);
    await expect.poll(() => page.locator('#catalog-results').innerText()).toMatch(/ничего не найдено/);
    await page.route('**/Catalog/SearchResults?**', route => route.fulfill({ status: 503, body: 'unavailable' }));
    await search.fill('failed');
    await page.waitForFunction(() => !document.querySelector('#catalog-error').hidden);
    await page.unroute('**/Catalog/SearchResults?**');
    await changeAndWait(() => search.fill('9060'));
    await expect.poll(() => cards.count()).toBe(1);
    assert.equal(await page.locator('#catalog-error').isVisible(), false);
    assert.ok(await page.locator('[data-product-badge]').count() > 0, 'badges survive asynchronous search updates');
    const swatches = cards.first().locator('[data-variant-choice]');
    await swatches.last().click();
    await expect.poll(() => swatches.last().getAttribute('aria-pressed')).toBe('true');
    assert.match(await cards.first().locator('[data-favorite-login]').getAttribute('href'), /^\/Account\/Login/);
    console.log('PASS search, sort, applied brands, empty state, request recovery and card controls');

    await page.goto('/Catalog');
    await page.mouse.move(0, 0);
    for (const width of [1920, 1280, 768, 390]) {
      await page.setViewportSize({ width, height: width === 390 ? 844 : 1080 });
      await page.evaluate(() => document.fonts.ready);
      const columns = await page.locator('.product-grid').evaluate(el => getComputedStyle(el).gridTemplateColumns.split(' ').length);
      assert.equal(columns, width >= 1200 ? 4 : 2, width + ': card columns');
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), width + ': no overflow');
      if (width === 390) {
        const details = page.locator('.catalog-filter-panel');
        await expect.poll(() => details.getAttribute('open'), { message: 'mobile filters start collapsed' }).toBe(null);
        await page.locator('.catalog-filter-panel > summary').focus();
        await page.keyboard.press('Enter');
        await expect.poll(() => details.getAttribute('open')).toBe('');
        await page.keyboard.press('Enter');
        await page.locator('.catalog-filter-panel > summary').blur();
      }
      if (process.env.SHOESTORE_SCREENSHOTS) {
        fs.mkdirSync(process.env.SHOESTORE_SCREENSHOTS, { recursive: true });
        await page.screenshot({ path: path.join(process.env.SHOESTORE_SCREENSHOTS, `catalog-${width}.png`) });
      }
    }
    const noJs = await browser.newContext({ baseURL, javaScriptEnabled: false, viewport: { width: 1280, height: 900 } });
    const plain = await noJs.newPage();
    await plain.goto('/Catalog');
    await plain.locator('#catalog-search').fill('9060');
    await plain.locator('#catalog-sort').selectOption('price-asc');
    await Promise.all([plain.waitForURL('**/Catalog?**'), plain.locator('.catalog-submit').click()]);
    assert.equal(new URL(plain.url()).searchParams.get('sort'), 'price-asc');
    assert.equal(await plain.locator('[data-product-card]').count(), 1);
    await plain.setViewportSize({ width: 390, height: 844 });
    await plain.locator('.catalog-filter-panel > summary').click();
    await plain.setViewportSize({ width: 844, height: 390 });
    assert.equal(await plain.locator('.catalog-filter-panel > summary').isVisible(), true,
      'closed filters can be reopened after rotating with JavaScript disabled');
    await plain.locator('.catalog-filter-panel > summary').click();
    assert.equal(await plain.locator('.catalog-submit').isVisible(), true);
    assert.deepEqual(errors, []);
    await noJs.close();
    console.log('PASS desktop/tablet/mobile layout and submission without JavaScript');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
