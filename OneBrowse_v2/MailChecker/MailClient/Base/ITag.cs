using System;

namespace MailChecker
{
    public interface ITag
    {
        string Value { get; }

        string NextValue();
    }
}
