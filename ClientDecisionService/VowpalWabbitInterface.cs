using System;
using System.Runtime.InteropServices;

namespace ClientDecisionService
{
    using VwExample = IntPtr;
    using VwHandle = IntPtr;

    public enum VowpalWabbitState
    {
        NotStarted = 0,
        Initialized,
        Finished
    }

    // COMMENT: why don't you model this as object wrapping VwHandle?
    public sealed class VowpalWabbitInterface
    {
        private const string LIBVW = "Include\\libvw.dll";

        [DllImport(LIBVW, EntryPoint = "VW_Initialize")]
        public static extern VwHandle Initialize([MarshalAs(UnmanagedType.LPWStr)]string arguments);

        [DllImport(LIBVW, EntryPoint = "VW_Finish")]
        public static extern void Finish(VwHandle vw);

        [DllImport(LIBVW, EntryPoint = "VW_ReadExample")]
        public static extern VwExample ReadExample(VwHandle vw, [MarshalAs(UnmanagedType.LPWStr)]string exampleString);

        [DllImport(LIBVW, EntryPoint = "VW_FinishExample")]
        public static extern void FinishExample(VwHandle vw, VwExample example);

        [DllImport(LIBVW, EntryPoint = "VW_GetCostSensitivePrediction")]
        public static extern float GetCostSensitivePrediction(VwExample example);

        [DllImport(LIBVW, EntryPoint = "VW_Predict")]
        public static extern float Predict(VwHandle vw, VwExample example);
    }
}
