//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace JoinServerUploader
{
    /// <summary>
    /// Custom JSON converter returning the underlying raw json (avoiding object allocation)
    /// </summary>
    internal class RawStringConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value != null)
            {
                writer.WriteRawValue((string)value);
            }
        }
    }
}