using Amazon.Lambda.Core;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CreateFunction;

public class Function
{
    public async Task<string> FunctionHandler(UserInfo request, ILambdaContext context)
    {
        var eventBusResponse = await PutEvent(request);
        context.Logger.LogDebug($"{eventBusResponse.FailedEntryCount}");
        if(eventBusResponse.FailedEntryCount != 0){
            return $"Event Message failed";
        }
        return $"Event Message has been sent";
    }
    private async Task<PutEventsResponse> PutEvent(UserInfo userInfo){
        PutEventsRequest events = new PutEventsRequest{
            Entries = new List<PutEventsRequestEntry>{
                new PutEventsRequestEntry {
                    Source = "event.source.createuser",
                    EventBusName =  Environment.GetEnvironmentVariable("UserEventBusArn"),
                    DetailType = "Create User",
                    Detail = JsonSerializer.Serialize(userInfo)
                }
            }
        };
        AmazonEventBridgeClient client = new AmazonEventBridgeClient();
       return await client.PutEventsAsync(events);
    } 
}
