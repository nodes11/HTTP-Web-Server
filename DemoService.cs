using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace HTTPWebServer
{
	public class DemoService : WebService
	{
		public override void Handler(WebRequest req)
		{
			long clength = 0;

			if (req.GetBody().GetType() != typeof(NetworkStream))
			{
				clength = req.GetBody().Length;

				if (clength == -1)
				{
					clength = 0;
				}
			}


			string response = String.Format(c_template, req.GetHTTPVersion(), req.GetUri(), clength, "11352911");

			string headerTemplate =
				"{0} 200 OK\r\n" +
				"Content-Type: text/html\r\n" +
				"Content-Length: {1}\r\n" +
				"\r\n\r\n";

			string header = string.Format(headerTemplate, req.GetHTTPVersion(), response.Length);

			req.WriteHTMLResponse(header + response);
		}

		private const string c_template = "<html>This is the response to the request:<br>" +
			"Method: {0}<br>Request-Target/URI: {1}<br>" +
			"Request body size, in bytes: {2}<br><br>" +
			"Student ID: {3}</html>";


		public override string ServiceURI
		{
			get
			{
				return "/";
			}
		}
	}
}
