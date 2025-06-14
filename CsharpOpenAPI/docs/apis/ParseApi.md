# Org.OpenAPITools.Api.ParseApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**ParseCommandEndpointApiV1ParsePost**](ParseApi.md#parsecommandendpointapiv1parsepost) | **POST** /api/v1/parse | Parse Command Endpoint |

<a id="parsecommandendpointapiv1parsepost"></a>
# **ParseCommandEndpointApiV1ParsePost**
> Object ParseCommandEndpointApiV1ParsePost (ParseCommandArgs parseCommandArgs)

Parse Command Endpoint

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using Org.OpenAPITools.Api;
using Org.OpenAPITools.Client;
using Org.OpenAPITools.Model;

namespace Example
{
    public class ParseCommandEndpointApiV1ParsePostExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            var apiInstance = new ParseApi(config);
            var parseCommandArgs = new ParseCommandArgs(); // ParseCommandArgs | 

            try
            {
                // Parse Command Endpoint
                Object result = apiInstance.ParseCommandEndpointApiV1ParsePost(parseCommandArgs);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ParseApi.ParseCommandEndpointApiV1ParsePost: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ParseCommandEndpointApiV1ParsePostWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Parse Command Endpoint
    ApiResponse<Object> response = apiInstance.ParseCommandEndpointApiV1ParsePostWithHttpInfo(parseCommandArgs);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ParseApi.ParseCommandEndpointApiV1ParsePostWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **parseCommandArgs** | [**ParseCommandArgs**](ParseCommandArgs.md) |  |  |

### Return type

**Object**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Successful Response |  -  |
| **422** | Validation Error |  -  |

[[Back to top]](#) [[Back to API list]](../../README.md#documentation-for-api-endpoints) [[Back to Model list]](../../README.md#documentation-for-models) [[Back to README]](../../README.md)

