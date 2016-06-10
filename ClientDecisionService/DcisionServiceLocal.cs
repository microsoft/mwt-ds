using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DcisionServiceLocal<TContext>
    {
        public int ChooseAction(string seed, TContext context)
        {
            return 0;
        }

        public void Reward(TContext context, int reward)
        {

        }
    }
}
