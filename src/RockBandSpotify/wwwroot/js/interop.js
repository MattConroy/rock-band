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
