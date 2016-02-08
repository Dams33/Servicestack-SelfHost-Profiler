using SelfhostProfiler.Core;
using ServiceStack;
using System;
using System.Reflection;

namespace SelfhostProfiler
{
    class Program
    {
        static void Main(string[] args)
        {
            var listeningOn = args.Length == 0 ? "http://*:1337/" : args[0];
            var appHost = new Selfhost("Selfhost Profiler", new Assembly[] { typeof(HelloService).Assembly })
                .Init()
                .Start(listeningOn);

            Console.WriteLine("AppHost Created at {0}, listening on {1}",
                DateTime.Now, listeningOn);

            Console.WriteLine("You can load a profiler with this url: {0}", "/profiler-results?id=");

            Console.ReadKey();
        }

        [Route("/hello/{Name}")]
        public class Hello
        {
            public string Name { get; set; }
        }

        public class HelloResponse
        {
            public string Result { get; set; }
        }

        public class HelloService : Service
        {
            public object Any(Hello request)
            {
                return new HelloResponse { Result = "Hello, " + request.Name };
            }
        }
    }
}
