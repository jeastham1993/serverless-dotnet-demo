using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Aspire.Hosting.AWS.Lambda;
using Projects;

#pragma warning disable CA2252 // Opt in to preview features

var builder = DistributedApplication.CreateBuilder(args);

var dynamoDbLocal = builder.AddAWSDynamoDBLocal("DynamoDBProducts");

builder.Eventing.Subscribe<ResourceReadyEvent>(dynamoDbLocal.Resource, async (evnt, ct) =>
{
    // Configure DynamoDB service client to connect to DynamoDB local.
    var serviceUrl = dynamoDbLocal.Resource.GetEndpoint("http").Url;
    var ddbClient = new AmazonDynamoDBClient(new AmazonDynamoDBConfig { ServiceURL = serviceUrl });

    // Create the Accounts table.
    await ddbClient.CreateTableAsync(new CreateTableRequest
    {
        TableName = "Products",
        AttributeDefinitions = new List<AttributeDefinition>
        {
            new() { AttributeName = "id", AttributeType = "S" }
        },
        KeySchema = new List<KeySchemaElement>
        {
            new() { AttributeName = "id", KeyType = "HASH" }
        },
        BillingMode = BillingMode.PAY_PER_REQUEST
    });
});

var superSimpleFunction = builder.AddAWSLambdaFunction<SuperSimpleFunction>("SuperSimpleFunction",
    "SuperSimpleFunctionSuperSimpleFunction");

var getProductsFunction = builder.AddAWSLambdaFunction<GetProducts>("GetProducts",
        "GetProducts::GetProducts.Function::FunctionHandler")
    .WithReference(dynamoDbLocal)
    .WaitFor(dynamoDbLocal);

var getProductFunction = builder.AddAWSLambdaFunction<GetProduct>("GetProduct",
        "GetProduct::GetProduct.Function::FunctionHandler")
    .WithReference(dynamoDbLocal)
    .WaitFor(dynamoDbLocal);
var createProductFunction = builder.AddAWSLambdaFunction<PutProduct>("PutProduct",
        "PutProduct::PutProduct.Function::FunctionHandler")
    .WithReference(dynamoDbLocal)
    .WaitFor(dynamoDbLocal);

builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithReference(getProductsFunction, Method.Get, "/")
    .WithReference(getProductFunction, Method.Get, "/{productId}")
    .WithReference(createProductFunction, Method.Post, "/");

await builder.Build().RunAsync();