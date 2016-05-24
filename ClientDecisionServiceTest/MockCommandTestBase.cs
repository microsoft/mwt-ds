using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class MockCommandTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            joinServer = new MockJoinServer(MockJoinServer.MockJoinServerAddress);

            joinServer.Run();

            commandCenter = new MockCommandCenter();
        }

        [TestCleanup]
        public void CleanUp()
        {
            joinServer.Stop();
        }

        protected MockJoinServer joinServer;
        protected MockCommandCenter commandCenter;
    }
}
