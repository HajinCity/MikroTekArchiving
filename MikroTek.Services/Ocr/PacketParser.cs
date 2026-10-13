using System.Globalization;
using System.Text.RegularExpressions;
using MikroTek.Domain;
using MikroTek.Services.DTO;
using MikroTek.Services.Utils;

namespace MikroTek.Services.Ocr
{
    /// <summary>
    /// Turns the pages read from a MOOE packet into a cash advance and its DVs, and checks the numbers against each other:
    /// the register's lines and taxes, the payments and the running balance, the Alphalist, the totals and the
    /// recapitulation. A misread amount is corrected when the other figures leave no doubt, and is otherwise flagged.
    /// </summary>
    public static class PacketParser
    {
        public record Account(string Code, string Title);
        public record KnownPayee(string Name, string? Tin, string? Address, PayeeTaxType TaxType);

        private const string OfficeSuppliesCode = "5020301000";
        private const decimal Tolerance = 0.05m;

        public static readonly Regex DvNoPattern = new(@"([JS5$])\s*[HN]\s*[S5$]\s*(\d{4})\s*[-–—]\s*(\d{2})\s*[-–—]\s*(\d{3})", RegexOptions.IgnoreCase);
        private static readonly Regex DatePattern = new(@"(\d{1,2})\s*/\s*(\d{1,2})\s*/\s*(\d{4}|\d{2})");

        // ------------------------------------------------------------------ the packet

        public static PacketDraft Parse(List<ReadPage> pages, IReadOnlyList<Account> accounts, IReadOnlyList<KnownPayee> payees)
        {
            var draft = new PacketDraft
            {
                PageCount = pages.Count,
                PageKinds = pages.Select(p => $"{p.Number} {KindName(p.Kind)}").ToList()
            };
            var ca = draft.CashAdvance;
            var register = pages.Where(p => p.Kind == PageKind.Register).OrderBy(p => p.Number).ToList();
            if (register.Count == 0)
            {
                draft.Notes.Add("No Cash Disbursements Register was found in this file, so no DVs could be read. " +
                                "Please check that the register pages are included and scanned straight.");
                return draft;
            }

            // Taxes found on the register whose rate (1/3/5%) could not be read.
            var unknownRates = new Dictionary<PacketVoucher, List<decimal>>(ReferenceEqualityComparer.Instance);
            var recap = ReadRegister(register, accounts, draft, unknownRates);
            var alphalist = ReadAlphalist(pages.Where(p => p.Kind == PageKind.Alphalist));
            ReadHeaders(pages, draft);

            foreach (var dv in draft.Vouchers) Reconcile(dv, alphalist, payees, unknownRates.GetValueOrDefault(dv));
            CheckBalances(draft);
            CheckTotals(draft, recap);

            if (string.IsNullOrWhiteSpace(ca.AdaNo))
                ca.Flags.Add(new("AdaNo", "check", "The ADA No. is handwritten on the MOOE slip. Please type it."));
            if (!string.IsNullOrWhiteSpace(ca.PayrollNo))
                ca.Flags.Add(new("PayrollNo", "check", "Read from the register. Compare it with the Payroll No. on the MOOE slip."));
            return draft;
        }

        private static string KindName(PageKind kind) => kind switch
        {
            PageKind.MooeSlip => "MOOE slip",
            PageKind.Register => "Cash Disbursements Register",
            PageKind.Alphalist => "Alphalist of payees",
            _ => "not used"
        };

        // ------------------------------------------------------------------ the register

        private record RecapEntry(string Code, decimal Amount);

        /// <summary>Reads the DV rows (with their account and tax sub-rows) and the recapitulation.</summary>
        private static List<RecapEntry> ReadRegister(List<ReadPage> pages, IReadOnlyList<Account> accounts, PacketDraft draft,
                                                     Dictionary<PacketVoucher, List<decimal>> unknownRates)
        {
            var ca = draft.CashAdvance;
            var recap = new List<RecapEntry>();
            PacketVoucher? current = null;
            var section = "rows";   // rows → totals → recap

            foreach (var page in pages)
                foreach (var row in page.Rows)
                {
                    string Cell(ColumnRole role) => page.Cell(row, role).Trim();
                    var particulars = Cell(ColumnRole.Particulars);
                    var description = Cell(ColumnRole.AccountDescription);

                    if (section == "rows" && Regex.IsMatch(particulars, @"^\W*Totals?\b", RegexOptions.IgnoreCase))
                    {
                        draft.RegisterTotalPayments = Amount(Cell(ColumnRole.Payment));
                        section = "totals";
                        current = null;
                        continue;
                    }
                    if (Regex.IsMatch(description, @"Recapitulation", RegexOptions.IgnoreCase)) { section = "recap"; continue; }
                    if (section == "recap")
                    {
                        if (Regex.IsMatch(description, @"^\W*Total\b", RegexOptions.IgnoreCase)) { section = "done"; continue; }
                        var code = Digits(Cell(ColumnRole.Code));
                        var amount = Amount(Cell(ColumnRole.Amount));
                        if (code.Length >= 8 && amount is not null && !IsBir(description, code))
                        {
                            var (account, _) = MatchAccount(code, description, accounts);
                            recap.Add(new(account?.Code ?? code, amount.Value));
                        }
                        continue;
                    }
                    if (section != "rows") continue;

                    var docNo = Cell(ColumnRole.DocNo);
                    var dvMatch = DvNoPattern.Match(docNo);
                    var cashAdvance = Amount(Cell(ColumnRole.CashAdvance));

                    if (dvMatch.Success)
                    {
                        current = new PacketVoucher
                        {
                            DvNo = DvNo(dvMatch),
                            CheckNo = CheckNo(docNo[(dvMatch.Index + dvMatch.Length)..]),
                            Date = Date(Cell(ColumnRole.Date)),
                            Payee = Name(particulars),
                            Payment = Amount(Cell(ColumnRole.Payment)),
                            Balance = Amount(Cell(ColumnRole.Balance))
                        };
                        if (current.Date is null) current.Flags.Add(new("Date", "check", "The date could not be read."));
                        if (string.IsNullOrEmpty(current.Payee)) current.Flags.Add(new("Payee", "check", "The payee could not be read."));
                        draft.Vouchers.Add(current);

                        var officeSupplies = Amount(Cell(ColumnRole.OfficeSupplies));
                        if (officeSupplies is > 0)
                        {
                            var account = accounts.FirstOrDefault(a => a.Code == OfficeSuppliesCode)
                                          ?? accounts.OrderByDescending(a => TitleScore("Office Supplies Expenses", a.Title)).FirstOrDefault();
                            current.Lines.Add(new PacketLine
                            {
                                Code = account?.Code ?? OfficeSuppliesCode,
                                Title = account?.Title ?? "Office Supplies Expenses",
                                Amount = officeSupplies,
                                Flag = account is null || account.Code != OfficeSuppliesCode
                                    ? $"{OfficeSuppliesCode} Office Supplies Expenses is not in Setup → UACS Accounts." : null
                            });
                        }
                        AddLine(current, description, Cell(ColumnRole.Code), Cell(ColumnRole.Amount), accounts, unknownRates);
                    }
                    else if (cashAdvance is > 0 && ca.Amount is null)
                    {
                        // The first row: the cash advance received.
                        ca.Amount = cashAdvance;
                        ca.Date = Date(Cell(ColumnRole.Date));
                        ca.PayrollNo = Regex.Replace(docNo, @"\s+", "").ToUpperInvariant();
                        ca.Officer = Name(particulars);
                        current = null;
                    }
                    else if (current is not null)
                    {
                        AddLine(current, description, Cell(ColumnRole.Code), Cell(ColumnRole.Amount), accounts, unknownRates);
                    }
                }
            return recap;
        }

        private static void AddLine(PacketVoucher dv, string description, string codeText, string amountText, IReadOnlyList<Account> accounts,
                                    Dictionary<PacketVoucher, List<decimal>> unknownRates)
        {
            var code = Digits(codeText);
            var amount = Amount(amountText);
            var hasText = Regex.IsMatch(description, @"[A-Za-z]{3}");
            // Specks in an empty row, or words with neither a code nor an amount: nothing to record.
            if (code.Length < 5 && amount is null) return;

            if (IsBir(description, code))
            {
                var value = amount is null ? (decimal?)null : Math.Abs(amount.Value);
                var rate = Regex.Match(description, @"(\d)\s*%");
                switch (rate.Success ? rate.Groups[1].Value : "")
                {
                    case "1": dv.Tax1 = Add(dv.Tax1, value); break;
                    case "3": dv.Tax3 = Add(dv.Tax3, value); break;
                    case "5": dv.Tax5 = Add(dv.Tax5, value); break;
                    default:
                        if (value is not null)
                        {
                            if (!unknownRates.TryGetValue(dv, out var list)) unknownRates[dv] = list = [];
                            list.Add(value.Value);
                        }
                        break;
                }
                return;
            }

            var (account, flag) = MatchAccount(code, description, accounts);
            dv.Lines.Add(new PacketLine
            {
                Code = account?.Code ?? (code.Length > 0 ? code : null),
                Title = account?.Title ?? Regex.Replace(description, @"\s+", " ").Trim(),
                Amount = amount is null ? null : Math.Abs(amount.Value),
                Flag = amount is null ? Join(flag, "The amount could not be read.") : flag
            });
        }

        private static decimal? Add(decimal? a, decimal? b) => a is null ? b : b is null ? a : a + b;

        private static bool IsBir(string description, string code) =>
            code == TaxRules.DueToBirCode || Regex.IsMatch(description, @"\bB\s*[I1l]\s*R\b|Due\s*to\s*B", RegexOptions.IgnoreCase);

        // ------------------------------------------------------------------ the alphalist

        private record AlphaRow(string? Payee, string? Tin, decimal? Gross, decimal? TaxBase, decimal? NonVat1, decimal? NonVat3,
                                decimal? Vat1, decimal? Vat5, decimal? Total);

        private static Dictionary<string, AlphaRow> ReadAlphalist(IEnumerable<ReadPage> pages)
        {
            var result = new Dictionary<string, AlphaRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var page in pages)
                foreach (var row in page.Rows)
                {
                    var match = DvNoPattern.Match(page.Cell(row, ColumnRole.DocNo));
                    if (!match.Success) continue;
                    var tin = Regex.Match(page.Cell(row, ColumnRole.Tin), @"\d{3}\s*-\s*\d{3}\s*-\s*\d{3}(?:\s*-\s*\d{3,5})?");
                    result[DvNo(match)] = new AlphaRow(
                        Name(page.Cell(row, ColumnRole.Payee)),
                        tin.Success ? Regex.Replace(tin.Value, @"\s", "") : null,
                        Abs(Amount(page.Cell(row, ColumnRole.GrossBase))),
                        Abs(Amount(page.Cell(row, ColumnRole.TaxBase))),
                        Abs(Amount(page.Cell(row, ColumnRole.NonVat1))),
                        Abs(Amount(page.Cell(row, ColumnRole.NonVat3))),
                        Abs(Amount(page.Cell(row, ColumnRole.Vat1))),
                        Abs(Amount(page.Cell(row, ColumnRole.Vat5))),
                        Abs(Amount(page.Cell(row, ColumnRole.Total))));
                }
            return result;
        }

        private static decimal? Abs(decimal? value) => value is null ? null : Math.Abs(value.Value);

        // ------------------------------------------------------------------ titles above the tables

        private static void ReadHeaders(List<ReadPage> pages, PacketDraft draft)
        {
            var ca = draft.CashAdvance;
            var register = pages.FirstOrDefault(p => p.Kind == PageKind.Register && p.Header.Length > 0)?.Header ?? "";
            var alphalist = pages.FirstOrDefault(p => p.Kind == PageKind.Alphalist)?.Header ?? "";
            var slip = pages.FirstOrDefault(p => p.Kind == PageKind.MooeSlip)?.Header ?? "";

            string? Find(string text, string pattern)
            {
                var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                return m.Success ? Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim(' ', ':', '-', '_', '.', ',') : null;
            }

            var officer = Find(register, @"Accountable\s*Officer\s*:?\s*([A-Z][A-Z .,'-]+?)\s*$");
            if (!string.IsNullOrWhiteSpace(officer) && officer.Length >= 5) ca.Officer = officer;
            var position = Find(register, @"Designation\s*:?\s*(.+?)\s*$");
            if (!string.IsNullOrWhiteSpace(position)) ca.OfficerPosition = Roman(position);
            ca.FundCluster = Find(register, @"Fund\s*Cluster\s*:?\s*(.+?)(?:\s+Register\b|\s*$)");
            ca.Period = Find(register, @"For\s+the\s+Month\s+of\s*(.+?)(?:\s+(?:SENIOR|JUNIOR)\s+HIGH|\s+Sheet\b|\s*$)")
                        ?? Find(alphalist, @"For\s+the\s+Month\s+of\s*(.+?)(?:\s+(?:SENIOR|JUNIOR)\s+HIGH|\s*$)");
            var verifier = Find(slip, @"^\s*(Administrative\s+Officer.*?)\s*$");
            if (verifier is not null) ca.VerifiedByPosition = Regex.Replace(verifier, @"\s*-\s*", " - ");

            // Department and batch: from the DV numbers (SHS2026-03-037 → SHS, batch 3), else from the titles.
            var numbers = draft.Vouchers.Select(v => v.DvNo ?? "").Where(n => n.Length > 0).ToList();
            if (numbers.Count > 0)
            {
                ca.Dept = numbers.GroupBy(n => n[..3]).OrderByDescending(g => g.Count()).First().Key == "JHS" ? Department.JHS : Department.SHS;
                ca.BatchNo = numbers.Select(n => int.Parse(n.Split('-')[1])).GroupBy(b => b).OrderByDescending(g => g.Count()).First().Key;
                if (ca.BatchNo is < 1 or > 99) ca.BatchNo = 1;
            }
            else
            {
                ca.Dept = Regex.IsMatch(register, @"SENIOR\s+HIGH|-\s*SHS\b", RegexOptions.IgnoreCase) ? Department.SHS : Department.JHS;
                ca.Flags.Add(new("BatchNo", "check", "No DV numbers were read, so the batch number is a guess."));
            }
            if (ca.Amount is null) ca.Flags.Add(new("Amount", "error", "The cash advance amount could not be read from the register."));
            if (ca.Date is null) ca.Flags.Add(new("Date", "error", "The cash advance date could not be read from the register."));
            if (string.IsNullOrWhiteSpace(ca.PayrollNo)) ca.Flags.Add(new("PayrollNo", "error", "The Payroll No. could not be read. Please type it from the MOOE slip."));
        }

        /// <summary>"PRINCIPAL Il" → "PRINCIPAL II" (a scanned roman numeral often comes out with l or 1).</summary>
        private static string Roman(string text) =>
            Regex.Replace(text, @"(?<=\s)([IVXl1|]{1,4})$", m => m.Value.Replace('l', 'I').Replace('1', 'I').Replace('|', 'I'));

        // ------------------------------------------------------------------ one DV against the other figures

        private static void Reconcile(PacketVoucher dv, Dictionary<string, AlphaRow> alphalist, IReadOnlyList<KnownPayee> payees,
                                      List<decimal>? unknown)
        {
            alphalist.TryGetValue(dv.DvNo ?? "", out var alpha);
            var known = payees.FirstOrDefault(p => SameName(p.Name, dv.Payee));
            if (known is null && alpha?.Payee is not null) known = payees.FirstOrDefault(p => SameName(p.Name, alpha.Payee));
            if (string.IsNullOrEmpty(dv.Payee) && alpha?.Payee is not null) dv.Payee = alpha.Payee;
            if (known is not null)
            {
                dv.Payee = known.Name;   // the spelling already in Setup → Payees
                dv.Address = known.Address;
            }
            dv.Tin = alpha?.Tin ?? known?.Tin;

            // Tax type: the Alphalist column it is in, else the taxes on the register, else none withheld.
            if (alpha is not null && (alpha.Vat1 > 0 || alpha.Vat5 > 0)) dv.PayeeTaxType = PayeeTaxType.Vat;
            else if (alpha is not null && (alpha.NonVat1 > 0 || alpha.NonVat3 > 0)) dv.PayeeTaxType = PayeeTaxType.NonVat;
            else if (dv.Tax5 > 0) dv.PayeeTaxType = PayeeTaxType.Vat;
            else if (dv.Tax1 > 0 || dv.Tax3 > 0 || unknown is { Count: > 0 }) dv.PayeeTaxType = known?.TaxType == PayeeTaxType.Vat ? PayeeTaxType.Vat : PayeeTaxType.NonVat;
            else dv.PayeeTaxType = PayeeTaxType.Exempt;

            var exempt = dv.PayeeTaxType == PayeeTaxType.Exempt;
            if (exempt) { dv.Tax1 ??= 0; dv.Tax3 ??= 0; dv.Tax5 ??= 0; }

            // A single missing line amount: what is left of the payment once the taxes and other lines are counted.
            var missing = dv.Lines.Where(l => l.Amount is null).ToList();
            if (missing.Count == 1 && dv.Payment is > 0 && (exempt || (dv.Tax1 is not null && (dv.Tax3 is not null || dv.Tax5 is not null))))
            {
                var rest = dv.Payment.Value + dv.Taxes - dv.Lines.Sum(l => l.Amount ?? 0);
                if (rest > 0)
                {
                    missing[0].Amount = rest;
                    missing[0].Flag = "The amount could not be read; worked out from the payment and the taxes.";
                }
            }
            // No line read at all but the Alphalist has the gross.
            if (dv.Lines.Count == 0)
                dv.Flags.Add(new("Lines", "error", "No account lines were read for this DV. Please add them."));

            var gross = dv.Gross;
            if (alpha?.Gross is > 0 && Math.Abs(alpha.Gross.Value - gross) > Tolerance && dv.Lines.All(l => l.Amount is not null))
            {
                // The register lines do not add up to the Alphalist's gross. If the payment agrees with the Alphalist, a line is misread.
                dv.Flags.Add(new("Lines", "error",
                    $"The account lines add up to {gross:N2} but the Alphalist shows a gross of {alpha.Gross:N2}."));
            }

            // Expected taxes from the tax type, and what the Alphalist says.
            var computed = TaxRules.Compute(gross, dv.PayeeTaxType);
            // Taxes whose rate could not be read: each goes where its size fits.
            if (unknown is not null)
                foreach (var value in unknown)
                {
                    if (dv.Tax1 is null && Math.Abs(value - computed.Tax1) <= Tolerance) dv.Tax1 = value;
                    else if (dv.Tax3 is null && Math.Abs(value - computed.Tax3) <= Tolerance) dv.Tax3 = value;
                    else if (dv.Tax5 is null && Math.Abs(value - computed.Tax5) <= Tolerance) dv.Tax5 = value;
                    else dv.Flags.Add(new("Taxes", "check", $"A Due to BIR amount of {value:N2} was read but its rate (1%, 3% or 5%) was not."));
                }

            var vat = dv.PayeeTaxType == PayeeTaxType.Vat;
            dv.Tax1 = Settle(dv, "Tax1", "1%", dv.Tax1, computed.Tax1, vat ? alpha?.Vat1 : alpha?.NonVat1, gross > 0);
            if (vat)
            {
                dv.Tax5 = Settle(dv, "Tax5", "5%", dv.Tax5, computed.Tax5, alpha?.Vat5, gross > 0);
                if (dv.Tax3 is > 0) dv.Flags.Add(new("Tax3", "check", "A 3% tax was read on a VAT payee. Please check."));
                dv.Tax3 ??= 0;
                dv.TaxBase = alpha?.TaxBase is > 0 && alpha.TaxBase < gross ? alpha.TaxBase : computed.TaxBase;
            }
            else
            {
                dv.Tax3 = Settle(dv, "Tax3", "3%", dv.Tax3, computed.Tax3, alpha?.NonVat3, gross > 0);
                dv.Tax5 ??= 0;
                dv.TaxBase = gross;
            }

            // A tax read wrong the same way on the register and the Alphalist ("(119.76)" as "( 19.76)"):
            // when putting the computed amount back makes gross − taxes equal the payment, that settles it.
            if (dv.Payment is not null && Math.Abs(dv.Payment.Value - dv.Net) > Tolerance)
            {
                var fixes = new[] { ("Tax1", "1%", dv.Tax1, computed.Tax1), ("Tax3", "3%", dv.Tax3, computed.Tax3), ("Tax5", "5%", dv.Tax5, computed.Tax5) }
                    .Where(x => x.Item3 is not null && Math.Abs(x.Item3.Value - x.Item4) > Tolerance
                                && Math.Abs(dv.Net + x.Item3.Value - x.Item4 - dv.Payment.Value) <= Tolerance)
                    .ToList();
                if (fixes.Count == 1)
                {
                    var (field, label, read, value) = fixes[0];
                    if (field == "Tax1") dv.Tax1 = value; else if (field == "Tax3") dv.Tax3 = value; else dv.Tax5 = value;
                    dv.Flags.RemoveAll(f => f.Field == field);
                    dv.Flags.Add(new(field, "check", $"The {label} tax was read as {read:N2}; the payment on the register shows it is {value:N2}."));
                }
            }

            // The payment on the register must be gross − taxes.
            if (dv.Payment is not null && Math.Abs(dv.Payment.Value - dv.Net) > Tolerance)
                dv.Flags.Add(new("Payment", "error",
                    $"Gross {gross:N2} less taxes {dv.Taxes:N2} is {dv.Net:N2}, but the register shows a payment of {dv.Payment:N2}."));

            foreach (var line in dv.Lines.Where(l => l.Flag is not null))
                dv.Flags.Add(new("Lines", "check", $"{line.Code} {line.Title}: {line.Flag}"));
        }

        /// <summary>
        /// Chooses one tax amount from what the register shows (read), the usual computation and the Alphalist.
        /// The register is the bookkeeper's record, so it is kept unless the other two agree with each other against it.
        /// </summary>
        private static decimal? Settle(PacketVoucher dv, string field, string label, decimal? read, decimal computed, decimal? alpha, bool canCompute)
        {
            static bool Same(decimal? a, decimal? b) => a is not null && b is not null && Math.Abs(a.Value - b.Value) <= Tolerance;

            if (read is null)
            {
                if (!canCompute) return null;
                var value = alpha is > 0 ? alpha.Value : computed;
                if (value > 0 || alpha is not null)
                    dv.Flags.Add(new(field, "check", $"The {label} tax could not be read on the register; {(alpha is > 0 ? "taken from the Alphalist" : "computed")}."));
                return value;
            }
            if (Same(read, computed) || Same(read, alpha)) return read;
            if (canCompute && Same(alpha, computed))
            {
                dv.Flags.Add(new(field, "check", $"The {label} tax was read as {read:N2}; the Alphalist and the computation both give {computed:N2}, so that was used."));
                return computed;
            }
            dv.Flags.Add(new(field, "check", $"The {label} tax on the register is {read:N2}; the usual computation gives {computed:N2}."));
            return read;
        }

        // ------------------------------------------------------------------ the running balance and totals

        private static void CheckBalances(PacketDraft draft)
        {
            // Each DV is checked from the balance before it as printed, so one misread does not flag every DV after it.
            var balance = draft.CashAdvance.Amount;
            decimal? printed = balance;
            foreach (var dv in draft.Vouchers)
            {
                if (balance is null) break;
                var from = printed ?? balance.Value;
                var after = from - dv.Net;
                var fits = dv.Balance is not null && Math.Abs(dv.Balance.Value - after) <= Tolerance;
                if (dv.Payment is null)
                {
                    if (fits) dv.Payment = dv.Net;   // the balances before and after show the payment
                    else dv.Flags.Add(new("Payment", "check", "The payment could not be read from the register."));
                }
                if (dv.Balance is not null && !fits && !dv.HasErrors)
                    dv.Flags.Add(new("Balance", "check", $"The register shows a balance of {dv.Balance:N2} after this DV; the amounts give {after:N2}."));
                balance -= dv.Net;
                printed = dv.Balance;
            }
            if (balance is not null && draft.Vouchers.Count > 0)
                draft.Notes.Add(balance.Value == 0
                    ? "The DVs use up the whole cash advance (balance 0.00), as on the register."
                    : $"Balance left on the cash advance after these DVs: {balance:N2}.");
        }

        private static void CheckTotals(PacketDraft draft, List<RecapEntry> recap)
        {
            var paid = draft.Vouchers.Sum(v => v.Net);
            if (draft.RegisterTotalPayments is not null)
                draft.Notes.Add(Math.Abs(draft.RegisterTotalPayments.Value - paid) <= Tolerance
                    ? $"The DVs' payments add up to {paid:N2}, the same as the register's total."
                    : $"The DVs' payments add up to {paid:N2} but the register's total is {draft.RegisterTotalPayments:N2}. Please check the flagged DVs.");

            if (recap.Count == 0) return;
            var byCode = draft.Vouchers.SelectMany(v => v.Lines).Where(l => l.Code is not null)
                .GroupBy(l => l.Code!).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount ?? 0));
            var differences = recap.GroupBy(r => r.Code).Select(g => (Code: g.Key, Recap: g.Sum(r => r.Amount),
                    Read: byCode.GetValueOrDefault(g.Key)))
                .Where(x => Math.Abs(x.Recap - x.Read) > Tolerance).ToList();
            draft.Notes.Add(differences.Count == 0
                ? "Every account total matches the register's recapitulation."
                : "Recapitulation differences: " + string.Join("; ", differences.Select(d => $"{d.Code} read {d.Read:N2}, recapitulation {d.Recap:N2}")));
        }

        // ------------------------------------------------------------------ reading single cells

        /// <summary>
        /// An amount as printed: "3,992.00", "(39.92)" (negative), "162,991 00". Stray marks around it are ignored.
        /// null when no amount with centavos can be found (a part of a number is worse than nothing).
        /// </summary>
        public static decimal? Amount(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = Regex.Replace(text, @"(?<=\d)[Oo]|[Oo](?=\d)", "0");
            t = Regex.Replace(t, @"(?<=\d)[lI]|[lI](?=[\d,.]\d)", "1");
            // "162,991 00": the space stands for the decimal point.
            if (!Regex.IsMatch(t, @"[.,]\s*\d{2}\s*\)?\W*$"))
                t = Regex.Replace(t, @"(\d)\s+(\d{2})(\s*\)?\W*)$", "$1.$2$3");
            t = Regex.Replace(t, @"(?<=[\d,.])\s+(?=[\d,.])", "");

            var matches = Regex.Matches(t, @"\d{1,3}(?:[.,]\d{3})+[.,]\d{2}(?!\d)|\d+[.,]\d{2}(?!\d)");
            if (matches.Count == 0) return null;
            var m = matches.OrderByDescending(x => x.Length).First();
            var digits = Regex.Replace(m.Value, @"[.,]", "");
            var value = decimal.Parse(digits, CultureInfo.InvariantCulture) / 100m;
            var negative = t[..m.Index].Contains('(') || t[(m.Index + m.Length)..].TrimStart().StartsWith(')');
            return negative ? -value : value;
        }

        public static DateTime? Date(string? text)
        {
            var m = DatePattern.Match(text ?? "");
            if (!m.Success) return null;
            int month = int.Parse(m.Groups[1].Value), day = int.Parse(m.Groups[2].Value), year = int.Parse(m.Groups[3].Value);
            if (year < 100) year += 2000;
            if (month is < 1 or > 12 || day < 1 || year is < 2000 or > 2100 || day > DateTime.DaysInMonth(year, month)) return null;
            return new DateTime(year, month, day);
        }

        public static string DvNo(Match m)
        {
            var dept = char.ToUpperInvariant(m.Groups[1].Value[0]) == 'J' ? "JHS" : "SHS";
            return $"{dept}{m.Groups[2].Value}-{m.Groups[3].Value}-{m.Groups[4].Value}";
        }

        /// <summary>The check number written after the DV number ("SHS2026-03-037 0103264037").</summary>
        private static string? CheckNo(string rest)
        {
            var m = Regex.Match(Regex.Replace(rest, @"(?<=\d)[Oo]|[Oo](?=\d)", "0"), @"\d{6,}");
            return m.Success ? m.Value : null;
        }

        public static string Digits(string text) => Regex.Replace(Regex.Replace(text, @"[Oo]", "0"), @"\D", "");

        /// <summary>A payee as printed, without scanning specks; null when it is not a name.</summary>
        public static string? Name(string? text)
        {
            var t = Regex.Replace(text ?? "", @"[^A-Za-z0-9Ññ .,&'()\-/]", " ");
            t = Regex.Replace(t, @"\s+", " ").Trim(' ', '.', ',', '-', '/');
            return Regex.Matches(t, @"[A-Za-zÑñ]").Count >= 4 && Regex.IsMatch(t, @"[A-Za-z]{3}") ? t : null;
        }

        public static bool SameName(string? a, string? b)
        {
            static string Key(string? s) => Regex.Replace((s ?? "").ToUpperInvariant(), @"[^A-Z0-9Ñ]", "");
            var ka = Key(a);
            var kb = Key(b);
            return ka.Length > 3 && (ka == kb || (Math.Max(ka.Length, kb.Length) >= 10 && Levenshtein(ka, kb) <= 2));
        }

        private static string? Join(string? a, string b) => string.IsNullOrEmpty(a) ? b : $"{a} {b}";

        // ------------------------------------------------------------------ UACS accounts

        /// <summary>
        /// The account a register line means. The code is trusted when it is in the chart of accounts and its title agrees;
        /// otherwise the account is found by its title (and a code that is only a digit or two off).
        /// </summary>
        public static (Account? Account, string? Flag) MatchAccount(string code, string title, IReadOnlyList<Account> accounts)
        {
            title = Regex.Replace(title, @"\s+", " ").Trim();
            var exact = accounts.FirstOrDefault(a => a.Code == code);
            var best = accounts.Select(a => (Account: a, Title: TitleScore(title, a.Title), Code: code.Length > 0 ? Levenshtein(code, a.Code) : 99))
                .OrderByDescending(x => x.Title + (x.Code <= 2 ? 0.3 * (3 - x.Code) / 3 : 0))
                .FirstOrDefault();

            if (exact is not null)
            {
                if (title.Length == 0 || TitleScore(title, exact.Title) >= 0.3) return (exact, null);
                if (best.Account is not null && best.Account != exact && best.Title >= 0.75 && best.Code <= 2)
                    return (best.Account, $"UACS code read as {code} ({exact.Title}); the title \"{title}\" fits {best.Account.Code} {best.Account.Title}.");
                return (exact, null);
            }
            if (best.Account is not null && (best.Title >= 0.6 || (best.Code <= 1 && best.Title >= 0.3)))
                return (best.Account, code.Length == 0
                    ? $"No UACS code was read; matched by the title \"{title}\"."
                    : $"UACS code read as {code}; matched to {best.Account.Code} by the title \"{title}\".");
            return (null, code.Length == 0
                ? $"\"{title}\" did not match any account in Setup → UACS Accounts."
                : $"UACS code {code} ({title}) is not in Setup → UACS Accounts.");
        }

        private static readonly HashSet<string> StopWords = ["and", "of", "the", "expense", "expenses", "exp", "for", "to"];

        /// <summary>0 … 1: how alike two account titles are, word by word (spelling slips allowed).</summary>
        public static double TitleScore(string a, string b)
        {
            static List<string> Words(string s) => Regex.Split(s.ToLowerInvariant().Replace("&", " and "), @"[^a-z]+")
                .Where(w => w.Length > 1 && !StopWords.Contains(w)).ToList();
            var wa = Words(a);
            var wb = Words(b);
            if (wa.Count == 0 || wb.Count == 0) return 0;
            var used = new bool[wb.Count];
            var matched = 0;
            foreach (var x in wa)
                for (var j = 0; j < wb.Count; j++)
                {
                    if (used[j] || !SameWord(x, wb[j])) continue;
                    used[j] = true;
                    matched++;
                    break;
                }
            return 2.0 * matched / (wa.Count + wb.Count);
        }

        private static bool SameWord(string a, string b)
        {
            if (a == b) return true;
            if (Math.Min(a.Length, b.Length) >= 4 && (a.StartsWith(b) || b.StartsWith(a))) return true;
            var longest = Math.Max(a.Length, b.Length);
            return longest >= 5 && Levenshtein(a, b) <= (longest >= 8 ? 2 : 1);
        }

        private static int Levenshtein(string a, string b)
        {
            var d = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) d[j] = j;
            for (var i = 1; i <= a.Length; i++)
            {
                var previous = d[0];
                d[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var temp = d[j];
                    d[j] = Math.Min(Math.Min(d[j] + 1, d[j - 1] + 1), previous + (a[i - 1] == b[j - 1] ? 0 : 1));
                    previous = temp;
                }
            }
            return d[b.Length];
        }
    }
}
