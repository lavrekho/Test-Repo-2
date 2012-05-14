using System;

namespace MailChecker
{
    public enum ImapState : byte
    {
        Authenticated       = 1,
        NotAuthenticated    = 2,
        Selected            = 3,
        Logout              = 4
    }
}
