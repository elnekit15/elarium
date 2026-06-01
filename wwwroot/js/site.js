// =========================================================
//  WISHLIST TOGGLE
// =========================================================
var _wishToastTimer = null;

function toggleWishlist(event, btn) {
    event.preventDefault();   // не переходити за посиланням картки
    event.stopPropagation();

    var productId   = btn.dataset.id;
    var productName = btn.dataset.name || '';

    var fd = new FormData();
    fd.append('productId', productId);

    fetch('/Wishlist/Toggle', { method: 'POST', body: fd })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            // Оновити всі серця з тим самим data-id на сторінці
            document.querySelectorAll(
                '.wish-btn[data-id="' + productId + '"],'
                + '.wish-btn-text[data-id="' + productId + '"]'
            ).forEach(function (b) {
                if (data.added) {
                    b.classList.add('wish-btn--active', 'wish-btn-text--active');
                    var span = b.querySelector('span');
                    if (span) span.textContent = 'В обраному';
                } else {
                    b.classList.remove('wish-btn--active', 'wish-btn-text--active');
                    var span = b.querySelector('span');
                    if (span) span.textContent = 'Додати до обраного';
                    // На сторінці /Wishlist — прибрати картку
                    var card = document.getElementById('wish-card-' + productId);
                    if (card) card.style.transition = 'opacity 0.3s';
                    if (card) { card.style.opacity = '0'; setTimeout(function() { card.remove(); }, 300); }
                }
            });

            // Оновити бейдж у хедері
            var badge = document.getElementById('wish-count-badge');
            if (badge) {
                badge.textContent = data.count;
                badge.style.display = data.count > 0 ? 'flex' : 'none';
            }

            showWishToast(productName, data.added);
        })
        .catch(function () {
            // Fallback — нічого не робимо
        });
}

function showWishToast(productName, added) {
    var toast  = document.getElementById('wish-toast');
    var textEl = document.getElementById('wish-toast-text');
    var link   = document.getElementById('wish-toast-link');
    if (!toast) return;

    if (textEl) textEl.textContent = added
        ? (productName ? productName + ' — додано до обраного' : 'Додано до обраного')
        : 'Видалено з обраного';
    if (link)  link.style.display = added ? '' : 'none';

    toast.classList.add('show');
    clearTimeout(_wishToastTimer);
    _wishToastTimer = setTimeout(function () {
        toast.classList.remove('show');
    }, 4000);
}

// =========================================================
//  CART TOAST NOTIFICATION
// =========================================================
var _cartToastTimer = null;

function updateCartBadge(count) {
    var badge = document.getElementById('cart-count-badge');
    if (badge) {
        badge.textContent = count;
        badge.style.display = count > 0 ? 'flex' : 'none';
    }
}

function showCartToast(productName, cartCount) {
    var toast  = document.getElementById('cart-toast');
    var nameEl = document.getElementById('cart-toast-name');
    if (!toast) return;

    if (nameEl) nameEl.textContent = productName || '';

    // Update cart badge
    if (cartCount !== undefined && cartCount !== null) {
        updateCartBadge(cartCount);
    }

    // Show
    toast.classList.add('show');
    clearTimeout(_cartToastTimer);
    _cartToastTimer = setTimeout(function () {
        toast.classList.remove('show');
    }, 2600);
}

// BFCache fix — refresh cart badge when user navigates back/forward
window.addEventListener('pageshow', function (e) {
    if (e.persisted) {
        fetch('/Cart/Count')
            .then(function (r) { return r.json(); })
            .then(function (data) { updateCartBadge(data.count); })
            .catch(function () {});
    }
});

// =========================================================
//  PRODUCT ROLLER
// =========================================================
(function () {
    const track = document.getElementById('roller-track');
    const outer = document.getElementById('roller-track-outer');
    const prev  = document.getElementById('roller-prev');
    const next  = document.getElementById('roller-next');
    if (!track || !outer || !prev || !next) return;

    let current = 0;

    function cardStep() {
        const card = track.querySelector('.roller-card');
        if (!card) return 234;
        return card.getBoundingClientRect().width + 24;
    }

    function visibleCount() {
        return Math.max(1, Math.floor(outer.offsetWidth / cardStep()));
    }

    function total() {
        return track.querySelectorAll('.roller-card').length;
    }

    function update() {
        const maxIndex = Math.max(0, total() - visibleCount());
        if (current > maxIndex) current = maxIndex;
        track.style.transform = `translateX(${-(current * cardStep()).toFixed(1)}px)`;
        prev.disabled = current === 0;
        next.disabled = current >= maxIndex;
    }

    prev.addEventListener('click', function () {
        if (current > 0) { current--; update(); }
    });

    next.addEventListener('click', function () {
        if (current < total() - visibleCount()) { current++; update(); }
    });

    update();
    window.addEventListener('resize', update, { passive: true });
})();

// =========================================================
//  HEADER: hide on scroll down, reveal on scroll up
// =========================================================
(function () {
    const header = document.querySelector('.site-header');
    if (!header) return;
    let lastY = window.scrollY;
    window.addEventListener('scroll', function () {
        const y = window.scrollY;
        if (y > lastY && y > 60) {
            header.classList.add('header-hidden');
        } else {
            header.classList.remove('header-hidden');
        }
        lastY = y;
    }, { passive: true });
})();

// =========================================================
//  FILTER PANEL
// =========================================================
(function () {
    const btn     = document.getElementById('filter-btn');
    const panel   = document.getElementById('filter-panel');
    const close   = document.getElementById('filter-close');
    const overlay = document.getElementById('filter-overlay');
    if (!btn || !panel) return;

    function open()  {
        panel.classList.add('open');
        overlay.classList.add('open');
        document.body.style.overflow = 'hidden';
    }
    function shut()  {
        panel.classList.remove('open');
        overlay.classList.remove('open');
        document.body.style.overflow = '';
    }

    btn.addEventListener('click', open);
    close?.addEventListener('click', shut);
    overlay?.addEventListener('click', shut);

    // ESC key closes panel
    document.addEventListener('keydown', e => { if (e.key === 'Escape') shut(); });
})();

// =========================================================
//  ELARIUM hero: breathing + scroll fade + motion blur trail
//  Mouse interaction only activates near/over the title text
// =========================================================
(function () {
    const banner = document.querySelector('.hero-banner');
    const title  = document.querySelector('.hero-title');
    if (!title) return;

    // --- Split ELARIUM into individual <span> letters ---
    title.innerHTML = [...title.textContent]
        .map(c => c === ' ' ? ' ' : `<span class="hero-letter">${c}</span>`)
        .join('');

    // --- State ---
    let t0          = null;
    let lastScrollY = window.scrollY;
    let blurTarget  = 0;   // scroll-driven blur
    let blurCurrent = 0;
    let mouseBlur   = 0;   // mouse-movement blur (only near title)

    // --- Scroll: measure velocity → blur ---
    window.addEventListener('scroll', () => {
        const delta = Math.abs(window.scrollY - lastScrollY);
        lastScrollY = window.scrollY;
        blurTarget  = Math.min(delta * 0.75, 20);
    }, { passive: true });

    // --- Mouse on banner: only react when cursor is near the title ---
    if (banner) {
        banner.addEventListener('mousemove', (e) => {
            const titleRect = title.getBoundingClientRect();

            // Define a zone around the title (80px padding on each side)
            const ZONE = 80;
            const inZone =
                e.clientX >= titleRect.left  - ZONE &&
                e.clientX <= titleRect.right + ZONE &&
                e.clientY >= titleRect.top   - ZONE &&
                e.clientY <= titleRect.bottom + ZONE;

            if (!inZone) {
                // Outside zone — gently reset letters
                title.querySelectorAll('.hero-letter').forEach(s => s.style.transform = '');
                return;
            }

            // Inside zone — light blur + letter repulsion
            const speed = Math.sqrt(e.movementX ** 2 + e.movementY ** 2);
            mouseBlur = Math.min(speed * 0.18, 4); // light — max 4px

            const bannerRect = banner.getBoundingClientRect();
            const relMx = e.clientX - bannerRect.left;
            const relMy = e.clientY - bannerRect.top;

            title.querySelectorAll('.hero-letter').forEach(span => {
                const sr   = span.getBoundingClientRect();
                const sx   = sr.left + sr.width  / 2 - bannerRect.left;
                const sy   = sr.top  + sr.height / 2 - bannerRect.top;
                const dx   = relMx - sx;
                const dy   = relMy - sy;
                const dist = Math.sqrt(dx * dx + dy * dy);
                const MAX  = 110; // repulsion radius (px)

                if (dist < MAX && dist > 0) {
                    const force = (1 - dist / MAX) * 14; // lighter force
                    const nx = -(dx / dist) * force;
                    const ny = -(dy / dist) * force;
                    span.style.transform = `translate(${nx.toFixed(1)}px, ${ny.toFixed(1)}px)`;
                } else {
                    span.style.transform = '';
                }
            });
        }, { passive: true });

        // Reset everything when mouse leaves banner
        banner.addEventListener('mouseleave', () => {
            title.querySelectorAll('.hero-letter').forEach(s => s.style.transform = '');
            mouseBlur = 0;
        });
    }

    // --- rAF animation loop ---
    function tick(ts) {
        if (!t0) t0 = ts;
        const secs = (ts - t0) / 1000;

        // Load fade-in: 0 → 1 over 2 seconds, cubic ease-out
        const loadProgress = Math.min(1, (ts - t0) / 2000);
        const loadFade = 1 - Math.pow(1 - loadProgress, 3);

        // Subtle breathing: 0.78 → 1.0, period ~5s
        const breathe = 0.89 + 0.11 * Math.sin(secs * (Math.PI / 2.5));

        // Scroll fade: full at top → 0 at 55% of banner height
        const heroH      = banner ? banner.offsetHeight : window.innerHeight;
        const scrollFade = Math.max(0, 1 - window.scrollY / (heroH * 0.55));

        // Blur: scroll blur (lerp + decay) + mouse blur (decay)
        blurCurrent += (blurTarget - blurCurrent) * 0.35;
        blurTarget  *= 0.78;
        mouseBlur   *= 0.80;

        const totalBlur = blurCurrent + mouseBlur;

        title.style.opacity = (loadFade * breathe * scrollFade).toFixed(3);
        title.style.filter  = `blur(${totalBlur.toFixed(2)}px)`;

        requestAnimationFrame(tick);
    }

    requestAnimationFrame(tick);
})();
