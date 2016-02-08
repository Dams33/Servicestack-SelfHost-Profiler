using ServiceStack.MiniProfiler;

namespace SelfhostProfiler.Core
{
    public sealed partial class SelfhostRequestProfilerProvider
    {
        /// <summary>
        /// SelfhostRequestProfilerProvider specific configurations
        /// </summary>
        public static class Settings
        {

            /// <summary>
            /// Provides user identification for a given profiling request.
            /// </summary>
            public static IUserProvider UserProvider
            {
                get;
                set;
            }
        }
    }
}
