/* UI KIT — Chart.js with one consistent, accessible style.
   Reads <canvas data-ui-chart='{"type":"bar","labels":[...],"series":[{"name":"..","values":[..]}],"money":true}'>
   Types: bar, hbar, stackedbar, line, area, donut. Colours follow the validated palette in fixed order. */
(function () {
    if (!window.Chart) return;
    const css = n => getComputedStyle(document.documentElement).getPropertyValue(n).trim();
    const palette = () => [1, 2, 3, 4, 5, 6, 7, 8].map(i => css(`--ui-s${i}`));
    const peso = new Intl.NumberFormat("en-PH", { style: "currency", currency: "PHP", minimumFractionDigits: 2 });
    const num = new Intl.NumberFormat("en-US");
    const short = v => Math.abs(v) >= 1e6 ? (v / 1e6).toFixed(1).replace(/\.0$/, "") + "M" : Math.abs(v) >= 1e3 ? (v / 1e3).toFixed(1).replace(/\.0$/, "") + "k" : String(v);
    const alpha = (hex, a) => { const n = parseInt(hex.slice(1), 16); return `rgba(${n >> 16 & 255},${n >> 8 & 255},${n & 255},${a})`; };
    const charts = [];

    function build(canvas) {
        const spec = JSON.parse(canvas.dataset.uiChart);
        const colors = palette(), surface = css("--app-surface"), ink = css("--app-muted"), grid = css("--ui-grid");
        const fmt = v => spec.money ? peso.format(v) : num.format(v);
        const axisFmt = v => (spec.money ? "₱" : "") + short(v);
        const t = spec.type, horizontal = t === "hbar", donut = t === "donut", line = t === "line" || t === "area";
        const multi = spec.series.length > 1;

        const datasets = spec.series.map((s, i) => {
            const c = donut ? spec.labels.map((_, j) => colors[j % 8]) : colors[i % 8];
            const base = { label: s.name, data: s.values };
            if (donut) return { ...base, backgroundColor: c, borderColor: surface, borderWidth: 2, hoverOffset: 4 };
            if (line) return { ...base, borderColor: c, backgroundColor: t === "area" ? alpha(c, .12) : c, fill: t === "area" ? "origin" : false,
                borderWidth: 2, tension: .3, cubicInterpolationMode: "monotone", pointRadius: 4, pointHoverRadius: 6, pointBackgroundColor: c, pointBorderColor: surface, pointBorderWidth: 2 };
            return { ...base, backgroundColor: c, hoverBackgroundColor: alpha(c, .85), maxBarThickness: 24, borderRadius: 4,
                borderSkipped: t === "stackedbar" ? false : "start", borderColor: surface, borderWidth: t === "stackedbar" ? { top: 2, right: 2 } : 0 };
        });

        const valueAxis = { beginAtZero: true, grid: { color: grid, drawTicks: false }, border: { display: false },
            ticks: { color: ink, callback: axisFmt, padding: 6, maxTicksLimit: 6 }, stacked: t === "stackedbar" };
        const clip = s => typeof s === "string" && s.length > 24 ? s.slice(0, 23) + "…" : s;
        const catAxis = { grid: { display: false }, border: { color: grid }, stacked: t === "stackedbar",
            ticks: { color: ink, autoSkip: true, maxRotation: 0, callback(v) { return clip(this.getLabelForValue(v)); } } };
        valueAxis.ticks.maxRotation = 0;

        return new Chart(canvas, {
            type: donut ? "doughnut" : line ? "line" : "bar",
            data: { labels: spec.labels, datasets },
            options: {
                responsive: true, maintainAspectRatio: false, animation: { duration: 400 },
                indexAxis: horizontal ? "y" : "x",
                cutout: donut ? "68%" : undefined,
                interaction: donut ? undefined : { mode: horizontal ? "nearest" : "index", intersect: false, axis: horizontal ? "y" : "x" },
                scales: donut ? {} : horizontal ? { x: valueAxis, y: catAxis } : { x: catAxis, y: valueAxis },
                plugins: {
                    legend: { display: multi || donut, position: donut ? "right" : "bottom",
                        labels: { color: css("--app-ink"), usePointStyle: true, pointStyle: "rectRounded", boxWidth: 10, boxHeight: 10, padding: 14 } },
                    tooltip: { backgroundColor: "#1c2434", padding: 10, cornerRadius: 6, boxPadding: 4,
                        callbacks: { label: ctx => ` ${donut ? ctx.label : ctx.dataset.label}: ${fmt(donut ? ctx.parsed : horizontal ? ctx.parsed.x : ctx.parsed.y)}`
                            + (donut ? ` (${(ctx.parsed / ctx.dataset.data.reduce((a, b) => a + b, 0) * 100).toFixed(1)}%)` : "") } }
                }
            }
        });
    }

    function renderAll() {
        charts.splice(0).forEach(c => c.destroy());
        Chart.defaults.font.family = css("--bs-body-font-family") || "Segoe UI, sans-serif";
        document.querySelectorAll("canvas[data-ui-chart]").forEach(c => charts.push(build(c)));
    }
    document.addEventListener("DOMContentLoaded", renderAll);
    document.addEventListener("ui:themechange", renderAll);   // colours differ in dark mode
    window.UiCharts = { renderAll };
})();
