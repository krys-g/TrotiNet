using System;
using System.IO;
using log4net;

namespace TrotiNet
{
    internal class Log
    {
        /// <summary>
        /// Create a class logger
        /// </summary>
        public static ILog Get()
        {
            return log4net.LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }
    }
}
