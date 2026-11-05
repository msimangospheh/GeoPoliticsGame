// Settings page: live sound preview, and push saved sound settings into this browser
(function () {
    const form = document.querySelector('[data-sound-settings]');
    if (!form || !window.GameSound) return;

    const input = name => form.querySelector(`[data-pref="${name}"]`);
    const read = () => ({
        enabled: input('enabled').checked,
        volume: Number(input('volume').value) / 100,
        music: input('music').checked,
        musicVolume: Number(input('musicVolume').value) / 100
    });

    // Show "60%" next to each slider
    function showValues() {
        form.querySelector('[data-show="SoundVolume"]').textContent = `${input('volume').value}%`;
        form.querySelector('[data-show="MusicVolume"]').textContent = `${input('musicVolume').value}%`;
    }

    // Just saved? Make this browser use the account's sound settings
    if (form.dataset.apply === 'true') GameSound.setAll(read());

    // Live preview while moving the controls (saved for good only when the form is submitted)
    input('volume').addEventListener('input', () => { showValues(); GameSound.setVolume(read().volume); });
    input('volume').addEventListener('change', () => GameSound.play('land'));
    input('musicVolume').addEventListener('input', () => { showValues(); GameSound.setMusicVolume(read().musicVolume); });
    input('enabled').addEventListener('change', () => GameSound.setEnabled(read().enabled));
    input('music').addEventListener('change', () => GameSound.setMusic(read().music));

    showValues();
})();
