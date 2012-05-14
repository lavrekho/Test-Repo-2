using System;

namespace MailChecker
{
    public enum ImapResponse : byte
    {
        None    = 0,
        OK      = 1,
        NO      = 2,
        BAD     = 3
    }
}
