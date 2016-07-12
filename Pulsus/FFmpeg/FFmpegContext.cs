using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace Pulsus.FFmpeg
{
	public unsafe class FFmpegContext : IDisposable
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int ReadStreamDelegate(IntPtr opaque, IntPtr buf, int buf_size);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int WriteStreamDelegate(IntPtr opaque, IntPtr buf, int buf_size);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate Int64 SeekStreamDelegate(IntPtr opaque, Int64 offset, int whence);

		public AVFormatContext* formatContext = null;
		public double framePts { get; private set; }

		AVIOContext* avioContext = null;
		SwrContext* swrContext = null;
		SwsContext* swsContext = null;
		AVPacket packet;
		AVFrame* frame = null;
		AVFrame* convertedFrame = null;
		int streamIndex = -1;
		AVMediaType type;

		sbyte* convertedData;
		int convertedSamples;
		int convertedBufferSize;
		int convertedMaxSize;
		int convertedLineSize;
		int targetFormat = 0;
		int targetSampleRate = 0;
		int targetChannels = 0;

		Stream stream;
		string file;
		sbyte* readBuffer;
		byte[] managedBuffer;

		int decodedFrames;
		int bufferedLength = 0;

		// prevents garbage collector from collecting delegates
		List<Delegate> delegateRefs = new List<Delegate>();

		private unsafe FFmpegContext(string file, FileMode mode, FileAccess access)
		{
			this.file = file;
			//stream = new FileStream(file, mode, access);
		}

		private unsafe FFmpegContext(Stream stream, string filename = null)
		{
			this.stream = stream;
			this.file = filename;
		}

		public static FFmpegContext Read(string file)
		{
			FFmpegContext context = new FFmpegContext(file, FileMode.Open, FileAccess.Read);
			context.OpenInput();

			return context;
		}

		public static FFmpegContext Read(Stream stream, string filename)
		{
			FFmpegContext context = new FFmpegContext(stream, filename);
			context.OpenInput();

			return context;
		}

		public static FFmpegContext Write(string file)
		{
			FFmpegContext context = new FFmpegContext(file, FileMode.Create, FileAccess.Write);

			return context;
		}

		private unsafe void OpenInput()
		{
			formatContext = ffmpeg.avformat_alloc_context();
			if (stream != null)
			{
				const int bufferSize = 8192;

				// with delegates and anonymous functions, we can capture
				// the local stream variable without passing it as a argument.

				ReadStreamDelegate readStream = (IntPtr opaque, IntPtr buf, int buf_size) =>
				{
					byte[] array = new byte[buf_size];

					int read = stream.Read(array, 0, buf_size);
					for (int i = 0; i < read; i++)
						Marshal.WriteByte(buf, i, array[i]);

					return read;
				};

				SeekStreamDelegate seekStream = (IntPtr opaque, Int64 offset, int whence) =>
				{
					if (whence == ffmpeg.AVSEEK_SIZE)
					{
						try
						{
							return stream.Length;
						}
						catch
						{
							return -1;
						}
					}

					if (!stream.CanSeek)
						return -1;

					stream.Seek(offset, (SeekOrigin)whence);
					return stream.Position;
				};

				// track the delegates, GC might remove them prematurely
				delegateRefs.Add(readStream);
				delegateRefs.Add(seekStream);

				// setup custom stream reader for ffmpeg to use with AVIO context
				readBuffer = (sbyte*)ffmpeg.av_malloc(bufferSize + ffmpeg.FF_INPUT_BUFFER_PADDING_SIZE);
				avioContext = FFmpegHelper.avio_alloc_context(readBuffer, bufferSize, 0, null,
					Marshal.GetFunctionPointerForDelegate(readStream), IntPtr.Zero,
					Marshal.GetFunctionPointerForDelegate(seekStream));

				formatContext->pb = avioContext;
				formatContext->flags |= ffmpeg.AVFMT_FLAG_CUSTOM_IO;
			}
			/*else
			{
				fixed (AVFormatContext** ptr = &formatContext)
					if (ffmpeg.avformat_open_input(ptr, file, null, null) != 0)
						throw new ApplicationException("Failed to open input stream: " + file);
			}*/
			
			int err = 0;
			fixed (AVFormatContext** ptr = &formatContext)
			{
				AVInputFormat* inputFormat = null;
				if ((err = ffmpeg.av_probe_input_buffer(formatContext->pb, &inputFormat, null, null, 0, 0)) != 0)
					throw new FFmpegException(err);

				if ((inputFormat->flags & ffmpeg.AVFMT_NOFILE) != 0)
				{
					// Input format (image2) doesn't support custom AVIOContext,
					// forcefully clear the flag and hope it works.
					inputFormat->flags &= ~ffmpeg.AVFMT_NOFILE;

					//ffmpeg.avio_close(formatContext->pb);
				}

				if ((err = ffmpeg.avformat_open_input(ptr, file, inputFormat, null)) != 0)
					throw new FFmpegException(err);
			}
			frame = ffmpeg.av_frame_alloc();
		}

		public int GetWidth()
		{
			AVCodecContext* codecContext = formatContext->streams[streamIndex]->codec;
			return codecContext->width;
		}

		public int GetHeight()
		{
			AVCodecContext* codecContext = formatContext->streams[streamIndex]->codec;
			return codecContext->height;
		}

		public double GetFrametime()
		{
			AVCodecContext* codecContext = formatContext->streams[streamIndex]->codec;
			return (double)codecContext->framerate.den / codecContext->framerate.num;
		}

		public double GetLength()
		{
			long duration = formatContext->streams[streamIndex]->duration;
			if ((ulong)duration == ffmpeg.AV_NOPTS_VALUE)
				return 0.0;
			AVRational time_base = formatContext->streams[streamIndex]->time_base;
			return (double)duration * time_base.num / time_base.den;
		}

		public void Dispose()
		{
			if (avioContext != null)
				ffmpeg.avio_close(avioContext);
			avioContext = null;

			if (formatContext != null)
			{
				if (streamIndex != -1 && formatContext->streams[streamIndex] != null)
					ffmpeg.avcodec_close(formatContext->streams[streamIndex]->codec);

				if (formatContext->oformat == null)
				{
					fixed (AVFormatContext** ptr = &formatContext)
						ffmpeg.avformat_close_input(ptr);

					ffmpeg.av_free(formatContext);
				}
				else
					ffmpeg.avformat_free_context(formatContext);

				formatContext = null;
			}

			if (swrContext != null)
			{
				ffmpeg.swr_close(swrContext);
				fixed (SwrContext** ptr = &swrContext)
					ffmpeg.swr_free(ptr);

				swrContext = null;
			}
			

			if (swsContext != null)
				ffmpeg.sws_freeContext(swsContext);
			swsContext = null;

			if (convertedFrame != null)
				fixed (AVFrame** framePtr = &convertedFrame)
					ffmpeg.av_frame_free(framePtr);
			convertedFrame = null;

			if (convertedData != null)
				ffmpeg.av_free(convertedData);
			convertedData = null;

			fixed (AVPacket* packetPtr = &packet)
				ffmpeg.av_free_packet(packetPtr);
			
			if (frame != null)
				fixed (AVFrame** framePtr = &frame)
					ffmpeg.av_frame_free(framePtr);
			frame = null;

			if (stream != null)		
				stream.Dispose();
			stream = null;

			delegateRefs.Clear();

			managedBuffer = null;
		}

		// read the media file ahead for proper stream info in cases
		// where the format has no header information.
		public unsafe void FindStreamInfo()
		{
			if (ffmpeg.avformat_find_stream_info(formatContext, null) != 0)
				throw new ApplicationException("Failed to find stream info");
		}

		public unsafe void SelectStream(AVMediaType type)
		{
			this.type = type;

			streamIndex = ffmpeg.av_find_best_stream(formatContext, type, -1, -1, null, 0);
			if (streamIndex < 0)
				throw new ApplicationException("No stream found for type " + type.ToString());

			SetupCodecContext(formatContext->streams[streamIndex]);
		}

		public unsafe void SelectStream(int index)
		{
			if (index >= formatContext->nb_streams)
				throw new ApplicationException("No stream found for index " + index.ToString());

			streamIndex = index;
			SetupCodecContext(formatContext->streams[streamIndex]);
		}

		private unsafe void SetupCodecContext(AVStream* stream)
		{
			AVCodec* decoder = ffmpeg.avcodec_find_decoder(stream->codec->codec_id);
			if (decoder == null)
				throw new ApplicationException("No decoder found for " + stream->codec->codec_id.ToString() + " : ");
	
			if (ffmpeg.avcodec_open2(stream->codec, decoder, null) < 0)
				throw new ApplicationException("Failed to open decoder for " + stream->codec->codec_id.ToString() + " : ");

			// there is one frame delay and the last frame needs to be
			// flushed out from ffmpeg's internal buffers
			//if ((decoder->capabilities & ffmpeg.CODEC_CAP_DELAY) != 0)
			//	hasDelay = true;
		}

		public unsafe void ConvertToFormat(AVSampleFormat sampleFormat, int sampleRate = -1, int channels = -1)
		{
			if (sampleRate == -1)
				sampleRate = GetSampleRate();
			if (channels == -1)
				channels = GetChannels();

			AVCodecContext* codecContext = formatContext->streams[streamIndex]->codec;
			if (codecContext->sample_fmt == sampleFormat &&
				codecContext->sample_rate == sampleRate &&
				codecContext->channels == channels)
			{
				return;
			}

			targetFormat = (int)sampleFormat;
			targetSampleRate = sampleRate;
			targetChannels = channels;
			
			int channelLayout = (int)ffmpeg.av_get_default_channel_layout(channels);

			swrContext = ffmpeg.swr_alloc();
			ffmpeg.av_opt_set_int(swrContext, "in_channel_layout", (int)codecContext->channel_layout, 0);
			ffmpeg.av_opt_set_int(swrContext, "out_channel_layout", channelLayout, 0);
			ffmpeg.av_opt_set_int(swrContext, "in_channel_count", codecContext->channels, 0);
			ffmpeg.av_opt_set_int(swrContext, "out_channel_count", channels, 0);
			ffmpeg.av_opt_set_int(swrContext, "in_sample_rate", codecContext->sample_rate, 0);
			ffmpeg.av_opt_set_int(swrContext, "out_sample_rate", sampleRate, 0);
			ffmpeg.av_opt_set_sample_fmt(swrContext, "in_sample_fmt", codecContext->sample_fmt, 0);
			ffmpeg.av_opt_set_sample_fmt(swrContext, "out_sample_fmt", sampleFormat, 0);

			if (ffmpeg.swr_init(swrContext) != 0)
				throw new ApplicationException("Failed init SwrContext: " + FFmpegHelper.logLastLine);
		}

		public unsafe void ConvertToFormat(AVPixelFormat pixelFormat)
		{
			targetFormat = (int)pixelFormat;

			AVCodecContext* codecContext = formatContext->streams[streamIndex]->codec;
			if (codecContext->pix_fmt == pixelFormat)
				return;

			convertedFrame = ffmpeg.av_frame_alloc();
			convertedFrame->format = targetFormat;
			convertedFrame->width = codecContext->width;
			convertedFrame->height = codecContext->height;
			convertedFrame->channels = 4;

			int swsFlags = 0;

			// fixes weird artifacts at the edgse for misaligned dimensions
			if (codecContext->width % 8 != 0 || codecContext->height % 8 != 0)
				swsFlags |= ffmpeg.SWS_ACCURATE_RND;

			swsContext = ffmpeg.sws_getContext(
				codecContext->width, codecContext->height, codecContext->pix_fmt,
				convertedFrame->width, convertedFrame->height, pixelFormat,
				swsFlags, null, null, null);

			// allocate buffers in frame
			if (ffmpeg.av_frame_get_buffer(convertedFrame, 1) != 0)
				throw new ApplicationException("Failed to allocate buffers for frame");
		}

		private unsafe bool ReadFrame()
		{
			fixed (AVPacket* packetPtr = &packet)
			{
				int length = 0;
				int decoded = 0;
				while ((length = ffmpeg.av_read_frame(formatContext, packetPtr)) >= 0)
				{
					if (packet.stream_index == streamIndex)
					{
						decoded = DecodeFrame();
						if (decoded != 0)
							break;
					}
					ffmpeg.av_packet_unref(packetPtr);
				}
				if (decoded == 0 /*&& hasDelay*/)
				{
					// flush out the last frame
					packet.size = 0;
					packet.data = null;
					decoded = DecodeFrame();
				}
				return decoded > 0;
			}
		}

		
		private unsafe int DecodeFrame()
		{
			int gotFrame = 0;
			int decodeLength = 0;
			fixed (AVPacket* packetPtr = &packet)
			{
				AVCodecContext* codecContext = formatContext->streams[packet.stream_index]->codec;
				// decode until full frame has been decoded
				do
				{
					if (type == AVMediaType.AVMEDIA_TYPE_VIDEO)
						decodeLength = ffmpeg.avcodec_decode_video2(codecContext, frame, &gotFrame, packetPtr);
					else if (type == AVMediaType.AVMEDIA_TYPE_AUDIO)
						decodeLength = ffmpeg.avcodec_decode_audio4(codecContext, frame, &gotFrame, packetPtr);

					if (decodeLength < 0)
					{
						System.Diagnostics.Debug.WriteLine("Failed to decode audio: " + FFmpegHelper.logLastLine);
						break;
					}

					if (gotFrame > 0)
					{
						decodedFrames++;

						AVRational timeBase = formatContext->streams[packet.stream_index]->time_base;

						long pts = ffmpeg.av_frame_get_best_effort_timestamp(frame);
						double currentFrameTime = pts > 0 ? ((double)pts * timeBase.num / timeBase.den) : 0.0;
						currentFrameTime -= formatContext->start_time / 1000000.0; // AV_TIME_BASE

						// current frame may be repeated multiple times
						currentFrameTime += frame->repeat_pict * (((double)timeBase.num / timeBase.den) * 0.5);
						
						framePts = currentFrameTime;

						// convert frame data to desired format
						if (swrContext != null || swsContext != null)
							ConvertFrame();

						return 1;
					}

					// offset to next frame in packet
					packet.size -= decodeLength;
					packet.data += decodeLength;

				} while (packet.size > 0);
			}
			// some images cannot be decoded in a single pass
			if (decodeLength > 0 && gotFrame == 0)
				return 0;

			return decodeLength;
		}

		private unsafe bool ConvertFrame()
		{
			if (type == AVMediaType.AVMEDIA_TYPE_VIDEO)
			{
				AVCodecContext* codecContext = formatContext->streams[packet.stream_index]->codec;
				if (ffmpeg.sws_scale(swsContext, &frame->data0, frame->linesize,
					0, codecContext->height, &convertedFrame->data0, convertedFrame->linesize) <= 0)
				{
					throw new ApplicationException("failed to convert frame: " + FFmpegHelper.logLastLine);
				}
			}
			else if (type == AVMediaType.AVMEDIA_TYPE_AUDIO)
			{
				AVFrame* inputFrame = frame;
				if (bufferedLength > 0)
				{
					// null frame flushes the remaining buffered data
					inputFrame = null;
				}

				convertedSamples = (int)ffmpeg.av_rescale_rnd(frame->nb_samples, targetSampleRate, frame->sample_rate, AVRounding.AV_ROUND_UP);
				if (convertedSamples > convertedMaxSize)
				{
					if (convertedData != null)
					{
						ffmpeg.av_free(convertedData);
						convertedData = null;
					}
					
					convertedMaxSize = convertedSamples;
					fixed (int* lineSize = &convertedLineSize)
						fixed (sbyte** data = &convertedData)
							ffmpeg.av_samples_alloc(data, lineSize, targetChannels, convertedMaxSize, (AVSampleFormat)targetFormat, 1);
				}
				int sampleCount;
				fixed (sbyte** data = &convertedData)
					sampleCount = ffmpeg.swr_convert(swrContext, data, convertedSamples, &frame->data0, frame->nb_samples);
				if (sampleCount < 0)
					throw new ApplicationException("failed to convert frame: " + FFmpegHelper.logLastLine);

				fixed (int* lineSize = &convertedLineSize)
					convertedBufferSize = ffmpeg.av_samples_get_buffer_size(lineSize, targetChannels, sampleCount, (AVSampleFormat)targetFormat, 1);
			}

			return true;
		}

		// returns true if there is data in next frame
		public unsafe bool ReadNextFrame()
		{
			// more data in previous frame, continue converting the buffered data
			if (bufferedLength > 0)
				return ConvertFrame();

			bool ret = ReadFrame();
			fixed (AVPacket* packetPtr = &packet)
				ffmpeg.av_free_packet(packetPtr);
			return ret;
		}

		public unsafe byte[] GetFrameData()
		{
			sbyte* buffer = null;
			int bufferSize = 0;

			// point to the correct frame where our output is
			AVFrame* dataFrame = frame;
			if (swsContext != null)
				dataFrame = convertedFrame;
	
			buffer = dataFrame->data0;
			if (type == AVMediaType.AVMEDIA_TYPE_VIDEO)
			{
				bufferSize = ffmpeg.av_image_get_buffer_size((AVPixelFormat)dataFrame->format,
					dataFrame->width, dataFrame->height, 1);
			}
			else if (type == AVMediaType.AVMEDIA_TYPE_AUDIO)
			{
				bufferSize = ffmpeg.av_samples_get_buffer_size(null, GetChannels(),
					dataFrame->nb_samples, (AVSampleFormat)dataFrame->format, 1);
			}	

			if (swrContext != null)
			{
				buffer = convertedData;
				bufferSize = convertedBufferSize;
			}

			if (bufferSize < 0)
				throw new ApplicationException("buffer size negative: " + FFmpegHelper.logLastLine);

			// allocate and copy the data to managed memory
			if (managedBuffer == null || managedBuffer.Length != bufferSize)
				managedBuffer = new byte[bufferSize];

			Marshal.Copy((IntPtr)buffer, managedBuffer, 0, bufferSize);

			return managedBuffer;
		}
	
		public int GetChannels()
		{
			AVCodecContext* codecContext = formatContext->streams[streamIndex]->codec;
			return codecContext->channels;
		}

		public int GetSampleRate()
		{
			AVCodecContext* codecContext = formatContext->streams[streamIndex]->codec;
			return codecContext->sample_rate;
		}

		// returns bytes per sample for audio
		public unsafe int GetBytesPerSample()
		{
			return ffmpeg.av_get_bytes_per_sample((AVSampleFormat)targetFormat);
		}

		// returns sample count for audio
		public unsafe int GetTotalSampleCount()
		{
			AVStream* stream = formatContext->streams[streamIndex];

			int bytesPerSample = GetBytesPerSample();
			long duration = stream->duration;
			int num = stream->time_base.num;
			int den = stream->time_base.den;
			int sampleRate = stream->codec->sample_rate;
			int channels = stream->codec->channels;

			int totalSampleCount = (int)((duration * sampleRate * channels * bytesPerSample * num) / den);
			return totalSampleCount;
		}

		// set output format from file extension
		public unsafe void SetOutputFormat(AVCodecID codecId, int width, int height, AVPixelFormat pixelFormat, int compression)
		{
			type = AVMediaType.AVMEDIA_TYPE_VIDEO;

			AVOutputFormat* outputFormat = ffmpeg.av_guess_format(null, file, null);
			if (outputFormat == null)
				throw new ApplicationException("Failed to guess output format for extension " + file);

			string name = Marshal.PtrToStringAnsi((IntPtr)outputFormat->name);
			string lname = Marshal.PtrToStringAnsi((IntPtr)outputFormat->long_name);
			string extlist = Marshal.PtrToStringAnsi((IntPtr)outputFormat->extensions);

			if (codecId == AVCodecID.AV_CODEC_ID_NONE)
				codecId = ffmpeg.av_guess_codec(outputFormat, null, file, null, AVMediaType.AVMEDIA_TYPE_VIDEO);

			outputFormat->video_codec = codecId;
			SetupOutput(outputFormat, codecId, width, height, pixelFormat, compression);
		}

		private unsafe void SetupOutput(AVOutputFormat* outputFormat, AVCodecID codecId, int width, int height, AVPixelFormat pixelFormat, int compression)
		{
			fixed (AVFormatContext** ptr = &formatContext)
				if (ffmpeg.avformat_alloc_output_context2(ptr, outputFormat, null, file) < 0)
					throw new ApplicationException("Failed to allocate format context");

			formatContext->oformat = outputFormat;

			AVCodec* encoder = ffmpeg.avcodec_find_encoder(codecId);
			if (encoder == null)
				throw new ApplicationException("Failed to load encoder for " + outputFormat->video_codec.ToString());

			AVStream* videoStream = ffmpeg.avformat_new_stream(formatContext, encoder);
			if (videoStream == null)
				throw new ApplicationException("Failed to create new stream");

			videoStream->id = (int)formatContext->nb_streams-1;
			streamIndex = videoStream->id;

			AVCodecContext* codecContext = videoStream->codec;
			codecContext->width = width;
			codecContext->height = height;
			codecContext->compression_level = compression;
			codecContext->pix_fmt = pixelFormat;

			for (int i = 0; encoder->pix_fmts[i] != AVPixelFormat.AV_PIX_FMT_NONE; i++)
			{
				if (encoder->pix_fmts[i] == AVPixelFormat.AV_PIX_FMT_RGBA && pixelFormat == AVPixelFormat.AV_PIX_FMT_BGRA)
				{
					ConvertToFormat(AVPixelFormat.AV_PIX_FMT_RGBA);
					codecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_RGBA;
					break;
				}
			}

			if (ffmpeg.avcodec_open2(codecContext, encoder, null) < 0)
				throw new ApplicationException("Failed to open codec for " + outputFormat->video_codec.ToString());

			videoStream->codec->codec_tag = 0;
			if ((formatContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
				videoStream->codec->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

			if ((formatContext->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
			{
				fixed (AVIOContext** ptr = &avioContext)
					if (ffmpeg.avio_open(ptr, file, ffmpeg.AVIO_FLAG_WRITE) < 0)
						throw new ApplicationException("Failed to open output file for writing");

				formatContext->pb = avioContext;
			}

			frame = ffmpeg.av_frame_alloc();
			frame->width = codecContext->width;
			frame->height = codecContext->height;
			frame->format = (int)codecContext->pix_fmt;

			if (ffmpeg.av_frame_get_buffer(frame, 1) != 0)
				throw new ApplicationException("Failed to allocate buffers for frame");
		}

		public unsafe void SetOutputFormat(AVCodecID codecId, int sampleRate, int sampleCount, AVSampleFormat sampleFormat)
		{
			type = AVMediaType.AVMEDIA_TYPE_AUDIO;

			AVOutputFormat* outputFormat = ffmpeg.av_guess_format(null, file, null);
			if (outputFormat == null)
				throw new ApplicationException("Failed to guess output format for extension " + file);

			string name = Marshal.PtrToStringAnsi((IntPtr)outputFormat->name);
			string lname = Marshal.PtrToStringAnsi((IntPtr)outputFormat->long_name);
			string extlist = Marshal.PtrToStringAnsi((IntPtr)outputFormat->extensions);

			if (codecId == AVCodecID.AV_CODEC_ID_NONE)
				codecId = ffmpeg.av_guess_codec(outputFormat, null, file, null, AVMediaType.AVMEDIA_TYPE_AUDIO);

			outputFormat->audio_codec = codecId;
			SetupOutput(outputFormat, codecId, sampleRate, sampleCount, sampleFormat);
		}

		private void SetupOutput(AVOutputFormat* outputFormat, AVCodecID codecId, int sampleRate, int sampleCount, AVSampleFormat sampleFormat)
		{
			fixed (AVFormatContext** ptr = &formatContext)
				if (ffmpeg.avformat_alloc_output_context2(ptr, outputFormat, null, file) < 0)
					throw new ApplicationException("Failed to allocate format context");

			AVCodec* encoder = ffmpeg.avcodec_find_encoder(codecId);
			if (encoder == null)
				throw new ApplicationException("Failed to load encoder for " + outputFormat->video_codec.ToString());

			AVStream* audioStream = ffmpeg.avformat_new_stream(formatContext, encoder);
			if (audioStream == null)
				throw new ApplicationException("Failed to create new stream");

			audioStream->id = (int)formatContext->nb_streams-1;

			AVCodecContext* codecContext = audioStream->codec;
			codecContext->sample_rate = sampleRate;
			codecContext->channels = 2;
			//codecContext->bit_rate = 64000;
			//codecContext->compression_level = 10;
			codecContext->channel_layout = ffmpeg.AV_CH_LAYOUT_STEREO;
			//codecContext->compression_level = compression;
			codecContext->sample_fmt = sampleFormat;
			
			if (ffmpeg.avcodec_open2(codecContext, encoder, null) < 0)
				throw new ApplicationException("Failed to open codec for " + outputFormat->video_codec.ToString());
			
			codecContext->codec_tag = 0;
			if ((formatContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
				codecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

			if ((formatContext->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
			{
				fixed (AVIOContext** ptr = &avioContext)
					if (ffmpeg.avio_open(ptr, file, ffmpeg.AVIO_FLAG_WRITE) < 0)
						throw new ApplicationException("Failed to open output file for writing");
				
				formatContext->pb = avioContext;
			}

			audioStream->time_base = new AVRational() { num = 1, den = sampleRate };

			frame = ffmpeg.av_frame_alloc();
			frame->sample_rate = codecContext->sample_rate;
			frame->channels = codecContext->channels;
			frame->format = (int)codecContext->sample_fmt;
			frame->channel_layout = codecContext->channel_layout;
			frame->nb_samples = sampleCount;

			if (ffmpeg.av_frame_get_buffer(frame, 1) != 0)
				throw new ApplicationException("Failed to allocate buffers for frame");
		}

		public unsafe void WriteHeader()
		{
			if (ffmpeg.avformat_write_header(formatContext, null) < 0)
				throw new ApplicationException("Failed to write header");
		}

		public void WriteFrame(byte[] data)
		{
			Marshal.Copy(data, 0, (IntPtr)frame->data0, data.Length);
	
			fixed (AVPacket* ptr = &packet)
			{
				ptr->data = null;
				ptr->size = 0;

				AVFrame* inputFrame = frame;
				if (convertedFrame != null)
				{
					if (!ConvertFrame())
						throw new ApplicationException("Failed to convert frame");
					inputFrame = convertedFrame;
				}

				int gotOutput = 0;
				AVCodecContext* context = formatContext->streams[ptr->stream_index]->codec;
				if (type == AVMediaType.AVMEDIA_TYPE_VIDEO)
				{
					if (ffmpeg.avcodec_encode_video2(context, ptr, inputFrame, &gotOutput) < 0)
						throw new ApplicationException("Failed to encode video");
				}
				else if (type == AVMediaType.AVMEDIA_TYPE_AUDIO)
				{
					if (ffmpeg.avcodec_encode_audio2(context, ptr, inputFrame, &gotOutput) < 0)
						throw new ApplicationException("Failed to encode audio");
				}

				if (gotOutput > 0)
				{
					if (ffmpeg.av_interleaved_write_frame(formatContext, ptr) < 0)
						throw new ApplicationException("Failed av_interleaved_write_frame");
				}
				else
					throw new ApplicationException("No output from avcodec_encode_video2");
			}
		}
	}
}
