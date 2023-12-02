using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace bsn.HttpClientSync {
	public class SyncStreamContent: StreamContent {
		// HttpClientHandler.CreateResponseMessage() creates a StreamContent and does some processing on the headers.
		// The SerializeToStreamAsync method needs to be made sync, so this method uses the FormatterServices to skip constructor calls and copy the fields over.
		internal static SyncStreamContent FromStreamContent(StreamContent content) {
			if (content is SyncStreamContent syncContent) {
				return syncContent;
			}
			syncContent = ReflectionHelper<SyncStreamContent>.CreateUninitialized();
			ReflectionHelper<StreamContent>.CopyFields(content, syncContent);
			return syncContent;
		}

		public SyncStreamContent(Stream content): base(content) { }

		public SyncStreamContent(Stream content, int bufferSize): base(content, bufferSize) {}

		protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) {
			var contentTask = this.CreateContentReadStreamAsync();
			Debug.Assert(contentTask.IsCompleted); // StreamContent.CreateContentReadStreamAsync is not actually async
			// ReSharper disable once AsyncApostle.AsyncWait
			using (var content = contentTask.Result) {
				content.CopyTo(stream);
			}
			return Task.CompletedTask;
		}
	}
}
