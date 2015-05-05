//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace ClientDecisionServiceTest
{
    /// <summary>
    /// Custom JSON converter returning the underlying raw json (avoiding object allocation)
    /// </summary>
    public class RawStringConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var sb = new StringBuilder();
            JsonToken previousToken = JsonToken.None;

            int depth = 0;

            do
            {
                if (sb.Length > 0)
                {
                    if (!reader.Read())
                    {
                        break;
                    }

                    if ((previousToken == JsonToken.Boolean
                        || previousToken == JsonToken.Date || previousToken == JsonToken.String
                        || previousToken == JsonToken.Float || previousToken == JsonToken.Integer
                        || previousToken == JsonToken.Raw || previousToken == JsonToken.Null
                        || previousToken == JsonToken.Bytes) &&
                        (reader.TokenType != JsonToken.EndArray && reader.TokenType != JsonToken.EndObject))
                    {
                        sb.Append(",");
                    }
                    else if ((previousToken == JsonToken.EndObject && reader.TokenType == JsonToken.StartObject)
                        || (previousToken == JsonToken.EndArray && reader.TokenType == JsonToken.StartArray))
                    {
                        sb.Append(",");
                    }
                }

                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        if (previousToken == JsonToken.EndObject || previousToken == JsonToken.EndArray)
                        {
                            sb.Append(',');
                        }

                        sb.AppendFormat(CultureInfo.InvariantCulture, "\"{0}\":", reader.Value);
                        break;

                    case JsonToken.Bytes:
                    case JsonToken.Comment:
                    case JsonToken.Boolean:
                    case JsonToken.Integer:
                    case JsonToken.Float:
                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0}", reader.Value);
                        break;

                    case JsonToken.Date:
                        sb.Append(JsonConvert.SerializeObject(reader.Value));
                        break;

                    case JsonToken.Null:
                        sb.Append("null");
                        break;

                    case JsonToken.String:
                        sb.AppendFormat(CultureInfo.InvariantCulture, "\"{0}\"", reader.Value);
                        break;

                    case JsonToken.Raw:
                        sb.Append(reader.Value);
                        break;

                    case JsonToken.StartArray:
                        sb.Append('[');
                        depth++;
                        break;

                    case JsonToken.EndArray:
                        sb.Append(']');
                        depth--;
                        break;

                    case JsonToken.StartObject:
                        sb.Append('{');
                        depth++;
                        break;

                    case JsonToken.EndObject:
                        sb.Append('}');
                        depth--;
                        break;
                }

                previousToken = reader.TokenType;
            }
            while (depth > 0);

            return sb.ToString();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteRawValue((string)value);
        }
    }
}