using System.Text.RegularExpressions;
using MikroTek.Domain;
using MikroTek.Services.DTO;
using MikroTek.Services.Utils;
using static MikroTek.Services.Ocr.PacketParser;

namespace MikroTek.Services.Ocr
{
    /// <summary>
    /// Turns what was read from a DV packet into the DV's fields: payee, TIN, particulars, the accounting entry
    /// (expense lines, Due to BIR credits), the Receipt of Payment box, the check number (from the photocopied check)
    /// and the transaction type with its ticks (from the checklist cover sheet).
    /// The amounts are checked against each other (debits = credits, gross − taxes = amount due); when a figure was
    /// misread — a bookkeeper's tick "/" in front of it is often read as a 7 — the other figures decide.
    /// </summary>
    public static class DvScanParser
    {
        private const decimal Tolerance = 0.05m;
        private const string AdvancesCode = "1990101000";
        private static readonly RegexOptions I = RegexOptions.IgnoreCase;

        /// <summary>A checklist template: its name and documents, to recognise the cover sheet.</summary>
        public record Template(long Id, string Name, IReadOnlyList<string> Items);

        /// <summary>One row of the accounting entry; the amounts are the possible readings, the likeliest first.</summary>
        private record Entry(string Title, string Code, List<decimal> Debit, List<decimal> Credit);

        /// <summary>DV numbers as written on a DV: the last part has 3 or 4 digits (JHS2025-09-0062).</summary>
        private static readonly Regex DvNoOnForm = new(@"([JS5$])\s*[HN]\s*[S5$]\s*(\d{4})\s*[-–—]\s*(\d{2})\s*[-–—]\s*(\d{3,4})(?!\d)", RegexOptions.IgnoreCase);

        public static DvScanResult Parse(DvScanPages read, IReadOnlyList<Account> accounts, IReadOnlyList<KnownPayee> payees,
                                         IReadOnlyList<Template> templates)
        {
            var result = new DvScanResult { PageCount = read.PageCount, DvPage = read.DvPage, PageKinds = read.PageKinds };
            Cover(read, templates, result);
            if (read.DvPage is null)
            {
                result.Message = read.PageCount == 0
                    ? "The file has no pages to read."
                    : read.Dpi is > 0 and < 120
                        ? $"The scan is too small to read (about {read.Dpi} dpi). Scan the DV at 150–300 dpi, or type the details from the scan."
                        : "No Disbursement Voucher form (Appendix 32) was found in the scan. Type the details from the scan instead.";
                return result;
            }
            result.Found = true;
            var lines = read.Lines;
            var f = result.Fields;

            // ---- simple label → value boxes (labels allow the usual misreadings: "Fund Claster", "TIN/Employce")
            Put(f, "fundCluster", Clean(ValueOf(lines, @"Fund\s*Cl\w{2,5}\s*:?"), 50, letters: true));
            var payee = Name(ValueOf(lines, @"^\W*Pay[ec]e\b\W*", rightFirst: true));
            Put(f, "payee", payee);
            var tin = ValueOf(lines, @"TIN\s*/?\s*Empl\w*\s*No\.?\s*:?");
            var tinMatch = Regex.Match(tin ?? "", @"\d{3}\s*-?\s*\d{3}\s*-?\s*\d{3}(?:\s*-?\s*\d{3,5})?");
            Put(f, "tin", tinMatch.Success ? Regex.Replace(tinMatch.Value, @"\s", "") : null);
            Put(f, "orsBurs", Clean(ValueOf(lines, @"ORS\s*/?\s*BURS\s*No\.?\s*:?"), 50, letters: false));
            Put(f, "address", Clean(ValueOf(lines, @"^\W*Addr\w*\b\W*", rightFirst: true, wholeCell: true), 250, letters: true));
            Put(f, "jevNo", Clean(ValueOf(lines, @"JEV\s*No\.?\s*:?"), 50, letters: false));

            // ---- Particulars | Responsibility Center | MFO/PAP | Amount
            var grossOnForm = new List<decimal>();
            var netOnForm = new List<List<decimal>>();
            var header = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"^\W*Particulars\W*$", I));
            if (header is not null)
            {
                var bodyRow = NextRow(lines, header.Row);
                var below = lines.Where(l => l.Row == bodyRow).ToList();
                FormLine? Head(string title) => lines.FirstOrDefault(l => l.Row == header.Row && Regex.IsMatch(l.Text, title, I));
                List<string> Under(FormLine? head, IEnumerable<FormLine> from) =>
                    head is null ? [] : from.Where(l => Overlap(l, head)).OrderBy(l => l.Top).Select(l => l.Text.Trim()).ToList();

                // The taxes are often repeated under the particulars ("Due to BIR 1% 174.65"): not part of the text.
                var particulars = Under(Head(@"^\W*Particulars"), below)
                    .Where(t => !Regex.IsMatch(t, @"Du[ce]\s*t[o0]\s*B|^\W*Less\b|^\W*[\d.,\s₱P]+\W*$", I)).ToList();
                Put(f, "particulars", Clean(JoinLines(particulars), 1000, letters: true));
                if (f.TryGetValue("particulars", out var text) && LooksGarbled(text))
                {
                    result.Unsure.Add("particulars");
                    result.Notes.Add("Part of the particulars was hard to read (a stamp or signature over it?). Please check it against the scan.");
                }
                Put(f, "rc", Clean(JoinLines(Under(Head("Responsibility"), below)), 50, letters: false));
                Put(f, "mfo", Clean(JoinLines(Under(Head("MFO|PAP"), below)), 50, letters: true));

                // The Amount column: the gross first, then "Less: 4%", the taxes, and the net in the row below.
                var amountHead = Head(@"^\W*Amount\W*$");
                var amounts = Under(amountHead, below);
                grossOnForm = amounts.Select(Options).FirstOrDefault(o => o.Count > 0) ?? [];
                var netRow = lines.Where(l => l.Row == NextRow(lines, bodyRow)).ToList();
                var net = Under(amountHead, netRow).Select(Options).FirstOrDefault(o => o.Count > 0);
                if (net is not null && !Regex.IsMatch(string.Join(" ", netRow.Select(l => l.Text)), @"Certified", I)) netOnForm.Add(net);
            }
            var due = ValueOf(lines, @"Amount\s*Due", rightFirst: true);
            if (due is not null && Options(due).Count > 0) netOnForm.Add(Options(due));
            var approved = ApprovedAmount(lines);
            if (approved.Count > 0) netOnForm.Add(approved);

            // ---- Mode of payment: "Others … CHECK" means a check; an X before ADA means ADA.
            var modeRow = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"MDS|Com\w*cial|Others", I))?.Row;
            var modeText = string.Join(" ", lines.Where(l => l.Row == modeRow).Select(l => l.Text));
            modeText = Regex.Replace(modeText, @"MDS\s*Check|Com\w*\s*Check", " ", I);
            if (Regex.IsMatch(modeText, @"\bCHECK\b", I)) f["mode"] = "Check";
            else if (Regex.IsMatch(modeText, @"[Xx]\W{0,3}ADA", I)) f["mode"] = "ADA";
            else if (Regex.IsMatch(modeText, @"[Xx]\W{0,3}Others", I)) f["mode"] = "Check";   // "Others" is ticked for checks

            // ---- Receipt of Payment
            var receipt = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"Receipt\s+of\s+Payment", I));
            var after = lines.Where(l => receipt is null || l.Row > receipt.Row).ToList();
            Put(f, "bank", BankOf(after));
            var printed = after.LastOrDefault(l => Regex.IsMatch(l.Text, @"Printed\s*Name", I));
            if (printed is not null)
            {
                // The name may run over two lines of its box.
                var name = Name(ValueAfter(lines, printed, @"Printed\s*Name\s*:?"));
                if (name is not null && payee is not null && SameName(name, payee)) name = payee;
                Put(f, "receivedBy", name);
            }

            // ---- what is often written by pen, and the photocopied check
            PenAndCheck(read, result);

            // ---- the accounting entry
            Accounting(Entries(lines), grossOnForm, netOnForm, accounts, result);

            // ---- a payee already in Setup: its spelling, TIN and address
            var known = payees.FirstOrDefault(p => SameName(p.Name, payee));
            if (known is not null)
            {
                f["payee"] = known.Name;
                if (!f.ContainsKey("tin") && !string.IsNullOrWhiteSpace(known.Tin)) f["tin"] = known.Tin!;
                if (!f.ContainsKey("address") && !string.IsNullOrWhiteSpace(known.Address)) f["address"] = known.Address!;
            }

            if (!f.ContainsKey("payee")) result.Notes.Add("The payee could not be read.");
            result.Message = $"Read from page {read.DvPage} of {read.PageCount}.";
            return result;
        }

        // ------------------------------------------------------------------ pen fields and the check

        private static void PenAndCheck(DvScanPages read, DvScanResult result)
        {
            var f = result.Fields;
            string Pen(string key) => Regex.Replace(read.PenFields.GetValueOrDefault(key) ?? "", @"^\W*(Date|DV\s*No\.?)\s*:?", "", I);

            var dvNo = DvNoOnForm.Match(read.PenFields.GetValueOrDefault("DvNo") ?? "");
            if (dvNo.Success)
            {
                f["dvNo"] = $"{(char.ToUpperInvariant(dvNo.Groups[1].Value[0]) == 'J' ? "JHS" : "SHS")}{dvNo.Groups[2].Value}-{dvNo.Groups[3].Value}-{dvNo.Groups[4].Value}";
                result.Unsure.Add("dvNo");
            }
            var date = Date(Pen("Date"));
            if (date is not null) { f["dvDate"] = date.Value.ToString("yyyy-MM-dd"); result.Unsure.Add("dvDate"); }
            var penCheck = Regex.Match(Regex.Replace(Pen("CheckNo"), @"(?<=\d)[Oo]|[Oo](?=\d)", "0"), @"\d{6,15}");
            var paid = Date(Pen("PaidDate"));
            if (paid is not null) { f["paidDate"] = paid.Value.ToString("yyyy-MM-dd"); result.Unsure.Add("paidDate"); }

            // The check itself has its number printed (and the date stamped by the check writer).
            var check = read.CheckText;
            var printedNo = Regex.Match(check, @"(?<![\d-])\d{10}(?![\d-])");
            if (!printedNo.Success) printedNo = Regex.Match(check, @"(?<![\d-])\d{8,12}(?![\d-])");
            if (printedNo.Success)
            {
                f["checkNo"] = printedNo.Value;
                if (penCheck.Success && penCheck.Value != printedNo.Value)
                    result.Notes.Add($"Check No. {printedNo.Value} is taken from the photocopied check (the DV's handwriting read as {penCheck.Value}).");
                else result.Notes.Add($"Check No. {printedNo.Value} is taken from the photocopied check.");
            }
            else if (penCheck.Success) { f["checkNo"] = penCheck.Value; result.Unsure.Add("checkNo"); }

            var checkDate = Regex.Match(Regex.Replace(check, @"(?<=\d)\s+(?=[\d-/])|(?<=[-/])\s+(?=\d)", ""), @"DATE\D{0,6}(\d{1,2})[-/.](\d{1,2})[-/.](\d{4})", I);
            var stamped = checkDate.Success ? Date($"{checkDate.Groups[1].Value}/{checkDate.Groups[2].Value}/{checkDate.Groups[3].Value}") : null;
            if (stamped is not null)
            {
                if (!f.ContainsKey("paidDate")) { f["paidDate"] = stamped.Value.ToString("yyyy-MM-dd"); result.Unsure.Add("paidDate"); }
                if (!f.ContainsKey("dvDate")) { f["dvDate"] = stamped.Value.ToString("yyyy-MM-dd"); result.Unsure.Add("dvDate"); result.Notes.Add("The DV date is taken from the date on the check."); }
            }

            var missing = new List<string>();
            if (!f.ContainsKey("dvNo")) missing.Add("DV No.");
            if (!f.ContainsKey("dvDate")) missing.Add("DV date");
            if (!f.ContainsKey("checkNo")) missing.Add("check/ADA no.");
            if (missing.Count > 0) result.Notes.Add($"Handwritten or unclear on the scan, please type: {string.Join(", ", missing)}.");
        }

        // ------------------------------------------------------------------ the accounting entry

        /// <summary>
        /// The "Account Title | UACS Code | Debit | Credit" rows. Each title line is a row (a code may be overwritten by
        /// pen and unreadable); a code with no title beside it is a row too.
        /// </summary>
        private static List<Entry> Entries(List<FormLine> lines)
        {
            var codeHead = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"UACS\s*Code", I));
            if (codeHead is null) return [];
            FormLine? Head(string pattern) => lines.FirstOrDefault(l => l.Row == codeHead.Row && Regex.IsMatch(l.Text, pattern, I));
            var titleHead = Head(@"Account\s*Title");
            var debitHead = Head(@"Debit");
            var creditHead = Head(@"Credit");
            var end = lines.Where(l => l.Row > codeHead.Row && Regex.IsMatch(l.Text, @"Certified|Approved\s+for", I))
                .Select(l => l.Row).DefaultIfEmpty(int.MaxValue).Min();
            var body = lines.Where(l => l.Row > codeHead.Row && l.Row < end).ToList();

            List<FormLine> Column(FormLine? head) => head is null ? [] : body.Where(l => Overlap(l, head)).ToList();
            var codes = Column(codeHead).Where(l => Digits(l.Text).Length >= 7).ToList();
            var titles = Column(titleHead).Where(l => Regex.IsMatch(l.Text, "[A-Za-z]{3}")).ToList();
            var debits = Column(debitHead);
            var credits = Column(creditHead);

            FormLine? Beside(List<FormLine> column, FormLine anchor)
            {
                var near = column.OrderBy(l => Math.Abs(l.Middle - anchor.Middle)).FirstOrDefault();
                return near is not null && Math.Abs(near.Middle - anchor.Middle) <= Math.Max(20, (anchor.Bottom - anchor.Top) * 0.6) ? near : null;
            }
            var anchors = titles.Concat(codes.Where(c => Beside(titles, c) is null)).OrderBy(a => a.Row).ThenBy(a => a.Middle).ToList();
            return anchors.Select(a =>
            {
                var isCode = codes.Contains(a);
                var title = isCode ? "" : Regex.Replace(a.Text, @"^\W*[\d/\\|]?\W*(?=[A-Za-z])", "").Trim();
                var code = isCode ? a : Beside(codes, a);
                return new Entry(title, code is null ? "" : Digits(code.Text),
                    Options(Beside(debits, a)?.Text), Options(Beside(credits, a)?.Text));
            }).ToList();
        }

        private static void Accounting(List<Entry> entries, List<decimal> grossOnForm, List<List<decimal>> netOnForm,
                                       IReadOnlyList<Account> accounts, DvScanResult result)
        {
            if (entries.Count == 0)
            {
                result.Notes.Add("The accounting entry could not be read. Please add the account lines.");
                return;
            }
            bool IsBir(Entry e) => e.Code == TaxRules.DueToBirCode || Regex.IsMatch(e.Title, @"\bB\s*[I1l]\s*R\b|Du[ce]\s*t[o0]\s*B", I);
            bool IsAdvance(Entry e) => e.Code == AdvancesCode || Regex.IsMatch(e.Title, @"Advances?\s+(to|for)\s+Operat", I);

            // ---- expense lines: the debits
            var expense = entries.Where(e => !IsBir(e) && !IsAdvance(e) && (e.Debit.Count > 0 || e.Credit.Count == 0)).ToList();
            var lines = new List<(Account? Account, string Code, string Title, decimal? Amount, string? Flag)>();
            foreach (var e in expense)
            {
                var (account, flag) = MatchAccount(e.Code, e.Title, accounts);
                // With one line, its amount is the gross in the Amount column.
                decimal? amount = e.Debit.Count == 0 ? null
                    : expense.Count == 1 ? e.Debit.FirstOrDefault(d => grossOnForm.Any(g => Same(g, d)), e.Debit[0]) : e.Debit[0];
                lines.Add((account, account?.Code ?? e.Code, account?.Title ?? e.Title, amount, flag));
            }
            // Several lines that do not add up to the gross: try the other reading of one of them.
            if (lines.Count > 1 && grossOnForm.Count > 0 && !grossOnForm.Any(g => Same(g, lines.Sum(l => l.Amount ?? 0))))
                for (var i = 0; i < lines.Count; i++)
                {
                    var others = lines.Where((_, k) => k != i).Sum(l => l.Amount ?? 0);
                    var fit = expense[i].Debit.Skip(1).FirstOrDefault(d => grossOnForm.Any(g => Same(g, others + d)));
                    if (fit != 0) { lines[i] = lines[i] with { Amount = fit }; break; }
                }

            // One expense amount not read: what is left of the gross.
            var missing = lines.Where(l => l.Amount is null).ToList();
            if (missing.Count == 1 && grossOnForm.Count > 0)
            {
                var rest = grossOnForm[0] - lines.Sum(l => l.Amount ?? 0);
                if (rest > 0)
                {
                    var i = lines.IndexOf(missing[0]);
                    lines[i] = lines[i] with { Amount = rest, Flag = "The amount could not be read; worked out from the total." };
                }
            }
            var gross = lines.Sum(l => l.Amount ?? 0);

            // ---- taxes: the Due to BIR credits, each taken as the reading that fits its rate
            var birs = entries.Where(IsBir).Select(e => (Rate: Regex.Match(e.Title, @"(\d)\s*%").Groups[1].Value, e.Credit)).ToList();
            var type = birs.Any(b => b.Rate == "5") ? PayeeTaxType.Vat : birs.Count > 0 ? PayeeTaxType.NonVat : PayeeTaxType.Exempt;
            var computed = TaxRules.Compute(gross, type);
            decimal? Tax(string rate, decimal expected)
            {
                var readings = birs.Where(b => b.Rate == rate).SelectMany(b => b.Credit).ToList();
                if (readings.Count == 0) return null;
                return readings.FirstOrDefault(r => Same(r, expected), readings[0]);
            }
            decimal? t1 = Tax("1", computed.Tax1), t3 = Tax("3", computed.Tax3), t5 = Tax("5", computed.Tax5);
            foreach (var value in birs.Where(b => b.Rate is not ("1" or "3" or "5")).Select(b => b.Credit.FirstOrDefault()).Where(v => v > 0))
            {
                if (t1 is null && Same(value, computed.Tax1)) t1 = value;
                else if (t3 is null && Same(value, computed.Tax3)) t3 = value;
                else if (t5 is null && Same(value, computed.Tax5)) t5 = value;
                else result.Notes.Add($"A Due to BIR amount of {value:N2} was read but not its rate (1%, 3% or 5%).");
            }

            // ---- the net paid: the reading that equals gross − taxes, else what most figures say
            var advance = entries.Where(IsAdvance).Select(e => e.Credit).FirstOrDefault(c => c.Count > 0);
            var netReadings = netOnForm.Concat(advance is null ? [] : [advance]).ToList();
            var expectedNet = gross - (t1 ?? 0) - (t3 ?? 0) - (t5 ?? 0);
            decimal? net = netReadings.SelectMany(n => n).Any(n => Same(n, expectedNet)) ? expectedNet
                : netReadings.Count == 0 ? null
                : netReadings.Select(n => n[0]).GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;

            // A misread tax: the computed amount is used when it makes gross − taxes equal the net on the form.
            if (net is not null && !Same(gross - ((t1 ?? 0) + (t3 ?? 0) + (t5 ?? 0)), net.Value))
            {
                var options = new (string Field, decimal? Read, decimal Computed)[] { ("tax1", t1, computed.Tax1), ("tax3", t3, computed.Tax3), ("tax5", t5, computed.Tax5) };
                var taxes = (t1 ?? 0) + (t3 ?? 0) + (t5 ?? 0);
                var fits = options.Where(o => o.Computed > 0 && Same(gross - (taxes - (o.Read ?? 0) + o.Computed), net.Value)).ToList();
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
            result.ManualTax = !Same(result.Tax1.Value, computed.Tax1) || !Same(result.Tax3.Value, computed.Tax3) || !Same(result.Tax5.Value, computed.Tax5);
            result.Net = net;

            var netNow = gross - result.Tax1.Value - result.Tax3.Value - result.Tax5.Value;
            if (type == PayeeTaxType.Exempt) result.Notes.Add("No tax is withheld on this DV, so the tax type is set to Exempt.");
            if (grossOnForm.Count > 0 && !grossOnForm.Any(g => Same(g, gross)))
            {
                result.Notes.Add($"The account lines add up to {gross:N2} but the Amount column shows {grossOnForm[0]:N2}.");
                result.Unsure.Add("lines");
            }
            if (net is not null && !Same(net.Value, netNow))
            {
                result.Notes.Add($"Gross {gross:N2} less taxes is {netNow:N2}, but the DV shows an amount due of {net:N2}.");
                result.Unsure.Add("lines");
            }
            else if (net is not null)
                result.Notes.Add($"The amounts agree with each other: gross {gross:N2} less taxes = amount due {net:N2}.");
        }

        private static List<decimal> ApprovedAmount(List<FormLine> lines)
        {
            var head = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"Approved\s+for\s+Payment", I));
            if (head is null) return [];
            var same = Options(Regex.Replace(head.Text, @".*Payment", "", I));
            if (same.Count > 0) return same;
            return lines.Where(l => l.Row > head.Row && l.Row <= head.Row + 2 && Overlap(l, head))
                .Select(l => Options(l.Text)).FirstOrDefault(a => a.Count > 0) ?? [];
        }

        /// <summary>
        /// The readings an amount can have: as read, and — when it starts with 1, 2, 4 or 7 — without that first digit,
        /// because the bookkeeper's tick "/" before a figure is often read as one of those.
        /// </summary>
        private static List<decimal> Options(string? text)
        {
            var result = new List<decimal>();
            var a = Amount(text);
            if (a is null) return result;
            result.Add(Math.Abs(a.Value));
            var m = Regex.Match(text!, @"^\W*([1247])(?=\d)");
            if (m.Success)
            {
                var b = Amount(text!.Remove(m.Groups[1].Index, 1));
                if (b is not null && !result.Contains(Math.Abs(b.Value))) result.Add(Math.Abs(b.Value));
            }
            return result;
        }

        private static bool Same(decimal a, decimal b) => Math.Abs(a - b) <= Tolerance;

        private static string? BankOf(List<FormLine> lines)
        {
            var label = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"Bank\s*Name", I));
            if (label is null) return null;
            var cell = lines.Where(l => l.Row == label.Row && l.Cell == label.Cell && l.Line >= label.Line)
                .TakeWhile(l => !Regex.IsMatch(l.Text, @"Printed\s*Name", I))
                .Select(l => l.Text.Trim()).ToList();
            // "Bank Name & Account Number: DBP …" or "Bank Name: DBP PAGADIAN & Account Number: 0-06071-840-8"
            var text = Regex.Replace(JoinLines(cell), @"Bank\s*Name\s*:?|&?\s*(and\s+)?Account\s*(Number|No\.?)?\s*:?|^\W*&", " ", I);
            return Clean(Regex.Replace(text, @"\s*&\s*(?=\d)", " "), 100, letters: true);
        }

        // ------------------------------------------------------------------ the checklist cover sheet

        /// <summary>
        /// The transaction type: the checklist template whose name is the cover sheet's title ("JOB ORDER - …",
        /// "OFFICE, TEACHER and PUPIL SUPPLIES …") or whose documents are the ones listed. Then the tick or NA of each
        /// of its documents, from the boxes on the cover sheet.
        /// </summary>
        private static void Cover(DvScanPages read, IReadOnlyList<Template> templates, DvScanResult result)
        {
            if (templates.Count == 0) return;
            var coverLines = read.CoverLines.Count > 0 ? read.CoverLines.Select(c => c.Text).ToList()
                : read.CoverText.Split('\n').Where(l => l.Trim().Length > 2).ToList();
            if (coverLines.Count == 0) return;

            double NameScore(Template t) => coverLines
                .Select(line => Regex.Replace(line, @"\s*[-–—:]\s.*$|\s*[-–—]\s*$", ""))
                .Where(line => !Regex.IsMatch(line, @"^\W*DV\b", I))
                .Select(line => Math.Max(TitleScore(line, t.Name), Contains(line, t.Name) * 0.9 + (string.Equals(line.Trim(), t.Name, StringComparison.OrdinalIgnoreCase) ? 0.1 : 0)))
                .DefaultIfEmpty(0).Max();
            double ItemScore(Template t) => t.Items.Count < 3 ? 0
                : t.Items.Count(item => coverLines.Any(line => TitleScore(line, item) >= 0.6)) / (double)t.Items.Count;

            var best = templates.Select(t => (Template: t, Score: Math.Max(NameScore(t), ItemScore(t))))
                .OrderByDescending(x => x.Score).First();
            if (best.Score < 0.6) return;
            result.Fields["typeSelect"] = best.Template.Id.ToString();

            // Each document of the checklist: the cover line that names it, and the mark in its box.
            if (read.CoverLines.Count == 0)
            {
                result.Notes.Add($"Transaction type \"{best.Template.Name}\" taken from the cover sheet. Tick its checklist from the cover sheet.");
                return;
            }
            var used = new HashSet<int>();
            foreach (var item in best.Template.Items)
            {
                var match = read.CoverLines.Select((c, i) => (c, i, Score: TitleScore(c.Text, item)))
                    .Where(x => !used.Contains(x.i) && x.Score >= 0.5).OrderByDescending(x => x.Score).FirstOrDefault();
                if (match.c is null) { result.Checklist.Add(new DvScanMark(item, "")); continue; }
                used.Add(match.i);
                result.Checklist.Add(new DvScanMark(item, match.c.Mark));
            }
            var marked = result.Checklist.Count(c => c.Mark != "");
            result.Notes.Add($"Transaction type \"{best.Template.Name}\" taken from the cover sheet; {marked} of its {best.Template.Items.Count} documents ticked or marked NA from the cover sheet's boxes. Please check the ticks.");
        }

        /// <summary>0 … 1: how many words of the name are in the line (spelling slips allowed).</summary>
        private static double Contains(string line, string name)
        {
            static List<string> Words(string s) => Regex.Split(s.ToLowerInvariant().Replace("&", " and "), "[^a-z]+").Where(w => w.Length > 1 && w != "and" && w != "of").ToList();
            var want = Words(name);
            var have = Words(line);
            if (want.Count == 0) return 0;
            return want.Count(w => have.Any(h => h == w || (Math.Min(w.Length, h.Length) >= 4 && (h.StartsWith(w) || w.StartsWith(h))))) / (double)want.Count;
        }

        // ------------------------------------------------------------------ finding values next to their labels

        /// <summary>
        /// The value of a label: the rest of the label's own line, else (rightFirst) the next box to the right on that
        /// row that holds more than a stray mark, else the next line in the same box.
        /// </summary>
        private static string? ValueOf(List<FormLine> lines, string label, bool rightFirst = false, bool wholeCell = false)
        {
            var at = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, label, I));
            if (at is null) return null;
            var rest = Regex.Replace(at.Text, "^.*?" + label.TrimStart('^'), "", I).Trim(' ', ':', '|', '_', '-');
            if (rest.Length >= 2) return rest;

            string? Right()
            {
                var cell = lines.Where(l => l.Row == at.Row && l.Cell > at.Cell).GroupBy(l => l.Cell).OrderBy(g => g.Key)
                    .FirstOrDefault(g => Regex.Matches(string.Join("", g.Select(l => l.Text)), "[A-Za-z0-9]").Count >= 3);
                if (cell is null) return null;
                var texts = cell.OrderBy(l => l.Top).Select(l => l.Text.Trim()).ToList();
                return wholeCell ? JoinLines(texts) : texts.FirstOrDefault();
            }
            string? Below() => lines.Where(l => l.Row == at.Row && l.Cell == at.Cell && l.Line > at.Line).OrderBy(l => l.Line).FirstOrDefault()?.Text.Trim();
            return rightFirst ? Right() ?? Below() : Below() ?? Right();
        }

        /// <summary>The rest of the label's line and the lines under it in the same box (a name running over two lines).</summary>
        private static string? ValueAfter(List<FormLine> lines, FormLine at, string label)
        {
            var rest = Regex.Replace(at.Text, "^.*?" + label, "", I).Trim(' ', ':', '|', '_');
            var below = lines.Where(l => l.Row == at.Row && l.Cell == at.Cell && l.Line > at.Line).OrderBy(l => l.Line)
                .Select(l => l.Text.Trim()).Where(t => !Regex.IsMatch(t, @"^\W*Date\b", I)).Take(2).ToList();
            var parts = (rest.Length >= 2 ? [rest] : new List<string>()).Concat(below).ToList();
            return parts.Count == 0 ? null : JoinLines(parts);
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
            text = Regex.Replace(text, @"\s+", " ").Trim(' ', ':', '-', '.', ',', '&');
            if (text.Length < 2 || (letters && !Regex.IsMatch(text, @"[A-Za-z]{2}")) || Regex.IsMatch(text, @"^N\s*/?\s*A$", I)) return null;
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

        private static void Put(Dictionary<string, string> fields, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) fields[key] = value.Trim();
        }
    }
}
