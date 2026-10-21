using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.EntityFrameworkCore;
using ShopWalletApp.Data;
using ShopWalletApp.Models;

namespace ShopWalletApp
{
    public partial class MainWindow : Window
    {
        private CollectionViewSource _depositViewSource;
        private CollectionViewSource _withdrawViewSource;

        private int _depositPageIndex = 0;
        private int _withdrawPageIndex = 0;
        private const int PageSize = 10;

        public MainWindow()
        {
            InitializeComponent();
            _depositViewSource = new CollectionViewSource();
            _withdrawViewSource = new CollectionViewSource();
            using (var db = new AppDbContext()) db.Database.Migrate();
            RefreshAll();
            Loaded += ShowStartupAlerts;
        }

        private void LoadData()
        {
            using (var db = new AppDbContext())
            {
                _alertThreshold = db.GetAlertThreshold();
                txtAlertThreshold.Text = _alertThreshold.ToString();

                // --- 1. Load Deposits with Grouping ---
                var deposits = db.Transactions.Where(t => t.TransactionType == "Deposit")
                                              .OrderByDescending(t => t.TransactionDate)
                                              .ThenByDescending(t => t.Id)
                                              .ToList();

                var groupedDeposits = deposits.GroupBy(t => t.TransactionDate.Date)
                    .SelectMany(g => g.Select(t => new GroupedTransaction
                    {
                        Transaction = t,
                        DailyTotal = g.Sum(row => row.Amount)
                    })).ToList();

                _depositViewSource.Source = groupedDeposits;
                _depositViewSource.GroupDescriptions.Clear();
                _depositViewSource.GroupDescriptions.Add(new PropertyGroupDescription("DateHeader"));

                // --- 2. Load Withdrawals with Grouping ---
                var withdrawals = db.Transactions.Where(t => t.TransactionType == "Withdraw")
                                                 .OrderByDescending(t => t.TransactionDate)
                                              .ThenByDescending(t => t.Id)
                                                 .ToList();

                var groupedWithdrawals = withdrawals.GroupBy(t => t.TransactionDate.Date)
                    .SelectMany(g => g.Select(t => new GroupedTransaction
                    {
                        Transaction = t,
                        DailyTotal = g.Sum(row => row.Amount)
                    })).ToList();

                _withdrawViewSource.Source = groupedWithdrawals;
                _withdrawViewSource.GroupDescriptions.Clear();
                _withdrawViewSource.GroupDescriptions.Add(new PropertyGroupDescription("DateHeader"));

                // --- 3. Calculate Global Totals ---
                DateTime today = DateTime.Today;
                DateTime startOfMonth = new DateTime(today.Year, today.Month, 1);

                decimal dailyDeposit = deposits.Where(t => t.TransactionDate.Date == today).Sum(t => t.Amount);
                decimal monthlyDeposit = deposits.Where(t => t.TransactionDate >= startOfMonth && t.TransactionDate < startOfMonth.AddMonths(1)).Sum(t => t.Amount);
                decimal dailyWithdraw = withdrawals.Where(t => t.TransactionDate.Date == today).Sum(t => t.Amount);
                decimal monthlyWithdraw = withdrawals.Where(t => t.TransactionDate >= startOfMonth && t.TransactionDate < startOfMonth.AddMonths(1)).Sum(t => t.Amount);
                int dailyCount = db.Transactions.Count(t => t.TransactionDate.Date == today);

                // Update Deposit/Withdraw Tabs Summary
                lblDailyDepositTotal.Text = dailyDeposit.ToString("C");
                lblMonthlyDepositTotal.Text = monthlyDeposit.ToString("C");
                lblDailyWithdrawTotal.Text = dailyWithdraw.ToString("C");
                lblMonthlyWithdrawTotal.Text = monthlyWithdraw.ToString("C");

                // Update Dashboard Summary Cards
                lblDashDailyDeposit.Text = dailyDeposit.ToString("C");
                lblDashMonthlyDeposit.Text = monthlyDeposit.ToString("C");
                lblDashDailyWithdraw.Text = dailyWithdraw.ToString("C");
                lblDashMonthlyWithdraw.Text = monthlyWithdraw.ToString("C");
                lblDashDailyCount.Text = dailyCount.ToString();

                // --- 4. Load Master Numbers & Dashboard Status ---
                var masters = db.MasterNumbers.ToList();
                lblDashMasterCount.Text = masters.Count.ToString();

                var dashboardStatuses = new List<MasterNumberDashboardStatus>();
                var masterStatusesForGrid = new List<MasterNumberStatus>(); // For the Master Numbers tab

                foreach (var master in masters)
                {
                    var usage = db.GetMasterUsage(master.PhoneNumber, today);
                    decimal dailyUsed = usage.Daily;
                    decimal weeklyUsed = usage.Weekly;
                    decimal monthlyUsed = usage.Monthly;

                    // Calculate progress percentage safely (avoid divide by zero)
                    double dailyProgress = CalculatePercentage(dailyUsed, master.DailyLimit);
                    double monthlyProgress = CalculatePercentage(monthlyUsed, master.MonthlyLimit);

                    // For Dashboard
                    dashboardStatuses.Add(new MasterNumberDashboardStatus
                    {
                        PhoneNumber = master.PhoneNumber,
                        AlertThreshold = _alertThreshold,
                        WeeklyRemaining = master.WeeklyLimit - weeklyUsed,
                        DailyTotalTransactions = dailyUsed,
                        DailyLimit = master.DailyLimit,
                        DailyRemaining = master.DailyLimit - dailyUsed,
                        DailyProgress = dailyProgress,
                        MonthlyTotalTransactions = monthlyUsed,
                        MonthlyLimit = master.MonthlyLimit,
                        MonthlyRemaining = master.MonthlyLimit - monthlyUsed,
                        MonthlyProgress = monthlyProgress
                    });

                    masterStatusesForGrid.Add(new MasterNumberStatus
                    {
                        AlertThreshold = _alertThreshold,
                        DailyTotalTransactions = dailyUsed,
                        WeeklyTotalTransactions = weeklyUsed,
                        MonthlyTotalTransactions = monthlyUsed,
                        PhoneNumber = master.PhoneNumber,
                        DailyLimit = master.DailyLimit,
                        DailyRemaining = master.DailyLimit - dailyUsed,
                        WeeklyLimit = master.WeeklyLimit,
                        WeeklyRemaining = master.WeeklyLimit - weeklyUsed,
                        MonthlyLimit = master.MonthlyLimit,
                        MonthlyRemaining = master.MonthlyLimit - monthlyUsed
                    });
                }

                GridDashboardMasterNumbers.ItemsSource = dashboardStatuses;
                GridMasterNumbers.ItemsSource = masterStatusesForGrid;

                cmbDepositSourceNumbers.ItemsSource = masters;
                cmbSourceNumbers.ItemsSource = masters;
                cmbSourceNumbers.DisplayMemberPath = "PhoneNumber";
                cmbSourceNumbers.SelectedValuePath = "PhoneNumber";

                // Apply Pagination
                ApplyPagination(_depositViewSource, GridDeposits, _depositPageIndex, lblDepositPageInfo);
                ApplyPagination(_withdrawViewSource, GridWithdraws, _withdrawPageIndex, lblWithdrawPageInfo);
            }
        }

        private decimal _alertThreshold = 1000m;

        private bool TrySaveTransaction(string type, string customerPhone, string sourcePhone, decimal amount)
        {
            decimal dailyRemaining;
            decimal weeklyRemaining;
            decimal monthlyRemaining;
            decimal threshold;
            try
            {
                using var db = new AppDbContext();
                // Keep the combined limit check and insert atomic across app instances.
                using var save = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
                var master = db.MasterNumbers.SingleOrDefault(m => m.PhoneNumber == sourcePhone);
                if (master == null) { MessageBox.Show("Master Number not found."); return false; }
                DateTime transactionTime = DateTime.Now;
                var usage = db.GetMasterUsage(sourcePhone, transactionTime);
                threshold = db.GetAlertThreshold();
                var periods = new[]
                {
                    (Name: "Daily", Remaining: master.DailyLimit - usage.Daily),
                    (Name: "Weekly", Remaining: master.WeeklyLimit - usage.Weekly),
                    (Name: "Monthly", Remaining: master.MonthlyLimit - usage.Monthly)
                };
                foreach (var period in periods)
                {
                    if (amount > period.Remaining)
                    {
                        MessageBox.Show($"{period.Name} Limit Exceeded for this Master Number! Remaining: {period.Remaining:N2} EGP");
                        return false;
                    }
                }
                var critical = periods.Where(p => p.Remaining - amount <= threshold).ToList();
                if (critical.Count > 0)
                {
                    string warning = string.Join("\n\n", critical.Select(p =>
                        $"WARNING: This transaction will bring the {p.Name} limit to a critical level!\nRemaining after this transaction: {p.Remaining - amount:N2} EGP"));
                    if (MessageBox.Show(warning + "\n\nDo you want to continue?", "Limit Warning",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                        return false;
                }
                db.Transactions.Add(new Transaction
                {
                    PhoneNumber = customerPhone, SourcePhone = sourcePhone,
                    Amount = amount, TransactionType = type, TransactionDate = transactionTime
                });
                db.SaveChanges();
                save.Commit();
                dailyRemaining = periods[0].Remaining - amount;
                weeklyRemaining = periods[1].Remaining - amount;
                monthlyRemaining = periods[2].Remaining - amount;
            }
            catch (DbUpdateException)
            {
                MessageBox.Show("The transaction could not be saved. Refresh and try again.", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                MessageBox.Show("The database could not complete the transaction. Refresh and try again.", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (dailyRemaining <= threshold || weeklyRemaining <= threshold || monthlyRemaining <= threshold)
                MessageBox.Show($"ALERT: Master Number [{sourcePhone}] is close to its limit!\nDaily Remaining: {dailyRemaining:N2} EGP\nWeekly Remaining: {weeklyRemaining:N2} EGP\nMonthly Remaining: {monthlyRemaining:N2} EGP",
                    "Low Limit Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
            return true;
        }

        private void ShowStartupAlerts(object sender, RoutedEventArgs e)
        {
            Loaded -= ShowStartupAlerts;
            var lines = new List<string>();
            foreach (var status in GridMasterNumbers.ItemsSource.Cast<MasterNumberStatus>())
            {
                if (status.DailyRemaining <= _alertThreshold)
                    lines.Add($"- {status.PhoneNumber}: Daily Remaining = {status.DailyRemaining:N2} EGP");
                if (status.WeeklyRemaining <= _alertThreshold)
                    lines.Add($"- {status.PhoneNumber}: Weekly Remaining = {status.WeeklyRemaining:N2} EGP");
                if (status.MonthlyRemaining <= _alertThreshold)
                    lines.Add($"- {status.PhoneNumber}: Monthly Remaining = {status.MonthlyRemaining:N2} EGP");
            }
            if (lines.Count > 0)
                MessageBox.Show("ATTENTION: The following Master Numbers are close to their limits:\n" + string.Join("\n", lines),
                    "Startup Limit Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnSaveAlertThreshold_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtAlertThreshold.Text, out decimal threshold) || threshold < 0m)
            {
                MessageBox.Show("Enter a valid alert threshold of zero or greater.");
                return;
            }
            using (var db = new AppDbContext()) db.SetAlertThreshold(threshold);
            RefreshAll();
            MessageBox.Show("Alert threshold saved successfully.");
        }


        private void RefreshAll() => LoadData();

        private static double CalculatePercentage(decimal used, decimal limit)
        {
            if (limit <= 0m || used <= 0m) return 0d;
            if (used >= limit) return 100d;
            return (double)(used / limit * 100m);
        }

        private static GroupedTransaction? ExtractTransaction(object item)
        {
            if (item is GroupedTransaction transaction) return transaction;
            if (item is CollectionViewGroup group)
                foreach (object child in group.Items)
                {
                    var row = ExtractTransaction(child);
                    if (row != null) return row;
                }
            return null;
        }

        private readonly System.Collections.Generic.Dictionary<CollectionViewSource, FilterEventHandler> _pageFilters = new();

        private void ApplyPagination(CollectionViewSource viewSource, DataGrid grid,
            int pageIndex, TextBlock pageInfo)
        {
            var rows = ((System.Collections.IEnumerable)viewSource.Source)
                .Cast<object>().Select(ExtractTransaction)
                .OfType<GroupedTransaction>().ToList();
            int totalPages = Math.Max(1, (rows.Count + PageSize - 1) / PageSize);
            pageIndex = Math.Clamp(pageIndex, 0, totalPages - 1);
            if (ReferenceEquals(viewSource, _depositViewSource))
                _depositPageIndex = pageIndex;
            else
                _withdrawPageIndex = pageIndex;

            var indexes = rows.Select((row, index) => new { row, index })
                .ToDictionary(x => x.row, x => x.index);
            int start = pageIndex * PageSize;
            if (_pageFilters.TryGetValue(viewSource, out var previousFilter))
                viewSource.Filter -= previousFilter;
            FilterEventHandler filter = (_, e) =>
            {
                var row = ExtractTransaction(e.Item);
                e.Accepted = row != null && indexes.TryGetValue(row, out int index)
                    && index >= start && index < start + PageSize;
            };
            _pageFilters[viewSource] = filter;
            viewSource.Filter += filter;
            viewSource.View.Refresh();
            grid.ItemsSource = viewSource.View;
            pageInfo.Text = $"Page {pageIndex + 1} of {totalPages}";
        }

        private void BtnPrevDeposit_Click(object sender, RoutedEventArgs e)
        {
            if (_depositPageIndex > 0) { _depositPageIndex--; RefreshAll(); }
        }

        private void BtnNextDeposit_Click(object sender, RoutedEventArgs e)
        {
            _depositPageIndex++; RefreshAll();
        }

        private void BtnPrevWithdraw_Click(object sender, RoutedEventArgs e)
        {
            if (_withdrawPageIndex > 0) { _withdrawPageIndex--; RefreshAll(); }
        }

        private void BtnNextWithdraw_Click(object sender, RoutedEventArgs e)
        {
            _withdrawPageIndex++; RefreshAll();
        }

        private void ClearInputs()
        {
            txtDepositPhone.Clear(); txtDepositAmount.Clear();
            txtWithdrawPhone.Clear(); txtWithdrawAmount.Clear();
            txtMasterPhone.Clear(); txtDailyLimit.Clear(); txtWeeklyLimit.Clear(); txtMonthlyLimit.Clear();
            cmbSourceNumbers.SelectedIndex = -1;
            cmbDepositSourceNumbers.SelectedIndex = -1;
        }

        private bool IsValidPhoneNumber(string phone)
        {
            string pattern = @"^(010|011|012|015)\d{8}$";
            return Regex.IsMatch(phone, pattern);
        }

        // ================= DEPOSIT LOGIC =================
        private void BtnDeposit_Click(object sender, RoutedEventArgs e)
        {
            string phone = txtDepositPhone.Text.Trim();
            string? sourcePhone = cmbDepositSourceNumbers.SelectedValue as string;
            if (string.IsNullOrEmpty(sourcePhone)) { MessageBox.Show("Please select a Source Master Number."); return; }
            if (!IsValidPhoneNumber(phone)) { MessageBox.Show("Invalid Phone Number. Must start with 010, 011, 012, or 015."); return; }
            if (!decimal.TryParse(txtDepositAmount.Text, out decimal amount) || amount <= 0m) { MessageBox.Show("Invalid Amount."); return; }

            if (!TrySaveTransaction("Deposit", phone, sourcePhone, amount)) return;
            RefreshAll();
            ClearInputs();
        }

        private void BtnEditDeposit_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var transaction = btn?.Tag as Transaction;
            if (transaction == null) return;

            var dialog = new EditWindow(transaction.Amount.ToString()) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            string input = dialog.NewValue;
            if (decimal.TryParse(input, out decimal newAmount) && newAmount > 0m)
            {
                using (var db = new AppDbContext())
                {
                    var t = db.Transactions.Find(transaction.Id);
                    if (t != null) { t.Amount = newAmount; db.SaveChanges(); }
                }
                RefreshAll();
            }
        }

        private void BtnDeleteDeposit_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var transaction = btn?.Tag as Transaction;
            if (transaction == null) return;

            if (MessageBox.Show("Are you sure you want to delete this deposit?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (var db = new AppDbContext())
                {
                    var t = db.Transactions.Find(transaction.Id);
                    if (t != null) { db.Transactions.Remove(t); db.SaveChanges(); }
                }
                RefreshAll();
            }
        }

        // ================= WITHDRAW LOGIC =================
        private void BtnWithdraw_Click(object sender, RoutedEventArgs e)
        {
            string customerPhone = txtWithdrawPhone.Text.Trim();
            string sourcePhone = cmbSourceNumbers.SelectedValue as string;

            if (string.IsNullOrEmpty(sourcePhone)) { MessageBox.Show("Please select a Source Master Number."); return; }
            if (!IsValidPhoneNumber(customerPhone)) { MessageBox.Show("Invalid Customer Phone Number."); return; }
            if (!decimal.TryParse(txtWithdrawAmount.Text, out decimal amount) || amount <= 0m) { MessageBox.Show("Invalid Amount."); return; }

            if (!TrySaveTransaction("Withdraw", customerPhone, sourcePhone, amount)) return;
            RefreshAll();
            ClearInputs();
        }

        private void BtnEditWithdraw_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var transaction = btn?.Tag as Transaction;
            if (transaction == null) return;

            var dialog = new EditWindow(transaction.Amount.ToString()) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            string input = dialog.NewValue;
            if (decimal.TryParse(input, out decimal newAmount) && newAmount > 0m)
            {
                using (var db = new AppDbContext())
                {
                    var t = db.Transactions.Find(transaction.Id);
                    if (t != null) { t.Amount = newAmount; db.SaveChanges(); }
                }
                RefreshAll();
            }
        }

        private void BtnDeleteWithdraw_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var transaction = btn?.Tag as Transaction;
            if (transaction == null) return;

            if (MessageBox.Show("Are you sure you want to delete this withdrawal?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (var db = new AppDbContext())
                {
                    var t = db.Transactions.Find(transaction.Id);
                    if (t != null) { db.Transactions.Remove(t); db.SaveChanges(); }
                }
                RefreshAll();
            }
        }

        // ================= MASTER NUMBERS LOGIC =================
        private void BtnSaveMaster_Click(object sender, RoutedEventArgs e)
        {
            string phone = txtMasterPhone.Text.Trim();
            if (!IsValidPhoneNumber(phone)) { MessageBox.Show("Invalid Master Phone Number."); return; }
            if (!decimal.TryParse(txtDailyLimit.Text, out decimal daily)) { MessageBox.Show("Invalid Daily Limit."); return; }
            if (!decimal.TryParse(txtWeeklyLimit.Text, out decimal weekly)) { MessageBox.Show("Invalid Weekly Limit."); return; }
            if (!decimal.TryParse(txtMonthlyLimit.Text, out decimal monthly)) { MessageBox.Show("Invalid Monthly Limit."); return; }

            using (var db = new AppDbContext())
            {
                var existing = db.MasterNumbers.FirstOrDefault(m => m.PhoneNumber == phone);
                if (existing != null)
                {
                    existing.DailyLimit = daily; existing.WeeklyLimit = weekly; existing.MonthlyLimit = monthly;
                }
                else
                {
                    db.MasterNumbers.Add(new MasterNumber { PhoneNumber = phone, DailyLimit = daily, WeeklyLimit = weekly, MonthlyLimit = monthly });
                }
                db.SaveChanges();
            }
            MessageBox.Show("Master Number Saved Successfully!");
            RefreshAll();
            ClearInputs();
        }

        private void BtnEditMaster_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var status = btn.Tag as MasterNumberStatus;
            if (status == null) return;

            txtMasterPhone.Text = status.PhoneNumber;
            txtDailyLimit.Text = status.DailyLimit.ToString();
            txtWeeklyLimit.Text = status.WeeklyLimit.ToString();
            txtMonthlyLimit.Text = status.MonthlyLimit.ToString();
        }

        private void BtnDeleteMaster_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var status = btn.Tag as MasterNumberStatus;
            if (status == null) return;

            if (MessageBox.Show("Are you sure you want to delete this Master Number?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (var db = new AppDbContext())
                {
                    var m = db.MasterNumbers.Find(status.PhoneNumber);
                    if (m != null) { db.MasterNumbers.Remove(m); db.SaveChanges(); }
                }
                RefreshAll();
            }
        }
    }
}