using System;
using System.Collections.Generic;
using System.Text;

namespace MikroTek.Domain
{
    public class Users
    {
        public long Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Name { get; set; }
        public DateTime LastName { get; set; }
        public DateTime LastLoginTime { get; set; }
    }
}
