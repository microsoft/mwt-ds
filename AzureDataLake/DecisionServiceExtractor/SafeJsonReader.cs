using Newtonsoft.Json;
using System;

namespace DecisionServiceExtractor
{
    internal static class SafeJsonReader
    {
        public static bool Read(JsonTextReader reader, ref string errorMessage)
        {
            bool result = false;
            try
            {
                result = reader.Read();
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                return false;
            }
            return result;
        }
    }
}
