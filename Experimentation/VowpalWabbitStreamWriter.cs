using System.IO;
using System.Text;
using VW;
using VW.Serializer;

namespace Experimentation
{
    public class VowpalWabbitStreamWriter : StreamWriter
    {
        private VowpalWabbit vw;

        public VowpalWabbitStreamWriter(Stream stream, Encoding encoding, string arguments) : base(stream, encoding)
        {
            vw = new VowpalWabbit(new VowpalWabbitSettings { Arguments = arguments, EnableStringExampleGeneration = true, EnableStringFloatCompact = true });
        }

        public override void WriteLine(string value)
        {
            using (var jsonSerializer = new VowpalWabbitJsonSerializer(vw))
            using (var example = jsonSerializer.ParseAndCreate(value))
            {
                if (example == null)
                    throw new InvalidDataException($"Invalid example: {value}");

                var str = example.VowpalWabbitString;
                if (example is VowpalWabbitMultiLineExampleCollection)
                    str += "\n";

                base.WriteLine(str);
            }
        }

        public override void Close()
        {
            vw.Dispose();
            base.Close();
        }
    }
}
