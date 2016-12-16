using System;
using System.IO;

namespace HTTPWebServer
{
	public class ConcatStream : Stream
	{
		private Stream First;
		private Stream Second;

		private long StreamLength;
		private long StreamPosition;
		private long SecondPosition;

		private bool FirstConstructor;

		/**
		 * First constructor specified by Evan. Doesn't take a length value.
		 **/
		public ConcatStream(Stream first, Stream second)
		{
			if (!first.CanSeek)
			{
				throw new ArgumentException();
			}
			//Do both support reading? Set length
			if (first.CanSeek && second.CanSeek)
			{
				StreamLength = first.Length + second.Length;

				StreamPosition = 0;
				SecondPosition = 0;
				first.Position = 0;
				second.Position = 0;
			}
			//The second doesn't support seeking, we assume it's at postion 0
			//We still need to set the StreamPostion and the first position to 0
			else
			{
				StreamPosition = 0;
				SecondPosition = 0;
				first.Position = 0;
			}

			First = first;
			Second = second;

			FirstConstructor = true;
		}

		/**
 		* Second constructor specified by Evan. Takes a length value.
 		**/
		public ConcatStream(Stream first, Stream second, long fixedLength)
		{
			StreamLength = fixedLength;

			if (!first.CanSeek)
			{
				throw new ArgumentException();
			}
			//Do both support reading? Set positions
			if (first.CanSeek && second.CanSeek)
			{
				StreamPosition = 0;
				SecondPosition = 0;
				first.Position = 0;
				second.Position = 0;
			}
			//The second doesn't support seeking, we assume it's at postion 0
			//We still need to set the StreamPostion and the first position to 0
			else
			{
				StreamPosition = 0;
				SecondPosition = 0;
				first.Position = 0;
			}

			First = first;
			Second = second;

			FirstConstructor = false;
		}

		/**
		 * Returns a bool if the stream can read
		 **/
		public override bool CanRead
		{
			get
			{
				if (First.CanRead && Second.CanRead)
				{
					return true;
				}

				return false;
			}
		}

		/**
 		* Returns a bool if the stream can seek
 		**/
		public override bool CanSeek
		{
			get
			{
				if (First.CanSeek && Second.CanSeek)
				{
					return true;
				}

				return false;
			}
		}

		/**
		 * Returns a bool if the stream can write
		 **/
		public override bool CanWrite
		{
			get
			{
				if (First.CanWrite && Second.CanWrite)
				{
					return true;
				}

				return false;
			}
		}

		/**
 		 * Returns a bool if the stream can read
   		 **/
		public override long Length
		{
			get
			{
				if (FirstConstructor)
				{
					if (First.CanSeek && Second.CanSeek)
					{
						return StreamLength;
					}
					else
					{
						return -1;
					}
				}
				else
				{
					return StreamLength;
				}
			}
		}

		/**
		 * If we can seek, we can set the length
		 **/
		public override void SetLength(long value)
		{
			if (CanSeek)
			{
				StreamLength = value;
			}
		}

		/**
		 * Getter and the setter position
		 **/
		public override long Position
		{
			get
			{
				return StreamPosition;
			}
			set
			{
				if (CanSeek)
				{
					if (value < 0)
					{
						value = 0;
					}

					this.StreamPosition = value;

					//We need to move the first position to end, and the second
					//to the remainder of the offset
					if (value > this.First.Length)
					{
						this.First.Position = this.First.Length;

						this.Second.Position = value - this.First.Length;
						SecondPosition = value;
					}
					//Set first to the offset and the second to the
					//beginning
					else
					{
						this.First.Position = value;

						this.Second.Position = 0;
						SecondPosition = 0;
					}
				}
				else
				{
					throw new NotSupportedException();
				}

			}
		}

		/**
		 * Flushes both streams
		 **/
		public override void Flush()
		{
			//Call the base class methods
			First.Flush();
			Second.Flush();
		}

		/**
		 * Reads from streams
		 **/
		public override int Read(byte[] buffer, int offset, int count)
		{
			int bytesReadIn = 0;

			if (CanRead)
			{
				//We need to set the count if the count is greater than the stream length
				if (this.Length != -1 && count > (int)(this.Length - this.StreamPosition))
				{
					count = (int)(this.Length - this.StreamPosition);
				}

				//Make sure we aren't going to read outside the range of the buffer
				if (count + offset > buffer.Length)
				{
					count = buffer.Length - offset;
				}

				//Read from Second stream only
				if (First.Length < StreamPosition)
				{
					bytesReadIn = this.Second.Read(buffer, offset, count);
					this.StreamPosition += bytesReadIn;
					SecondPosition += bytesReadIn;
				}
				else
				{
					//Read from First stream only
					if (this.StreamPosition + count < this.First.Length)
					{
						bytesReadIn = this.First.Read(buffer, offset, count);
						this.StreamPosition += bytesReadIn;
					}
					//Read from both streams
					else
					{
						//Set number of bytes to read from the first stream
						int readFromFirst = (int)(this.First.Length - this.StreamPosition);
						bytesReadIn = this.First.Read(buffer, offset, readFromFirst);

						//Get the remaining bytes we need to read from the second stream
						if (count - readFromFirst > 0)
						{
							int readFromSecond = this.Second.Read(buffer, offset + bytesReadIn, count - bytesReadIn);
							bytesReadIn += readFromSecond;
							SecondPosition += readFromSecond;
						}

						this.StreamPosition += bytesReadIn;
					}
				}
			}

			return bytesReadIn;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (this.CanSeek)
			{
				//Case where we set the origin from the beginning
				if (origin == SeekOrigin.Begin)
				{
					this.StreamPosition = offset;

					//We need to move the first position to end, and the second
					//to the remainder of the offset
					if (offset > this.First.Length)
					{
						this.First.Position = First.Length;

						this.Second.Position = offset - this.First.Length;
						SecondPosition = offset - this.First.Length;
					}
					//Set first to the offset and the second to the
					//beginning
					else
					{
						this.First.Position = offset;
						this.Second.Position = 0;
						SecondPosition = 0;
					}
				}
				//Case where the origin is the current position
				else if (origin == SeekOrigin.Current)
				{
					//We need to move the first position to the end of the first stream,
					//and the second to the remainder of the offset
					if (this.StreamPosition + offset > this.First.Length)
					{
						this.First.Position = this.First.Length;

						this.Second.Position += offset - this.First.Length;
						SecondPosition += offset- this.First.Length;
					}
					//We just need to set the first position to offset
					//and the second position to 0
					else
					{
						this.First.Position = this.StreamPosition + offset;
						this.Second.Position = 0;
						SecondPosition = 0;
					}

					this.StreamPosition += offset;
				}
				//Case where we are at the end of the stream
				else
				{
					//If the offset is negative and the abs value is greater than the stream
					//length set it to 0
					if (Math.Abs(offset) > this.StreamLength)
					{
						this.StreamPosition = 0;
						this.First.Position = 0;
						this.Second.Position = 0;
						SecondPosition = this.Second.Position;
					}
					//We're in the second stream
					else if (this.StreamLength + offset > this.First.Length)
					{
						this.StreamPosition = this.StreamLength + offset;
						this.First.Position = this.First.Length;
						this.Second.Position = this.StreamLength + offset - this.First.Length;
						SecondPosition = this.Second.Position;
					}
					//We're in the first stream
					else
					{
						this.StreamPosition = this.StreamLength + offset;
						this.First.Position = this.StreamLength + offset;
						this.Second.Position = 0;
						SecondPosition = this.Second.Position;
					}
				}
			}

			return this.StreamPosition;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (CanWrite)
			{
				if (offset + count > buffer.Length)
				{
					count = buffer.Length - offset;
				}

				//Write only to the Second stream
				if (this.StreamPosition > this.First.Length)
				{
					//We want to write to the second buffer, and we can seek, so we will move the position back
					//and overwrite the buffer
					if (this.Second.CanSeek)
					{
						this.Second.Seek(this.StreamPosition - this.First.Length, SeekOrigin.Begin);
						this.Second.Write(buffer, offset, count);
						this.StreamPosition += count;
						SecondPosition += this.Second.Position;
					}
					//In second stream and the second stream can't seek and the position is in the correct spots
					else if (this.SecondPosition == this.StreamLength - this.First.Length)
					{
						this.Second.Write(buffer, offset, count);
						this.StreamPosition += count;
						SecondPosition += count;
					}
					//We want to write to the buffer but we can't seek so throw an exception
					else
					{
						throw new NotSupportedException();
					}
				}
				else
				{
					//We only want to write to the first buffer
					if (count < this.First.Length - this.StreamPosition)
					{
						this.First.Write(buffer, offset, count);
						this.StreamPosition += count;
					}
					//We want to write to both buffers
					else
					{
						//How much do we want to write to the first buffer?
						int writeToFirst = (int)(this.First.Length - this.StreamPosition);
						this.First.Write(buffer, offset, writeToFirst);
						this.StreamPosition += writeToFirst;

						//We want to write to the second buffer, and we can seek, so we will move the position back
						//and overwrite the buffer
						if (this.Second.CanSeek)
						{
							this.Second.Seek(0, SeekOrigin.Begin);
							this.Second.Write(buffer, offset + writeToFirst, count - writeToFirst);
							this.StreamPosition += count - writeToFirst;
							SecondPosition = this.Second.Position;
						}
						//In second stream and the second stream can't seek and the position is in the correct spots
						else if (this.SecondPosition == 0)
						{
							this.Second.Write(buffer, offset + writeToFirst, count - writeToFirst);
							this.StreamPosition += count - writeToFirst;
							SecondPosition += count - writeToFirst;
						}
						else
						{
							throw new NotSupportedException();
						}
					}
				}

				//In the case we have two expandable streams, we will be able to expand the length if the
				//length is not -1.
				if (this.StreamLength != -1 && this.StreamPosition > this.StreamLength)
				{
					this.StreamLength += this.StreamPosition - this.StreamLength;
				}
			}
		}

	}
}
