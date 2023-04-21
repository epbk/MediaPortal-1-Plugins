using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Net.Http
{
    public enum GetResponseResultEnum
    {
        None = 0,
        OK,
        Abort,
        AbortBeforeDownload,
        AbortPost,
        Error,
        ErrorTimeout,
        ErrorRemoteSocketClosed,
        ErrorInvalidResponse,
        ErrorFailedConnect,
        ErrorInvalidProxy,
        ErrorInvalidHeader,
        ErrorResumeNotAvailable,
        ErrorResumeMismatch,
        ErrorOtherConnectionRequired
    }
}
