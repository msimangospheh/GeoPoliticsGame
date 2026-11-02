// Live spectating: watcher count, cheers, live feed, and refreshing the page when the game changes.
(function () {
    const root = document.querySelector('[data-live-game]');
    if (!root || typeof signalR === 'undefined') return;

    const gameId = root.dataset.liveGame;
    const isSpectator = root.dataset.spectator === 'true';
    const ownerName = root.dataset.owner || 'Commander';
    const feed = document.querySelector('[data-live-feed]');
    let lastSeq = 0;
    let watchers = 1;

    // ---- Watcher count ----
    function showWatchers() {
        document.querySelectorAll('[data-watchers]').forEach(el => (el.textContent = `${watchers} watching`));
    }

    // ---- Cheers ----
    function floatCheer(kind) {
        const bubble = document.createElement('span');
        bubble.className = 'cheer ' + (kind === 'retreat' ? 'cheer-retreat' : 'cheer-go');
        bubble.textContent = kind === 'retreat' ? 'Retreat!' : `Go ${ownerName}!`;
        bubble.style.left = (5 + Math.random() * 20) + '%';
        document.body.appendChild(bubble);
        setTimeout(() => bubble.remove(), 2600);
    }

    // ---- Live feed ----
    function timeAgo(iso) {
        const seconds = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
        if (seconds < 45) return 'JUST NOW';
        if (seconds < 3600) return `${Math.round(seconds / 60)} MIN AGO`;
        return `${Math.round(seconds / 3600)} H AGO`;
    }

    function feedItem(e) {
        const item = document.createElement('div');
        item.className = 'feed-item feed-' + e.kind;
        const head = document.createElement('div');
        head.className = 'who';
        head.textContent = `${timeAgo(e.at)} · ${e.label}`;
        const message = document.createElement('div');
        message.textContent = e.message;                 // textContent: names can never inject HTML
        item.append(head, message);
        if (e.commentary) {
            const quote = document.createElement('div');
            quote.className = 'small fst-italic muted';
            quote.textContent = `\u201C${e.commentary}\u201D`;
            item.appendChild(quote);
        }
        return item;
    }

    async function loadFeed() {
        if (!feed) return;
        try {
            const response = await fetch(`/Play/${gameId}/Events?after=${lastSeq}`, { credentials: 'same-origin' });
            if (!response.ok) return;
            const items = await response.json();
            items.forEach(e => {
                lastSeq = Math.max(lastSeq, e.seq);
                feed.prepend(feedItem(e));
            });
            if (items.length) feed.querySelector('[data-feed-empty]')?.remove();
            while (feed.children.length > 25) feed.lastElementChild.remove();
        } catch { /* the feed is optional */ }
    }

    // ---- Refresh the changing parts of the page without a full reload ----
    let busy = false;
    async function refresh() {
        if (busy) return;
        busy = true;
        try {
            const response = await fetch(location.pathname, { credentials: 'same-origin' });
            if (response.redirected || !response.ok) { location.reload(); return; }   // e.g. the game ended

            const doc = new DOMParser().parseFromString(await response.text(), 'text/html');
            const fresh = doc.querySelector('[data-live-game]');
            if (!fresh || fresh.dataset.view !== root.dataset.view) { location.reload(); return; }   // board <-> battle

            doc.querySelectorAll('[data-live-region]').forEach(newPart => {
                const oldPart = document.querySelector(`[data-live-region="${newPart.dataset.liveRegion}"]`);
                if (oldPart) oldPart.replaceWith(newPart);
            });
            showWatchers();
            await loadFeed();
        } catch {
            location.reload();
        } finally {
            busy = false;
        }
    }

    // ---- Connect ----
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/game')
        .withAutomaticReconnect()
        .build();

    connection.on('Watchers', count => { watchers = count; showWatchers(); });
    connection.on('Cheer', floatCheer);
    connection.on('GameUpdated', () => { if (isSpectator) refresh(); });
    connection.onreconnected(() => connection.invoke('JoinGame', gameId));

    document.querySelectorAll('[data-cheer]').forEach(button =>
        button.addEventListener('click', () => connection.invoke('Cheer', gameId, button.dataset.cheer).catch(() => { })));

    connection.start()
        .then(() => connection.invoke('JoinGame', gameId))
        .catch(() => { /* live updates unavailable - the page still works with manual refresh */ });

    if (isSpectator) loadFeed();
})();