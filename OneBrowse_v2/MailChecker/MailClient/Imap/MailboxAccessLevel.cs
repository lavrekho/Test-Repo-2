using System;
using System.Text.RegularExpressions;

namespace MailChecker
{
    public enum MailboxPermissions : byte
    {
        None        = 0,
        ReadWrite   = 1,
        ReadOnly    = 2
    }

    public static class MailboxPermissionsParser
    {
        /// <param name="permissionsStr">Tagged SELECT response like " [READ-WRITE] SELECT completed"</param>
        public static MailboxPermissions Parse(string taggedSelectResponse)
        {
            MailboxPermissions permissions = MailboxPermissions.None;

            Match m = Regex.Match(taggedSelectResponse, "\\[(?<permissions>(READ-WRITE)|(READ-ONLY))\\]", RegexOptions.IgnoreCase);

            if (m.Success)
            {
                string permissionsStr = m.Groups["permissions"].Value.Replace("-", string.Empty);

                try
                {
                    permissions = (MailboxPermissions)Enum.Parse(typeof(MailboxPermissions), permissionsStr, true);
                }
                catch { }
            }

            return permissions;
        }
    }
}
