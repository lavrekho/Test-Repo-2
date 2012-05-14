//#define PROFILE


using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace MailChecker
{
    internal class ImapMonitor : BaseMonitor
    {
        #region Class Level Variables
        
        private int _reconnectInterval = 60000; 
        
        #endregion

        #region Constructors / Destructors
        
        public ImapMonitor() : base()
        {
            _mailClient = new ImapClient();
        }
 
        #endregion

        #region Methods (Protected)

        protected override void PoolMailbox()
        {
            ImapClient mailClient = (ImapClient)_mailClient;
            ImapResponse result;
            int unsuccessfulAttempts;

            try
            {
                do
                {
                    if (mailClient.Connect())
                    {
                        if (mailClient.Login() == ImapResponse.OK)
                        {
                            MailboxStatistic mailboxInfo;
                            int[] sequenceSet, sequenceSet1, sequenceSet2, sequenceSet3;
                            int unseenMessages;

                            while (mailClient.Select("INBOX", out mailboxInfo) == ImapResponse.OK)
                            {
                                unseenMessages = 0;
#if PROFILE
                                DateTime stTime = DateTime.Now;
#endif
                                sequenceSet = new int[0];

                                
                                unsuccessfulAttempts = 0;

                                // 1-th method
                                result = mailClient.Search("NEW", out sequenceSet1);

                                if (result == ImapResponse.OK && sequenceSet1.Length > sequenceSet.Length)
                                {
                                    sequenceSet = sequenceSet1;
                                }
                                else if (result != ImapResponse.OK)
                                {
                                    unsuccessfulAttempts++;
                                }

                                // 2-th method
                                result = mailClient.Search("UNSEEN RECENT", out sequenceSet2);

                                if (result == ImapResponse.OK && sequenceSet2.Length > sequenceSet.Length)
                                {
                                    sequenceSet = sequenceSet2;
                                }
                                else if (result != ImapResponse.OK)
                                {
                                    unsuccessfulAttempts++;
                                }

                                // 3-th method
                                result = mailClient.Search("UNSEEN", out sequenceSet3);

                                if (result == ImapResponse.OK && sequenceSet3.Length > sequenceSet.Length)
                                {
                                    sequenceSet = sequenceSet3;
                                }
                                else if (result != ImapResponse.OK)
                                {
                                    unsuccessfulAttempts++;
                                }

                                unseenMessages = sequenceSet.Length;

                                if (unsuccessfulAttempts == 3)
                                    break;


                                _newMessages.Clear();

                                if (unseenMessages > 0)
                                {
                                    Sorter.SortDesc(ref sequenceSet);

                                    if (sequenceSet.Length > LATEST_MESSAGES_NUM)
                                    {
                                        // get 5 latest messages
                                        Array.Resize<int>(ref sequenceSet, LATEST_MESSAGES_NUM);
                                    }

                                    Dictionary<int, MessageInfo> newMessages;
                                    MessageInfo newMessage;

                                    if (sequenceSet.Length > 1 && sequenceSet[0] - sequenceSet[sequenceSet.Length - 1] == sequenceSet.Length - 1)
                                    {
                                        // "x:y"
                                        string set = sequenceSet[sequenceSet.Length - 1].ToString() + ":" + sequenceSet[0].ToString();

                                        if (mailClient.FetchMessageInfo(set, out newMessages) == ImapResponse.OK)
                                        {
                                            mailClient.Store(set, Action.Delete, false, "(\\Seen)");

                                            // merge changes
                                            var enm = newMessages.Keys.GetEnumerator();

                                            while (enm.MoveNext())
                                            {
                                                newMessage = newMessages[enm.Current];
                                                newMessage.AssociatedAccount = _mailClient.AccountInfo;

                                                _newMessages.Add(newMessage);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        foreach (int seqNum in sequenceSet)
                                        {
                                            if (mailClient.FetchMessageInfo(seqNum.ToString(), out newMessages) == ImapResponse.OK)
                                            {
                                                mailClient.Store(seqNum.ToString(), Action.Delete, false, "(\\Seen)");

                                                // merge changes
                                                var enm = newMessages.Keys.GetEnumerator();

                                                while (enm.MoveNext())
                                                {
                                                    newMessage = newMessages[enm.Current];
                                                    newMessage.AssociatedAccount = _mailClient.AccountInfo;

                                                    _newMessages.Add(newMessage);
                                                }
                                            }
                                        }
                                    }
                                }
#if PROFILE
                                Debug.WriteLine("MailChecker.ImapMonitor.Monitor() : total miliseconds: " + DateTime.Now.Subtract(stTime).TotalMilliseconds.ToString());
#endif

                                if (_unseenMessages != unseenMessages)
                                {
                                    _unseenMessages = unseenMessages;

                                    OnNewMessagesAvailable();
                                }

                                Thread.Sleep(_poolInterval);
                            }

                            mailClient.Logout();

                            mailClient.Disconnect();
                        }
                    }

                    Thread.Sleep(_reconnectInterval);
                }
                while (true);
            }
            catch (ThreadAbortException)
            {
                mailClient.Logout();

                mailClient.Disconnect();
            }
        }
        
        #endregion
    }
}
