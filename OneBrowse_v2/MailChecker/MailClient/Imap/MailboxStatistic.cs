using System;

namespace MailChecker
{
    public class MailboxStatistic
    {
        #region Properties
        
        /// <summary>
        /// Defined flags in the mailbox.
        /// </summary>
        public MailboxFlags Flags { get; set; }

        /// <summary>
        /// The number of messages in the mailbox.
        /// </summary>
        public int Exists { get; set; }

        /// <summary>
        /// The number of messages with the \Recent flag set.
        /// </summary>
        public int Recent { get; set; }

        /// <summary>
        /// The message sequence number of the first unseen message in the mailbox.
        /// </summary>
        public int Unseen { get; set; }

        /// <summary>
        /// A list of message flags that the client can change permanently.  
        /// If this is missing, the client should assume that all flags can be changed permanently.
        /// </summary>
        public MailboxFlags PermanentFlags { get; set; }

        /// <summary>
        /// The next unique identifier value.
        /// </summary>
        public int UIDNEXT { get; set; }

        /// <summary>
        /// The unique identifier validity value.
        /// </summary>
        public int UIDVALIDITY { get; set; }

        /// <summary>
        /// Modifier access to mailbox.
        /// </summary>
        public MailboxPermissions Permissions { get; set; }

        #endregion

        #region Constructors / Destructors
        
        public MailboxStatistic()
        {
            Flags = MailboxFlags.None;
            Exists = -1;
            Recent = -1;
            Unseen = -1;
            PermanentFlags = MailboxFlags.None;
            UIDNEXT = -1;
            UIDVALIDITY = -1;
            Permissions = MailboxPermissions.None;
        }
        
        #endregion
    }
}
