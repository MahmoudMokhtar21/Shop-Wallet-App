using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShopWalletApp.Models
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(11)]
        public string PhoneNumber { get; set; } // Customer number

        [StringLength(11)]
        public string SourcePhone { get; set; } // The Master Number used for the transaction

        public decimal Amount { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        public string TransactionType { get; set; } // "Deposit" or "Withdraw"
    }

    public class MasterNumber
    {
        [Key]
        public string PhoneNumber { get; set; } // Shop's source number

        public decimal DailyLimit { get; set; }
        public decimal WeeklyLimit { get; set; }
        public decimal MonthlyLimit { get; set; }
    }

    // Helper class for Pagination and Grouping
    public class GroupedTransaction
    {
        public Transaction Transaction { get; set; } = new Transaction();
        public string DateHeader => Transaction.TransactionDate.ToString("yyyy-MM-dd");
        public int Id => Transaction.Id;
        public string PhoneNumber => Transaction.PhoneNumber;
        public string SourcePhone => Transaction.SourcePhone;
        public decimal Amount => Transaction.Amount;
        public DateTime TransactionDate => Transaction.TransactionDate;
        public decimal DailyTotal { get; set; }

    }

    public class MasterNumberStatus
    {
        public decimal AlertThreshold { get; set; } = 1000m;
        public string DailyAlertLevel => LimitAlerts.Level(DailyRemaining, AlertThreshold);
        public string WeeklyAlertLevel => LimitAlerts.Level(WeeklyRemaining, AlertThreshold);
        public string MonthlyAlertLevel => LimitAlerts.Level(MonthlyRemaining, AlertThreshold);
        public string LowLimitLevel => LimitAlerts.Level(Math.Min(DailyRemaining, Math.Min(WeeklyRemaining, MonthlyRemaining)), AlertThreshold);
        public decimal DailyTotalTransactions { get; set; }
        public decimal WeeklyTotalTransactions { get; set; }
        public decimal MonthlyTotalTransactions { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal DailyLimit { get; set; }
        public decimal DailyRemaining { get; set; }
        public decimal WeeklyLimit { get; set; }
        public decimal WeeklyRemaining { get; set; }
        public decimal MonthlyLimit { get; set; }
        public decimal MonthlyRemaining { get; set; }
    }

    // Helper class for Dashboard Overview
    public class DashboardStats
    {
        public decimal DailyDepositTotal { get; set; }
        public decimal MonthlyDepositTotal { get; set; }
        public decimal DailyWithdrawTotal { get; set; }
        public decimal MonthlyWithdrawTotal { get; set; }
        public int DailyTransactionCount { get; set; }
        public int TotalMasterNumbers { get; set; }
    }

    // Helper class for Master Number Status on Dashboard
    public class MasterNumberDashboardStatus
    {
        public decimal AlertThreshold { get; set; } = 1000m;
        public string DailyAlertLevel => LimitAlerts.Level(DailyRemaining, AlertThreshold);
        public string WeeklyAlertLevel => LimitAlerts.Level(WeeklyRemaining, AlertThreshold);
        public string MonthlyAlertLevel => LimitAlerts.Level(MonthlyRemaining, AlertThreshold);
        public string LowLimitLevel => LimitAlerts.Level(Math.Min(DailyRemaining, Math.Min(WeeklyRemaining, MonthlyRemaining)), AlertThreshold);
        public decimal WeeklyRemaining { get; set; }
        public string PhoneNumber { get; set; }

        // Daily Data
        public decimal DailyTotalTransactions { get; set; }
        public decimal DailyLimit { get; set; }
        public decimal DailyRemaining { get; set; }
        public double DailyProgress { get; set; } // For Progress Bar (0 to 100)

        // Monthly Data
        public decimal MonthlyTotalTransactions { get; set; }
        public decimal MonthlyLimit { get; set; }
        public decimal MonthlyRemaining { get; set; }
        public double MonthlyProgress { get; set; } // For Progress Bar (0 to 100)
    }
    public class AppSettings
    {
        [Key]
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class MasterUsage
    {
        public decimal Daily { get; set; }
        public decimal Weekly { get; set; }
        public decimal Monthly { get; set; }
    }

    public static class LimitAlerts
    {
        public static string Level(decimal remaining, decimal threshold)
        {
            if (remaining <= threshold) return "Critical";
            // Equivalent to remaining <= threshold * 2 without overflow.
            return remaining - threshold <= threshold ? "Warning" : "Safe";
        }
    }

}
