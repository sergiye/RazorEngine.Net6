using RazorEngine.Templating;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Test.RazorEngine.Core.NetCore, PublicKey="+
"0024000004800000940000000602000000240000525341310004000001000100ad3b3604eb9ba3"+
"17840ece0a65ec22fa67ee54cb4abb5148f184a90d9e9cdbc77c098fe3447ce9e13ef73d3e0460"+
"16e7053f4c5c0ccd9f521514200dd09aa12cedc63bf39c30eb0516ac6b42bb645dfd41902290a8"+
"7ceaf0309a9f08bfdd9cceb27b6186bfbe68ca91dca2508820c0723b0e4d94f3ef8049b8aa3f52"+
"4d4715ca")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Test.RazorEngine.Core.Roslyn.NetCore, PublicKey=" +
"0024000004800000940000000602000000240000525341310004000001000100ad3b3604eb9ba3" +
"17840ece0a65ec22fa67ee54cb4abb5148f184a90d9e9cdbc77c098fe3447ce9e13ef73d3e0460" +
"16e7053f4c5c0ccd9f521514200dd09aa12cedc63bf39c30eb0516ac6b42bb645dfd41902290a8" +
"7ceaf0309a9f08bfdd9cceb27b6186bfbe68ca91dca2508820c0723b0e4d94f3ef8049b8aa3f52" +
"4d4715ca")]

namespace RazorEngine
{
    /// <summary>
    /// Provides quick access to the functionality of the <see cref="RazorEngineService"/> class.
    /// </summary>
    public static class Engine
    {
        private static object _syncLock = new object();
        private static IRazorEngineService _service;

        /// <summary>
        /// Quick access to RazorEngine. See <see cref="IRazorEngineService"/>.
        /// </summary>
        public static IRazorEngineService Razor
        {
            get
            {
                if (_service == null)
                {
                    lock (_syncLock)
                    {
                        if (_service == null)
                        {
                            _service = RazorEngineService.Create();
                        }
                    }
                }
                return _service;
            }
            set
            {
                _service = value;
            }
        }

#if !NO_APPDOMAIN
        private static IRazorEngineService _isolatedService;

        /// <summary>
        /// Quick access to an isolated RazorEngine. See <see cref="IRazorEngineService"/>.
        /// </summary>
        public static IRazorEngineService IsolatedRazor
        {
            get
            {
                if (_isolatedService == null)
                {
                    lock (_syncLock)
                    {
                        if (_isolatedService == null)
                        {
                            _isolatedService = IsolatedRazorEngineService.Create();
                        }
                    }
                }
                return _isolatedService;
            }
            set
            {
                _isolatedService = value;
            }
        }
#endif
    }
}
