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

// The catalogue grid's header sits in its own non-scrolling table above the
// body's scrolling table, so their columns line up exactly only if the body
// table's content width (which a visible scrollbar narrows) is matched by
// reserving the same width on the header. Scrollbar width varies by
// platform (0 on mobile/overlay scrollbars, ~15-17px on desktop) so it's
// measured directly rather than guessed.
(function () {
    function setScrollbarWidth() {
        var probe = document.createElement("div");
        probe.style.cssText = "position:absolute; visibility:hidden; top:-9999px; width:100px; height:100px; overflow:scroll;";
        document.body.appendChild(probe);
        var width = probe.offsetWidth - probe.clientWidth;
        document.body.removeChild(probe);
        document.documentElement.style.setProperty("--rb-scrollbar-width", width + "px");
    }
    setScrollbarWidth();
})();
