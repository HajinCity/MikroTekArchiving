using MikroTek.Domain;

namespace MikroTek.Services.Utils
{
    /// <summary>One feature that can be ticked for a role on the Roles &amp; Access page.</summary>
    public record PermissionInfo(string Key, string Label, string Hint, bool AdminOnly = false);

    public record PermissionGroup(string Name, string Icon, PermissionInfo[] Items);

    /// <summary>
    /// Every feature of the portal that access can be given to.
    /// To add a feature: add its key here, put it in a group below, then guard the page with
    /// [Permission(Permissions.TheKey)]. It then shows on the Roles &amp; Access page by itself.
    /// </summary>
    public static class Permissions
    {
        public const string DashboardView = "dashboard.view";
        public const string DashboardExport = "dashboard.export";
        public const string CashAdvancesView = "cashadvances.view";
        public const string CashAdvancesEncode = "cashadvances.encode";
        public const string VouchersView = "vouchers.view";
        public const string VouchersEncode = "vouchers.encode";
        public const string ReportsView = "reports.view";
        public const string SetupPayees = "setup.payees";
        public const string SetupAccounts = "setup.accounts";
        public const string SetupBudgets = "setup.budgets";
        public const string SetupChecklists = "setup.checklists";
        public const string SetupSchool = "setup.school";
        public const string SettingsUsers = "settings.users";
        public const string SettingsRoles = "settings.roles";

        public static readonly PermissionGroup[] Groups =
        [
            new("Dashboard", "bi-speedometer2",
            [
                new(DashboardView, "View the dashboard", "Fund cards, totals and charts"),
                new(DashboardExport, "Export the dashboard to Excel", "Download what the dashboard shows")
            ]),
            new("Cash Advances", "bi-cash-stack",
            [
                new(CashAdvancesView, "View cash advances", "The list and each cash advance's details"),
                new(CashAdvancesEncode, "Record cash advances", "Add a new cash advance")
            ]),
            new("Disbursement Vouchers", "bi-folder2-open",
            [
                new(VouchersView, "View and print DVs", "Search, open and print archived vouchers"),
                new(VouchersEncode, "Upload, encode and edit DVs", "Scanned packets, manual encoding, checklists and files")
            ]),
            new("Reports", "bi-printer",
            [
                new(ReportsView, "View and print liquidation reports", "Cash Disbursements Register and Alphalist")
            ]),
            new("Setup", "bi-sliders",
            [
                new(SetupPayees, "Manage payees", "Suppliers and other payees"),
                new(SetupAccounts, "Manage UACS accounts", "The account codes used on DVs"),
                new(SetupBudgets, "Set fund budgets", "Yearly budget of each fund"),
                new(SetupChecklists, "Manage checklist templates", "Required documents per type of DV"),
                new(SetupSchool, "Edit school & signatories", "School profile and the names on reports")
            ]),
            new("Settings", "bi-gear",
            [
                new(SettingsUsers, "Manage staff users", "Add, edit, disable accounts and reset passwords", AdminOnly: true),
                new(SettingsRoles, "Manage roles & access", "This page", AdminOnly: true)
            ])
        ];

        public static readonly PermissionInfo[] All = Groups.SelectMany(g => g.Items).ToArray();

        /// <summary>Features only the System Admin can ever have, so nobody else can give themselves more access.</summary>
        public static readonly HashSet<string> AdminOnly = All.Where(p => p.AdminOnly).Select(p => p.Key).ToHashSet();

        /// <summary>Ticking the first automatically ticks the second (you cannot encode what you cannot see).</summary>
        public static readonly Dictionary<string, string> Requires = new()
        {
            [CashAdvancesEncode] = CashAdvancesView,
            [VouchersEncode] = VouchersView,
            [DashboardExport] = DashboardView
        };

        /// <summary>The ticks each role starts with. The admin can change all but the System Admin's.</summary>
        public static string[] DefaultsFor(UserRole role) => role switch
        {
            UserRole.Admin => All.Select(p => p.Key).ToArray(),
            UserRole.Bookkeeper =>
            [
                DashboardView, DashboardExport, CashAdvancesView, CashAdvancesEncode,
                VouchersView, VouchersEncode, ReportsView, SetupPayees, SetupAccounts
            ],
            UserRole.DisbursementOfficer =>
            [
                DashboardView, DashboardExport, CashAdvancesView, CashAdvancesEncode,
                VouchersView, VouchersEncode, ReportsView
            ],
            _ => [DashboardView, DashboardExport, CashAdvancesView, VouchersView, ReportsView]
        };
    }

    /// <summary>The names of the roles as people read them.</summary>
    public static class RoleNames
    {
        /// <summary>Order used on screens: most access first.</summary>
        public static readonly UserRole[] Order =
            [UserRole.Admin, UserRole.Bookkeeper, UserRole.DisbursementOfficer, UserRole.Viewer];

        public static string Display(UserRole role) => role switch
        {
            UserRole.Admin => "System Admin",
            UserRole.DisbursementOfficer => "Disbursement Officer",
            _ => role.ToString()
        };

        public static string Display(string? role) =>
            Enum.TryParse<UserRole>(role, out var r) ? Display(r) : role ?? "";

        public static string Describe(UserRole role) => role switch
        {
            UserRole.Admin => "Everything, including users and access",
            UserRole.Bookkeeper => "Encodes DVs, keeps payees and accounts",
            UserRole.DisbursementOfficer => "Records cash advances and encodes DVs",
            _ => "Search, view and print only"
        };
    }
}
