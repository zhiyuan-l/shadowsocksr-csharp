
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Shadowsocks.Model;
using Shadowsocks.Util;
using System;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Shadowsocks.Controller
{
    class TunTapService : Listener.Service
    {
        // default interface settings
        private const string DEFAULT_INTERFACE_NAME = "SSETUN";
        private const string DEFAULT_INTERFACE_ADDRESS = "10.10.5.5";
        private const string DEFAULT_INTERFACE_MASK = "255.255.255.0";
        private const string DEFAULT_INTERFACE_PRI_DNS = "1.1.1.1";
        private const string DEFAULT_INTERFACE_SEC_DNS = "1.1.0.0";

        public const int FILE_ATTRIBUTE_SYSTEM = 0x4;
        public const int FILE_FLAG_OVERLAPPED = 0x40000000;

        public FileStream tap { get; private set; }
        public TunTap tun { get; private set; }

        public TunTapService(TunTap tun)
        {
            this.tun = tun;
        }

        public TunTapService(String guid)
        {
            TunTap tun = new TunTap(guid);
            this.tun = tun;
        }

        bool Listener.Service.Handle(byte[] firstPacket, int length, Socket socket)
        {
            throw new NotImplementedException();
        }

        private bool isValid = false;
        public bool isOpen { get; private set; } = false;

        // validate current status and config of tun/tap device
        // return validate status
        public bool validate()
        {
            TunTapService.FixNetworkInterface(this.tun);
            reload();
            this.isValid = true;
            return this.isValid;
        }

        public void open()
        {
            if (validate())
            {
                // tap file
                string fileName = TunTap.USERMODEDEVICEDIR + tun.devGuid + TunTap.TAP_WIN_SUFFIX;
                IntPtr handle = TunTap.CreateFile(fileName, FileAccess.ReadWrite,
                    FileShare.ReadWrite, 0, FileMode.Open, FILE_ATTRIBUTE_SYSTEM | FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                tap = new FileStream(new SafeFileHandle(handle, true), FileAccess.ReadWrite, 10000, true);

                int len;
                IntPtr ps = Marshal.AllocHGlobal(1);
                var flag = TunTap.DeviceIoControl(
                    handle, // hDevice
                    TunTap.TAP_WIN_IOCTL_GET_MAC, // IO control code
                    ps, 100,      // IN buffer and size
                    ps, 100,      // OUT buffer and size
                    out len, // size returned
                    IntPtr.Zero // overlapped
                    );

                if (!flag)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // set the status of the device to be connected
                IntPtr pstatus = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(pstatus, 1);
                TunTap.DeviceIoControl(
                    handle, // hDevice
                    TunTap.TAP_WIN_IOCTL_SET_MEDIA_STATUS, // IO control code
                    pstatus, 4,      // IN buffer and size
                    pstatus, 4,      // OUT buffer and size
                    out len, // size returned
                    IntPtr.Zero // overlapped
                    );

                // config tun
                IntPtr ptun = Marshal.AllocHGlobal(12);
                Marshal.WriteInt32(ptun, 0, 0x0100030a);
                Marshal.WriteInt32(ptun, 4, 0x0000030a);
                Marshal.WriteInt32(ptun, 8, unchecked((int)0x00ffffff));
                TunTap.DeviceIoControl(
                    handle,
                    TunTap.TAP_WIN_IOCTL_CONFIG_POINT_TO_POINT,
                    ptun, 12,
                    ptun, 12,
                    out len,
                    IntPtr.Zero
                    );

                this.isOpen = true;
            }
        }
        
        //
        // Pick up the first tuntap device and return its node GUID
        //
        public static string GetOneDeviceGuid(string AdapterKey = TunTap.ADAPTER_KEY, string ComponentId = TunTap.DEFAULT_COMPONENT_ID)
        {
            RegistryKey regAdapters = Registry.LocalMachine.OpenSubKey(AdapterKey);
            string[] keyNames = regAdapters.GetSubKeyNames();
            string devGuid = "";
            foreach (string x in keyNames)
            {
                try
                {
                    RegistryKey regAdapter = regAdapters.OpenSubKey(x);
                    object id = regAdapter.GetValue("ComponentId");
                    if (id != null && id.ToString() == ComponentId)
                    {
                        devGuid = regAdapter.GetValue("NetCfgInstanceId").ToString();
                    }
                }
                catch (Exception e)
                {
                    // Console.WriteLine(e.Message);
                }

            }
            return devGuid;
        }
        //
        // Returns the device name from the Control panel based on GUID
        //
        public static string GetInterfaceName(string guid, string ConnectionKey = TunTap.NETWORK_CONNECTIONS_KEY)
        {
            if (guid != "")
            {
                RegistryKey regConnection = Registry.LocalMachine.OpenSubKey($"{ConnectionKey}\\{guid}\\Connection");
                if (regConnection != null)
                {
                    object name = regConnection.GetValue("Name");
                    if (name != null)
                    {
                        return name.ToString();
                    }
                }
            }
            return string.Empty;
        }

        public static bool SetInterfaceName(string name, string newname = DEFAULT_INTERFACE_NAME)
        {
            if (name != "" && newname != "")
            {
                Utils.RunCommand("netsh", $"interface set interface name=\"{name}\" newname=\"{newname}\"", "runas");
            }
            return false;
        }

        public static void SetInterfaceStaticAddress(
            string name,
            string address = DEFAULT_INTERFACE_ADDRESS,
            string netmask = DEFAULT_INTERFACE_MASK)
        {
            if (name != "")
            {
                Utils.RunCommand("netsh", $"interface ip set address \"{name}\" static \"{address}\" \"{netmask}\"", "runas");
            }
        }

        public static void SetPrimaryDNS(
            string name,
            string primaryDNS = DEFAULT_INTERFACE_PRI_DNS)
        {
            if (name != "")
            {
                Utils.RunCommand("netsh", $"interface ip set dns \"{name}\" static {primaryDNS}", "runas");
            }
        }

        public static void SetSecondaryDNS(string name,
            string secondaryDNS = DEFAULT_INTERFACE_SEC_DNS)
        {
            if (name != "")
            {
                // Utils.RunCommand("netsh", $"interface ipv4 add dns \"{name}\" addr=\"{secondaryDNS}\" index=2", "runas");
            }
        }

        public static void FixNetworkInterface(
            string guid,
            string newname = DEFAULT_INTERFACE_NAME,
            string address = DEFAULT_INTERFACE_ADDRESS,
            string netmask = DEFAULT_INTERFACE_MASK,
            string primaryDNS = DEFAULT_INTERFACE_PRI_DNS,
            string secondaryDNS = DEFAULT_INTERFACE_SEC_DNS
            )
        {
            string name = GetInterfaceName(guid);
            if (name != "")
            {
                SetInterfaceStaticAddress(name, address, netmask);
                SetPrimaryDNS(name, primaryDNS);
                SetSecondaryDNS(secondaryDNS);
                SetInterfaceName(name, newname);
            }
        }

        public static void FixNetworkInterface(
            TunTap tun,
            string newname = DEFAULT_INTERFACE_NAME,
            string address = DEFAULT_INTERFACE_ADDRESS,
            string netmask = DEFAULT_INTERFACE_MASK,
            string primaryDNS = DEFAULT_INTERFACE_PRI_DNS,
            string secondaryDNS = DEFAULT_INTERFACE_SEC_DNS
            )
        {
            string guid = tun.devGuid;
            string name = GetInterfaceName(guid);
            if (name != "")
            {
                SetInterfaceStaticAddress(name, address, netmask);
                SetPrimaryDNS(name, primaryDNS);
                SetSecondaryDNS(secondaryDNS);
                SetInterfaceName(name, newname);
                SetGlobalRoute();
                reloadTunTap(tun);
            }
        }

        // get latest tun/tap device info
        public static void reloadTunTap(TunTap tun)
        {
            string guid = tun.devGuid;
            string name = GetInterfaceName(guid);

            tun.name = name;
        }

        public void reload()
        {
            reloadTunTap(this.tun);
        }

        public static void SetGlobalRoute()
        {
            IPForwardRow row = new IPForwardRow();
            row.Dest = System.Net.IPAddress.Parse("0.0.0.0");
            row.Mask = System.Net.IPAddress.Parse("0.0.0.0");
            row.NextHop = System.Net.IPAddress.Parse(DEFAULT_INTERFACE_ADDRESS);
            RouteTableUtil.CreateIpForwardEntry(row);
        }

        //
        // Fix device name of the TAP device
        //
        public static void CorrectInterfaceName(string guid, string newname = DEFAULT_INTERFACE_NAME)
        {
            string name = GetInterfaceName(guid);
            if (name != newname)
            {
                SetInterfaceName(name, newname);
            }
        }
    }
}
