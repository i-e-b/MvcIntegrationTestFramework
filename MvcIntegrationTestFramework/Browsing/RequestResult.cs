using System.Web;
using System.Web.Mvc;

namespace MvcIntegrationTestFramework.Browsing
{
    /// <summary>
    /// Represents the result of a simulated request
    /// </summary>
    public class RequestResult
    {
        public HttpResponse Response { get; set; }
        public string ResponseText { get; set; }

        public ActionExecutedContext ActionExecutedContext { get; set; }
        public ResultExecutedContext ResultExecutedContext { get; set; }

        public bool IsRedirect { get { return Response != null && Response.StatusCode >= 300 && Response.StatusCode < 400; } }
        public bool IsSuccess { get { return Response != null && Response.StatusCode >= 200 && Response.StatusCode < 300; } }
        public bool IsClientError { get { return Response != null && Response.StatusCode >= 400 && Response.StatusCode < 500; } }
        public bool IsServerError { get { return Response != null && Response.StatusCode >= 500 && Response.StatusCode < 600; } }
    }
}