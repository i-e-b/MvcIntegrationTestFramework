using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Optimization;
using MvcIntegrationTestFramework.Browsing;
using MvcIntegrationTestFramework.Interception;

namespace MvcIntegrationTestFramework.Hosting
{
    /// <summary>
    /// Hosts an ASP.NET application within an ASP.NET-enabled .NET appdomain
    /// and provides methods for executing test code within that appdomain
    /// </summary>
    public class AppHost : IDisposable
    {
        /// <summary>
        /// If set to true, all the binaries from the test folder will be loaded into the MVC project.
        /// If set to false, only essential binaries are copied. Defaults to false
        /// </summary>
        public static bool LoadAllBinaries = false;

        /// <summary>
        /// If set to true, the proxy will try to resolve assembly mismatches by name alone.
        /// If set to fale, default assembly resolving will be used. Defaults to false
        /// </summary>
        public static bool IgnoreVersions = false;

        private readonly AppDomainProxy _appDomainProxy; // The gateway to the ASP.NET-enabled .NET appdomain

        private AppHost(string appPhysicalDirectory, string virtualDirectory)
        {
            _appDomainProxy = (AppDomainProxy) ApplicationHost.CreateApplicationHost(typeof(AppDomainProxy), virtualDirectory, appPhysicalDirectory);

            if (IgnoreVersions)
            {
                AddAssemblyResolver(appPhysicalDirectory);
            }

            _appDomainProxy.RunCodeInAppDomain(() =>
            {
                InitializeApplication();
                FilterProviders.Providers.Add(new InterceptionFilterProvider());
                LastRequestData.Reset();
            });
        }

        private void AddAssemblyResolver(string appPhysicalDirectory)
        {
            _appDomainProxy.RunCodeInAppDomain(baseDir =>
            {
                var closeList = new HashSet<string>();
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    var tokens = args.Name.Split(",".ToCharArray());
                    string assemblyCulture;
                    string assemblyName = tokens[0];
                    string assemblyFileName = assemblyName.Replace(".resources", "") + ".dll";
                    string assemblyPath;
                    if (tokens.Length < 2) assemblyCulture = null;
                    else assemblyCulture = tokens[2].Substring(tokens[2].IndexOf('=') + 1);

                    if (assemblyName.EndsWith(".resources"))
                    {
                        // Specific resources are located in app subdirectories
                        string resourceDirectory = Path.Combine(baseDir, assemblyCulture ?? "en");

                        assemblyPath = Path.Combine(resourceDirectory, assemblyName + ".dll");
                        if (File.Exists(assemblyPath)) return Assembly.LoadFile(assemblyPath);
                    }

                    assemblyPath = Path.Combine(baseDir, assemblyFileName);
                    if (!File.Exists(assemblyPath)) {
                        Console.WriteLine("Basis path is wrong?");
                        return null;
                    }

                    if (closeList.Contains(assemblyFileName)){
                        Console.WriteLine("Duplicate request");
                        return null;
                    }

                    try
                    {
                        closeList.Add(assemblyFileName);


                        var found = Assembly.LoadFile(assemblyPath);


                        return found;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        return null;
                    }
                };
            }, Path.Combine(appPhysicalDirectory, "bin"));
        }

        /// <summary>
        /// Run a set of test actions in the ASP.Net application domain.
        /// BrowsingSession object is connected to the MVC project supplied to the `Simulate` method.
        /// </summary>
        public void Start(Action<BrowsingSession> testScript)
        {
            var serializableDelegate = new SerializableDelegate<Action<BrowsingSession>>(testScript);
            _appDomainProxy.RunBrowsingSessionInAppDomain(serializableDelegate);
        }

        /// <summary>
        /// Creates an instance of the AppHost so it can be used to simulate a browsing session.
        /// Use the `Start` method on the returned AppHost to communicate with the MVC host.
        /// </summary>
        /// <param name="mvcProjectDirectories">Directory containing the MVC project, relative to the solution base path</param>
        public static AppHost Simulate(params string[] mvcProjectDirectories)
        {
            var caller = Assembly.GetCallingAssembly();
            string mvcProjectPath = null;
            foreach (var mvcProjectDirectory in mvcProjectDirectories)
            {
                mvcProjectPath = GetMvcProjectPath(mvcProjectDirectory);
                if (mvcProjectPath != null) break;
            }
            if (mvcProjectPath == null)
            {
                throw new ArgumentException("The MVC Projects '" + string.Join(", ", mvcProjectDirectories) + "' were not found when searching from '" + AppDomain.CurrentDomain.BaseDirectory + "'");
            }
            CopyDllFiles(mvcProjectPath, caller.Location);
            return new AppHost(mvcProjectPath, "/");
        }
        
        /// <summary>
        /// Shutdown the application host's domain. The AppHost cannot be used after this.
        /// </summary>
        public void Dispose()
        {
            var childDomain = _appDomainProxy.CurrentDomain();
            var maxGen = GC.MaxGeneration;
            try
            {
                GC.Collect(maxGen, GCCollectionMode.Forced, blocking: true);
                AppDomain.Unload(childDomain);
            }
            catch (CannotUnloadAppDomainException)
            {
                GC.Collect(maxGen, GCCollectionMode.Forced, blocking: true);
                AppDomain.Unload(childDomain);
            }
        }

        private static void InitializeApplication()
        {
            BundleTable.VirtualPathProvider = new TestVPP();

            var appInstance = GetApplicationInstance();
            appInstance.PostRequestHandlerExecute += delegate
            {
                // Collect references to context objects that would otherwise be lost
                // when the request is completed
                if (LastRequestData.HttpSessionState == null)
                    LastRequestData.HttpSessionState = HttpContext.Current.Session;
                if (LastRequestData.Response == null)
                    LastRequestData.Response = HttpContext.Current.Response;
            };
            RefreshEventsList(appInstance);

            RecycleApplicationInstance(appInstance);
        }

        private static readonly MethodInfo GetApplicationInstanceMethod;
        private static readonly MethodInfo RecycleApplicationInstanceMethod;

        static AppHost()
        {
            // Get references to some MethodInfos we'll need to use later to bypass nonpublic access restrictions
            var httpApplicationFactory = typeof(HttpContext).Assembly.GetType("System.Web.HttpApplicationFactory", true);
            GetApplicationInstanceMethod = httpApplicationFactory.GetMethod("GetApplicationInstance", BindingFlags.Static | BindingFlags.NonPublic);
            RecycleApplicationInstanceMethod = httpApplicationFactory.GetMethod("RecycleApplicationInstance", BindingFlags.Static | BindingFlags.NonPublic);
        }

        private static HttpApplication GetApplicationInstance()
        {
            var writer = new StringWriter();
            var workerRequest = new SimpleWorkerRequest("", "", writer);
            var httpContext = new HttpContext(workerRequest);
            httpContext.User = null;

            // This can fail with "BuildManager.EnsureTopLevelFilesCompiled This method cannot be called during the application's pre-start initialization phase"
            //   at System.Web.Compilation.BuildManager.EnsureTopLevelFilesCompiled()
            //at System.Web.Compilation.BuildManager.GetGlobalAsaxTypeInternal()
            //at System.Web.HttpApplicationFactory.CompileApplication()
            //at System.Web.HttpApplicationFactory.Init()
            //at System.Web.HttpApplicationFactory.EnsureInited()
            //at System.Web.HttpApplicationFactory.GetApplicationInstance(HttpContext context)

            // I've seen this with SimpleInjector's
            // [assembly: WebActivator.PostApplicationStartMethod(...)]
            // start-up code. Removing this fixes the error.

            return (HttpApplication) GetApplicationInstanceMethod.Invoke(null, new object[] {httpContext});
        }

        private static void RecycleApplicationInstance(HttpApplication appInstance)
        {
            RecycleApplicationInstanceMethod.Invoke(null, new object[] {appInstance});
        }

        private static void RefreshEventsList(HttpApplication appInstance)
        {
            var stepManagerField = typeof(HttpApplication).GetField("_stepManager", BindingFlags.NonPublic | BindingFlags.Instance);
            var resumeStepsWaitCallbackField = typeof(HttpApplication).GetField("_resumeStepsWaitCallback", BindingFlags.NonPublic | BindingFlags.Instance);

            if (stepManagerField == null || resumeStepsWaitCallbackField == null) throw new Exception("Expected fields were not present on HttpApplication type. This version of MvcTestIntegrationFramework may not be suitable for your project.");

            var stepManager = stepManagerField.GetValue(appInstance);
            var resumeStepsWaitCallback = resumeStepsWaitCallbackField.GetValue(appInstance);
            var buildStepsMethod = stepManager.GetType().GetMethod("BuildSteps", BindingFlags.NonPublic | BindingFlags.Instance);
            buildStepsMethod?.Invoke(stepManager, new[] {resumeStepsWaitCallback});
        }

        /// <summary>
        /// Copy the test files into the MVC project path
        /// </summary>
        private static void CopyDllFiles(string mvcProjectPath, string callerLocation)
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var callerFile = Path.GetFileName(callerLocation);

            foreach (var file in Directory.GetFiles(baseDirectory, "*.dll"))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(mvcProjectPath, "bin", fileName);

                var knownAssemblies = new[] {"MvcIntegrationTestFramework.dll", "nunit.framework.dll"};

                if (knownAssemblies.Contains(fileName) // update the test assembly
                    || fileName == callerFile) // bring tests along
                {
                    File.Copy(file, destFile, true);
                }
                else if (LoadAllBinaries)
                {
                    if (!File.Exists(destFile) || File.GetCreationTimeUtc(destFile) != File.GetCreationTimeUtc(file))
                        File.Copy(file, destFile, true);
                }
            }
            Thread.Sleep(500);
        }

        private static string GetMvcProjectPath(string mvcProjectName)
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            while (Path.GetPathRoot(baseDirectory) != baseDirectory)
            {
                baseDirectory = baseDirectory.Substring(0, baseDirectory.LastIndexOf("\\", StringComparison.Ordinal));
                if (baseDirectory.Length < 3) return null; // Safety check for TFS
                var mvcPath = Path.Combine(baseDirectory, mvcProjectName);
                if (Directory.Exists(mvcPath))
                {
                    return mvcPath;
                }
            }
            return null;
        }
    }
}