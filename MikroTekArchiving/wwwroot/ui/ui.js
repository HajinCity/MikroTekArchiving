/* UI KIT — layout behaviour: sidebar, theme, fullscreen, cards, tooltips, confirm dialog. */
(function () {
    const html = document.documentElement;
    const store = {
        get(k) { try { return localStorage.getItem(k); } catch { return null; } },
        set(k, v) { try { localStorage.setItem(k, v); } catch { /* storage blocked */ } }
    };
    const isDesktop = () => window.matchMedia("(min-width: 992px)").matches;

    // ---- Old browser notice: the kit needs Edge/Chrome 111+ or Firefox 121+ (2023 or newer) ----
    const modern = window.CSS && CSS.supports &&
        CSS.supports("color", "color-mix(in srgb, red, blue)") &&
        CSS.supports("container-type", "inline-size") &&
        CSS.supports("selector(:has(a))");
    if (!modern) document.addEventListener("DOMContentLoaded", () => {
        const bar = document.createElement("div");
        bar.setAttribute("role", "alert");
        bar.style.cssText = "position:sticky;top:0;z-index:2000;padding:.6rem 1rem;background:#fff3cd;color:#664d03;border-bottom:1px solid #ffda6a;font:14px/1.4 system-ui,sans-serif;text-align:center";
        bar.textContent = "This browser is out of date, so some parts of the portal may look wrong. Please update Microsoft Edge or Google Chrome.";
        document.body.prepend(bar);
    });

    // ---- Sidebar: mini on desktop, off-canvas on phones ----
    if (store.get("ui.sidebarMini") === "1") html.classList.add("sidebar-mini");
    document.addEventListener("click", e => {
        if (e.target.closest("[data-ui-sidebar-toggle]")) {
            if (isDesktop()) {
                html.classList.toggle("sidebar-mini");
                store.set("ui.sidebarMini", html.classList.contains("sidebar-mini") ? "1" : "0");
                window.dispatchEvent(new Event("resize")); // let charts resize
            } else html.classList.toggle("sidebar-open");
        }
        if (e.target.closest("[data-ui-sidebar-close]")) html.classList.remove("sidebar-open");
    });

    // ---- Light / dark theme ----
    const applyIcon = () => document.querySelectorAll("[data-ui-theme-toggle] i")
        .forEach(i => i.className = "bi " + (html.dataset.bsTheme === "dark" ? "bi-sun" : "bi-moon-stars"));
    document.addEventListener("click", e => {
        if (!e.target.closest("[data-ui-theme-toggle]")) return;
        html.dataset.bsTheme = html.dataset.bsTheme === "dark" ? "light" : "dark";
        store.set("ui.theme", html.dataset.bsTheme);
        applyIcon();
        document.dispatchEvent(new CustomEvent("ui:themechange"));
    });

    // ---- Fullscreen ----
    document.addEventListener("click", e => {
        if (!e.target.closest("[data-ui-fullscreen]")) return;
        if (document.fullscreenElement) document.exitFullscreen(); else document.documentElement.requestFullscreen?.();
    });

    // ---- Card close button ----
    document.addEventListener("click", e => {
        const b = e.target.closest("[data-ui-close-card]");
        if (!b) return;
        const card = b.closest(".card");
        card.style.transition = "opacity .2s"; card.style.opacity = "0";
        setTimeout(() => card.remove(), 200);
    });

    // ---- Chart "table view" toggle ----
    document.addEventListener("click", e => {
        const b = e.target.closest("[data-chart-table-toggle]");
        if (!b) return;
        const box = b.closest(".ui-chart");
        const t = box.querySelector(".ui-chart-table"), c = box.querySelector(".ui-chart-canvas");
        const showTable = t.hidden;
        t.hidden = !showTable; c.hidden = showTable;
        b.classList.toggle("active", showTable);
        b.innerHTML = showTable ? '<i class="bi bi-bar-chart"></i>' : '<i class="bi bi-table"></i>';
    });

    // ---- Confirm dialog: add data-ui-confirm="Message" (and optional data-confirm-label="Delete") to any button or link ----
    let pending = null;
    document.addEventListener("click", e => {
        const el = e.target.closest("[data-ui-confirm]");
        if (!el || el.dataset.confirmed) return;
        e.preventDefault();
        pending = el;
        const modal = document.getElementById("uiConfirm");
        modal.querySelector("[data-confirm-text]").textContent = el.dataset.uiConfirm;
        modal.querySelector("[data-confirm-ok]").textContent = el.dataset.confirmLabel || "Yes, continue";
        bootstrap.Modal.getOrCreateInstance(modal).show();
    });
    document.addEventListener("click", e => {
        if (!e.target.closest("#uiConfirm [data-confirm-ok]") || !pending) return;
        bootstrap.Modal.getInstance(document.getElementById("uiConfirm")).hide();
        const el = pending;
        pending = null;
        el.dataset.confirmed = "1";
        el.click();                      // runs the original action (submit / navigate)
        delete el.dataset.confirmed;
    });

    // ---- Hover tooltip for charts/meters (elements with data-tip) ----
    document.addEventListener("DOMContentLoaded", () => {
        applyIcon();
        document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(el => new bootstrap.Tooltip(el));
        const tip = document.createElement("div");
        tip.className = "viz-tip"; tip.setAttribute("role", "tooltip");
        document.body.appendChild(tip);
        const show = (el, x, y) => {
            tip.innerHTML = el.dataset.tip; tip.classList.add("show");
            const r = tip.getBoundingClientRect();
            tip.style.left = Math.min(x + 12, window.innerWidth - r.width - 8) + "px";
            tip.style.top = Math.max(y - r.height - 10, 8) + "px";
        };
        document.addEventListener("mousemove", e => {
            const el = e.target.closest("[data-tip]");
            if (el) show(el, e.clientX, e.clientY); else tip.classList.remove("show");
        });
        document.addEventListener("focusin", e => {
            const el = e.target.closest("[data-tip]");
            if (!el) return;
            const r = el.getBoundingClientRect(); show(el, r.left + r.width / 2, r.top);
        });
        document.addEventListener("focusout", () => tip.classList.remove("show"));
    });
})();
