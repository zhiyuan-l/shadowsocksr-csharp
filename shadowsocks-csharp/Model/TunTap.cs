using Microsoft.Win32;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;


namespace Shadowsocks.Model
{
    class TunTap
    {

        /*
         * =============
         * TAP IOCTLs 
         * ref: tap-windows.h
         * =============
         */
         
        /* Present in 8.1 */

        public static uint TAP_WIN_IOCTL_GET_MAC = TAP_WIN_CONTROL_CODE(1, METHOD_BUFFERED);
        public static uint TAP_WIN_IOCTL_GET_VERSION = TAP_WIN_CONTROL_CODE(2, METHOD_BUFFERED);
        public static uint TAP_WIN_IOCTL_GET_MTU = TAP_WIN_CONTROL_CODE(3, METHOD_BUFFERED);
        public static uint TAP_WIN_IOCTL_GET_INFO = TAP_WIN_CONTROL_CODE(4, METHOD_BUFFERED);
        public static uint TAP_WIN_IOCTL_CONFIG_POINT_TO_POINT = TAP_WIN_CONTROL_CODE(5, METHOD_BUFFERED);
        public static uint TAP_WIN_IOCTL_SET_MEDIA_STATUS = TAP_WIN_CONTROL_CODE(6, METHOD_BUFFERED);
        public static uint TAP_WIN_IOCTL_CONFIG_DHCP_MASQ = TAP_WIN_CONTROL_CODE(7, METHOD_BUFFERED);
        public static uint TAP_WIN_IOCTL_GET_LOG_LINE = TAP_WIN_CONTROL_CODE(8, METHOD_BUFFERED);
        public static uint TAP_WIN_IOCTL_CONFIG_DHCP_SET_OPT = TAP_WIN_CONTROL_CODE(9, METHOD_BUFFERED);

        /* Added in 8.2 */

        /* obsoletes TAP_WIN_IOCTL_CONFIG_POINT_TO_POINT */
        public static uint TAP_WIN_IOCTL_CONFIG_TUN = TAP_WIN_CONTROL_CODE(10, METHOD_BUFFERED);

        /*
         * =================
         * Registry keys
         * =================
         */
        public const string ADAPTER_KEY = "SYSTEM\\CurrentControlSet\\Control\\Class\\{4D36E972-E325-11CE-BFC1-08002BE10318}";

        public const string NETWORK_CONNECTIONS_KEY = "SYSTEM\\CurrentControlSet\\Control\\Network\\{4D36E972-E325-11CE-BFC1-08002BE10318}";

        /*
         * ======================
         * Filesystem prefixes
         * ======================
         */

        public const string USERMODEDEVICEDIR = "\\\\.\\Global\\";
        public const string SYSDEVICEDIR = "\\Device\\";
        public const string USERDEVICEDIR = "\\DosDevices\\Global\\";

        public const string TAP_WIN_SUFFIX = ".tap";
        public const string DEFAULT_COMPONENT_ID = "tap0901";
        
        private const uint METHOD_BUFFERED = 0;
        private const uint FILE_ANY_ACCESS = 0;
        private const uint FILE_DEVICE_UNKNOWN = 0x00000022;

        public string devGuid { get; private set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string address { get; set; } = string.Empty;
        public string netmask { get; set; } = string.Empty;
        public string primaryDNS { get; set; } = string.Empty;
        public string secondaryDNS { get; set; } = string.Empty;

        public TunTap(string guid)
        {
            this.devGuid = guid;
        }

        private static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access)
        {
            return ((DeviceType << 16) | (Access << 14) | (Function << 2) | Method);
        }

        public static uint TAP_WIN_CONTROL_CODE(uint request, uint method)
        {
            return CTL_CODE(FILE_DEVICE_UNKNOWN, request, method, FILE_ANY_ACCESS);
        }

        /// <summary>
        /// ref: https://docs.microsoft.com/en-us/windows/desktop/api/fileapi/nf-fileapi-createfilea
        /// </summary>
        [DllImport("Kernel32.dll", /* ExactSpelling = true, */ SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateFile(
            string filename,
            [MarshalAs(UnmanagedType.U4)]FileAccess fileaccess,
            [MarshalAs(UnmanagedType.U4)]FileShare fileshare,
            int securityattributes,
            [MarshalAs(UnmanagedType.U4)]FileMode creationdisposition,
            int flags,
            IntPtr template
            );


        /// <summary>
        /// ref: https://msdn.microsoft.com/en-us/library/windows/desktop/aa363216(v=vs.85).aspx
        /// </summary>
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
            IntPtr lpInBuffer, uint nInBufferSize,
            IntPtr lpOutBuffer, uint nOutBufferSize,
            out int lpBytesReturned, IntPtr lpOverlapped);
    }
}
