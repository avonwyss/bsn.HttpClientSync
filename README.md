<!-- GRAPHIC -->

# bsn.HttpClientSync

.NET Library providing sync (blocking) calls on top of legacy `HttpClient` (before .NET 6.0).

This library should be able to avoid soft deadlocks and reduce threadpool starvation compared to calling `.Result` on tasks returned from async methods.

<!-- badges -->

---
## Links

- [GitHub issue "Sync API for HttpClient"](https://github.com/dotnet/runtime/issues/32125)

---
## Description

The library works by using a custom `HttpClientHandler` which processes the request in a sync fashion.

In order to be compatible with the original `HttpClient`, the library invokes the private methods `CreateAndPrepareWebRequest` and `CreateResponseMessage` of the `HttpClientHandler` class.
Furthermore, the response content is replaced by a custom `SyncStreamContent` which ensures that serializing/buffering will be done synchronously. 

### `HttpClient` instantiation for sync operations

```cs
var client = new HttpClient(new HttpClientSyncHandler());
```

### Invoke sync HTTP call

```cs
using var request = new HttpRequestMessage(HttpMethod.Get, "https://1.1.1.1/");
using var response = client.Send(request);
// use the response here
// response.Content.ReadAsStream()
// response.Content.ReadAsByte[]()
// response.Content.ReadAsString()
```

---
## FAQ
- **Q**: Is the sync functionality on par with the .NET 6 implementation?
    - A: No. Some methods may still use async code somewhere. However, the common code paths should run synchroneously and avoid common deadlock issues. Your mileage may vary.

---
## Source

[https://github.com/avonwyss/bsn.HttpClientSync](https://github.com/avonwyss/bsn.HttpClientSync)

---
## License

- **[MIT license](LICENSE.txt)**
- Copyright 2023 © Arsène von Wyss.
