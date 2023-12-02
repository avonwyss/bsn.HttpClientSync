using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using NLog;

namespace bsn.HttpClientSync {
	public class RetryHandler: DelegatingHandler {
		private class RetryableHttpContent: HttpContent {
			private MemoryStream bufferStream;

			public RetryableHttpContent(HttpContent original) {
				this.Original = original;
				foreach (var header in original.Headers) {
					this.Headers.Add(header.Key, header.Value);
				}
			}

			public HttpContent Original {
				get;
			}

			public async ValueTask AssertBufferStream() {
				if (this.bufferStream == null) {
					this.bufferStream = new MemoryStream();
					await this.Original.CopyToAsync(this.bufferStream, null).ConfigureAwait(false);
					this.bufferStream.Seek(0, SeekOrigin.Begin);
				}
			}

			protected override async Task<Stream> CreateContentReadStreamAsync() {
				await this.AssertBufferStream().ConfigureAwait(false);
				return this.bufferStream;
			}

			protected override void Dispose(bool disposing) {
				if (disposing) {
					this.bufferStream?.Dispose();
					this.Original.Dispose();
				}
				base.Dispose(disposing);
			}

			public void Reset() {
				this.bufferStream?.Seek(0, SeekOrigin.Begin);
			}

			protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context) {
				await this.AssertBufferStream().ConfigureAwait(false);
				await stream.WriteAsync(this.bufferStream.GetBuffer(), 0, (int)this.bufferStream.Length).ConfigureAwait(false);
			}

			protected override bool TryComputeLength(out long length) {
				if (this.bufferStream != null) {
					length = this.bufferStream.Length;
					return true;
				}
				length = 0;
				return false;
			}
		}

		private const int RetryUnavailableMilliseconds = 5000;
		private const int RetryTooManyRequestsMilliseconds = 1000;
		private const int RetryExceptionMilliseconds = 2000;

		private static readonly ILogger log = LogManager.GetCurrentClassLogger();

		private static bool IsNetworkError(Exception ex) {
			// Check if it's a network error
			if (ex is SocketException) {
				return true;
			}
			if (ex.InnerException != null) {
				return IsNetworkError(ex.InnerException);
			}
			return false;
		}

		private readonly int retryCount;

		public RetryHandler(int retryCount, HttpMessageHandler innerHandler): base(innerHandler) {
			this.retryCount = retryCount;
		}

		private ValueTask Delay(bool sync, int milliseconds, CancellationToken cancellationToken) {
			if (sync) {
				cancellationToken.WaitHandle.WaitOne(milliseconds);
				return default;
			}
			return new ValueTask(Task.Delay(milliseconds, cancellationToken));
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
			RetryableHttpContent retryableHttpContent = null;
			try {
				if (request.Content != null) {
					retryableHttpContent = new RetryableHttpContent(request.Content);
					request.Content = retryableHttpContent;
					await retryableHttpContent.AssertBufferStream().ConfigureAwait(false);
				}
				HttpResponseMessage response = null;
				var sync = request.IsSynchronous();
				var retries = this.retryCount;
				do {
					try {
						// base.SendAsync calls the inner handler
						response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
						if (retries-- <= 0) {
							if (!response.IsSuccessStatusCode) {
								log.Error("{method} {uri}: {status} {reason}, no more retries left", request.Method, request.RequestUri.GetLeftPart(UriPartial.Path), response.StatusCode, response.ReasonPhrase);
							}
							break;
						}
						switch (response.StatusCode) {
						case HttpStatusCode.ServiceUnavailable:
							// 503 Service Unavailable
							// Wait a bit and try again later
							log.Warn("{method} {uri}: Service Unavailable, {retries} left, waiting {ms}ms", request.Method, request.RequestUri.GetLeftPart(UriPartial.Path), retries, RetryUnavailableMilliseconds);
							await this.Delay(sync, RetryUnavailableMilliseconds, cancellationToken).ConfigureAwait(false);
							break;
						case (HttpStatusCode)429:
							// 429 Too many requests
							// Wait a bit and try again later
							log.Info("{method} {uri}: Too Many Requests, {retries} left, waiting {ms}ms", request.Method, request.RequestUri.GetLeftPart(UriPartial.Path), retries, RetryTooManyRequestsMilliseconds);
							await this.Delay(sync, RetryTooManyRequestsMilliseconds, cancellationToken).ConfigureAwait(false);
							break;
						default:
							// Not something we can retry, return the response as is
							return response;
						}
						response.Dispose();
						retryableHttpContent?.Reset();
					} catch (Exception ex) when (IsNetworkError(ex)) {
						// Network error
						// Wait a bit and try again later
						log.Warn(ex, "{method} {uri}: Error {message}, {retries} left, waiting {ms}ms", request.Method, request.RequestUri.GetLeftPart(UriPartial.Path), ex.Message, retries, RetryExceptionMilliseconds);
						await this.Delay(sync, RetryExceptionMilliseconds, cancellationToken).ConfigureAwait(false);
					}
				} while (!cancellationToken.IsCancellationRequested);
				cancellationToken.ThrowIfCancellationRequested();
				return response;
			} finally {
				request.Content = retryableHttpContent?.Original;
			}
		}
	}
}
