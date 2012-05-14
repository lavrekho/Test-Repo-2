using System;

namespace MailChecker
{
    [Serializable]
    public class MailAccount
    {
        public int ID { get; set; }

        public string Email {get; set;}
        
        public string WebInterfaceAddress { get; set; }

        public MailProtocols Protocol { get; set; }
        
        public string Server { get; set; }
        
        public int Port { get; set; }
        
        public bool UseSSL { get; set; }
        
        public string Login { get; set; }
        
        public string Password { get; set; }

        public int PopTotalMessages { get; set; }

        public int PopUnseenMessages { get; set; }

        public static int GetAccountID(MailAccount account)
        {
            string subjectForHash = account.Server.ToLower() + account.Login.ToLower();
            int id = 216613626;

            for (int i = 0; i < subjectForHash.Length; i++)
            {
                id = (id | Convert.ToInt32(subjectForHash[i])) * 16777619;
            }

            return id;
        }
    }
}