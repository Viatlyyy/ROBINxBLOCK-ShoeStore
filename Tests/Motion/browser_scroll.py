"""Browser regressions for wheel smoothing. Requires the local storefront and Playwright."""
from argparse import ArgumentParser
from pathlib import Path
from urllib.parse import urlsplit

from playwright.sync_api import sync_playwright

parser = ArgumentParser()
parser.add_argument("--base", default="http://127.0.0.1:5195")
parser.add_argument("--browser", choices=["chrome", "msedge", "firefox", "webkit"], default="chrome")
parser.add_argument("--script", type=Path)
args = parser.parse_args()
if urlsplit(args.base).hostname not in ("localhost", "127.0.0.1", "::1"):
    parser.error("Run against a local storefront.")

INIT = """() => {
  window.scrollWrites = [];
  window.scrollProbe = { wheel: [], next: 0 };
  window.addEventListener('wheel', event => scrollProbe.wheel.push([scrollY, event.deltaY]), {capture: true, passive: true});
  const native = window.scrollTo.bind(window);
  window.scrollTo = (...args) => {
    scrollWrites.push(typeof args[0] === 'object' ? args[0].top : args[1]);
    return native(...args);
  };
  const frame = requestAnimationFrame;
  window.requestAnimationFrame = fn => frame.call(window, time => {
    scrollProbe.next++;
    fn(time);
  });
}"""

def prepare(page):
    assert page.goto(args.base + "/Catalog").ok
    # Provide deterministic room for resizing and distances, retaining the real storefront DOM.
    page.evaluate("""() => {
      const extra = Object.assign(document.createElement('div'), {id:'scroll-test-space'});
      extra.style.height = '5000px';
      document.body.append(extra);
      window.scrollTo({top:1000,behavior:'instant'});
    }""")
    page.mouse.move(690, 590)
    page.wait_for_timeout(160)

def y(page):
    return page.evaluate("scrollY")

def near(page, expected):
    page.wait_for_function("v => Math.abs(scrollY - v) <= 2", arg=expected, timeout=1800)

def wheel(page, amount):
    page.mouse.wheel(0, amount)

def grows(page):
    wheel(page, 480)
    page.wait_for_function("() => scrollY > 1020")
    page.evaluate("document.querySelector('#scroll-test-space').style.height = '5450px'")
    near(page, 1480)

def shrinks_at_end(page):
    old = page.evaluate("document.scrollingElement.scrollHeight - innerHeight")
    page.evaluate("window.scrollTo({top: document.scrollingElement.scrollHeight - innerHeight - 250,behavior:'instant'})")
    wheel(page, 900)
    page.wait_for_function("target => scrollY > target - 220", arg=old)
    maximum = page.evaluate("""() => {
      document.querySelector('#scroll-test-space').style.height='4900px';
      return document.scrollingElement.scrollHeight-innerHeight;
    }""")
    near(page, maximum)
    page.wait_for_timeout(700)
    count = page.evaluate('scrollProbe.next')
    page.wait_for_timeout(130)
    assert page.evaluate('scrollProbe.next') == count

def reverse(page):
    wheel(page, 500)
    page.wait_for_function("() => scrollY > 1040")
    wheel(page, -45)
    page.wait_for_function("() => scrollProbe.wheel.at(-1)?.[1] === -45")
    at = page.evaluate("scrollProbe.wheel.at(-1)[0]")
    near(page, at - 45)

def external_before_first_frame(page):
    page.evaluate("""() => {
      document.body.dispatchEvent(new WheelEvent('wheel', {bubbles:true,cancelable:true,deltaY:420}));
      window.scrollTo({top:2300,behavior:'instant'});
    }""")
    page.wait_for_timeout(720)
    assert abs(y(page)-2300) <= 1

def external_with_new_wheel(page):
    wheel(page, 500)
    page.wait_for_function("() => scrollY > 1040")
    page.evaluate("""() => {
      document.querySelector('#scroll-test-space').style.height = '5450px';
      window.scrollTo({top:2300,behavior:'instant'});
      document.body.dispatchEvent(new WheelEvent('wheel', {bubbles:true,cancelable:true,deltaY:60}));
    }""")
    near(page,2360)

def external(page):
    wheel(page, 500)
    page.wait_for_function("() => scrollY > 1040")
    page.evaluate("window.scrollTo({top: 2300, behavior: 'instant'})")
    page.wait_for_timeout(720)
    assert abs(y(page)-2300) <= 1
    wheel(page, 60)
    near(page, 2360)

def small_and_delta_mode(page):
    start=y(page)
    result=page.evaluate("""() => {
      const fire = options => document.body.dispatchEvent(new WheelEvent('wheel', {
        bubbles: true, cancelable: true, ...options
      }));
      for (const deltaY of [.2,.2,.2,.2,.2,.2,.2,.2,.2,.2]) fire({deltaY});
      return [fire({deltaY:100,ctrlKey:true}),
        fire({deltaX:70,deltaY:10}), fire({deltaY:11,cancelable:false})];
    }""")
    assert result==[True,True,True]
    # Modifier and horizontal gestures transfer ownership, so test precision after them.
    page.evaluate("""() => {
      for (let i=0;i<10;i++) document.body.dispatchEvent(new WheelEvent('wheel',
        {bubbles:true,cancelable:true,deltaY:.4}));
    }""")
    page.wait_for_function("start => scrollY >= start + 2", arg=start, timeout=4000)
    page.wait_for_timeout(500)
    near(page,start+4)
    page.evaluate("""document.body.dispatchEvent(new WheelEvent('wheel',
      {bubbles:true,cancelable:true,deltaY:3,deltaMode:1}))""")
    near(page,start+52)
    page.evaluate("""document.body.dispatchEvent(new WheelEvent('wheel',
      {bubbles:true,cancelable:true,deltaY:1,deltaMode:2}))""")
    near(page,start+952)

def nested_and_lock(page):
    page.evaluate("""() => {
      const box=Object.assign(document.createElement('div'), {id:'scroll-test-box'});
      box.style.cssText='position:fixed;z-index:99;top:100px;left:20px;width:210px;height:160px;overflow:auto;overscroll-behavior-y:contain';
      box.innerHTML='<div style="height:900px">scroll area</div>';
      document.body.append(box);
    }""")
    start=y(page)
    page.mouse.move(60,160)
    wheel(page, 110)
    page.wait_for_function("() => document.querySelector('#scroll-test-box').scrollTop >= 100")
    assert abs(y(page)-start)<=1.25, (start,y(page))
    page.evaluate("document.querySelector('#scroll-test-box').scrollTop=999")
    containment = page.evaluate("""() =>
      getComputedStyle(document.querySelector('#scroll-test-box')).overscrollBehaviorY === 'contain'""")
    wheel(page,100)
    if containment:
        page.wait_for_timeout(170)
        assert abs(y(page)-start)<=1.25, (start,y(page))
    else:
        # A browser without this CSS feature retains its native scroll chaining.
        near(page,start+100)
        page.evaluate("start => window.scrollTo({top:start,behavior:'instant'})",start)
        near(page,start)
    page.evaluate("document.querySelector('#scroll-test-box').style.overscrollBehaviorY='auto'")
    wheel(page,90)
    near(page,start+90)
    page.mouse.move(700,550)
    wheel(page,450)
    page.wait_for_function("() => scrollY>1109")
    page.evaluate("document.body.style.overflow='hidden'")
    page.wait_for_timeout(110)
    stopped=y(page)
    wheel(page,100)
    page.wait_for_timeout(450)
    assert abs(y(page)-stopped)<=1.25, ("body lock",stopped,y(page))
    page.evaluate("document.body.style.overflow=''")
    wheel(page,70)
    near(page,stopped+70)

def duplicate_attach(page):
    page.evaluate("""async () => {
      const original=Array.from(document.scripts).find(script=>script.src.includes('/smooth-scroll.js'));
      await new Promise((resolve,reject)=>{
        const duplicate=document.createElement('script');
        duplicate.src=original.src;
        duplicate.onload=resolve;
        duplicate.onerror=reject;
        document.body.append(duplicate);
      });
    }""")
    wheel(page,120)
    near(page,1120)
    page.wait_for_timeout(700)
    count=page.evaluate('scrollProbe.next')
    page.wait_for_timeout(120)
    assert page.evaluate('scrollProbe.next')==count

def visibility_handoff(page):
    wheel(page, 450)
    page.wait_for_function("() => scrollY > 1020")
    started = page.evaluate("""() => {
      Object.defineProperty(document,'hidden',{configurable:true,value:true});
      document.dispatchEvent(new Event('visibilitychange'));
      return scrollY;
    }""")
    page.wait_for_timeout(300)
    assert y(page)==started
    page.evaluate("""() => {
      delete document.hidden;
      document.dispatchEvent(new Event('visibilitychange'));
    }""")
    wheel(page,50)
    near(page,started+50)

def route_and_keyboard(page):
    wheel(page,420)
    page.wait_for_function("() => scrollY>1020")
    page.keyboard.press('Home')
    near(page,0)
    page.wait_for_timeout(500)
    assert y(page)==0
    wheel(page,180)
    near(page,180)
    assert page.goto(args.base+'/').ok
    page.evaluate('location.hash="catalog"')
    page.wait_for_function("() => scrollY > 300")
    page.wait_for_timeout(420)
    page.evaluate("window.scrollTo({top:420,behavior:'instant'})")
    near(page,420)
    page.locator('.header nav a').first.click()
    page.wait_for_url("**/Catalog")
    page.evaluate("history.back()")
    page.wait_for_url("**/#catalog", wait_until="commit")
    page.wait_for_function("() => scrollY > 300")
    page.wait_for_timeout(350)
    restored = y(page)
    wheel(page,120)
    near(page,restored+120)

with sync_playwright() as playwright:
    browser=(playwright.chromium.launch(channel=args.browser, ignore_default_args=["--disable-back-forward-cache"])
             if args.browser in ('chrome','msedge') else getattr(playwright,args.browser).launch())
    print(args.browser, browser.version, flush=True)
    failed=[]
    for check in [grows, shrinks_at_end, reverse, external_before_first_frame, external_with_new_wheel, external, small_and_delta_mode,
                  nested_and_lock, duplicate_attach, visibility_handoff, route_and_keyboard]:
        page=browser.new_page(viewport={"width":1440,"height":900},reduced_motion='reduce')
        page.set_default_timeout(6000)
        if args.script:
            page.route("**/js/smooth-scroll.js*", lambda route: route.fulfill(
                path=args.script, content_type="application/javascript"))
        page.add_init_script(f"({INIT})()")
        prepare(page)
        try:
            check(page)
            print("PASS",check.__name__,flush=True)
        except Exception as error:
            failed.append(check.__name__)
            print("FAIL",check.__name__, "scrollY", y(page),(str(error).splitlines() or [type(error).__name__])[0],flush=True)
        finally:
            page.close()
    browser.close()
    assert not failed, failed


