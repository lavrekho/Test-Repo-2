using System;

namespace MailChecker
{
    public class ScanListing
    {
        #region Constructors / Destructors

        public ScanListing()
        {
            MessageID = MessageSize = -1;
        } 
        
        #endregion

        #region Properties
        
        public int MessageID { get; set; }

        public int MessageSize { get; set; } 
        
        #endregion
    }
}
