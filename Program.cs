using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CS422
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			WebServer.Start(3000, 10);

			//Add the DemoService to the list of available services
			WebServer.AddService(new DemoService());
			WebServer.AddService(new FilesWebService(StandardFileSystem.Create("/Users")));

			while (true)
			{
				string input = Console.ReadLine();

				if (input == "Stop")
				{
					WebServer.Stop();
					break;
				}
			}

			/*RequestValidator rv = new RequestValidator();
			string putr = "PUT /hello.htm HTTP/1.1\r\nUser-Agent: Mozilla/4.0 (compatible; MSIE5.01; Windows NT)\r\nHost: www.tutorialspoint.com\r\nAccept-Language: en-us\r\nConnection: Keep-Alive\r\nContent-type: text/html\r\nContent-Length: 182\r\n\r\n";
			rv.testValidator(putr);*/
		}
	}
}
