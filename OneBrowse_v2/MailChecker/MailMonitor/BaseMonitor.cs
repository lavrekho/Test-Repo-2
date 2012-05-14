using System;
using System.Threading;
using System.Collections.Generic;

using OneBrowse;


namespace MailChecker
{
    internal abstract class BaseMonitor
    {
        #region Constants
        
        protected const int LATEST_MESSAGES_NUM = 5;

        private event EventHandler _newMessagesAvailable;

        #endregion

        #region Class Level Variables

        protected int _unseenMessages = 0;

        protected List<MessageInfo> _newMessages = new List<MessageInfo>();

        protected BaseClient _mailClient = null;

        protected int _poolInterval;

        protected Thread _monitorThread;


        private State _currentState = State.Stopped;

        private object _currentStateLock = new object();

        #endregion

        #region Constructors / Destructors

        protected BaseMonitor()
        {
            _newMessagesAvailable += new EventHandler(BaseMonitor__newMessagesAvailable);
        }
        
        #endregion

        #region Properties
        
        public int NewMessagesCount
        {
            get
            {
                return _unseenMessages;
            }
        }

        internal List<MessageInfo> NewMessages
        {
            get
            {
                return _newMessages;
            }
        }

        public MailAccount Account
        {
            get
            {
                return _mailClient.AccountInfo;
            }
            set
            {
                _mailClient.AccountInfo = value;
            }
        }

        /// <summary>
        /// Value in miliseconds
        /// </summary>
        public int PoolInterval
        {
            get
            {
                return _poolInterval;
            }
            set
            {
                _poolInterval = value;
            }
        } 
        
        #endregion

        #region Methods (Public)

        public void Start()
        {
            lock (_currentStateLock)
            {
                if (_currentState != State.Stopped)
                {
                    return;
                }

                _currentState = State.Starting;
            }


            _monitorThread = new Thread(new ThreadStart(PoolMailbox))
                {
                    IsBackground = true
                };

            _monitorThread.Start();


            lock (_currentStateLock)
            {
                _currentState = State.Started;
            }
        }

        public void Stop()
        {
            lock (_currentStateLock)
            {
                if (_currentState != State.Started)
                {
                    return;
                }

                _currentState = State.Stopping;
            }


            try
            {
                _monitorThread.Abort();

                _monitorThread.Join();
            }
            catch { }


            lock (_currentStateLock)
            {
                _currentState = State.Stopped;
            }
        }

        public void ResetNewMessages()
        {
            _unseenMessages = 0;

            _newMessages.Clear();
        }

        #endregion

        #region Methods (Protected)
        
        protected abstract void PoolMailbox();

        protected void OnNewMessagesAvailable()
        {
            if (_newMessagesAvailable != null)
            {
                _newMessagesAvailable(this, EventArgs.Empty);
            };
        }

        #endregion

        #region Methods (Private)
        
        private void BaseMonitor__newMessagesAvailable(object sender, EventArgs e)
        {

        } 
        
        #endregion

        #region Events

        public event EventHandler NewMessagesAvailable
        {
            add
            {
                _newMessagesAvailable += value;
            }
            remove
            {
                _newMessagesAvailable -= value;
            }
        }

        #endregion
    }
}
