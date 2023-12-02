using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace bsn.HttpClientSync {
	public static class HttpClientSyncExtensions {
		private const string SynchronousPropertyKey = "Synchronous";

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HttpResponseMessage Send(this HttpClient that, HttpRequestMessage request) {
			return that.Send(request, default, default);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HttpResponseMessage Send(this HttpClient that, HttpRequestMessage request, CancellationToken cancellationToken) {
			return that.Send(request, default, cancellationToken);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HttpResponseMessage Send(this HttpClient that, HttpRequestMessage request, HttpCompletionOption completionOption) {
			return that.Send(request, completionOption, default);
		}

		public static HttpResponseMessage Send(this HttpClient that, HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken) {
			request.Properties[SynchronousPropertyKey] = true;
			return that.SendAsync(request, completionOption, cancellationToken).GetAwaiter().GetResult();
		}

		public static Stream ReadAsStream(this HttpContent that, TransportContext context = default) {
			return that.ReadAsStreamAsync().GetAwaiter().GetResult();
		}

		public static string ReadAsString(this HttpContent that, TransportContext context = default) {
			var encoding = Encoding.UTF8;
			if (that.Headers.ContentType is { CharSet: { } }) {
				try {
					encoding = Encoding.GetEncoding(that.Headers.ContentType.CharSet);
				} catch (ArgumentException ex) {
					throw new InvalidOperationException("Invalid charset in HttpContent header", (Exception)ex);
				}
			}
			using var stream = that.ReadAsStream(context);
			using var reader = new StreamReader(stream, encoding, true);
			return reader.ReadToEnd();
		}

		public static byte[] ReadAsByteArray(this HttpContent that, TransportContext context = default) {
			using var stream = that.ReadAsStream(context);
			if (stream is MemoryStream memoryStream) {
				return memoryStream.ToArray();
			}
			using (memoryStream = new MemoryStream()) {
				stream.CopyTo(memoryStream);
				return memoryStream.ToArray();
			}
		}

		public static void CopyTo(this HttpContent that, Stream stream, TransportContext context = default, CancellationToken cancellationToken = default) {
			const int BufferSize = 81920;
			using (var source = that.ReadAsStream(context)) {
				var buffer = new byte[BufferSize];
				while (!cancellationToken.IsCancellationRequested) {
					var count = source.Read(buffer, 0, buffer.Length);
					if (count == 0 || cancellationToken.IsCancellationRequested) {
						break;
					}
					stream.Write(buffer, 0, count);
				}
			}
			cancellationToken.ThrowIfCancellationRequested();
		}

		public static bool IsSynchronous(this HttpRequestMessage that) {
			return that.Properties.TryGetValue(SynchronousPropertyKey, out var sync) && true.Equals(sync);
		}
	}
}
