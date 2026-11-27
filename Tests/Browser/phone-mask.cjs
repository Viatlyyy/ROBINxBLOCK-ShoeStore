// Run with Node.js and Playwright installed: node Tests/Browser/phone-mask.cjs
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    for (const view of ['Account/Register', 'Cabinet/Profile', 'Cart/Checkout']) {
      const source = fs.readFileSync(path.join(root, 'Views', view + '.cshtml'), 'utf8')
        .replace(/aria-invalid="@\([^\n]*\)"/g, '');
      const input = source.match(/<input\b[^>]*(?:asp-for="Phone"|name="phone")[^>]*>/i)?.[0];
      assert.ok(input, view + ': phone field exists');
      await page.setContent('<form>' + input.replace('asp-for="Phone"', 'name="Phone"') + '</form>');
      const script = path.join(root, 'wwwroot/js/phone-mask.js');
      if (fs.existsSync(script)) await page.addScriptTag({ path: script });
      const phone = page.locator('input');

      await phone.evaluate(el => { el.value = '89991234567'; window.dispatchEvent(new Event('pageshow')); });
      assert.equal(await phone.inputValue(), '+7 (999) 123-45-67', view + ': restored value');
      await phone.fill('');

      await phone.pressSequentially('9991234567');
      assert.equal(await phone.inputValue(), '+7 (999) 123-45-67', view + ': typing');
      for (const [typed, expected] of [
        ['8005553535', '+7 (800) 555-35-35'],
        ['88005553535', '+7 (800) 555-35-35'],
        ['89991234567', '+7 (999) 123-45-67'],
        ['79991234567', '+7 (999) 123-45-67']
      ]) {
        await phone.fill('');
        await phone.pressSequentially(typed);
        assert.equal(await phone.inputValue(), expected, view + ': typing ' + typed);
      }
      for (const value of ['89991234567', '+79991234567', '+7 (999) 123-45-67', '9991234567']) {
        await phone.fill(value);
        assert.equal(await phone.inputValue(), '+7 (999) 123-45-67', view + ': pasted ' + value);
      }
      await phone.fill('');
      await phone.pressSequentially('8999');
      await phone.press('Tab');
      await phone.focus();
      await phone.press('End');
      await phone.pressSequentially('1234567');
      assert.equal(await phone.inputValue(), '+7 (999) 123-45-67', view + ': continue after blur');
      await phone.fill('');
      await phone.pressSequentially('89990');
      await phone.press('Backspace');
      await phone.pressSequentially('1234567');
      assert.equal(await phone.inputValue(), '+7 (999) 123-45-67', view + ': continue after typo correction');
      await phone.fill('9991234567');
      await phone.press('Home');
      await phone.press('Delete');
      assert.equal(await phone.inputValue(), '+7 (999) 123-45-67', view + ': protect country prefix');
      await phone.press('End');
      await phone.press('Backspace');
      assert.equal(await phone.inputValue(), '+7 (999) 123-45-6', view + ': delete final digit');
      await phone.fill('999');
      await phone.press('Backspace');
      assert.equal(await phone.inputValue(), '+7 (99', view + ': delete across separator');
      await phone.fill('9991234567');
      await phone.evaluate(el => el.setSelectionRange(13, 13));
      await phone.press('Backspace');
      assert.equal(await phone.inputValue(), '+7 (999) 124-56-7', view + ': backspace across hyphen');
      await phone.fill('9991234567');
      await phone.evaluate(el => el.setSelectionRange(7, 7));
      await phone.press('Delete');
      assert.equal(await phone.inputValue(), '+7 (999) 234-56-7', view + ': delete across closing parenthesis');
      await phone.fill('9991234567');
      await phone.evaluate(el => el.setSelectionRange(10, 11));
      await phone.press('8');
      assert.equal(await phone.inputValue(), '+7 (999) 183-45-67', view + ': replace middle digit');
      assert.equal(await phone.evaluate(el => el.selectionStart), 11, view + ': caret stays in middle');
      await phone.press('ControlOrMeta+A');
      await phone.press('Backspace');
      assert.equal(await phone.inputValue(), '', view + ': clear optional field');
      await phone.fill('letters');
      assert.equal(await phone.inputValue(), '', view + ': letters removed');
      await phone.fill('+7 (800) 555-35-35');
      await phone.press('End');
      await phone.press('9');
      assert.equal(await phone.inputValue(), '+7 (800) 555-35-35', view + ': preserve filled number on overflow');
      await phone.fill('+780055535359');
      assert.equal(await phone.inputValue(), '+7 (800) 555-35-35', view + ': preserve explicit country code on paste');
      await phone.fill('999123456789');
      assert.equal(await phone.inputValue(), '+7 (999) 123-45-67', view + ': digit limit');
      console.log('PASS ' + view);
    }
    assert.deepEqual(errors, [], 'no browser script errors');
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
