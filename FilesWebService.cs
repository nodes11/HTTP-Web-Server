using System;
using System.Collections.Generic;
using System.IO;

namespace HTTPWebServer
{
	public class FilesWebService : WebService
	{
		private readonly FileSys422 FileSystem;

		private const string putHeaderTemplate =
			"{0} 200 OK\r\n" +
			"Content-Type: {1}\r\n" +
			"Content-Length: {2}\r\n" +
			"\r\n";
		private const string dirHeaderTemplate =
			"{0} 200 OK\r\n" +
			"Content-Type: {1}\r\n" +
			"Content-Length: {2}\r\n" +
			"\r\n";
		private const string fileHeaderTemplate =
			"{0} 200 OK\r\n" +
			"Content-Type: {1}\r\n" +
			"Content-Length: {2}\r\n" +
			"\r\n";
		private const string partialFileHeaderTemplate =
			"{0} 206 Partial Content\r\n" +
			"Content-Type: {1}\r\n" +
			"Content-Range: bytes {2}-{3}/{4}\r\n" +
			"Content-Length: {5}\r\n" +
			"\r\n";

		public FilesWebService(FileSys422 fs)
		{
			FileSystem = fs;
		}

		public override string ServiceURI
		{
			get
			{
				return "/files";
			}
		}

		public override void Handler(WebRequest req)
		{
			//If the request URI doesn't start with the proper service URI
			if (!req.GetUri().StartsWith(this.ServiceURI, StringComparison.CurrentCulture))
			{
				throw new InvalidOperationException();
			}

			//Get Uri pieces
			string[] uriPieces = GetUriPeices(req);

			//Decode path
			uriPieces = DecodePath(uriPieces);

			//Get the root directory
			Dir422 currentLocation = FileSystem.GetRoot();

			//Handle PUT requests
			if (req.GetHTTPType() == "PUT")
			{
				FileStream newFileStream = null;

				try
				{
					currentLocation = TraverseToDirectory(currentLocation, uriPieces, req);

					if (currentLocation.ContainsFile(uriPieces[uriPieces.Length - 1], false) == true)
					{
						req.WriteNotFoundResponse("<html>File Already Exists!</html>");
					}
					else
					{
						File422 newFile = currentLocation.CreateFile(uriPieces[uriPieces.Length - 1]);
						newFileStream = (FileStream)newFile.OpenReadWrite();

						CopyBytes(req.GetBody(), newFileStream, req.GetBody().Length);

						newFileStream.Close();

						string response = "<html>File Uploaded!</html>";
						string header = string.Format(putHeaderTemplate, req.GetHTTPVersion(), "text/html", response.Length);

						req.WriteHTMLResponse(header + response);
					}
				}
				catch (Exception e)
				{
					if (newFileStream != null)
					{
						newFileStream.Close();
					}
				}

			}
			//Handle GET requests
			else
			{
				//If we're at the root directory, respond with a list
				if (uriPieces.Length == 0)
				{
					string response = BuildDirHTML(FileSystem.GetRoot());

					string header = string.Format(dirHeaderTemplate, req.GetHTTPVersion(), "text/html", response.Length);

					req.WriteHTMLResponse(header + response);
				}
				else
				{
					currentLocation = TraverseToDirectory(currentLocation, uriPieces, req);

					//If the wanted item is a file, respond with a file
					if (currentLocation.GetFile(uriPieces[uriPieces.Length - 1]) != null)
					{
						//If we have a range header, we need to use a partial response header
						if (req.GetHeaders().ContainsKey("range"))
						{
							string header = GetRangeHeader(req, currentLocation, uriPieces);

							req.WriteHTMLResponse(header);
						}
						//Write repsonse for displaying file contents
						else {
							Stream selectedFile = currentLocation.GetFile(uriPieces[uriPieces.Length - 1]).OpenReadOnly();
							string header = string.Format(fileHeaderTemplate, req.GetHTTPVersion(), GetContentType(currentLocation.GetFile(uriPieces[uriPieces.Length - 1]).Name), selectedFile.Length);
							selectedFile.Close();

							req.WriteHTMLResponse(header);
						}

						BuildFileHTML(currentLocation.GetFile(uriPieces[uriPieces.Length - 1]), req);
					}
					//If the wanted item is a directory, respond with a directory listing
					else
					{
						//Write response for displaying directory
						if (currentLocation.GetDir(uriPieces[uriPieces.Length - 1]) != null)
						{
							string response = BuildDirHTML(currentLocation.GetDir(uriPieces[uriPieces.Length - 1]));

							string header = string.Format(dirHeaderTemplate, req.GetHTTPVersion(), "text/html", response.ToString().Length);

							req.WriteHTMLResponse(header + response);
						}
						//Request item could not be found
						else
						{
							req.WriteNotFoundResponse("<html>404 File Not Found<br>" + "</html>");
						}
					}
				}
			}
		}

		private string BuildDirHTML(Dir422 Dir)
		{
			var html = new System.Text.StringBuilder("<html>");

			html.AppendLine(@"<script>
				function selectedFileChanged(fileInput, urlPrefix) {
				document.getElementById('uploadHdr').innerText = 'Uploading ' + fileInput.files[0].name + '...';
				// Need XMLHttpRequest to do the upload
				if (!window.XMLHttpRequest)
				{
					alert('Your browser does not support XMLHttpRequest. Please update your browser.');
					return;
				}
				// Hide the file selection controls while we upload
				var uploadControl = document.getElementById('uploader');
				if (uploadControl)
				{
					uploadControl.style.visibility = 'hidden';
				}
				// Build a URL for the request
				if (urlPrefix.lastIndexOf('/') != urlPrefix.length - 1)
				{
					urlPrefix += '/';
				}
 				var uploadURL = urlPrefix + fileInput.files[0].name;
				// Create the service request object
				var req = new XMLHttpRequest();
				req.open('PUT', uploadURL);
				req.onreadystatechange = function() {
					document.getElementById('uploadHdr').innerText = 'Upload (request status == ' + req.status + ')';
					location.reload();
					document.getElementById('responseMessage').innerHTML= req.responseText;
				};
				req.send(fileInput.files[0]);
			}
			</script> ");

			//Add all filenames and links
			html.AppendLine("<h1>Files</h1>");
			foreach (File422 file in Dir.GetFiles())
			{
				string encodedName = file.Name.Replace("#", "%23");
				html.AppendFormat("<a href=\"{0}\">{1}</a><br>", BuildPath(null, file) + "/" + encodedName, file.Name);
			}

			//Add all directories and links
			html.AppendLine("<br><h1>Directories</h1>");
			foreach (Dir422 dir in Dir.GetDirs())
			{
				html.AppendFormat("<a href=\"{0}\">{1}</a><br>", BuildPath(dir, null), dir.Name);
			}

			html.AppendFormat("<hr><h3 id='uploadHdr'>Upload</h3><br>" +
				"<input id=\"uploader\" type='file' " + "onchange='selectedFileChanged(this,\"{0}\")' /><h2 id='responseMessage'></h2><hr>", BuildPath(Dir, null));

			//Close page tags
			html.AppendLine("</html>");

			return html.ToString();
		}

		private void BuildFileHTML(File422 File, WebRequest req)
		{
			long start = 0,
				end = -1;

			Stream fileStream = File.OpenReadOnly();

			//Do we have a range header? Set the bounds
			if (req.GetHeaders().ContainsKey("range"))
			{
				string value = "";
				req.GetHeaders().TryGetValue("range", out value);

				if (value != "")
				{
					value = value.Substring(6);
					string[] nums = value.Split('-');
					long.TryParse(nums[0], out start);
					long.TryParse(nums[1], out end);

					for (int i = 0; i < nums.Length; i++)
					{
						if (nums[i].Contains("\r\n"))
						{
							nums[i] = nums[i].Substring(0, nums[i].LastIndexOf("\r\n"));
						}
					}

					if (end == 0 && nums[1] == string.Empty)
					{
						end = fileStream.Length;
						nums[1] = end.ToString();
					}

					fileStream.Seek(start, SeekOrigin.Begin);

					if (end > fileStream.Length)
					{
						end = (int)fileStream.Length;
					}

					if (end < 0)
					{
						start = 0;
						end = 0;
					}

					if (start > fileStream.Length)
					{
						start = 0;
						end = 0;
					}

					if (start < 0)
					{
						start = 0;
					}
				}
			}
			//No range header? Set the bounds to 0 an length
			else
			{
				start = 0;
				end = (int)fileStream.Length;
			}

			CopyBytes(fileStream, req.GetBody(), end - start);

			fileStream.Close();
		}

		private string BuildPath(Dir422 dir, File422 file)
		{
			Dir422 tempDir;

			//Check if we are building a path for a file or a directory
			if (file != null)
			{
				tempDir = file.Parent;
			}
			else
			{
				tempDir = dir;
			}

			List<string> pathItems = new List<string>();

			if (tempDir.Parent != null)
			{
				//Work our way up the tree
				while (tempDir.Parent.Parent != null)
				{
					pathItems.Add(tempDir.Name);
					tempDir = tempDir.Parent;
				}
			}

			string path = "";

			//Are they accessing an item in the root directory?
			if (pathItems.Count > 0)
			{
				path = "/files/" + pathItems[pathItems.Count - 1];

				for (int i = pathItems.Count - 2; i > -1; i--)
				{
					path += "/" + pathItems[i];
				}
			}
			else
			{
				if (file != null)
				{
					path = "/files/" + file.Name;
				}
				else
				{
					path = "/files/" + dir.Name;
				}
			}

			return path;
		}

		private string GetContentType(string filename)
		{
			string type = GetFileType(filename);

			if (type == ".pdf")
			{
				return "application/pdf";
			}
			else if (type == ".html")
			{
				return "text/html";
			}
			else if (type == ".xml")
			{
				return "text/xml";
			}
			else if (type == ".png")
			{
				return "image/png";
			}
			else if (type == ".jpeg" || type == ".jpg")
			{
				return "image/jpeg";
			}
			else if (type == ".mp4")
			{
				return "video/mp4";
			}
			else
			{
				return "text/plain";
			}
	   }

		private string GetFileType(string filename)
		{
			int lastIndexOf = filename.LastIndexOf('.');
			string type = "";

			if (lastIndexOf >= 0)
				type = filename.Substring(filename.LastIndexOf('.'));

			return type;
		}

		private string GetRangeHeader(WebRequest req, Dir422 currentLocation, string[] uriPieces)
		{
			string header = "";
			string value = "";

			req.GetHeaders().TryGetValue("range", out value);
			value = value.Substring(6);
			string[] nums = value.Split('-');

			for (int i = 0; i < nums.Length; i++)
			{
				if (nums[i].Contains("\r\n"))
				{
					nums[i] = nums[i].Substring(0, nums[i].LastIndexOf("\r\n"));
				}
			}

			Stream selectedFile = currentLocation.GetFile(uriPieces[uriPieces.Length - 1]).OpenReadOnly();

			long a, b;
			long.TryParse(nums[0], out a);
			long.TryParse(nums[1], out b);

			if (b == 0 && nums[1] == string.Empty)
			{
				b = (int)selectedFile.Length;
				nums[1] = b.ToString();
			}

			long length = b - a;

			if (b == selectedFile.Length)
			{
				b = b - 1;
			}

			header = string.Format(partialFileHeaderTemplate, req.GetHTTPVersion(), GetContentType(currentLocation.GetFile(uriPieces[uriPieces.Length - 1]).Name), a, b, selectedFile.Length, length);

			selectedFile.Close();

			return header;
		}

		private Dir422 TraverseToDirectory(Dir422 currentLocation, string[] uriPieces, WebRequest req)
		{
			File422 fileAtRoot = null;

			//Iterate over all path parts until we get to the file or directory wanted
			for (int item = 0; item < uriPieces.Length - 1; item++)
			{
				fileAtRoot = currentLocation.GetFile(uriPieces[item]);

				if (fileAtRoot == null)
				{
					currentLocation = currentLocation.GetDir(uriPieces[item]);

					if (currentLocation == null)
					{
						req.WriteNotFoundResponse("<html>404 File Not Found<br>" + "</html>");
					}
				}
				else
				{
					break;
				}
			}

			return currentLocation;
		}

		private void CopyBytes(Stream source, Stream destination, long bytesToRead)
		{
			long bytesReadIn = 0;
			byte[] buffer = new byte[1024];

			while ((bytesReadIn = source.Read(buffer, 0, buffer.Length)) != 0)
			{
				//Did we read more bytes than we want to write?
				if (bytesReadIn > bytesToRead)
				{
					bytesReadIn = bytesToRead;
				}

				try
				{
					destination.Write(buffer, 0, (int)bytesReadIn);

					bytesToRead -= bytesReadIn;

					//Have we read the total number of bytes we want to read in
					if (bytesToRead == 0)
					{
						break;
					}
				}
				catch (Exception e)
				{

				}
			}
		}

		private string[] GetUriPeices(WebRequest req)
		{
			return req.GetUri().Substring(this.ServiceURI.Length).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
		}

		private string[] DecodePath(string[] pieces)
		{
			//Use .NET functionality to convert all encoded characters
			for (int i = 0; i < pieces.Length; i++)
			{
				pieces[i] = System.Uri.UnescapeDataString(pieces[i]);
			}

			return pieces;
		}
	}
}
