using DecisionServicePrivateWeb.Classes;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DecisionServicePrivateWeb.Controllers
{
    internal static class ApiUtil
    {
        internal static void Authenticate(HttpRequestBase request)
        {
            var authToken = request.Headers["Authorization"];

            if (authToken == null)
                throw new UnauthorizedAccessException("AuthorizationToken missing");

            if (string.IsNullOrWhiteSpace(authToken))
                throw new UnauthorizedAccessException("AuthorizationToken missing");

            if (authToken != ConfigurationManager.AppSettings[ApplicationMetadataStore.AKUserToken])
                throw new UnauthorizedAccessException();
        }

        internal static string ReadBody(HttpRequestBase request)
        {
            var req = request.InputStream;
            req.Seek(0, System.IO.SeekOrigin.Begin);
            return new StreamReader(req).ReadToEnd();
        }

        internal static string CreateEventId()
        {
            return Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        }
    }
}