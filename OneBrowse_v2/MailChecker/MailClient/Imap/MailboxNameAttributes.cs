using System;

namespace MailChecker
{
    public enum MailboxNameAttributes : byte
    {
        None            = 0,
        Noinferiors     = 1,
        Noselect        = 2,
        Marked          = 4,
        Unmarked        = 8,
        HasNoChildren   = 16
    }

    public static class MailboxNameAttributesParser
    {
        /// <param name="attributesStr">Attributes string like "(\Marked \HasNoChildren)" --or-- "(\NoInferiors)"</param>
        public static MailboxNameAttributes Parse(string nameAttributesStr)
        {
            nameAttributesStr = nameAttributesStr.Trim(new char[] { '(', ')' });

            string[] nameAttributes = nameAttributesStr.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            MailboxNameAttributes attributes = MailboxNameAttributes.None;
            MailboxNameAttributes attribute;

            foreach (string nameAttribute in nameAttributes)
            {
                try
                {
                    attribute = (MailboxNameAttributes) Enum.Parse(typeof(MailboxNameAttributes), nameAttribute.Trim(), true);

                    attributes |= attribute;
                }
                catch {} // unknown or unsupported name attribute
            }

            return attributes;
        }
    }
}
