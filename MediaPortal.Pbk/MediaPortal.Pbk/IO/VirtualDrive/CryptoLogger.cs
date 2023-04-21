using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.IO.VirtualDrive
{
    public class CryptoLogger : DokanNet.Logging.ILogger
    {
        private NLog.Logger _Logger;

        public CryptoLogger(NLog.Logger logger)
        {
            this._Logger = logger;
        }

        public void Debug(string message, params object[] args)
        {
            this._Logger.Debug(message, args);
        }

        public bool DebugEnabled
        {
            get { return this._Logger != null; }
        }

        public void Error(string message, params object[] args)
        {
            this._Logger.Error(message, args);
        }

        public void Fatal(string message, params object[] args)
        {
            this._Logger.Fatal(message, args);
        }

        public void Info(string message, params object[] args)
        {
            this._Logger.Info(message, args);
        }

        public void Warn(string message, params object[] args)
        {
            this._Logger.Warn(message, args);
        }
    }
}
