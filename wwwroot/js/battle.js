// Battle page: soldier counts tick down after the dice land, plus optional dice sounds
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

    // ---- Dice sounds (off by default, remembered in this browser) ----
    const toggle = document.querySelector('[data-sound-toggle]');
    let soundOn = false;
    try { soundOn = localStorage.getItem('diceSound') === 'on'; } catch { /* storage blocked */ }

    function showLabel() {
        if (toggle) toggle.textContent = soundOn ? 'Dice sounds: On' : 'Dice sounds: Off';
    }

    // A short "clatter" made from bursts of filtered noise - no sound files needed
    function rattle() {
        try {
            const AudioCtx = window.AudioContext || window.webkitAudioContext;
            if (!AudioCtx) return;
            const ctx = new AudioCtx();
            for (let i = 0; i < 6; i++) {
                const when = ctx.currentTime + i * 0.07 + Math.random() * 0.03;
                const buffer = ctx.createBuffer(1, Math.floor(ctx.sampleRate * 0.03), ctx.sampleRate);
                const data = buffer.getChannelData(0);
                for (let j = 0; j < data.length; j++) data[j] = (Math.random() * 2 - 1) * (1 - j / data.length);

                const source = ctx.createBufferSource();
                source.buffer = buffer;
                const filter = ctx.createBiquadFilter();
                filter.type = 'bandpass';
                filter.frequency.value = 2200 + Math.random() * 800;
                const gain = ctx.createGain();
                gain.gain.value = 0.25;
                source.connect(filter).connect(gain).connect(ctx.destination);
                source.start(when);
            }
        } catch { /* sound is optional */ }
    }

    toggle?.addEventListener('click', () => {
        soundOn = !soundOn;
        try { localStorage.setItem('diceSound', soundOn ? 'on' : 'off'); } catch { /* ignore */ }
        showLabel();
        if (soundOn) rattle();
    });

    showLabel();
    if (soundOn && document.querySelector('.die3d')) rattle();
})();