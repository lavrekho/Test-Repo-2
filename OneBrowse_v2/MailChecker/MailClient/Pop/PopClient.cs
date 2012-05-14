using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net.Sockets;

namespace MailChecker
{
    public class PopClient : BaseClient
    {
        #region Constructors / Destructors
        
        public PopClient()
            : base()
        {
            _noopTimer.Interval = 15000;

            _serverGreeting = _serverBye = "+OK";

            _state = (byte)PopState.Authorization;

            _cmdTag = null;
        }
 
        #endregion

        #region Methods (Protected)
        
        protected override bool ResetAutoLogoutTimer()
        {
            if (!_connected)
                return false;

            string[] untaggedResponses;
            string taggedResponse;

            PopResponse result = (PopResponse) ExecuteCommand("NOOP", 
                ref _cmdTag,
                out untaggedResponses, 
                out taggedResponse);

            _connected = (result == PopResponse.OK);

            return _connected;
        }

        protected override byte ExecuteCommand(string protocolCmd, ref ITag tag, out string[] untaggedResponses, out string taggedResponse)
        {
            PopResponse result = PopResponse.None;
            untaggedResponses = new string[] { };
            taggedResponse = string.Empty;

            if (!_connected)
            {
                return (byte)result;
            }

            lock (_client)
            {
                protocolCmd += CRLF;

                Send(protocolCmd);

                string response = Recv();

                if (response.Length == 0 )
                {
                    return (byte)result;
                }

                untaggedResponses = response.Split(new string[] { CRLF }, StringSplitOptions.RemoveEmptyEntries);

                string firstLine = untaggedResponses[0];

                if (firstLine.StartsWith("+OK"))
                {
                    taggedResponse = firstLine.Substring(3);

                    if (untaggedResponses.Length > 1)
                    {
                        string lastLine = untaggedResponses[untaggedResponses.Length - 1];

                        if (lastLine == ".")
                        {
                            List<string> temp = new List<string>(untaggedResponses);
                            temp.RemoveAt(0);
                            temp.RemoveAt(temp.Count-1);

                            untaggedResponses = temp.ToArray();

                            result = PopResponse.OK;
                        }
                    }
                    else
                    {
                        result = PopResponse.OK;
                    }
                }
                else if (firstLine.StartsWith("-ERR"))
                {
                    result = PopResponse.ERR;
                    taggedResponse = firstLine.Substring(3);
                }
                else
                {
                    result = PopResponse.None;
                    taggedResponse = firstLine;
                }
            }

            return (byte)result;
        }

        protected override string Recv()
        {
            string data = string.Empty;
            
            MemoryStream recvBuffer = new MemoryStream();
            byte[] buffer = new byte[8096];
            int recvNum;
            bool multiline = false;

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

                    if (recvNum > 0 && !multiline)
                    {
                        data = Encoding.ASCII.GetString(recvBuffer.ToArray());

                        multiline = (data.IndexOf(CRLF) < data.Length - 2);
                    }

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
                            data = Encoding.ASCII.GetString(recvBuffer.ToArray());

                            if (multiline)
                            {
                                if (data.EndsWith(CRLF + "." + CRLF))
                                {
                                    break;
                                }
                            }
                            else if (_client.Connected && _client.Client.Poll(500000, SelectMode.SelectRead) == false)
                            {
                                break;
                            }
                        }
                    }
                }
                while (recvNum > 0);
            }
            catch
            {
                _connected = false;
            }

            return data;
        }

        #endregion

        #region Methods (Public)

        public PopResponse User(string name)
        {
            string[] untaggedResponses;
            string taggedResponse;

            PopResponse result = (PopResponse)ExecuteCommand("USER " + name,
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            return result;
        }

        public PopResponse Pass(string password)
        {
            string[] untaggedResponses;
            string taggedResponse;

            PopResponse result = (PopResponse)ExecuteCommand("PASS " + password,
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            if (result == PopResponse.OK)
            {
                _state = (byte) PopState.Transaction;
            }

            return result;
        }

        public PopResponse Stat(out DropListing dropListing)
        {
            dropListing = null;


            string[] untaggedResponses;
            string taggedResponse;

            PopResponse result = (PopResponse)ExecuteCommand("STAT",
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            if (result == PopResponse.OK)
            {
                dropListing = new DropListing();

                Match m = Regex.Match(taggedResponse, "^ (?<messages>[\\d]*) (?<size>[\\d]*)$");

                if (m.Success)
                {
                    dropListing.Messages = Int32.Parse(m.Groups["messages"].Value);
                    dropListing.MaildropSize = Int32.Parse(m.Groups["size"].Value);
                }
            }

            return result;
        }

        public PopResponse List(int msg, out ScanListing [] scanListing)
        {
            scanListing = null;


            string[] untaggedResponses;
            string taggedResponse;

            PopResponse result = (PopResponse)ExecuteCommand("LIST" + ((msg > 0) ? " " + msg.ToString() : ""),
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            if (result == PopResponse.OK)
            {
                List<ScanListing> temp = new List<ScanListing>();

                ScanListing messageInfo;

                foreach (string untaggedResponse in untaggedResponses)
                {
                    messageInfo = new ScanListing()
                    {
                        MessageID = Int32.Parse(untaggedResponse.Substring(0, untaggedResponse.IndexOf(' '))),

                        MessageSize = Int32.Parse(untaggedResponse.Substring(untaggedResponse.IndexOf(' ') + 1))
                    };

                    temp.Add(messageInfo);
                }

                scanListing = temp.ToArray();
            }

            return result;
        }

        internal PopResponse Top(int msg, int bodyLines, out MessageInfo messageInfo)
        {
            messageInfo = null;


            string[] untaggedResponses;
            string taggedResponse;

            PopResponse result = (PopResponse)ExecuteCommand("TOP " + msg.ToString() + " " + bodyLines.ToString(),
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            if (result == PopResponse.OK)
            {
                messageInfo = new MessageInfo();

                foreach (string untaggedResponse in untaggedResponses)
                {
                    if (untaggedResponse.StartsWith("From: ", StringComparison.OrdinalIgnoreCase))
                    {
                        messageInfo.From = untaggedResponse.Substring("From: ".Length);
                    }

                    if (untaggedResponse.StartsWith("Subject: ", StringComparison.OrdinalIgnoreCase))
                    {
                        messageInfo.Subject = untaggedResponse.Substring("Subject: ".Length);
                    }

                    if (untaggedResponse.StartsWith("Date: ", StringComparison.OrdinalIgnoreCase))
                    {
                        messageInfo.Date = DateTime.Parse(untaggedResponse.Substring("Date: ".Length));
                    }
                }
            }

            return result;
        }

        public PopResponse Quit()
        {
            string[] untaggedResponses;
            string taggedResponse;

            PopResponse result = (PopResponse)ExecuteCommand("QUIT",
                ref _cmdTag,
                out untaggedResponses,
                out taggedResponse);

            return result;
        }

        #endregion
    }
}
