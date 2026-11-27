const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const engine = fs.readFileSync(process.argv[2] || path.join(__dirname, '../../wwwroot/js/smooth-scroll.js'), 'utf8');

class Box {
  clientHeight = 900;
  scrollHeight = 5000;
  matches() { return false; }
}

function simulate(quantum = 1) {
  const root = new Box();
  const body = new Box();
  const document = Object.assign(new EventTarget(), {
    body, scrollingElement: root, documentElement: root, hidden: false, querySelector: () => null
  });
  const window = Object.assign(new EventTarget(), {
    devicePixelRatio: 1 / quantum, scrollY: 500, AbortController,
    matchMedia: () => ({ matches: true }),
    scrollTo(options, legacyTop) {
      const top = typeof options === 'object' ? options.top : legacyTop;
      this.scrollY = Math.round(top / quantum) * quantum;
    }
  });
  const style = { overflowY: 'visible', overscrollBehaviorY: 'auto', position: 'static', scrollSnapType: 'none' };
  const pending = new Map();
  let time = 0;
  let id = 0;
  const requestAnimationFrame = callback => {
    pending.set(++id, callback);
    return id;
  };
  const cancelAnimationFrame = id => pending.delete(id);
  window.requestAnimationFrame = requestAnimationFrame;
  const context = vm.createContext({
    window, document, AbortController, Element: Box, WheelEvent: { DOM_DELTA_LINE: 1, DOM_DELTA_PAGE: 2 },
    performance: { now: () => time }, getComputedStyle: () => style,
    requestAnimationFrame, cancelAnimationFrame
  });
  const mount = () => vm.runInContext(engine, context);
  mount();

  return {
    window, pending, mount,
    wheel(deltaY, fields) {
      const event = Object.assign(new Event('wheel', { cancelable: true }), {
        deltaX: 0, deltaY, deltaMode: 0, composedPath: () => [body, root, window], ...fields
      });
      return !window.dispatchEvent(event);
    },
    tick(dt) {
      time += dt;
      const current = [...pending.values()];
      pending.clear();
      for (const callback of current) callback(time);
      return window.scrollY;
    }
  };
}

for (const hz of [60, 120, 144, 279]) {
  const scroll = simulate(hz === 144 ? .5 : 1);
  scroll.mount(); // Loading the script again does not duplicate the owner.
  for (let i = 0; i < 6; i++) assert(scroll.wheel(20));
  assert.equal(scroll.pending.size, 1);
  let previous = scroll.window.scrollY;
  let atTwoHundred;
  for (let frame = 1; frame <= hz; frame++) {
    const current = scroll.tick(1000 / hz);
    assert(current >= previous && current <= 620);
    if (frame === Math.round(hz * .2)) atTwoHundred = current;
    previous = current;
  }
  assert(Math.abs(atTwoHundred - 606) <= 3, hz + ' Hz differs in the 200 ms sample');
  assert.equal(previous, 620);
  assert.equal(scroll.pending.size, 0);
  console.log(hz + ' Hz: reached 620, ' + atTwoHundred + ' at 200 ms, idle.');
}

const fractional = simulate();
for (let i = 0; i < 10; i++) {
  assert(fractional.wheel(.2));
  while (fractional.pending.size) fractional.tick(16.667);
}
assert.equal(fractional.window.scrollY, 502);
assert(!fractional.wheel(100, { ctrlKey: true }));
assert(!fractional.wheel(4, { deltaX: 40 }));

const stalled = simulate();
stalled.wheel(500);
const atFirst = stalled.tick(16.667);
const afterPause = stalled.tick(230);
assert(afterPause - atFirst < (1000 - atFirst) * .3, 'Stall produced a large catch-up step');
while (stalled.pending.size) stalled.tick(16.667);
assert.equal(stalled.window.scrollY, 1000);
stalled.wheel(200);
stalled.window[Symbol.for('storefront.smoothScroll')]();
assert.equal(stalled.pending.size, 0);
assert(!stalled.wheel(100));
stalled.mount();
assert(stalled.wheel(100));
console.log('Fractional input, native gestures, stall, disposal and remount passed.');

