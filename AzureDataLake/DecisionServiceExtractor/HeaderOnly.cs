using Microsoft.Analytics.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DecisionServiceExtractor
{
    [SqlUserDefinedExtractor(AtomicFileProcessing = false)]
    public class HeaderOnly : IExtractor
    {
        private readonly FieldExpression[] expressions;

        public HeaderOnly(params string[] expressions)
        {
            // TODO: error handling
            this.expressions = expressions
                .Select(l =>
                {
                    var index = l.IndexOf(' ');
                    return new FieldExpression { FieldName = l.Substring(0, index), JsonPath = l.Substring(index + 1) };
                })
                .ToArray();
        }


        public override IEnumerable<IRow> Extract(IUnstructuredReader input, IUpdatableRow output)
        {
            var parser = new CbParser(output.Schema, this.expressions);
            int idxParseError;
            bool hasParseError = Helpers.HasColumnOfType(output.Schema, "ParseError", typeof(string), out idxParseError);
            foreach (Stream current in input.Split((byte)'\n'))
            {
                IRow row;
                try
                {
                    if (hasParseError)
                    {
                        output.Set<string>(idxParseError, null);
                    }

                    row = parser.ParseEvent(output, current);
                }
                catch (Exception e)
                {
                    if (hasParseError)
                    {
                        output.Set<string>(idxParseError, $"ParseError: {e.Message}");
                        row = output.AsReadOnly();
                    }
                    else { row = null; }
                }
                if (row != null)
                    yield return row;
            }
        }
    }
}
