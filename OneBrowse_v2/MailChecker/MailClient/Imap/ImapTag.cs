using System;
using System.Text.RegularExpressions;

namespace MailChecker
{
    /// <summary>
    /// A001 
    /// --or--
    /// D092
    /// </summary>
    public class ImapTag : ITag
    {
        #region Class level Variables
        
        private static char[] _alphabet = new char[] { 'A', 'B', 'C', 'D', 'E', 'F' };

        private int _alphabetPos;

        private int _number;

        private string _value; 
        
        #endregion

        #region Constructors / Destructors
        
        public ImapTag()
        {
            _alphabetPos = 0;

            _number = 0;

            _value = _alphabet[_alphabetPos] + _number.ToString().PadLeft(3, '0');
        }

        public ImapTag(string initialTag)
        {
            if (!Regex.IsMatch(initialTag, "^([A-F]{1})([0-9]{3})$"))
            {
                throw new ArgumentException("Invalid tag value specified.");
            }

            _alphabetPos = (int)initialTag[0] - 65;

            _number = Int32.Parse(initialTag.Substring(1));

            _value = _alphabet[_alphabetPos] + _number.ToString().PadLeft(3, '0');
        }
 
        #endregion

        #region Properties
        
        public string Value
        {
            get
            {
                return _value;
            }
        } 

        #endregion

        #region Methods (Public)
        
        public string NextValue()
        {
            lock (_alphabet)
            {
                if (_number == 999)
                {
                    _alphabetPos = (_alphabetPos + 1 == _alphabet.Length) ? 0 : _alphabetPos + 1;

                    _number = 0;
                }
                else
                {
                    _number++;
                }
            }

            _value = _alphabet[_alphabetPos] + _number.ToString().PadLeft(3, '0');

            return _value;
        }
 
        #endregion
    }
}
