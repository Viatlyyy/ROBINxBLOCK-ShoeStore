(() => {
  const sizes = { thumbnail: 144, compact: 320, card: 512, detail: 1180 };
  const prepared = new Map();
  const selector = 'img[data-product-photo]';

  function canvas(width, height) {
    const element = document.createElement('canvas');
    element.width = width;
    element.height = height;
    return element;
  }

  function edgeLightness(pixels, width, height) {
    const edge = [];
    for (let x = 0; x < width; x++) {
      edge.push(x, (height - 1) * width + x);
    }
    for (let y = 1; y < height - 1; y++) {
      edge.push(y * width, y * width + width - 1);
    }
    const opaque = edge.filter(index => pixels[index * 4 + 3] > 128);
    if (opaque.length < edge.length / 3) return -1;
    const brightness = opaque.map(index => {
      const start = index * 4;
      return Math.min(pixels[start], pixels[start + 1], pixels[start + 2]);
    }).sort((first, second) => first - second);
    return brightness[Math.floor(brightness.length / 2)];
  }

  function foreground(pixels, width, height, edge) {
    const background = new Uint8Array(width * height);
    const queue = new Int32Array(background.length);
    const threshold = Math.max(219, edge - 8);
    const ceiling = Math.min(255, edge + 8);
    let tail = 0;

    function enqueue(index) {
      if (background[index]) return;
      const start = index * 4;
      const red = pixels[start], green = pixels[start + 1], blue = pixels[start + 2];
      const lightness = Math.min(red, green, blue);
      const neutral = Math.max(red, green, blue) - lightness <= 16;
      if (pixels[start + 3] > 24 && (edge < 0 || lightness < threshold || lightness > ceiling || !neutral)) return;
      background[index] = 1;
      queue[tail++] = index;
    }

    for (let x = 0; x < width; x++) {
      enqueue(x);
      enqueue((height - 1) * width + x);
    }
    for (let y = 1; y < height - 1; y++) {
      enqueue(y * width);
      enqueue(y * width + width - 1);
    }
    // Only pixels reachable from the outside belong to the studio background.
    for (let head = 0; head < tail; head++) {
      const index = queue[head];
      const x = index % width;
      if (x > 0) enqueue(index - 1);
      if (x < width - 1) enqueue(index + 1);
      if (index >= width) enqueue(index - width);
      if (index < background.length - width) enqueue(index + width);
    }

    if (tail < background.length * .12 || tail > background.length * .98) return null;
    if (edge >= 0) fillLightDetails(background, width, height);
    const columns = new Uint16Array(width);
    const rows = new Uint16Array(height);
    for (let index = 0; index < background.length; index++) {
      if (background[index] || pixels[index * 4 + 3] < 25) continue;
      columns[index % width]++;
      rows[Math.floor(index / width)]++;
    }
    const minColumn = Math.max(2, Math.floor(height * .013));
    const minRow = Math.max(2, Math.floor(width * .013));
    const fromX = columns.findIndex(pixels => pixels >= minColumn);
    const toX = columns.findLastIndex(pixels => pixels >= minColumn);
    const fromY = rows.findIndex(pixels => pixels >= minRow);
    const toY = rows.findLastIndex(pixels => pixels >= minRow);
    if (fromX < 0 || toX <= fromX || fromY < 0 || toY <= fromY) return null;

    const pad = Math.max(2, Math.round(Math.max(toX - fromX, toY - fromY) * .045));
    const left = Math.max(0, fromX - pad);
    const top = Math.max(0, fromY - pad);
    const right = Math.min(width, toX + pad + 1);
    const bottom = Math.min(height, toY + pad + 1);
    return { background, left, top, width: right - left, height: bottom - top };
  }

  function fillLightDetails(background, width, height) {
    const firstX = new Int16Array(height).fill(width);
    const lastX = new Int16Array(height).fill(-1);
    const firstY = new Int16Array(width).fill(height);
    const lastY = new Int16Array(width).fill(-1);
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        if (background[y * width + x]) continue;
        if (x < firstX[y]) firstX[y] = x;
        lastX[y] = x;
        if (y < firstY[x]) firstY[x] = y;
        lastY[x] = y;
      }
    }
    // Highlights inside the silhouette can be as bright as the surrounding studio backdrop.
    for (let y = 0; y < height; y++) {
      for (let x = firstX[y] + 1; x < lastX[y]; x++) {
        if (y > firstY[x] && y < lastY[x]) background[y * width + x] = 0;
      }
    }
  }

  function normalize(image, size) {
    const analysisScale = Math.min(1, 320 / Math.max(image.naturalWidth, image.naturalHeight));
    const width = Math.max(1, Math.round(image.naturalWidth * analysisScale));
    const height = Math.max(1, Math.round(image.naturalHeight * analysisScale));
    const mask = canvas(width, height);
    const context = mask.getContext('2d', { willReadFrequently: true });
    context.drawImage(image, 0, 0, width, height);
    const data = context.getImageData(0, 0, width, height);
    const edge = edgeLightness(data.data, width, height);
    if (edge >= 0 && edge < 218) return null;
    const crop = foreground(data.data, width, height, edge);
    if (!crop) return null;

    const sourceScaleX = image.naturalWidth / width;
    const sourceScaleY = image.naturalHeight / height;
    const sourceWidth = crop.width * sourceScaleX;
    const sourceHeight = crop.height * sourceScaleY;
    const ratio = Math.min(1, size / Math.max(sourceWidth, sourceHeight));
    const output = canvas(Math.max(1, Math.round(sourceWidth * ratio)), Math.max(1, Math.round(sourceHeight * ratio)));
    const paint = output.getContext('2d');
    paint.imageSmoothingQuality = 'high';
    paint.drawImage(image, crop.left * sourceScaleX, crop.top * sourceScaleY, sourceWidth, sourceHeight,
      0, 0, output.width, output.height);

    // An alpha mask preserves the original color and texture, including light panels of the shoe.
    const alpha = context.createImageData(width, height);
    for (let index = 0; index < crop.background.length; index++) {
      if (!crop.background[index]) alpha.data[index * 4 + 3] = 255;
    }
    context.putImageData(alpha, 0, 0);
    paint.globalCompositeOperation = 'destination-in';
    paint.drawImage(mask, crop.left, crop.top, crop.width, crop.height, 0, 0, output.width, output.height);
    return new Promise(resolve => output.toBlob(blob => resolve(blob ? URL.createObjectURL(blob) : null), 'image/webp', .94));
  }

  function load(image, source) {
    return new Promise(resolve => {
      image.onload = () => resolve(true);
      image.onerror = () => resolve(false);
      image.src = source;
    });
  }

  function prepare(source, role) {
    const url = new URL(source, document.baseURI);
    const size = sizes[role] ?? sizes.card;
    const key = size + ' ' + url.href;
    if (!prepared.has(key)) {
      prepared.set(key, (async () => {
        const image = new Image();
        if (!await load(image, url.href)) {
          prepared.delete(key);
          return null;
        }
        try { await image.decode?.(); } catch { return url.href; }
        if (url.origin !== location.origin || !url.pathname.startsWith('/images/')) return url.href;
        try {
          const normalized = await normalize(image, size);
          if (normalized) {
            if (await load(new Image(), normalized)) return normalized;
            URL.revokeObjectURL(normalized);
          }
        } catch { /* The original still works if normalization or blob loading fails. */ }
        return url.href;
      })());
    }
    return prepared.get(key);
  }

  function show(image, source, result) {
    image.dataset.photoSource = new URL(source, document.baseURI).href;
    image.dataset.photoState = result.startsWith('blob:') ? 'normalized' : 'original';
    image.src = result;
  }

  async function upgrade(image) {
    const source = image.getAttribute('src') && image.src;
    if (!source || image.dataset.photoSource) return;
    const result = await prepare(source, image.dataset.productPhoto);
    // A color or gallery selection may have replaced this image while it was being decoded.
    if (result && image.isConnected && image.src === source && !image.dataset.photoSource) show(image, source, result);
  }

  window.productPhotos = { prepare, show };
  function missing(image) {
    if (!(image instanceof HTMLImageElement) || !image.matches('[data-variant-image]')) return;
    const placeholder = image.closest('[data-product-card]')?.querySelector('[data-variant-image-empty]');
    if (!placeholder) return;
    image.hidden = true;
    placeholder.hidden = false;
  }
  document.addEventListener('error', event => missing(event.target), true);

  let observer;
  try {
    if (typeof IntersectionObserver === 'function') {
      observer = new IntersectionObserver(entries => {
        for (const entry of entries) {
          if (!entry.isIntersecting) continue;
          observer.unobserve(entry.target);
          void upgrade(entry.target);
        }
      }, { rootMargin: '180px' });
    }
  } catch { /* Original photos remain available. */ }

  function register(image) {
    if (image.getAttribute('src') && image.complete && !image.naturalWidth) missing(image);
    else observer?.observe(image);
  }
  document.querySelectorAll(selector).forEach(register);
  if (typeof MutationObserver !== 'function') return;
  const changes = new MutationObserver(entries => {
    for (const entry of entries) {
      for (const node of entry.removedNodes) {
        if (!(node instanceof Element)) continue;
        if (node.matches(selector)) observer?.unobserve(node);
        node.querySelectorAll(selector).forEach(image => observer?.unobserve(image));
      }
      for (const node of entry.addedNodes) {
        if (!(node instanceof Element)) continue;
        if (node.matches(selector)) register(node);
        node.querySelectorAll(selector).forEach(register);
      }
    }
  });
  changes.observe(document.querySelector('main') || document.body, { childList: true, subtree: true });
})();
