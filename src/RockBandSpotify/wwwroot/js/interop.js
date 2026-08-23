// Minimal JS interop: localStorage access and full-page redirect for the
// Spotify Authorization Code + PKCE flow.

// Storage keys used to be abbreviated (rb_spotify_token and friends). Anyone
// who signed in before the rename still has their token and fetched library
// under the old names, so the values move across once, on load, rather than
// silently signing them out and dropping the library.
(function moveStorageToFullWords() {
    var moved = {
        rb_catalogue_columns: "rock_band_catalogue_columns",
        rb_owned_songs: "rock_band_owned_songs",
        rb_spotify_playlist: "rock_band_spotify_playlist",
        rb_pkce_verifier: "rock_band_pkce_verifier",
        rb_spotify_token: "rock_band_spotify_token",
        rb_pkce_return_path: "rock_band_pkce_return_path",
        rb_psn_npsso: "rock_band_playstation_npsso"
    };
    try {
        Object.keys(moved).forEach(function (was) {
            var now = moved[was];
            var value = window.localStorage.getItem(was);
            // Never overwrite a value already saved under the new name.
            if (value !== null && window.localStorage.getItem(now) === null) {
                window.localStorage.setItem(now, value);
            }
            if (value !== null) {
                window.localStorage.removeItem(was);
            }
        });
    } catch (e) {
        // Storage blocked entirely; the app copes with having none.
    }
})();

window.rockBandSpotify = {
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
    },

    // The catalogue's header and body are separate tables in separate
    // scrolling elements, so the header has to be told to follow when the
    // body scrolls sideways — otherwise the labels sit over the wrong
    // columns the moment a phone scrolls to reach Source.
    openInNewTab: function (url) {
        window.open(url, "_blank", "noopener");
    },

    syncTableScroll: function (headerId, bodyId) {
        var header = document.getElementById(headerId);
        var body = document.getElementById(bodyId);
        if (!header || !body || body.dataset.scrollSynced) return;
        body.dataset.scrollSynced = "1";
        body.addEventListener("scroll", function () {
            header.scrollLeft = body.scrollLeft;
        }, { passive: true });
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
