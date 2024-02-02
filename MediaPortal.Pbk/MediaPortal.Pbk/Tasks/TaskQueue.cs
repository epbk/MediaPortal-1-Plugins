using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NLog;

namespace MediaPortal.Pbk.Tasks
{
    /// <summary>
    /// Task queue
    /// </summary>
    public class TaskQueue
    {
        #region Constants
        private const int _MAX_CONCURRENT_THREADS = 5;
        #endregion

        #region Types
        private class Task
        {
            private static long _IdCounter = -1;

            public long ID;
            public string Name;
            public bool InProgress = false;
            public TaskActionHandler Action;
            public object StateObject;
            public TaskPriority Priority;
            public int Attempts;
            public TaskCompletation Completation;

            public Task(string strName, TaskActionHandler action, object stateObject, TaskPriority priority, int iAttempts, TaskCompletation completation)
            {
                this.ID = Interlocked.Increment(ref _IdCounter);
                this.Name = strName;
                this.Action = action;
                this.StateObject = stateObject;
                this.Priority = priority;
                this.Completation = completation;

                if (iAttempts < 1)
                    this.Attempts = 1;
                else
                    this.Attempts = iAttempts;
            }
        }
        #endregion

        #region Private fields
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        private List<Task> _TaskQueue = new List<Task>();
        private int _TotalInProgress = 0;

        private int _ID;
        private static int _IdCounter = -1;

        private string _Name = "Queue";

        private ThreadPriority _Priority;

        private TaskThreadInitHandler _TaskThreadInitHandler;
        private TaskThreadDisposeHandler _TaskThreadDisposeHandler;
        #endregion

        #region public Properies
        /// <summary>
        /// Static instance of Task queue
        /// </summary>
        public static TaskQueue Instance
        {
            get
            {
                if (_Instance == null)
                    _Instance = new TaskQueue("TaskQueue");

                return _Instance;
            }
        }private static TaskQueue _Instance = null;

        /// <summary>
        /// Maxium concurrent threads. Default = 5
        /// </summary>
        public int MaxConcurrentThreads
        {
            get
            {
                return this._MaxConcurrentThreads;
            }

            set
            {
                if (value < 1)
                    this._MaxConcurrentThreads = 1;
                else if (value > 100)
                    this._MaxConcurrentThreads = 100;
                else
                    this._MaxConcurrentThreads = value;
            }
        }private int _MaxConcurrentThreads = _MAX_CONCURRENT_THREADS;

        /// <summary>
        /// Enable or disable queue. Default = true.
        /// </summary>
        public bool Run
        {
            set
            {
                if (!this._Run)
                {
                    this._Run = true;
                    this.process(null);
                }
                else
                    this._Run = false;
            }
        }private bool _Run = true;

        /// <summary>
        /// Thread priority
        /// </summary>
        public ThreadPriority Priority
        { get { return this._Priority; } }
        #endregion

        #region ctor
        /// <summary>
        /// Task queue initialization
        /// </summary>
        /// <param name="strName">Name of the queue</param>
        public TaskQueue(string strName)
            : this(strName, null, null, ThreadPriority.Normal)
        {
        }

        /// <summary>
        /// Task queue initialization
        /// </summary>
        /// <param name="strName">Name of the queue</param>
        /// <param name="threadInitHandler">Delegate to initialize thread state object. Called upon creation of the thread.</param>
        /// <param name="threadDisposeHandler">Delegate to dispose thread state object. Called before termination of the thread.</param>
        public TaskQueue(string strName, TaskThreadInitHandler threadInitHandler, TaskThreadDisposeHandler threadDisposeHandler)
            : this(strName, threadInitHandler, threadDisposeHandler, ThreadPriority.Normal)
        {
        }

        /// <summary>
        /// Task queue initialization
        /// </summary>
        /// <param name="strName">Name of the queue</param>
        /// <param name="threadInitHandler">Delegate to initialize thread state object. Called upon creation of the thread.</param>
        /// <param name="threadDisposeHandler">Delegate to dispose thread state object. Called before termination of the thread.</param>
        /// <param name="threadPriority">Thread priority.</param>
        public TaskQueue(string strName, TaskThreadInitHandler threadInitHandler, TaskThreadDisposeHandler threadDisposeHandler, ThreadPriority threadPriority)
        {
            this._ID = Interlocked.Increment(ref _IdCounter);

            if (!string.IsNullOrEmpty(strName))
                this._Name = strName;

            this._TaskThreadInitHandler = threadInitHandler;
            this._TaskThreadDisposeHandler = threadDisposeHandler;

            this._Priority = threadPriority;
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Add new taks to the queue
        /// </summary>
        /// <param name="action">Delegate to call user action</param>
        /// <param name="stateObject">State object passed to the action</param>
        public void Add(TaskActionHandler action, object stateObject)
        {
            this.Add(action, stateObject, null, TaskPriority.Normal, 1);
        }

        /// <summary>
        /// Add new taks to the queue
        /// </summary>
        /// <param name="action">Delegate to call user action</param>
        /// <param name="stateObject">State object passed to the action</param>
        /// <param name="strName">Name of the task. Default = null</param>
        public void Add(TaskActionHandler action, object stateObject, string strName)
        {
            this.Add(action, stateObject, strName, TaskPriority.Normal, 1);
        }

        /// <summary>
        /// Add new taks to the queue
        /// </summary>
        /// <param name="action">Delegate to call user action</param>
        /// <param name="stateObject">State object passed to the action</param>
        /// <param name="priority">Task execution priority. Default = normal</param>
        /// <param name="strName">Name of the task. Default = null</param>
        /// <param name="iAttempts">Task execution attempts. Default = 1</param>
        public void Add(TaskActionHandler action, object stateObject, string strName, TaskPriority priority, int iAttempts)
        {
            this.Add(action, stateObject, strName, TaskPriority.Normal, iAttempts, null);
        }

        /// <summary>
        /// Add new taks to the queue
        /// </summary>
        /// <param name="action">Delegate to call user action</param>
        /// <param name="stateObject">State object passed to the action</param>
        /// <param name="priority">Task execution priority. Default = normal</param>
        /// <param name="strName">Name of the task. Default = null</param>
        /// <param name="iAttempts">Task execution attempts. Default = 1</param>
        /// <param name="completation">Completation instance.</param>
        public void Add(TaskActionHandler action, object stateObject, string strName, TaskPriority priority, int iAttempts, TaskCompletation completation)
        {
            if (action == null || stateObject == null)
                throw new ArgumentNullException();

            Task task = new Task(strName, action, stateObject, priority, iAttempts, completation);

            task.InProgress = true;
            this.process(task);
        }

        /// <summary>
        /// Find existing task
        /// </summary>
        /// <param name="match">Check for existing state object</param>
        /// <returns>Existing state object</returns>
        public object Find(Predicate<object> match)
        {
            lock (this._TaskQueue)
            {
                Task task = this._TaskQueue.Find((t) => match(t.StateObject));

                return task != null ? task.StateObject : null;
            }
        }

        /// <summary>
        /// Find existing task
        /// </summary>
        /// <param name="match">Check for existing state object</param>
        /// <param name="callback">When the check for exisiting task is complete, the callback is executed. Object represents existing state object, i.e. existing task. Entire process is thread safe.</param>
        public void Find(Predicate<object> match, Action<object> callback)
        {
            lock (this._TaskQueue)
            {
                Task task = this._TaskQueue.Find((t) => match(t.StateObject));
                callback(task != null ? task.StateObject : null);
            }
        }

        /// <summary>
        /// Wait for completation of all tasks
        /// </summary>
        /// <param name="iTime">The number of milliseconds to wait, or System.Threading.Timeout. Infinite (-1) to wait indefinitely.</param>
        /// <returns>True if the event was raised before the specified time elapsed.</returns>
        public bool WaitForAll(int iTime)
        {
            DateTime dt = DateTime.Now;
            lock (this._TaskQueue)
            {
                while (true)
                {
                    if (this._TotalInProgress == 0 && this._TaskQueue.Count == 0)
                    {
                        _Logger.Debug("[{0}][WaitForAll] Complete.", this._ID);
                        return true;
                    }

                    int iWait;
                    if (iTime > 0)
                    {
                        iWait = iTime - (int)(DateTime.Now - dt).TotalMilliseconds;
                        if (iWait < 5)
                            return false; //time elapsed
                    }
                    else
                        iWait = -1;

                    //Release the lock and wait
                    if (!Monitor.Wait(this._TaskQueue, iWait))
                        return false; //time elapsed

                    //Event raised. Lock acquired.
                }
            }
        }
        #endregion

        #region Private methods

        private void process(Task taskToStart)
        {
            lock (this._TaskQueue)
            {
                //Completation
                if (taskToStart != null && taskToStart.Completation != null)
                {
                    if (taskToStart.Completation.InProgress <= 0)
                    {
                        taskToStart.Completation.InProgress = 1;
                        taskToStart.Completation.Complete.Reset();
                    }
                    else
                        taskToStart.Completation.InProgress++;
                }

                if (this._Run && (this._TaskQueue.Count > 0 || taskToStart != null) && this._TotalInProgress < this._MaxConcurrentThreads)
                {
                    //New thread
                    Thread t = new Thread(new ParameterizedThreadStart((o) =>
                    {
                        //Thread start
                        string strThreadName = Thread.CurrentThread.Name;
                        Task task = o as Task;
                        _Logger.Debug("[{0}][process][Start][{1}] TaskID:{2} TaskName:'{3}'", this._ID, strThreadName, task.ID, task.Name);

                        task.Attempts--;

                        //Thread state object init
                        object threadStateObject = null;

                        if (this._TaskThreadInitHandler != null)
                        {
                            try { threadStateObject = this._TaskThreadInitHandler(); }
                            catch { }
                        }

                        while (true)
                        {
                            #region Job

                            TaskActionResultEnum result;

                            try
                            {
                                //Task execution
                                result = task.Action(task.StateObject, threadStateObject);
                            }
                            catch (Exception ex)
                            {
                                _Logger.Error("[{0}][process][Error][{1}] TaskID:{2} TaskName:'{3}'\r\nException: {4}",
                                    this._ID, strThreadName, task.ID, task.Name, ex.Message);

                                result = TaskActionResultEnum.Failed;
                            }
                            finally
                            {
                                task.InProgress = false;
                            }
                            #endregion

                            _Logger.Debug("[{0}][process][End][{1}] Result:{2} TaskID:{3} TaskName:'{4}'", this._ID, strThreadName, result, task.ID, task.Name);

                            lock (this._TaskQueue)
                            {
                                //Result
                                if (result == TaskActionResultEnum.Failed && task.Attempts > 0)
                                {
                                    //Put the task back to the queue
                                    this._TaskQueue.Add(task);
                                    _Logger.Debug("[{0}][process][PutBackToQueue] Attempts:{1} TaskID:{2} InQueue:{3} InProgress:{4}",
                                        this._ID, task.Attempts, taskToStart.ID, this._TaskQueue.Count, this._TotalInProgress);
                                }

                                //Completation
                                if (task.Completation != null)
                                {
                                    task.Completation.InProgress--;

                                    if (task.Completation.InProgress <= 0)
                                        task.Completation.Complete.Set();
                                }

                                //Try to pop another task
                                task = null;
                                task = this.popTask();
                                if (task == null)
                                {
                                    //No more in queue

                                    //Thread state object dispose
                                    if (this._TaskThreadDisposeHandler != null)
                                    {
                                        try { this._TaskThreadDisposeHandler(threadStateObject); }
                                        catch { }
                                    }

                                    threadStateObject = null;

                                    this._TotalInProgress--;

                                    _Logger.Debug("[{0}][process][Closed][{1}] InProgress:{2}", this._ID, strThreadName, this._TotalInProgress);

                                    if (this._TotalInProgress == 0 && this._TaskQueue.Count == 0)
                                    {
                                        //Queue empty notification
                                        Monitor.PulseAll(this._TaskQueue);
                                        _Logger.Debug("[{0}][process][Complete]", this._ID);
                                    }

                                    return;
                                }

                                //Continue with another task
                                _Logger.Debug("[{0}][process][Reusing][{1}] TaskID:{2} TaskName:'{3}'", this._ID, strThreadName, task.ID, task.Name);
                            }
                        }
                    }));

                    //Thread name
                    t.Name = this._Name + '.' + this._ID + ".Thread." + this._TotalInProgress;

                    //Thread priority
                    if (this._Priority != ThreadPriority.Normal)
                        t.Priority = this._Priority;

                    //Thread start
                    if (taskToStart != null)
                        t.Start(taskToStart); //Start the requested task
                    else
                        t.Start(this.popTask()); //Pop a task from queue

                    this._TotalInProgress++;
                }
                else if (taskToStart != null)
                {
                    //Put the task to the queue
                    this._TaskQueue.Add(taskToStart);
                    _Logger.Debug("[{0}][process][AddToQueue] TaskID:{1} InQueue:{2} InProgress:{3}",
                        this._ID, taskToStart.ID, this._TaskQueue.Count, this._TotalInProgress);

                    return;
                }

                _Logger.Debug("[{0}][process] InQueue:{1} InProgress:{2}", this._ID, this._TaskQueue.Count, this._TotalInProgress);
            }
        }

        private Task popTask()
        {
            if (!this._Run || this._TaskQueue.Count == 0)
                return null;

            int iIdx = 0;
            Task task = this._TaskQueue[0];
            for (int i = 1; i < this._TaskQueue.Count; i++)
            {
                if (this._TaskQueue[i].Priority > task.Priority)
                {
                    task = this._TaskQueue[i];
                    iIdx = i;
                }
            }

            this._TaskQueue.RemoveAt(iIdx);

            return task;
        }
        #endregion
    }
}
