using Funq;
using ServiceStack;
using ServiceStack.MiniProfiler;
using ServiceStack.MiniProfiler.Storage;
using ServiceStack.Text;
using ServiceStack.Web;
using System;
using System.Net;
using System.Reflection;

namespace SelfhostProfiler.Core
{
    public sealed class Selfhost : AppSelfHostBase
    {
        #region Constructor

        /// <summary>
        /// Initialize new custom selfhost.
        /// </summary>
        public Selfhost(string applicationName, Assembly[] assembliesWithServices)
            : base(applicationName, assembliesWithServices)
        { }

        #endregion

        #region Public Methods

        /// <summary>
        /// Configure the container.
        /// </summary>
        /// <param name="container">The built-in IoC used with ServiceStack.</param>
        public override void Configure(Container container)
        {
            JsConfig<Guid>.SerializeFn = guid => guid.ToString().ToUpper();
            JsConfig<Guid?>.SerializeFn = guid => guid == null ? null : guid.ToString().ToUpper();

            // Custom provider
            Profiler.Settings.ProfilerProvider = new SelfhostRequestProfilerProvider();

            // SQL storage
            Profiler.Settings.Storage = new SqlServerStorage("Server=LOCALHOST; Database=Test;");

            Profiler.Settings.IgnoredPaths = new string[] { "/ssr-", "/content/", "/scripts/", "/favicon.ico", "/profiler-" };
            Profiler.Settings.Results_Authorize = (x, y) => true;
            Profiler.Settings.PopupShowTimeWithChildren = true;
            Profiler.Settings.ShowControls = true;

            // Custom handler for result display page
            RawHttpHandlers.Add(httpReq => SelfhostProfilerHandler.MatchesRequest(httpReq));
        }

        #endregion

        #region AppSelfHostBase implementation

        /// <summary>
        /// Called on request beginning
        /// </summary>
        /// <param name="context"></param>
        protected override void OnBeginRequest(HttpListenerContext context)
        {
            // The only way I found to store/access the http context
            RequestContext.Instance.Items["Context"] = context;

            // Profile only local request (you can also use configuration)
            if (context.Request.IsLocal)
            {
                // Start profiling request
                Profiler.Start(ProfileLevel.Verbose);
            }

            base.OnBeginRequest(context);
        }

        /// <summary>
        /// Called on request ending
        /// </summary>
        /// <param name="request"></param>
        public override void OnEndRequest(IRequest request = null)
        {
            // Stop profiling request
            Profiler.Stop(false);

            base.OnEndRequest(request);
        }

        #endregion
    }
}
