using Microsoft.Analytics.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DecisionServiceExtractor
{
    internal class CcbParser
    {
        ColumnInfo EventIdColumn;
        ColumnInfo SlotIdxColumn;
        ColumnInfo SessionIdColumn;
        ColumnInfo ParserErrorColumn;

        internal CcbParser(ISchema schema)
        {
            this.EventIdColumn = new ColumnInfo(schema, "EventId", typeof(string));
            this.SlotIdxColumn = new ColumnInfo(schema, "SlotIdx", typeof(int));
            this.SessionIdColumn = new ColumnInfo(schema, "SessionId", typeof(string));
            this.ParserErrorColumn = new ColumnInfo(schema, "ParseError", typeof(string));
        }

        //called on every line
        public IEnumerable<IRow> ParseEvent(IUpdatableRow output, Stream input)
        {
            output.Set(this.ParserErrorColumn, string.Empty);

            TextReader inputReader;
            bool firstPass = true;
            int slotIdx = 0;
            inputReader = new StreamReader(input, Encoding.UTF8);
            output.Set(this.SessionIdColumn, Guid.NewGuid().ToString());

            string errorMessage = null;
            // this is a optimized version only parsing parts of the data
            using (var jsonReader = new JsonTextReader(inputReader))
            {
                jsonReader.DateFormatString = "o";
                while (SafeJsonReader.Read(jsonReader, ref errorMessage))
                {
                    switch (jsonReader.TokenType)
                    {
                        case JsonToken.PropertyName:
                            {
                                var propertyName = (string)jsonReader.Value;

                                switch (propertyName)
                                {
                                    case "_outcomes":
                                        break;
                                    case "_id":
                                        if (!firstPass) yield return output.AsReadOnly();
                                        firstPass = false;
                                        Helpers.ExtractPropertyString(jsonReader, output, this.EventIdColumn);
                                        output.Set(this.SlotIdxColumn, slotIdx++);
                                        break;
                                    case "Timestamp":
                                   /*     output.Set(this.)
                                        if (this.hasTimestamp)
                                            output.Set(this.idxTimestamp, (DateTime)jsonReader.ReadAsDateTime());*/
                                        break;
                                    default:
                                        jsonReader.Skip();
                                        break;
                                }
                            }
                            break;
                    }
                }
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    output.Set(this.ParserErrorColumn, errorMessage);
                }
            }

            yield return output.AsReadOnly();
        }
    }

}
