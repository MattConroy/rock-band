// Minimal JS interop: localStorage access and full-page redirect for the
// Spotify Authorization Code + PKCE flow.
window.rbSpotify = {
    getItem: function (key) {
        return window.localStorage.getItem(key);
    },
    setItem: function (key, value) {
        window.localStorage.setItem(key, value);
    },
    removeItem: function (key) {
        window.localStorage.removeItem(key);
    },
    redirect: function (url) {
        window.location.assign(url);
    },
    // Replace the current history entry to strip ?code=...&state=... after login,
    // so a refresh doesn't try to reuse a spent authorization code.
    replaceUrl: function (url) {
        window.history.replaceState({}, document.title, url);
    },
    getViewportWidth: function () {
        return window.innerWidth;
    }
};

// CSS vh/dvh both proved unreliable for sizing the full-height app shell on
// real mobile browsers (the address bar changes what's actually visible
// without always updating those units correctly). Measure the real
// viewport height directly and expose it as a CSS custom property instead,
// kept in sync on resize/orientation change/address-bar show-hide.
(function () {
    function setAppViewportHeight() {
        document.documentElement.style.setProperty("--app-vh", window.innerHeight + "px");
    }
    setAppViewportHeight();
    window.addEventListener("resize", setAppViewportHeight);
})();
