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
    }
}