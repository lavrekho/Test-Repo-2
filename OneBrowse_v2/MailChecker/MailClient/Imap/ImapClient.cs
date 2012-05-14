using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace MailChecker
{
    public sealed class ImapClient : BaseClient
    {
        #region Constructors / Destructors
        
        public ImapClient()
            : base()
        {
            _noopTimer.Interval = 60000;

            _serverGreeting = "* OK";

            _serverBye = "* BYE";

            _state = (byte)ImapState.NotAuthenticated;

            _cmdTag = new ImapTag("A000");
        }
 
        #endregion

        #region  Methods (Protected)
        
        protected override bool ResetAutoLogoutTimer()
        {
            if (!_connected)
                return false;

            string[] untaggedResponses;
            string taggedResponse;

            ImapResponse result = (ImapResponse)ExecuteCommand("NOOP", 
                ref _cmdTag, 
                out untaggedResponses, 
                out taggedResponse);

            _connected = (result == ImapResponse.OK);

            return _connected;
        }

        protected override byte ExecuteCommand(string protocolCmd, ref ITag tag, out string[] untaggedResponses, out string taggedResponse)
        {
            ImapResponse result = ImapResponse.None;
            untaggedResponses = new string[] { };
            taggedResponse = string.Empty;

            if (!_connected || tag == null)
            {
                return (byte)result;
            }

            lock (_client)
            {
                lock (tag)
                {
                    protocolCmd = tag.Value + " " + protocolCmd + CRLF;

                    Send(protocolCmd);

                    string response = Recv();

                    if (response.Length == 0)
                    {
                        tag.NextValue();

                        return (byte)result;
                    }

                    untaggedResponses = response.Split(new string[] { CRLF }, StringSplitOptions.RemoveEmptyEntries);

                    string lastLine = untaggedResponses[untaggedResponses.Length - 1];

                    if (lastLine.StartsWith(tag.Value + " OK"))
                    {
                        result = ImapResponse.OK;
                        taggedResponse = lastLine.Substring((tag.Value + " OK").Length);
                    }
                    else if (lastLine.StartsWith(tag.Value + " NO"))
                    {
                        result = ImapResponse.NO;
                        taggedResponse = lastLine.Substring((tag.Value + " NO").Length);
                    }
                    else if (lastLine.StartsWith(tag.Value + " BAD"))
                    {
                        result = ImapResponse.BAD;
                        taggedResponse = lastLine.Substring((tag.Value + " BAD").Length);
                    }
                    else
                    {
                        result = ImapResponse.None;
                        taggedResponse = lastLine;
                    }

                    tag.NextValue();
                    Array.Resize<string>(ref untaggedResponses, untaggedResponses.Length - 1);
                }
            }

            return (byte)result;
        }

        protected override string Recv()
        {
            string data = string.Empty;

            if (_cmdTag == null)
                return data;

            MemoryStream recvBuffer = new MemoryStream();
            byte[] buffer = new byte[8096];
            int recvNum;

            try
            {
                do
                {
                    try
                    {
                        recvNum = _stream.Read(buffer, 0, buffer.Length);
                    }
                    catch
                    {
                        recvNum = 0;
                    }

                    recvBuffer.Write(buffer, 0, recvNum);

                    if (recvNum > 0 && recvBuffer.Length >= 2)
                    {
                        byte[] receivedData = null;

                        if (recvNum < 2)
                        {
                            receivedData = recvBuffer.ToArray();

                            recvNum = 2;
                            buffer[0] = receivedData[receivedData.Length - 2];
                            buffer[1] = receivedData[receivedData.Length - 1];
                        }

                        if (buffer[recvNum - 2] == '\r' && buffer[recvNum - 1] == '\n')
                        {
                            if (receivedData == null)
                            {
                                receivedData = recvBuffer.ToArray();
                            }

                            data = Encoding.ASCII.GetString(receivedData);

                            if ((data.StartsWith(_serverGreeting) || data.Contains(CRLF + _cmdTag.Value + " ") || data.StartsWith(_cmdTag.Value + " ")) && data.EndsWith(CRLF))
                            {
                                break;
                            }
                        }
                    }
                }
                while (_client.Client.Poll(1500000, SelectMode.SelectRead));
            }
            catch
            {
                _connected = false;
            }

            return data;
        }

        #endregion

        #region Methods (Public)
        
        public ImapResponse Login()
        {
            string[] untaggedResponses;
            string taggedResponse;

            ImapResponse result = (ImapResponse)ExecuteCommand("LOGIN " + _accountInfo.Login + " " + _accountInfo.Password,
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            if (result == ImapResponse.OK)
            {
                _state = (byte)ImapState.Authenticated;
            }

            return result;
        }

        public ImapResponse List(string arguments, out Mailbox[] mailboxes)
        {
            mailboxes = new Mailbox[] { };


            string[] untaggedResponses;
            string taggedResponse;

            ImapResponse result = (ImapResponse)ExecuteCommand("LIST " + arguments,
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            if (result == ImapResponse.OK)
            {
                Match m;

                List<Mailbox> validMailboxes = new List<Mailbox>();
                Mailbox mailbox;

                foreach (string untaggedResponse in untaggedResponses)
                {
                    m = Regex.Match(untaggedResponse, "^\\* LIST (?<attributes>\\([^\\)]*\\)) \"(?<delimeter>[^\"]*)\" (?<name>[\\w\\W]*)$", RegexOptions.IgnoreCase);

                    if (m.Success)
                    {
                        mailbox = new Mailbox()
                        {
                            NameAttributes = MailboxNameAttributesParser.Parse(m.Groups["attributes"].Value),

                            HierarchyDelimeter = m.Groups["delimeter"].Value,

                            Name = m.Groups["name"].Value
                        };

                        validMailboxes.Add(mailbox);
                    }
                }

                mailboxes = validMailboxes.ToArray();
            }

            return result;
        }

        public ImapResponse Select(string mailboxName, out MailboxStatistic mailboxStatistic)
        {
            mailboxStatistic = null;


            string[] untaggedResponses;
            string taggedResponse;

            ImapResponse result = (ImapResponse)ExecuteCommand("SELECT " + mailboxName,
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            if (result == ImapResponse.OK)
            {
                mailboxStatistic = new MailboxStatistic();

                foreach (string untaggedResponse in untaggedResponses)
                {
                    // "* FLAGS (\Answered \Flagged \Deleted \Seen \Draft)"
                    if (untaggedResponse.StartsWith("* FLAGS ", StringComparison.OrdinalIgnoreCase))
                    {
                        mailboxStatistic.Flags = MailboxFlagsParser.Parse(untaggedResponse.Substring("* FLAGS ".Length));

                        mailboxStatistic.PermanentFlags = mailboxStatistic.Flags;
                    }
                    // "* 172 EXISTS"
                    else if (untaggedResponse.EndsWith("EXISTS", StringComparison.OrdinalIgnoreCase))
                    {
                        int exists = 0;
                        int endInd = untaggedResponse.LastIndexOf(' ');

                        if (endInd > 2)
                        {
                            Int32.TryParse(untaggedResponse.Substring(2, endInd - 2), out exists);
                        }

                        mailboxStatistic.Exists = exists;
                    }
                    // "* 1 RECENT"
                    else if (untaggedResponse.EndsWith("RECENT", StringComparison.OrdinalIgnoreCase))
                    {
                        int recent = 0;
                        int endInd = untaggedResponse.LastIndexOf(' ');

                        if (endInd > 2)
                        {
                            Int32.TryParse(untaggedResponse.Substring(2, endInd - 2), out recent);
                        }

                        mailboxStatistic.Recent = recent;
                    }
                    // * OK [UNSEEN 12] Message 12 is first unseen"
                    else if (untaggedResponse.StartsWith("* OK [UNSEEN ", StringComparison.OrdinalIgnoreCase))
                    {
                        int unseen = 0;

                        int stInd = "* OK [UNSEEN ".Length;
                        int endInd = untaggedResponse.IndexOf("]");

                        if (endInd > stInd)
                        {
                            Int32.TryParse(untaggedResponse.Substring(stInd, endInd - stInd), out unseen);
                        }

                        mailboxStatistic.Unseen = unseen;
                    }
                    // "* OK [PERMANENTFLAGS (\\Answered \\Deleted \\Seen \\Flagged \\Draft $Forwarded \\*)] Limited"
                    else if (untaggedResponse.StartsWith("* OK [PERMANENTFLAGS ", StringComparison.OrdinalIgnoreCase))
                    {
                        int stInd = "* OK [PERMANENTFLAGS ".Length;
                        int endInd = untaggedResponse.IndexOf("]");

                        if (endInd > stInd)
                        {
                            mailboxStatistic.PermanentFlags = MailboxFlagsParser.Parse(untaggedResponse.Substring(stInd, endInd - stInd));
                        }
                    }
                    // "* OK [UIDNEXT 4392] Predicted next UID"
                    else if (untaggedResponse.StartsWith("* OK [UIDNEXT ", StringComparison.OrdinalIgnoreCase))
                    {
                        int uidNext = 0;

                        int stInd = "* OK [UIDNEXT ".Length;
                        int endInd = untaggedResponse.IndexOf("]");

                        if (endInd > stInd)
                        {
                            Int32.TryParse(untaggedResponse.Substring(stInd, endInd - stInd), out uidNext);
                        }

                        mailboxStatistic.UIDNEXT = uidNext;
                    }
                    // * OK [UIDVALIDITY 3857529045] UIDs valid"
                    else if (untaggedResponse.StartsWith("* OK [UIDVALIDITY ", StringComparison.OrdinalIgnoreCase))
                    {
                        int uidValidity = 0;

                        int stInd = "* OK [UIDVALIDITY ".Length;
                        int endInd = untaggedResponse.IndexOf("]");

                        if (endInd > stInd)
                        {
                            Int32.TryParse(untaggedResponse.Substring(stInd, endInd - stInd), out uidValidity);
                        }

                        mailboxStatistic.UIDVALIDITY = uidValidity;
                    }
                }

                mailboxStatistic.Permissions = MailboxPermissionsParser.Parse(taggedResponse);
            }

            return result;
        }

        public ImapResponse Select(Mailbox mailbox, out MailboxStatistic mailboxStatistic)
        {
            return Select(mailbox.Name, out mailboxStatistic);
        }

        public ImapResponse Search(string searchingCriteria, out int[] messageSequenceNumbers)
        {
            messageSequenceNumbers = new int[] { };


            string[] untaggedResponses;
            string taggedResponse;

            ImapResponse result = (ImapResponse)ExecuteCommand("SEARCH " + searchingCriteria,
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            if (result == ImapResponse.OK)
            {
                if (untaggedResponses.Length == 1 && untaggedResponses[0].StartsWith("* SEARCH ", StringComparison.OrdinalIgnoreCase))
                {
                    List<int> seqNumbers = new List<int>();
                    int seqNumber;

                    string[] seqNumbersStr = untaggedResponses[0].Substring("* SEARCH ".Length).Split(new char[] { ' ' });

                    foreach (string seqNumberStr in seqNumbersStr)
                    {
                        if (Int32.TryParse(seqNumberStr, out seqNumber) && seqNumber >= 0)
                        {
                            seqNumbers.Add(seqNumber);
                        }
                    }

                    messageSequenceNumbers = seqNumbers.ToArray();
                }
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequenceSet">single value "x" or values range "x:y"</param>
        /// <param name="messagesInfos"></param>
        /// <returns></returns>
        internal ImapResponse FetchMessageInfo(string sequenceSet, out Dictionary<int, MessageInfo> messagesInfos)
        {
            messagesInfos = null;


            string[] untaggedResponses;
            string taggedResponse;

            ImapResponse result = (ImapResponse)ExecuteCommand("FETCH " + sequenceSet + " (BODY[HEADER.FIELDS (SUBJECT FROM DATE)])",
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            if (result == ImapResponse.OK)
            {
                messagesInfos = new Dictionary<int, MessageInfo>();
                MessageInfo messagesInfo;

                int stMsgNum, endMsgNum;

                if (sequenceSet.Contains(":"))
                {
                    stMsgNum = Int32.Parse(sequenceSet.Substring(0, sequenceSet.IndexOf(':')));
                    endMsgNum = Int32.Parse(sequenceSet.Substring(sequenceSet.IndexOf(':') + 1));
                }
                else
                {
                    stMsgNum = endMsgNum = Int32.Parse(sequenceSet);
                }

                string untaggedResponse;
                int msgNum;
                for (int i = 0, lng = untaggedResponses.Length; i < lng; i++)
                {
                    untaggedResponse = untaggedResponses[i];

                    if (untaggedResponse.StartsWith("* ") && Regex.IsMatch(untaggedResponse, "^* [\\d]+ FETCH", RegexOptions.IgnoreCase))
                    {
                        if (Int32.TryParse(untaggedResponse.Substring(2, untaggedResponse.IndexOf(' ', 2) - 2), out msgNum) && msgNum >= stMsgNum && msgNum <= endMsgNum)
                        {
                            messagesInfo = new MessageInfo();

                            for (i++; i < lng; i++)
                            {
                                untaggedResponse = untaggedResponses[i];

                                if (untaggedResponse.StartsWith("*"))
                                {
                                    i--;
                                    break;
                                }

                                if (untaggedResponse.StartsWith("Subject: ", StringComparison.OrdinalIgnoreCase))
                                {
                                    messagesInfo.Subject = untaggedResponse.Substring("Subject: ".Length);
                                }

                                if (untaggedResponse.StartsWith("From: ", StringComparison.OrdinalIgnoreCase))
                                {
                                    messagesInfo.From = untaggedResponse.Substring("From: ".Length);
                                }

                                if (untaggedResponse.StartsWith("Date: ", StringComparison.OrdinalIgnoreCase))
                                {
                                    // "Date: Fri, 4 May 2012 10:15:53 +0400 (UTC)"
                                    // "Date: Fri, 4 May 2012 10:15:53 +0400 (CDT)"
                                    // "Date: Fri, 4 May 2012 10:15:53 +0400 (MSK)"
                                    
                                    if (Regex.IsMatch(untaggedResponse, "\\(\\w\\w\\w\\)$"))
                                    {
                                        untaggedResponse = untaggedResponse.Substring(0, untaggedResponse.Length - 5);
                                    }

                                    messagesInfo.Date = DateTime.Parse(untaggedResponse.Substring("Date: ".Length));
                                }
                            }

                            if (!messagesInfos.ContainsKey(msgNum))
                            {
                                messagesInfos.Add(msgNum, messagesInfo);
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequenceSet"></param>
        /// <param name="action"></param>
        /// <param name="silent"></param>
        /// <param name="flaglist">String like "(\Deleted \Flagged \Seen)"</param>
        /// <param name="o"></param>
        /// <returns></returns>
        
        // Implemented requests like "<tag> STORE x:y -FLAGS (\Seen)" and last-line (tagged) respone parsing only (in ExecuteCommand())
        public ImapResponse Store(string sequenceSet, Action action, bool silent, string flaglist)
        {
            string[] untaggedResponses;
            string taggedResponse;

            string cmd = "STORE " + sequenceSet + " " + ((action == Action.Add) ? "+" : (action == Action.Delete) ? "-" : "") + "FLAGS" + (silent ? ".SILENT" : "") + " " + flaglist;

            ImapResponse result = (ImapResponse)ExecuteCommand(cmd,
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            return result;
        }

        public ImapResponse Logout()
        {
            string[] untaggedResponses;
            string taggedResponse;

            ImapResponse result = (ImapResponse)ExecuteCommand("LOGOUT",
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            return result;
        } 
        
        #endregion
    }
}