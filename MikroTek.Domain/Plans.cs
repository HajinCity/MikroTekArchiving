using System;
using System.Collections.Generic;
using System.Text;

namespace MikroTek.Domain
{
    public class Plans
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int Duration { get; set; }
        public decimal PlanPrice { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        
    }
}
