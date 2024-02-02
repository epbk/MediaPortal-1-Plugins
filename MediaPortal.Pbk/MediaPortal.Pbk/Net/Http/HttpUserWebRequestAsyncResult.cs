using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpUserWebRequestAsyncResult : IAsyncResult
    {
        private AsyncCallback _AsyncCallback;
        private object _AsyncState;
        private ManualResetEvent _WaitHandle = new ManualResetEvent(false);
        private int _Completed = 0;
        private bool _CompletedSync = false;
        private object _Result = false;
        private Exception _Ex = null;
        private HttpUserWebRequest _Wr;
        private int _LifeTime;


        public HttpUserWebRequest Request
        {
            get
            {
                return this._Wr;
            }
        }

        public object Result
        {
            get
            {
                return this._Result;
            }
        }

        public Exception Exception
        {
            get { return this._Ex; }
        }

        public int LifeTime
        {
            get
            {
                return this._LifeTime;
            }
        }


        #region IAsyncResult
        public HttpUserWebRequestAsyncResult(HttpUserWebRequest wr, int iLifeTime, AsyncCallback asyncCallback, Object asyncState)
        {
            this._Wr = wr;
            this._LifeTime = iLifeTime;
            this._AsyncCallback = asyncCallback;
            this._AsyncState = asyncState;
        }

        public object AsyncState
        {
            get { return this._AsyncState; }
        }

        public System.Threading.WaitHandle AsyncWaitHandle
        {
            get { return this._WaitHandle; }
        }

        public bool CompletedSynchronously
        {
            get { return this._CompletedSync; }
        }

        public bool IsCompleted
        {
            get { return this._Completed > 0; }
        }
        #endregion

        public void SetComplete(object result, Exception ex)
        {
            if (Interlocked.CompareExchange(ref this._Completed, 1, 0) == 0)
            {
                this._Result = result;
                this._Ex = ex;

                this._WaitHandle.Set();

                if (this._AsyncCallback != null)
                    this._AsyncCallback(this);
            }
        }
    }
}
