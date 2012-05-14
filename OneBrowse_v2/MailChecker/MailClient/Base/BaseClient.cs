using System;
using System.Net.Sockets;
using System.Timers;
using System.Text;
using System.IO;
using System.Net.Security;

namespace MailChecker
{
    public abstract class BaseClient
    {
        #region Constants
        
        protected const string CRLF = "\r\n"; 
        
        #endregion

        #region Class Level Variables
        
        protected string _serverGreeting;

        protected string _serverBye;

        protected TcpClient _client;

        protected Stream _stream;

        protected Timer _noopTimer;

        protected MailAccount _accountInfo;

        protected bool _connected;

        protected byte _state;

        protected ITag _cmdTag;
        
        #endregion

        #region Constructors / Destructors
        
        public BaseClient()
        {
            _noopTimer = new Timer();
            _noopTimer.Elapsed += new ElapsedEventHandler(_noopTimer_Elapsed);

            _connected = false;
        }
 
        #endregion

        #region Properties
        
        public MailAccount AccountInfo
        {
            get
            {
                return _accountInfo;
            }
            set
            {
                _accountInfo = value;
            }
        }
 
        #endregion

        #region Methods (Private)
        
        private void _noopTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _noopTimer.Stop();

            if (ResetAutoLogoutTimer())
            {
                _noopTimer.Start();
            }
        }
 
        #endregion

        #region Methods (Protected)
        
        protected abstract bool ResetAutoLogoutTimer();

        protected abstract byte ExecuteCommand(string protocolCmd, ref ITag tag, out string[] untaggedResponses, out string taggedResponse);

        protected void Send(string data)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(data);            
            int bytesToSend, sentNum = 0;

            try
            {
                do
                {
                    bytesToSend = (bytes.Length - sentNum < 8096) ? bytes.Length - sentNum : 8096;

                    _stream.Write(bytes, sentNum, bytesToSend);

                    sentNum += bytesToSend;
                }
                while (sentNum < bytes.Length);
            }
            catch
            {
                _connected = false;
            }
        }

        protected abstract string Recv();
        
        #endregion

        #region Methods (Public)
        
        public bool Connect()
        {
            if (_connected)
                return false;

            if (_accountInfo == null)
            {
                throw new NullReferenceException("BaseClient.AccountInfo property value can't be null.");
            }

            try
            {
                _client = new TcpClient()
                {
                    ReceiveTimeout = 5000
                };

                _client.Connect(_accountInfo.Server, _accountInfo.Port);

                if (_accountInfo.UseSSL)
                {
                    _stream = new SslStream(_client.GetStream());

                    ((SslStream)_stream).AuthenticateAsClient(_accountInfo.Server);
                }
                else
                {
                    _stream = _client.GetStream();
                }

                string data = Recv();

                _connected = (data.StartsWith(_serverGreeting, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
                _client = null;

                _connected = false;
            }

            if (_connected)
            {
                _noopTimer.Start();
            }

            return _connected;
        }

        public void Disconnect()
        {
            if (!_connected)
                return;

            _noopTimer.Stop();

            _connected = false;

            try
            {
                _client.Close();

                _stream.Dispose();
            }
            catch { }
        } 
        
        #endregion
    }
}
