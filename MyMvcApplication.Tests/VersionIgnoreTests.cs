using System.Web.Mvc;
using MvcIntegrationTestFramework.Browsing;
using MvcIntegrationTestFramework.Hosting;
using NUnit.Framework;

namespace MyMvcApplication.Tests
{
    [TestFixture]
    public class VersionIgnoreTests {
        [Test]
        public void setting_the_ignore_flag_doesnt_break_anything (){

            AppHost.LoadAllBinaries = true;
            AppHost.IgnoreVersions = true;
            using (var appHost = AppHost.Simulate("MyMvcApplication"))
            {
                appHost.Start(browsingSession =>
                {
                    // Request the root URL
                    RequestResult result = browsingSession.Get("");

                    // Can make assertions about the ActionResult...
                    var viewResult = (ViewResult)result.ActionExecutedContext.Result;
                    Assert.AreEqual("Index", viewResult.ViewName);
                    Assert.AreEqual("Welcome to ASP.NET MVC!", viewResult.ViewData["Message"]);

                    // ... or can make assertions about the rendered HTML
                    Assert.IsTrue(result.ResponseText.Contains("<!DOCTYPE html"));
                });
            }
        }
        
        [Test]
        public void using_mismatching_mvc_version_still_works()
        {

            // So, the idea here is that *MvcIntegrationTestFramework* stops caring about the mvc versions it's dependent on.
            // Removing the dependency from the target is difficult and risky.

            AppHost.LoadAllBinaries = true;
            AppHost.IgnoreVersions = true;
            using (var appHost = AppHost.Simulate("MvcUpgraded"))
            {
                appHost.Start(browsingSession =>
                {
                    // Request the root URL
                    RequestResult result = browsingSession.Get("");

                    // Can make assertions about the ActionResult...
                    var viewResult = (ViewResult)result.ActionExecutedContext.Result;
                    Assert.AreEqual("Index", viewResult.ViewName);

                    // ... or can make assertions about the rendered HTML
                    Assert.IsTrue(result.ResponseText.Contains("<!DOCTYPE html"), "Response text was not as expected");
                });
            }
        }
        
        [Test, Explicit]
        public void external_test (){

            AppHost.LoadAllBinaries = false;
            AppHost.IgnoreVersions = true;
            using (var appHost = AppHost.Simulate(@"C:\Temp\WrappedSites\SampleHostedApi"))
            {
                appHost.Start(browsingSession =>
                {
                    // Request the root URL
                    RequestResult result = browsingSession.Get("");

                    // Can make assertions about the ActionResult...
                    var viewResult = (ViewResult)result.ActionExecutedContext.Result;
                    Assert.AreEqual("Index", viewResult.ViewName);
                    Assert.AreEqual("Welcome to ASP.NET MVC!", viewResult.ViewData["Message"]);

                    // ... or can make assertions about the rendered HTML
                    Assert.IsTrue(result.ResponseText.Contains("<!DOCTYPE html"));
                });
            }
        }
    }
}