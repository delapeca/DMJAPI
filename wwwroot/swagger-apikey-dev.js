// swagger-apikey-dev.js (DEV)
// Força X-Api-Key a totes les requests de Swagger UI.
// IMPORTANT: No toca url/urls ni SwaggerEndpoint.
(function () {
    function patchFetch() {
        try {
            var ui = window.ui;
            if (!ui || !ui.getSystem || !ui.getSystem().fn) return false;

            var sys = ui.getSystem();
            var fn = sys.fn;
            var orig = fn.fetch;
            if (!orig || orig.__dmj_patched) return true;

            fn.fetch = function (req) {
                try {
                    var auth = ui.getState && ui.getState().get("auth");
                    var apiKey = auth && auth.getIn && auth.getIn(["authorized", "ApiKey", "value"]);
                    if (apiKey) {
                        req.headers = req.headers || {};
                        req.headers["X-Api-Key"] = apiKey;
                    }
                } catch (e) { /* ignore */ }
                return orig(req);
            };
            fn.fetch.__dmj_patched = true;
            console.log("[DMJAPI] Swagger fetch patched: X-Api-Key will be added.");
            return true;
        } catch (e) { return false; }
    }

    var tries = 0;
    var t = setInterval(function () {
        tries++;
        if (patchFetch() || tries >= 40) clearInterval(t); // ~10s
    }, 250);
})();
