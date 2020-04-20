using Microsoft.Analytics.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DecisionServiceExtractor
{
    internal class CcbParser
    {
        private class SharedFields
        {
            public ColumnInfo SessionIdColumn { get; private set; }

            public ColumnInfo TimestampColumn { get; private set; }

            public ColumnInfo NumActionsColumn { get; private set; }

            public ColumnInfo PdropColumn { get; private set; }

            public string SessionId { get; set; }

            public DateTime Timestamp { get; set; }

            public int NumActions { get; set; }

            public float pdrop { get; set; } 

            public SharedFields(ISchema schema)
            {
                this.SessionIdColumn = new ColumnInfo(schema, "SessionId", typeof(string));
                this.TimestampColumn = new ColumnInfo(schema, "Timestamp", typeof(DateTime));
                this.NumActionsColumn = new ColumnInfo(schema, "NumActions", typeof(int));
                this.PdropColumn = new ColumnInfo(schema, "pdrop", typeof(float));
            }

            public IRow Apply(IUpdatableRow row)
            {
                row.Set(this.SessionIdColumn, this.SessionId);
                row.Set(this.TimestampColumn, this.Timestamp);
                row.Set(this.NumActionsColumn, this.NumActions);
                row.Set(this.PdropColumn, this.pdrop);

                return row.AsReadOnly();
            }
        }

        private readonly ColumnInfo EventIdColumn;
        private readonly ColumnInfo SlotIdxColumn;
        private readonly ColumnInfo ParserErrorColumn;
        private readonly ColumnInfo CostColumn;
        private readonly ColumnInfo ProbabilityColumn;
        private readonly ColumnInfo ActionColumn;
        private readonly ColumnInfo CbActionColumn;
        private readonly ColumnInfo NumActionsPerSlotColumn;
        private readonly ColumnInfo HasObservationsColumn;
        private readonly ColumnInfo IsDanglingColumn;
        private readonly ColumnInfo EnqueuedTimeUtcColumn;

        internal CcbParser(ISchema schema)
        {
            this.EventIdColumn = new ColumnInfo(schema, "EventId", typeof(string));
            this.SlotIdxColumn = new ColumnInfo(schema, "SlotIdx", typeof(int));
            this.ParserErrorColumn = new ColumnInfo(schema, "ParseError", typeof(string));
            this.CostColumn = new ColumnInfo(schema, "Cost", typeof(float));
            this.ProbabilityColumn = new ColumnInfo(schema, "Prob", typeof(float));
            this.ActionColumn = new ColumnInfo(schema, "Action", typeof(int));
            this.CbActionColumn = new ColumnInfo(schema, "CbAction", typeof(int));
            this.NumActionsPerSlotColumn = new ColumnInfo(schema, "NumActionsPerSlot", typeof(int));
            this.HasObservationsColumn = new ColumnInfo(schema, "HasObservations", typeof(int));
            this.IsDanglingColumn = new ColumnInfo(schema, "IsDangling", typeof(bool));
            this.EnqueuedTimeUtcColumn = new ColumnInfo(schema, "EnqueuedTimeUtc", typeof(DateTime));
        }

        //called on every line
        public IEnumerable<IRow> ParseEvent(IUpdatableRow output, Stream input)
        {
            var shared = new SharedFields(output.Schema);
            output.Set(this.ParserErrorColumn, string.Empty);

            TextReader inputReader;
            bool firstPass = true;
            int slotIdx = 0;
            inputReader = new StreamReader(input, Encoding.UTF8);
            shared.SessionId =  Guid.NewGuid().ToString();

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
                                    case "EventId":
                                        Helpers.ExtractPropertyString(jsonReader, output, this.EventIdColumn);
                                        break;
                                    case "Timestamp":
                                        shared.Timestamp = (DateTime)jsonReader.ReadAsDateTime();
                                        break;
                                    case "_outcomes":
                                        break;
                                    case "_id":
                                        Helpers.ExtractPropertyString(jsonReader, output, this.EventIdColumn);
                                        output.Set(this.SlotIdxColumn, slotIdx++);
                                        break;
                                    case "_label_cost":
                                        if (!firstPass)
                                        {
                                            yield return shared.Apply(output);
                                        }
                                        firstPass = false;
                                        Helpers.ExtractPropertyDouble(jsonReader, output, this.CostColumn);
                                        output.Set(this.IsDanglingColumn, false);
                                        break;
                                    case "EnqueuedTimeUtc":
                                        output.Set(this.EnqueuedTimeUtcColumn, (DateTime)jsonReader.ReadAsDateTime());
                                        output.Set(this.IsDanglingColumn, true);
                                        break;
                                    case "_p":
                                        if (this.ProbabilityColumn.IsRequired)
                                        {
                                            output.Set(this.ProbabilityColumn.Idx, Helpers.EnumerateFloats(jsonReader).First());
                                        }
                                        break;
                                    case "_a":
                                        if (this.ActionColumn.IsRequired || this.NumActionsPerSlotColumn.IsRequired || this.CbActionColumn.IsRequired)
                                        {
                                            var actions = Helpers.EnumerateInts(jsonReader).ToList();
                                            output.Set(this.NumActionsPerSlotColumn, actions.Count);
                                            var chosen = actions[0];
                                            output.Set(this.ActionColumn, chosen);
                                            if (this.CbActionColumn.IsRequired) {
                                                output.Set(this.CbActionColumn, actions.Count(a => a < chosen));
                                            }
                                        }
                                        break;
                                    case "_o":
                                        if (!this.HasObservationsColumn.IsRequired)
                                            jsonReader.Skip();
                                        else
                                        {
                                            if (this.HasObservationsColumn.IsRequired)
                                            {
                                                output.Set(this.HasObservationsColumn.Idx, Helpers.CountObjects(jsonReader) > 0 ? 1 : 0);
                                            }
                                        }
                                        break;
                                    case "c":
                                        if (!shared.NumActionsColumn.IsRequired)
                                        {
                                            jsonReader.Skip();
                                        }
                                        break;
                                    case "_multi":
                                        if (shared.NumActionsColumn.IsRequired)
                                        {
                                            shared.NumActions = Helpers.CountObjects(jsonReader);
                                        }
                                        break;
                                    case "pdrop":
                                        shared.pdrop = Helpers.GetPropertyDouble(jsonReader);
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

            yield return shared.Apply(output);
        }
    }

}
