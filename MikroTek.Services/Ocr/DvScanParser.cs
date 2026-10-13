using System.Text.RegularExpressions;
using MikroTek.Domain;
using MikroTek.Services.DTO;
using MikroTek.Services.Utils;
using static MikroTek.Services.Ocr.PacketParser;

namespace MikroTek.Services.Ocr
{
    /// <summary>
    /// Turns the lines read from a DV form (Appendix 32) into the DV's fields: payee, TIN, particulars, the accounting
    /// entry (expense lines, Due to BIR credits) and the Receipt of Payment box. The amounts are checked against each
    /// other (debits = credits, gross − taxes = amount due) and a misread amount is corrected when the others settle it.
    /// </summary>
    public static class DvScanParser
    {
        private const decimal Tolerance = 0.05m;
        private const string AdvancesCode = "1990101000";

        public record Template(long Id, string Name);

        private record Entry(string Title, string Code, decimal? Debit, decimal? Credit);

        public static DvScanResult Parse(DvScanPages read, IReadOnlyList<Account> accounts, IReadOnlyList<KnownPayee> payees,
                                         IReadOnlyList<Template> templates)
        {
            var result = new DvScanResult { PageCount = read.PageCount, DvPage = read.DvPage };
            if (read.DvPage is null)
            {
                result.Message = read.PageCount == 0
                    ? "The file has no pages to read."
                    : read.Dpi is > 0 and < 120
                        ? $"The scan is too small to read (about {read.Dpi} dpi). Scan the DV at 150–300 dpi, or type the details from the scan."
                        : "No Disbursement Voucher form (Appendix 32) was found in the scan. Type the details from the scan instead.";
                Cover(read, templates, result);
                return result;
            }
            result.Found = true;
            var lines = read.Lines;
            var f = result.Fields;

            // ---- simple label → value boxes
            Put(f, "fundCluster", Clean(ValueOf(lines, @"Fund\s*Cluster\s*:?"), 50, letters: true));
            var payee = Name(ValueOf(lines, @"^\W*Payee\b\W*", rightFirst: true));
            Put(f, "payee", payee);
            var tin = ValueOf(lines, @"TIN\s*/?\s*Employee\s*No\.?\s*:?");
            var tinMatch = Regex.Match(tin ?? "", @"\d{3}\s*-?\s*\d{3}\s*-?\s*\d{3}(?:\s*-?\s*\d{3,5})?");
            Put(f, "tin", tinMatch.Success ? Regex.Replace(tinMatch.Value, @"\s", "") : null);
            Put(f, "orsBurs", Clean(ValueOf(lines, @"ORS\s*/?\s*BURS\s*No\.?\s*:?"), 50, letters: false));
            Put(f, "address", Clean(ValueOf(lines, @"^\W*Address\b\W*", rightFirst: true, wholeCell: true), 250, letters: true));
            Put(f, "jevNo", Clean(ValueOf(lines, @"JEV\s*No\.?\s*:?"), 50, letters: false));

            // ---- Particulars | Responsibility Center | MFO/PAP | Amount
            decimal? grossOnForm = null;
            var header = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"^\W*Particulars\W*$", RegexOptions.IgnoreCase));
            if (header is not null)
            {
                var below = lines.Where(l => l.Row == NextRow(lines, header.Row)).ToList();
                string? Under(string title)
                {
                    var head = lines.FirstOrDefault(l => l.Row == header.Row && Regex.IsMatch(l.Text, title, RegexOptions.IgnoreCase));
                    if (head is null) return null;
                    var parts = below.Where(l => Overlap(l, head)).OrderBy(l => l.Top).Select(l => l.Text.Trim()).ToList();
                    return parts.Count == 0 ? null : JoinLines(parts);
                }
                Put(f, "particulars", Clean(Under(@"^\W*Particulars"), 1000, letters: true));
                if (f.TryGetValue("particulars", out var particulars) && LooksGarbled(particulars))
                {
                    result.Unsure.Add("particulars");
                    result.Notes.Add("Part of the particulars was hard to read (a stamp or signature over it?). Please check it against the scan.");
                }
                Put(f, "rc", Clean(Under(@"Responsibility"), 50, letters: false));
                Put(f, "mfo", Clean(Under(@"MFO|PAP"), 50, letters: true));
                grossOnForm = Abs(Amount(Under(@"^\W*Amount\W*$")));
            }
            var amountDue = Abs(Amount(ValueOf(lines, @"Amount\s*Due", rightFirst: true)));
            var approved = ApprovedAmount(lines);

            // ---- Mode of payment: "Others CHECK" means a check; an X before ADA means ADA.
            var modeLine = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"MDS|Commercial|Others", RegexOptions.IgnoreCase))?.Text ?? "";
            if (Regex.IsMatch(modeLine, @"Others\s+CHECK", RegexOptions.IgnoreCase)) f["mode"] = "Check";
            else if (Regex.IsMatch(modeLine, @"\[\s*[Xx]\s*\]\s*ADA|\bX\s+ADA", RegexOptions.IgnoreCase)) f["mode"] = "ADA";

            // ---- Receipt of Payment
            var receipt = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"Receipt\s+of\s+Payment", RegexOptions.IgnoreCase));
            var bankLines = lines.Where(l => receipt is null || l.Row > receipt.Row).ToList();
            var bank = BankOf(bankLines);
            Put(f, "bank", bank);
            var printed = lines.Where(l => (receipt is null || l.Row > receipt.Row) && Regex.IsMatch(l.Text, @"Printed\s*Name", RegexOptions.IgnoreCase)).LastOrDefault();
            if (printed is not null)
            {
                // The name may run over two lines of its box.
                var name = Name(ValueAfter(lines, printed, @"Printed\s*Name\s*:?", allLines: true));
                if (name is not null && payee is not null && SameName(name, payee)) name = payee;
                Put(f, "receivedBy", name);
            }

            // ---- what is often written by pen
            var dvNoMatch = DvNoPattern.Match(read.PenFields.GetValueOrDefault("DvNo") ?? "");
            if (dvNoMatch.Success) { f["dvNo"] = DvNo(dvNoMatch); result.Unsure.Add("dvNo"); }
            var date = Date(Regex.Replace(read.PenFields.GetValueOrDefault("Date") ?? "", @"^\W*Date\s*:?", "", RegexOptions.IgnoreCase));
            if (date is not null) { f["dvDate"] = date.Value.ToString("yyyy-MM-dd"); result.Unsure.Add("dvDate"); }
            var checkNo = Regex.Match(Regex.Replace(read.PenFields.GetValueOrDefault("CheckNo") ?? "", @"(?<=\d)[Oo]|[Oo](?=\d)", "0"), @"\d{6,15}");
            if (checkNo.Success) { f["checkNo"] = checkNo.Value; result.Unsure.Add("checkNo"); }
            var paid = Date(Regex.Replace(read.PenFields.GetValueOrDefault("PaidDate") ?? "", @"^\W*Date\s*:?", "", RegexOptions.IgnoreCase));
            if (paid is not null) { f["paidDate"] = paid.Value.ToString("yyyy-MM-dd"); result.Unsure.Add("paidDate"); }
            var missingPen = new List<string>();
            if (!f.ContainsKey("dvNo")) missingPen.Add("DV No.");
            if (!f.ContainsKey("dvDate")) missingPen.Add("date");
            if (!f.ContainsKey("checkNo")) missingPen.Add("check/ADA no.");
            if (missingPen.Count > 0)
                result.Notes.Add($"Handwritten or unclear on the scan, please type: {string.Join(", ", missingPen)}.");

            // ---- the accounting entry
            var entries = Entries(lines);
            Accounting(entries, grossOnForm, amountDue ?? approved, accounts, result);

            // ---- a payee already in Setup: its spelling, TIN and address
            var known = payees.FirstOrDefault(p => SameName(p.Name, payee));
            if (known is not null)
            {
                f["payee"] = known.Name;
                if (!f.ContainsKey("tin") && !string.IsNullOrWhiteSpace(known.Tin)) f["tin"] = known.Tin!;
                if (!f.ContainsKey("address") && !string.IsNullOrWhiteSpace(known.Address)) f["address"] = known.Address!;
            }

            Cover(read, templates, result);
            if (!f.ContainsKey("payee")) result.Notes.Add("The payee could not be read.");
            result.Message = $"Read from page {read.DvPage} of {read.PageCount}.";
            return result;
        }

        // ------------------------------------------------------------------ the accounting entry

        /// <summary>Rows of the "Account Title | UACS Code | Debit | Credit" table, one per UACS code.</summary>
        private static List<Entry> Entries(List<FormLine> lines)
        {
            var codeHead = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"UACS\s*Code", RegexOptions.IgnoreCase));
            if (codeHead is null) return [];
            FormLine? Head(string pattern) => lines.FirstOrDefault(l => l.Row == codeHead.Row && Regex.IsMatch(l.Text, pattern, RegexOptions.IgnoreCase));
            var titleHead = Head(@"Account\s*Title");
            var debitHead = Head(@"^\W*Debit");
            var creditHead = Head(@"^\W*Credit");
            var end = lines.Where(l => l.Row > codeHead.Row && Regex.IsMatch(l.Text, @"Certified|Approved\s+for", RegexOptions.IgnoreCase))
                .Select(l => l.Row).DefaultIfEmpty(int.MaxValue).Min();
            var body = lines.Where(l => l.Row > codeHead.Row && l.Row < end).ToList();

            List<FormLine> Column(FormLine? head) => head is null ? [] : body.Where(l => Overlap(l, head)).ToList();
            var codes = Column(codeHead).Where(l => Digits(l.Text).Length >= 7).ToList();
            var titles = Column(titleHead);
            var debits = Column(debitHead);
            var credits = Column(creditHead);

            FormLine? Beside(List<FormLine> column, FormLine anchor)
            {
                var near = column.OrderBy(l => Math.Abs(l.Middle - anchor.Middle)).FirstOrDefault();
                return near is not null && Math.Abs(near.Middle - anchor.Middle) <= (anchor.Bottom - anchor.Top) * 0.6 ? near : null;
            }
            return codes.OrderBy(c => c.Row).ThenBy(c => c.Top).Select(c => new Entry(
                Beside(titles, c)?.Text.Trim() ?? "",
                Digits(c.Text),
                Abs(Amount(Beside(debits, c)?.Text)),
                Abs(Amount(Beside(credits, c)?.Text)))).ToList();
        }

        private static void Accounting(List<Entry> entries, decimal? grossOnForm, decimal? netOnForm, IReadOnlyList<Account> accounts, DvScanResult result)
        {
            if (entries.Count == 0)
            {
                result.Notes.Add("The accounting entry could not be read. Please add the account lines.");
                return;
            }
            bool IsBir(Entry e) => e.Code == TaxRules.DueToBirCode || Regex.IsMatch(e.Title, @"\bB\s*[I1l]\s*R\b", RegexOptions.IgnoreCase);
            bool IsAdvance(Entry e) => e.Code == AdvancesCode || Regex.IsMatch(e.Title, @"Advances?\s+(to|for)\s+Operating", RegexOptions.IgnoreCase);

            // Expense lines: the debits.
            var expense = entries.Where(e => !IsBir(e) && !IsAdvance(e) && (e.Debit is not null || e.Credit is null)).ToList();
            var lines = new List<(Account? Account, string Code, string Title, decimal? Amount, string? Flag)>();
            foreach (var e in expense)
            {
                var (account, flag) = MatchAccount(e.Code, e.Title, accounts);
                lines.Add((account, account?.Code ?? e.Code, account?.Title ?? e.Title, e.Debit, flag));
            }

            // Taxes: the Due to BIR credits.
            decimal? t1 = null, t3 = null, t5 = null;
            var unknownRate = new List<decimal>();
            foreach (var e in entries.Where(IsBir))
            {
                var rate = Regex.Match(e.Title, @"(\d)\s*%");
                switch (rate.Success ? rate.Groups[1].Value : "")
                {
                    case "1": t1 = (t1 ?? 0) + (e.Credit ?? 0); break;
                    case "3": t3 = (t3 ?? 0) + (e.Credit ?? 0); break;
                    case "5": t5 = (t5 ?? 0) + (e.Credit ?? 0); break;
                    default: if (e.Credit is not null) unknownRate.Add(e.Credit.Value); break;
                }
            }
            var advanceCredit = entries.Where(IsAdvance).Select(e => e.Credit).FirstOrDefault(c => c is not null);

            // The net paid: what most of the figures on the form agree on.
            var netCandidates = new[] { netOnForm, advanceCredit }.Where(x => x is > 0).Select(x => x!.Value).ToList();
            decimal? net = netCandidates.Count == 0 ? null
                : netCandidates.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;

            // One expense amount not read: what is left of the gross (or of net + taxes).
            var missing = lines.Where(l => l.Amount is null).ToList();
            var taxesRead = (t1 ?? 0) + (t3 ?? 0) + (t5 ?? 0) + unknownRate.Sum();
            var grossTarget = grossOnForm ?? (net is not null ? net + taxesRead : null);
            if (missing.Count == 1 && grossTarget is not null)
            {
                var rest = grossTarget.Value - lines.Sum(l => l.Amount ?? 0);
                if (rest > 0)
                {
                    var i = lines.IndexOf(missing[0]);
                    lines[i] = lines[i] with { Amount = rest, Flag = "The amount could not be read; worked out from the total." };
                }
            }

            var gross = lines.Sum(l => l.Amount ?? 0);
            var type = t5 > 0 ? PayeeTaxType.Vat : (t1 > 0 || t3 > 0 || unknownRate.Count > 0) ? PayeeTaxType.NonVat : PayeeTaxType.Exempt;
            var computed = TaxRules.Compute(gross, type);
            foreach (var value in unknownRate)
            {
                if (t1 is null && Math.Abs(value - computed.Tax1) <= Tolerance) t1 = value;
                else if (t3 is null && Math.Abs(value - computed.Tax3) <= Tolerance) t3 = value;
                else if (t5 is null && Math.Abs(value - computed.Tax5) <= Tolerance) t5 = value;
                else result.Notes.Add($"A Due to BIR amount of {value:N2} was read but not its rate (1%, 3% or 5%).");
            }

            // A misread tax: the computed amount is used when it makes gross − taxes equal the net on the form.
            if (net is not null && Math.Abs(gross - ((t1 ?? 0) + (t3 ?? 0) + (t5 ?? 0)) - net.Value) > Tolerance)
            {
                var options = new (string Field, decimal? Read, decimal Computed)[] { ("tax1", t1, computed.Tax1), ("tax3", t3, computed.Tax3), ("tax5", t5, computed.Tax5) };
                var taxes = (t1 ?? 0) + (t3 ?? 0) + (t5 ?? 0);
                var fits = options.Where(o => o.Computed > 0 && Math.Abs(gross - (taxes - (o.Read ?? 0) + o.Computed) - net.Value) <= Tolerance).ToList();
                if (fits.Count == 1)
                {
                    var (field, read, value) = fits[0];
                    if (field == "tax1") t1 = value; else if (field == "tax3") t3 = value; else t5 = value;
                    result.Unsure.Add(field);
                    result.Notes.Add($"A tax was read as {read:N2}; the amount due on the DV shows it is {value:N2}.");
                }
            }

            foreach (var l in lines)
                result.Lines.Add(new DvScanLine(l.Account?.Code ?? (l.Code.Length > 0 ? l.Code : null), l.Title, l.Amount, l.Flag));

            result.Tax1 = t1 ?? 0;
            result.Tax3 = t3 ?? 0;
            result.Tax5 = t5 ?? 0;
            result.Fields["payeeTax"] = type.ToString();
            result.TaxBase = type == PayeeTaxType.Vat ? computed.TaxBase : gross;
            // Automatic when the DV's taxes are the usual ones for the tax type, otherwise as written (Manual).
            result.ManualTax = Math.Abs(result.Tax1.Value - computed.Tax1) > Tolerance || Math.Abs(result.Tax3.Value - computed.Tax3) > Tolerance
                               || Math.Abs(result.Tax5.Value - computed.Tax5) > Tolerance;
            result.Net = net;

            var netNow = gross - result.Tax1.Value - result.Tax3.Value - result.Tax5.Value;
            if (type == PayeeTaxType.Exempt) result.Notes.Add("No tax is withheld on this DV, so the tax type is set to Exempt.");
            if (grossOnForm is not null && Math.Abs(grossOnForm.Value - gross) > Tolerance)
            {
                result.Notes.Add($"The account lines add up to {gross:N2} but the Amount column shows {grossOnForm:N2}.");
                result.Unsure.Add("lines");
            }
            if (net is not null && Math.Abs(net.Value - netNow) > Tolerance)
            {
                result.Notes.Add($"Gross {gross:N2} less taxes is {netNow:N2}, but the DV shows an amount due of {net:N2}.");
                result.Unsure.Add("lines");
            }
            else if (net is not null)
                result.Notes.Add($"The amounts agree with each other: gross {gross:N2} less taxes = amount due {net:N2}.");
        }

        private static decimal? ApprovedAmount(List<FormLine> lines)
        {
            var head = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"Approved\s+for\s+Payment", RegexOptions.IgnoreCase));
            if (head is null) return null;
            var same = Abs(Amount(Regex.Replace(head.Text, @".*Payment", "", RegexOptions.IgnoreCase)));
            if (same is not null) return same;
            return lines.Where(l => l.Row > head.Row && l.Row <= head.Row + 2 && Overlap(l, head))
                .Select(l => Abs(Amount(l.Text))).FirstOrDefault(a => a is not null);
        }

        private static string? BankOf(List<FormLine> lines)
        {
            var label = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"Bank\s*Name", RegexOptions.IgnoreCase));
            if (label is null) return null;
            var cell = lines.Where(l => l.Row == label.Row && l.Cell == label.Cell && l.Line >= label.Line)
                .TakeWhile(l => !Regex.IsMatch(l.Text, @"Printed\s*Name", RegexOptions.IgnoreCase))
                .Select(l => l.Text.Trim()).ToList();
            var text = Regex.Replace(JoinLines(cell), @"^.*?Bank\s*Name\s*&?\s*(and\s*)?Account\s*(Number|No\.?)?\s*:?\s*", "", RegexOptions.IgnoreCase);
            return Clean(text, 100, letters: true);
        }

        // ------------------------------------------------------------------ the checklist cover sheet

        /// <summary>The transaction type from the cover sheet ("JOB ORDER - …"), when it matches a checklist template.</summary>
        private static void Cover(DvScanPages read, IReadOnlyList<Template> templates, DvScanResult result)
        {
            if (read.CoverText.Length == 0 || templates.Count == 0) return;
            var best = read.CoverText.Split('\n')
                .Select(line => Regex.Match(line, @"^\W*([A-Za-z][A-Za-z0-9 &/.]{2,60}?)\s*[-–—:]").Groups[1].Value.Trim())
                .Where(t => t.Length >= 3 && !Regex.IsMatch(t, @"^DV\b", RegexOptions.IgnoreCase))
                .SelectMany(t => templates.Select(tp => (Template: tp, Score: TitleScore(t, tp.Name) + (string.Equals(t, tp.Name, StringComparison.OrdinalIgnoreCase) ? 1 : 0))))
                .OrderByDescending(x => x.Score).FirstOrDefault();
            if (best.Template is not null && best.Score >= 0.6)
            {
                result.Fields["typeSelect"] = best.Template.Id.ToString();
                result.Notes.Add($"Transaction type \"{best.Template.Name}\" taken from the cover sheet. Tick its checklist from the cover sheet.");
            }
        }

        // ------------------------------------------------------------------ finding values next to their labels

        /// <summary>
        /// The value of a label: the rest of the label's own line, else (rightFirst) the next box to the right on that
        /// row, else the next line in the same box.
        /// </summary>
        private static string? ValueOf(List<FormLine> lines, string label, bool rightFirst = false, bool wholeCell = false)
        {
            var at = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, label, RegexOptions.IgnoreCase));
            if (at is null) return null;
            var rest = Regex.Replace(at.Text, "^.*?" + label.TrimStart('^'), "", RegexOptions.IgnoreCase).Trim(' ', ':', '|', '_', '-');
            if (rest.Length >= 2) return rest;

            string? Right()
            {
                var cell = lines.Where(l => l.Row == at.Row && l.Cell > at.Cell).GroupBy(l => l.Cell).OrderBy(g => g.Key).FirstOrDefault();
                if (cell is null) return null;
                var texts = cell.OrderBy(l => l.Top).Select(l => l.Text.Trim()).ToList();
                return wholeCell ? JoinLines(texts) : texts.FirstOrDefault();
            }
            string? Below() => lines.Where(l => l.Row == at.Row && l.Cell == at.Cell && l.Line > at.Line).OrderBy(l => l.Line).FirstOrDefault()?.Text.Trim();
            return rightFirst ? Right() ?? Below() : Below() ?? Right();
        }

        private static string? ValueAfter(List<FormLine> lines, FormLine at, string label, bool allLines = false)
        {
            var rest = Regex.Replace(at.Text, "^.*?" + label, "", RegexOptions.IgnoreCase).Trim(' ', ':', '|', '_');
            if (rest.Length >= 2) return rest;
            var below = lines.Where(l => l.Row == at.Row && l.Cell == at.Cell && l.Line > at.Line).OrderBy(l => l.Line).Select(l => l.Text.Trim()).ToList();
            return below.Count == 0 ? null : allLines ? JoinLines(below.Take(3)) : below[0];
        }

        private static int NextRow(List<FormLine> lines, int row) => lines.Where(l => l.Row > row).Select(l => l.Row).DefaultIfEmpty(row + 1).Min();

        private static bool Overlap(FormLine a, FormLine b) => Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left) > 20;

        /// <summary>Lines of one box as one text; a word broken with a hyphen at the end of a line is joined again.</summary>
        private static string JoinLines(IEnumerable<string> parts)
        {
            var text = "";
            foreach (var p in parts.Where(p => p.Length > 0))
                text = text.Length == 0 ? p : text.EndsWith('-') ? text + p : text + " " + p;
            return text;
        }

        private static string? Clean(string? text, int max, bool letters)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = Regex.Replace(text, @"[|_~`{}\[\]<>©®™=]+", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim(' ', ':', '-', '.', ',');
            if (text.Length < 2 || (letters && !Regex.IsMatch(text, @"[A-Za-z]{2}")) || Regex.IsMatch(text, @"^N\s*/?\s*A$", RegexOptions.IgnoreCase)) return null;
            return text.Length > max ? text[..max] : text;
        }

        private static readonly HashSet<string> ShortWords = new(StringComparer.OrdinalIgnoreCase)
            { "a", "i", "&", "of", "to", "in", "as", "at", "by", "on", "an", "or", "no", "is", "it", "for", "per", "the", "and" };

        /// <summary>
        /// Text with several broken words: stray letters, letters mixed with digits, or symbols inside a word —
        /// typical where a stamp or signature lies over the typing.
        /// </summary>
        private static bool LooksGarbled(string text)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim('.', ',', ';', ':', '(', ')')).Where(w => w.Length > 0).ToList();
            var odd = words.Count(w =>
                (Regex.IsMatch(w, "^[A-Za-z]{1,2}$") && !ShortWords.Contains(w))
                || (Regex.IsMatch(w, "[A-Za-z]") && Regex.IsMatch(w, "[0-9]") && !Regex.IsMatch(w, @"^\d+(st|nd|rd|th|s)$"))
                || Regex.IsMatch(w, @"[A-Za-z][^A-Za-z0-9'\-/&][A-Za-z]")
                || Regex.IsMatch(w, "[\"™=^*~]"));
            return odd >= 2 || (words.Count > 0 && odd * 6 > words.Count);
        }

        private static decimal? Abs(decimal? value) => value is null ? null : Math.Abs(value.Value);

        private static void Put(Dictionary<string, string> fields, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) fields[key] = value.Trim();
        }
    }
}
