using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Messenger
{
    public interface IMessage
    {
        string MessageText { get; set; }
        string MessageLogo { get; set; }
        string MessageToken { get; set; }
        int MessageTtl { get; set; }
        bool DeleteMessageAfterPresentation { get; set; }
        bool ShowNotifyDialogOnly { get; set; }
        bool MessageRead { get; set; }
    }
}
