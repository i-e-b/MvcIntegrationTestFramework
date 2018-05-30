using System;
using System.Collections.Specialized;
using System.Text;
using System.Web.Mvc;
using MvcIntegrationTestFramework.Browsing;
using MvcIntegrationTestFramework.Hosting;
using NUnit.Framework;

namespace MyMvcApplication.Tests
{
    [TestFixture]
    public class HomeControllerTests
    {
        private AppHost appHost;

		[TestFixtureSetUp]
		public void TestFixtureSetUp()
		{
            //If you MVC project is not in the root of your solution directory then include the path
            //e.g. AppHost.Simulate("Website\MyMvcApplication")

		    try
		    {
		        AppHost.LoadAllBinaries = true;
		        appHost = AppHost.Simulate("MyMvcApplication");
		    }
		    catch (Exception e)
		    {
		        Console.WriteLine(e.ToString());
		        throw;
		    }
		}

	    [TestFixtureTearDown]
	    public void TestFixtureTearDown()
	    {
	        appHost.Dispose();
	    }

	    [Test]
		public void Root_Url_Renders_Index_View()
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

	    [Test]
	    public void default_identity_of_http_context()
	    {
	        appHost.Start(session =>
	        {
	            var result = session.Get("Home/WhoAmI");
	            Assert.That(result.ResponseText, Is.EqualTo("GenericIdentity"));
	        });
	    }

	    [Test]
	    public void using_the_wrong_verb_results_in_a_failure_code()
	    {
	        appHost.Start(session =>
	        {
	            var result = session.Post("Home/Index", new {data = "hello"});

	            Assert.That(result.IsClientError);
	        });
	    }

	    [Test]
	    public void querying_a_non_existent_route_gives_a_404()
	    {
	        appHost.Start(session =>
	        {
	            var result = session.Get("/Home/MotTheHoople");
	            Assert.That(result.IsClientError);
	            Assert.That(result.Response.StatusCode, Is.EqualTo(404));
	        });
	    }

	    [Test]
	    public void thrown_exceptions_can_be_caught_and_asserted_against()
	    {
	        appHost.Start(session => {
	            var result = session.Get("/Home/FaultyRoute");

	            Assert.That(result.IsServerError);

	            var thrownException = result.ActionExecutedContext.Exception;
	            Assert.That(thrownException, Is.Not.Null);
	            Assert.That(thrownException.Message, Is.EqualTo("This is a sample exception"));
	        });
	    }

	    [Test]
	    public void can_provide_custom_data_and_HTTP_verbs_in_requests()
	    {
	        appHost.Start(session =>
	        {
	            var headers = new NameValueCollection();
	            headers.Add("Content-Type", "application/json");
	            var bodyData = Encoding.UTF8.GetBytes("{\"Hello\":\"World\"}");
	            var result = session.Request("/Home/Echo", HttpVerbs.Put, headers, bodyData);

	            Assert.That(result.ResponseText, Is.EqualTo("application/json {\"Hello\":\"World\"}"));
	        });
	    }

	    [Test]
		public void session_values_and_cookies_can_be_inpected()
		{
			appHost.Start(browsingSession =>
			{
				string url = "Home/DoStuffWithSessionAndCookies";
			    browsingSession.AddCookie("InputCookie", "inputValue"); // this is a shortcut for `browsingSession.Cookies.Add(new HttpCookie(...));`

				var result = browsingSession.Get(url);

			    Assert.That(result.IsSuccess);

				// Can make assertions about cookies
				Assert.AreEqual("inputValue_Changed", browsingSession.Cookies["mycookie"]?.Value);

				// Can read Session as long as you've already made at least one request
				// (you can also write to Session from your test if you want)
				Assert.AreEqual(1, browsingSession.Session["myIncrementingSessionItem"]);

				// Session values persist within a browsingSession
				browsingSession.Get(url);
				Assert.AreEqual(2, browsingSession.Session["myIncrementingSessionItem"]);
				browsingSession.Get(url);
				Assert.AreEqual(3, browsingSession.Session["myIncrementingSessionItem"]);
			});
		}

		[Test]
		public void Complex_multi_page_interactions__LogInProcess()
		{
			string securedActionUrl = "/Home/SecretAction";

			appHost.Start(browsingSession =>
			{
				// First try to request a secured page without being logged in                
				RequestResult initialRequestResult = browsingSession.Get(securedActionUrl);

			    Assert.That(initialRequestResult.IsRedirect);

				string loginRedirectUrl = initialRequestResult.Response.RedirectLocation;
				Assert.IsTrue(loginRedirectUrl.StartsWith("/Account/LogOn"), "Didn't redirect to logon page");

				// Now follow redirection to logon page
				string loginFormResponseText = browsingSession.Get(loginRedirectUrl).ResponseText;

			    if (loginFormResponseText.Contains(" cannot be cast to ")) throw new Exception("Check your assembly bindings are correct.");

				string suppliedAntiForgeryToken = MvcUtils.ExtractAntiForgeryToken(loginFormResponseText);

				// Now post the login form, including the verification token
				RequestResult loginResult = browsingSession.Post(loginRedirectUrl, new
																					   {
																						   UserName = "steve",
																						   Password = "secret",
																						   __RequestVerificationToken = suppliedAntiForgeryToken
																					   });
			    Assert.NotNull(loginResult.Response);
				string afterLoginRedirectUrl = loginResult.Response.RedirectLocation;
				Assert.AreEqual(securedActionUrl, afterLoginRedirectUrl, "Didn't redirect back to SecretAction");

				// Check that we can now follow the redirection back to the protected action, and are let in
				RequestResult afterLoginResult = browsingSession.Get(securedActionUrl);
				Assert.AreEqual("Hello, you're logged in as steve", afterLoginResult.ResponseText);
			});
		}

	    [Test]
	    public void stress_test__100_calls_in_single_session()
	    {
	        appHost.Start(session => {
	            for (int i = 0; i < 100; i++)
	            {
	                var result = session.Get("");
	                Assert.That(result.IsSuccess);
	            }
            });
	    }

	    [Test]
	    public void stress_test__100_sessions_with_a_single_call()
	    {
            for (int i = 0; i < 100; i++)
            {
                appHost.Start(session => {
                    var result = session.Get("");
                    Assert.That(result.IsSuccess);
                });
            }
        }
    }
}
