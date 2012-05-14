using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

using OneBrowse;


namespace MailChecker
{
    internal class AccountsMonitor
    {
        #region Class Level Variables

        private Dictionary<int, BaseMonitor> _id2monitor = new Dictionary<int, BaseMonitor>();
                
        private List<BaseMonitor> _monitors = new List<BaseMonitor>();

        // store unseen (new) messages from all accounts, sorted by date in descent order
        private List<MessageInfo> _messages = new List<MessageInfo>();


        private State _currentState = State.Stopped;

        private object _currentStateLock = new object();

        #endregion

        #region Properties

        // HttpModule
        public List<MailAccount> Accounts
        {
            get
            {
                List<MailAccount> accounts = new List<MailAccount>();

                foreach(BaseMonitor monitor in _monitors)
                {
                    accounts.Add(monitor.Account);
                }

                return accounts;
            }
            set
            {
                //Debug.Assert(_id2monitor.Count == 0 && _monitors.Count == 0);

                foreach (MailAccount account in value)
                {
                    //Debug.Assert(account.ID > 0);

                    UpdateAccount(account);
                }
            }
        }

        // receiveFinished
        public int NewMessagesCount
        {
            get
            {
                return _messages.Count;
            }
        }

        // HttpModule
        public MessageInfo [] NewMessages
        {
            get
            {
                MessageInfo[] messages;

                lock (_messages)
                {
                    messages = new MessageInfo[_messages.Count];

                    _messages.CopyTo(0, messages, 0, _messages.Count);
                }

                return messages;
            }
        }

        #endregion

        #region Methods (Public)

        // HttpModule
        public void UpdateAccount(MailAccount account)
        {
            BaseMonitor monitor = null;


            if (account.ID == -1) // add new account
            {
                account.ID = MailAccount.GetAccountID(account);

                if (account.Protocol == MailProtocols.IMAP)
                {
                    monitor = new ImapMonitor();
                }
                else if (account.Protocol == MailProtocols.POP)
                {
                    monitor = new PopMonitor();
                }

                monitor.PoolInterval = 10000;
                monitor.Account = account;
                monitor.NewMessagesAvailable += monitor_NewMessagesAvailable;


                lock (_id2monitor)
                {
                    if (_id2monitor.ContainsKey(account.ID))
                    {
                        return;
                    }

                    _id2monitor.Add(account.ID, monitor);

                    lock (_monitors)
                    {
                        _monitors.Add(monitor);

                        lock (_currentStateLock)
                        {
                            if (_currentState == State.Started)
                            {
                                monitor.Start();
                            }
                        }
                    }
                }
            }
            else // update an existing account
            {
                Debug.Assert(account.ID > 0);

                RemoveAccount(account.ID); // remove outdated account instance...

                account.ID = -1;

                UpdateAccount(account); // then add new one
            }
        }

        // HttpModule
        public void RemoveAccount(int accountID)
        {
            BaseMonitor monitor = null;

            lock (_id2monitor)
            {
                if (!_id2monitor.ContainsKey(accountID))
                {
                    return;
                }

                monitor = _id2monitor[accountID];

                _id2monitor.Remove(accountID);

                lock (_monitors)
                {
                    _monitors.Remove(monitor);

                    lock (_currentStateLock)
                    {
                        if (_currentState == State.Started)
                        {
                            monitor.Stop();
                        }
                    }
                }
            }

            lock (_messages)
            {
                // remove account-bound messages from global list
                for (int i = 0; i < _messages.Count; i++)//(MessageInfo messageInfo in _messages)
                {
                    if (_messages[i].AssociatedAccount.ID == accountID)
                    {
                        _messages.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        // MailChecker_Enable
        public void StartAccountsMonitoring()
        {
            lock (_monitors) // prevent adding new monitor(s) while starting existing
            {
                lock (_currentStateLock)
                {
                    if (_currentState != State.Stopped)
                    {
                        return;
                    }

                    _currentState = State.Starting;

                    foreach (BaseMonitor monitor in _monitors)
                    {
                        monitor.Start();
                    }

                    _currentState = State.Started;
                }
            }
        }

        // MailChecker_Disable
        public void StopAccountsMonitoring()
        {
            lock (_monitors) // prevent adding new monitor(s) while starting existing
            {
                lock (_currentStateLock)
                {
                    if (_currentState != State.Started)
                    {
                        return;
                    }

                    _currentState = State.Stopping;

                    foreach (BaseMonitor monitor in _monitors)
                    {
                        monitor.Stop();
                    }

                    _currentState = State.Stopped;
                }
            }
        }

        // HttpModule
        public void MarkAllMessagesAsRead(int accountID)
        {
            BaseMonitor monitor;

            lock (_id2monitor)
            {
                if (!_id2monitor.ContainsKey(accountID))
                {
                    return;
                }

                monitor = _id2monitor[accountID];

                lock (_messages)
                {
                    // remove account-bound messages from the global list
                    for (int i = 0; i < _messages.Count; i++)//(MessageInfo messageInfo in _messages)
                    {
                        if (_messages[i].AssociatedAccount.ID == accountID)
                        {
                            _messages.RemoveAt(i);
                            i--;
                        }
                    }

                    monitor.ResetNewMessages();
                }
            }
        }
        
        #endregion

        #region Methods (Private)
        
        private void monitor_NewMessagesAvailable(object sender, EventArgs e)
        {
            BaseMonitor monitor = sender as BaseMonitor;            
            int accountID = monitor.Account.ID;
            bool inserted;


            lock (_messages)
            {
                // add new messages from mailbox to the global list of messages, in date order
                foreach (MessageInfo messageInfo in monitor.NewMessages)
                {
                    if (_messages.Contains(messageInfo))
                        continue;

                    inserted = false;

                    for (int i = 0, lng = _messages.Count; i < lng && !inserted; i++)
                    {
                        if (messageInfo.Date > _messages[i].Date)
                        {
                            _messages.Insert(i, messageInfo); // insert message in date order
                            inserted = true;
                        }
                    }

                    if (!inserted)
                    {
                        _messages.Add(messageInfo); // this is the latest message, so append it to the end of list
                    }
                }

                // remove from the global list read messages
                for (int i = 0; i < _messages.Count; i++)
                {
                    if (accountID == _messages[i].AssociatedAccount.ID && !monitor.NewMessages.Contains(_messages[i]))
                    {
                        _messages.Remove(_messages[i]);
                        i--;
                    }
                }

                // remove extra messages
                if (_messages.Count > 5)
                {
                    _messages.RemoveRange(5, _messages.Count - 5);
                }
            }
        }
 
        #endregion
    }
}