using MikroTek.Common;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    
    public abstract class CoreServices
    {
        protected CoreServices(DB db, Logger logger)
        {
            this.db = db;
            _logger = logger;
        }

        protected DB db;
        protected string _logType { get; set; } = "";
        protected Logger _logger;                  
        protected long _userId;
    }
}
