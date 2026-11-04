// Victory celebration: a big burst, two side cannons, then a slow confetti rain.
// Uses the canvas-confetti library (loaded from cdnjs on the result page).
(function () {
    if (typeof confetti !== 'function') return;

    // Game colours: gold, khaki, olive, light olive, terracotta, parchment
    const colors = ['#c9a24a', '#e8cf94', '#8a9a4b', '#d4e09a', '#b5543a', '#f4ece1'];
    const base = { colors, zIndex: 1000, disableForReducedMotion: true };
    let rainTimer = null;

    function celebrate() {
        // 1. One big burst from behind the title
        confetti({ ...base, particleCount: 180, spread: 110, startVelocity: 48, scalar: 1.15, origin: { x: 0.5, y: 0.3 } });

        // 2. Cannons from both sides for 4 seconds
        const cannonsEnd = Date.now() + 4000;
        (function cannons() {
            confetti({ ...base, particleCount: 6, angle: 60, spread: 65, startVelocity: 58, origin: { x: 0, y: 0.8 } });
            confetti({ ...base, particleCount: 6, angle: 120, spread: 65, startVelocity: 58, origin: { x: 1, y: 0.8 } });
            if (Date.now() < cannonsEnd) requestAnimationFrame(cannons);
        })();

        // 3. Slow rain falling from the top for 12 seconds
        clearInterval(rainTimer);
        const rainEnd = Date.now() + 12000;
        rainTimer = setInterval(() => {
            if (Date.now() > rainEnd) { clearInterval(rainTimer); return; }
            confetti({
                ...base, particleCount: 3, startVelocity: 0, gravity: 0.45, ticks: 450,
                spread: 360, scalar: 0.95, origin: { x: Math.random(), y: -0.05 }
            });
        }, 120);
    }

    // Start as soon as the page loads, and again whenever the button is clicked
    celebrate();
    document.querySelector('[data-celebrate]')?.addEventListener('click', celebrate);
})();
