// Board page: reinforce steppers, pop-up tabs, share link
(function () {
    const modal = document.getElementById('muster');

    function showTab(tab) {
        if (!modal) return;
        modal.querySelectorAll('[data-tab-panel]').forEach(p => p.classList.toggle('d-none', p.dataset.tabPanel !== tab));
        modal.querySelectorAll('[data-tab-btn]').forEach(b => b.classList.toggle('active', b.dataset.tabBtn === tab));
    }

    document.querySelectorAll('[data-open-modal]').forEach(btn =>
        btn.addEventListener('click', () => { modal?.classList.add('open'); showTab(btn.dataset.openModal); }));
    modal?.querySelectorAll('[data-tab-btn]').forEach(btn =>
        btn.addEventListener('click', () => showTab(btn.dataset.tabBtn)));
    modal?.querySelectorAll('[data-close]').forEach(btn =>
        btn.addEventListener('click', () => modal.classList.remove('open')));

    // ---- Reinforce: + / - buttons ----
    const form = document.querySelector('[data-reinforce]');
    if (form && form.querySelector('[data-row]')) {
        const total = parseInt(form.dataset.total, 10) || 0;
        const rows = [...form.querySelectorAll('[data-row]')];
        const leftEl = form.querySelector('[data-left]');
        const placedEl = form.querySelector('[data-placed]');
        const deployBtn = form.querySelector('[data-deploy]');

        const valueOf = row => parseInt(row.querySelector('[data-input]').value, 10) || 0;

        function setValue(row, value) {
            const input = row.querySelector('[data-input]');
            input.value = value;
            row.querySelector('[data-value]').textContent = value;
            row.querySelector('[data-after]').textContent = parseInt(input.dataset.base, 10) + value;
        }

        function refresh() {
            const placed = rows.reduce((sum, row) => sum + valueOf(row), 0);
            leftEl.textContent = total - placed;
            placedEl.textContent = placed;
            deployBtn.disabled = placed !== total;          // must place every soldier
            rows.forEach(row => {
                row.querySelector('[data-plus]').disabled = placed >= total;
                row.querySelector('[data-minus]').disabled = valueOf(row) === 0;
            });
        }

        rows.forEach(row => {
            row.querySelector('[data-plus]').addEventListener('click', () => { setValue(row, valueOf(row) + 1); refresh(); });
            row.querySelector('[data-minus]').addEventListener('click', () => { setValue(row, Math.max(0, valueOf(row) - 1)); refresh(); });
        });

        form.querySelector('[data-reset]')?.addEventListener('click', () => { rows.forEach(r => setValue(r, 0)); refresh(); });

        form.querySelector('[data-auto-spread]')?.addEventListener('click', () => {
            rows.forEach((row, i) =>
                setValue(row, Math.floor(total / rows.length) + (i < total % rows.length ? 1 : 0)));
            refresh();
        });

        refresh();
    }

    // ---- Share link ----
    document.querySelectorAll('[data-copy-link]').forEach(btn => btn.addEventListener('click', async () => {
        const url = location.origin + location.pathname;
        try {
            await navigator.clipboard.writeText(url);
            btn.textContent = 'Link copied!';
        } catch {
            window.prompt('Copy this link:', url);
        }
        setTimeout(() => (btn.textContent = 'Share link'), 2000);
    }));
})();