using System;

namespace MailChecker
{
    public enum PopState : byte
    {
        Authorization   = 1,
        Transaction     = 2,
        Update          = 3
    }
}
