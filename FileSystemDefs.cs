using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace HTTPWebServer
{
	/**
 	* Abstract for a FileSytem422 object
 	**/
	public abstract class FileSys422
	{
		public abstract Dir422 GetRoot();

		public virtual bool Contains(Dir422 dir)
		{
			Dir422 parent = dir.Parent;

			while (parent.Parent != null)
			{
				parent = parent.Parent;
			}

			if (parent == GetRoot())
			{
				return true;
			}

			return false;
		}

		public virtual bool Contains(File422 file)
		{
			return Contains(file.Parent);
		}
	}

	/**
 	 * Abstract class for a Dir422 object
 	 **/
	public abstract class Dir422
	{
		public abstract string Name { get; }

		//Return all directories or files
		public abstract IList<Dir422> GetDirs();
		public abstract IList<File422> GetFiles();

		public abstract string GetPath();

		public abstract Dir422 Parent { get; }

		//Check for directory or file in tree
		public abstract bool ContainsFile(string fileName, bool recursive);
		public abstract bool ContainsDir(string dirName, bool recursive);

		//Get a directory of a file
		public abstract Dir422 GetDir(string dirName);
		public abstract File422 GetFile(string fileName);

		//Create a directory or a file
		public abstract Dir422 CreateDir(string dirName);
		public abstract File422 CreateFile(string fileName);
	}

	/**
	 * Abstract class for a File422 object
	 **/
	public abstract class File422
	{
		public abstract string Name { get; }

		public abstract Dir422 Parent { get; }

		public abstract string GetPath();

		//DO NOT SUPPORT WRITING!
		//Stream.CanWrite should be false
		public abstract Stream OpenReadOnly();

		//Support both reading and writing
		public abstract Stream OpenReadWrite();
	}

	/**
	 * Standard File System
	 **/
	public class StandardFileSystem : FileSys422
	{
		private static StdFSDir Root;

		//Set the root and return the filesystem object
		public static StandardFileSystem Create(string rootPath)
		{
			Root = new StdFSDir(rootPath);
			Root.SetRoot(true);
			return new StandardFileSystem();
		}

		//Get the root
		public override Dir422 GetRoot()
		{
			return Root;
		}
	}

	/**
	 * Standard File System Directory
	 **/
	public class StdFSDir : Dir422
	{
		private string _path;
		private bool _root = false;

		public StdFSDir(string path)
		{
			_path = path;
		}

		public void SetRoot(bool isRoot)
		{
			_root = isRoot;
		}

		public bool GetRoot()
		{
			return _root;
		}

		public override string Name
		{
			get
			{
				return Path.GetFileName(_path);
			}
		}

		public override Dir422 Parent
		{
			get
			{
				if (_root == true)
				{
					return null;
				}

				DirectoryInfo parentDirectoryInfo = Directory.GetParent(_path);

				if (parentDirectoryInfo != null)
				{
					return new StdFSDir(parentDirectoryInfo.FullName);
				}
				else
				{
					return null;
				}
			}
		}

		public override string GetPath()
		{
			return _path;
		}

		//Iterate over all directories and store them in a list
		public override IList<Dir422> GetDirs()
		{
			List<Dir422> dirs = new List<Dir422>();

			foreach (string dir in Directory.GetDirectories(_path))
			{
				dirs.Add(new StdFSDir(dir));
			}

			return dirs;
		}

		//Iterate over all files and store them in a list
		public override IList<File422> GetFiles()
		{
			List<File422> files = new List<File422>();

			foreach (string file in Directory.GetFiles(_path))
			{
				files.Add(new StdFSFile(file));
			}

			return files;
		}

		//Iterate over files and search for file. If not found and not recursive
		//is true, perform a DFS on the directories.
		public override bool ContainsFile(string fileName, bool recursive)
		{
			if (!CheckValidName(fileName))
			{
				return false;
			}

			bool doesContain = false;

			//Iterate over the current directory files, if file exists return true.
			IList<File422> Files = GetFiles();
			foreach (File422 file in Files)
			{
				if (file.Name == fileName)
				{
					doesContain = true;
					return doesContain;
				}
			}

			//Iterative step where we step into each child directory and look at the children
			if (recursive)
			{
				IList<Dir422> Directories = GetDirs();
				foreach (Dir422 dir in Directories)
				{
					doesContain = dir.ContainsFile(fileName, true);

					if (doesContain == true)
					{
						return doesContain;
					}
				}
			}

			return doesContain;
		}

		//Iterate over directories and search for directory. If not found and recursive
		//is true, perform a DFS on the directories.
		public override bool ContainsDir(string dirName, bool recursive)
		{
			if (!CheckValidName(dirName))
			{
				return false;
			}

			bool doesContain = false;

			//Iterate over the current directory files, if file exists return true.
			IList<Dir422> Directories = GetDirs();
			foreach (Dir422 dir in Directories)
			{
				if (dir.Name == dirName)
				{
					doesContain = true;
					return doesContain;
				}
			}

			//Iterative step where we step into each child directory and look at the children
			if (recursive)
			{
				foreach (Dir422 dir in Directories)
				{
					doesContain = dir.ContainsDir(dirName, true);

					if (doesContain == true)
					{
						return doesContain;
					}
				}
			}

			return doesContain;
		}

		//Iterarte over directories and search for a file.
		public override Dir422 GetDir(string dirName)
		{
			if (!CheckValidName(dirName))
			{
				return null;
			}

			//Iterate over the current directory directories, if directory exists return a Dir422 object.
			IList<Dir422> Directories = GetDirs();
			foreach (Dir422 dir in Directories)
			{
				if (dir.Name == dirName)
				{
					return dir;
				}

			}

			return null;
		}

		//Iterate over files and search
		public override File422 GetFile(string fileName)
		{
			if (!CheckValidName(fileName))
			{
				return null;
			}

			//Iterate over the current directory files, if file exists return a File422 object.
			IList<File422> Files = GetFiles();
			foreach (File422 file in Files)
			{
				if (file.Name == fileName)
				{
					return file;
				}
			}

			return null;
		}

		//Create a new directory. If the directory exists then just return
		//an instance of it.
		public override Dir422 CreateDir(string dirName)
		{
			if (!CheckValidName(dirName))
			{
				return null;
			}

			Dir422 newDir = null;

			if (Directory.Exists(_path + "/" + dirName))
			{
				newDir = GetDir(dirName);
			}
			else
			{
				try
				{
					Directory.CreateDirectory(_path + "/" + dirName);
					newDir = new StdFSDir(_path + "/" + dirName);
				}
				catch (Exception e)
				{
					newDir = null;
				}
			}

			return newDir;
		}

		//Create a new file. If the file exists, set the length to 0.
		public override File422 CreateFile(string fileName)
		{
			if (!CheckValidName(fileName))
			{
				return null;
			}

			File422 newFile = null;
			FileStream newFileStream = null;

			if (File.Exists(fileName))
			{
				try
				{
					newFile = GetFile(fileName);
					newFileStream = (FileStream)newFile.OpenReadWrite();

					//Remove all existing contents
					newFileStream.SetLength(0);
					newFileStream.Close();
				}
				catch (Exception e)
				{
					newFile = null;
				}
			}
			else
			{
				try
				{
					newFileStream = File.Create(_path + "/" + fileName);
					newFile = new StdFSFile(_path + "/" + fileName);
					newFileStream.Close();
				}
				catch (Exception e)
				{
					newFile = null;
				}
			}

			return newFile;
		}

		//Checks to makes the sure the filename is valid
		public bool CheckValidName(string name)
		{
			if (name.Contains("/") || name == null || name.Contains(@"\"))
			{
				return false;
			}

			return true;
		}
	}

	/**
	 * Standard File System File
	 **/
	public class StdFSFile : File422
	{
		private string _path;

		public StdFSFile(string path)
		{
			_path = path;
		}

		public override string Name
		{
			get
			{
				return Path.GetFileName(_path);
			}
		}

		public override Dir422 Parent
		{
			get
			{
				DirectoryInfo ParentInfo = Directory.GetParent(_path);
				return new StdFSDir(ParentInfo.FullName);
			}
		}

		public override string GetPath()
		{
			return _path;
		}

		public override Stream OpenReadOnly()
		{
			Stream openFileStream = null;

			try
			{
				openFileStream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
			}
			catch (Exception e)
			{

			}

			return openFileStream;
		}

		public override Stream OpenReadWrite()
		{
			Stream openFileStream = null;

			try
			{
				openFileStream = File.Open(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
			}
			catch (Exception e)
			{

			}

			return openFileStream;
		}
	}

	/**
	 * Memory File System
	 **/
	public class MemoryFileSystem : FileSys422
	{
		private MemFSDir Root;

		public override Dir422 GetRoot()
		{
			return Root;
		}

		public MemoryFileSystem()
		{
			Root = new MemFSDir("/", null);
			Root.SetRoot(true);
		}

	}

	/**
	 * Memory File System Directory
	 **/
	public class MemFSDir : Dir422
	{
		private string _path;
		private MemFSDir _parent;
		private IList<Dir422> _directories;
		private IList<File422> _files;
		private bool _root = false;

		public MemFSDir(string path, MemFSDir parent)
		{
			if (parent != null && !parent.GetRoot())
			{
				_path = parent.GetPath() + "/" + path;
			}
			else if (parent != null && parent.GetRoot())
			{
				if (parent.GetPath().EndsWith("/"))
				{
					_path = parent.GetPath() + path;
				}
				else
				{
					_path = parent.GetPath() + "/" + path;
				}
			}
			else
			{
				_path = path;
			}

			_parent = parent;
			_directories = new List<Dir422>();
			_files = new List<File422>();
		}

		public override string Name
		{
			get
			{
				if (_path == "/")
				{
					return _path;
				}
				else
				{
					string[] pathItems = _path.Split('/');
					return pathItems[pathItems.Length - 1];
				}
			}
		}

		public override Dir422 Parent
		{
			get
			{
				if (_root)
				{
					return null;
				}

				return _parent;
			}
		}

		public void SetRoot(bool isRoot)
		{
			_root = isRoot;
		}

		public bool GetRoot()
		{
			return _root;
		}

		public override string GetPath()
		{
			return _path;
		}

		public override bool ContainsDir(string dirName, bool recursive)
		{
			if (!CheckValidName(dirName))
			{
				return false;
			}

			bool doesContain = false;

			foreach (Dir422 dir in _directories)
			{
				if (dir.Name == dirName)
				{
					doesContain = true;
					return doesContain;
				}
			}

			if (recursive)
			{
				foreach (Dir422 dir in _directories)
				{
					doesContain = dir.ContainsDir(dirName, true);

					if (doesContain == true)
					{
						return true;
					}
				}
			}

			return doesContain;
		}

		public override bool ContainsFile(string fileName, bool recursive)
		{
			if (!CheckValidName(fileName))
			{
				return false;
			}

			bool doesContain = false;

			foreach (File422 file in _files)
			{
				if (file.Name == fileName)
				{
					doesContain = true;
					return doesContain;
				}
			}

			if (recursive)
			{
				foreach (Dir422 dir in _directories)
				{
					doesContain = dir.ContainsFile(fileName, true);

					if (doesContain == true)
					{
						return true;
					}
				}
			}

			return doesContain;
		}

		public override Dir422 CreateDir(string dirName)
		{
			if (!CheckValidName(dirName))
			{
				return null;
			}

			Dir422 newDir = GetDir(dirName);

			if (newDir == null)
			{
				newDir = new MemFSDir(dirName, this);
				_directories.Add(newDir);
			}

			return newDir;
		}

		public override File422 CreateFile(string fileName)
		{
			if (!CheckValidName(fileName))
			{
				return null;
			}

			File422 newFile = GetFile(fileName);

			if (newFile == null)
			{
				newFile = new MemFSFile(fileName, this);
				_files.Add(newFile);
			}

			return newFile;
		}

		//Search for directory in list of directories
		public override Dir422 GetDir(string dirName)
		{
			if (!CheckValidName(dirName))
			{
				return null;
			}

			foreach (Dir422 dir in _directories)
			{
				if (dir.Name == dirName)
				{
					return dir;
				}
			}

			return null;
		}

		//Return list of directories in the current directory
		public override IList<Dir422> GetDirs()
		{
			return _directories;
		}

		//Search for file in list files
		public override File422 GetFile(string fileName)
		{
			//TODO: Check Path for invalid characters
			if (!CheckValidName(fileName))
			{
				return null;
			}

			foreach (File422 file in _files)
			{
				if (file.Name == fileName)
				{
					return file;
				}
			}

			return null;
		}

		//Return list of files in the current directory
		public override IList<File422> GetFiles()
		{
			return _files;
		}

		//Check for a valid filename
		public bool CheckValidName(string name)
		{
			if (name.Contains("/") || name == null || name.Contains(@"\"))
			{
				return false;
			}

			return true;
		}
	}

	/**
	 * Memory File System File
	 **/
	public class MemFSFile : File422
	{
		private string _path;
		private MemFSDir _parent;
		private ConcurrentDictionary<ObservableMemoryStream, ObservableMemoryStream> _writing;
		private ConcurrentDictionary<ObservableMemoryStream, ObservableMemoryStream> _reading;
		private MemoryStream _data;

		public MemFSFile(string path, MemFSDir parent)
		{
			if (parent != null && !parent.GetRoot())
			{
				_path = parent.GetPath() + "/" + path;
			}
			else if (parent != null && parent.GetRoot())
			{
				if (parent.GetPath().EndsWith("/"))
				{
					_path = parent.GetPath() + path;
				}
				else
				{
					_path = parent.GetPath() + "/" + path;
				}
			}
			else
			{
				_path = path;
			}

			_parent = parent;
			_reading = new ConcurrentDictionary<ObservableMemoryStream, ObservableMemoryStream>();
			_writing = new ConcurrentDictionary<ObservableMemoryStream, ObservableMemoryStream>();
			_data = new MemoryStream();
		}

		public override string Name
		{
			get
			{
				return Path.GetFileName(_path);
			}
		}

		public override Dir422 Parent
		{
			get
			{
				return _parent;
			}
		}

		public override string GetPath()
		{
			return _path;
		}

		//Opens a stream from only reading
		public override Stream OpenReadOnly()
		{
			//Make sure there are no writing streams open
			if (_writing.Count > 0)
			{
				return null;
			}

			//Open the stream for readonly and subscribe it to the event listener
			_data.Seek(0, SeekOrigin.Begin);
			ObservableMemoryStream readOnlyStream = new ObservableMemoryStream(false, _data.GetBuffer());
			readOnlyStream.StreamClosed += StreamWasClosed;

			//Add to list readonly only streams
			_reading.TryAdd(readOnlyStream, readOnlyStream);

			return readOnlyStream;
		}

		//Opens a stream from reading and writing
		public override Stream OpenReadWrite()
		{
			//Make sure there are no other streams open
			if (_writing.Count > 0 || _reading.Count > 0)
			{
				return null;
			}

			//Open the stream for readwrite and subscribe it to the event listener
			_data.Seek(0, SeekOrigin.Begin);
			ObservableMemoryStream readWriteStream = new ObservableMemoryStream(true, _data.GetBuffer());
			readWriteStream.StreamClosed += StreamWasClosed;

			//Add to list of writing streams
			_writing.TryAdd(readWriteStream, readWriteStream);

			return readWriteStream;
		}

		//Event handler that removes stream from respective list
		void StreamWasClosed(object sender, EventArgs e)
		{
			ObservableMemoryStream removed;

			//If the stream that was a writer
			if (_writing.ContainsKey(sender as ObservableMemoryStream))
			{
				(sender as ObservableMemoryStream).Seek(0, SeekOrigin.Begin);
				(sender as ObservableMemoryStream).CopyTo(_data);
				(sender as ObservableMemoryStream).Seek(0, SeekOrigin.Begin);
				_data.Seek(0, SeekOrigin.Begin);

				_writing.TryRemove(sender as ObservableMemoryStream, out removed);
			}
			//If the stream that was a reader
			else if (_reading.ContainsKey(sender as ObservableMemoryStream))
			{
				(sender as ObservableMemoryStream).Seek(0, SeekOrigin.Begin);
				(sender as ObservableMemoryStream).CopyTo(_data);
				(sender as ObservableMemoryStream).Seek(0, SeekOrigin.Begin);
				_data.Seek(0, SeekOrigin.Begin);

				_reading.TryRemove(sender as ObservableMemoryStream, out removed);
			}
		}
	}

	/**
	 * MemoryStream that notifies the file everytime an instance of the file is closed
	 **/
	public class ObservableMemoryStream : MemoryStream
	{
		private bool _writeable;
		public event EventHandler StreamClosed;

		//We need to set whether or not the stream can write
		public ObservableMemoryStream(bool canWrite, byte[] existingData)
		{
			_writeable = true;

			if (_writeable)
			{
				this.Write(existingData, 0, existingData.Length);
				this.Seek(0, SeekOrigin.Begin);
			}

			_writeable = canWrite;
		}

		//Override the CanWrite property to use the _writable member variable
		public override bool CanWrite
		{
			get
			{
				return _writeable;
			}
		}

		//Override the dispose method to call the OnStreamClosed notify method
		protected override void Dispose(bool disposing)
		{
			OnStreamClosed(EventArgs.Empty);
			base.Dispose(disposing);
		}

		//Called in the Close method to notify the file that the stream has closed
		public virtual void OnStreamClosed(EventArgs e)
		{
			EventHandler handler = StreamClosed;

			if (handler != null)
			{
				handler(this, e);
			}
		}
	}
}
