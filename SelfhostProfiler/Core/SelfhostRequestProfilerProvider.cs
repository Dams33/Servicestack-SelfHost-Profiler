using ServiceStack;
using ServiceStack.MiniProfiler;
using System.Net;

namespace SelfhostProfiler.Core
{
    /// <summary>
    /// Custom provider for System.Net.HttpListennerRequest
    /// </summary>
    public sealed partial class SelfhostRequestProfilerProvider : BaseProfilerProvider
    {
        #region Variables & Constants

        private const string CacheKey = ":mini-profiler:";

        private string _remoteIp;
        private HttpListenerRequest _request;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the currently running MiniProfiler for the current HttpContext; null if no MiniProfiler was <see cref="Start"/>ed.
        /// </summary>
        private Profiler Current
        {
            get
            {
                return RequestContext.Instance.Items[CacheKey] as Profiler;
            }
            set
            {
                RequestContext.Instance.Items[CacheKey] = value;
            }
        }

        private string RemoteIp
        {
            get
            {
                return _remoteIp ?? (_remoteIp = XForwardedFor ?? (XRealIp ?? Request.UserHostAddress));
            }
        }

        private HttpListenerRequest Request
        {
            get
            {
                return _request ?? (_request = (RequestContext.Instance.Items["Context"] as HttpListenerContext)?.Request);
            }
        }

        private string XForwardedFor
        {
            get
            {
                return string.IsNullOrEmpty(Request.Headers[HttpHeaders.XForwardedFor]) ? null : Request.Headers[HttpHeaders.XForwardedFor];
            }
        }

        private string XRealIp
        {
            get
            {
                return string.IsNullOrEmpty(Request.Headers[HttpHeaders.XRealIp]) ? null : Request.Headers[HttpHeaders.XRealIp];
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts a new MiniProfiler and associates it with the current <see cref="HttpContext.Current"/>.
        /// </summary>
        public override Profiler Start(ProfileLevel level)
        {
            if (Request == null) return null;

            var path = Request.RawUrl;

            // don't profile /content or /scripts, either - happens in web.dev
            foreach (var ignored in Profiler.Settings.IgnoredPaths ?? new string[0])
            {
                if (path.ToUpperInvariant().Contains((ignored ?? "").ToUpperInvariant()))
                    return null;
            }

            var result = new Profiler(Request.Url.ToString(), level);
            Current = result;

            SetProfilerActive(result);

            // I'm not managing user yet
            result.User = RemoteIp;

            return result;
        }

        /// <summary>
        /// Ends the current profiling session, if one exists.
        /// </summary>
        /// <param name="discardResults">
        /// When true, clears the <see cref="DolistProfiler.Current"/> for this HttpContext, allowing profiling to 
        /// be prematurely stopped and discarded. Useful for when a specific route does not need to be profiled.
        /// </param>
        public override void Stop(bool discardResults)
        {
            if (Request == null)
                return;

            var current = Current;
            if (current == null)
                return;

            // stop our timings - when this is false, we've already called .Stop before on this session
            if (!StopProfiler(current))
                return;

            if (discardResults)
            {
                Current = null;
                return;
            }

            // set the profiler name to Controller/Action or /url
            EnsureName(current, Request);

            // save the profiler
            SaveProfiler(current);
        }

        /// <summary>
        /// Returns the current profiler
        /// </summary>
        /// <returns></returns>
        public override Profiler GetCurrentProfiler()
        {
            return Current;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Makes sure 'profiler' has a Name, pulling it from route data or url. Add verb for rest api profiling
        /// </summary>
        private static void EnsureName(Profiler profiler, HttpListenerRequest request)
        {
            // also set the profiler name to Controller/Action or /url
            if (string.IsNullOrWhiteSpace(profiler.Name))
            {
                profiler.Name = string.Format("{0} {1}", request.HttpMethod, request.RawUrl);
                if (profiler.Name.Length > 70)
                    profiler.Name = profiler.Name.Remove(70);
            }
        }

        #endregion
    }
}
