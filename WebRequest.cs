using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace HTTPWebServer
{
	public class WebRequest
	{
		private string HTTPType;
		private string HTTPVersion;
		private string Uri;
		private ConcurrentDictionary<string, string> Headers;
		private Stream Body;
		public byte[] BodyBuf;
		private NetworkStream ResponseTarget;

		public WebRequest(ConcurrentDictionary<string, string> headers, Stream body, NetworkStream clientStream)
		{
			Headers = headers;
			Headers.TryGetValue("HTTPType", out this.HTTPType);
			Headers.TryGetValue("HTTPVersion", out this.HTTPVersion);
			Headers.TryGetValue("URL", out this.Uri);
			ResponseTarget = clientStream;

			if (HTTPVersion.EndsWith("\r\n"))
			{
				HTTPVersion = HTTPVersion.Substring(0, HTTPVersion.Length - 2);
			}

			string length = "";

			if (Headers.TryGetValue("content-length", out length))
			{
				long clength = 0;
				long.TryParse(length, out clength);

				if (body.GetType() == typeof(MemoryStream))
				{
					Body = new ConcatStream(body, clientStream, clength);
				}
				else if (body.GetType() == typeof(NetworkStream))
				{
					Body = body;
				}
			}
			else
			{
				if (body.GetType() == typeof(MemoryStream))
				{
					Body = new ConcatStream(body, clientStream);
				}
				else if (body.GetType() == typeof(NetworkStream))
				{
					Body = body;
				}
			}

		}

		public void WriteNotFoundResponse(string pageHTML)
		{
			string headerTemplate =
				"HTTP/1.1 404 Not Found\r\n" +
				"Content-Type: text/html\r\n" +
				"Content-Length: {0}\r\n" +
				"\r\n\r\n";

			long clength = 0;

			if (Body.GetType() != typeof(NetworkStream))
			{
				clength = Body.Length;

				if (clength == -1)
				{
					clength = 0;
				}
			}

			string header = string.Format(headerTemplate, HTTPVersion, pageHTML.Length);


			byte[] bufToWrite = System.Text.Encoding.ASCII.GetBytes(header + pageHTML);
			this.ResponseTarget.Write(bufToWrite, 0, bufToWrite.Length);
		}

		public bool WriteHTMLResponse(string htmlString)
		{
			byte[] bufToWrite = System.Text.Encoding.ASCII.GetBytes(htmlString);
			this.ResponseTarget.Write(bufToWrite, 0, bufToWrite.Length);

			return true;
		}

		public bool WritePartialHTMLResponse(string htmlString)
		{
			byte[] bufToWrite = System.Text.Encoding.ASCII.GetBytes(htmlString);
			this.ResponseTarget.Write(bufToWrite, 0, bufToWrite.Length);

			return true;
		}

		public string GetHTTPType()
		{
			return HTTPType;
		}

		public string GetHTTPVersion()
		{
			return HTTPVersion;
		}

		public string GetUri()
		{
			return Uri;
		}

		public ConcurrentDictionary<string, string> GetHeaders()
		{
			return Headers;
		}

		public Stream GetBody()
		{
			return Body;
		}

		public void Dispose()
		{
			ResponseTarget.Dispose();
		}
	}
}
