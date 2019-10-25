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
        private readonly bool hasEventId;
        private readonly bool hasSlotIdx;
        private readonly bool hasSessionId;

        private readonly bool hasParseError;

        private readonly int idxEventId;
        private readonly int idxSlotIdx;
        private readonly int idxSessionId;

        private readonly int idxParseError;

        internal CcbParser(ISchema schema)
        {
            this.hasSessionId = Helpers.HasColumnOfType(schema, "SessionId", typeof(string), out this.idxSessionId);
            this.hasSlotIdx = Helpers.HasColumnOfType(schema, "SlotIdx", typeof(int), out this.idxSlotIdx);
            this.hasEventId = Helpers.HasColumnOfType(schema, "EventId", typeof(string), out this.idxEventId);
       
            this.hasParseError = Helpers.HasColumnOfType(schema, "ParseError", typeof(string), out idxParseError);
        }

        //called on every line
        public IEnumerable<IRow> ParseEvent(IUpdatableRow output, Stream input)
        {
            if (this.hasParseError)
            {
                output.Set(this.idxParseError, string.Empty);
            }

            TextReader inputReader;
            bool firstPass = true;
            int slotIdx = 0;
            inputReader = new StreamReader(input, Encoding.UTF8);
            output.Set(this.idxSessionId, Guid.NewGuid().ToString());

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
                                        Helpers.ExtractPropertyString(jsonReader, output, this.idxEventId, this.hasEventId);
                                        if (this.hasSlotIdx)
                                        {
                                            output.Set(this.idxSlotIdx, slotIdx++);
                                        }
                                        break;
                                    default:
                                        jsonReader.Skip();
                                        break;
                                }
                            }
                            break;
                    }
                }
                if (!string.IsNullOrEmpty(errorMessage) && this.hasParseError)
                {
                    output.Set(this.idxParseError, errorMessage);
                }
            }

            yield return output.AsReadOnly();
        }
    }

}
