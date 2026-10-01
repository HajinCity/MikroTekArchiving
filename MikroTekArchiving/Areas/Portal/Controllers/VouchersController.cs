using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.TemplateData;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

public class VouchersController : PortalControllerBase
{
    public IActionResult Index(string? q, string? dept, string? status, int? ca)
    {
        var list = SampleData.Vouchers.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q))
            list = list.Where(v => $"{v.DvNo} {v.Payee} {v.Particulars} {v.CheckNo}"
                .Contains(q, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(dept)) list = list.Where(v => v.Dept == dept);
        if (ca is not null) list = list.Where(v => v.CashAdvanceId == ca);
        if (status == "complete") list = list.Where(v => v.IsComplete);
        if (status == "incomplete") list = list.Where(v => !v.IsComplete);
        return View(list.OrderByDescending(v => v.Date).ThenByDescending(v => v.DvNo).ToList());
    }

    public IActionResult Details(int id) => View(SampleData.Voucher(id));

    // ── Scan-first flow ───────────────────────────────────────────────
    // 1. Upload the scanned packet → a Draft DV is created with the file attached.
    // 2. The offline OCR reads the pages and pre-fills what it can.
    // 3. The bookkeeper reviews side-by-side with the scan and confirms.

    [HttpGet]
    public IActionResult Upload() => View();

    [HttpPost, ValidateAntiForgeryToken, ActionName("Upload")]
    public IActionResult UploadPost() => RedirectToAction(nameof(Review), new { id = SampleData.Scan.DraftId });

    [HttpGet]
    public IActionResult Review(int id) => View(SampleData.Scan);

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Confirm(int id) => TemplateSaved("DV confirmed and archived", nameof(Details), new { id = 10 });

    /// <summary>Manual encoding (backup when there is no scan yet).</summary>
    [HttpGet]
    public IActionResult Create(int? ca) => View("Edit", null);

    [HttpGet]
    public IActionResult Edit(int id) => View("Edit", SampleData.Voucher(id));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(int? id) =>
        TemplateSaved("Disbursement voucher saved", id is null ? nameof(Index) : nameof(Details), id is null ? null : new { id });

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SaveChecklist(int id) => TemplateSaved("Checklist updated", nameof(Details), new { id });

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult AddFiles(int id) => TemplateSaved("File uploaded", nameof(Details), new { id });

    /// <summary>Printable archive record: DV details + checklist cover sheet.</summary>
    public IActionResult Print(int id) => View(SampleData.Voucher(id));
}
