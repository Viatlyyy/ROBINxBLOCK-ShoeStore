// Run against a store with test products. Optional SHOESTORE_SCREENSHOTS saves previews.
const { chromium } = require('playwright');
const { expect } = require('playwright/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const baseURL = process.env.SHOESTORE_BASE_URL || 'http://127.0.0.1:5189';

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ baseURL, viewport: { width: 1920, height: 1080 } });
    const baseline = await browser.newPage({ baseURL, viewport: { width: 1920, height: 1080 } });
    await baseline.route('**/css/home-typography.css*', route => route.fulfill({ contentType: 'text/css', body: '' }));
    if (process.argv.includes('--baseline')) {
      await page.route('**/css/home-typography.css*', route => route.fulfill({ contentType: 'text/css', body: '' }));
    }
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    if (!process.argv.includes('--home-only')) {
      for (const route of ['/Catalog', '/']) {
        await page.goto(route);
        for (const width of [1920, 390]) {
          await page.setViewportSize({ width, height: 1080 });
          await page.locator('.header .logo').hover();
          const dimensions = await page.locator('.header .logo').evaluate(el => {
            const text = document.createRange();
            text.selectNodeContents(el);
            return { link: el.getBoundingClientRect().width, text: text.getBoundingClientRect().width,
              line: parseFloat(getComputedStyle(el, '::after').width) };
          });
          assert.ok(dimensions.link <= dimensions.text + 2, route + '/' + width + ': logo link fits text');
          assert.ok(dimensions.line <= dimensions.text + 2, route + '/' + width + ': underline stays under logo');
        }
      }
      console.log('PASS short logo underline on home/catalog, desktop/mobile');
    }
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/Catalog');
    const catalogNameSize = await page.locator('.product h3').first().evaluate(el => getComputedStyle(el).fontSize);
    const catalogPriceSize = await page.locator('.product strong').first().evaluate(el => getComputedStyle(el).fontSize);
    await page.goto('/');
    assert.equal(await page.locator('.home-catalog .product h3').first().evaluate(el => getComputedStyle(el).fontSize), catalogNameSize, 'product name matches catalog typography');
    assert.equal(await page.locator('.home-catalog .product strong').first().evaluate(el => getComputedStyle(el).fontSize), catalogPriceSize, 'product price matches catalog typography');
    assert.ok(await page.locator('.culture-board__story > p:not(.eyebrow)').evaluate(el => parseFloat(getComputedStyle(el).fontSize)) >= 14, 'readable journal text');
    await page.locator('[data-hero-next]').click();
    await expect.poll(() => page.locator('.hero-slide').nth(1).getAttribute('class')).toContain('is-active');
    await page.locator('.hero-slide.is-active .button').focus();
    assert.match(await page.locator('.hero-slide.is-active .button').getAttribute('href'), /^\/Catalog/);
    const swatches = page.locator('.home-catalog .product').first().locator('[data-variant-choice]');
    await swatches.last().click();
    await expect.poll(() => swatches.last().getAttribute('aria-pressed')).toBe('true');
    await page.goto('/');
    await baseline.goto('/');
    for (const width of [1920, 1280, 768, 390]) {
      await page.setViewportSize({ width, height: width === 390 ? 844 : 1080 });
      await baseline.setViewportSize({ width, height: width === 390 ? 844 : 1080 });
      await page.mouse.move(0, 0);
      await page.evaluate(() => document.fonts.ready);
      for (const selector of ['.hero-carousel', '.home-catalog', '.home-promotions', '.culture-board', '.home-catalog .image-wrap', '.feature-tile']) {
        const geometry = el => {
          const css = getComputedStyle(el);
          return { width: css.width, columns: css.gridTemplateColumns, padding: css.padding,
            background: css.backgroundColor, height: el.matches('.hero-carousel, .image-wrap, .feature-tile') ? css.height : null };
        };
        assert.deepEqual(await page.locator(selector).first().evaluate(geometry),
          await baseline.locator(selector).first().evaluate(geometry), width + ': original layout retained at ' + selector);
      }
      // Inspect lower sections too, including content-visibility and hover transforms.
      for (const selector of ['.hero-carousel', '.home-catalog', '.home-promotions', '.culture-board', '.store-footer']) {
        await page.locator(selector).scrollIntoViewIfNeeded();
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), width + ': no overflow at ' + selector);
      }
      if (process.env.SHOESTORE_SCREENSHOTS) {
        fs.mkdirSync(process.env.SHOESTORE_SCREENSHOTS, { recursive: true });
        await page.locator('.home-catalog').screenshot({ path: path.join(process.env.SHOESTORE_SCREENSHOTS, `home-products-${width}.png`) });
        await page.evaluate(() => window.scrollTo(0, 0));
        await page.screenshot({ path: path.join(process.env.SHOESTORE_SCREENSHOTS, `home-${width}.png`), fullPage: true });
      }
    }
    assert.deepEqual(errors, []);
    await baseline.close();
    console.log('PASS readable home typography; original section/card/image geometry preserved');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
