# LidStatusService #
LidStatusService is a Windows Service for monitoring laptop lid open/close events.

There are a few examples of determining lid open/close events in windowed C# applications [[1]](https://stackoverflow.com/questions/3355606/detect-laptop-lid-closure-and-opening) [[2]](https://github.com/pescuma/vsix-time-tracker/blob/master/Lid.cs) or by using a hidden window in a console application [[3]](http://www.zachburlingame.com/2011/04/capturing-windows-power-events-in-a-console-application/).

But nowhere seems to provide an example of this process using a Windows Service, which is [a little bit different](https://msdn.microsoft.com/en-us/library/windows/desktop/aa373196(v=vs.85).aspx).

## How It Works ##
Monitoring power event changes in Windows requires making a Windows API call to [RegisterPowerSettingNotification](https://msdn.microsoft.com/en-us/library/windows/desktop/aa373196(v=vs.85).aspx), whose signature is:

    HPOWERNOTIFY WINAPI RegisterPowerSettingNotification(
      _In_ HANDLE  hRecipient,
      _In_ LPCGUID PowerSettingGuid,
      _In_ DWORD   Flags
    );

The PowerSettingGuid for lid switch state changes is **GUID_LIDSWITCH_STATE_CHANGE**. The [documentation for Windows Desktop App Development](https://msdn.microsoft.com/en-us/library/hh448380(v=vs.85).aspx) list a few of the most "useful" GUIDs, but to find this one you will have to dig into [WinNT.h](http://www.codemachine.com/downloads/win71/winnt.h).

    // Lid state changes
    // -----------------
    //
    // Specifies the current state of the lid (open or closed). The callback won't
    // be called at all until a lid device is found and its current state is known.
    //
    // Values:
    //
    // 0 - closed
    // 1 - opened
    //
    // { BA3E0F4D-B817-4094-A2D1-D56379E6A0F3 }
    //
    
    DEFINE_GUID( GUID_LIDSWITCH_STATE_CHANGE,  0xBA3E0F4D, 0xB817, 0x4094, 0xA2, 0xD1, 0xD5, 0x63, 0x79, 0xE6, 0xA0, 0xF3 );

The flags argument that RegisterPowerSettingNotification accepts can have values of **DEVICE_NOTIFY_WINDOW_HANDLE** (0) for a windowed application or **DEVICE_NOTIFY_SERVICE_HANDLE** (1) for a service. We have a service, so we will want to call this method using the latter value.

Once we register our service for notifications, we have to define a callback to listen for messages from Windows. We also need to let Windows know about our callback. For a service, the documentation states:

> Notifications are sent to the HandlerEx callback function with a dwControl parameter of SERVICE_CONTROL_POWEREVENT and a dwEventType of PBT_POWERSETTINGCHANGE.

Digging further into Microsoft's documentation reveals theses values:
**SERVICE_CONTROL_POWEREVENT** = [0x0000000D](https://msdn.microsoft.com/en-us/library/windows/desktop/ms683241(v=vs.85).aspx)
**PBT_POWERSETTINGCHANGE** = [0x8013](https://msdn.microsoft.com/en-us/library/windows/desktop/aa372722(v=vs.85).aspx)

The HandlerEx callback has the following signature:

    DWORD WINAPI HandlerEx(
      _In_ DWORD  dwControl,
      _In_ DWORD  dwEventType,
      _In_ LPVOID lpEventData,
      _In_ LPVOID lpContext
    );

This callback can receive other types of events, but for our implementation, we only care about certain types of power events. In our callback, we can filter for these by checking whether dwControl is a **SERVICE_CONTROL_POWEREVENT**, and whether dwEventType is **PBT_POWERSETTINGCHANGE**. The documentation for HandlerEx also states:

> SERVICE_CONTROL_POWEREVENT - Notifies a service of system power events. The dwEventType parameter contains additional information. If dwEventType is PBT_POWERSETTINGCHANGE, the lpEventData parameter also contains additional information.

What is this "additional information"? According to the [documentation](https://msdn.microsoft.com/en-us/library/windows/desktop/aa372722(v=vs.85).aspx) for **PBT_POWERSETTINGCHANGE**, it's a pointer to a **POWERBROADCAST_SETTING** struct.

**POWERBROADCAST_SETTING** has this [signature](https://msdn.microsoft.com/en-us/library/windows/desktop/aa372723(v=vs.85).aspx):

    typedef struct {
      GUID  PowerSetting;
      DWORD DataLength;
      UCHAR Data[1];
    } POWERBROADCAST_SETTING, *PPOWERBROADCAST_SETTING;

Where the Data field is "The new value of the power setting. The type and possible values for this member depend on PowerSetting." 

We know from the documentation in WinNT.h that the callback can have two values, 0 for closed, and 1 for open.

That's about it. We can wrap these ugly Windows API call in a nice class and use it in our Windows service!