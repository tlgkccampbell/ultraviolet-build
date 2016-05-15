using System;

namespace UvTestViewer.Models
{
    /// <summary>
    /// Represents a page of rendering tests.
    /// </summary>
    public class RenderingTestPage
    {
        /// <summary>
        /// Gets or sets a value indicating whether any of the tests on this page failed.
        /// </summary>
        public Boolean Failed
        {
            get;
            set;
        }
    }
}