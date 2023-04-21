using System;
using System.Threading;
using MediaPortal.GUI.Library;
using MediaPortal.Dialogs;

namespace MediaPortal.Pbk.Tasks
{
    public class GuiTaskHandler
    {
        public static GuiTaskHandler Instance
        {
            get
            {
                if (_Instance == null)
                    _Instance = new GuiTaskHandler("GuiTaskHandler");

                return _Instance;
            }
        }private static GuiTaskHandler _Instance = null;

        public bool IsBusy { get { return this._Busy != 0; } } private int _Busy = 0;

        private string _Name;
        private Action<bool, object> _CurrentResultHandler = null;
        private object _CurrentResult = null;
        private bool? _CurrentTaskSuccess = null;
        private Exception _CurrentError = null;
        private string _CurrentTaskDescription = null;
        private Thread _BackgroundThread = null;
        private bool _AbortedByUser = false;
        private System.Timers.Timer _WatchDog = new System.Timers.Timer(30 * 1000) { AutoReset = false };

        private static NLog.Logger _Logger = NLog.LogManager.GetCurrentClassLogger();


        # region ctor

        public GuiTaskHandler(string strName)
        {
            this._Name = strName;
            this._WatchDog.Elapsed += this.cbWatchdogElapsed;
        }
        #endregion


        /// <summary>
        /// This method should be used to call methods that might take a few seconds.
        /// It makes sure only on thread at a time executes and has a timeout for the execution.
        /// It also catches Exceptions from the utils and writes errors to the log, and show a message on the GUI.
        /// The Wait Cursor will be shown on while executing the task and the resultHandler will be called on the MPMain thread.
        /// </summary>
        /// <param name="task">method to invoke on a background thread</param>
        /// <param name="resultHandler">method to invoke on the GUI Thread with the result of the task</param>
        /// <param name="strTaskDescription">description of the tak to be invoked - will be shown in the error message if execution fails or times out</param>
        /// <param name="iTimeout">Timeout in seconds. Zero or less: wait forever</param>
        /// <returns>true, if the task could be successfully started in the background</returns>
        public bool ExecuteInBackgroundAndCallback(Func<object> task, Action<bool, object> resultHandler, string strTaskDescription, int iTimeout)
        {
            if (Thread.CurrentThread.ManagedThreadId != 1)
            {
                _Logger.Error("[ExecuteInBackgroundAndCallback] Not called on the MPMain thread - not executing any background action!");
                return false;
            }

            // make sure only one background task can be executed at a time
            if (Interlocked.CompareExchange(ref this._Busy, 1, 0) == 0)
            {
                try
                {
                    this._AbortedByUser = false;
                    this._CurrentResultHandler = resultHandler;
                    this._CurrentTaskDescription = strTaskDescription;
                    this._CurrentResult = null;
                    this._CurrentError = null;
                    this._CurrentTaskSuccess = null;// while this is null the task has not finished (or later on timeouted), true indicates successfull completion and false error
                    GUIWaitCursor.Init();
                    GUIWaitCursor.Show(); // init and show the wait cursor in MediaPortal

                    this._BackgroundThread = new Thread(delegate()
                    {
                        try
                        {
                            this._CurrentResult = task.Invoke();
                            this._CurrentTaskSuccess = true;
                        }
                        catch (ThreadAbortException)
                        {
                            if (!this._AbortedByUser)
                                _Logger.Warn("[ExecuteInBackgroundAndCallback] Timeout waiting for results.");

                            Thread.ResetAbort();
                        }
                        catch (Exception threadException)
                        {
                            this._CurrentError = threadException as Exception;
                            _Logger.Warn(threadException.ToString());
                            this._CurrentTaskSuccess = false;
                        }

                        this._WatchDog.Stop();

                        // hide the wait cursor
                        GUIWaitCursor.Hide();

                        // execute the ResultHandler on the Main Thread
                        GUIWindowManager.SendThreadCallbackAndWait((p1, p2, o) =>
                        {
                            executeTaskResultHandler();
                            return 0;
                        }, 0, 0, null);

                    }) { Name = this._Name + ':' + strTaskDescription, IsBackground = true };

                    // disable timeout when debugging
                    if (iTimeout > 0 && !System.Diagnostics.Debugger.IsAttached)
                    {
                        this._WatchDog.Interval = iTimeout * 1000;
                        this._WatchDog.Start();
                    }

                    this._BackgroundThread.Start();

                    // successfully started the background task
                    return true;
                }
                catch (Exception ex)
                {
                    _Logger.Error(ex);
                    this._CurrentResultHandler = null;
                    GUIWaitCursor.Hide(); // hide the wait cursor
                    this._Busy = 0;
                    return false; // could not start the background task
                }
            }
            else
            {
                _Logger.Error("[ExecuteInBackgroundAndCallback] Another thread tried to execute a task in background.");
                return false;
            }
        }

        public void StopBackgroundTask()
        {
            this.stopBackgroundTask(true);
        }


        private void stopBackgroundTask(bool bByUserRequest)
        {
            if (this.IsBusy && this._CurrentTaskSuccess == null && this._BackgroundThread != null && this._BackgroundThread.IsAlive)
            {
                _Logger.Info("[stopBackgroundTask] Aborting background thread{0}.", bByUserRequest ? " by User Request" : "");
                this._BackgroundThread.Abort();
                this._AbortedByUser = bByUserRequest;
                return;
            }
        }

        private void cbWatchdogElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.stopBackgroundTask(false);
        }

        private void executeTaskResultHandler()
        {
            if (!this.IsBusy)
                return;

            // show an error message if task was not completed successfully
            if (this._CurrentTaskSuccess != true)
            {
                if (this._CurrentError != null)
                {
                    GUIDialogOK dlg = (GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
                    if (dlg != null)
                    {
                        dlg.Reset();
                        dlg.SetHeading(this._Name);
                        //if (_CurrentError.ShowCurrentTaskDescription)
                        {
                            dlg.SetLine(1, string.Format("{0} {1}", "Error", this._CurrentTaskDescription));
                        }
                        dlg.SetLine(2, this._CurrentError.Message);
                        dlg.DoModal(GUIWindowManager.ActiveWindow);
                    }
                }
                else
                {
                    GUIDialogNotify dlg = (GUIDialogNotify)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_NOTIFY);
                    if (dlg != null)
                    {
                        dlg.Reset();
                        //dlg_error.SetImage(...);
                        dlg.SetHeading(this._Name);
                        dlg.SetText(string.Format("{0} {1}", this._CurrentTaskSuccess.HasValue ? "Error" : "Timeout", this._CurrentTaskDescription));

                        if (!this._AbortedByUser)
                            dlg.DoModal(GUIWindowManager.ActiveWindow);
                    }
                }
            }

            // backup the result
            bool bResultSuccess = this._CurrentTaskSuccess == true;
            Action<bool, object> resultHandler = this._CurrentResultHandler;
            object resultObject = this._CurrentResult;

            // clear all fields and allow execution of another background task 
            // before actually executing the result handler -> this way a result handler can also inovke another background task)
            this._CurrentResultHandler = null;
            this._CurrentResult = null;
            this._CurrentTaskSuccess = null;
            this._CurrentError = null;
            this._BackgroundThread = null;
            this._AbortedByUser = false;
            this._WatchDog.Stop();
            this._Busy = 0;

            // execute the result handler
            if (resultHandler != null)
                resultHandler.Invoke(bResultSuccess, resultObject);
        }
    }
}
