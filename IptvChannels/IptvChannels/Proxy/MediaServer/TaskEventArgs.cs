using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class TaskEventArgs : EventArgs
    {
        public TaskEventTypeEnum Type = TaskEventTypeEnum.None;
        public object Tag;
        public string Message;
        public TaskReportTypeEnum ReportType;
    }
}
