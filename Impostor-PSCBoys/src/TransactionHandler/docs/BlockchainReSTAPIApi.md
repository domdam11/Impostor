# IO.Swagger.Api.BlockchainReSTAPIApi

All URIs are relative to *http://localhost:8080*

Method | HTTP request | Description
------------- | ------------- | -------------
[**AddPlayer**](BlockchainReSTAPIApi.md#addplayer) | **POST** /api/addPlayer/{ID} | Aggiungi un giocatore
[**ChangeState**](BlockchainReSTAPIApi.md#changestate) | **POST** /api/changeState/{ID} | Cambia stato di un asset
[**CreateAsset**](BlockchainReSTAPIApi.md#createasset) | **POST** /api/createAsset | Crea un asset
[**CreateEvent**](BlockchainReSTAPIApi.md#createevent) | **POST** /api/createEvent/{ID} | Crea un evento
[**GetClientID**](BlockchainReSTAPIApi.md#getclientid) | **GET** /api/clientID | Dettagli utente
[**Hello**](BlockchainReSTAPIApi.md#hello) | **GET** /api/hello | 
[**ReadAsset**](BlockchainReSTAPIApi.md#readasset) | **GET** /api/readAsset/{ID} | Leggi asset
[**ReadEvent**](BlockchainReSTAPIApi.md#readevent) | **GET** /api/readEvent/{ID} | Leggi evento
[**RemovePlayer**](BlockchainReSTAPIApi.md#removeplayer) | **POST** /api/removePlayer/{ID} | Rimuovi un giocatore
[**UpdateDescription**](BlockchainReSTAPIApi.md#updatedescription) | **POST** /api/updateDescription/{ID} | Aggiorna la descrizione dell&#x27;asset

<a name="addplayer"></a>
# **AddPlayer**
> string AddPlayer (PlayerDTO body, string ID)

Aggiungi un giocatore

Aggiungi un giocatore di una sessione di gioco quando è in preparazione.

### Example
```csharp
using System;
using System.Diagnostics;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace Example
{
    public class AddPlayerExample
    {
        public void main()
        {
            var apiInstance = new BlockchainReSTAPIApi();
            var body = new PlayerDTO(); // PlayerDTO | 
            var ID = ID_example;  // string | Identificativo dell'asset a cui aggiungere un giocatore

            try
            {
                // Aggiungi un giocatore
                string result = apiInstance.AddPlayer(body, ID);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling BlockchainReSTAPIApi.AddPlayer: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **body** | [**PlayerDTO**](PlayerDTO.md)|  | 
 **ID** | **string**| Identificativo dell&#x27;asset a cui aggiungere un giocatore | 

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: */*

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)
<a name="changestate"></a>
# **ChangeState**
> string ChangeState (AssetDTO body, string ID)

Cambia stato di un asset

Cambia lo stato di una sessione di gioco.

### Example
```csharp
using System;
using System.Diagnostics;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace Example
{
    public class ChangeStateExample
    {
        public void main()
        {
            var apiInstance = new BlockchainReSTAPIApi();
            var body = new AssetDTO(); // AssetDTO | 
            var ID = ID_example;  // string | Identificativo dell'asset di cui cambiare lo stato

            try
            {
                // Cambia stato di un asset
                string result = apiInstance.ChangeState(body, ID);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling BlockchainReSTAPIApi.ChangeState: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **body** | [**AssetDTO**](AssetDTO.md)|  | 
 **ID** | **string**| Identificativo dell&#x27;asset di cui cambiare lo stato | 

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: */*

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)
<a name="createasset"></a>
# **CreateAsset**
> string CreateAsset (AssetDTO body)

Crea un asset

Crea un nuovo asset che traccia una sessione di gioco.

### Example
```csharp
using System;
using System.Diagnostics;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace Example
{
    public class CreateAssetExample
    {
        public void main()
        {
            var apiInstance = new BlockchainReSTAPIApi();
            var body = new AssetDTO(); // AssetDTO | 

            try
            {
                // Crea un asset
                string result = apiInstance.CreateAsset(body);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling BlockchainReSTAPIApi.CreateAsset: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **body** | [**AssetDTO**](AssetDTO.md)|  | 

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: */*

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)
<a name="createevent"></a>
# **CreateEvent**
> string CreateEvent (EventDTO body, string ID)

Crea un evento

Aggiungi un evento per una sessione di gioco.

### Example
```csharp
using System;
using System.Diagnostics;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace Example
{
    public class CreateEventExample
    {
        public void main()
        {
            var apiInstance = new BlockchainReSTAPIApi();
            var body = new EventDTO(); // EventDTO | 
            var ID = ID_example;  // string | Identificativo dell'asset su cui registrare l'evento

            try
            {
                // Crea un evento
                string result = apiInstance.CreateEvent(body, ID);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling BlockchainReSTAPIApi.CreateEvent: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **body** | [**EventDTO**](EventDTO.md)|  | 
 **ID** | **string**| Identificativo dell&#x27;asset su cui registrare l&#x27;evento | 

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: */*

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)
<a name="getclientid"></a>
# **GetClientID**
> string GetClientID ()

Dettagli utente

Recupera i dettagli dell'utente che interagisce con l'API.

### Example
```csharp
using System;
using System.Diagnostics;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace Example
{
    public class GetClientIDExample
    {
        public void main()
        {
            var apiInstance = new BlockchainReSTAPIApi();

            try
            {
                // Dettagli utente
                string result = apiInstance.GetClientID();
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling BlockchainReSTAPIApi.GetClientID: " + e.Message );
            }
        }
    }
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: */*

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)
<a name="hello"></a>
# **Hello**
> string Hello ()



### Example
```csharp
using System;
using System.Diagnostics;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace Example
{
    public class HelloExample
    {
        public void main()
        {
            var apiInstance = new BlockchainReSTAPIApi();

            try
            {
                string result = apiInstance.Hello();
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling BlockchainReSTAPIApi.Hello: " + e.Message );
            }
        }
    }
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: */*

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)
<a name="readasset"></a>
# **ReadAsset**
> string ReadAsset (string ID)

Leggi asset

Recupera tutti i dettagli di una sessione di gioco.

### Example
```csharp
using System;
using System.Diagnostics;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace Example
{
    public class ReadAssetExample
    {
        public void main()
        {
            var apiInstance = new BlockchainReSTAPIApi();
            var ID = ID_example;  // string | Identificativo dell'asset da leggere

            try
            {
                // Leggi asset
                string result = apiInstance.ReadAsset(ID);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling BlockchainReSTAPIApi.ReadAsset: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **ID** | **string**| Identificativo dell&#x27;asset da leggere | 

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: */*

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)
<a name="readevent"></a>
# **ReadEvent**
> string ReadEvent (string ID)

Leggi evento

Recupera tutti i dettagli degli eventi inviati dall'utente.

### Example
```csharp
using System;
using System.Diagnostics;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace Example
{
    public class ReadEventExample
    {
        public void main()
        {
            var apiInstance = new BlockchainReSTAPIApi();
            var ID = ID_example;  // string | Identificativo dell'asset da cui leggere gli eventi

            try
            {
                // Leggi evento
                string result = apiInstance.ReadEvent(ID);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling BlockchainReSTAPIApi.ReadEvent: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **ID** | **string**| Identificativo dell&#x27;asset da cui leggere gli eventi | 

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: */*

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)
<a name="removeplayer"></a>
# **RemovePlayer**
> string RemovePlayer (PlayerDTO body, string ID)

Rimuovi un giocatore

Rimuovi un giocatore da una sessione di gioco in corso.

### Example
```csharp
using System;
using System.Diagnostics;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace Example
{
    public class RemovePlayerExample
    {
        public void main()
        {
            var apiInstance = new BlockchainReSTAPIApi();
            var body = new PlayerDTO(); // PlayerDTO | 
            var ID = ID_example;  // string | Identificativo dell'asset da cui rimuovere un giocatore

            try
            {
                // Rimuovi un giocatore
                string result = apiInstance.RemovePlayer(body, ID);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling BlockchainReSTAPIApi.RemovePlayer: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **body** | [**PlayerDTO**](PlayerDTO.md)|  | 
 **ID** | **string**| Identificativo dell&#x27;asset da cui rimuovere un giocatore | 

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: */*

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)
<a name="updatedescription"></a>
# **UpdateDescription**
> string UpdateDescription (AssetDTO body, string ID)

Aggiorna la descrizione dell'asset

Aggiorna la descrizione di un asset di una sessione di gioco.

### Example
```csharp
using System;
using System.Diagnostics;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace Example
{
    public class UpdateDescriptionExample
    {
        public void main()
        {
            var apiInstance = new BlockchainReSTAPIApi();
            var body = new AssetDTO(); // AssetDTO | 
            var ID = ID_example;  // string | Identificativo dell'asset di cui aggiornare la descrizione

            try
            {
                // Aggiorna la descrizione dell'asset
                string result = apiInstance.UpdateDescription(body, ID);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling BlockchainReSTAPIApi.UpdateDescription: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **body** | [**AssetDTO**](AssetDTO.md)|  | 
 **ID** | **string**| Identificativo dell&#x27;asset di cui aggiornare la descrizione | 

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: */*

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)
