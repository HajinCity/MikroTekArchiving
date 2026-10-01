// Small helpers for the portal pages (template stage).
document.addEventListener("DOMContentLoaded", () => {
    // Bootstrap tooltips wherever data-bs-toggle="tooltip" is used.
    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(el => new bootstrap.Tooltip(el));

    // Chart hover tooltip: any element with data-tip shows it on hover or keyboard focus.
    const tip = document.createElement("div");
    tip.className = "viz-tip";
    tip.setAttribute("role", "tooltip");
    document.body.appendChild(tip);
    const show = (el, x, y) => {
        tip.innerHTML = el.dataset.tip;
        tip.classList.add("show");
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
        const r = el.getBoundingClientRect();
        show(el, r.left + r.width / 2, r.top);
    });
    document.addEventListener("focusout", () => tip.classList.remove("show"));
});
