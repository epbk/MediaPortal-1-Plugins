using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MediaPortal.GUI.Library;
using System.Runtime.CompilerServices;
using NLog;

namespace MediaPortal.Pbk.Messenger
{
    public class MessageHandler : IEnumerable<IMessage>
    {
        #region Constants
        private const int _TIME_MIN_WAIT = 500;
        private const int _TIME_MIN_IDDLE = 8000;
        private const int _TIME_ACTIVE_MESSAGE = 23000;
        private const int _TIME_ACTIVE_MESSAGE_MIN = 3000;
        private const int _TIME_ACTIVE_CHAR_BASE = 200;
        #endregion

        #region Types
        private enum Status { Off, MessageActive, MessageClosing, MessageOffPeriod }
        #endregion

        #region Private fileds
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        private ManualResetEvent _FlagWakeUp = new ManualResetEvent(false);
        private Thread _ThreadMessenger;
        private Status _MessageStatus = Status.Off;
        private List<IMessage> _MessageList = new List<IMessage>();
        private bool _MessengerActive = false;
        private bool _GuiActive = false;

        private string _TagActive, _TagMessageActive, _TagMessage, _TagMessageIn, _TagMessageOut, _TagMessageLogo, _TagMessageLogoIn, _TagMessageLogoOut;
        private string _TagPreffix = "";

        private int _MessageIdx = 0;

        private bool _Terminate = false;
        private bool _Paused = false;
        private bool _IsSleeping = false;

        private int _TimePeriod = 0;

        private MessangerEventArgs _Args = new MessangerEventArgs();

        private int _Id = -1;
        private static int _IdCounter = -1;

        private bool _IsAnyMessageToShow
        {
            get
            {
                return this._MessageList.Count > 0 && this._MessageList.Any(p => !p.MessageRead);
            }
        }

        private bool _IsMessageToShowSingle(IMessage msg)
        {
            if (this._MessageList.Count > 0)
            {
                int iCnt = 0;
                foreach (IMessage m in this._MessageList.Where(p => !p.MessageRead))
                {
                    if (iCnt++ != 0 || msg != m)
                        return false;
                }

                return iCnt == 1;
            }
            else
                return false;
        }

        private int _MessagesToShowCount
        {
            get
            {
                return this._MessageList.Count > 0 ? this._MessageList.Count(p => !p.MessageRead) : 0;
            }
        }

        private IMessage _MessageToShowFirst
        {
            get
            {
                return this._MessageList.Count > 0 ? this._MessageList.FirstOrDefault(p => !p.MessageRead) : null;
            }
        }

        private int getMessagePeriod(IMessage msg)
        {
            //Presentation period
            if (this.TimeMessageActiveAutomatic)
                return Math.Max(_TIME_ACTIVE_MESSAGE_MIN, msg.MessageText.Length * _TIME_ACTIVE_CHAR_BASE);
            else
                return this._TimeMessageActive;
        }

        #endregion

        #region Events
        public event EventHandler MessageEvent;
        #endregion

        #region Public fields
        public int TimeMessageActive
        {
            get
            {
                return this._TimeMessageActive;
            }

            set
            {
                if (value < 1000)
                    this._TimeMessageActive = 100;
                else if (value > 60000)
                    this._TimeMessageActive = 60000;
                else
                    this._TimeMessageActive = value;
            }
        }private int _TimeMessageActive = _TIME_ACTIVE_MESSAGE;

        public bool TimeMessageActiveAutomatic = true;

        /// <summary>
        /// Automacitally set message TTL if not specified [ms]; >0: multiples of default time otherwise disabled
        /// </summary>
        public int AutoSetMessageTtl
        {
            get { return this._AutoSetMessageTtl; }
            set
            {
                if (value != this._AutoSetMessageTtl)
                {
                    this._AutoSetMessageTtl = value;

                    _Logger.Debug("[{0}][AutoSetMessageTtl] Value: {1}", this._Id, value);

                    if (value > 0 && this._IsSleeping)
                        this._FlagWakeUp.Set();
                }
            }
        } private int _AutoSetMessageTtl = -1;
        #endregion

        #region ctor
        static MessageHandler()
        {
            Logging.Log.Init();
        }

        public MessageHandler(string strGuiPrefix)
        {
            this._Id = Interlocked.Increment(ref _IdCounter);

            this._TagPreffix = strGuiPrefix;

            this._TagMessage = strGuiPrefix + ".Messenger.Message";
            this._TagMessageLogo = strGuiPrefix + ".Messenger.MessageLogo";
            this._TagMessageIn = strGuiPrefix + ".Messenger.Message.In";
            this._TagMessageLogoIn = strGuiPrefix + ".Messenger.MessageLogo.In";
            this._TagMessageOut = strGuiPrefix + ".Messenger.Message.Out";
            this._TagMessageLogoOut = strGuiPrefix + ".Messenger.MessageLogo.Out";
            this._TagMessageActive = strGuiPrefix + ".Messenger.MessageActive";
            this._TagActive = strGuiPrefix + ".Messenger.Active";

            _Logger.Debug("[{0}][ctor] Prefix: '{1}'", this._Id, strGuiPrefix);

            this.Clear();
        }
        #endregion

        #region Private methods
        /// <summary>
        /// Process the message
        /// </summary>
        /// <param name="msg">Message to precess</param>
        /// <returns>True if the given message should be shown</returns>
        private bool processMessage(IMessage msg)
        {
            bool bResult;

            //Delete message if needed
            if (msg.ShowNotifyDialogOnly || (msg.DeleteMessageAfterPresentation && msg.MessageTtl == 0))
            {
                //Remove from the list
                this._MessageList.Remove(msg);

                if (this.MessageEvent != null)
                {
                    //Throw event
                    this._Args.Message = msg;
                    this._Args.EventType = MessangerEventTypeEnum.MessageRemove;
                    try { this.MessageEvent(this, this._Args); }
                    catch { }
                }

                return false;  //expired
            }
            else if (msg.MessageTtl == 0 || msg.MessageRead)
                bResult = false; // expired
            else
            {
                //Time To Live preselection
                if (msg.MessageTtl < 0 && this._AutoSetMessageTtl > 0)
                {
                    //Presentation period
                    msg.MessageTtl = this._TimePeriod * this._AutoSetMessageTtl;
                    _Logger.Debug("[{0}][processMessage] Message TTL[{1}] init: {2}", this._Id, msg.MessageTtl, msg.MessageText);
                }

                //Time To Live couner
                if (msg.MessageTtl > 0)
                {
                    if ((msg.MessageTtl -= this._TimePeriod) < 0)
                        msg.MessageTtl = 0;
                }

                //Mark message as read if TTL elapsed 
                if (msg.MessageTtl == 0)
                {
                    msg.MessageRead = true;

                    if (this.MessageEvent != null)
                    {
                        //Throw event
                        this._Args.Message = msg;
                        this._Args.EventType = MessangerEventTypeEnum.MessageRead;
                        try { this.MessageEvent(this, this._Args); }
                        catch { }
                    }

                    _Logger.Debug("[{0}][processMessage] Message marked as Read: {1}", this._Id, msg.MessageText);
                }

                bResult = true; //show the message
            }


            //Increase idx to the next message
            this._MessageIdx++;

            return bResult;
        }

        /// <summary>
        /// Main process thread
        /// </summary>
        private void process()
        {
            IMessage msg = null;
            this._TimePeriod = _TIME_MIN_WAIT;

            while (!this._Terminate)
            {
                this._FlagWakeUp.Reset();

                lock (this._MessageList)
                {
                    if (!this._MessengerActive || this._Paused || this._TimePeriod == 0)
                        this._IsSleeping = true;

                    if (this._Terminate)
                        break; //terminaton
                }

                if (this._IsSleeping)
                {
                    _Logger.Debug("[{0}][process] Sleeping...", this._Id);
                    this._FlagWakeUp.WaitOne();
                }
                else
                {
                    this._TimePeriod = Math.Max(_TIME_MIN_WAIT, this._TimePeriod);
                    this._FlagWakeUp.WaitOne(this._TimePeriod);
                }

                this._IsSleeping = false;

                if (this._Terminate)
                    break; //terminaton

                if (this._Paused)
                    continue;

                //Keep GUI animation
                GUIGraphicsContext.ResetLastActivity();

                lock (this._MessageList)
                {
                    switch (this._MessageStatus)
                    {
                        case Status.Off:
                            if (this._IsAnyMessageToShow)
                                break; //activate new msg
                            else
                            {
                                this._MessengerActive = false; //we are going to sleep
                                this._MessageIdx = 0;
                                continue;
                            }

                        case Status.MessageActive:
                            //Message presentation elapsed
                            if (this._IsMessageToShowSingle(msg))
                            {
                                //Single message; do not rotate; just stay on
                                if (msg.MessageTtl < 0 && this._AutoSetMessageTtl < 1)
                                    this._TimePeriod = 0; //infinite time; go to sleep
                                else
                                {
                                    this._TimePeriod = this.getMessagePeriod(msg);

                                    //Decrease TTL; delete the message if needed
                                    if (!this.processMessage(msg))
                                        goto nxt; //expired
                                }

                                this._MessageIdx = 0;

                                //if (msg.MessageRead)
                                //{
                                //    GUIPropertyManager.SetProperty(this._TagActive, "");
                                //    GUIPropertyManager.SetProperty(this._TagMessageActive, "");
                                //    this._GuiActive = false;
                                //}

                                continue; //continue to show the current message
                            }

                        nxt:
                            if (this._IsAnyMessageToShow)
                            {
                                //Close current message and wait a little
                                this._TimePeriod = _TIME_MIN_WAIT;
                                this._MessageStatus = Status.MessageClosing;
                                GUIPropertyManager.SetProperty(this._TagMessageActive, "");
                                continue;
                            }
                            else
                                goto deactivate; //no more messages


                        case Status.MessageClosing:
                            //Safe period elapsed
                            msg = null;

                            if (!this._IsAnyMessageToShow)
                                goto deactivate; //no more messages
                            else
                                break; //activate new msg

                        default:
                            throw new ArgumentOutOfRangeException("MessageStatus", this._MessageStatus.ToString());
                    }
                }

                //Activate new message
                lock (this._MessageList)
                {
                    do
                    {
                        if (!this._IsAnyMessageToShow)
                            goto deactivate;

                        if (this._MessageIdx >= this._MessageList.Count)
                            this._MessageIdx = 0;

                        //Current message
                        msg = this._MessageList[this._MessageIdx];

                        //Presentation period
                        this._TimePeriod = this.getMessagePeriod(msg);

                    } while (!this.processMessage(msg)); //Decrease TTL; delete the message if needed

                    //Show the message

                    //Throw event
                    if (this.MessageEvent != null)
                    {
                        this._Args.Message = msg;
                        this._Args.EventType = MessangerEventTypeEnum.MessageShow;
                        try { this.MessageEvent(this, this._Args); }
                        catch { }
                    }
                }

                //Tags
                GUIPropertyManager.SetProperty(this._TagMessageLogo, msg.MessageLogo);
                GUIPropertyManager.SetProperty(this._TagMessage, msg.MessageText);

                if (!this._GuiActive)
                {
                    GUIPropertyManager.SetProperty(this._TagActive, "1");
                    this._GuiActive = true;
                }

                GUIPropertyManager.SetProperty(this._TagMessageActive, "1");

                //Rotator
                GUIPropertyManager.SetProperty(this._TagMessageLogoOut, GUIPropertyManager.GetProperty(this._TagMessageLogoIn));
                GUIPropertyManager.SetProperty(this._TagMessageOut, GUIPropertyManager.GetProperty(this._TagMessageIn));
                GUIPropertyManager.SetProperty(this._TagMessageLogoIn, "");
                GUIPropertyManager.SetProperty(this._TagMessageIn, "");
                Thread.Sleep(100);
                GUIPropertyManager.SetProperty(this._TagMessageLogoIn, msg.MessageLogo);
                GUIPropertyManager.SetProperty(this._TagMessageIn, msg.MessageText);


                this._MessageStatus = Status.MessageActive;

                continue;

            deactivate:
                _Logger.Debug("[{0}][process] Deactivated.", this._Id);
                GUIPropertyManager.SetProperty(this._TagActive, "");
                GUIPropertyManager.SetProperty(this._TagMessageActive, "");
                this._TimePeriod = _TIME_MIN_IDDLE;
                this._MessageStatus = Status.Off;
                this._GuiActive = false;
                this._MessageIdx = 0;
                msg = null;

            }

            _Logger.Debug("[{0}][process] Terminated.", this._Id);
        }

        private void init()
        {
            GUIPropertyManager.SetProperty(this._TagMessage, "");
            GUIPropertyManager.SetProperty(this._TagMessageLogo, "");
            GUIPropertyManager.SetProperty(this._TagMessageIn, "");
            GUIPropertyManager.SetProperty(this._TagMessageOut, "");
            GUIPropertyManager.SetProperty(this._TagMessageLogoIn, "");
            GUIPropertyManager.SetProperty(this._TagMessageLogoOut, "");
            GUIPropertyManager.SetProperty(this._TagActive, "");
            GUIPropertyManager.SetProperty(this._TagMessageActive, "");
            this._MessageStatus = Status.Off;
            this._MessengerActive = false;
            this._GuiActive = false;
            this._MessageIdx = 0;
            this._Terminate = false;
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Start the messanger.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start()
        {
            if (this._ThreadMessenger == null)
            {
                _Logger.Debug("[{0}][Start]", this._Id);

                this.init();

                //Activate the messenger if we have some messages
                this._MessengerActive = this._IsAnyMessageToShow;

                //Start main proccess
                this._ThreadMessenger = new Thread(new ThreadStart(() =>
                {
                    this.process();
                    this._ThreadMessenger.Priority = ThreadPriority.AboveNormal;
                }));
                this._ThreadMessenger.Start();
            }
        }

        /// <summary>
        /// Terminate the messanger.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Stop()
        {
            if (this._ThreadMessenger != null)
            {
                _Logger.Debug("[{0}][Stop]", this._Id);

                lock (this._MessageList)
                {
                    this._Terminate = true;
                }
                this._FlagWakeUp.Set();
                this._ThreadMessenger.Join();
                this._ThreadMessenger = null;
            }
        }

        /// <summary>
        /// Pause the messanger.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Pause()
        {
            if (!this._Paused)
            {
                _Logger.Debug("[{0}][Pause]", this._Id);
                this._Paused = true;
            }
        }

        /// <summary>
        /// Resume the paused messanger
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Resume()
        {
            if (this._Paused)
            {
                _Logger.Debug("[{0}][Resume]", this._Id);
                this._Paused = false;
                this._FlagWakeUp.Set();
            }
        }

        /// <summary>
        /// Add new message
        /// </summary>
        /// <param name="message">Message to be added</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddNewMessage(IMessage message)
        {
            if (message == null)
            {
                _Logger.Warn("[{0}][AddNewMessage] Invalid Message.", this._Id);
                return;
            }

            lock (this._MessageList)
            {
                this._MessageList.Add(message);
                if (this._ThreadMessenger != null && !this._Paused && (!this._MessengerActive || this._IsSleeping))
                {
                    _Logger.Debug("[{0}][AddNewMessage] Wake-Up", this._Id);
                    this._MessengerActive = true;
                    this._FlagWakeUp.Set();
                }
            }
        }

        /// <summary>
        /// Remove message
        /// </summary>
        /// <param name="strToken">Message identifier</param>
        /// <returns>Renmoved message</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public IMessage RemoveMessage(string strToken)
        {
            if (string.IsNullOrWhiteSpace(strToken))
                return null;

            lock (this._MessageList)
            {
                IMessage msg = this._MessageList.Find(p => p.MessageToken.Equals(strToken));

                if (msg == null)
                    return null;

                this._MessageList.Remove(msg);

                //Wake up the threed if sleeping, becouse the current message can be the message we are deleting
                if (this._IsSleeping)
                    this._FlagWakeUp.Set();

                return msg;
            }
        }

        /// <summary>
        /// Wake-up the messanger form sleep mode.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void WakeFromSleep()
        {
            lock (this._MessageList)
            {
                if (!this._Paused && this._IsSleeping)
                {
                    this._MessengerActive = true;
                    this._FlagWakeUp.Set();
                }
            }
        }

        /// <summary>
        /// Clear all messages
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            lock (this._MessageList)
            {
                this._MessageList.Clear();
                this.init();
            }
        }

        /// <summary>
        /// Acquire message list lock.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void LockAcquire()
        {
            Monitor.Enter(this._MessageList);
            return;
        }

        /// <summary>
        /// Releases acquired message list lock.
        /// </summary>
        public void LockRelease()
        {
            Monitor.Exit(this._MessageList);
            return;
        }

        /// <summary>
        /// Gets the message at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the message to get.</param>
        /// <returns>The message at the specified index.</returns>
        public IMessage this[int index]
        {
            get
            {
                lock (this._MessageList)
                {
                    return this._MessageList[index];
                }
            }
        }

        /// <summary>
        /// Gets the number of messages actually contained in the list.
        /// </summary>
        public int Count
        {
            get
            {
                return this._MessageList.Count;
            }
        }

        /// <summary>
        /// Searches for an message that matches the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The <see cref="T:System.Predicate`1"/> delegate that defines the conditions of the <see cref="IMessage"/></param>
        /// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, null.</returns>
        public IMessage Find(Predicate<IMessage> match)
        {
            lock (this._MessageList)
            {
                return this._MessageList.Find(match);
            }
        }

        /// <summary>
        /// Searches for an message that matches the conditions defined by the specified predicate and executes callback with the result. Entire process is thread safe: i.e. the lock is released after execution of the callback.
        /// </summary>
        /// <param name="match">The <see cref="T:System.Predicate`1"/> delegate that defines the conditions of the <see cref="IMessage"/></param>
        /// <param name="callback">When the check for exisiting message is complete, the callback is executed.</param>
        public void Find(Predicate<IMessage> match, Action<IMessage> callback)
        {
            lock (this._MessageList)
            {
                callback(this._MessageList.Find(match));
            }
        }

        /// <summary>
        /// Retrieves all the messages that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The <see cref="T:System.Predicate`1"/> delegate that defines the conditions of the <see cref="IMessage"/> to search for.</param>
        /// <returns>A <see cref="T:System.Collections.Generic.List`1"/> containing all the messages that match the conditions defined by the specified predicate, if found; otherwise, an empty <see cref="IMessage"/>.</returns>
        public List<IMessage> GetAll(Predicate<IMessage> match)
        {
            lock (this._MessageList)
            {
                List<IMessage> result = new List<IMessage>();

                this._MessageList.ForEach(msg =>
                {
                    if (match(msg))
                        result.Add(msg);
                });

                return result;
            }
        }

        /// <summary>
        /// Performs the specified action on each <see cref="IMessage"/> of the message list.
        /// </summary>
        /// <param name="action">The <see cref="T:System.Action`1"/> delegate to perform on each <see cref="IMessage"/> of the message list.</param>
        public void ForEach(Action<IMessage> action)
        {
            lock (this._MessageList)
            {
                this._MessageList.ForEach(action);
            }
        }

        /// <summary>
        /// Removes the all the messages that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The <see cref="T:System.Predicate`1"/> delegate that defines the conditions of the messages to remove.</param>
        /// <returns>Returns: The number of messages removed from the message list</returns>
        public int RemoveAll(Predicate<IMessage> match)
        {
            lock (this._MessageList)
            {
                int iCnt = 0;
                this._MessageList.ForEach(msg =>
                {
                    if (match(msg))
                    {
                        this._MessageList.Remove(msg);
                        iCnt++;
                    }
                });

                //Wake up the threed if sleeping, becouse the current message can be the message we are deleting
                if (this._IsSleeping)
                    this._FlagWakeUp.Set();

                return iCnt;
            }
        }

        #endregion

        #region IEnumerable<IMessage>
        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="T:System.Collections.Generic.List`1"/>. Non thread safe.
        /// </summary>
        /// <returns>A <see cref="T:System.Collections.Generic.List.Enumerator`1"/> for the <see cref="T:System.Collections.Generic.List`1"/>.</returns>
        public IEnumerator<IMessage> GetEnumerator()
        {
            return this._MessageList.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
