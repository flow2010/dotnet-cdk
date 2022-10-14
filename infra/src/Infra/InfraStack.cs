using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.EventSchemas;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SQS;
using Constructs;
using System.IO;

namespace Infra
{
    public class InfraStack : Stack
    {
        internal InfraStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {

            Function createFunction = new Function(this, $"{id}-create-user", new FunctionProps
            {
                Runtime = Runtime.DOTNET_6,
                Code = Code.FromAsset("../app/CreateFunction/src/CreateFunction/bin/Debug/net6.0/"),
                Handler = "CreateFunction::CreateFunction.Function::FunctionHandler",
                Timeout = Duration.Seconds(120)
            });

            Function updateFunction = new Function(this, $"{id}-update-user", new FunctionProps
            {
                Runtime = Runtime.DOTNET_6,
                Code = Code.FromAsset("../app/ProcessFunction/src/ProcessFunction/bin/Debug/net6.0"),
                Handler = "ProcessFunction::ProcessFunction.Function::FunctionHandler",
                Timeout = Duration.Seconds(120)
            });

            ///Create EventBus
            Amazon.CDK.AWS.Events.EventBus bus = new Amazon.CDK.AWS.Events.EventBus(this, $"{id}-event-bus", new Amazon.CDK.AWS.Events.EventBusProps {
                    EventBusName = "UserEventBus"});    

            //Create custom eventbus registry
            var registry = new CfnRegistry(this, $"{id}-Registry", new CfnRegistryProps {
                        Description = "description",
                        RegistryName = "UserRegistry"                        
                    });  

            //Read file content        
            var schemaContent = File.ReadAllText("src/Infra/CreateUser.json");

            //Create custom Schema for eventbus events.
            var  schema = new CfnSchema(this, $"{id}-schema", new CfnSchemaProps {
                        Content = schemaContent,
                        RegistryName = "UserRegistry",
                        Type = "OpenApi3",                        
                        Description = "create users",
                        SchemaName = "UserSchema",                        
                    });

            //Add Event Rule
            Rule rule = new Rule(this, $"{id}-rule", new RuleProps {                                       
                    EventBus = bus,
                    EventPattern = new EventPattern {
                    Source = new [] { "event.source.createuser"},
                    DetailType =new [] {"Create User"},

            }});

            //Create DLQ for SQS
            Queue userDLQ = new Queue(this, $"{id}-user-dlq", new QueueProps
            {
                QueueName = "UserDLQ",
            });

            //Create Queue and setup DLQ.
            Queue userQueue = new Queue(this, $"{id}-user-queue", new QueueProps
            {
                QueueName = "UserQueue",
                VisibilityTimeout = Duration.Seconds(120),
                ReceiveMessageWaitTime = Duration.Seconds(20),
                DeadLetterQueue = new DeadLetterQueue{
                                    MaxReceiveCount = 10,
                                    Queue = userDLQ
                }
            });  

            //add rule to SQS as target        
            rule.AddTarget(new SqsQueue(userQueue));

            //Add SQS to Lambda event Source                 
            updateFunction.AddEventSource(new SqsEventSource(userQueue, new SqsEventSourceProps {
                                                           BatchSize = 10,  // default                                                                              
                                                           MaxBatchingWindow = Duration.Minutes(1)                                                                                                                                  
            }));
            
            createFunction.AddEnvironment("UserEventBusArn", bus.EventBusArn);

            //Grant  put permission to Lambda
            bus.GrantPutEventsTo(createFunction);            
        }
    }
}
