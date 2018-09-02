using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Shadowsocks.Controller;
using System;
using System.IO;
using Shadowsocks.Controller;
using System.Runtime.InteropServices;
using System.Threading;
using Shadowsocks.Model;
using System.Text;

namespace Shadowsocks.Util
{
    /// <summary>
    /// Summary description for Class1.
    /// </summary>
    public class TunTest
	{
        private static EventWaitHandle readWait;
        private static EventWaitHandle writeWait;
        private static int BytesRead;
        private static FileStream Tap;
        static byte[] buf = new byte[10000];


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Test()
        {
            string guid = TunTapService.GetOneDeviceGuid();
            TunTapService tunTapService = new TunTapService(guid);
            tunTapService.open();
            Tap = tunTapService.tap;
            // return;

            object asyncReadState = new int();
            readWait = new EventWaitHandle(false, EventResetMode.AutoReset);
            object asyncWriteState = new int();
            writeWait = new EventWaitHandle(false, EventResetMode.AutoReset);
            AsyncCallback readCallback = new AsyncCallback(ReadCallback);
            AsyncCallback writeCallback = new AsyncCallback(WriteCallback);
            IAsyncResult res, res2;
            while (true)
            {
                Tap.BeginRead(buf, 0, buf.Length, readCallback, asyncReadState);
                readWait.WaitOne();
                //
                // Reverse IPv4 addresses and send back to tun
                //
                //for (int i = 0; i < 4; ++i)
                //{
                //    byte tmp = buf[12 + i];
                //    buf[12 + i] = buf[16 + i];
                //    buf[16 + i] = tmp;
                //}
                Tap.BeginWrite(buf, 0, BytesRead, writeCallback, asyncWriteState);
                writeWait.WaitOne();
            }
        }

        public static void ReadCallback(IAsyncResult asyncResult)
        {
            BytesRead = Tap.EndRead(asyncResult);
            Console.WriteLine(BytesRead.ToString());
            readWait.Set();
        }

        public static void WriteCallback(IAsyncResult asyncResult)
        {
            Tap.EndWrite(asyncResult);
            Console.WriteLine(BytesRead.ToString());
            writeWait.Set();
        }
    }

}