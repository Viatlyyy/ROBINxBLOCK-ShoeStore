from argparse import ArgumentParser
from base64 import b64decode
from time import sleep
from pathlib import Path
from uuid import uuid4
from urllib.parse import urlsplit
import re
from playwright.sync_api import sync_playwright, expect

parser = ArgumentParser()
parser.add_argument("--base", default="http://127.0.0.1:5187")
parser.add_argument("--browser", default="chrome", choices=["chrome", "msedge", "firefox", "webkit"])
parser.add_argument("--evidence", type=Path)
parser.add_argument("--group", choices=["motion", "store", "resilience", "layout", "lifecycle", "all"], default="all")
args = parser.parse_args()
if urlsplit(args.base).hostname not in ("localhost", "127.0.0.1", "::1"):
    parser.error("Use a loopback test server. The store scenario creates a test user and an order.")


def open_page(page, path="/"):
    response = page.goto(args.base + path)
    assert response.ok, (path, response.status)
    expect(page.locator("main")).to_be_visible()


def test_full(browser):
    durations = []
    for os_motion, saved in (("no-preference", "reduced"), ("reduce", "system")):
        page = browser.new_page(viewport={"width": 1440, "height": 900}, reduced_motion=os_motion)
        page.add_init_script(f"localStorage.setItem('storefront:motion', '{saved}')")
        page.add_init_script("""window.events = [];
          for (const name of ['animationstart', 'transitionrun']) {
            document.addEventListener(name, event => events.push({
              name: event.animationName ?? event.propertyName, target: event.target.className
            }));
          }""")
        open_page(page)
        assert page.evaluate("matchMedia('(prefers-reduced-motion: reduce)').matches") == (os_motion == "reduce")
        expect(page.locator(".header select, [data-motion-control]")).to_have_count(0)
        expect(page.locator("[data-hero-controls] button")).to_have_count(2)
        expect(page.locator(".hero-carousel").get_by_role("button", name="Пауза карусели")).to_have_count(0)
        page.wait_for_function("() => events.some(e => e.name === 'page-enter')")
        nav = page.locator(".header nav a").first
        nav.hover()
        page.wait_for_function("() => events.some(e => e.name === 'transform' && e.target === '')")
        page.wait_for_function("() => getComputedStyle(document.querySelector('.header nav a')).transform.includes('-2')")
        duration = nav.evaluate("(link) => getComputedStyle(link).transitionDuration")
        durations.append(duration)
        page.locator("[data-hero-next]").click()
        expect(page.locator(".hero-slide").nth(1)).to_have_class("hero-slide is-active")
        page.wait_for_function("() => events.some(e => e.name === 'opacity' && e.target.includes('hero-slide'))")
        page.locator("[data-hero-prev]").click()
        expect(page.locator(".hero-slide").first).to_have_class("hero-slide is-active")
        page.locator("[data-reveal]").first.evaluate("(tile) => tile.scrollIntoView({block: 'center'})")
        page.wait_for_function("() => events.some(e => e.name === 'feature-appear')", timeout=2500)
        ticker = page.locator(".brand-ticker__track")
        page.locator(".brand-ticker").scroll_into_view_if_needed()
        ticker_sample = ticker.evaluate("(track) => getComputedStyle(track).transform")
        page.wait_for_function("""start => getComputedStyle(document.querySelector(
            '.brand-ticker__track')).transform !== start""", arg=ticker_sample)
        page.emulate_media(reduced_motion="reduce" if os_motion == "no-preference" else "no-preference")
        expect(ticker).to_have_css("animation-name", "brand-ticker-move")
        open_page(page, "/Cart")
        expect(page.locator(".header select")).to_have_count(0)
        page.wait_for_function("() => events.some(e => e.name === 'purchase-enter')")
        expect(page.locator(".cart-empty")).to_be_visible()
        page.close()
    assert len(set(durations)) == 1, durations
    print("PASS full in both OS media modes, including live changes and obsolete saved choices", durations)


def test_fallback(browser):
    page = browser.new_page(reduced_motion="reduce")
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.add_init_script("""delete Element.prototype.animate;
      delete window.IntersectionObserver;
      Object.defineProperty(window, 'localStorage', { get() { throw new Error('Blocked storage'); } });""")
    open_page(page)
    expect(page.locator("[data-reveal]").first).to_have_css("opacity", "1")
    page.locator("[data-hero-next]").click()
    expect(page.locator(".hero-slide").nth(1)).to_have_class("hero-slide is-active")
    open_page(page, "/Catalog/Details/1")
    next_button = page.locator("[data-gallery-next]")
    next_button.click()
    expect(page.locator("[data-gallery-counter]")).to_have_text("2 / 2")
    expect(page.locator(".detail-image")).not_to_have_attribute("aria-busy", "true")
    assert not errors, errors
    page.close()
    print("PASS without WAAPI, Observer and storage")



def capture(page, name, moving=False):
    if args.evidence:
        args.evidence.mkdir(parents=True, exist_ok=True)
        page.evaluate("() => document.fonts.ready")
        if not moving:
            page.evaluate("""() => {
              const settle = document.getAnimations().filter(animation =>
                animation.effect?.getComputedTiming().endTime < 2000);
              return Promise.race([Promise.allSettled(settle.map(animation => animation.finished)),
                                   new Promise(resolve => setTimeout(resolve, 2100))]);
            }""")
        page.screenshot(path=args.evidence / f"{args.browser}-{name}.png",
                        caret="hide", animations="allow", timeout=12000)


def select_size(page, size):
    page.locator(f'[data-size-options]:not([hidden]) label:has(input[value="{size}"])').click()


def add_to_cart(page, size, total):
    select_size(page, size)
    button = page.locator(".product-order-controls__submit")
    button.click()
    expect(page.locator("[data-cart-count]")).to_have_text(str(total))
    expect(button).not_to_have_attribute("aria-busy", "true")


def test_store(browser):
    context = browser.new_context(viewport={"width": 1440, "height": 900}, reduced_motion="reduce")
    if args.evidence:
        context.tracing.start(screenshots=True, snapshots=True)
    page = context.new_page()
    page.set_default_timeout(8000)
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    try:
        open_page(page, "/Catalog")
        expect(page.locator("[data-product-card]")).to_have_count(3)
        brand = page.locator(".catalog-brand input").first
        brand.check()
        page.locator(".catalog-submit").click()
        expect(page.locator("[data-product-card]")).to_have_count(2)
        brand.uncheck()
        page.locator("#catalog-sort").select_option("price-asc")
        page.locator(".catalog-submit").click()
        expect(page.locator("[data-product-card]").first).to_contain_text("530")
        search = page.locator("#catalog-search")
        search.fill("9060")
        search.fill("does not exist")
        expect(page.locator("#catalog-results .catalog-empty")).to_be_visible()
        search.fill("9060")
        expect(page.locator("[data-product-card]")).to_have_count(1)
        page.locator(".product-card__link").click()
        page.wait_for_url("**/Catalog/Details/**")
        expect(page.locator("[data-gallery-main]")).to_be_visible()
        page.locator("[data-gallery-next]").click()
        page.wait_for_function("""() => {
          const buffer = document.querySelector('[data-gallery-buffer]');
          const alpha = Number(getComputedStyle(buffer).opacity);
          return alpha > 0 && alpha < 1;
        }""")
        capture(page, "gallery-motion", moving=True)
        page.locator(".detail-image").evaluate(
            "(frame) => frame.getAnimations({ subtree: true }).forEach(animation => animation.cancel())")
        expect(page.locator("[data-gallery-buffer]")).not_to_have_attribute("src", re.compile(r".+"))
        expect(page.locator(".detail-image")).not_to_have_attribute("aria-busy", "true")
        expect(page.locator("[data-gallery-counter]")).to_have_text("2 / 2")
        variants = page.locator(".details [data-variant-choice]")
        variants.nth(1).click()
        variants.nth(0).click()
        variants.nth(1).click()
        expect(variants.nth(1)).to_have_attribute("aria-pressed", "true")
        expect(page.locator("[data-variant-input]")).to_have_value("2")
        expect(page.locator("[data-gallery-main]")).to_be_visible()
        expect(page.locator(".detail-image")).not_to_have_attribute("aria-busy", "true")

        page.route("**/Cart/Add", lambda route: route.fulfill(
            status=400, json={"message": "Тестовая ошибка корзины"}), times=1)
        select_size(page, 37)
        page.locator(".product-order-controls__submit").click()
        expect(page.locator("[data-cart-error]")).to_have_text("Тестовая ошибка корзины")
        expect(page.locator(".cart-flying-product")).to_have_count(0)
        expect(page.locator("[data-cart-count]")).to_have_text("0")

        add_to_cart(page, 37, 1)
        if args.browser in ("chrome", "msedge"):
            assert page.locator(".cart-flying-product").count() == 1
        add_to_cart(page, 38, 2)
        page.evaluate("""() => document.querySelector('.cart-flying-product')
          ?.getAnimations().forEach(animation => animation.cancel())""")
        expect(page.locator(".cart-flying-product")).to_have_count(0)
        page.locator("[aria-label='Корзина']").click()
        page.wait_for_url("**/Cart")
        lines = page.locator("[data-cart-line]:not([data-motion-exit])")
        expect(lines).to_have_count(2)
        plus = lines.first.locator("button[value='1']")
        plus.evaluate("(button) => { button.click(); button.click(); }")
        expect(lines.first.locator("[data-cart-line-quantity]")).to_have_text("3")
        expect(page.locator("[data-cart-count]")).to_have_text("4")
        capture(page, "cart")
        lines.first.locator("[data-cart-remove-form] button").click()
        expect(lines).to_have_count(1)
        expect(page.locator("[data-cart-count]")).to_have_text("1")

        page.locator("[data-cart-checkout]").click()
        page.wait_for_url("**/Account/Login?**")
        page.locator(".account-secondary").click()
        page.wait_for_url("**/Account/Register?**")
        email = "motion-" + uuid4().hex + "@example.test"
        password = "Motion_Test#2026"
        page.locator("#FirstName").fill("Тестовый покупатель")
        page.locator("#Email").fill(email)
        page.locator("#Password").fill(password)
        page.locator("#ConfirmPassword").fill(password)
        page.locator("#AcceptTerms").check()
        page.locator("[data-account-form] button[type='submit']").click()
        page.wait_for_url("**/Account/Login?**")
        page.locator("#Email").fill(email)
        page.locator("#Password").fill(password)
        page.locator("[data-password-toggle]").click()
        expect(page.locator("#Password")).to_have_attribute("type", "text")
        page.locator("[data-password-toggle]").click()
        page.locator("[data-account-form] button[type='submit']").click()
        page.wait_for_url("**/Cart/Checkout")
        expect(page.locator("[data-checkout-form]")).to_be_visible()
        page.locator("label:has(#DeliveryMethod-courier)").click()
        expect(page.locator("#Street")).to_be_enabled()
        page.locator("label:has(#DeliveryMethod-collection)").click()
        page.wait_for_function("""() => document.querySelector('[data-delivery-fields]')
          .getAnimations().some(animation => animation.currentTime > 0)""")
        expect(page.locator("#Street")).to_be_disabled()
        expect(page.locator("#City")).to_be_enabled()
        page.locator("label:has(#DeliveryMethod-pickup)").click()
        expect(page.locator("#City")).to_be_disabled()
        page.locator("#Phone").fill("")
        page.locator(".checkout-submit").click()
        expect(page.locator("#checkout-phone-error")).not_to_be_empty()
        page.locator("#Phone").fill("+79000000000")
        saved_email = page.locator("#Email").input_value()
        page.locator("#Email").fill("invalid")
        page.locator("[data-checkout-form]").evaluate("(form) => { form.noValidate = true; }")
        with page.expect_navigation():
            page.locator(".checkout-submit").click()
        expect(page.locator("#checkout-email-error")).not_to_be_empty()
        expect(page.locator("#Email")).to_have_value("invalid")
        expect(page.locator(".checkout-submit")).not_to_have_attribute("aria-busy", "true")
        page.locator("#Email").fill(saved_email)
        capture(page, "checkout")
        page.locator(".checkout-submit").evaluate("(button) => { button.click(); button.click(); }")
        page.wait_for_url("**/Cart/Success/**")
        expect(page.locator("#success-title")).to_have_text("Заказ принят")
        expect(page.locator(".order-success__number")).to_contain_text("№")
        capture(page, "success")
        page.locator(".order-success__link").click()
        page.wait_for_url("**/Account/Orders/**")
        expect(page.locator("#account-content")).to_be_visible()
        expect(page.locator(".customer-account__nav-link[aria-current='page']")).to_contain_text("Мои заказы")

        trigger = page.locator("[data-account-menu-trigger]")
        trigger.focus()
        page.keyboard.press("Escape")
        expect(trigger).to_be_focused()
        page.keyboard.press("ArrowDown")
        expect(trigger).to_have_attribute("aria-expanded", "true")
        expect(page.locator("[role='menuitem']").first).to_be_focused()
        page.keyboard.press("Escape")
        expect(trigger).to_be_focused()
        expect(trigger).to_have_attribute("aria-expanded", "false")
        trigger.click()
        page.locator("#account-content h1").click()
        expect(trigger).to_have_attribute("aria-expanded", "false")

        open_page(page, "/Account/Orders")
        expect(page.locator(".account-orders--history .account-order-row")).to_have_count(1)

        open_page(page, "/Account/Profile")
        page.locator("#DisplayName").fill("Тестовое имя")
        page.locator("[data-customer-profile] button[type='submit']").click()
        expect(page.locator("#DisplayName")).to_have_value("Тестовое имя")
        expect(page.locator(".customer-profile__notice")).to_be_visible()
        avatar = b64decode("iVBORw0KGgoAAAANSUhEUgAAABAAAAAMCAYAAABr5z2BAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAaSURBVDhPY7gjYvOfEjxqwKgBIDzkDbD5DwATUqBQPwgNzAAAAABJRU5ErkJggg==")
        page.locator("#profile-photo").set_input_files(
            {"name": "test-avatar.png", "mimeType": "image/png", "buffer": avatar})
        expect(page.locator(".account-avatar-notice")).to_be_visible()
        capture(page, "profile")

        open_page(page, "/Catalog")
        hearts = page.locator(".favorite-button")
        page.route("**/Account/Favorites", lambda route: route.fulfill(
            status=500, json={}), times=1)
        hearts.nth(0).click()
        expect(page.locator("[data-favorite-error]")).to_be_visible()
        expect(hearts.nth(0)).to_have_attribute("aria-pressed", "false")
        for position in (0, 1):
            hearts.nth(position).click()
            expect(hearts.nth(position)).to_have_attribute("aria-pressed", "true")
        open_page(page, "/Account/Favorites")
        cards = page.locator("[data-favorites-list] [data-product-card]:not([data-motion-exit])")
        expect(cards).to_have_count(2)
        expect(page.locator("[data-favorites-count]")).to_have_text("2")
        cards.first.locator(".favorite-button").click()
        expect(cards).to_have_count(1)
        expect(page.locator("[data-favorites-count]")).to_have_text("1")
        cards.first.locator(".favorite-button").click()
        expect(page.locator("[data-favorite-empty]")).to_be_visible()
        expect(page.locator("[data-favorites-count]")).to_have_text("0")
        open_page(page, "/Cart")
        expect(page.locator("[data-cart-empty]")).to_be_visible()
        page.evaluate("() => history.back()")
        expect(page).to_have_url(re.compile(r"/Account/Favorites$"))
        expect(page.locator("[data-favorite-empty]")).to_be_visible()
        page.evaluate("() => history.forward()")
        expect(page).to_have_url(re.compile(r"/Cart$"))
        expect(page.locator("[data-cart-empty]")).to_be_visible()
        page.locator("[data-account-menu-trigger]").click()
        page.locator("[data-account-menu-panel] button[type='submit']").click()
        expect(page.locator(".header-actions > a[href*='Login']")).to_be_visible()
        assert not errors, errors
        print("PASS search, gallery cancellation, cart error/queue, registration/login, real checkout, profile/avatar, favorite rollback, history and logout")
    finally:
        if args.evidence:
            context.tracing.stop(path=args.evidence / f"{args.browser}-store-trace.zip")
        context.close()


def test_resilience(browser):
    page = browser.new_page(reduced_motion="reduce")
    for script in ("page-entry", "motion-runtime"):
        page.route(f"**/js/{script}.js?*", lambda route: route.abort())
    page.route("**/fonts/*.woff2*", lambda route: route.abort())
    page.route("**/images/products/9060.jpg*", lambda route: route.abort())
    page.add_init_script("""document.addEventListener('DOMContentLoaded', () => {
      const style = document.createElement('style');
      style.textContent = '*, *::before, *::after { animation: none !important; transition: none !important; }';
      document.head.append(style);
    });""")
    open_page(page, "/")
    page.locator("[data-reveal]").first.evaluate("(tile) => tile.scrollIntoView({block: 'center'})")
    expect(page.locator("[data-reveal]").first).to_have_css("opacity", "1")
    expect(page.locator(".header")).to_have_css("opacity", "1")
    open_page(page, "/Catalog/Details/1")
    expect(page.locator("[data-gallery-empty]")).to_be_visible()
    page.locator("[data-gallery-next]").click()
    expect(page.locator("[data-gallery-main]")).to_be_visible()
    add_to_cart(page, 37, 1)
    open_page(page, "/Cart")
    expect(page.locator("[data-cart-line]")).to_have_count(1)
    page.locator("[data-cart-remove-form] button").click()
    expect(page.locator("[data-cart-empty]")).to_be_visible()
    page.close()

    if args.browser in ("chrome", "msedge"):
        page = browser.new_page()
        def originals_only(route):
            response = route.fetch()
            headers = {**response.headers}
            headers["content-security-policy"] = headers["content-security-policy"].replace(
                "img-src 'self' data: blob:", "img-src 'self'")
            route.fulfill(response=response, headers=headers)
        page.route("**/Catalog/Details/1", originals_only)
        open_page(page, "/Catalog/Details/1")
        expect(page.locator("[data-gallery-main]")).to_have_attribute("data-photo-state", "original")
        expect(page.locator("[data-gallery-main]")).to_be_visible()
        page.locator("[data-gallery-next]").click()
        expect(page.locator("[data-gallery-counter]")).to_have_text("2 / 2")
        expect(page.locator(".detail-image")).not_to_have_attribute("aria-busy", "true")
        expect(page.locator("[data-gallery-main]")).to_be_visible()
        page.close()

    page = browser.new_page(java_script_enabled=False, reduced_motion="reduce")
    open_page(page)
    expect(page.locator(".hero-slide.is-active h1")).to_be_visible()
    expect(page.locator("[data-hero-controls]")).to_be_hidden()
    expect(page.locator(".header select")).to_have_count(0)
    page.locator(".header nav a").first.click()
    expect(page.locator("[data-product-card]")).to_have_count(3)
    page.goto(args.base + "/Catalog/Details/1")
    select_size(page, 37)
    page.locator(".product-order-controls__submit").click()
    page.wait_for_url("**/Cart")
    expect(page.locator("[data-cart-line]")).to_have_count(1)
    page.close()

    page = browser.new_page(reduced_motion="reduce")
    page.add_init_script("""window.entries = [];
      document.addEventListener('animationstart', event => entries.push(event.animationName));""")
    def delayed_resource(route):
        sleep(.3)
        route.continue_()
    page.route("**/js/page-entry.js?*", delayed_resource)
    page.route("**/fonts/*.woff2*", delayed_resource)
    open_page(page)
    page.wait_for_function("() => entries.includes('page-enter')")
    expect(page.locator(".hero-slide.is-active h1")).to_be_visible()
    page.close()
    print("PASS blocked motion, CSS, font, photo and blob; server forms without JS; slow entry script/fonts")



def test_layout(browser):
    for width, height in ((1920, 1080), (1600, 900), (1440, 900), (1366, 768),
                          (768, 1024), (430, 900), (390, 844), (360, 800), (320, 800)):
        page = browser.new_page(viewport={"width": width, "height": height},
                                has_touch=width < 500, color_scheme="dark",
                                device_scale_factor=2 if width < 500 else 1.25)
        open_page(page)
        assert page.evaluate("() => document.documentElement.scrollWidth") <= width, ("home overflow", width)
        expect(page.locator("html")).to_have_css("color-scheme", "light")
        for path, target in (("/Catalog", "#catalog-title"), ("/Catalog/Details/1", ".details h1"),
                             ("/Account/Login", ".account-page h1"), ("/Cart", "#cart-title")):
            open_page(page, path)
            expect(page.locator(target)).to_be_visible()
            measured = page.evaluate("() => document.documentElement.scrollWidth")
            assert measured <= width, (path, width, measured)
        if width == 360:
            open_page(page)
            expect(page.locator("[data-hero-controls] button")).to_have_count(2)
            page.locator("[data-hero-next]").tap()
            expect(page.locator(".hero-slide.is-active h1")).to_contain_text("3XL")
            assert page.locator(".hero-slide.is-active").evaluate("""slide => {
              const copy = slide.querySelector('.eyebrow').getBoundingClientRect();
              const label = slide.querySelector('.hero-model').getBoundingClientRect();
              return copy.top >= label.bottom;
            }""")
            capture(page, "mobile-360")
        page.close()
    page = browser.new_page(forced_colors="active")
    open_page(page)
    assert page.evaluate("matchMedia('(forced-colors: active)').matches")
    page.keyboard.press("Tab")
    assert page.evaluate("() => getComputedStyle(document.activeElement).outlineStyle") == "solid"
    capture(page, "forced-colors")
    page.close()
    print("PASS responsive widths 320–1920, DPR, touch, dark system and forced colors")



def test_lifecycle(browser):
    context = browser.new_context(viewport={"width": 1440, "height": 900})
    page = context.new_page()
    page.add_init_script("""window.pageShows = []; window.sampledHidden = null;
      addEventListener('pageshow', event => pageShows.push(event.persisted));
      const hidden = Object.getOwnPropertyDescriptor(Document.prototype, 'hidden').get;
      Object.defineProperty(document, 'hidden', { get: () => sampledHidden ?? hidden.call(document) });""")
    open_page(page)
    system_signal = page.evaluate("matchMedia('(prefers-reduced-motion: reduce)').matches")
    fonts = page.evaluate("""async () => {
      await Promise.all([document.fonts.load('400 12px Inter', 'Каталог'), document.fonts.load('800 12px Inter', 'RONIN')]);
      return [...document.fonts].filter(face => face.family === 'Inter').every(face => face.status === 'loaded');
    }""")
    assert fonts, "Inter Latin and Cyrillic must both be loaded"
    page.wait_for_function("() => document.querySelector('.hero-slide.is-active').classList.contains('is-pair')", timeout=7000)
    expect(page.locator(".hero-slide").nth(1)).to_have_class("hero-slide is-active", timeout=7500)
    expect(page.locator(".hero-carousel")).to_have_class(re.compile(r"\bis-playing\b"))

    page.evaluate("""() => {
      sampledHidden = true;
      document.dispatchEvent(new Event('visibilitychange'));
    }""")
    expect(page.locator(".hero-carousel")).not_to_have_class(re.compile(r"\bis-playing\b"))
    page.evaluate("""() => {
      sampledHidden = null;
      document.dispatchEvent(new Event('visibilitychange'));
    }""")
    expect(page.locator(".hero-carousel")).to_have_class(re.compile(r"\bis-playing\b"))
    page.mouse.move(1100, 650)
    page.mouse.wheel(0, 520)
    page.wait_for_function("() => Math.abs(scrollY - 520) <= 2")
    page.locator(".brand-ticker").evaluate("(e) => e.scrollIntoView({block: 'center'})")
    track = page.locator(".brand-ticker__track")
    expect(track).to_have_css("animation-play-state", "running")
    page.locator("[data-ticker-pause]").click()
    expect(track).to_have_css("animation-play-state", "paused")
    page.locator("[data-ticker-pause]").click()
    expect(track).to_have_css("animation-play-state", "running")

    with context.expect_page() as opened:
        page.locator(".header nav a").nth(1).click(modifiers=["Control"])
    second_page = opened.value
    expect(second_page).to_have_url(re.compile(r"/Catalog\?tag=new$"))
    expect(second_page.locator(".header select")).to_have_count(0)
    second_page.close()
    page.locator(".header nav a").first.click()
    page.wait_for_url("**/Catalog")
    page.evaluate("() => history.back()")
    expect(page).to_have_url(args.base + "/")
    expect(page.locator(".hero-slide.is-active h1")).to_be_visible()
    expect(page.locator("html")).not_to_have_attribute("data-motion-entry", "")
    cached = page.evaluate("() => pageShows.includes(true)")
    open_page(page, "/Catalog/Details/1")
    add_to_cart(page, 37, 1)
    page.evaluate("() => history.back()")
    expect(page).to_have_url(args.base + "/")
    expect(page.locator("[data-cart-count]")).to_have_text("1")
    if cached:
        assert page.evaluate("() => pageShows.includes(true)"), "the updated badge came from a restored document"
    open_page(page, "/Catalog")
    page.locator("#catalog-search").evaluate("""search => {
      search.value = 'no-matches-before-back';
      search.dispatchEvent(new Event('input', { bubbles: true }));
      document.querySelector('.product-card__link').click();
    }""")
    page.wait_for_url("**/Catalog/Details/**")
    page.evaluate("() => history.back()")
    expect(page).to_have_url(re.compile(r"/Catalog(?:\?|$)"))
    if page.evaluate("() => pageShows.includes(true)"):
        expect(page.locator("#catalog-results .catalog-empty")).to_be_visible()


    print(f"PASS real carousel auto-advance, manual ticker pause, simulated hidden tab, opening navigation in a new tab, fonts, history, restored cart badge and search; native OS reduce={system_signal}, bfcache={cached}")
    context.close()


def run(browser):
    print(args.browser, browser.version)
    if args.group in ("motion", "all"):
        test_full(browser)
        test_fallback(browser)
    if args.group in ("store", "all"):
        test_store(browser)
    if args.group in ("resilience", "all"):
        test_resilience(browser)
    if args.group in ("lifecycle", "all"):
        test_lifecycle(browser)
    if args.group in ("layout", "all"):
        test_layout(browser)


if __name__ == "__main__":
    with sync_playwright() as playwright:
        if args.browser in ("chrome", "msedge"):
            browser = playwright.chromium.launch(channel=args.browser, ignore_default_args=["--disable-back-forward-cache"])
        else:
            browser = getattr(playwright, args.browser).launch()
        run(browser)

