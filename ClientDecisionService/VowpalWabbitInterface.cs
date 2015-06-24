using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace ClientDecisionService
{
    using SizeT = IntPtr;
    using VwExample = IntPtr;
    using VwHandle = IntPtr;

    internal enum VowpalWabbitState
    {
        NotStarted = 0,
        Initialized,
        Finished
    }

    internal sealed class VowpalWabbitInterface
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

        [DllImport(LIBVW, EntryPoint = "VW_GetMultilabelPredictions")]
        public static extern IntPtr GetMultilabelPredictions(VwHandle vw, VwExample example, ref SizeT length);

        [DllImport(LIBVW, EntryPoint = "VW_Predict")]
        public static extern float Predict(VwHandle vw, VwExample example);
    }

    internal sealed class VowpalWabbitInstance
    {
        internal VowpalWabbitInstance(string arguments)
        {
            vw = VowpalWabbitInterface.Initialize(arguments);
            vwState = VowpalWabbitState.Initialized;
        }

        internal uint Predict(string exampleLine)
        {
            IntPtr example = VowpalWabbitInterface.ReadExample(vw, exampleLine);
            VowpalWabbitInterface.Predict(vw, example);
            VowpalWabbitInterface.FinishExample(vw, example);

            return (uint)VowpalWabbitInterface.GetCostSensitivePrediction(example);
        }

        internal uint[] PredictMultilabel(string exampleLine)
        {
            IntPtr example = VowpalWabbitInterface.ReadExample(vw, exampleLine);
            VowpalWabbitInterface.Predict(vw, example);
            VowpalWabbitInterface.FinishExample(vw, example);

            IntPtr labelCount = (IntPtr)0;
            IntPtr labelsPtr = VowpalWabbitInterface.GetMultilabelPredictions(vw, example, ref labelCount);

            int[] labels = new int[(int)labelCount];
            Marshal.Copy(labelsPtr, labels, 0, labels.Length);

            return labels.OfType<uint>().ToArray();
        }

        internal void Finish()
        {
            if (vwState == VowpalWabbitState.Initialized)
            {
                VowpalWabbitInterface.Finish(vw);
                vwState = VowpalWabbitState.Finished;
            }
        }

        IntPtr vw;
        VowpalWabbitState vwState;
    }
}
