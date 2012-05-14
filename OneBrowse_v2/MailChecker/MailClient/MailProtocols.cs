using System;

namespace MailChecker
{
    [Serializable]
    public enum MailProtocols : byte
    {
        POP = 1,
        IMAP = 2
    }
}
