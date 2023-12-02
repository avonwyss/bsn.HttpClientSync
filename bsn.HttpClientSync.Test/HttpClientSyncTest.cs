using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace bsn.HttpClientSync {
	public class HttpClientSyncTest: IDisposable {
		private readonly ITestOutputHelper output;
		private readonly HttpClient client;

		public HttpClientSyncTest(ITestOutputHelper output) {
			this.output = output;
			TestOutputTarget.Configure(output);
			this.client = new HttpClient(new HttpClientSyncHandler());
		}

		[Fact]
		public void SyncRequest() {
			using var request = new HttpRequestMessage(HttpMethod.Get, "https://1.1.1.1/");
			using var response = this.client.Send(request);
			this.output.WriteLine($"{(int)response.StatusCode} {(string.IsNullOrEmpty(response.ReasonPhrase) ? response.StatusCode.ToString() : response.ReasonPhrase)}");
			this.output.WriteLine("");
			this.output.WriteLine(response.Content.ReadAsString());
		}

		public void Dispose() {
			this.client.Dispose();
		}
	}
}
