using Microsoft.Analytics.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;

namespace DecisionServiceExtractor
{
    [SqlUserDefinedExtractor(AtomicFileProcessing = false)]
    public class CcbExtractor : IExtractor
    {
        public CcbExtractor()
        {
        }


        public override IEnumerable<IRow> Extract(IUnstructuredReader input, IUpdatableRow output)
        {
            var parser = new CcbParser(output.Schema);
            foreach (Stream current in input.Split((byte)'\n'))
            {
                foreach (var row in parser.ParseEvent(output, current))
                    yield return row;
            }
        }
    }
}
