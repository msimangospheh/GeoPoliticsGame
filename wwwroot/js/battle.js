// Battle page: soldier counts tick down after the dice land.
// (Dice and soldier sounds are played by sound.js, timed to the animations.)
(function () {
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // ---- Count from the old number down to the new one ----
    document.querySelectorAll('[data-count-from]').forEach(el => {
        const to = parseInt(el.textContent, 10);
        const from = parseInt(el.dataset.countFrom, 10);
        const delay = parseInt(el.dataset.countDelay, 10) || 0;
        if (reduceMotion || isNaN(from) || from === to) return;

        el.textContent = from;
        const duration = 600;
        const start = performance.now() + delay;
        function step(now) {
            const t = Math.min(1, Math.max(0, (now - start) / duration));
            el.textContent = Math.round(from + (to - from) * t);
            if (t < 1) requestAnimationFrame(step);
        }
        requestAnimationFrame(step);
    });

    // ---- "Sounds: On/Off" button on the battle page (uses the game-wide setting) ----
    const toggle = document.querySelector('[data-sound-toggle]');
    if (toggle && window.GameSound) {
        toggle.dataset.sound = 'none';
        const showLabel = () => (toggle.textContent = GameSound.prefs().enabled ? 'Sounds: On' : 'Sounds: Off');
        toggle.addEventListener('click', () => GameSound.setEnabled(!GameSound.prefs().enabled));
        document.addEventListener('gamesound:change', showLabel);
        showLabel();
    }
})();
