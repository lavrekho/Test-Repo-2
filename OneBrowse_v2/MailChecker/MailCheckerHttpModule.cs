#define TRACE_INTERNALS_CHANGES
//#define TRACE_NETWORK_REQUESTS


using System;
using System.Net;
using System.Reflection;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Microsoft.Win32;

using OneBrowse;

namespace MailChecker
{
    public sealed class MailCheckerHttpModule : HttpModule
    {
        #region Class Level Variables

        private Random _rnd = new Random(DateTime.Now.Millisecond);

        private AccountsMonitor _monitor;

        private string _messageTemplate;

        private string _htmlPopupTemplate;

        #endregion
        
        #region Constructors / Destructors

        public MailCheckerHttpModule(FilterPlugin ownerPlugin) : base(ownerPlugin)
        {
            /* initialize base members */ 

            _moduleResources = LoadEmbeddedResources(Assembly.GetExecutingAssembly(), "MailChecker.EmbeddedResources");

            _htmlTemplate = (_moduleResources.ContainsKey("module.html")) ? Encoding.UTF8.GetString(_moduleResources["module.html"].ToArray()) : string.Empty;


            /* initialize child members */ 

            _monitor = ((MailChecker)ownerPlugin).Monitor;

            _messageTemplate = (_moduleResources.ContainsKey("row.html")) ? Encoding.UTF8.GetString(_moduleResources["row.html"].ToArray()) : string.Empty;

            _htmlPopupTemplate = (_moduleResources.ContainsKey("popup.html")) ? Encoding.UTF8.GetString(_moduleResources["popup.html"].ToArray()) : string.Empty;
        }

        #endregion

        #region Properties

        public string PopupIFrame
        {
            get
            {
                return Encoding.UTF8.GetString(_moduleResources["iframe.html"].ToArray());
            }
        } 
        
        #endregion

        #region Methods (Private)
        
        private string RenderMailAccount(MailAccount account)
        {
            string html = string.Empty;

            string template = Encoding.UTF8.GetString(_moduleResources["account.html"].ToArray());

            if (account == null)
            {
                html = string.Format(template,

                    // form header
                    "New Account",

                    // "/MailChecker/"
                    _pluginSubDir,

                    // account ID
                    "-1",

                    // email
                    "",

                    // web-interface link
                    "http://",

                    // label#id
                    _rnd.Next(100000, 200000).ToString(),

                    "",

                    // label#id
                    _rnd.Next(100000, 200000).ToString(),

                    "",

                    // server
                    "",

                    // port
                    "",

                    // checkbox#id
                    _rnd.Next(100000, 200000).ToString(),

                    "",

                    // login
                    "",

                    // password
                    ""

                    );
            }
            else
            {
                html = string.Format(template,

                   // form header
                   account.Email,

                   // "/MailChecker/"
                   _pluginSubDir,

                   // account ID
                   account.ID.ToString(),

                   // email
                   account.Email,

                   // web-interface link
                   account.WebInterfaceAddress,

                   // label#id
                   _rnd.Next(100000, 200000).ToString(),

                   (account.Protocol == MailProtocols.POP) ? "checked" : "",

                   // label#id
                   _rnd.Next(100000, 200000).ToString(),

                   (account.Protocol == MailProtocols.IMAP) ? "checked" : "",

                   // server
                  account.Server,

                   // port
                   account.Port,

                   // checkbox#id
                   _rnd.Next(100000, 200000).ToString(),

                   (account.UseSSL) ? "checked" : "",

                   // login
                   account.Login,

                   // password
                   account.Password

                   );
            }

            return html;
        }

        private static bool ValidateInputAccountDetails(NameValueCollection query)
        {
            bool result = false;

            int id, port;

            result =
            (
                query.Get("accountID") != null && Int32.TryParse(query.Get("accountID"), out id) && (id == -1 || id > 0) &&
                query.Get("address") != null && query.Get("address").IndexOf('@') > 0 && query.Get("address").IndexOf('@') < query.Get("address").Length - 1 &&
                query.Get("permalink") != null && query.Get("permalink").Length > 0 &&
                query.Get("protocol") != null && (query.Get("protocol") == "pop3" || query.Get("protocol") == "imap") &&
                query.Get("server") != null && query.Get("server").Length > 3 &&
                query.Get("port") != null && Int32.TryParse(query.Get("port"), out port) && port > 0 &&
                query.Get("login") != null && query.Get("login").Length > 0 &&
                query.Get("password") != null && query.Get("password").Length > 0
            );

            return result;
        }

        private byte[] RenderPopupPage()
        {
            string messages = string.Empty;

            string subject, from;

            foreach (MessageInfo message in _monitor.NewMessages)
            {
                subject = message.Subject;

                subject = (subject.Trim().Length == 0) ? "<no subject>" : (subject.Length > 30) ? subject.Substring(0, 30) + "..." : subject;


                from = message.From;

                from = (from.Trim().Length == 0) ? "<no address>" : (from.Length > 33) ? from.Substring(0, 33) + "..." : from;

                from = from.Replace("<", "&lt;").Replace(">", "&gt;");


                messages += '\n' + string.Format(_messageTemplate, 
                    message.Date.ToString(),
                    message.AssociatedAccount.WebInterfaceAddress,
                    message.AssociatedAccount.ID,
                    subject,
                    from);
            }

            string popup = string.Format(_htmlPopupTemplate, messages);

            return Encoding.UTF8.GetBytes(popup);
        }

        #endregion

        #region HttpModule Members

        // Handle requests to "http://localhost:port/MailChecker/[...]"
        public override byte[] ProcessClientRequest(HttpListenerRequest request, out string mime, out List<KeyValuePair<string, string>> customHeaders)
        {
#if TRACE_NETWORK_REQUESTS
            Debug.WriteLine("MailChecker._MailCheckerHttpModule.ProcessClientRequest() : HttpListenerRequest.Url = " + request.Url.ToString());
#endif
            mime = "text/html";
            customHeaders = new List<KeyValuePair<string, string>>();

            byte[] responseBytes = new byte[0];

            if (_htmlTemplate.Length == 0)
                return responseBytes;

            //if (_pluginSubDir.Length == 0) // called only once to get plugin name
            //    _pluginSubDir = string.Format("/{0}/", PluginName);


            // "/MailChecker/"
            if (request.Url.LocalPath == _ownerPlugin.Name)
            {
                // render template
                responseBytes = RenderPage(out mime);
            }

            // "/MailChecker/control?plugin=disable"
            // --or--
            // "/MailChecker/control?plugin=enable"
            else if (request.Url.LocalPath == _pluginSubDir + "control")
            {
                if (request.QueryString.Count > 0)
                {
                    string value = request.QueryString.Get("plugin");

                    if (value != null)
                    {
                        value = value.ToLower();

                        if (value == "disable" && _ownerPlugin.Enabled)
                        {
#if TRACE_INTERNALS_CHANGES
                            Debug.WriteLine("MailChecker._MailCheckerHttpModule : trying disable plugin");
#endif
                            _ownerPlugin.TurnPlugin(false, true);
                        }
                        else if (value == "enable" && !_ownerPlugin.Enabled)
                        {
#if TRACE_INTERNALS_CHANGES
                            Debug.WriteLine("MailChecker._MailCheckerHttpModule : trying enable plugin");
#endif
                            _ownerPlugin.TurnPlugin(true, true);
                        }
                    }
                }

                // render template
                responseBytes = RenderPage(out mime);
                mime = "text/html";
            }

            // "/MailChecker/processes?process=opera.exe&add="
            // --or--
            // "/MailChecker/processes?process=firefox.exe&remove="
            else if (request.Url.LocalPath == _pluginSubDir + "processes")
            {
                if (request.QueryString.Count > 0)
                {
                    string value = request.QueryString.Get("process");

                    if (value != null)
                    {
                        if (request.QueryString.Get("add") != null)
                        {
#if TRACE_INTERNALS_CHANGES
                            Debug.WriteLine("MailChecker._MailCheckerHttpModule : trying add process : " + value);
#endif
                            _ownerPlugin.ProcessesToCapture.Add(value);
                        }
                        else if (request.QueryString.Get("remove") != null)
                        {
#if TRACE_INTERNALS_CHANGES
                            Debug.WriteLine("MailChecker._MailCheckerHttpModule : trying remove process : " + value);
#endif
                            _ownerPlugin.ProcessesToCapture.Remove(value);
                        }
                    }
                }

                // render template
                responseBytes = RenderPage(out mime);
                mime = "text/html";
            }

            // "/MailChecker/accounts?action=create"
            // --or--
            // --or--
            else if (request.Url.LocalPath == _pluginSubDir + "accounts")
            {
                if (request.QueryString.Count > 0)
                {
                    string value = request.QueryString.Get("action");

                    if (value != null)
                    {
                        if (value == "create")
                        {
                            responseBytes = Encoding.UTF8.GetBytes(RenderMailAccount(null));
                            mime = "text/html";

                            return responseBytes;
                        }
                        else if (value == "save")
                        {
                            if (ValidateInputAccountDetails(request.QueryString))
                            {
                                MailAccount account = new MailAccount();

                                /* parse query string */
                                account.ID = Int32.Parse(request.QueryString.Get("accountID"));
                                account.Email = request.QueryString.Get("address").Trim();
                                account.WebInterfaceAddress = request.QueryString.Get("permalink").Trim();
                                account.Protocol = (request.QueryString.Get("protocol") == "pop3") ? MailProtocols.POP : MailProtocols.IMAP;
                                account.Server = request.QueryString.Get("server").Trim();
                                account.Port = Int32.Parse(request.QueryString.Get("port"));
                                account.UseSSL = (request.QueryString.Get("useSSL") != null);
                                account.Login = request.QueryString.Get("login").Trim();
                                account.Password = request.QueryString.Get("password").Trim();


                                _monitor.UpdateAccount(account); // create new or update existing account
                            }
                        }
                        else if (value == "delete")
                        {
                            int id;

                            if (Int32.TryParse(request.QueryString.Get("accountID"), out id))
                            {
                                _monitor.RemoveAccount(id);
                            }
                        }
                    }
                }

                responseBytes = RenderPage(out mime);
                mime = "text/html";
            }

            // "/MailChecker/reset?accountID=123456"
            else if (request.Url.LocalPath == _pluginSubDir + "reset")
            {
                int id = Int32.Parse(request.QueryString.Get("accountID"));
                
                _monitor.MarkAllMessagesAsRead(id);

                mime = "text/html";
            }

            // "/MailChecker/any_bad_path?any=bad_parameters"
            // --or--
            // "/MailChecker/style.css"            
            else
            {
                string rsName = request.Url.LocalPath.Substring(request.Url.LocalPath.LastIndexOf('/') + 1);

                if (rsName == "popup.html")
                {
                    responseBytes = RenderPopupPage();
                    mime = GetMimeType(rsName);
                }
                else
                {
                    if (_moduleResources.ContainsKey(rsName))
                    {
                        // return requested file
                        responseBytes = _moduleResources[rsName].ToArray();
                        mime = GetMimeType(rsName);
                    }
                    else
                    {
                        // we being user friendly for bad requests and return plugin's main page, not an 404 error
                        responseBytes = RenderPage(out mime);
                    }
                }
            }

            customHeaders.Add(new KeyValuePair<string, string>("Access-Control-Allow-Origin", "*"));

            return responseBytes;
        }

        // render plugin's main page
        protected override byte[] RenderPage(out string mime)
        {
            if (_pluginSubDir.Length == 0) // called only once to get the plugin name
                _pluginSubDir = string.Format("/{0}/", _ownerPlugin.Name);

            /* render processes*/
            string processes = string.Empty;

            foreach (string process in _ownerPlugin.ProcessesToCapture.Processes)
            {
                processes += process + "\n";
            }

            /* render accounts */
            string accounts = string.Empty;

            foreach (MailAccount account in _monitor.Accounts)
            {
                accounts += RenderMailAccount(account) + "\n";
            }

            string response = string.Format(_htmlTemplate,

                _ownerPlugin.FriendlyName,

                Assembly.GetExecutingAssembly().GetName().Version,

                (_ownerPlugin.Enabled) ? "enabled" : "disabled",

                (_ownerPlugin.Enabled) ? "disabled" : "",

                (_ownerPlugin.Enabled) ? "" : "disabled",

                _pluginSubDir,

                (_ownerPlugin.Enabled) ? "on.png" : "off.png",

                processes,

                accounts);


            mime = "text/html";

            return Encoding.UTF8.GetBytes(response);
        }

        #endregion
    }
}