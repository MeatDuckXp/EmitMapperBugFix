using NUnit.Framework;

namespace EmitMapperTests.NUnit
{
    [TestFixture]
    public class MapWithObjectState
    {
        private class A1
        { 
            public string Filed1 { get; set; }

            public string Filed2 { get; set; }

            public string Filed3 { get; set; }
        }

        private class A2
        {
            public string Filed1 { get; set; }

            public string Filed2 { get; set; }

            public string Filed3 { get; set; }
        }

        private class B2
        {
            public string fld2 = "B2::fld2";
            public string fld3 = "B2::fld3";
        }

        [Test]
        public void Test_ConvertWithObjectState()
        {
            
        }
    }
}
