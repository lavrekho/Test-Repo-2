using System;

namespace MailChecker
{
    public class DropListing
    {
        #region Constructors / Destructors
        
        public DropListing()
        {
            Messages = MaildropSize = -1;
        } 
        
        #endregion

        #region Properties
        
        public int Messages { get; set; }

        public int MaildropSize { get; set; } 
        
        #endregion
    }
}
