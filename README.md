# ObjectSerializer

Object serializer for networking other programming languages

This module is driven by reflection and optimizes networking.

```cs
var buffer = receiveData.Data;
// deserialize from byte buffer
var response = buffer.GetObjectFromByte<ResponseType>();

var request = new RequestType();
// serialize to byte buffer
var body = response.ToByteArray<RequestType>();

// Send to network!!
NetworkClient.Send(header,body);
```
