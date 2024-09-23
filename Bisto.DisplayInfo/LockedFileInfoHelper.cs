using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Bisto.DisplayInfo
{
    internal class LockedFileInfoHelper
    {
        public const int CCH_RM_MAX_APP_NAME = 255;

        public const int CCH_RM_MAX_SVC_NAME = 63;

        public const int ERROR_MORE_DATA = 234;

        public enum RM_APP_TYPE
        {
            RmUnknownApp = 0,

            RmMainWindow = 1,

            RmOtherWindow = 2,

            RmService = 3,

            RmExplorer = 4,

            RmConsole = 5,

            RmCritical = 1000
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        public static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        public static extern int RmRegisterResources(
            uint pSessionHandle,
            uint nFiles,
            string[] rgsFilenames,
            uint nApplications,
            [In] RM_UNIQUE_PROCESS[] rgApplications,
            uint nServices,
            string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        public static extern int RmGetList(
            uint dwSessionHandle,
            out uint pnProcInfoNeeded,
            ref uint pnProcInfo,
            [In] [Out] RM_PROCESS_INFO[] rgAffectedApps,
            ref uint lpdwRebootReasons);

        public static List<Process> FindLockers(string filename)
        {
            uint sessionHandle;
            string sessionKey = Guid.NewGuid().ToString();
            List<Process> processes = new List<Process>();

            int result = RmStartSession(out sessionHandle, 0, sessionKey);
            if (result != 0)
            {
                throw new Exception("Error starting Restart Manager session.");
            }

            try
            {
                string[] resources = { filename };
                result = RmRegisterResources(sessionHandle, (uint)resources.Length, resources, 0, null, 0, null);
                if (result != 0)
                {
                    throw new Exception("Could not register resource.");
                }

                uint pnProcInfoNeeded = 0;
                uint num_procs = 0;
                uint lpdwRebootReasons = 0;
                result = RmGetList(sessionHandle, out pnProcInfoNeeded, ref num_procs, null, ref lpdwRebootReasons);

                if (result == ERROR_MORE_DATA)
                {
                    RM_PROCESS_INFO[] processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    num_procs = pnProcInfoNeeded;

                    result = RmGetList(
                        sessionHandle,
                        out pnProcInfoNeeded,
                        ref num_procs,
                        processInfo,
                        ref lpdwRebootReasons);
                    if (result == 0)
                    {
                        processes = processInfo.Take((int)num_procs)
                            .Select(p => Process.GetProcessById(p.Process.dwProcessId))
                            .ToList();
                    }
                }
            }
            finally
            {
                RmEndSession(sessionHandle);
            }

            return processes;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;

            public RM_APP_TYPE ApplicationType;

            public uint AppStatus;

            public uint TSSessionId;

            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;

            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }
    }
}
