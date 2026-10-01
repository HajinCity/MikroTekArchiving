using System;
using System.Collections.Generic;
using System.Text;

namespace MikroTek.Domain
{
    public class HotspotSessions
    {
        public long Id { get; set; }
        public long VoucherId { get; set; }
        public string MacAddress { get; set; }
        public string IpAddress { get; set; }
        public DateTime LoginAt { get; set; }
        public DateTime LogoutAt { get; set; }
        public string EndReason { get; set; }
    }
}
