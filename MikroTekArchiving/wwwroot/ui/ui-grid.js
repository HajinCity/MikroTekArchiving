/* UI KIT — DataGrid: search, sort, paging, live totals, CSV export, print.
   Works on any <div data-ui-grid> rendered by Views/Shared/UI/_DataGrid.cshtml. */
(function () {
    const money = new Intl.NumberFormat("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const whole = new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 });

    class UiGrid {
        constructor(root) {
            this.root = root;
            this.table = root.querySelector("table");
            this.tbody = this.table.tBodies[0];
            this.emptyRow = this.tbody.querySelector(".ui-grid-empty");
            this.rows = [...this.tbody.rows].filter(r => r !== this.emptyRow);
            this.rows.forEach(r => r._text = r.textContent.toLowerCase().replace(/\s+/g, " "));
            this.pageSize = parseInt(root.dataset.pageSize || "10", 10);
            this.page = 1; this.query = ""; this.sortCol = -1; this.sortDir = 1;
            this.bind(); this.render();
        }
        bind() {
            const r = this.root;
            r.querySelector("[data-grid-search]")?.addEventListener("input", e => { this.query = e.target.value.trim().toLowerCase(); this.page = 1; this.render(); });
            r.querySelector("[data-grid-size]")?.addEventListener("change", e => { this.pageSize = parseInt(e.target.value, 10); this.page = 1; this.render(); });
            r.querySelector("[data-grid-export]")?.addEventListener("click", () => this.exportCsv());
            r.querySelector("[data-grid-print]")?.addEventListener("click", () => this.print());
            this.table.querySelectorAll("th.sortable").forEach(th => {
                const go = () => this.sort(parseInt(th.dataset.col, 10), th);
                th.addEventListener("click", go);
                th.addEventListener("keydown", e => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); go(); } });
            });
            r.querySelector("[data-grid-pager]")?.addEventListener("click", e => {
                const a = e.target.closest("[data-page]"); if (!a) return;
                e.preventDefault(); this.page = parseInt(a.dataset.page, 10); this.render();
            });
        }
        filtered() { return this.query ? this.rows.filter(r => r._text.includes(this.query)) : this.rows; }
        sort(col, th) {
            this.sortDir = this.sortCol === col ? -this.sortDir : 1; this.sortCol = col;
            this.table.querySelectorAll("th.sortable").forEach(h => h.setAttribute("aria-sort", "none"));
            th.setAttribute("aria-sort", this.sortDir === 1 ? "ascending" : "descending");
            const type = th.dataset.type;
            const key = r => r.cells[col]?.dataset.sort ?? "";
            this.rows.sort((a, b) => {
                const x = key(a), y = key(b);
                if (type === "num") return ((parseFloat(x) || 0) - (parseFloat(y) || 0)) * this.sortDir;
                return x.localeCompare(y, undefined, { numeric: true, sensitivity: "base" }) * this.sortDir;
            });
            this.rows.forEach(r => this.tbody.insertBefore(r, this.emptyRow));
            this.render();
        }
        render() {
            const list = this.filtered();
            const size = this.pageSize || list.length || 1;
            const pages = Math.max(1, Math.ceil(list.length / size));
            this.page = Math.min(this.page, pages);
            const start = (this.page - 1) * size, end = Math.min(start + size, list.length);
            const visible = new Set(list.slice(start, end));
            this.rows.forEach(r => r.hidden = !visible.has(r));
            if (this.emptyRow) this.emptyRow.hidden = list.length > 0;

            // live totals of the filtered rows
            this.table.querySelectorAll("tfoot [data-total-col]").forEach(td => {
                const c = parseInt(td.dataset.totalCol, 10);
                const sum = list.reduce((s, r) => s + (parseFloat(r.cells[c]?.dataset.sort) || 0), 0);
                td.textContent = td.dataset.money === "1" ? money.format(sum) : whole.format(sum);
            });

            const info = this.root.querySelector("[data-grid-info]");
            if (info) info.textContent = list.length === 0 ? "No matching records"
                : `Showing ${start + 1}–${end} of ${list.length}` + (list.length !== this.rows.length ? ` (filtered from ${this.rows.length})` : "");

            const pager = this.root.querySelector("[data-grid-pager]");
            if (!pager) return;
            if (pages <= 1) { pager.innerHTML = ""; return; }
            const li = (p, label, dis, act) => `<li class="page-item ${dis ? "disabled" : ""} ${act ? "active" : ""}"><a class="page-link" href="#" data-page="${p}" ${act ? 'aria-current="page"' : ""}>${label}</a></li>`;
            let from = Math.max(1, this.page - 2), to = Math.min(pages, from + 4); from = Math.max(1, to - 4);
            let h = li(this.page - 1, "‹", this.page === 1);
            for (let p = from; p <= to; p++) h += li(p, p, false, p === this.page);
            pager.innerHTML = h + li(this.page + 1, "›", this.page === pages);
        }
        headers() { return [...this.table.tHead.rows[0].cells].filter(th => !th.classList.contains("no-export")).map(th => th.textContent.trim()); }
        exportCsv() {
            const esc = v => /[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v;
            const lines = [this.headers().map(esc).join(",")];
            this.filtered().forEach(r => lines.push([...r.cells].filter(td => !td.classList.contains("no-export")).map(td => esc(td.dataset.export ?? td.textContent.trim())).join(",")));
            const blob = new Blob(["﻿" + lines.join("\r\n")], { type: "text/csv;charset=utf-8" });
            const a = document.createElement("a");
            a.href = URL.createObjectURL(blob);
            a.download = `${this.root.dataset.exportName || "export"}-${new Date().toISOString().slice(0, 10)}.csv`;
            a.click(); setTimeout(() => URL.revokeObjectURL(a.href), 1000);
        }
        print() {
            const rows = this.filtered().map(r => `<tr>${[...r.cells].filter(td => !td.classList.contains("no-export")).map(td => `<td class="${td.className}">${td.dataset.export ?? td.textContent.trim()}</td>`).join("")}</tr>`).join("");
            const foot = this.table.tFoot ? this.table.tFoot.outerHTML : "";
            const title = document.querySelector(".app-pagebar h1")?.textContent ?? document.title;
            const w = window.open("", "_blank");
            w.document.write(`<html><head><title>${title}</title><style>body{font-family:Segoe UI,Arial,sans-serif;font-size:11px;margin:16px}h1{font-size:15px}
                table{width:100%;border-collapse:collapse}th,td{border:1px solid #999;padding:4px 6px}th{background:#eee;text-align:left}.text-end{text-align:right}
                tfoot td{font-weight:bold}</style></head><body><h1>${title}</h1><p>Printed ${new Date().toLocaleString()}</p>
                <table><thead><tr>${this.headers().map(h => `<th>${h}</th>`).join("")}</tr></thead><tbody>${rows}</tbody>${foot}</table></body></html>`);
            w.document.close(); w.focus(); w.print();
        }
    }
    document.addEventListener("DOMContentLoaded", () => document.querySelectorAll("[data-ui-grid]").forEach(el => el._grid = new UiGrid(el)));
    window.UiGrid = UiGrid;
})();
