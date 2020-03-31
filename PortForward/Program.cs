using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Topshelf;

namespace PortForward
{
    class Program
    {
        static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.Service<ForwardService>(s =>
                {
                    s.ConstructUsing(name => new ForwardService());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();

                x.SetDescription("PortForward");
                x.SetDisplayName("PortForward");
                x.SetServiceName("PortForward");
            });

        }
    }
}
