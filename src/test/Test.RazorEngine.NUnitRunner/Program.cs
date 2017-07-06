using NUnitLite;

namespace Test.RazorEngine.NUnitRunner
{
    class Program
    {
        static int Main(string[] args)
        {
            return new AutoRun(typeof(RazorEngineServiceTestFixture).Assembly).Execute(args);
        }
    }
}
