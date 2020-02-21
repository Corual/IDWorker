using System;
using System.Collections.Generic;
using System.Text;

namespace System
{
    /// <summary>
    /// 时间倒退异常
    /// </summary>
    public class TimeRegressionException : Exception
    {
        public TimeRegressionException(string message) : base(message)
        {

        }

        public TimeRegressionException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
