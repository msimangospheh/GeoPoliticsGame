// Plays the NPC moves back on the board, one at a time, at 1x / 2x / 4x speed.
// The server already decided everything - this only SHOWS what happened.
(function () {
    const dataEl = document.getElementById('replay-data');
    if (!dataEl) return;

    const data = JSON.parse(dataEl.textContent);
    const players = Object.fromEntries(data.players.map(p => [p.id, p]));
    const board = document.querySelector('.board');
    const svg = board.querySelector('.board-lines');
    const logList = document.getElementById('npc-log-list');
    const strip = document.querySelector('[data-strip]');
    const $ = selector => document.querySelector(selector);
    const banner = {
        badge: $('[data-banner-badge]'), round: $('[data-banner-round]'), title: $('[data-banner-title]'),
        left: $('[data-banner-left]'), bar: $('[data-banner-bar]')
    };

    // ---- Speed (remembered in this browser) ----
    let speed = 1;
    try { speed = parseInt(localStorage.getItem('npcSpeed'), 10) || 1; } catch { /* storage blocked: use 1x */ }
    const speedButtons = document.querySelectorAll('[data-speed]');
    function setSpeed(value) {
        speed = value;
        speedButtons.forEach(b => b.classList.toggle('active', Number(b.dataset.speed) === value));
        try { localStorage.setItem('npcSpeed', String(value)); } catch { /* ignore */ }
    }
    speedButtons.forEach(b => b.addEventListener('click', () => setSpeed(Number(b.dataset.speed))));
    setSpeed(speed);

    const wait = ms => new Promise(resolve => setTimeout(resolve, ms / speed));

    // ---- Board helpers ----
    const card = code => board.querySelector(`[data-code="${code}"]`);
    const soldiersOf = code => parseInt(card(code)?.querySelector('[data-soldiers]')?.textContent, 10) || 0;

    function setSoldiers(code, value) {
        const c = card(code);
        if (!c) return;
        c.querySelector('[data-soldiers]').textContent = Math.max(0, value);
        c.classList.remove('bump');
        void c.offsetWidth;            // restart the CSS animation
        c.classList.add('bump');
    }

    function setOwner(code, playerId) {
        const c = card(code), p = players[playerId];
        if (!c || !p) return;
        c.classList.toggle('you', p.human);
        c.classList.toggle('npc', !p.human);
        const badge = c.querySelector('[data-badge]');
        badge.textContent = p.badge;
        badge.classList.toggle('you', p.human);
    }

    function floatText(code, text, bad) {
        const c = card(code);
        if (!c) return;
        const s = document.createElement('span');
        s.className = 'float-badge' + (bad ? ' bad' : '');
        s.textContent = text;
        c.appendChild(s);
        setTimeout(() => s.remove(), 1400);
    }

    function clearAttack() {
        svg.querySelectorAll('[data-replay]').forEach(l => l.remove());
        board.querySelectorAll('.target, .attacking').forEach(c => c.classList.remove('target', 'attacking'));
    }

    function showAttack(from, to) {
        clearAttack();
        const a = card(from), b = card(to);
        if (!a || !b) return;
        const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        line.setAttribute('x1', a.dataset.x); line.setAttribute('y1', a.dataset.y);
        line.setAttribute('x2', b.dataset.x); line.setAttribute('y2', b.dataset.y);
        line.setAttribute('class', 'attack-line');
        line.dataset.replay = '1';
        svg.appendChild(line);
        a.classList.add('attacking');
        b.classList.add('target');
    }

    // ---- Side panels (textContent only, so names can never inject HTML) ----
    function addLogCard(e) {
        if (!logList) return;
        const p = players[e.actor];
        const div = document.createElement('div');
        div.className = 'log-card fresh';
        const who = document.createElement('div'); who.className = 'who';
        who.textContent = `R${e.round} · ${p ? p.name : 'NPC'}`;
        const msg = document.createElement('div'); msg.textContent = e.message;
        div.append(who, msg);
        if (e.reason) {
            const why = document.createElement('div');
            why.className = 'small fst-italic muted';
            why.textContent = 'Why: ' + e.reason;
            div.appendChild(why);
        }
        logList.prepend(div);
        while (logList.children.length > 6) logList.lastElementChild.remove();
    }

    function showStrip(title, aDice, dDice) {
        strip.classList.remove('d-none');
        strip.replaceChildren();
        const head = document.createElement('div');
        head.className = 'eyebrow mb-0';
        head.textContent = title;
        strip.appendChild(head);
        if (aDice && dDice) {
            const row = document.createElement('div');
            row.className = 'strip-dice';
            aDice.forEach((a, i) => {
                const d = dDice[i];
                const pair = document.createElement('span');
                pair.className = 'strip-pair';
                [[a, a > d, a < d], [d, d > a, d < a]].forEach(([value, win, lose]) => {
                    const die = document.createElement('span');
                    die.className = 'die sm' + (win ? ' win' : lose ? ' lose' : '');
                    die.textContent = value;
                    pair.appendChild(die);
                });
                row.appendChild(pair);
            });
            strip.appendChild(row);
        }
    }

    // How many NPC turns are still to come before the human's turn
    const turnsLeftAfter = i =>
        data.events.slice(i + 1).filter(e => e.type === 'TurnStarted' && e.actor !== data.human).length;

    // ---- Play everything back ----
    async function play() {
        const total = data.events.length;
        for (let i = 0; i < total; i++) {
            const e = data.events[i];
            const p = players[e.actor];
            banner.bar.style.width = `${Math.round(100 * (i + 1) / total)}%`;
            const left = turnsLeftAfter(i);
            banner.left.textContent = left === 0 ? 'Your turn next' : `Your turn in ${left} turn${left === 1 ? '' : 's'}`;

            switch (e.type) {
                case 'TurnStarted':
                    if (p) {
                        banner.badge.textContent = p.badge;
                        banner.round.textContent = `${p.human ? 'Your turn' : 'NPC turn'} · Round ${e.round}`;
                        banner.title.textContent = p.human ? 'Your turn is coming up!' : `${p.name} is moving`;
                    }
                    clearAttack();
                    strip.classList.add('d-none');
                    await wait(700);
                    break;

                case 'Reinforced':
                    Object.entries(e.placements || {}).forEach(([code, n]) => {
                        setSoldiers(code, soldiersOf(code) + n);
                        floatText(code, `+${n}`);
                    });
                    addLogCard(e);
                    await wait(1100);
                    break;

                case 'AttackStarted':
                    showAttack(e.from, e.to);
                    showStrip(`Live · ${p ? p.name : ''} · ${e.message}`);
                    addLogCard(e);
                    await wait(1100);
                    break;

                case 'DiceRolled':
                    setSoldiers(e.from, soldiersOf(e.from) - (e.aLoss || 0));
                    setSoldiers(e.to, soldiersOf(e.to) - (e.dLoss || 0));
                    if (e.dLoss) floatText(e.to, `-${e.dLoss}`, true);
                    if (e.aLoss) floatText(e.from, `-${e.aLoss}`, true);
                    showStrip(`Live · ${e.message}`, e.aDice, e.dDice);
                    await wait(900);
                    break;

                case 'Conquered':
                    setOwner(e.to, e.actor);
                    setSoldiers(e.to, e.amount || 0);
                    floatText(e.to, 'Conquered!');
                    showStrip(`Live · ${e.message}`);
                    clearAttack();
                    await wait(1300);
                    break;

                case 'AttackFailed':
                case 'Retreated':
                    showStrip(`Live · ${e.message}`);
                    addLogCard(e);
                    clearAttack();
                    await wait(1000);
                    break;

                case 'Transferred':
                    setSoldiers(e.from, soldiersOf(e.from) - (e.amount || 0));
                    setSoldiers(e.to, soldiersOf(e.to) + (e.amount || 0));
                    floatText(e.to, `+${e.amount}`);
                    addLogCard(e);
                    await wait(1000);
                    break;

                case 'TurnSkipped':
                    addLogCard(e);
                    await wait(700);
                    break;

                case 'PlayerEliminated':
                case 'GameLost':
                case 'GameWon':
                    showStrip(e.message);
                    await wait(1800);
                    break;

                default:
                    await wait(300);
            }
        }

        banner.title.textContent = 'Your turn!';
        setTimeout(() => location.replace(location.pathname), 900 / speed);   // reload the real board
    }

    play();
})();