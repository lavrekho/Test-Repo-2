using System;
using System.Text.RegularExpressions;
using System.Text;

namespace MailChecker
{
    internal class MessageInfo
    {
        #region Class Level Variables

        private string _from = string.Empty;

        private string _subject = string.Empty;

        private MailAccount _account;

        #region Regexes
        
        private static Regex _mimeHeaderFieldPattern = new Regex("=\\?([^\t\\(\\)<>@,;:\\/\\[\\]\\?\\.\\=]+?)\\?([QqBb]{1})\\?([\a-zA-Z0-9\\+\\/=]+?)\\?=");

        private static Regex _qpSoftBreakPattern = new Regex("(=[\r\n]+)|(=(?![0-9A-F][0-9A-F]))");

        private static Regex _qpSubstrPattern = new Regex("(=[0-9A-F][0-9A-F])+");

        private static Regex _qpCharPattern = new Regex("=([0-9A-F][0-9A-F])");

        private static Regex _headerFieldFormatPattern = new Regex("([\r\n]+ )|([\r\n\t]+?)"); 
        
        #endregion

        #endregion

        #region Properties

        public string From
        {
            get
            {
                return _from;
            }
            set
            {
                value = _mimeHeaderFieldPattern.Replace(value, MimeHeaderFieldReplacer);

                _from = _headerFieldFormatPattern.Replace(value, string.Empty);
            }
        }

        public string Subject
        {
            get
            {
                return _subject;
            }
            set
            {
                value = _mimeHeaderFieldPattern.Replace(value, MimeHeaderFieldReplacer);

                _subject = _headerFieldFormatPattern.Replace(value, string.Empty);
            }
        }

        public DateTime Date { get; set; }


        public MailAccount AssociatedAccount 
        {
            get
            {
                return _account;
            }
            set
            {
                _account = value;
            }
        }

        #endregion

        #region Methods (Private)
        
        private static string MimeHeaderFieldReplacer(Match m)
        {
            byte[] decodedBytes = new byte[] { };
            Encoding encoding = Encoding.GetEncoding(m.Groups[1].Value);

            if (m.Groups[2].Value.ToLower() == "b")
                decodedBytes = System.Convert.FromBase64String(m.Groups[3].Value);

            else if (m.Groups[2].Value.ToLower() == "q")
                decodedBytes = FromQuotedPrintableString(m.Groups[3].Value, encoding);

            string decodedString = encoding.GetString(decodedBytes);

            return decodedString;
        }

        private static byte[] FromQuotedPrintableString(string encodedString, Encoding encoding)
        {
            string decodedString = _qpSoftBreakPattern.Replace(encodedString, String.Empty);

            MatchCollection decMatches = _qpSubstrPattern.Matches(decodedString);
            string decSubstr;

            int stInd = 0;
            string decodedString2 = String.Empty;

            foreach (Match decMatch in decMatches)
            {
                // add unencoded substring
                decodedString2 += decodedString.Substring(stInd, decMatch.Index - stInd);

                // next unencoded substring
                stInd = decMatch.Index + decMatch.Length;

                // decode encoded substring
                decSubstr = GetEncodedSubstr(decMatch, encoding);

                // add decoded substring
                decodedString2 = decodedString2 + decSubstr;
            }

            decodedString2 = decodedString2 + decodedString.Substring(stInd);

            // convert decoded string to byte array 
            return encoding.GetBytes(decodedString2);
        }

        private static string GetEncodedSubstr(Match m, Encoding destEncoding)
        {
            // "=0D=0A"
            string matchStr = m.Groups[0].Value;

            // "=0D=0A" to "13*10*"
            string encBytesStr = _qpCharPattern.Replace(matchStr, GetCharValue);

            // "13*10*" to Sting(){13, 10}
            string[] encBytesStrArr = encBytesStr.Split(new string[] { "*" }, StringSplitOptions.RemoveEmptyEntries);

            System.Diagnostics.Debug.WriteLine("*** encBytesStr = " + encBytesStr);

            // Sting(){13, 10} to Byte(){13, 10}
            byte[] encBytesArr = new byte[encBytesStrArr.Length];
            int i = 0;
            foreach (string encByteStr in encBytesStrArr)
            {
                encBytesArr[i++] = System.Convert.ToByte(encByteStr);
            }

            // convert "byte(){13, 10}" to "\r\n"
            string encSubstr = destEncoding.GetString(encBytesArr);

            return encSubstr;
        }

        private static string GetCharValue(Match m)
        {
            // "0D"
            string hexValue = m.Groups[1].Value;
            // 13
            byte decValue = System.Convert.ToByte(hexValue, 16);
            // "13*"
            string decSubstr = decValue.ToString() + "*";

            return decSubstr;
        } 
        
        #endregion

        #region Methods (Public)
        
        public override int GetHashCode()
        {
            return Subject.GetHashCode() + From.GetHashCode() + Date.ToString().GetHashCode() + AssociatedAccount.ID;
        }

        public override bool Equals(object obj)
        {
            return (obj.GetHashCode() == GetHashCode());
        } 
        
        #endregion
    }
}
