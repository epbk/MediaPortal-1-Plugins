using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Messenger
{
    public class MessangerEventArgs: EventArgs
    {
        public IMessage Message;
        public MessangerEventTypeEnum EventType = MessangerEventTypeEnum.None;
    }
}
