﻿using Microsoft.Analytics.Interfaces;
using Newtonsoft.Json;
using System;

namespace DecisionServiceExtractor
{
    internal static class Helpers
    {
        public static bool HasColumnOfType(ISchema schema, string name, Type t, out int idx)
        {
            idx = schema.IndexOf(name);
            if (idx < 0)
                return false;

            var column = schema[idx];

            // make sure we can write
            if (column.IsReadOnly)
                return false;

            // make sure it's the proper type
            return column.Type == t;
        }

        public static void ExtractPropertyString(JsonTextReader jsonReader, IUpdatableRow output, ColumnInfo columnInfo)
        {
            jsonReader.Read();
            output.Set(columnInfo, (string)jsonReader.Value);
        }

        public static void ExtractPropertyBool(JsonTextReader jsonReader, IUpdatableRow output, ColumnInfo columnInfo)
        {
            jsonReader.Read();
            output.Set(columnInfo, (bool)jsonReader.Value);
        }

        public static void ExtractPropertyInteger(JsonTextReader jsonReader, IUpdatableRow output, ColumnInfo columnInfo)
        {
            jsonReader.Read();
            output.Set(columnInfo, (int)(long)jsonReader.Value);
        }

        public static void ExtractPropertyDouble(JsonTextReader jsonReader, IUpdatableRow output, ColumnInfo columnInfo)
        {
            jsonReader.Read();

            if (columnInfo.IsRequired)
            {
                switch (jsonReader.TokenType)
                {
                    case JsonToken.Integer:
                        output.Set(columnInfo.Idx, (float)(long)jsonReader.Value);
                        break;
                    case JsonToken.Float:
                        output.Set(columnInfo.Idx, (float)(double)jsonReader.Value);
                        break;
                    default:
                        throw new Exception("wrong data type");
                }
            }
        }

        public static int CountArrayElements(JsonTextReader jsonReader)
        {
            int numActions = 0;

            while (jsonReader.Read())
            {
                switch (jsonReader.TokenType)
                {
                    case JsonToken.Integer:
                        numActions++;
                        break;
                    case JsonToken.EndArray:
                        return numActions;
                }
            }

            return numActions;
        }

        public static void Set<T>(this IUpdatableRow row, ColumnInfo columnInfo, T value)
        {
            if (columnInfo.IsRequired)
            {
                row.Set(columnInfo.Idx, value);
            }
        }
    }
}
