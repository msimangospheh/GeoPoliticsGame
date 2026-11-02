// Homepage: the world map drifts slowly behind the page as you scroll (parallax)
(function () {
    const layer = document.querySelector('.map-layer');
    if (!layer) return;
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

    let ticking = false;

    function move() {
        const y = window.scrollY;
        // The map moves at a third of the scroll speed (and a little sideways),
        // but never further than the extra height we gave it in CSS
        const maxShift = layer.offsetHeight - window.innerHeight;
        const shiftY = Math.min(y * 0.3, maxShift);
        const shiftX = Math.min(y * 0.04, 60);
        layer.style.transform = `translate3d(${-shiftX}px, ${-shiftY}px, 0)`;
        ticking = false;
    }

    window.addEventListener('scroll', () => {
        if (!ticking) {
            ticking = true;
            requestAnimationFrame(move);
        }
    }, { passive: true });

    move();
})();
