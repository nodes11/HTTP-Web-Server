using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HTTPWebServer
{
	public class WebServer
	{
		//A ConcurrentBag is a thread safe undordered collection. It will
		//provide the threadsafe functionality for the AddService method
		private static ConcurrentBag<WebService> WebServices;
		private static CS422.ThreadPool threadpool;
		private static TcpListener Listener;
		private static Thread listenerThread;
		private static bool Listening;
		private static bool Working;

		public static bool Start(int port, int threads)
		{
			bool isValid = true;
			Working = true;
			Listening = true;

			Console.WriteLine("Type 'Stop' to shutdown server\n");

			WebServices = new ConcurrentBag<WebService>();

			//Set our thread count and intialize our threadpool
			if (threads <= 0)
			{
				threads = 64;
			}
			threadpool = new CS422.ThreadPool(threads);

			//Start the listener thread
			listenerThread = new Thread(ClientListener);
			listenerThread.Start(new TcpListener(System.Net.IPAddress.Any, port));

			return isValid;
		}

		public static bool Stop()
		{
			threadpool.Dispose();

			//Break loops
			Listening = false;
			Working = false;

			//Stop TcpListener and join the listening thread
			Listener.Stop();
			listenerThread.Join();

			return true;
		}

		/**
		 * This is the method that the listener thread waits
		 * and dispatches clients in. Uses the AcceptTcpClient
		 * blocking call to accept a client and then adds the
		 * client to the threadpool to get picked up.
		 **/
		private static void ClientListener(object listener)
		{
			//try/catch/finally format taken from MSDN:
			//https://msdn.microsoft.com/en-us/library/system.net.sockets.tcplistener.stop(v=vs.110).aspx
			try
			{
				Console.WriteLine("Waiting for clients!");
				Listener = (TcpListener)listener;
				Listener.Start();

				while (Listening)
				{
					TcpClient Client = Listener.AcceptTcpClient();
					Console.WriteLine("Accepting new client...");
					threadpool.AddItemToBlockingCollection(Client);
				}
			}
			//TODO: Look into exception being thrown here after a fast stop and start
			catch (SocketException e)
			{
				//Do nothing
			}
			finally
			{
				Console.WriteLine("Closing listener thread...");
			}
		}

		/**
		 * This is the method that all threads wait and process
		 * clients in.
		 **/
		public static void ThreadWork()
		{
			while (Working)
			{
				bool serviceFound = false;

				//Use blocking call to wait until we can get a client to process
				TcpClient Client;

				try
				{
					Client = threadpool.GetItemFromCollection();
				}
				catch (Exception e)
				{
					Client = null;
				}

				if (Client != null)
				{
					//Try to create a request from
					WebRequest ClientRequest = WebServer.BuildRequest(Client);

					if (ClientRequest != null)
					{
						//Loop through all of WebServices to see if we have a handler for the request
						foreach (WebService service in WebServices)
						{
							//Check if the Request length is at least as long as the ServiceURI
							if (ClientRequest.GetUri().Length >= service.ServiceURI.Length)
							{
								//Compare the ServiceURI to a the start of the ClientRequest URI
								if (service.ServiceURI == ClientRequest.GetUri().Substring(0, service.ServiceURI.Length))
								{
									service.Handler(ClientRequest);
									serviceFound = true;
									break;
								}
							}
						}

						//If we can't find the service specified, display a 400 error
						if (!serviceFound)
						{
							ClientRequest.WriteNotFoundResponse("<html>404 Not Found<br>" + "</html>");
						}
					}

					//Close and dispose of the stream and client
					Client.GetStream().Dispose();
					Client.Close();
				}
				//If the client is null, the server wants to shut down.
				else
				{
					Console.WriteLine("Closing worker thread...");
					break;
				}
			}
		}

		/**
		 * Validates the request. Returns a WebRequest object if the
		 * request was valid, or null if the request was invalid.
		 **/
		private static WebRequest BuildRequest(TcpClient Client)
		{
			ConcurrentDictionary<string, string> requestParts = new ConcurrentDictionary<string, string>();
			Stream body = null;

			//Use our validator to make sure the request is valid
			RequestValidator validator = new RequestValidator();
			bool isValid = validator.ValidateRequest(Client, requestParts, ref body);
			validator = null;

			//If the request was invalid, return null
			if (isValid == false)
			{
				return null;
			}

			//Build Request from valid header
			return new WebRequest(requestParts, body, Client.GetStream());
		}

		/**
		 * Adds a service to our ConcurrentBag in a thread safe manner
		 **/
		public static void AddService(WebService service)
		{
			WebServices.Add(service);
		}
	}

	/**
	 * Request Validator class
	 **/
	public class RequestValidator
	{
		private StringBuilder RequestData;
		private ConcurrentDictionary<string, string> Contents = new ConcurrentDictionary<string,string>();
		private NetworkStream ClientStream;
		private Stream RequestBody = new MemoryStream();

		private static Stopwatch TimeoutStopWatch;

		/**
		 * Constants used throughout the process
	 	 **/
		private const string CRLF = "\r\n";
		private const string SP = " ";
		private const string VERSION = "HTTP/1.1\r\n";
		private const string GET_TYPE = "GET";
		private const string PUT_TYPE = "PUT";
		private const string COLON = ":";
		private const int MAX_FIRST_LINE_SIZE = 2048;
		private const int MAX_HEADER_SIZE = 1024 * 100;

		/***
		 * Used for valdiation of properties
		 **/
		private int RequestLineValidated;
		private int ValidUpToIndex;
		private int TotalBytesReadIn;
		private int CRLFCount;
		private int EndOfHeader = 0;
		private bool ValidType;
		private bool ValidURL;
		private bool ValidVersion;
		private bool ValidField;
		private bool EndOfRequest;

		public bool ValidateRequest(TcpClient Client, ConcurrentDictionary<string, string> requestParts, ref Stream body)
		{
			byte[] clientBuffer = new byte[4096];
			int bytesReadIn;
			string RequestedURL;
			bool isValid = false;
			string EntireRequest = "";

			RequestData = new StringBuilder();
			Contents = requestParts;
			TotalBytesReadIn = 0;

			//From MSDN: https://msdn.microsoft.com/en-us/library/system.timers.timer(v=vs.110).aspx
			TimeoutStopWatch = new Stopwatch();
			TimeoutStopWatch.Start();

			while (true)
			{
				try
				{
					//Set the read timeout and
					Client.GetStream().ReadTimeout = 1000;
					ClientStream = Client.GetStream();

					RequestLineValidated = 0;
					ValidUpToIndex = 0;
					CRLFCount = 0;
					EndOfRequest = false;

					//Start reading data from the client
					while ((bytesReadIn = ClientStream.Read(clientBuffer, 0, clientBuffer.Length)) != 0)
					{
						if (TimeoutStopWatch.ElapsedMilliseconds > 10000)
						{
							isValid = false;
							throw new IOException();
						}

						RequestBody.Write(clientBuffer, 0, bytesReadIn);

						string NewRequestData = System.Text.Encoding.ASCII.GetString(clientBuffer, 0, bytesReadIn);

						EntireRequest += NewRequestData;
						TotalBytesReadIn += bytesReadIn;

						if (TotalBytesReadIn >= MAX_FIRST_LINE_SIZE && RequestLineValidated == 0)
						{
							if (!EntireRequest.Substring(0, MAX_FIRST_LINE_SIZE).Contains(CRLF))
							{
								isValid = false;
								throw new IOException();
							}
						}

						if (TotalBytesReadIn >= MAX_HEADER_SIZE && RequestLineValidated >= 1)
						{
							if (!EntireRequest.Substring(0, MAX_HEADER_SIZE).Contains(CRLF + CRLF))
							{
								isValid = false;
								throw new IOException();
							}
						}

						if (NewRequestData.EndsWith("\r"))
						{
							int newBytesReadIn = ClientStream.Read(clientBuffer, 0, clientBuffer.Length);
							bytesReadIn += newBytesReadIn;
							NewRequestData += System.Text.Encoding.ASCII.GetString(clientBuffer, 0, newBytesReadIn);
							EntireRequest += System.Text.Encoding.ASCII.GetString(clientBuffer, 0, newBytesReadIn);
						}

						//Parse the data we've read in so far
						if (Parse(NewRequestData, out RequestedURL) == false)
						{
							isValid = false;
							break;
						}

						//Add the data to our StringBuilder which holds the entire request
						RequestData.Append(NewRequestData);

						//Done reading data, send the response
						if (EndOfRequest && ClientStream.DataAvailable == false)
						{
							isValid = true;
							break;
						}
						//Incomplete request
						else if (EndOfRequest == false && ClientStream.DataAvailable == false)
						{
							isValid = false;
							break;
						}
					}
				}
				catch (IOException e)
				{
					Console.WriteLine("ReadTimeout occured. Ending request.");
				}

				TimeoutStopWatch.Stop();
				break;
			}


			EndOfHeader = EntireRequest.IndexOf("\r\n\r\n", StringComparison.CurrentCulture);

			if (EndOfHeader >= 3)
			{
				byte[] bodyBytes = new byte[EntireRequest.Length - (EndOfHeader + 4)];
				RequestBody.Seek(EndOfHeader + 4, SeekOrigin.Begin);
				RequestBody.Read(bodyBytes, 0, (int)(EntireRequest.Length - (EndOfHeader + 4)));

				if (EntireRequest.Substring(EndOfHeader, 4) == "\r\n\r\n" && EndOfHeader + 4 != EntireRequest.Length)
				{
					body = new MemoryStream(bodyBytes);
				}
				else if (EntireRequest.Substring(EndOfHeader, 4) == "\r\n\r\n")
				{
					body = Client.GetStream();
				}
				else
				{
					body = null;
				}
			}

			return isValid;
		}

		/*public void testValidator(string request)
		{
			string url;
			Console.WriteLine(Parse(request, out url));
		}*/

		private bool Parse(string Request, out string RequestedURL)
		{
			int charactersProcessed = 0;
			bool isValid = true;
			RequestedURL = "";

			//Are at the final CRLF
			if (CheckEndOfRequest())
			{
				return true;
			}

			//Have we validated the first line yet?
			if (RequestLineValidated == 0)
			{
				//If the string contains CRLF(s), we want to parse at the end of the first occurance
				if (Request.Contains(CRLF))
				{
					int EndOfLinePostion = Request.IndexOf(CRLF, StringComparison.CurrentCulture) + CRLF.Length;
					isValid = RequestTypeValidation(Request.Substring(0, EndOfLinePostion), out RequestedURL);
					Request = Request.Substring(EndOfLinePostion, Request.Length - EndOfLinePostion);
				}
				else
				{
					isValid = RequestTypeValidation(Request, out RequestedURL);
				}
			}
			//We have the first line validated, validate the remaning fields
			if (RequestLineValidated > 0)
			{
				int charactersLeftToProcess = Request.Length;

				//Loop over all characters recieved
				while (charactersProcessed < charactersLeftToProcess)
				{
					if (CheckEndOfRequest())
					{
						return true;
					}

					//If the string contains CRLF(s), we want to parse at the end of the first occurance
					if (Request.Contains(CRLF))
					{
						int EndOfLinePostion = Request.IndexOf(CRLF, StringComparison.CurrentCulture) + CRLF.Length;
						isValid = RequestFieldValidation(Request.Substring(0, EndOfLinePostion));

						charactersProcessed += Request.Substring(0, EndOfLinePostion).Length;

						Request = Request.Substring(EndOfLinePostion, Request.Length - EndOfLinePostion);
					}
					else
					{
						isValid = RequestFieldValidation(Request);

						charactersProcessed += Request.Length;
					}

					//Is the request invalid and we haven't finsihed going through all the character?
					if (isValid == false)
					{
						return false;
					}

				}
			}

			return isValid;
		}

		private bool RequestTypeValidation(string Header, out string RequestedURL)
		{
			List<string> parts = new List<string>(Header.Split(SP.ToCharArray(), StringSplitOptions.RemoveEmptyEntries));
			int partIndex = 0;

			RequestedURL = "";
			bool isValid = true;

			if (RequestLineValidated == 0)
			{
				//Have we validated the Type yet?
				if (ValidType == false && (parts.Count > partIndex))
				{
					if ((isValid = CheckHTTPType(parts[partIndex], GET_TYPE)) == false &&
					    (isValid = CheckHTTPType(parts[partIndex], PUT_TYPE)) == false)
					{
						return false;
					}

					if (ValidType)
					{
						ValidUpToIndex = 0;
						partIndex++;
					}
				}
				//Have we validated the URL? (Type must be set and there must be another item in parts)
				if (ValidURL == false && ValidType && partIndex < parts.Count)
				{
					if ((isValid = CheckURL(parts[partIndex])) == false)
					{
						return false;
					}

					if (ValidURL)
					{
						RequestedURL = parts[partIndex];
						ValidUpToIndex = 0;
						partIndex++;
					}
				}
				//Have we validated the Version? (Type and URL must be set and there must be another item in parts)
				if (ValidVersion == false && ValidURL && ValidType && partIndex < parts.Count)
				{
					if ((isValid = CheckVersion(parts[partIndex])) == false)
					{
						return false;
					}

					if (ValidVersion)
					{
						ValidUpToIndex = 0;
						RequestLineValidated += 1;
						CRLFCount += 1;
					}
				}
			}

			return isValid;
		}

		private bool RequestFieldValidation(string Line)
		{
			List<string> parts = new List<string>(Line.Split(SP.ToCharArray(), StringSplitOptions.RemoveEmptyEntries));
			int partIndex = 0;
			bool isValid = true;

			if (parts.Count == 0)
			{
				return true;
			}

			//Was the previous item a CRLF and current is not? Set our counter back to 0.
			if (parts[partIndex] != CRLF && CRLFCount > 0)
			{
				CRLFCount = 0;
			}
			else if (parts[partIndex] == CRLF && CRLFCount > 0)
			{
				CRLFCount += 1;
				if (CheckEndOfRequest())
				{
					return true;
				}
			}

			//Is the field set and we just need a token or CRLF?
			if (ValidField)
			{
				if (parts[parts.Count - 1].EndsWith(CRLF))
				{
					RequestLineValidated++;
					ValidField = false;
					CRLFCount += 1;
				}
			}
			else if (ValidField == false)
			{
				isValid = CheckField(parts[partIndex]);
				if (isValid == false)
				{
					return false;
				}

				//The field was confirmed and set
				if (ValidField)
				{
					ValidUpToIndex = 0;
					if (parts[parts.Count - 1].EndsWith(CRLF))
					{
						string fieldName = parts[0].Substring(0, parts[0].Length - 1);
						string fieldValue = "";
						for (int i = 1; i < parts.Count; i++)
						{
							fieldValue += parts[i];
						}
						Contents.TryAdd(fieldName.ToLower(), fieldValue);
						RequestLineValidated++;
						ValidField = false;
						CRLFCount += 1;
					}
					partIndex++;
				}
				else if (parts.Count == 2 && ValidField == false)
				{
					isValid = false;
				}
			}

			return isValid;
		}

		private bool CheckHTTPType(string Type, string CorrectType)
		{
			if (Type.Length + ValidUpToIndex > CorrectType.Length || ValidUpToIndex == CorrectType.Length)
			{
				return false;
			}

			if ((Type == CorrectType.Substring(ValidUpToIndex, Type.Length)) == false)
			{
				return false;
			}

			ValidUpToIndex += Type.Length;

			if (ValidUpToIndex == CorrectType.Length)
			{
				Contents.TryAdd("HTTPType", Type);
				ValidType = true;
			}

			return true;
		}

		private bool CheckURL(string URL)
		{
			if (URL.Length < 1)
			{
				return false;
			}

			ValidUpToIndex += URL.Length;

			ValidURL = true;
			Contents.TryAdd("URL", URL);

			return true;
		}

		private bool CheckVersion(string Version)
		{
			if (Version.Length + ValidUpToIndex > 10 || ValidUpToIndex == 10)
			{
				return false;
			}

			if (Version == (VERSION.Substring(ValidUpToIndex, Version.Length)) == false)
			{
				return false;
			}

			ValidUpToIndex += Version.Length;

			if (ValidUpToIndex == 10)
			{
				Contents.TryAdd("HTTPVersion", Version);
				ValidVersion = true;
			}

			return true;
		}

		private bool CheckField(string Field)
		{
			if (Field == "")
			{
				return false;
			}

			if (Field.Substring(0, 1) == COLON && ValidUpToIndex == 0)
			{
				return false;
			}

			if (ValidField == true)
			{
				return false;
			}

			if (Field.Contains(CRLF))
			{
				return false;
			}

			if ((ValidUpToIndex > 1 || Field.Length > 1) && Field.EndsWith(COLON))
			{
				ValidField = true;
			}


			ValidUpToIndex += Field.Length;

			return true;
		}

		private bool CheckEndOfRequest()
		{
			if (CRLFCount == 2)
			{
				EndOfRequest = true;
				EndOfHeader = ValidUpToIndex;
				return true;
			}

			return false;
		}
	}
}
