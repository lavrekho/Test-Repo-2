using System;

namespace MailChecker
{
    public class Mailbox
    {
        #region Properties
        
        public MailboxNameAttributes NameAttributes { get; set; }

        public string HierarchyDelimeter { get; set; }

        public string Name { get; set; } 
        
        #endregion
    }
}
