#define PROFILE


using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace MailChecker
{
    internal class PopMonitor : BaseMonitor
    {
        #region Constructors / Destructors

        public PopMonitor() : base()
        {
            _mailClient = new PopClient();
        }
        
        #endregion

        #region Methods (Protected)

        protected override void PoolMailbox()
        {
            PopClient mailClient = (PopClient)_mailClient;
            PopResponse result;

            _unseenMessages = mailClient.AccountInfo.PopUnseenMessages;

            bool restoredUnseen = (_unseenMessages > 0);

            try
            {
                int unseenMessages;

                do
                {
                    unseenMessages = _unseenMessages;
#if PROFILE
                    DateTime stTime = DateTime.Now;
#endif
                    if (mailClient.Connect())
                    {
                        result = mailClient.User(_mailClient.AccountInfo.Login);

                        if (result == PopResponse.OK)
                        {
                            result = mailClient.Pass(_mailClient.AccountInfo.Password);

                            if (result == PopResponse.OK)
                            {
                                DropListing dropListing;

                                result = mailClient.Stat(out dropListing);

                                if (dropListing != null && dropListing.Messages >= 0)
                                {
                                    /* update total messages counter */
                                    if (dropListing.Messages > _mailClient.AccountInfo.PopTotalMessages)
                                    {
                                        if (unseenMessages > 0)
                                        {
                                            unseenMessages += dropListing.Messages - _mailClient.AccountInfo.PopTotalMessages;
                                        }
                                        else
                                        {
                                            unseenMessages = dropListing.Messages - _mailClient.AccountInfo.PopTotalMessages;
                                        }
                                    }
                                    else if (dropListing.Messages <= _mailClient.AccountInfo.PopTotalMessages)
                                    {
                                        // unseenMessages = 0;
                                        unseenMessages = dropListing.Messages;
                                    }


                                    /* update last 5 messages info */
                                    ScanListing[] scanListing;

                                    result = mailClient.List(-1, out scanListing);

                                    if (result == PopResponse.OK)
                                    {
                                        // sort in DESC order
                                        Array.Reverse(scanListing, 0, scanListing.Length);

                                        // select last 5 messages
                                        Array.Resize<ScanListing>(ref scanListing, (unseenMessages > LATEST_MESSAGES_NUM) ? LATEST_MESSAGES_NUM : unseenMessages);

                                        _newMessages.Clear();

                                        MessageInfo messageInfo;

                                        foreach (ScanListing scanItem in scanListing)
                                        {
                                            if (mailClient.Top(scanItem.MessageID, 0, out messageInfo) == PopResponse.OK)
                                            {
                                                _newMessages.Add(messageInfo);
                                            }
                                        }
                                    }
                                    
                                    
                                    _mailClient.AccountInfo.PopTotalMessages = dropListing.Messages;
                                }
                            }
                        }

#if PROFILE
                        Debug.WriteLine("MailChecker.PopMonitor.Monitor() : total miliseconds: " + DateTime.Now.Subtract(stTime).TotalMilliseconds.ToString());
#endif

                        mailClient.Quit();

                        mailClient.Disconnect();
                    }

                    if (_unseenMessages != unseenMessages || restoredUnseen)
                    {
                        _unseenMessages = unseenMessages;

                        restoredUnseen = false;

                        OnNewMessagesAvailable();
                    }

                    Thread.Sleep(_poolInterval);

                } while (true);
            }
            catch (ThreadAbortException)
            {
                mailClient.AccountInfo.PopUnseenMessages = _unseenMessages;
            }
        }
        
        #endregion
    }
}