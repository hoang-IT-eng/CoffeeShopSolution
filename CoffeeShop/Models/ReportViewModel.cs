// Models/ReportViewModel.cs
using System;
using System.Collections.Generic;

namespace CoffeeShop.Models
{
    public class ReportViewModel
    {
        public IEnumerable<DailySale> DailySales { get; set; }
        public IEnumerable<PaymentMethodSummary> PaymentMethods { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class DailySale
    {
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
    }

    public class PaymentMethodSummary
    {
        public string Method { get; set; }
        public decimal Total { get; set; }
    }
}