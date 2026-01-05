using Xunit;
using Xunit.Abstractions;
namespace SharpCompress.Test.BZip2MT
{
    public class TestsRandomData
    {
        private readonly ITestOutputHelper _console;
        protected int _repeatCount = 1;

        public TestsRandomData(ITestOutputHelper console)
        {
            this._console = console;
        }
        
        [Fact]
        public void RandomSingleByteLongTest_CST_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.SingleByteValue, TestCommon.TestMode.CST_DST, 100000000);
        [Fact]
        public void RandomLongTest_CST_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytes, TestCommon.TestMode.CST_DST);
        [Fact]
        public void RandomLongTestWithRepeatedValues_CST_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytesRepeat, TestCommon.TestMode.CST_DST);
        [Fact]
        public void RandomSingleByteLongTest_CMT_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.SingleByteValue, TestCommon.TestMode.CMT_DST, 100000000);
        [Fact]
        public void RandomLongTest_CMT_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytes, TestCommon.TestMode.CMT_DST);
        [Fact]
        public void RandomLongTestWithRepeatedValues_CMT_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytesRepeat, TestCommon.TestMode.CMT_DST);
        
        [Fact]
        public void RandomSingleByteLongTest_CST_DMT() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.SingleByteValue, TestCommon.TestMode.CST_DMT, 100000000);
        [Fact]
        public void RandomLongTest_CST_DMT() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytes, TestCommon.TestMode.CST_DMT);
        [Fact]
        public void RandomLongTestWithRepeatedValues_CST_DMT() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytesRepeat, TestCommon.TestMode.CST_DMT);
        [Fact]
        public void RandomSingleByteLongTest_CMT_DMT() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.SingleByteValue, TestCommon.TestMode.CMT_DMT, 100000000);
        [Fact]
        public void RandomLongTest_CMT_DMT() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytes, TestCommon.TestMode.CMT_DMT);
        [Fact]
        public void RandomLongTestWithRepeatedValues_CMT_DMT() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytesRepeat, TestCommon.TestMode.CMT_DMT);
    }
}
