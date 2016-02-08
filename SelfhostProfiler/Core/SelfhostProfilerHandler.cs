using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ServiceStack.MiniProfiler;
using ServiceStack.MiniProfiler.UI;
using ServiceStack.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace SelfhostProfiler.Core
{
    /// <summary>
    /// Override default handler
    /// </summary>
    public sealed class SelfhostProfilerHandler : MiniProfilerHandler
    {
        /// <summary>
        /// Check if request can be handled, new path for this handler
        /// </summary>
        /// <param name="request"></param>
        public new static IHttpHandler MatchesRequest(IHttpRequest request)
        {
            var file = GetFileNameWithoutExtension(request.PathInfo);
            return file != null && file.StartsWith("profiler-")
                ? new SelfhostProfilerHandler()
                : null;
        }

        /// <summary>
        /// Only manage full page results
        /// </summary>
        /// <param name="httpReq"></param>
        /// <param name="httpRes"></param>
        /// <param name="operationName"></param>
        public override void ProcessRequest(IRequest httpReq, IResponse httpRes, string operationName)
        {
            var path = httpReq.PathInfo;

            string output;
            switch (Path.GetFileNameWithoutExtension(path))
            {
                case "profiler-results":
                    output = Results(httpReq, httpRes);
                    break;

                default:
                    output = NotFound(httpRes);
                    break;
            }

            httpRes.Write(output);
        }

        /// <summary>
        /// Handles rendering a previous MiniProfiler session, identified by its "?id=GUID" on the query.
        /// </summary>
        private static string Results(IRequest httpReq, IResponse httpRes)
        {
            // this guid is the MiniProfiler.Id property
            var id = new Guid();
            if (!Guid.TryParse(httpReq.QueryString["id"], out id))
            {
                return NotFound(httpRes, "text/plain", "No Guid id specified on the query string");
            }

            // load profiler
            var profiler = Profiler.Settings.Storage.Load(id);
            if (profiler == null)
            {
                return NotFound(httpRes, "text/plain", "No MiniProfiler results found with Id=" + id.ToString());
            }

            // ensure that callers have access to these results
            var authorize = Profiler.Settings.Results_Authorize;
            if (authorize != null && !authorize(httpReq, profiler))
            {
                httpRes.StatusCode = 401;
                httpRes.ContentType = "text/plain";
                return "Unauthorized";
            }

            // Only manage full page
            return ResultsFullPage(httpRes, profiler);
        }

        /// <summary>
        /// Build full page result
        /// </summary>
        /// <param name="httpRes"></param>
        /// <param name="profiler"></param>
        /// <returns></returns>
        private static string ResultsFullPage(IResponse httpRes, Profiler profiler)
        {
            httpRes.ContentType = "text/html";
            return new StringBuilder()
                .AppendLine("<html><head>")
                .AppendFormat("<title>{0} ({1} ms) - MvcMiniProfiler Results</title>", profiler.Name, profiler.DurationMilliseconds)
                .AppendLine()
                .AppendLine("<script type='text/javascript' src='https://ajax.googleapis.com/ajax/libs/jquery/1.6.2/jquery.min.js'></script>")
                .Append("<script type='text/javascript'> var profiler = ")

                //.Append(Profiler.ToJson(profiler)) // There is a problem, property ElapsedTicks can't be serialized

                .Append(SerializeProfiler(profiler))
                .AppendLine(";</script>")
                .Append(RenderIncludes(profiler)) // figure out how to better pass display options
                .AppendLine("</head><body><div class='profiler-result-full'></div></body></html>")
                .ToString();
        }

        /// <summary>
        /// Profiler JSON serialization. It's better to use exclusion filter than inclusion, this implementation was just for testing purpose.
        /// </summary>
        /// <param name="profiler"></param>
        /// <returns></returns>
        private static string SerializeProfiler(Profiler profiler)
        {
            var jss = new JsonSerializerSettings();
            jss.ContractResolver = new DynamicContractResolver(new List<string>()
            {
                "Id", "Name", "Started", "MachineName", "Level", "Root", "User", "HasUserViewed", "DurationMilliseconds",
                "HasTrivialTimings", "HasAllTrivialTimings", "TrivialDurationThresholdMilliseconds", "StartMilliseconds",
                "Children", "KeyValues", "SqlTimings", "ParentTimingId", "ExecuteType", "CommandString", "FormattedCommandString",
                "StackTraceSnippet", "FirstFetchDurationMilliseconds", "Parameters", "IsDuplicate", "DurationMillisecondsInSql",
                "ExecutedNonQueries", "ExecutedReaders", "ExecutedScalars", "HasDuplicateSqlTimings", "HasSqlTimings", "Depth",
                "DurationMilliseconds", "DurationWithoutChildrenMilliseconds", "HasChildren", "IsRoot", "IsTrivial", "KeyValues",
                "SqlTimingsDurationMilliseconds",
                //"ElapsedTicks"
            });
            return JsonConvert.SerializeObject(profiler, jss);
        }

        /// <summary>
        /// Méthode helper qui retourne une erreur 404.
        /// </summary>
        private static string NotFound(IResponse httpRes, string contentType = "text/plain", string message = null)
        {
            httpRes.StatusCode = 404;
            httpRes.ContentType = contentType;

            return message;
        }

        /// <summary>
        /// Custom contract resolver
        /// </summary>
        public class DynamicContractResolver : DefaultContractResolver
        {
            private IList<string> _propertiesToSerialize = null;

            public DynamicContractResolver(IList<string> propertiesToSerialize)
            {
                _propertiesToSerialize = propertiesToSerialize;
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                IList<JsonProperty> properties = base.CreateProperties(type, memberSerialization);
                return properties.Where(p => _propertiesToSerialize.Contains(p.PropertyName)).ToList();
            }
        }
    }
}
