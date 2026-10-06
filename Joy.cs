using System.Runtime.InteropServices;

namespace GamepadLauncher;

/// <summary>Проверка, какие джойстики сейчас подключены (через winmm — тот же список, что в joy.cpl).</summary>
static class Joy
{
    [StructLayout(LayoutKind.Sequential)]
    struct JOYINFOEX
    {
        public uint dwSize, dwFlags, dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos,
                    dwButtons, dwButtonNumber, dwPOV, dwReserved1, dwReserved2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct JOYCAPS
    {
        public ushort wMid, wPid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
        public uint wXmin, wXmax, wYmin, wYmax, wZmin, wZmax, wNumButtons, wPeriodMin, wPeriodMax,
                    wRmin, wRmax, wUmin, wUmax, wVmin, wVmax, wCaps, wMaxAxes, wNumAxes, wMaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szRegKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szOEMVxD;
    }

    [DllImport("winmm.dll")]
    static extern uint joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    static extern uint joyGetDevCapsW(UIntPtr uJoyID, ref JOYCAPS pjc, uint cbjc);

    const uint JOY_RETURNALL = 0xFF;

    /// <summary>Возвращает имена всех подключённых сейчас джойстиков (пусто — ничего не подключено).</summary>
    public static List<string> GetConnected()
    {
        var result = new List<string>();
        for (uint id = 0; id < 16; id++)
        {
            var info = new JOYINFOEX
            {
                dwSize = (uint)Marshal.SizeOf<JOYINFOEX>(),
                dwFlags = JOY_RETURNALL
            };
            if (joyGetPosEx(id, ref info) != 0) continue; // 0 = устройство на связи

            string name = "джойстик #" + id;
            var caps = new JOYCAPS();
            if (joyGetDevCapsW((UIntPtr)id, ref caps, (uint)Marshal.SizeOf<JOYCAPS>()) == 0
                && !string.IsNullOrWhiteSpace(caps.szPname))
                name = caps.szPname;

            result.Add(name);
        }
        return result;
    }
}
