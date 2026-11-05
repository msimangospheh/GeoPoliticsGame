// =====================================================================
// GameSound: every sound in the game, made in code with the Web Audio API
// (no sound files to download or license).
//
//   GameSound.play('attack')        play a sound now
//   GameSound.play('hit', 600)      ...or after 600 ms
//   GameSound.setEnabled(false)     sound effects on/off
//   GameSound.setVolume(0.5)        0 to 1
//   GameSound.setMusic(true)        background war drums on/off
//
// Choices are remembered in this browser (localStorage "gp-sound").
// =====================================================================
(function () {
    const KEY = 'gp-sound';
    const DEFAULTS = { enabled: true, volume: 0.6, music: false, musicVolume: 0.35 };

    let prefs = load();
    let ctx = null, sfxBus = null, musicBus = null;
    let musicTimer = null, nextBar = 0, drone = null;

    function load() {
        try { return { ...DEFAULTS, ...JSON.parse(localStorage.getItem(KEY) || '{}') }; }
        catch { return { ...DEFAULTS }; }
    }

    function save() {
        try { localStorage.setItem(KEY, JSON.stringify(prefs)); } catch { /* storage blocked */ }
        document.dispatchEvent(new CustomEvent('gamesound:change', { detail: { ...prefs } }));
    }

    // Create the audio engine on first use. Browsers keep it "suspended" until the
    // player has clicked something on the site, so we try to wake it up each time.
    function audio() {
        if (!ctx) {
            const AC = window.AudioContext || window.webkitAudioContext;
            if (!AC) return null;
            ctx = new AC();
            sfxBus = ctx.createGain();
            sfxBus.gain.value = prefs.volume;
            sfxBus.connect(ctx.destination);
            musicBus = ctx.createGain();
            musicBus.gain.value = prefs.musicVolume * prefs.volume;
            musicBus.connect(ctx.destination);
        }
        if (ctx.state === 'suspended') ctx.resume().catch(() => { });
        return ctx;
    }

    // ---------- Building blocks ----------
    // A note: frequency (Hz), start time, length, wave shape, loudness, optional pitch slide
    function tone(freq, at, dur, { type = 'sine', gain = 0.3, slideTo = null, attack = 0.01, filter = null, bus = sfxBus } = {}) {
        const osc = ctx.createOscillator();
        const amp = ctx.createGain();
        osc.type = type;
        osc.frequency.setValueAtTime(freq, at);
        if (slideTo) osc.frequency.exponentialRampToValueAtTime(slideTo, at + dur);
        amp.gain.setValueAtTime(0.0001, at);
        amp.gain.exponentialRampToValueAtTime(gain, at + attack);
        amp.gain.exponentialRampToValueAtTime(0.0001, at + dur);
        let node = osc;
        if (filter) {
            const lp = ctx.createBiquadFilter();
            lp.type = 'lowpass';
            lp.frequency.value = filter;
            osc.connect(lp);
            node = lp;
        }
        node.connect(amp).connect(bus);
        osc.start(at);
        osc.stop(at + dur + 0.05);
    }

    // A burst of noise (rattles, snares, impacts)
    function noise(at, dur, { gain = 0.3, freq = 2000, type = 'bandpass', q = 1, bus = sfxBus } = {}) {
        const buffer = ctx.createBuffer(1, Math.max(1, Math.floor(ctx.sampleRate * dur)), ctx.sampleRate);
        const data = buffer.getChannelData(0);
        for (let i = 0; i < data.length; i++) data[i] = (Math.random() * 2 - 1) * (1 - i / data.length);
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        const filter = ctx.createBiquadFilter();
        filter.type = type;
        filter.frequency.value = freq;
        filter.Q.value = q;
        const amp = ctx.createGain();
        amp.gain.value = gain;
        src.connect(filter).connect(amp).connect(bus);
        src.start(at);
    }

    // A brass-like note for horns and fanfares
    function brass(freq, at, dur, gain = 0.18) {
        tone(freq, at, dur, { type: 'sawtooth', gain, attack: 0.04, filter: 1400 });
        tone(freq * 1.005, at, dur, { type: 'sawtooth', gain: gain * 0.6, attack: 0.05, filter: 1100 });
    }

    // A deep drum (timpani / war drum)
    function drum(at, gain = 0.6, bus = sfxBus) {
        tone(90, at, 0.45, { gain, slideTo: 45, attack: 0.005, bus });
        noise(at, 0.08, { gain: gain * 0.25, freq: 300, type: 'lowpass', bus });
    }

    // ---------- The sounds ----------
    const SOUNDS = {
        click(t) { tone(880, t, 0.05, { type: 'triangle', gain: 0.12 }); },
        pop(t) { tone(520, t, 0.09, { gain: 0.2, slideTo: 820 }); },
        npc(t) { tone(700, t, 0.06, { type: 'square', gain: 0.05, filter: 1500 }); tone(1050, t + 0.07, 0.06, { type: 'square', gain: 0.04, filter: 1500 }); },
        dice(t) {
            for (let i = 0; i < 7; i++) noise(t + i * 0.07 + Math.random() * 0.03, 0.03, { gain: 0.35, freq: 2200 + Math.random() * 900, q: 3 });
        },
        land(t) { noise(t, 0.05, { gain: 0.4, freq: 1400, q: 2 }); tone(220, t, 0.08, { type: 'triangle', gain: 0.12 }); },
        hit(t) { drum(t, 0.45); noise(t, 0.12, { gain: 0.15, freq: 900, q: 0.7 }); },
        attack(t) {   // war horn
            brass(196, t, 0.9, 0.2);
            brass(294, t + 0.05, 0.85, 0.12);
            drum(t, 0.5);
        },
        march(t) { for (let i = 0; i < 3; i++) noise(t + i * 0.13, 0.07, { gain: 0.25, freq: 1800, type: 'highpass' }); },
        retreat(t) { brass(392, t, 0.25, 0.14); brass(330, t + 0.22, 0.25, 0.14); brass(262, t + 0.44, 0.6, 0.14); },
        conquer(t) {
            [392, 523, 659].forEach((f, i) => brass(f, t + i * 0.12, 0.16, 0.16));
            brass(784, t + 0.36, 0.6, 0.18);
            drum(t + 0.36, 0.5);
        },
        victory(t) {
            const notes = [[523, 0.15], [523, 0.15], [523, 0.15], [659, 0.45], [587, 0.15], [659, 0.15], [784, 0.9]];
            let at = t;
            notes.forEach(([f, d]) => { brass(f, at, d + 0.05, 0.17); at += d; });
            [0, 0.45, 0.9, 1.4].forEach(o => drum(t + o, 0.55));
        },
        defeat(t) {
            [[330, 0.6], [311, 0.6], [294, 0.6], [262, 1.4]].reduce((at, [f, d]) => { brass(f, at, d, 0.13); return at + d; }, t);
            [0, 1.2, 2.4].forEach(o => drum(t + o, 0.6));
        }
    };

    function play(name, delayMs = 0) {
        if (!prefs.enabled || !SOUNDS[name]) return;
        if (delayMs > 0) { setTimeout(() => play(name, 0), delayMs); return; }
        if (!audio() || ctx.state !== 'running') return;   // not allowed to make sound yet: stay silent
        try { SOUNDS[name](ctx.currentTime + 0.01); } catch { /* sound is optional */ }
    }

    // ---------- Background music: slow war drums over a low drone ----------
    const BEAT = 60 / 72;   // 72 beats per minute

    function scheduleMusic() {
        // Keep about two bars of drums scheduled ahead of time
        while (nextBar < ctx.currentTime + 2 * 4 * BEAT) {
            drum(nextBar, 0.5, musicBus);
            drum(nextBar + BEAT * 1.5, 0.3, musicBus);
            drum(nextBar + BEAT * 2, 0.45, musicBus);
            noise(nextBar + BEAT * 3, 0.1, { gain: 0.08, freq: 1500, type: 'highpass', bus: musicBus });
            noise(nextBar + BEAT * 3.5, 0.1, { gain: 0.06, freq: 1500, type: 'highpass', bus: musicBus });
            nextBar += 4 * BEAT;
        }
    }

    function startMusic() {
        if (musicTimer || !prefs.music || !audio() || ctx.state !== 'running') return;
        nextBar = ctx.currentTime + 0.1;
        scheduleMusic();
        musicTimer = setInterval(scheduleMusic, 500);

        // Two quiet, slightly out-of-tune low notes make the "tension" drone
        drone = [55, 82.4].map(f => {
            const osc = ctx.createOscillator();
            const lp = ctx.createBiquadFilter();
            const amp = ctx.createGain();
            osc.type = 'sawtooth';
            osc.frequency.value = f;
            lp.type = 'lowpass';
            lp.frequency.value = 260;
            amp.gain.value = 0.05;
            osc.connect(lp).connect(amp).connect(musicBus);
            osc.start();
            return osc;
        });
    }

    function stopMusic() {
        clearInterval(musicTimer);
        musicTimer = null;
        (drone || []).forEach(osc => { try { osc.stop(); } catch { /* already stopped */ } });
        drone = null;
    }

    // ---------- Settings ----------
    function applyVolume() {
        if (!ctx) return;
        sfxBus.gain.value = prefs.volume;
        musicBus.gain.value = prefs.musicVolume * prefs.volume;
    }

    const GameSound = {
        play,
        prefs: () => ({ ...prefs }),
        setEnabled(on) { prefs.enabled = !!on; save(); if (on) play('click'); },
        setVolume(v) { prefs.volume = Math.min(1, Math.max(0, Number(v) || 0)); applyVolume(); save(); },
        setMusicVolume(v) { prefs.musicVolume = Math.min(1, Math.max(0, Number(v) || 0)); applyVolume(); save(); },
        setMusic(on) { prefs.music = !!on; save(); on ? startMusic() : stopMusic(); },
        setAll(values) { prefs = { ...prefs, ...values }; applyVolume(); save(); prefs.music ? startMusic() : stopMusic(); }
    };
    window.GameSound = GameSound;

    // ---------- Automatic sounds (no changes needed in the pages) ----------

    // Buttons: attack buttons blow the horn, everything else clicks.
    // Add data-sound="name" to a button to choose its sound, or data-sound="none" for silence.
    document.addEventListener('click', e => {
        audio();                       // the first click wakes up the audio engine
        if (prefs.music) startMusic();
        const el = e.target.closest('[data-sound], .btn, button');
        if (!el || el.disabled) return;
        let name = el.dataset.sound;
        if (!name) {
            if (el.classList.contains('btn-attack')) name = 'attack';
            else if (el.matches('[data-plus], [data-auto-spread]')) name = 'pop';
            else if (el.matches('[data-deploy]')) name = 'march';
            else name = 'click';
        }
        if (name !== 'none') play(name);
    }, true);

    // Header buttons: <button data-sound-btn="sfx"> and <button data-sound-btn="music">
    function refreshButtons() {
        document.querySelectorAll('[data-sound-btn]').forEach(btn => {
            const on = btn.dataset.soundBtn === 'music' ? prefs.music : prefs.enabled;
            btn.classList.toggle('off', !on);
            btn.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
    }

    document.querySelectorAll('[data-sound-btn]').forEach(btn => {
        btn.dataset.sound = 'none';
        btn.addEventListener('click', () => {
            if (btn.dataset.soundBtn === 'music') GameSound.setMusic(!prefs.music);
            else GameSound.setEnabled(!prefs.enabled);
        });
    });
    document.addEventListener('gamesound:change', refreshButtons);
    refreshButtons();

    // Page sounds, timed to the animations
    window.addEventListener('load', async () => {
        if (!audio()) return;
        // Waking up takes a moment; give up quietly if the browser says "not yet"
        try { await Promise.race([ctx.resume(), new Promise(r => setTimeout(r, 400))]); } catch { /* blocked */ }
        if (ctx.state !== 'running') return;
        if (prefs.music) startMusic();

        // Battle page: dice rattle, each die clacks as it lands, fallen soldiers thud
        const cubes = document.querySelectorAll('.dice-cube-wrap');
        if (cubes.length) {
            play('dice');
            cubes.forEach(c => play('land', (parseInt(getComputedStyle(c).getPropertyValue('--delay'), 10) || 0) + 1400));
            document.querySelectorAll('.soldier.lost').forEach(s =>
                play('hit', parseInt(getComputedStyle(s).getPropertyValue('--burst'), 10) || 1500));
        }

        // Result page: fanfare for a win, slow drums for a loss
        if (document.querySelector('.result-page.won')) play('victory', 300);
        if (document.querySelector('.result-page.lost')) play('defeat', 300);
    });
})();
