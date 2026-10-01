using System;
using System.Collections.Generic;
using System.Text;

namespace MikroTek.Services.CoreServices
{
    public class CoreServices
    {
        protected DB db;
        protected string _logType { get; set; }
        protected Logger _logger;
        protected long _shopId;
        protected long _parentId;
        protected long _userId;
    }
}
