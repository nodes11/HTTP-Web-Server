using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HTTPWebServer
{
    public class ThreadPool : IDisposable
    {
		private BlockingCollection<TcpClient> ClientCollection;
        private bool disposed;
        private int ThreadCount;

        /**
         * Constructor for the class. Sets all memeber variables and intializes the
         * threads.
         **/
		public ThreadPool(int threadCount)
        {
			ClientCollection = new BlockingCollection<TcpClient>();
            disposed = false;

            //Check to make sure they gave us at least 1 thread. If not,
            //create 64 threads.
            if (threadCount <= 0)
            {
                ThreadCount = 64;
            }
            else
            {
                ThreadCount = threadCount;
            }

            //Initialize all threads
            for (int i = 0; i < threadCount; i++)
            {
				Thread thread = new Thread(WebServer.ThreadWork);
                thread.Start();
            }
        }

		/**
		 * Adds a new item to the blocking collection
		 **/
		public void AddItemToBlockingCollection(TcpClient Client)
		{
			ClientCollection.Add(Client);
		}

		/**
		 * Takes an item from the blocking collection and returns it
		 **/
		public TcpClient GetItemFromCollection()
		{
			TcpClient NewClient = null;

			try
			{
				NewClient = ClientCollection.Take();
			}
			catch (Exception e)
			{
				Console.Write("");
			}

			return NewClient;
		}

        /**
         * Used dispose format from MSDN
         *
         * https://msdn.microsoft.com/en-us/library/system.idisposable.dispose(v=vs.110).aspx
         **/
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool dispose)
        {
            if (disposed)
            {
                return;
            }

            if (dispose)
            {
                StopAllThreads();
            }

            disposed = true;
        }

        /**
         * Take the number of threads and add that many null values
         * to the blocking collection. The ThreadWork() method in the WebServer class
         * will get the null value when the Take() command is run and will
         * will receive null. This null will cause the Thread to terminate.
         *
         **/
        public void StopAllThreads()
        {
			for (int i = 0; i < ThreadCount; i++)
			{
				ClientCollection.Add(null);
            }
        }
    }
}
