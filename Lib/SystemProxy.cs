/*
 * Utility class for SystemProxySettings
 *
 * Adapted from:
 * http://huddledmasses.org/setting-windows-internet-connection-proxy-from-c/
 */

/*
 * From http://support.microsoft.com/kb/226473:
 *
 * INTERNET_OPTION_PER_CONNECTION_OPTION causes the settings to be changed
 * on a system-wide basis when a NULL handle is used. To correctly reflect
 * global proxy settings, you must call the InternetSetOption function with
 * the INTERNET_OPTION_REFRESH option flag. Or, to set the settings on a per
 * session basis, a valid session handle can be used.
 */

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace TrotiNet
{
    /// <summary>
    /// Utility class for setting the system proxy (i.e. the options you
    /// get from the 'Internet Options' panel)
    /// </summary>
    public static class SystemProxy
    {
        #region Windows API

        // INTERNET_OPTION options for InternetSetOption
        enum INTERNET_OPTION: int
        {
            REFRESH = 37,
            SETTINGS_CHANGED = 39,
            PER_CONNECTION_OPTION = 75
        }

        // PER_CONN_FLAGS
        [Flags]
        enum PER_CONN_FLAGS: int
        {
            // Direct connection
            PROXY_TYPE_DIRECT = 0x00000001,

            // Connection via named proxy
            PROXY_TYPE_PROXY = 0x00000002,

            // Use autoproxy URL
            PROXY_TYPE_AUTO_PROXY_URL = 0x00000004,

            // Use autoproxy detection
            PROXY_TYPE_AUTO_DETECT = 0x00000008
        }

        // Options for INTERNET_PER_CONN_OPTION
        enum INTERNET_PER_CONN: int
        {
            FLAGS = 1,
            PROXY_SERVER = 2,
            PROXY_BYPASS = 3
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct INTERNET_PER_CONN_OPTION_LIST
        {
            public int dwSize;
            public IntPtr pszConnection;
            public int dwOptionCount;
            public int dwOptionError;
            public IntPtr pOptions;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct INTERNET_PER_CONN_OPTION
        {
            public INTERNET_PER_CONN dwOption;
            public INTERNET_PER_CONN_OPTION_UNION Value;

            // Implement the union field of INTERNET_PER_CONN_OPTION
            [StructLayout(LayoutKind.Explicit)]
            public struct INTERNET_PER_CONN_OPTION_UNION
            {
                [FieldOffset(0)]
                public FILETIME ft; // equivalent to ftValue

                [FieldOffset(0)]
                public int i; // equivalent to dwValue

                [FieldOffset(0)]
                public IntPtr iptr; // equivalent to pszValue
            }
        }

        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool InternetSetOption(IntPtr hInternet,
            INTERNET_OPTION dwOption, IntPtr lpBuffer, int dwBufferLength);

        #endregion

        /// <summary>
        /// Notify WinInet applications that the proxy settings have changed
        /// </summary>
        // XXX NOT SURE IF WE NEED THIS AT ALL
        public static void RefreshSettings()
        {
            /* See: http://social.msdn.microsoft.com/Forums/en/csharpgeneral/
                    thread/19517edf-8348-438a-a3da-5fbe7a46b61a
             */
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION.SETTINGS_CHANGED,
                IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION.REFRESH,
                IntPtr.Zero, 0);
        }

        /// <summary>
        /// Set the system proxy, as per the 'Internet Options' panel
        /// </summary>
        /// <returns>True if the operation was successful</returns>
        public static bool SetProxy(bool ProxyEnable,
            string ProxyServer, string ProxyBypass)
        {
            var options = new INTERNET_PER_CONN_OPTION[3];

            // Option #1: ProxyEnable
            options[0].dwOption = INTERNET_PER_CONN.FLAGS;
            options[0].Value.i = (int) (ProxyEnable
                ? (PER_CONN_FLAGS.PROXY_TYPE_DIRECT |
                    PER_CONN_FLAGS.PROXY_TYPE_PROXY)
                : PER_CONN_FLAGS.PROXY_TYPE_DIRECT);

            // Option #2: ProxyServer
            options[1].dwOption = INTERNET_PER_CONN.PROXY_SERVER;
            options[1].Value.iptr = Marshal.StringToHGlobalAuto(
                ProxyServer);

            // Option #3: ProxyBypass
            options[2].dwOption = INTERNET_PER_CONN.PROXY_BYPASS;
            options[2].Value.iptr = Marshal.StringToHGlobalAuto(
                ProxyBypass);

            var list = new INTERNET_PER_CONN_OPTION_LIST();
            list.dwSize = Marshal.SizeOf(list);
            list.pszConnection = IntPtr.Zero; // change globally
            list.dwOptionCount = options.Length;
            list.dwOptionError = 0;

            // Marshall each option in options
            int optSize = Marshal.SizeOf(typeof(INTERNET_PER_CONN_OPTION));
            IntPtr optionsPtr = Marshal.AllocCoTaskMem(optSize * options.Length);
            for (int i = 0; i < options.Length; ++i)
            {
                var opt = new IntPtr(optionsPtr.ToInt32() + (i * optSize));
                Marshal.StructureToPtr(options[i], opt, false);
            }
            list.pOptions = optionsPtr;

            // and then make a pointer out of the whole list
            IntPtr ipcoListPtr = Marshal.AllocCoTaskMem((Int32)list.dwSize);
            Marshal.StructureToPtr(list, ipcoListPtr, false);

            // Finally, call the InternetSetOption API
            int res = InternetSetOption(IntPtr.Zero,
               INTERNET_OPTION.PER_CONNECTION_OPTION, ipcoListPtr, list.dwSize)
               ? -1 : 0;
            if (res == 0)
                res = Marshal.GetLastWin32Error();

            Marshal.FreeCoTaskMem(optionsPtr);
            Marshal.FreeCoTaskMem(ipcoListPtr);
            if (res > 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            return (res < 0);
        }
    }
}
