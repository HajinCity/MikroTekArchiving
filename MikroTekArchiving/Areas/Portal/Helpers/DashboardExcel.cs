using ClosedXML.Excel;
using MikroTek.Archiving.Areas.Portal.Models;

namespace MikroTek.Archiving.Areas.Portal.Helpers;

/// <summary>Writes the dashboard (exactly what is filtered on screen) to an Excel workbook.</summary>
public static class DashboardExcel
{
    private const string Money = "#,##0.00;[Red](#,##0.00);\"-\"";
    private const string Date = "mm/dd/yyyy";

    public static byte[] Create(DashboardData d, string exportedBy)
    {
        using var wb = new XLWorkbook();
        Summary(wb, d, exportedBy);
        Vouchers(wb, d);
        ByMonth(wb, d);
        Amounts(wb, "By Type", "Type of spending", null, d.ByType.Where(x => x.Amount != 0).ToList());
        Amounts(wb, "By UACS Account", "Account title", "UACS code", d.ByAccount);
        CashAdvances(wb, d);

        using var ms = new MemoryStream();
        // Work the totals out now, so they also show in viewers that do not calculate formulas (phone previews).
        wb.SaveAs(ms, new SaveOptions { EvaluateFormulasBeforeSaving = true });
        return ms.ToArray();
    }

    private static void Summary(XLWorkbook wb, DashboardData d, string exportedBy)
    {
        var ws = wb.AddWorksheet("Summary");
        ws.Cell("A1").Value = "Disbursement Archive — Dashboard";
        ws.Cell("A1").Style.Font.SetBold().Font.SetFontSize(14);
        ws.Cell("A2").Value = "Leonardo National High School";

        var r = 4;
        void Line(string label, XLCellValue value, string? format = null)
        {
            ws.Cell(r, 1).Value = label;
            ws.Cell(r, 2).Value = value;
            if (format is not null) ws.Cell(r, 2).Style.NumberFormat.Format = format;
            r++;
        }
        Line("Dates (DV date)", $"{d.Filter.From:MM/dd/yyyy} to {d.Filter.To:MM/dd/yyyy}");
        Line("Department", d.Filter.DeptText);
        Line("Exported", $"{DateTime.Now:MM/dd/yyyy h:mm tt} by {exportedBy}");
        r++;
        Line("DVs archived", d.Vouchers.Count, "#,##0");
        Line("Gross amount", d.Gross, Money);
        Line("Taxes withheld", d.Taxes, Money);
        Line("Total disbursed (net)", d.Net, Money);
        Line("Incomplete DV packets", d.Incomplete.Count, "#,##0");
        ws.Range(4, 1, r - 1, 1).Style.Font.SetBold();
        ws.Range(4, 2, r - 1, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

        r++;
        ws.Cell(r, 1).Value = $"Fund budgets — year {d.Filter.BudgetYear}";
        ws.Cell(r, 1).Style.Font.SetBold();
        r++;
        var top = r;
        Header(ws, r, "Fund", "Budget", "Spent in selected dates", "Spent this year (to end date)", "Remaining", "% used");
        foreach (var f in d.Funds)
        {
            r++;
            ws.Cell(r, 1).Value = f.Name;
            ws.Cell(r, 2).Value = f.Budget;
            ws.Cell(r, 3).Value = f.SpentInRange;
            ws.Cell(r, 4).Value = f.SpentInYear;
            ws.Cell(r, 5).Value = f.HasBudget ? f.Remaining : Blank.Value;
            ws.Cell(r, 6).Value = f.HasBudget ? f.SpentInYear / f.Budget : Blank.Value;
        }
        ws.Range(top + 1, 2, r, 5).Style.NumberFormat.Format = Money;
        ws.Range(top + 1, 6, r, 6).Style.NumberFormat.Format = "0.0%";

        ws.Column(1).Width = 30;
        ws.Columns(2, 6).Width = 26;
    }

    private static void Vouchers(XLWorkbook wb, DashboardData d)
    {
        var ws = wb.AddWorksheet("DVs");
        Header(ws, 1, "DV No.", "DV Date", "Department", "Fund", "Cash Advance", "Transaction Type", "Payee", "TIN",
            "Particulars", "Gross", "Tax 1%", "Tax 3%", "Net Paid", "Mode", "Check No.", "Date Paid", "Packet", "Docs Missing");
        var advances = TemplateData.SampleData.CashAdvances.ToDictionary(c => c.Id);
        var r = 1;
        foreach (var v in d.Vouchers)
        {
            r++;
            advances.TryGetValue(v.CashAdvanceId, out var ca);
            ws.Cell(r, 1).Value = v.DvNo;
            ws.Cell(r, 2).Value = v.Date;
            ws.Cell(r, 3).Value = v.Dept;
            ws.Cell(r, 4).Value = ca?.FundName ?? "";
            ws.Cell(r, 5).Value = ca?.PayrollNo ?? "";
            ws.Cell(r, 6).Value = v.Type;
            ws.Cell(r, 7).Value = v.Payee;
            ws.Cell(r, 8).Value = v.Tin;
            ws.Cell(r, 9).Value = v.Particulars;
            ws.Cell(r, 10).Value = v.Gross;
            ws.Cell(r, 11).Value = v.Tax1;
            ws.Cell(r, 12).Value = v.Tax3;
            ws.Cell(r, 13).Value = v.Net;
            ws.Cell(r, 14).Value = v.Mode;
            ws.Cell(r, 15).Value = v.CheckNo;
            ws.Cell(r, 16).Value = v.PaidDate is { } paid ? paid : Blank.Value;
            ws.Cell(r, 17).Value = v.IsComplete ? "Complete" : "Incomplete";
            ws.Cell(r, 18).Value = v.DocsMissing;
        }
        Totals(ws, r, 1, 10, 11, 12, 13);
        ws.Range(2, 2, r + 1, 2).Style.NumberFormat.Format = Date;
        ws.Range(2, 16, r + 1, 16).Style.NumberFormat.Format = Date;
        ws.Range(2, 10, r + 1, 13).Style.NumberFormat.Format = Money;
        ws.Column(8).Style.NumberFormat.Format = "@";   // TIN and check numbers are text, not numbers
        ws.Column(15).Style.NumberFormat.Format = "@";
        Finish(ws, r, 18, widths: [18, 12, 12, 16, 16, 34, 34, 18, 60, 15, 13, 13, 15, 12, 14, 12, 12, 13]);
    }

    private static void ByMonth(XLWorkbook wb, DashboardData d)
    {
        var ws = wb.AddWorksheet("By Month");
        Header(ws, 1, "Month", "Junior High (net)", "Senior High (net)", "Total (net)");
        var r = 1;
        foreach (var m in d.Months)
        {
            r++;
            ws.Cell(r, 1).Value = m.Month.ToString("MMMM yyyy");
            ws.Cell(r, 2).Value = m.Jhs;
            ws.Cell(r, 3).Value = m.Shs;
            ws.Cell(r, 4).Value = m.Total;
        }
        Totals(ws, r, 1, 2, 3, 4);
        ws.Range(2, 2, r + 1, 4).Style.NumberFormat.Format = Money;
        Finish(ws, r, 4, widths: [20, 20, 20, 20]);
    }

    private static void Amounts(XLWorkbook wb, string sheet, string nameHeader, string? codeHeader, List<AmountRow> rows)
    {
        var ws = wb.AddWorksheet(sheet);
        var hasCode = codeHeader is not null;
        var amountCol = hasCode ? 3 : 2;
        if (hasCode) Header(ws, 1, codeHeader!, nameHeader, "Gross amount", "% of total");
        else Header(ws, 1, nameHeader, "Gross amount", "% of total");

        var total = rows.Sum(x => x.Amount);
        var r = 1;
        foreach (var x in rows)
        {
            r++;
            if (hasCode) { ws.Cell(r, 1).Value = x.Code; ws.Cell(r, 2).Value = x.Name; }
            else ws.Cell(r, 1).Value = x.Name;
            ws.Cell(r, amountCol).Value = x.Amount;
            ws.Cell(r, amountCol + 1).Value = total == 0 ? 0 : x.Amount / total;
        }
        Totals(ws, r, 1, amountCol, amountCol + 1);
        if (hasCode) ws.Column(1).Style.NumberFormat.Format = "@";
        ws.Range(2, amountCol, r + 1, amountCol).Style.NumberFormat.Format = Money;
        ws.Range(2, amountCol + 1, r + 1, amountCol + 1).Style.NumberFormat.Format = "0.0%";
        Finish(ws, r, amountCol + 1, widths: hasCode ? [16, 48, 18, 12] : [30, 18, 12]);
    }

    private static void CashAdvances(XLWorkbook wb, DashboardData d)
    {
        var ws = wb.AddWorksheet("Cash Advances");
        Header(ws, 1, "Department", "Fund", "Batch", "Payroll No.", "ADA No.", "Date", "Period Covered", "Amount", "Spent to date", "Balance", "Status");
        var r = 1;
        foreach (var c in d.CashAdvances)
        {
            r++;
            ws.Cell(r, 1).Value = c.Dept;
            ws.Cell(r, 2).Value = c.FundName;
            ws.Cell(r, 3).Value = c.BatchNo;
            ws.Cell(r, 4).Value = c.PayrollNo;
            ws.Cell(r, 5).Value = c.AdaNo;
            ws.Cell(r, 6).Value = c.Date;
            ws.Cell(r, 7).Value = c.Period;
            ws.Cell(r, 8).Value = c.Amount;
            ws.Cell(r, 9).Value = c.Spent;
            ws.Cell(r, 10).Value = c.Balance;
            ws.Cell(r, 11).Value = c.Status;
        }
        Totals(ws, r, 1, 8, 9, 10);
        ws.Column(5).Style.NumberFormat.Format = "@";
        ws.Range(2, 6, r + 1, 6).Style.NumberFormat.Format = Date;
        ws.Range(2, 8, r + 1, 10).Style.NumberFormat.Format = Money;
        Finish(ws, r, 11, widths: [12, 16, 8, 18, 14, 12, 24, 16, 16, 16, 12]);
    }

    // ---------------------------------------------------------------- shared look

    private static void Header(IXLWorksheet ws, int row, params string[] titles)
    {
        for (var i = 0; i < titles.Length; i++) ws.Cell(row, i + 1).Value = titles[i];
        var range = ws.Range(row, 1, row, titles.Length);
        range.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        range.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#0B3A8A"));
        range.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetWrapText();
    }

    /// <summary>Adds a bold "Total" row under the last data row with SUM formulas (so they update if a cell is edited).</summary>
    private static void Totals(IXLWorksheet ws, int lastRow, int labelCol, params int[] sumCols)
    {
        var row = lastRow + 1;
        ws.Cell(row, labelCol).Value = "Total";
        foreach (var c in sumCols)
        {
            var letter = ws.Column(c).ColumnLetter();
            ws.Cell(row, c).FormulaA1 = lastRow >= 2 ? $"SUM({letter}2:{letter}{lastRow})" : "0";
        }
        var range = ws.Range(row, 1, row, ws.LastColumnUsed()?.ColumnNumber() ?? sumCols.Max());
        range.Style.Font.SetBold();
        range.Style.Border.SetTopBorder(XLBorderStyleValues.Medium);
    }

    /// <summary>Freezes the header row, turns on the filter arrows and sets column widths.</summary>
    private static void Finish(IXLWorksheet ws, int lastRow, int lastCol, int[] widths)
    {
        ws.SheetView.FreezeRows(1);
        if (lastRow >= 2) ws.Range(1, 1, lastRow, lastCol).SetAutoFilter();
        for (var i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];
        ws.Row(1).Height = 30;
    }
}
