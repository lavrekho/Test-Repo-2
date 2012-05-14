using System;

namespace MailChecker
{
    public enum MailboxFlags : byte
    {
        None        = 0,
        Answered    = 1,
        Flagged     = 2,
        Deleted     = 4,
        Seen        = 8,
        Draft       = 16
    }

    public static class MailboxFlagsParser
    {
        /// <param name="flagsStr">Flags string like "(\\Answered \\Deleted \\Seen \\Flagged \\Draft $Forwarded \\*)" --or-- "(\Deleted \Seen \*)"</param>
        public static MailboxFlags Parse(string flagsStr)
        {
            flagsStr = flagsStr.Trim(new char[] { '(', ')' });

            string[] flags = flagsStr.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            MailboxFlags mailboxFlags = MailboxFlags.None;
            MailboxFlags mailboxFlag;

            foreach (string flag in flags)
            {
                try
                {
                    mailboxFlag = (MailboxFlags)Enum.Parse(typeof(MailboxFlags), flag.Substring(1), true);

                    mailboxFlags |= mailboxFlag;
                }
                catch { } // unknown or unsupported flag value
            }

            return mailboxFlags;
        }
    }
}
