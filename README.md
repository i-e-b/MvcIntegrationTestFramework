MvcIntegrationTestFramework
===========================

Integration test harness for ASP.Net MVC 5. Allows you to fully integration test an MVC web project without needing to host under IIS or similar. Allows access to both server state and client responses in a single assertion.

![Batteries included](https://raw.githubusercontent.com/i-e-b/MvcIntegrationTestFramework/master/batteries_small.png)
https://www.nuget.org/packages/MvcIntegrationTestFramework

This fork targets MVC 5, VS2015.

Everything should just work out of the box, no need for post build steps.

Usage
----------

In your test set-up start a new AppHost targeting the folder containing your MVC application:

```csharp
	this.appHost = AppHost.Simulate("MyMvcApplication");
```

Then for each test flow, start a browsing session, make your calls and assert against the results:

```csharp
	this.appHost.Start(browsingSession =>
	{
		// Request the root URL
		RequestResult result = browsingSession.Get("/welcome");

		// Check the result status
		Assert.That(result.IsSuccess);

		// Make assertions about the ActionResult
		var viewResult = (ViewResult)result.ActionExecutedContext.Result;
		Assert.AreEqual("Index", viewResult.ViewName);
		Assert.AreEqual("Welcome to ASP.NET MVC!", viewResult.ViewData["Message"]);

		// Or make assertions about the rendered HTML
		Assert.IsTrue(result.ResponseText.Contains("<!DOCTYPE html"));
	});
```

See the `MyMvcApplication.Tests` project and the `HomeControllerTests.cs` file for more examples.

The framework injects it's own System.Web.Optimization bundle provider, which supplies blank CSS and Javascript files.

Known issues
============

Web Activator
---------------

`[assembly: WebActivator.PostApplicationStartMethod(...)]` injection can cause problems with the ASP.Net hosting enviroment. If you see the error message `This method cannot be called during the application's pre-start initialization phase`, try to remove the assembly level injector and call your setup from `Global.aspx` to solve this.

TFS Builds
----------

TFS Build servers can restructure your project, causing the test framework to not find the MVC project directory.
To work around this, you can pass multiple potential locations for your project to the `Simulate` function, and the first one to be found will be used.

For example,

```csharp
    appHost = AppHost.Simulate(
        "Source\\Web\\MyMvcApplication",             // Local solution location
        "a\\_PublishedWebsites\\MyMvcApplication");  // TFS build system, under ...\Agent\_work\1\a\_PublishedWebsites ...
```


[Icon via game-icones.net](http://game-icons.net/lorc/originals/batteries.html) 
