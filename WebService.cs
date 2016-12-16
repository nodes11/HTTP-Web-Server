using System;
namespace HTTPWebServer
{
	public abstract class WebService
	{
		public abstract void Handler(WebRequest req);

		public abstract string ServiceURI
		{
			get;
		}
	}
}
