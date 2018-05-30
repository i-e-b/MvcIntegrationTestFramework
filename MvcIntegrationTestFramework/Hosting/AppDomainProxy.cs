using System;
using MvcIntegrationTestFramework.Browsing;

namespace MvcIntegrationTestFramework.Hosting
{
    /// <summary>
    /// Simply provides a remoting gateway to execute code within the ASP.NET-hosting appdomain
    /// </summary>
    internal class AppDomainProxy : MarshalByRefObject
    {
        public void RunCodeInAppDomain(Action codeToRun)
        {
            codeToRun();
        }
        
        public void RunCodeInAppDomain<T>(Action<T> codeToRun, T data)
        {
            codeToRun(data);
        }

        public void RunBrowsingSessionInAppDomain(SerializableDelegate<Action<BrowsingSession>> script)
        {
            var browsingSession = new BrowsingSession();
            script.Delegate(browsingSession);
        }

        public override object InitializeLifetimeService()
        {
            return null; // Tells .NET not to expire this remoting object
        }

        public AppDomain CurrentDomain()
        {
            return AppDomain.CurrentDomain;
        }
    }
}