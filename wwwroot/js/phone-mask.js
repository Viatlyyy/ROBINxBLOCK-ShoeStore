(() => {
  const phones = document.querySelectorAll('input[type="tel"], input[autocomplete="tel"]');
  const states = new WeakMap();

  function format(digits) {
    if (!digits) return '';
    let value = '+7 (' + digits.slice(0, 3);
    if (digits.length > 3) value += ') ' + digits.slice(3, 6);
    if (digits.length > 6) value += '-' + digits.slice(6, 8);
    if (digits.length > 8) value += '-' + digits.slice(8, 10);
    return value;
  }

  function digitPositions(value) {
    return [...value.matchAll(/\d/g)].slice(1).map(match => match.index);
  }

  function setValue(input, digits, beforeCaret, prefixOnly = false) {
    input.value = format(digits) || (prefixOnly ? '+7' : '');
    if (document.activeElement === input) {
      const positions = digitPositions(input.value);
      const caret = beforeCaret > 0 ? (positions[beforeCaret - 1] ?? input.value.length - 1) + 1
        : Math.min(4, input.value.length);
      input.setSelectionRange(caret, caret);
    }
  }

  function update(input, event) {
    const raw = input.value;
    const caret = input.selectionStart ?? raw.length;
    const state = states.get(input);
    const typingDigit = event?.inputType === 'insertText' && /^\d$/.test(event.data ?? '');
    const replacingValue = event?.inputType && !typingDigit && !event.inputType.startsWith('delete');
    if (replacingValue || (!event && raw !== state.value)) state.pendingPrefix = false;
    if (typingDigit && !state.value && /^[78]$/.test(raw)) state.pendingPrefix = true;
    const digits = raw.replace(/\D/g, '');
    let national = digits;
    let prefixLength = 0;
    const explicitPrefix = /^\s*\+7/.test(raw);
    if (explicitPrefix) {
      national = national.slice(1);
      prefixLength++;
    }
    if (!/^[78]/.test(national) || /^80/.test(national)) state.pendingPrefix = false;
    // A leading 8 can belong to a ten-digit number (for example, 800).
    // Treat it as the trunk prefix only once an eleventh digit is entered.
    if (national.length > 10 && /^[78]/.test(national) && (!explicitPrefix || state.pendingPrefix)) {
      national = national.slice(1);
      prefixLength++;
      state.pendingPrefix = false;
    }
    const beforeCaret = Math.max(0, raw.slice(0, caret).replace(/\D/g, '').length - prefixLength);
    setValue(input, national.slice(0, 10), beforeCaret, prefixLength > 0 && !national);
    state.value = input.value;
  }

  for (const input of phones) {
    states.set(input, { value: '', pendingPrefix: false });
    input.inputMode = 'tel';
    input.placeholder = '+7 (___) ___-__-__';
    update(input);
    input.addEventListener('input', event => { if (!event.isComposing) update(input, event); });
    input.addEventListener('change', () => update(input));
    input.addEventListener('beforeinput', event => {
      if (!['deleteContentBackward', 'deleteContentForward'].includes(event.inputType)) return;
      const start = input.selectionStart;
      const end = input.selectionEnd;
      if (start === null || end === null || !event.cancelable) return;
      if (start === end && start < 4) {
        event.preventDefault();
        input.setSelectionRange(Math.min(4, input.value.length), Math.min(4, input.value.length));
        return;
      }
      const positions = digitPositions(input.value);
      const digits = positions.map(position => input.value[position]);
      let from = positions.findIndex(position => position >= start);
      if (from < 0) from = digits.length;
      let to = positions.findIndex(position => position >= end);
      if (to < 0) to = digits.length;
      if (start === end) {
        if (event.inputType === 'deleteContentBackward') from = Math.max(0, from - 1);
        else to = Math.min(digits.length, from + 1);
      }
      event.preventDefault();
      digits.splice(from, to - from);
      setValue(input, digits.join(''), from);
      input.dispatchEvent(new Event('input', { bubbles: true }));
    });
  }
  window.addEventListener('pageshow', () => phones.forEach(input => update(input)));
})();
