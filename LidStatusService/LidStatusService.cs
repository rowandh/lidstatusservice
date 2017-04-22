using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace LidStatusService
{
    public partial class LidStatusService : ServiceBase
    {
        [DllImport(@"User32", SetLastError = true, EntryPoint = "RegisterPowerSettingNotification", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, Int32 Flags);

        [DllImport("User32", EntryPoint = "UnregisterPowerSettingNotification", CallingConvention = CallingConvention.StdCall)]
        private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

        private delegate IntPtr ServiceControlHandlerEx(int control, int eventType, IntPtr eventData, IntPtr context);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern IntPtr RegisterServiceCtrlHandlerEx(string lpServiceName, ServiceControlHandlerEx cbex, IntPtr context);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct POWERBROADCAST_SETTING
        {
            public Guid PowerSetting;
            public uint DataLength;
            public byte Data;
        }

        private static Guid GUID_LIDSWITCH_STATE_CHANGE = new Guid(0xBA3E0F4D, 0xB817, 0x4094, 0xA2, 0xD1, 0xD5, 0x63, 0x79, 0xE6, 0xA0, 0xF3);
        private const int DEVICE_NOTIFY_SERVICE_HANDLE = 0x00000001;
        private const int SERVICE_CONTROL_POWEREVENT = 0x0000000D;
        private const int PBT_POWERSETTINGCHANGE = 0x8013;

        private IntPtr _powerSettingsNotificationHandle;

        public LidStatusService()
        {
            InitializeComponent();
        }

        private void RegisterForPowerNotifications()
        {
            _powerSettingsNotificationHandle = RegisterPowerSettingNotification(ServiceHandle,
                 ref GUID_LIDSWITCH_STATE_CHANGE,
                 DEVICE_NOTIFY_SERVICE_HANDLE);
        }

        private IntPtr MessageHandler(int dwControl, int dwEventType, IntPtr lpEventData, IntPtr lpContext)
        {
            // If dwControl is SERVICE_CONTROL_POWEREVENT
            // and dwEventType is PBT_POWERSETTINGCHANGE
            // then lpEventData is a pointer to a POWERBROADCAST_SETTING struct
            // Ref. https://msdn.microsoft.com/en-us/library/ms683241(v=vs.85).aspx
            if (dwControl == SERVICE_CONTROL_POWEREVENT && dwEventType == PBT_POWERSETTINGCHANGE)
            {
                var ps = (POWERBROADCAST_SETTING) Marshal.PtrToStructure(lpEventData, typeof(POWERBROADCAST_SETTING));

                Log("{0}: Power setting: {1}", DateTime.Now, ps.PowerSetting);

                if (ps.PowerSetting == GUID_LIDSWITCH_STATE_CHANGE)
                {
                    var isLidOpen = ps.Data != 0;

                    Log("{0}: Lid data received: {1}", DateTime.Now, ps.Data);

                    LidStatusChanged(isLidOpen);
                }
            }

            return IntPtr.Zero;
        }

        private void LidStatusChanged(bool isLidOpen)
        {
            if (isLidOpen)
            {
                Log("{0}: Lid opened!", DateTime.Now);
            }
            else
            {
                Log("{0}: Lid closed!", DateTime.Now);
            }
        }

        protected override void OnStart(string[] args)
        {
            RegisterForPowerNotifications();
            RegisterServiceCtrlHandlerEx(ServiceName, MessageHandler, IntPtr.Zero);

            Log("{0}: Service Running", DateTime.Now);
        }

        protected override void OnStop()
        {
            UnregisterPowerSettingNotification(_powerSettingsNotificationHandle);
        }

        private void Log(string format, params object[] arg)
        {
            using (var sw = new StreamWriter(@"log.txt", true))
            {
                sw.WriteLine(format, arg);
            }
        }
    }
}
