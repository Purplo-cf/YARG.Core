//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace YARG.Core
{
    public static class YargMath
    {
        #region Lerp
        public static int Lerp(int start, int end, float percent)
        {
            return (int) (start + (end - start) * percent);
        }

        public static uint Lerp(uint start, uint end, float percent)
        {
            return (uint) (start + (end - start) * percent);
        }

        public static float Lerp(float start, float end, float percent)
        {
            return (float) (start + (end - start) * percent);
        }

        public static double Lerp(double start, double end, float percent)
        {
            return (double) (start + (end - start) * percent);
        }

        public static int Lerp(int start, int end, double percent)
        {
            return (int) (start + (end - start) * percent);
        }

        public static uint Lerp(uint start, uint end, double percent)
        {
            return (uint) (start + (end - start) * percent);
        }

        public static float Lerp(float start, float end, double percent)
        {
            return (float) (start + (end - start) * percent);
        }

        public static double Lerp(double start, double end, double percent)
        {
            return (double) (start + (end - start) * percent);
        }

        public static float LerpF(int start, int end, float percent)
        {
            return start + (end - start) * percent;
        }

        public static float LerpF(uint start, uint end, float percent)
        {
            return start + (end - start) * percent;
        }

        public static double LerpD(int start, int end, double percent)
        {
            return start + (end - start) * percent;
        }

        public static double LerpD(uint start, uint end, double percent)
        {
            return start + (end - start) * percent;
        }

        #endregion

        #region InverseLerp
        public static float InverseLerpF(int start, int end, int value)
        {
            return (value - start) / (end - start);
        }

        public static float InverseLerpF(uint start, uint end, uint value)
        {
            return (value - start) / (end - start);
        }

        public static float InverseLerpF(float start, float end, float value)
        {
            return (value - start) / (end - start);
        }

        public static double InverseLerpD(int start, int end, int value)
        {
            return (value - start) / (end - start);
        }

        public static double InverseLerpD(uint start, uint end, uint value)
        {
            return (value - start) / (end - start);
        }

        public static double InverseLerpD(double start, double end, double value)
        {
            return (value - start) / (end - start);
        }

        #endregion
    }
}