using System;
using System.Collections.Generic;
using System.Text;

namespace MikroTek.Domain
{
    public class Vouchers
    {
        public long Id { get; set; }
        public string VoucherCode { get; set; }
        public long PlanId { get; set; }
        public string Status { get; set; }
        public string BatchNo { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime CreatedBy { get; set; }
        public DateTime FirstUsedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string MacAddress { get; set; }

    }
}
